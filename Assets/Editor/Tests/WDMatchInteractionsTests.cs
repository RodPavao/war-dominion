#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class WDMatchInteractionsTests
{
    // 01. Fixture usa GameManager, comandos, fila e snapshots reais. Nenhum mock de ordem.
    private sealed class Fixture : IDisposable
    {
        private readonly WDMatchSetup previous = WDMatchSetupContext.Current;
        public readonly GameObject ManagerObject = new("InteractionManager");
        public readonly GameObject Root = new("InteractionSafeArea", typeof(RectTransform));
        public readonly GameObject TerritoryObject = new("InteractionTerritory");
        public readonly WarDominionUITheme Theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        public readonly GameManager Manager;
        public readonly MatchUIPresenter Presenter;
        public readonly MatchUICommands Commands;
        public readonly FilaTransferencias Queue;
        public readonly TerritorioClique Territory;
        public readonly WDMatchInformationalOverlayController Info;
        public readonly WDMatchInteractionView View;
        public readonly RectTransform Entry;
        public Fixture(int teamSize = 2, float width = 1920)
        {
            var participants = Enumerable.Range(0,teamSize*2).Select(i => new WDMatchParticipant(
                "p"+i,"PLAYER "+(i+1),i%2,i,Color.white,"","",i==0?WDMatchParticipantKind.Local:WDMatchParticipantKind.Remote)).ToArray();
            WDMatchSetupContext.Store(new WDMatchSetup("teams","","test_map","","",10,false,42,participants));
            Manager = ManagerObject.AddComponent<GameManager>();
            Queue = ManagerObject.AddComponent<FilaTransferencias>();
            var transfers = ManagerObject.AddComponent<SistemaTransferencias>(); transfers.Inicializar(Queue);
            Set(Manager,"filaTransferencias",Queue); Set(Manager,"sistemaTransferencias",transfers);
            Set(Manager,"filaAcoes",ManagerObject.AddComponent<FilaAcoes>());
            Set(Manager,"bloqueioInicioPartida",false);
            Manager.faseAtual=GameManager.FaseTurno.Preparacao;
            Manager.estadoPreparacao=GameManager.EstadoPreparacao.Preparando;
            Manager.jogadorLocal=TerritorioClique.Dono.Jogador1;
            TerritoryObject.AddComponent<TerritorioTropas>(); TerritoryObject.AddComponent<TerritorioFronteiras>();
            Territory=TerritoryObject.AddComponent<TerritorioClique>(); Territory.idTerritorio="interaction_test";
            Assert.That(Territory.TentarAplicarEstadoAutoritativo(TerritorioClique.Dono.Jogador1,4,TerritorioClique.EstadoTerritorial.Ocupado),Is.True);
            Presenter=ManagerObject.AddComponent<MatchUIPresenter>(); Set(Presenter,"gameManager",Manager);
            Commands=new MatchUICommands(Manager);
            Info=ManagerObject.AddComponent<WDMatchInformationalOverlayController>();
            ((RectTransform)Root.transform).sizeDelta=new Vector2(width,1080);
            Info.Configure((RectTransform)Root.transform, Theme);
            View=Root.AddComponent<WDMatchInteractionView>(); View.Build(Theme); View.Bind(Commands,Info);
            Presenter.EstadoAlterado += state => { Info.Present(state); View.Present(state); };
            Entry=WDUIFactory.Rect("PlayerEntry",Root.transform); Entry.sizeDelta=new Vector2(54,54);
            Entry.anchoredPosition=new Vector2(width/2-50,300);
            Snapshot();
        }
        public void Snapshot() => Presenter.SolicitarSnapshotAtual();
        public void Select() { Commands.SelecionarModoTransferencia(); Manager.ClicarTerritorio(Territory); Snapshot(); }
        public void Dispose()
        {
            Object.DestroyImmediate(Root); Object.DestroyImmediate(ManagerObject);
            Object.DestroyImmediate(TerritoryObject); Object.DestroyImmediate(Theme);
            if(previous==null)WDMatchSetupContext.Clear();else WDMatchSetupContext.Store(previous);
        }
    }
    private static void Set(object target,string name,object value) =>
        target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);

    [TestCase(2)] [TestCase(3)]
    public void TransferRequiresConfirmationUsesRealAlliesAndActionsCancellation(int teamSize)
    {
        using var f=new Fixture(teamSize);
        f.Select();
        Assert.That(f.Manager.EmModoTransferencia,Is.True);
        Assert.That(f.Queue.Ordens,Is.Empty,"Selecionar o território não registra a ordem.");
        Assert.That(f.Manager.territorioTransferenciaSelecionado,Is.SameAs(f.Territory));
        Assert.That(f.View.ProgrammedTerritoryId,Is.Empty);
        Assert.That(f.View.IsAllySelection,Is.EqualTo(teamSize==3));
        Assert.That(f.Presenter.EstadoAtual.DestinatariosTransferencia.Count,Is.EqualTo(teamSize-1));
        f.Commands.SelecionarDestinatarioTransferencia(TerritorioClique.Dono.Jogador1);
        f.Commands.SelecionarDestinatarioTransferencia(TerritorioClique.Dono.Jogador2);
        if(teamSize==3)
        {
            f.View.ConfirmTransfer(); Assert.That(f.Queue.Ordens,Is.Empty);
            f.View.ChooseAlly(1);
            Assert.That(f.Queue.Ordens,Is.Empty,"Escolher aliado também não confirma.");
        }
        f.View.ConfirmTransfer();
        f.View.ConfirmTransfer();
        Assert.That(f.Queue.Ordens.Count,Is.EqualTo(1));
        Assert.That(f.Queue.Ordens[0].Destinatario,Is.EqualTo(teamSize==2?TerritorioClique.Dono.Jogador3:TerritorioClique.Dono.Jogador5));
        Assert.That(f.Manager.EmModoTransferencia,Is.False);
        Assert.That(f.Manager.territorioTransferenciaSelecionado,Is.Null);
        Assert.That(f.Territory.dono,Is.EqualTo(TerritorioClique.Dono.Jogador1),"Preparation não executa a transferência.");
        Assert.That(f.View.ProgrammedTerritoryId,Is.EqualTo(f.Territory.idTerritorio));
        var actionsObject=WDUIFactory.Rect("Actions",f.Root.transform).gameObject;
        var actions=actionsObject.AddComponent<WDMatchActionsView>();actions.Build(f.Theme);actions.Bind(f.Commands,null);
        actions.Present(f.Presenter.EstadoAtual); actions.Toggle();
        var cancel=actions.GetComponentsInChildren<Button>().Single(b=>b.name=="Cancel"&&b.transform.parent.name=="Transfer");
        cancel.onClick.Invoke();
        Assert.That(f.Queue.Ordens,Is.Empty);Assert.That(f.View.ProgrammedTerritoryId,Is.Empty);
    }
    [Test]
    public void TransferCancelInvalidTerritoryAndMasterSelectionDoNotProgramOrReinforce()
    {
        using var f=new Fixture();
        f.Commands.SelecionarModoTransferencia();
        f.Territory.TentarAplicarEstadoAutoritativo(TerritorioClique.Dono.Jogador2,4,TerritorioClique.EstadoTerritorial.Ocupado);
        f.Manager.ClicarTerritorio(f.Territory);Assert.That(f.Manager.territorioTransferenciaSelecionado,Is.Null);
        f.Territory.TentarAplicarEstadoAutoritativo(TerritorioClique.Dono.Jogador1,4,TerritorioClique.EstadoTerritorial.Ocupado);
        f.Manager.reforcosDisponiveis=5;
        f.Manager.ClicarTerritorio(f.Territory);f.Snapshot();
        Assert.That(f.Presenter.EstadoAtual.Distribuicoes,Is.Empty);
        Assert.That(f.Queue.Ordens,Is.Empty);
        f.View.Dismiss();
        Assert.That(f.Manager.EmModoTransferencia,Is.False);Assert.That(f.Manager.territorioTransferenciaSelecionado,Is.Null);
        Assert.That(f.Queue.Ordens,Is.Empty);Assert.That(f.View.IsOpen,Is.False);
    }
    [Test]
    public void OneVersusOneHasNoTransferOrTacticalAlly()
    {
        using var f=new Fixture(1);
        f.Commands.SelecionarModoTransferencia();
        Assert.That(f.Manager.EmModoTransferencia,Is.False);
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador2,f.Entry),Is.False);
    }
    [Test]
    public void PhaseChangeClearsUnconfirmedTerritoryWithoutProgrammingIt()
    {
        using var f=new Fixture(3);f.Select();
        Assert.That(f.View.IsAllySelection,Is.True);
        f.Manager.faseAtual=GameManager.FaseTurno.Resolucao;f.Snapshot();
        Assert.That(f.View.IsOpen,Is.False);
        Assert.That(f.Manager.territorioTransferenciaSelecionado,Is.Null);
        Assert.That(f.Queue.Ordens,Is.Empty);
    }
    [TestCase(GameManager.FaseTurno.Preparacao)] [TestCase(GameManager.FaseTurno.Resolucao)]
    public void EmojiUsesOwnPlayerSixNotoOptionsAndLocalPayload(GameManager.FaseTurno phase)
    {
        using var f=new Fixture(); f.Manager.faseAtual=phase;f.Snapshot();
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador2,f.Entry),Is.False);
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador1,f.Entry),Is.True);
        WDMatchReactionIntent? received=null;f.View.Reactions.Requested+=value=>received=value;
        Assert.That(WDMatchReactions.Options,Is.EqualTo(new[]{"😎","😢","😘","✌️","😁","😂"}));
        Assert.That(f.View.SelectEmoji("unknown"),Is.False);
        Assert.That(f.View.SelectEmoji("😎"),Is.True);
        Assert.That(received.Value.Author,Is.EqualTo(TerritorioClique.Dono.Jogador1));
        Assert.That(received.Value.Emoji,Is.EqualTo("😎"));Assert.That(f.View.IsOpen,Is.False);
        var labels=f.Root.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t=>t.name.StartsWith("Emoji")&&t.name!="EmojiRadial").ToArray();
        Assert.That(labels.Length,Is.EqualTo(5));
        Assert.That(labels.All(t=>t.spriteAsset!=null&&t.spriteAsset.spriteCharacterTable.Count==34&&!t.richText),Is.True);
        Assert.That(f.Root.transform.Find("EmojiRadial/NotoKiss").GetComponent<Image>().sprite,Is.Not.Null);
        Assert.That(WDChatEmojiColor.SpriteAsset.spriteCharacterTable.Count,Is.EqualTo(34),"A reação adicional não muda o catálogo/aliases do Chat.");
    }
    [TestCase(WDTacticalPingKind.Attack)] [TestCase(WDTacticalPingKind.Defend)]
    [TestCase(WDTacticalPingKind.Focus)] [TestCase(WDTacticalPingKind.Transfer)] [TestCase(WDTacticalPingKind.Attention)]
    public void TacticalIntentRequiresRealAllyAndPreparationWithoutGameplayEffects(WDTacticalPingKind kind)
    {
        using var f=new Fixture(3);
        Assert.That(f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador1,kind),Is.False);
        Assert.That(f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador2,kind),Is.False);
        var owner = kind == WDTacticalPingKind.Attack || kind == WDTacticalPingKind.Focus
            ? TerritorioClique.Dono.Jogador2 : TerritorioClique.Dono.Jogador3;
        f.Territory.TentarAplicarEstadoAutoritativo(owner, 4, TerritorioClique.EstadoTerritorial.Ocupado);
        f.Snapshot();
        WDTacticalPing? received=null; int emitted = 0;
        f.Info.TacticalPingRequested+=p=>{ received=p; emitted++; };
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador3,f.Entry),Is.True);
        Assert.That(f.View.SelectTactic(kind),Is.True);
        Assert.That(received, Is.Null, "Radial ainda não escolheu território.");
        Assert.That(f.Info.Pings, Is.Empty);
        Assert.That(f.Info.HasPendingTacticalIntent, Is.True);
        Assert.That(f.View.IsOpen, Is.False);
        f.Manager.ClicarTerritorio(f.Territory);
        Assert.That(emitted, Is.EqualTo(1));
        Assert.That(f.Info.HasPendingTacticalIntent, Is.False);
        Assert.That(received.Value.Kind,Is.EqualTo(kind));Assert.That(received.Value.Recipient,Is.EqualTo(TerritorioClique.Dono.Jogador3));
        Assert.That(received.Value.Author, Is.EqualTo(TerritorioClique.Dono.Jogador1));
        Assert.That(received.Value.SenderPlayerId, Is.EqualTo("p0"));
        Assert.That(received.Value.TargetPlayerId, Is.EqualTo("p2"));
        Assert.That(received.Value.Round, Is.EqualTo(f.Presenter.EstadoAtual.Round));
        Assert.That(received.Value.Phase, Is.EqualTo(GameManager.FaseTurno.Preparacao));
        Assert.That(received.Value.MatchId, Is.EqualTo(WDMatchSetupContext.Current.MatchId));
        Assert.That(received.Value.TerritoryId, Is.EqualTo(f.Territory.idTerritorio));
        var marker = f.Root.transform.Cast<Transform>().Single(t => t.name.StartsWith("Tactical_"));
        var label = marker.GetComponentInChildren<TMPro.TMP_Text>(true);
        Assert.That(label.text, Is.EqualTo(WDTacticalVisualStyle.Label(kind)));
        Assert.That(label.color, Is.EqualTo(WDTacticalVisualStyle.Color(kind)));
        Assert.That(marker.GetComponentsInChildren<TMPro.TMP_Text>(true).Length, Is.EqualTo(1));
        var image = marker.Find(WDTacticalVisualStyle.IconName(kind)).GetComponent<Image>();
        Assert.That(image.sprite.texture, Is.SameAs(Resources.Load<Texture2D>(WDTacticalVisualStyle.MarkerAssetRoot + WDTacticalVisualStyle.IconName(kind))));
        Assert.That(marker.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(f.Root.transform.Find("TacticalMessage"), Is.Null);
        Assert.That(f.Presenter.EstadoAtual.Acoes, Is.Empty);
        Assert.That(f.Territory.dono, Is.EqualTo(owner));
        Assert.That(f.Territory.Tropas, Is.EqualTo(4));
        Assert.That(f.Queue.Ordens,Is.Empty);
        f.Manager.faseAtual=GameManager.FaseTurno.Resolucao;f.Snapshot();
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador3,f.Entry),Is.False);
        Assert.That(f.Info.HasPendingTacticalIntent,Is.False);
    }
    [TestCase(1920f)] [TestCase(2400f)]
    public void BothRadialsRemainWithinSafeAreaAtExtremePlayerPositions(float width)
    {
        using var f=new Fixture(3,width);
        foreach(float y in new[]{520f,-520f})
        foreach(var owner in new[]{TerritorioClique.Dono.Jogador1,TerritorioClique.Dono.Jogador3})
        {
            f.Entry.anchoredPosition=new Vector2(width/2-30,y);
            Assert.That(f.View.OpenPlayer(owner,f.Entry),Is.True);
            var hub=(RectTransform)f.Root.transform.Find(owner==TerritorioClique.Dono.Jogador1?"EmojiRadial":"TacticalRadial");
            var corners=new Vector3[4];hub.GetWorldCorners(corners);
            var avatarCorners = new Vector3[4]; f.Entry.GetWorldCorners(avatarCorners);
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(avatarCorners[0].x - 15.9f));
            foreach(Vector3 corner in corners)
            {
                Vector3 local=f.Root.transform.InverseTransformPoint(corner);
                Assert.That(local.x,Is.InRange(-width/2+7.9f,width/2-7.9f));
                Assert.That(local.y,Is.InRange(-532.1f,532.1f));
            }
            f.View.Close();
        }
    }

    [Test]
    public void V2CoordinatesTransferRadialsAndActionsWithoutDiscardingQueuedTransfer()
    {
        using var f=new Fixture();
        var rect=WDUIFactory.Rect("MatchV2",f.Root.transform);
        var ui=rect.gameObject.AddComponent<WDMatchUIV2>();ui.Build(f.Theme,f.Commands,null,null);
        ui.BindInformational(f.Info);
        f.Presenter.EstadoAlterado+=ui.Present;ui.Present(f.Presenter.EstadoAtual);
        var transfer=rect.Find("LeftCommandRail/Transfer").GetComponent<Button>();
        transfer.onClick.Invoke();
        Assert.That(f.Manager.EmModoTransferencia,Is.True);
        Assert.That(transfer.GetComponent<Image>().color,Is.EqualTo(new Color(.65f,.12f,.12f)));
        Assert.That(transfer.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("TRANSFERÊNCIA ×"));
        transfer.onClick.Invoke();Assert.That(f.Manager.EmModoTransferencia,Is.False);
        Assert.That(transfer.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("TRANSFERÊNCIA"));
        rect.Find("LeftCommandRail/Support").GetComponent<Button>().onClick.Invoke();
        Assert.That(rect.Find("MatchSupport").GetComponent<WDMatchSupportView>().IsOpen,Is.True);
        transfer.onClick.Invoke();
        Assert.That(rect.Find("MatchSupport").GetComponent<WDMatchSupportView>().IsOpen,Is.False);
        var interaction=rect.Find("MatchInteractions").GetComponent<WDMatchInteractionView>();
        Assert.That(interaction.OpenPlayer(TerritorioClique.Dono.Jogador1,f.Entry),Is.True);
        Assert.That(f.Manager.EmModoTransferencia,Is.False,"O hub abandona apenas a intenção transitória.");
        interaction.Close();
        f.Select();f.View.ConfirmTransfer();
        Assert.That(f.Queue.Ordens.Count,Is.EqualTo(1));
        Assert.That(interaction.OpenPlayer(TerritorioClique.Dono.Jogador3,f.Entry),Is.True);
        rect.Find("LeftCommandRail/Actions").GetComponent<Button>().onClick.Invoke();
        Assert.That(interaction.IsOpen,Is.False);
        Assert.That(rect.Find("MatchActions").GetComponent<WDMatchActionsView>().IsOpen,Is.True);
        Assert.That(f.Queue.Ordens.Count,Is.EqualTo(1),"Trocar overlay não cancela programação.");
        f.Manager.faseAtual=GameManager.FaseTurno.Resolucao;f.Snapshot();
        Assert.That(interaction.OpenPlayer(TerritorioClique.Dono.Jogador3,f.Entry),Is.False);
    }

    [Test]
    public void UtilityBindingCanRepublishSynchronouslyWhileAbandoningTransfer()
    {
        using var f=new Fixture();
        var rect=WDUIFactory.Rect("MatchV2",f.Root.transform);
        var ui=rect.gameObject.AddComponent<WDMatchUIV2>();ui.Build(f.Theme,f.Commands,null,null);
        var utility=new WDMatchUtilityCommands(()=>{},()=>false);
        ui.BindUtilities(utility);ui.Present(f.Presenter.EstadoAtual);
        int publications=0;
        f.Presenter.EstadoAlterado+=state=>
        {
            Assert.That(++publications,Is.LessThan(12),"Binding não pode entrar em publicação recursiva infinita.");
            ui.BindUtilities(utility);ui.Present(state);
        };
        f.Commands.SelecionarModoTransferencia();
        utility.Toggle(WDMatchUtilityModule.Menu);
        Assert.That(f.Manager.EmModoTransferencia,Is.False);
        Assert.That(f.Manager.territorioTransferenciaSelecionado,Is.Null);
        Assert.That(f.Queue.Ordens,Is.Empty);
        Assert.That(utility.ActiveModule,Is.EqualTo(WDMatchUtilityModule.Menu));
    }

    [TestCase(1920f)] [TestCase(2400f)]
    public void ReactionsTrackTheirSenderAfterRowsMoveWithoutChangingSizeOrLifetime(float width)
    {
        using var f = new Fixture(3, width);
        foreach (var owner in new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador3 })
        {
            f.Manager.jogadorLocal = owner; f.Snapshot();
            f.Entry.anchoredPosition = new Vector2(width / 2 - 100, owner == TerritorioClique.Dono.Jogador1 ? 200 : -100);
            Assert.That(f.View.OpenPlayer(owner, f.Entry), Is.True);
            float before = Time.unscaledTime;
            Assert.That(f.View.SelectEmoji("😎"), Is.True);
            var reaction = (RectTransform)f.Root.transform.Find("LocalReaction");
            Assert.That(reaction.sizeDelta, Is.EqualTo(new Vector2(80, 80)));
            Assert.That(reaction.GetComponent<TMPro.TMP_Text>().fontSize, Is.EqualTo(42));
            float until = (float)typeof(WDMatchInteractionView).GetField("reactionUntil", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.View);
            Assert.That(until, Is.InRange(before + 3f, Time.unscaledTime + 3f));
            AssertReactionPosition(f.Entry, reaction);
            f.Entry.anchoredPosition += new Vector2(0, 76);
            TickView(f.View);
            AssertReactionPosition(f.Entry, reaction);
            Assert.That((float)typeof(WDMatchInteractionView).GetField("reactionUntil", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.View), Is.EqualTo(until));
        }
    }
    private static void AssertReactionPosition(RectTransform entry, RectTransform reaction)
    {
        var avatarCorners = new Vector3[4]; entry.GetWorldCorners(avatarCorners);
        var reactionCorners = new Vector3[4]; reaction.GetWorldCorners(reactionCorners);
        Assert.That(reaction.TransformPoint(reaction.rect.center).y,
            Is.EqualTo(entry.TransformPoint(entry.rect.center).y).Within(.01f));
        Assert.That(reactionCorners[2].x, Is.LessThanOrEqualTo(avatarCorners[0].x - 11.9f));
    }
    private static void TickView(WDMatchInteractionView view) => typeof(WDMatchInteractionView)
        .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);

    [TestCase(1920f)] [TestCase(2400f)]
    public void WiderPlayerColumnKeepsTypographyAndAllOverlaysOutsideItsBounds(float width)
    {
        using var f = new Fixture(3, width);
        var rect = WDUIFactory.Rect("MatchV2", f.Root.transform);
        var ui = rect.gameObject.AddComponent<WDMatchUIV2>(); ui.Build(f.Theme, f.Commands, null, null);
        ui.Present(f.Presenter.EstadoAtual);
        var players = (RectTransform)rect.Find("Players");
        rect.Find("PlayersContrast").GetComponent<WDMatchPlayersContrast>().Refresh();
        LayoutRebuilder.ForceRebuildLayoutImmediate(players);
        var panel = (RectTransform)rect.Find("PlayersContrast");
        Assert.That(panel.rect.width, Is.EqualTo(234));
        var panelCorners = new Vector3[4]; panel.GetWorldCorners(panelCorners);
        Assert.That(panelCorners[2].x, Is.EqualTo(width / 2 - 20).Within(.01));
        var view = rect.Find("MatchInteractions").GetComponent<WDMatchInteractionView>();
        var entry = (RectTransform)players.Find("Player-Jogador1/AvatarSlot");
        var identity = players.Find("Player-Jogador1/Identity").GetComponent<TMPro.TMP_Text>();
        Assert.That(identity.fontSize, Is.EqualTo(17));
        Assert.That(identity.rectTransform.rect.width, Is.EqualTo(150).Within(.01));
        Assert.That(entry.rect.size, Is.EqualTo(new Vector2(54, 54)));
        Assert.That(view.OpenPlayer(TerritorioClique.Dono.Jogador1, entry), Is.True);
        var hubCorners = new Vector3[4]; ((RectTransform)view.transform.Find("EmojiRadial")).GetWorldCorners(hubCorners);
        Assert.That(hubCorners[2].x, Is.LessThanOrEqualTo(panelCorners[0].x - 15.9f));
        Assert.That(view.SelectEmoji("😘"), Is.True);
        var reaction = (RectTransform)view.transform.Find("LocalKissReaction");
        Assert.That(reaction.sizeDelta, Is.EqualTo(new Vector2(63, 63)));
        var reactionCorners = new Vector3[4]; reaction.GetWorldCorners(reactionCorners);
        Assert.That(reactionCorners[2].x, Is.LessThanOrEqualTo(panelCorners[0].x - 11.9f));
        Assert.That(reaction.position.y, Is.EqualTo(entry.TransformPoint(entry.rect.center).y).Within(.01));
        var allyEntry = (RectTransform)players.Find("Player-Jogador3/AvatarSlot");
        Assert.That(view.OpenPlayer(TerritorioClique.Dono.Jogador3, allyEntry), Is.True);
        ((RectTransform)view.transform.Find("TacticalRadial")).GetWorldCorners(hubCorners);
        Assert.That(hubCorners[2].x, Is.LessThanOrEqualTo(panelCorners[0].x - 15.9f));
        foreach (string hubName in new[] { "EmojiRadial", "TacticalRadial" })
        {
            ((RectTransform)view.transform.Find(hubName)).GetWorldCorners(hubCorners);
            foreach (Vector3 corner in hubCorners)
            {
                Vector3 local = f.Root.transform.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(-width / 2 + 7.9f, width / 2 - 7.9f));
                Assert.That(local.y, Is.InRange(-532.1f, 532.1f));
            }
        }
    }

    [Test]
    public void EmojiEyeUsesCompleteEyeAndSeparateSlashInBothStates()
    {
        using var f = new Fixture();
        f.View.OpenPlayer(TerritorioClique.Dono.Jogador1, f.Entry);
        var radial = f.Root.transform.Find("EmojiRadial");
        Assert.That(radial.Find("HideEye").GetComponent<Image>().sprite, Is.Not.Null);
        Assert.That(radial.Find("HideEye").GetComponent<Image>().sprite.texture,
            Is.SameAs(Resources.Load<Texture2D>("UI/Match/FigmaCurrent/Hubs/HideEye")));
        Assert.That(radial.Find("HideSlash").GetComponent<Image>().sprite.texture,
            Is.SameAs(Resources.Load<Texture2D>("UI/Match/FigmaCurrent/Hubs/HideSlash")));
        Assert.That(radial.Find("HideSlash").gameObject.activeSelf, Is.True);
        var eye = new Texture2D(2, 2); var slash = new Texture2D(2, 2);
        try
        {
            eye.LoadImage(System.IO.File.ReadAllBytes("Assets/Resources/UI/Match/FigmaCurrent/Hubs/HideEye.png"));
            slash.LoadImage(System.IO.File.ReadAllBytes("Assets/Resources/UI/Match/FigmaCurrent/Hubs/HideSlash.png"));
            Assert.That(eye.GetPixel(17, 12).a, Is.GreaterThan(.8f), "Pupila completa.");
            Assert.That(eye.GetPixel(1, 12).a, Is.GreaterThan(.5f), "Contorno lateral do olho, fora da diagonal.");
            Assert.That(slash.GetPixel(1, 12).a, Is.LessThan(.05f), "A camada removida é somente a diagonal.");
            Assert.That(slash.GetPixel(17, 12).a, Is.GreaterThan(.5f));
        }
        finally { Object.DestroyImmediate(eye); Object.DestroyImmediate(slash); }
        f.View.Reactions.ToggleHidden(); TickView(f.View);
        Assert.That(radial.Find("HideEye").gameObject.activeSelf, Is.True);
        Assert.That(radial.Find("HideSlash").gameObject.activeSelf, Is.False);
        Assert.That(radial.Find("HideLabel").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("MOSTRAR\nEMOJIS"));
        f.View.Reactions.ToggleHidden(); TickView(f.View);
        Assert.That(radial.Find("HideSlash").gameObject.activeSelf, Is.True);
        Assert.That(radial.Find("HideLabel").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("OCULTAR\nEMOJIS"));
    }

    [TestCase("cancel")] [TestCase("confirm")] [TestCase("phase")]
    public void ConfirmationUsesExistingHighlightAndCleansUpWithCompactTextFreeBody(string exit)
    {
        using var f = new Fixture();
        f.TerritoryObject.AddComponent<SpriteRenderer>();
        var visual = f.TerritoryObject.AddComponent<TerritorioVisual>();
        f.Select();
        Assert.That(visual.TransferConfirmationHighlighted, Is.True);
        Assert.That(visual.SelectionRaised, Is.True);
        // Cor não branca controlada: a paleta global não deve decidir se o
        // aumento de luminosidade é mensurável nesta fixture.
        Set(visual, "corBase", new Color(.1f, .2f, .3f));
        visual.SetTransferConfirmationHighlight(true);
        var selectedSprite = f.TerritoryObject.transform.Find("SelectionScaleRoot/SelectionSprite").GetComponent<SpriteRenderer>();
        float strongBrightness = selectedSprite.color.grayscale;
        visual.SetTransferConfirmationHighlight(false);
        Assert.That(strongBrightness, Is.GreaterThan(selectedSprite.color.grayscale));
        visual.SetTransferConfirmationHighlight(true);
        var modal = (RectTransform)f.Root.transform.Find("TransferConfirmation");
        Assert.That(modal.sizeDelta, Is.EqualTo(new Vector2(560, 230)));
        Assert.That(modal.Find("Detail"), Is.Null);
        Assert.That(modal.Find("Footer"), Is.Null);
        if (exit == "cancel") f.View.Dismiss();
        else if (exit == "confirm") f.View.ConfirmTransfer();
        else { f.Manager.faseAtual = GameManager.FaseTurno.Resolucao; f.Snapshot(); }
        Assert.That(visual.TransferConfirmationHighlighted, Is.False);
        Assert.That(f.Queue.Ordens.Count, Is.EqualTo(exit == "confirm" ? 1 : 0));
    }

    [Test]
    public void TacticalPayloadCanBePresentedToRecipientExpiresAndRejectsWrongContext()
    {
        using var f = new Fixture(3);
        f.Territory.TentarAplicarEstadoAutoritativo(TerritorioClique.Dono.Jogador3, 4, TerritorioClique.EstadoTerritorial.Ocupado);
        WDTacticalPing? sent = null; f.Info.TacticalPingRequested += ping => sent = ping;
        f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, WDTacticalPingKind.Transfer);
        f.Info.HandleTerritory(f.Territory.idTerritorio);
        var ping = sent.Value;
        f.Manager.jogadorLocal = TerritorioClique.Dono.Jogador3; f.Snapshot();
        Assert.That(f.Info.ReceiveTacticalPing(ping), Is.True, "Mesmo contrato pode chegar ao cliente do destinatário.");
        Assert.That(f.Queue.Ordens, Is.Empty);
        Assert.That(f.Presenter.EstadoAtual.Acoes, Is.Empty);
        var wrongIdentity = new WDTacticalPing(ping.Kind, ping.TerritoryId, ping.Author, ping.Recipient,
            "wrong", ping.TargetPlayerId, ping.Round, ping.Phase, ping.MatchId);
        Assert.That(f.Info.ReceiveTacticalPing(wrongIdentity), Is.False);
        var enemy = new WDTacticalPing(ping.Kind, ping.TerritoryId, TerritorioClique.Dono.Jogador2, ping.Recipient,
            "p1", ping.TargetPlayerId, ping.Round, ping.Phase, ping.MatchId);
        Assert.That(f.Info.ReceiveTacticalPing(enemy), Is.False);
        var wrongRound = new WDTacticalPing(ping.Kind, ping.TerritoryId, ping.Author, ping.Recipient,
            ping.SenderPlayerId, ping.TargetPlayerId, ping.Round + 1, ping.Phase, ping.MatchId);
        Assert.That(f.Info.ReceiveTacticalPing(wrongRound), Is.False);
        typeof(WDMatchInformationalOverlayController).GetMethod("ExpireTacticalSignals", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(f.Info, new object[] { Time.unscaledTime + WDMatchInformationalOverlayController.TacticalSignalDuration + .1f });
        TickView(f.View);
        Assert.That(f.Info.Pings, Is.Empty);
        Assert.That(f.Root.transform.Cast<Transform>().Any(t => t.name.StartsWith("Tactical_")), Is.False);
        f.Manager.faseAtual = GameManager.FaseTurno.Resolucao; f.Snapshot();
        Assert.That(f.Info.ReceiveTacticalPing(ping), Is.False);
    }

    // 02. Intenção passiva: ownership decide somente a comunicação, nunca highlights.
    [TestCase(WDTacticalPingKind.Attack)] [TestCase(WDTacticalPingKind.Defend)]
    [TestCase(WDTacticalPingKind.Focus)] [TestCase(WDTacticalPingKind.Transfer)] [TestCase(WDTacticalPingKind.Attention)]
    public void TacticalPredicateMatrixDiscardsInvalidIntentWithoutHighlightsOrPayload(WDTacticalPingKind kind)
    {
        using var f = new Fixture(3);
        f.TerritoryObject.AddComponent<SpriteRenderer>();
        var visual = f.TerritoryObject.AddComponent<TerritorioVisual>();
        int emitted = 0; f.Info.TacticalPingRequested += _ => emitted++;
        var owners = new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador3,
            TerritorioClique.Dono.Jogador5, TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Neutro };
        foreach (var owner in owners)
        {
            int troops = owner == TerritorioClique.Dono.Neutro ? 1 : 4;
            Assert.That(f.Territory.TentarAplicarEstadoAutoritativo(owner, troops,
                owner == TerritorioClique.Dono.Neutro ? TerritorioClique.EstadoTerritorial.Neutro : TerritorioClique.EstadoTerritorial.Ocupado), Is.True);
            bool expected = kind == WDTacticalPingKind.Attack || kind == WDTacticalPingKind.Focus
                ? owner == TerritorioClique.Dono.Jogador2
                : kind == WDTacticalPingKind.Defend
                    ? owner == TerritorioClique.Dono.Jogador1 || owner == TerritorioClique.Dono.Jogador3 || owner == TerritorioClique.Dono.Jogador5
                    : owner == TerritorioClique.Dono.Jogador3;
            Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador3, f.Entry), Is.True);
            Assert.That(f.View.SelectTactic(kind), Is.True);
            Assert.That(f.View.IsOpen, Is.False);
            Assert.That(f.Info.HasPendingTacticalIntent, Is.True);
            Assert.That(visual.SelectionRaised, Is.False);
            Assert.That(f.Territory.transform.Find("SelectionScaleRoot"), Is.Null);
            int before = emitted, markersBefore = f.Info.Pings.Count;
            Assert.That(f.Info.TryConsumeTacticalIntent(f.Territory.idTerritorio), Is.EqualTo(expected));
            Assert.That(emitted - before, Is.EqualTo(expected ? 1 : 0));
            Assert.That(f.Info.Pings.Count - markersBefore, Is.EqualTo(expected ? 1 : 0));
            Assert.That(f.Root.transform.Cast<Transform>().Count(t => t.name.StartsWith("Tactical_")), Is.EqualTo(f.Info.Pings.Count));
            Assert.That(f.Info.HasPendingTacticalIntent, Is.False);
            Assert.That(visual.SelectionRaised, Is.False);
            Assert.That(f.Queue.Ordens, Is.Empty);
            Assert.That(f.Presenter.EstadoAtual.Acoes, Is.Empty);
            Assert.That(f.Territory.dono, Is.EqualTo(owner));
            Assert.That(f.Territory.Tropas, Is.EqualTo(troops));
            Assert.That(f.Manager.territorioSelecionado, Is.Null);
        }
    }

    [TestCase(WDTacticalPingKind.Attack)] [TestCase(WDTacticalPingKind.Focus)]
    [TestCase(WDTacticalPingKind.Transfer)] [TestCase(WDTacticalPingKind.Attention)]
    public void InvalidTacticLetsTheSameClickSelectOwnedTerritoryNormally(WDTacticalPingKind kind)
    {
        using var f = new Fixture(3);
        // 02.1. A seleção normal requer AcaoTerrestre; a fixture nasce em Nenhum.
        // A tática não deve ativar esse modo por conta própria.
        f.Commands.SelecionarModoAcao();
        Assert.That(f.Manager.modoAcao, Is.EqualTo(GameManager.ModoAcao.AcaoTerrestre));
        Assert.That(f.Manager.PodeEditarPreparacao, Is.True);
        Assert.That(f.Manager.reforcosDisponiveis, Is.Zero);
        int emitted = 0; f.Info.TacticalPingRequested += _ => emitted++;
        Assert.That(f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, kind), Is.True);
        f.Manager.ClicarTerritorio(f.Territory); // Um único clique, sem replay.
        Assert.That(f.Info.HasPendingTacticalIntent, Is.False);
        Assert.That(emitted, Is.Zero); Assert.That(f.Info.Pings, Is.Empty);
        Assert.That(f.Manager.territorioSelecionado, Is.SameAs(f.Territory));
        Assert.That(f.Territory.Tropas, Is.EqualTo(4));
        Assert.That(f.Queue.Ordens, Is.Empty);
        Assert.That(f.Root.transform.Cast<Transform>().Any(t => t.name.StartsWith("Tactical_")), Is.False);
    }

    [Test]
    public void PendingTacticDoesNotReplaceExistingGameplayHighlight()
    {
        using var f = new Fixture(3);
        f.TerritoryObject.AddComponent<SpriteRenderer>();
        var visual = f.TerritoryObject.AddComponent<TerritorioVisual>();
        f.Commands.SelecionarModoAcao();
        f.Manager.ClicarTerritorio(f.Territory);
        f.Snapshot();
        var existing = f.Territory.transform.Find("SelectionScaleRoot");
        Assert.That(visual.SelectionRaised, Is.True);
        Assert.That(existing, Is.Not.Null);
        // Exercitar a coordenação real da V2: antes ela cancelava a seleção no Opening.
        var rect = WDUIFactory.Rect("MatchV2", f.Root.transform);
        var ui = rect.gameObject.AddComponent<WDMatchUIV2>(); ui.Build(f.Theme, f.Commands, null, null);
        ui.BindInformational(f.Info); f.Presenter.EstadoAlterado += ui.Present;
        ui.Present(f.Presenter.EstadoAtual);
        var interaction = rect.Find("MatchInteractions").GetComponent<WDMatchInteractionView>();
        Assert.That(interaction.OpenPlayer(TerritorioClique.Dono.Jogador3, f.Entry), Is.True);
        Assert.That(f.Manager.territorioSelecionado, Is.SameAs(f.Territory));
        Assert.That(visual.SelectionRaised, Is.True);
        Assert.That(interaction.SelectTactic(WDTacticalPingKind.Attack), Is.True);
        Assert.That(interaction.IsOpen, Is.False);
        Assert.That(f.Info.HasPendingTacticalIntent, Is.True);
        Assert.That(visual.SelectionRaised, Is.True);
        Assert.That(f.Territory.transform.Find("SelectionScaleRoot"), Is.SameAs(existing));
        f.Info.CancelTacticalSelection();
        Assert.That(visual.SelectionRaised, Is.True);
        Assert.That(f.Manager.territorioSelecionado, Is.SameAs(f.Territory));
    }

    // 03. Mesmo pipeline de input, incluindo o contador que inicia pressão/hold.
    [Test]
    public void CounterKeepsNormalGestureOnInvalidOrNeutralTarget(
        [Values(WDTacticalPingKind.Attack, WDTacticalPingKind.Defend, WDTacticalPingKind.Focus,
            WDTacticalPingKind.Transfer, WDTacticalPingKind.Attention)] WDTacticalPingKind kind,
        [Values(false, true)] bool neutral)
    {
        using var f = new Fixture(3);
        var previousManager = GameManager.instance;
        var inputObject = new GameObject("PassiveTacticInput"); inputObject.SetActive(false);
        var counterObject = new GameObject("Counter"); counterObject.transform.SetParent(f.Territory.transform);
        try
        {
            // Fixture entrega apenas dependências do pipeline, sem executar Update/Test Runner.
            GameManager.instance = f.Manager;
            var input = inputObject.AddComponent<InputPartida>();
            var counter = counterObject.AddComponent<ContadorTropas>(); Set(counter, "territorio", f.Territory);
            var counterHit = counterObject.AddComponent<BoxCollider2D>();
            var territoryHit = f.TerritoryObject.AddComponent<BoxCollider2D>();
            if (neutral)
                Assert.That(f.Territory.TentarAplicarEstadoAutoritativo(TerritorioClique.Dono.Neutro, 1,
                    TerritorioClique.EstadoTerritorial.Neutro), Is.True);
            Set(f.Manager, "reforcosBaseDisponiveis", 3); f.Manager.reforcosDisponiveis = 3;
            int troops = f.Territory.Tropas, emitted = 0;
            f.Info.TacticalPingRequested += _ => emitted++;
            f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, kind);
            typeof(InputPartida).GetMethod("ProcessarCliqueTerritorial", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(input, new object[] { new Collider2D[] { territoryHit, counterHit }, (Vector2?)new Vector2(.2f, .1f) });
            bool valid = !neutral && kind == WDTacticalPingKind.Defend;
            Assert.That(f.Info.HasPendingTacticalIntent, Is.False);
            Assert.That(emitted, Is.EqualTo(valid ? 1 : 0));
            Assert.That(f.Info.Pings.Count, Is.EqualTo(valid ? 1 : 0));
            Assert.That(typeof(InputPartida).GetField("contadorPressionado", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(input), valid ? Is.Null : Is.SameAs(counter));
            Assert.That(typeof(ContadorTropas).GetField("pressionando", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(counter), Is.EqualTo(!valid));
            counter.FinalizarPressaoInput(); // Up do mesmo gesto, não é outro clique.
            Assert.That(f.Territory.Tropas, Is.EqualTo(troops + (!valid && !neutral ? 1 : 0)));
            Assert.That(f.Manager.reforcosDisponiveis, Is.EqualTo(3 - (!valid && !neutral ? 1 : 0)));
            Assert.That(f.Queue.Ordens, Is.Empty);
            Assert.That(f.Manager.territorioSelecionado, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(inputObject); Object.DestroyImmediate(counterObject);
            GameManager.instance = previousManager;
        }
    }

    [TestCase("cancel")] [TestCase("phase")] [TestCase("disable")] [TestCase("transfer")]
    [TestCase("context")] [TestCase("controller-disable")]
    public void TacticalPendingIntentClearsOnEveryExitWithoutHighlightResidue(string exit)
    {
        using var f = new Fixture(3);
        f.TerritoryObject.AddComponent<SpriteRenderer>();
        var visual = f.TerritoryObject.AddComponent<TerritorioVisual>();
        f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, WDTacticalPingKind.Defend);
        Assert.That(visual.SelectionRaised, Is.False);
        if (exit == "cancel") f.View.Dismiss();
        else if (exit == "phase") { f.Manager.faseAtual = GameManager.FaseTurno.Resolucao; f.Snapshot(); }
        else if (exit == "context") { f.Manager.jogadorLocal = TerritorioClique.Dono.Jogador3; f.Snapshot(); }
        else if (exit == "controller-disable")
        {
            f.Info.enabled = false;
            typeof(WDMatchInformationalOverlayController).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Info, null);
        }
        else if (exit == "disable")
        {
            f.View.enabled = false;
            Assert.That(f.View.isActiveAndEnabled, Is.False);
            // MonoBehaviour sem ExecuteAlways: nesta fixture EditMode síncrona,
            // entregar explicitamente o callback que o Player dispara no disable.
            // Exercitar OnDisable real, sem substituir pelo cancelamento direto.
            var onDisable = typeof(WDMatchInteractionView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDisable, Is.Not.Null);
            onDisable.Invoke(f.View, null);
        }
        else { f.Commands.SelecionarModoTransferencia(); f.Snapshot(); }
        Assert.That(f.Info.HasPendingTacticalIntent, Is.False);
        Assert.That(visual.SelectionRaised, Is.False);
        Assert.That(f.Info.Pings, Is.Empty);
        Assert.That(f.Territory.transform.Find("SelectionScaleRoot"), Is.Null);
        Assert.That(typeof(WDMatchInformationalOverlayController).GetField("pendingRecipient", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(f.Info), Is.EqualTo(TerritorioClique.Dono.Neutro));
        Assert.That(typeof(WDMatchInformationalOverlayController).GetField("selectionMatchId", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(f.Info), Is.Null);
    }

    [Test]
    public void AllyChoiceContainsOnlyTitleAndRealAllyCards()
    {
        using var f = new Fixture(3); f.Select();
        var modal = f.Root.transform.Find("TransferConfirmation");
        Assert.That(modal.Find("Title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("SELECIONE UM ALIADO"));
        Assert.That(modal.Find("Ally0").gameObject.activeSelf, Is.True);
        Assert.That(modal.Find("Ally1").gameObject.activeSelf, Is.True);
        Assert.That(modal.Find("Detail"), Is.Null); Assert.That(modal.Find("Footer"), Is.Null);
        Assert.That(modal.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text),
            Has.None.Contains("após sua confirmação").And.None.Contains("Selecione um aliado para continuar"));
        Assert.That(f.Queue.Ordens, Is.Empty);
    }

    // 02. Regressão da cena real: câmera existe, mas sua tag é Untagged.
    // Canvas + imagens/texto ativos são verificados além da mera criação do payload.
    [TestCase(WDTacticalPingKind.Attack)] [TestCase(WDTacticalPingKind.Defend)]
    [TestCase(WDTacticalPingKind.Focus)] [TestCase(WDTacticalPingKind.Transfer)]
    [TestCase(WDTacticalPingKind.Attention)]
    public void TerritorialMarkerIsVisibleWithUntaggedMatchCamera(WDTacticalPingKind kind)
    {
        using var f = new Fixture(3);
        var existingCameras = Object.FindObjectsByType<Camera>()
            .Select(c => c.gameObject).Distinct().ToArray();
        var cameraObject = new GameObject("MainCamera", typeof(Camera));
        var canvasObject = new GameObject("TacticalCanvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            foreach (var previous in existingCameras) previous.SetActive(false);
            Assert.That(Camera.main, Is.Null, "Nome MainCamera sem tag reproduz as cenas da partida.");
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            f.Root.transform.SetParent(canvasObject.transform, false);
            Canvas.ForceUpdateCanvases();
            var owner = kind == WDTacticalPingKind.Attack || kind == WDTacticalPingKind.Focus
                ? TerritorioClique.Dono.Jogador2 : TerritorioClique.Dono.Jogador3;
            f.Territory.TentarAplicarEstadoAutoritativo(owner, 4, TerritorioClique.EstadoTerritorial.Ocupado);
            f.Territory.transform.position = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, 10));
            f.Snapshot();
            Assert.That(f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, kind), Is.True);
            f.Manager.ClicarTerritorio(f.Territory);
            Assert.That(f.Info.Pings.Count, Is.EqualTo(1));
            Assert.That(f.Info.Pings[0].TerritoryId, Is.EqualTo(f.Territory.idTerritorio));
            var marker = (RectTransform)f.Root.transform.Cast<Transform>().Single(t => t.name.StartsWith("Tactical_"));
            Assert.That(marker.gameObject.activeInHierarchy, Is.True);
            Assert.That(marker.GetComponentInParent<Canvas>(), Is.SameAs(canvas));
            Assert.That(marker.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(marker.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(marker.GetComponent<Graphic>(), Is.Null, "Sem placa/painel de fundo.");
            var icon = marker.Find(WDTacticalVisualStyle.IconName(kind)).GetComponent<Image>();
            Assert.That(icon.isActiveAndEnabled, Is.True);
            Assert.That(icon.color.a, Is.EqualTo(1f));
            Assert.That(icon.sprite.texture, Is.SameAs(Resources.Load<Texture2D>(
                WDTacticalVisualStyle.MarkerAssetRoot + WDTacticalVisualStyle.IconName(kind))));
            var label = marker.GetComponentInChildren<TMPro.TMP_Text>();
            Assert.That(label.text, Is.EqualTo(WDTacticalVisualStyle.Label(kind)));
            Assert.That(label.color, Is.EqualTo(WDTacticalVisualStyle.Color(kind)));
            Assert.That(label.font.sourceFontFile.name, Is.EqualTo("Gabarito-Medium"));
            Assert.That(label.fontSize, Is.EqualTo(13.125f).Within(.01f));
            Assert.That(label.characterSpacing, Is.EqualTo(4f));
            Assert.That(marker.sizeDelta.x, Is.EqualTo(kind == WDTacticalPingKind.Transfer ? 77f : 64.75f).Within(.1f));
            Assert.That(marker.sizeDelta.y, Is.InRange(45f, 50f));
            Assert.That(marker.localScale.x, Is.InRange(.89f, .95f));
            var textMaterial = label.fontSharedMaterial;
            Assert.That(textMaterial.IsKeywordEnabled("GLOW_ON"), Is.False);
            Assert.That(textMaterial.GetColor("_GlowColor").a, Is.Zero);
            Assert.That(textMaterial.GetFloat("_GlowInner"), Is.Zero);
            Assert.That(textMaterial.GetFloat("_GlowOuter"), Is.Zero);
            Assert.That(textMaterial.GetFloat("_GlowOffset"), Is.Zero);
            Assert.That(textMaterial.IsKeywordEnabled("UNDERLAY_ON"), Is.False);
            Assert.That(textMaterial.IsKeywordEnabled("UNDERLAY_INNER"), Is.False);
            Assert.That(textMaterial.GetColor("_UnderlayColor").a, Is.Zero);
            Assert.That(textMaterial.GetFloat("_OutlineSoftness"), Is.Zero);
            Assert.That(textMaterial.GetFloat("_FaceDilate"), Is.Zero);
            Assert.That(textMaterial.GetColor("_OutlineColor").a, Is.EqualTo(1f));
            Assert.That(textMaterial.GetColor("_OutlineColor").grayscale, Is.LessThan(.05f));
            float outlinePixels = textMaterial.GetFloat("_OutlineWidth") * textMaterial.GetFloat("_GradientScale") *
                label.fontSize / label.font.faceInfo.pointSize * .5f;
            Assert.That(outlinePixels, Is.EqualTo(.5f).Within(.1f), "Contorno fino sistêmico nas cinco labels.");
            Assert.That(label.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Center));
            Assert.That(icon.rectTransform.anchoredPosition.x, Is.EqualTo(label.rectTransform.anchoredPosition.x));
            Assert.That(icon.rectTransform.anchoredPosition.y, Is.GreaterThan(label.rectTransform.anchoredPosition.y));
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.characterCount, Is.EqualTo(label.text.Length));
            Assert.That(marker.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
            Assert.That(f.Info.HasPendingTacticalIntent, Is.False);
            Assert.That(f.Territory.dono, Is.EqualTo(owner)); Assert.That(f.Territory.Tropas, Is.EqualTo(4));
            Assert.That(f.Queue.Ordens, Is.Empty); Assert.That(f.Presenter.EstadoAtual.Acoes, Is.Empty);
        }
        finally
        {
            f.Root.transform.SetParent(null, false);
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(canvasObject);
            foreach (var previous in existingCameras) if (previous != null) previous.SetActive(true);
        }
    }

    [TestCase(1920f)] [TestCase(2400f)]
    public void TerritorialMarkerTracksTerritoryFadesAndReleasesTemporaryReferences(float width)
    {
        using var f = new Fixture(3, width);
        GameObject cameraObject = null;
        var camera = Camera.main;
        if (camera == null)
        {
            cameraObject = new GameObject("TacticalProjectionCamera", typeof(Camera));
            cameraObject.tag = "MainCamera"; camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        }
        try
        {
            f.Root.transform.position = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            f.Territory.transform.position = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, 10));
            f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, WDTacticalPingKind.Defend);
            f.Manager.ClicarTerritorio(f.Territory);
            var marker = (RectTransform)f.Root.transform.Cast<Transform>().Single(t => t.name.StartsWith("Tactical_"));
            Assert.That(marker.gameObject.activeInHierarchy, Is.True);
            Assert.That(marker.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            var text = marker.GetComponentInChildren<TMPro.TMP_Text>();
            var markerFont = text.font; var fontAtlases = markerFont.atlasTextures.ToArray();
            var textMaterial = text.fontSharedMaterial;
            var sprites = marker.GetComponentsInChildren<Image>(true).Select(i => i.sprite).ToArray();
            var positions = typeof(WDMatchInformationalOverlayController).GetMethod("PositionMarkers", BindingFlags.NonPublic | BindingFlags.Instance);
            var expiry = (System.Collections.Generic.List<float>)typeof(WDMatchInformationalOverlayController)
                .GetField("pingExpiry", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f.Info);
            Assert.That(marker.localScale.x, Is.InRange(.89f, .95f));
            positions.Invoke(f.Info, new object[] { expiry[0] - WDMatchInformationalOverlayController.TacticalSignalDuration + .11f });
            Assert.That(marker.localScale.x, Is.EqualTo(1.05f).Within(.01f));
            positions.Invoke(f.Info, new object[] { expiry[0] - WDMatchInformationalOverlayController.TacticalSignalDuration + .23f });
            Assert.That(marker.localScale.x, Is.EqualTo(1f).Within(.001f));
            Assert.That(marker.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            var originalPosition = marker.anchoredPosition;
            f.Territory.transform.position = camera.ViewportToWorldPoint(new Vector3(.6f, .5f, 10));
            positions.Invoke(f.Info, new object[] { expiry[0] - .25f });
            Assert.That(marker.gameObject.activeInHierarchy, Is.True);
            Assert.That(marker.anchoredPosition, Is.Not.EqualTo(originalPosition));
            Assert.That(MapOverlayProjection.TryWorldToOverlay((RectTransform)f.Root.transform,
                MapOverlayProjection.GetTerritoryWorldAnchor(f.Territory), camera, out var expected), Is.True);
            expected += new Vector2(0, 52);
            Assert.That(Vector2.Distance(marker.anchoredPosition, expected), Is.LessThan(.01f));
            Assert.That(marker.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.5f).Within(.01f));
            // Na borda do overlay a associação territorial prevalece sobre clamp de HUD.
            Vector2 edgeScreen = RectTransformUtility.WorldToScreenPoint(null,
                f.Root.transform.TransformPoint(new Vector3(width / 2f - 4f, 0, 0)));
            f.Territory.transform.position = camera.ScreenToWorldPoint(new Vector3(edgeScreen.x, edgeScreen.y, 10));
            positions.Invoke(f.Info, new object[] { expiry[0] - .25f });
            Assert.That(marker.anchoredPosition.x, Is.EqualTo(width / 2f - 4f).Within(.1f));
            typeof(WDMatchInformationalOverlayController).GetMethod("ExpireTacticalSignals", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(f.Info, new object[] { expiry[0] + .01f });
            Assert.That(f.Info.Pings, Is.Empty);
            Assert.That(marker == null, Is.True);
            Assert.That(sprites.All(sprite => sprite == null), Is.True);
            Assert.That(markerFont == null && textMaterial == null, Is.True);
            Assert.That(fontAtlases.All(atlas => atlas == null), Is.True);
            Assert.That(f.Queue.Ordens, Is.Empty);
            Assert.That(f.Presenter.EstadoAtual.Acoes, Is.Empty);
        }
        finally { if (cameraObject != null) Object.DestroyImmediate(cameraObject); }
    }

    // 03. Polish territorial conserva os símbolos e usa halo em pixels transparentes.
    [TestCase(WDTacticalPingKind.Attack, "FF334D")]
    [TestCase(WDTacticalPingKind.Defend, "268CFF")]
    [TestCase(WDTacticalPingKind.Focus, "39FF88")]
    [TestCase(WDTacticalPingKind.Transfer, "FFC94A")]
    [TestCase(WDTacticalPingKind.Attention, "A855F7")]
    public void TerritorialPolishKeepsOfficialColorAndSilhouetteContrast(WDTacticalPingKind kind, string hex)
    {
        Assert.That(ColorUtility.ToHtmlStringRGB(WDTacticalVisualStyle.Color(kind)), Is.EqualTo(hex));
        var texture = Resources.Load<Texture2D>(WDTacticalVisualStyle.MarkerAssetRoot + WDTacticalVisualStyle.IconName(kind));
        Assert.That(texture, Is.Not.Null);
        var pixels = texture.GetPixels32();
        Assert.That(pixels.Any(p => p.a > 30 && p.r < 25 && p.g < 25 && p.b < 25), Is.True,
            "Halo escuro acompanha a silhueta, inclusive em território da mesma cor.");
        Assert.That(pixels.Any(p => p.a > 0 && p.a < 200), Is.True, "Difusão translúcida.");
        Assert.That(texture.GetPixel(0, 0).a, Is.LessThan(.05f), "Canto transparente, sem fundo retangular.");
        string svg = System.IO.File.ReadAllText("Assets/Resources/" + WDTacticalVisualStyle.MarkerAssetRoot +
            WDTacticalVisualStyle.IconName(kind) + ".svg");
        Assert.That(svg, Does.Contain("#" + hex));
        Assert.That(svg, Does.Contain("stdDeviation=\"1\"")); // Núcleo de glow 2 px do ícone.
        Assert.That(svg, Does.Contain("stdDeviation=\"3.5\"")); // Difusão 7 px preservada.
        Assert.That(WDTacticalVisualStyle.MarkerIconCoreSize(kind), Is.InRange(27.5f, 30f));
    }

    // 04. O mesmo pipeline territorial serve mouse/touch; recebe a posição do gesto,
    // sem ler mouse global, repetir clique ou converter posteriormente a partir do centro.
    [TestCase(1920f, false, false)] [TestCase(2400f, false, true)]
    [TestCase(1920f, true, true)] [TestCase(2400f, true, false)]
    public void TacticalGesturePreservesClickedLocalAnchorThroughProjectionAndDelivery(float width, bool counterRoute, bool touch)
    {
        using var f = new Fixture(3, width);
        var previousManager = GameManager.instance;
        var previousCameras = Object.FindObjectsByType<Camera>().Select(c => c.gameObject).Distinct().ToArray();
        var cameraObject = new GameObject("AnchorCamera", typeof(Camera));
        var inputObject = new GameObject("AnchorInput");
        var previousPointer = UnityEngine.InputSystem.Pointer.current;
        UnityEngine.InputSystem.Pointer pointer = touch
            ? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>()
            : UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        var counterObject = new GameObject("Counter", typeof(BoxCollider2D), typeof(ContadorTropas));
        try
        {
            foreach (var previous in previousCameras) previous.SetActive(false);
            GameManager.instance = f.Manager;
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.pixelRect = new Rect(0, 0, width, 1080);
            f.Root.transform.position = new Vector3(width / 2f, 540, 0);
            var hit = f.TerritoryObject.AddComponent<BoxCollider2D>(); hit.size = Vector2.one * 4f;
            var counter = counterObject.GetComponent<ContadorTropas>();
            // Mesmo contrato de vínculo utilizado pela fixture de press/release existente.
            Set(counter, "territorio", f.Territory);
            var input = inputObject.AddComponent<InputPartida>();
            var route = typeof(InputPartida).GetMethod("ProcessarCliqueTerritorial", BindingFlags.NonPublic | BindingFlags.Instance);
            var samples = new[] { new Vector2(-.7f, -.4f), new Vector2(.8f, .6f) };
            int gestureIndex = 0;
            foreach (var sample in samples)
            {
                Assert.That(f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, WDTacticalPingKind.Defend), Is.True);
                var screen = camera.WorldToScreenPoint(f.Territory.transform.TransformPoint(sample));
                // Dispositivos de teste alimentam a mesma posição Pointer usada pelo Update.
                if (touch)
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent((UnityEngine.InputSystem.Touchscreen)pointer,
                        new UnityEngine.InputSystem.LowLevel.TouchState { touchId = 1, position = screen,
                            phase = gestureIndex == 0 ? UnityEngine.InputSystem.TouchPhase.Began : UnityEngine.InputSystem.TouchPhase.Moved });
                else
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent((UnityEngine.InputSystem.Mouse)pointer,
                        new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
                UnityEngine.InputSystem.InputSystem.Update();
                pointer.MakeCurrent();
                Vector2 gestureWorld = camera.ScreenToWorldPoint(UnityEngine.InputSystem.Pointer.current.position.ReadValue());
                gestureIndex++;
                route.Invoke(input, new object[] { new Collider2D[] { counterRoute ? counterObject.GetComponent<Collider2D>() : hit }, (Vector2?)gestureWorld });
            }
            var payloads = f.Info.Pings.ToArray();
            Assert.That(payloads.Length, Is.EqualTo(2));
            Assert.That(payloads[0].TerritoryLocalAnchor.HasValue, Is.True);
            Assert.That(payloads[0].TerritoryLocalAnchor, Is.Not.EqualTo(payloads[1].TerritoryLocalAnchor));
            for (int i = 0; i < 2; i++)
                Assert.That(Vector2.Distance((Vector2)payloads[i].TerritoryLocalAnchor.Value, samples[i]), Is.LessThan(.01f));
            var position = typeof(WDMatchInformationalOverlayController).GetMethod("PositionMarkers", BindingFlags.NonPublic | BindingFlags.Instance);
            Action assertPositions = () =>
            {
                position.Invoke(f.Info, new object[] { Time.unscaledTime });
                for (int i = 0; i < 2; i++)
                {
                    var marker = (RectTransform)f.Root.transform.Find("Tactical_" + f.Territory.idTerritorio + "_" + i);
                    Assert.That(MapOverlayProjection.TryWorldToOverlay((RectTransform)f.Root.transform,
                        f.Territory.transform.TransformPoint(payloads[i].TerritoryLocalAnchor.Value), camera, out var expected), Is.True);
                    Assert.That(Vector2.Distance(marker.anchoredPosition, expected), Is.LessThan(.1f), "Sem offset de centro/contador.");
                    Assert.That(marker.gameObject.activeInHierarchy, Is.True);
                }
            };
            assertPositions();
            f.Territory.transform.position = new Vector3(1, -.5f, 0);
            f.Territory.transform.localScale = new Vector3(1.3f, .8f, 1);
            f.Territory.transform.rotation = Quaternion.Euler(0, 0, 20);
            camera.orthographicSize *= 1.2f;
            assertPositions();
            f.Manager.jogadorLocal = TerritorioClique.Dono.Jogador3; f.Snapshot();
            Assert.That(f.Info.Pings, Is.Empty);
            foreach (var payload in payloads) Assert.That(f.Info.ReceiveTacticalPing(payload), Is.True);
            assertPositions();
            Assert.That(f.Info.Pings[0].TerritoryLocalAnchor, Is.EqualTo(payloads[0].TerritoryLocalAnchor));
            Assert.That(f.Territory.Tropas, Is.EqualTo(4)); Assert.That(f.Queue.Ordens, Is.Empty);
            Assert.That(f.Presenter.EstadoAtual.Acoes, Is.Empty);
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(pointer);
            if (previousPointer != null && previousPointer.added) previousPointer.MakeCurrent();
            Object.DestroyImmediate(counterObject); Object.DestroyImmediate(inputObject); Object.DestroyImmediate(cameraObject);
            foreach (var previous in previousCameras) if (previous != null) previous.SetActive(true);
            GameManager.instance = previousManager;
        }
    }

    [Test]
    public void TerritorialEntryDoesNotRestartWhenAnotherPingRebuildsPresentation()
    {
        using var f = new Fixture(3);
        var cameraObject = new GameObject("EntryCamera", typeof(Camera));
        try
        {
            Assert.That(f.Info.BeginTacticalPing(TerritorioClique.Dono.Jogador3, WDTacticalPingKind.Defend), Is.True);
            f.Manager.ClicarTerritorio(f.Territory);
            var expiry = (System.Collections.Generic.List<float>)typeof(WDMatchInformationalOverlayController)
                .GetField("pingExpiry", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f.Info);
            // O relógio visual do primeiro ping já está além da entrada.
            expiry[0] = Time.unscaledTime + WDMatchInformationalOverlayController.TacticalSignalDuration - .8f;
            Assert.That(f.Info.ReceiveTacticalPing(f.Info.Pings[0]), Is.True);
            var markers = f.Root.transform.Cast<Transform>().Where(t => t.name.StartsWith("Tactical_")).ToArray();
            Assert.That(markers.Length, Is.EqualTo(2));
            Assert.That(markers[0].localScale.x, Is.EqualTo(1f).Within(.001f));
            Assert.That(markers[1].localScale.x, Is.InRange(.89f, .95f));
            Assert.That(f.Queue.Ordens, Is.Empty);
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void TacticalSectorAssetsShareOpacityAndActionIdentityWithoutGeometryChanges()
    {
        var kinds = new[] { WDTacticalPingKind.Attention, WDTacticalPingKind.Transfer, WDTacticalPingKind.Focus,
            WDTacticalPingKind.Defend, WDTacticalPingKind.Attack };
        using var f = new Fixture(3);
        for (int i = 0; i < kinds.Length; i++)
        {
            string root = "Assets/Resources/UI/Match/FigmaCurrent/Hubs/";
            string sector = System.IO.File.ReadAllText(root + "TacticalSegment" + (i+1) + ".svg");
            string icon = System.IO.File.ReadAllText(root + WDTacticalVisualStyle.IconName(kinds[i]) + ".svg");
            string hex = "#" + ColorUtility.ToHtmlStringRGB(WDTacticalVisualStyle.Color(kinds[i]));
            Assert.That(sector, Does.Contain("stroke=\"" + hex + "\""));
            Assert.That(icon, Does.Contain(hex));
            Assert.That(sector, Does.Contain("fill-opacity=\"" + WDTacticalVisualStyle.SectorFillOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\""));
            var rect = (RectTransform)f.Root.transform.Find("TacticalRadial/TacticalSegment" + (i+1));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(198.739f, 155.821f)));
        }
        Assert.That(WDTacticalVisualStyle.SectorFillOpacity * .9f, Is.GreaterThan(.82f * .9f).And.LessThan(1f));
    }

    [Test]
    public void RadialHitAreasSelectOnlyTheirSectorAndHideIsNotASeventhReaction()
    {
        using var f=new Fixture();
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador1,f.Entry),Is.True);
        var root=(RectTransform)f.Root.transform.Find("EmojiRadial");
        var filters=root.GetComponentsInChildren<WDMatchRadialHit>();
        Assert.That(filters.Length,Is.EqualTo(7));
        for(int i=0;i<7;i++)
        {
            float angle=(90+(i+.5f)*180/7)*Mathf.Deg2Rad;
            Vector2 local=new Vector2(345.5f,-357.5f)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*230;
            Vector2 screen=root.TransformPoint(local);
            Assert.That(filters.Count(hit=>hit.IsRaycastLocationValid(screen,null)),Is.EqualTo(1));
            Assert.That(root.Find("SectorHit"+i).GetComponent<WDMatchRadialHit>().IsRaycastLocationValid(screen,null),Is.True);
        }
        Vector2 center=root.TransformPoint(new Vector2(345.5f,-357.5f));
        Assert.That(filters.Any(hit=>hit.IsRaycastLocationValid(center,null)),Is.False);
        int reactions=0;f.View.Reactions.Requested+=_=>reactions++;
        root.Find("SectorHit6").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.View.Reactions.Hidden,Is.True);Assert.That(reactions,Is.Zero);Assert.That(f.View.IsOpen,Is.False);
        f.View.OpenPlayer(TerritorioClique.Dono.Jogador1,f.Entry);
        Assert.That(f.View.SelectEmoji("😘"),Is.True);Assert.That(reactions,Is.EqualTo(1));
        Assert.That(f.Root.transform.Find("LocalKissReaction").gameObject.activeSelf,Is.False);
        f.View.OpenPlayer(TerritorioClique.Dono.Jogador1,f.Entry);
        f.Manager.faseAtual=GameManager.FaseTurno.Resolucao;f.Snapshot();
        Assert.That(f.View.IsOpen,Is.False,"Mudança de fase fecha o gesto transitório; poderá reabrir em Resolution.");
        Assert.That(f.View.OpenPlayer(TerritorioClique.Dono.Jogador1,f.Entry),Is.True);
        f.Root.SetActive(false);
        Assert.That(f.View.isActiveAndEnabled,Is.False);
        // MonoBehaviour sem ExecuteAlways: a fixture EditMode precisa entregar o
        // callback que o Player dispara ao desativar a hierarquia. Não substituir
        // por Close(): esta assertion deve continuar exercitando o cleanup real.
        typeof(WDMatchInteractionView).GetMethod("OnDisable",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.View,null);
        Assert.That(f.View.IsOpen,Is.False);Assert.That(f.Info.HasPendingTacticalIntent,Is.False);
    }
}
#endif

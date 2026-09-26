#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class WDMatchChatViewTests
{
    // 01. Fixture usa as autoridades reais com dados locais controlados pelo teste.
    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new("ChatTestSafeArea", typeof(RectTransform));
        public readonly GameObject Events = new("ChatTestEvents", typeof(EventSystem));
        public readonly WarDominionUITheme Theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        public readonly WDProfileRepository Profiles = new(null, new WDMemoryProfilePrivacyStore());
        public readonly WDChatRepository Repository;
        public readonly WDChatChannel Channel;
        public readonly WDMatchUIV2 View;
        public readonly WDMatchChatView Chat;
        public readonly WDMatchUtilityCommands Utilities;
        public readonly WDMatchUIPresentationBinding Binding;
        public readonly string FriendId;
        public bool RegionsOpen;

        public Fixture(bool withFriends = true)
        {
            ((RectTransform)Root.transform).sizeDelta = new Vector2(1920, 1080);
            FriendId = Profiles.AllProfiles.First(p => !p.IsLocalPlayer).PlayerId;
            var social = new WDSocialRepository(Profiles, withFriends
                ? new[] { new WDSocialMockSeed(FriendId, WDProfileSocialState.Friend, WDSocialPresence.Online) }
                : Array.Empty<WDSocialMockSeed>());
            Repository = new WDChatRepository(Profiles, social, () => WDSettingsData.Defaults(),
                () => new DateTimeOffset(2026, 9, 25, 21, 0, 0, TimeSpan.Zero));
            Channel = Repository.CreateMatchTeamChannel("CHAT-TEST", Repository.LocalPlayerId,
                new[] { (Repository.LocalPlayerId, 1), (FriendId, 1), ("Opponent", 2) });
            var viewObject = new GameObject("V2", typeof(RectTransform));
            viewObject.transform.SetParent(Root.transform, false);
            View = viewObject.AddComponent<WDMatchUIV2>();
            View.Build(Theme, null, null, null);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)viewObject.transform);
            Utilities = new WDMatchUtilityCommands(() => RegionsOpen = !RegionsOpen, () => RegionsOpen);
            Binding = Root.AddComponent<WDMatchUIPresentationBinding>();
            Binding.Configure(null, null, null, Utilities);
            Binding.ConfigureChat(Repository, Channel);
            Binding.Register(View);
            Chat = View.GetComponentInChildren<WDMatchChatView>(true);
            // 01.1. A fixture roda em EditMode, mas testa o ciclo de vida runtime.
            // Habilita callbacks somente nesta instância: SetActive deve executar
            // OnEnable/OnDisable para inscrever, atualizar e desinscrever o painel.
            // Não chama Refresh/OpenContent manualmente nem altera os contratos.
            Chat.runInEditMode = true;
        }

        public Button ChatButton => View.transform.Find("UtilityControls/Chat").GetComponent<Button>();
        public Transform Panel => Chat.transform.Find("Panel");
        public TMP_InputField Composer => Panel.Find("Columns/Conversation/Composer/Field/MessageInput").GetComponent<TMP_InputField>();
        public Button Send => Panel.Find("Columns/Conversation/Composer/Field/Send").GetComponent<Button>();
        public TMP_Text[] Labels => Chat.GetComponentsInChildren<TMP_Text>(true);

        public void Open() => ChatButton.onClick.Invoke();
        public void Dispose()
        {
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(Events);
            Object.DestroyImmediate(Theme);
        }
    }

    // 02. Um callback, uma autoridade e exclusividade real da apresentação V2.
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void LocalSetupMapsSocialIdentityAndRestrictsTeam(int teamSize)
    {
        using var f = new Fixture();
        var participants = Enumerable.Range(0, teamSize * 2).Select(i => new WDMatchParticipant(
            "session-player-" + i, "Participant " + i, i % 2, i, Color.white, "", "",
            i == 0 ? WDMatchParticipantKind.Local : WDMatchParticipantKind.Bot)).ToArray();
        var setup = new WDMatchSetup("test", "", "map", "", "", 20, false, 10, participants);
        var channel = f.Repository.CreateLocalMatchChannel(setup.MatchId, setup, null, TerritorioClique.Dono.Neutro, false);
        if (teamSize == 1) { Assert.That(channel, Is.Null); return; }
        Assert.That(channel.CanAccess(f.Repository.LocalPlayerId), Is.True);
        Assert.That(channel.Participants.Count, Is.EqualTo(teamSize));
        CollectionAssert.AreEquivalent(
            participants.Where(p => p.TeamIndex == 0).Select(p =>
                p.Kind == WDMatchParticipantKind.Local ? f.Repository.LocalPlayerId : p.PlayerId),
            channel.Participants);
        Assert.That(channel.CanAccess("session-player-1"), Is.False);
        Assert.That(f.Repository.Send(channel.Id, "session-player-1", "forbidden"), Is.False);
        f.Binding.ConfigureChat(f.Repository, channel);
        f.Open();
        // Rebinding preserva a conversa ativa da fixture; navegar seleciona o novo PARTIDA.
        Assert.That(f.Chat.SelectedChannel, Is.SameAs(f.Channel));
        f.Panel.Find("Columns/Left/Match").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Chat.SelectedChannel, Is.SameAs(channel));
        Assert.That(f.Composer.interactable, Is.True);
        f.Composer.text = "Equipe 😁❤️✌️";
        f.Composer.onSubmit.Invoke(f.Composer.text);
        Assert.That(channel.Messages.Count, Is.EqualTo(1));
        Assert.That(f.Channel.Messages, Is.Empty, "O envio deve atingir somente o canal selecionado.");
        Assert.That(channel.Messages.Last().Text, Is.EqualTo("Equipe 😁❤️✌️"));
        f.Composer.text = "rascunho";
        f.Chat.Select(f.Repository.ClanChannel);
        f.Chat.Select(channel);
        Assert.That(f.Composer.text, Is.EqualTo("rascunho"));
        f.Open(); f.Open();
        Assert.That(f.Composer.text, Is.EqualTo("rascunho"));
        Assert.That(channel.Messages.Last().Text, Is.EqualTo("Equipe 😁❤️✌️"));
    }

    [TestCase(2, true)]
    [TestCase(4, true)]
    [TestCase(6, true)]
    [TestCase(4, false)]
    public void DirectSceneUsesOnlySnapshotParticipants(int count, bool teams)
    {
        using var f = new Fixture();
        var players = Enumerable.Range(0, count).Select(i => new MatchUIPlayerState(
            (TerritorioClique.Dono)(i + 1), teams ? (i % 2 == 0 ? EquipesJogadores.Equipe.Vanguard : EquipesJogadores.Equipe.Sentinel)
                : EquipesJogadores.Equipe.Nenhuma, 1, i == 0, "Player", Color.white, MatchUIPlayerRoundStatus.Participante)).ToArray();
        var channel = f.Repository.CreateLocalMatchChannel("direct", null, players, TerritorioClique.Dono.Jogador1, teams);
        if (!teams || count == 2) { Assert.That(channel, Is.Null); return; }
        Assert.That(channel.Participants.Count, Is.EqualTo(count / 2));
        Assert.That(channel.CanAccess(f.Repository.LocalPlayerId), Is.True);
        Assert.That(channel.CanAccess("match:direct:slot:2"), Is.False);
    }

    [Test]
    public void FreeForAllSetupDoesNotCreateTeamChannel()
    {
        using var f = new Fixture();
        var participants = Enumerable.Range(0, 4).Select(i => new WDMatchParticipant(
            "ffa-" + i, "Player", i, i, Color.white, "", "",
            i == 0 ? WDMatchParticipantKind.Local : WDMatchParticipantKind.Bot)).ToArray();
        var setup = new WDMatchSetup("ffa", "", "map", "", "", 20, false, 10, participants);
        Assert.That(f.Repository.CreateLocalMatchChannel(setup.MatchId, setup, null,
            TerritorioClique.Dono.Jogador1, true), Is.Null);
    }

    [Test]
    public void ComposerSubmitAndButtonShareSingleValidatedUnicodeSend()
    {
        using var f = new Fixture();
        f.Root.AddComponent<Canvas>();
        f.Open();
        Assert.That(f.Composer.lineType, Is.EqualTo(TMP_InputField.LineType.SingleLine));
        Assert.That(f.Composer.textComponent.fontSize, Is.EqualTo(20));
        int before = f.Channel.Messages.Count;
        // Changed é síncrono: uma tentativa reentrante também não pode duplicar envio.
        f.Repository.Changed += f.Chat.SendDraft;
        f.Composer.text = "😁❤️";
        f.Composer.onSubmit.Invoke(f.Composer.text);
        f.Send.onClick.Invoke();
        f.Composer.onSubmit.Invoke("");
        Assert.That(f.Channel.Messages.Count, Is.EqualTo(before + 1));
        Assert.That(f.Channel.Messages.Last().Text, Is.EqualTo("😁❤️"));
        var body = f.Labels.First(t => t.name == "Body" && t.text == "😁❤️");
        Assert.That(body.fontSize, Is.EqualTo(20));
        body.ForceMeshUpdate();
        Assert.That(body.textInfo.characterInfo.Take(body.textInfo.characterCount).Count(c => c.elementType == TMP_TextElementType.Sprite), Is.EqualTo(2));
        f.Composer.text = "   "; f.Composer.onSubmit.Invoke("   ");
        Assert.That(f.Channel.Messages.Count, Is.EqualTo(before + 1));
        f.Composer.text = "Botão"; f.Send.onClick.Invoke();
        Assert.That(f.Channel.Messages.Count, Is.EqualTo(before + 2));
    }

    [Test]
    public void EmojiPickerStaysLocalClosesWithChatAndHistoryKeepsLiteralUnicode()
    {
        using var f = new Fixture();
        f.Root.AddComponent<Canvas>();
        f.Open();
        var picker = f.Chat.GetComponentInChildren<WDChatEmojiPicker>(true);
        int messageCount = f.Channel.Messages.Count;
        f.Composer.text = "Boa"; f.Composer.stringPosition = 3; picker.CaptureSelection();
        picker.Toggle();
        var layer = f.Panel.Find("EmojiPickerLayer");
        ExecuteEvents.ExecuteHierarchy(layer.Find("Popup").gameObject,
            new PointerEventData(f.Events.GetComponent<EventSystem>()), ExecuteEvents.pointerClickHandler);
        Assert.That(f.Chat.IsOpen, Is.True);
        Assert.That(picker.IsOpen, Is.True);
        Assert.That(picker.Choose("🔥"), Is.True);
        Assert.That(f.Composer.text, Is.EqualTo("Boa🔥"));
        Assert.That(f.Channel.Messages.Count, Is.EqualTo(messageCount));
        picker.Toggle();
        ExecuteEvents.ExecuteHierarchy(layer.gameObject, new PointerEventData(f.Events.GetComponent<EventSystem>()), ExecuteEvents.pointerClickHandler);
        Assert.That(picker.IsOpen, Is.False); Assert.That(f.Chat.IsOpen, Is.True);
        picker.Toggle(); f.Chat.Select(f.Repository.ClanChannel);
        Assert.That(picker.IsOpen, Is.False);
        f.Chat.Select(f.Channel); picker.Toggle(); f.Utilities.CloseActive();
        Assert.That(picker.IsOpen, Is.False);
        f.Repository.ReceiveLocalMock(f.Channel.Id, f.FriendId, "Olá 😀❤️ <b>literal</b>");
        f.Open();
        var body = f.Labels.First(t => t.text == "Olá 😀❤️ <b>literal</b>");
        Assert.That(body.richText, Is.False);
        Assert.That(body.font.fallbackFontAssetTable, Is.Not.Empty);
        Assert.That(body.spriteAsset, Is.SameAs(WDChatEmojiColor.SpriteAsset));
        // PreferredValues mede, mas não finaliza textInfo.characterCount.
        // Materializar a malha real como faria o Canvas em runtime.
        body.ForceMeshUpdate();
        Assert.That(body.textInfo.characterInfo.Take(body.textInfo.characterCount).Count(c => c.elementType == TMP_TextElementType.Sprite), Is.EqualTo(2));
        Assert.That(body.text, Is.EqualTo("Olá 😀❤️ <b>literal</b>"));
        f.Repository.ReceiveLocalMock(f.Channel.Id, f.FriendId, "Atualização do histórico");
        var rebuilt = f.Labels.First(t => t.text == "Olá 😀❤️ <b>literal</b>");
        Assert.That(rebuilt, Is.Not.SameAs(body));
        rebuilt.ForceMeshUpdate();
        Assert.That(rebuilt.richText, Is.False);
        Assert.That(rebuilt.spriteAsset, Is.SameAs(WDChatEmojiColor.SpriteAsset));
        Assert.That(rebuilt.textInfo.characterInfo.Take(rebuilt.textInfo.characterCount).Count(c => c.elementType == TMP_TextElementType.Sprite), Is.EqualTo(2));
        f.Composer.text = "Boa partida 😁🔥";
        f.Send.onClick.Invoke();
        Assert.That(f.Channel.Messages.Last().Text, Is.EqualTo("Boa partida 😁🔥"));
        f.Panel.Find("Columns/Friends/FriendsList/Content/Friend_" + f.FriendId).GetComponent<Button>().onClick.Invoke();
        f.Composer.text = "Boa partida 😁🔥";
        f.Send.onClick.Invoke();
        Assert.That(f.Chat.SelectedChannel.Messages.Last().Text, Is.EqualTo("Boa partida 😁🔥"));
        var preview = f.Labels.First(t => t.name == "Preview" && t.text == "Boa partida 😁🔥");
        Assert.That(preview.spriteAsset, Is.SameAs(WDChatEmojiColor.SpriteAsset));
        Assert.That(preview.richText, Is.False);
        Assert.That(preview.gameObject.activeInHierarchy, Is.True);
        Assert.That(preview.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(preview.rectTransform.rect.height,
            Is.GreaterThanOrEqualTo(preview.GetPreferredValues(float.PositiveInfinity, float.PositiveInfinity).y));
        preview.ForceMeshUpdate();
        Assert.That(preview.textInfo.characterInfo.Take(preview.textInfo.characterCount).Count(c => c.elementType == TMP_TextElementType.Sprite), Is.EqualTo(2));
        Assert.That(preview.textInfo.characterInfo.Take(preview.textInfo.characterCount)
            .Count(c => c.elementType == TMP_TextElementType.Sprite && c.isVisible), Is.EqualTo(2));
        Assert.That(preview.isTextTruncated, Is.False);
        Assert.That(preview.text, Is.EqualTo("Boa partida 😁🔥"));
    }

    [Test]
    public void InterfaceFontOwnsResourcesAcrossConsumerSceneTeardown()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        TMP_FontAsset font = null;
        Material material = null;
        Texture2D atlas = null;
        try
        {
            using var first = new Fixture();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(first.Root, scene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(first.Events, scene);
            var label = first.View.GetComponentsInChildren<TMP_Text>(true)
                .First(t => t.font.name == "Gabarito SemiBold Runtime");
            font = label.font;
            label.GetPreferredValues();
            material = font.material;
            atlas = font.atlasTextures[0];
            Assert.That(material.hideFlags, Is.EqualTo(HideFlags.DontSave));
            foreach (var texture in font.atlasTextures.Take(font.atlasTextureCount))
                Assert.That(texture.hideFlags, Is.EqualTo(HideFlags.DontSave));
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        using var second = new Fixture();
        second.Root.AddComponent<Canvas>();
        var consumer = second.View.GetComponentsInChildren<TMP_Text>(true)
            .First(t => t.font.name == "Gabarito SemiBold Runtime" && t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text));
        Assert.That(consumer.font, Is.SameAs(font));
        Assert.That(material != null && atlas != null, Is.True);
        Assert.That(font.material, Is.SameAs(material));
        Assert.That(font.atlasTextures[0], Is.SameAs(atlas));
        Assert.That(material.mainTexture, Is.SameAs(atlas));
        consumer.ForceMeshUpdate();
        Assert.That(consumer.textInfo.characterCount, Is.GreaterThan(0));
    }

    [Test]
    public void RebindingDoesNotDuplicateChatCallbackAndToggleReturnsToNone()
    {
        using var f = new Fixture();
        f.Binding.Register(f.View);
        f.View.BindUtilities(f.Utilities);
        f.Open();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Chat));
        Assert.That(f.Chat.IsOpen, Is.True);
        Assert.That(f.Chat.SelectedChannel, Is.SameAs(f.Channel));
        f.Open();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        Assert.That(f.Chat.IsOpen, Is.False);
    }

    [TestCase(WDMatchUtilityModule.Menu)]
    [TestCase(WDMatchUtilityModule.Regions)]
    public void ChatAndExistingUtilityAreExclusiveInBothDirections(WDMatchUtilityModule module)
    {
        using var f = new Fixture();
        f.Utilities.Toggle(module);
        f.Open();
        Assert.That(f.Chat.IsOpen, Is.True);
        Assert.That(f.RegionsOpen, Is.False);
        Assert.That(f.View.transform.Find("MenuPlayersModal").gameObject.activeSelf, Is.False);
        f.Utilities.Toggle(module);
        Assert.That(f.Chat.IsOpen, Is.False);
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(module));
        Assert.That(f.RegionsOpen, Is.EqualTo(module == WDMatchUtilityModule.Regions));
        Assert.That(f.View.transform.Find("MenuPlayersModal").gameObject.activeSelf,
            Is.EqualTo(module == WDMatchUtilityModule.Menu));
        f.Utilities.CloseActive();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        Assert.That(f.RegionsOpen, Is.False);
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void MouseAndTouchPanelClicksStayOpenAndBackdropCloses(int pointerId)
    {
        using var f = new Fixture();
        f.Open();
        var pointer = new PointerEventData(f.Events.GetComponent<EventSystem>()) { pointerId = pointerId };
        GameObject receiver = ExecuteEvents.ExecuteHierarchy(f.Panel.Find("Columns/Conversation/Header").gameObject,
            pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(receiver, Is.EqualTo(f.Panel.gameObject));
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Chat));
        ExecuteEvents.Execute(f.Chat.transform.Find("Backdrop").gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        Assert.That(f.Chat.IsOpen, Is.False);
    }

    [Test]
    public void BackdropOnlyPassesExistingUtilityTargetsAndStillCoversMap()
    {
        using var f = new Fixture();
        f.Open();
        var filter = f.Chat.GetComponentInChildren<WDMatchChatBackdropFilter>();
        Vector2 chatPoint = RectTransformUtility.WorldToScreenPoint(null, f.ChatButton.transform.position);
        Vector2 panelPoint = RectTransformUtility.WorldToScreenPoint(null, f.Panel.position);
        Assert.That(filter.IsRaycastLocationValid(chatPoint, null), Is.False);
        Assert.That(filter.IsRaycastLocationValid(panelPoint, null), Is.True);
        Assert.That(filter.IsRaycastLocationValid(new Vector2(-20000, -20000), null), Is.True);
        Assert.That(filter.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(f.Panel.GetComponent<Image>().raycastTarget, Is.True);
    }

    // 03. Histórico/composer reais, preservação de draft e atualização de unread.
    [Test]
    public void ComposerSendsOnceThroughRepositoryAndIncomingDataPreservesDraft()
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "Ordem dinâmica 47";
        Assert.That(f.Send.interactable, Is.True);
        f.Send.onClick.Invoke();
        Assert.That(f.Channel.Messages.Count, Is.EqualTo(1));
        Assert.That(f.Channel.Messages[0].SenderId, Is.EqualTo(f.Repository.LocalPlayerId));
        Assert.That(f.Channel.Messages[0].Text, Is.EqualTo("Ordem dinâmica 47"));
        Assert.That(f.Composer.text, Is.Empty);
        f.Composer.text = "Rascunho preservado";
        f.Repository.ReceiveLocalMock(f.Channel.Id, f.FriendId, "Resposta externa 91");
        Assert.That(f.Composer.text, Is.EqualTo("Rascunho preservado"));
        Assert.That(f.Channel.IsUnread, Is.False);
        CollectionAssert.Contains(f.Labels.Select(t => t.text).ToArray(), "Resposta externa 91");
        Assert.That(f.Labels.Any(t => t.name == "Timestamp" && t.text.Contains(f.Profiles.Find(f.FriendId).Nickname)), Is.True);
        Assert.That(f.Labels.Any(t => t.text.Contains("Entregue")), Is.False);
    }

    [Test]
    public void EmptyMessagesCannotSendAndEndMatchDisablesComposer()
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "   ";
        Assert.That(f.Send.interactable, Is.False);
        f.Send.onClick.Invoke();
        Assert.That(f.Channel.Messages, Is.Empty);
        f.Repository.EndMatch("CHAT-TEST");
        f.Composer.text = "Não pode enviar";
        f.Send.onClick.Invoke();
        Assert.That(f.Composer.interactable, Is.False);
        Assert.That(f.Channel.Messages, Is.Empty);
    }

    [Test]
    public void HiddenChatDoesNotConsumeUnreadAndReopeningRefreshesWithoutLosingDraft()
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "Vou continuar depois";
        f.Utilities.CloseActive();
        f.Repository.ReceiveLocalMock(f.Channel.Id, f.FriendId, "Chegou enquanto fechado");
        Assert.That(f.Channel.IsUnread, Is.True);
        f.Open();
        Assert.That(f.Channel.IsUnread, Is.False);
        Assert.That(f.Composer.text, Is.EqualTo("Vou continuar depois"));
        CollectionAssert.Contains(f.Labels.Select(t => t.text).ToArray(), "Chegou enquanto fechado");
    }

    [Test]
    public void FriendsAndPrivateConversationBindToSocialAuthority()
    {
        using var f = new Fixture();
        f.Open();
        Transform friends = f.Panel.Find("Columns/Friends/FriendsList/Content");
        Button friend = friends.Find("Friend_" + f.FriendId).GetComponent<Button>();
        Assert.That(friend.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(f.Profiles.Find(f.FriendId).Nickname));
        friend.onClick.Invoke();
        Assert.That(f.Chat.SelectedChannel.Type, Is.EqualTo(WDChatChannelType.Private));
        Assert.That(f.Repository.Channels, Does.Contain(f.Chat.SelectedChannel));
        f.Composer.text = "Conversa recente real";
        f.Send.onClick.Invoke();
        Assert.That(f.Panel.Find("Columns/Left/Recents/Content/Recent_" + f.Chat.SelectedChannel.Id), Is.Not.Null);
    }

    [Test]
    public void MissingFriendsAndMatchChannelDoNotFabricateData()
    {
        using var f = new Fixture(false);
        f.View.BindChat(f.Repository, null);
        f.Chat.Select(null);
        f.Open();
        Transform friends = f.Panel.Find("Columns/Friends/FriendsList/Content");
        Assert.That(friends.GetComponentsInChildren<Button>(), Is.Empty);
        Assert.That(f.Panel.Find("Columns/Friends/Count").GetComponent<TMP_Text>().text,
            Is.EqualTo("0 companheiros ativos"));
        Assert.That(f.Composer.interactable, Is.False);
        Assert.That(f.Panel.Find("Columns/Left/Support").GetComponent<Button>().interactable, Is.False);
        Assert.That(f.Panel.Find("Columns/Friends/DiscordCommunity").GetComponent<Button>().interactable, Is.False);
    }

    [Test]
    public void HistoryGrowsAndLiteralPlayerTextCannotInjectRichText()
    {
        using var f = new Fixture();
        for (int i = 0; i < 15; i++) f.Repository.Send(f.Channel.Id, f.FriendId, "<size=100>Mensagem " + i + "</size>");
        f.Open();
        ScrollRect scroll = f.Panel.Find("Columns/Conversation/Messages").GetComponent<ScrollRect>();
        Assert.That(scroll.content.childCount, Is.EqualTo(15));
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        Assert.That(scroll.content.GetComponentsInChildren<TMP_Text>().All(t => !t.richText), Is.True);
    }

    // 03.1. Navegação não se confunde com disponibilidade de envio do canal.
    [TestCase("available")]
    [TestCase("unavailable")]
    [TestCase("missing")]
    public void MatchToClanToMatchRestoresBoundConversationAndDraft(string availability)
    {
        using var f = new Fixture();
        WDChatChannel clan = f.Repository.ClanChannel;
        Assert.That(clan, Is.Not.Null);
        Assert.That(clan.CanAccess(f.Repository.LocalPlayerId), Is.True);
        f.Repository.Send(f.Channel.Id, f.FriendId, "Histórico da partida");
        f.Repository.Send(clan.Id, f.Repository.LocalPlayerId, "Histórico do clã");
        WDChatChannel expectedMatch = availability == "missing" ? null : f.Channel;
        if (availability == "missing")
        {
            f.View.BindChat(f.Repository, null);
            f.Chat.Select(null);
        }
        f.Open();
        if (availability != "missing") f.Composer.text = "Rascunho da partida";
        if (availability == "unavailable") f.Repository.EndMatch("CHAT-TEST");
        Button match = f.Panel.Find("Columns/Left/Match").GetComponent<Button>();
        Button clanButton = f.Panel.Find("Columns/Left/Clan").GetComponent<Button>();
        var pointer = new PointerEventData(f.Events.GetComponent<EventSystem>());
        ExecuteEvents.Execute(clanButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(f.Chat.SelectedChannel, Is.SameAs(clan));
        Assert.That(f.Composer.interactable, Is.True);
        f.Composer.text = "Rascunho do clã";
        Assert.That(match.interactable, Is.True, "Navegar não pode depender da permissão de envio.");
        ExecuteEvents.Execute(match.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(f.Chat.SelectedChannel, Is.SameAs(expectedMatch));
        Assert.That(f.Panel.Find("Columns/Conversation/Header/Name").GetComponent<TMP_Text>().text,
            Is.EqualTo(expectedMatch?.DisplayName ?? "PARTIDA"));
        Assert.That(match.GetComponent<Image>().color.a, Is.GreaterThan(0));
        Assert.That(clanButton.GetComponent<Image>().color.a, Is.Zero);
        Assert.That(f.Composer.interactable, Is.EqualTo(availability == "available"));
        Assert.That(f.Composer.text, Is.EqualTo(availability == "missing" ? string.Empty : "Rascunho da partida"));
        string[] history = f.Panel.Find("Columns/Conversation/Messages/Content")
            .GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Body").Select(t => t.text).ToArray();
        Assert.That(history, Does.Not.Contain("Histórico do clã"));
        if (availability == "available") CollectionAssert.Contains(history, "Histórico da partida");
        else Assert.That(history, Is.Empty);
        ExecuteEvents.Execute(clanButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(f.Composer.text, Is.EqualTo("Rascunho do clã"));
        ExecuteEvents.Execute(match.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(f.Chat.SelectedChannel, Is.SameAs(expectedMatch));
    }

    // 04. Bounds reais dos controles dentro do painel e da Safe Area.
    [TestCase(1920f, 1080f)]
    [TestCase(2280f, 1080f)]
    [TestCase(1280f, 720f)]
    public void PanelAndEssentialControlsFitAvailableSafeArea(float width, float height)
    {
        using var f = new Fixture();
        ((RectTransform)f.Root.transform).sizeDelta = new Vector2(width, height);
        f.Open();
        f.Chat.FitSafeArea();
        RectTransform safe = (RectTransform)f.Root.transform;
        foreach (RectTransform rect in new[] { f.Chat.Panel, (RectTransform)f.Composer.transform,
            (RectTransform)f.Send.transform, (RectTransform)f.Panel.Find("Columns/Friends/ViewAll") })
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = safe.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(safe.rect.xMin, safe.rect.xMax));
                Assert.That(local.y, Is.InRange(safe.rect.yMin, safe.rect.yMax));
            }
        }
        Assert.That(f.Chat.Panel.localScale.x, Is.EqualTo(f.Chat.Panel.localScale.y));
    }
}
#endif

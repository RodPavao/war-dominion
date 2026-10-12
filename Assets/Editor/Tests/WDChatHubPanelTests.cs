#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class WDChatHubPanelTests
{
    // ============================================================
    // 01. AUTORIDADES REAIS; LIFECYCLE EDITMODE SEM DESTRUIR CACHE TMP
    // ============================================================
    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new("GeneralChatFixture", typeof(RectTransform));
        public readonly GameObject Events = new("GeneralChatEvents", typeof(EventSystem));
        public readonly WDProfileRepository Profiles = new(null, new WDMemoryProfilePrivacyStore());
        public readonly WDSocialRepository Social;
        public readonly WDSettingsData Settings = WDSettingsData.Defaults();
        public readonly WDChatRepository Repository;
        public readonly WDNavigationStack Navigation = new();
        public readonly WDChatHubPanel Hub;
        public readonly WDChatChannel Private;
        public readonly string FriendId;
        public string ProfileOpened;
        public int FriendsOpened;
        private int minute;
        private readonly string[] originalRecents;

        public Fixture(bool friends = true, bool allFriends = false, Texture2D background = null)
        {
            ((RectTransform)Root.transform).sizeDelta = new Vector2(1920, 1080);
            FriendId = Profiles.AllProfiles.First(p => !p.IsLocalPlayer).PlayerId;
            Social = new WDSocialRepository(Profiles, allFriends
                ? Profiles.AllProfiles.Where(p => !p.IsLocalPlayer).Select(p => new WDSocialMockSeed(p.PlayerId, WDProfileSocialState.Friend, WDSocialPresence.Online)).ToArray()
                : friends
                ? new[] { new WDSocialMockSeed(FriendId, WDProfileSocialState.Friend, WDSocialPresence.Online) }
                : Array.Empty<WDSocialMockSeed>());
            Repository = new WDChatRepository(Profiles, Social, () => Settings,
                () => new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero).AddMinutes(++minute));
            Private = Repository.OpenPrivate(FriendId);
            Navigation.SetRoot(new WDNavigationContext("home", _ => { }));
            Hub = Root.AddComponent<WDChatHubPanel>();
            Hub.Build(null, Repository, Social, Profiles, Navigation,
                id => { ProfileOpened = id; Navigation.Push(new WDNavigationContext("profile", _ => { })); },
                (_, __, accept) => accept(),
                () => { FriendsOpened++; Navigation.Push(new WDNavigationContext("friends", _ => { })); }, background);
            // Ativa os callbacks reais da view nesta fixture; não chama Refresh manualmente.
            Hub.runInEditMode = true;
            originalRecents = Picker.Recents.ToArray();
        }
        public Transform Columns => Root.transform.Find("ChatContent/Panel/ColumnsSurface");
        public TMP_InputField Composer => Columns.Find("Conversation/Composer/Field/MessageInput").GetComponent<TMP_InputField>();
        public Button Send => Columns.Find("Conversation/Composer/Field/Send").GetComponent<Button>();
        public WDChatEmojiPicker Picker => Hub.GetComponentInChildren<WDChatEmojiPicker>(true);
        public ScrollRect Messages => Columns.Find("Conversation/Messages").GetComponent<ScrollRect>();
        public ScrollRect Recents => Columns.Find("Channels/Recents").GetComponent<ScrollRect>();
        public ScrollRect Friends => Columns.Find("OnlineFriends/FriendsList").GetComponent<ScrollRect>();
        public TMP_Text[] Labels => Hub.GetComponentsInChildren<TMP_Text>(true);
        public void Open(bool select = true) => Hub.OpenFromHome(select ? FriendId : null);
        public void Dispose()
        {
            // Recentes são compartilhados em produção, mas o teste não deixa sua escolha na sessão.
            var recents = (IList<string>)Picker.Recents;
            recents.Clear();
            foreach (string value in originalRecents) recents.Add(value);
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(Events);
            // Fontes, atlas e materiais pertencem às autoridades compartilhadas.
        }
    }

    // ============================================================
    // 02. CANAIS SOCIAIS, DRAFTS, LEITURA E ENVIO ÚNICO
    // ============================================================
    [Test]
    public void GeneralContextFiltersMatchWithoutChangingRepository()
    {
        using var f = new Fixture();
        WDChatChannel match = f.Repository.CreateMatchTeamChannel("general-context", f.Repository.LocalPlayerId,
            new[] { (f.Repository.LocalPlayerId, 1), (f.FriendId, 1), ("enemy", 2) });
        f.Open();
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Private));
        f.Hub.Select(match);
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Private));
        Assert.That(f.Labels.Any(t => t.text == "PARTIDA"), Is.False);
        f.Columns.Find("Channels/Clan").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Repository.ClanChannel));
        Assert.That(match.IsAvailable, Is.True);
        Assert.That(match.Participants, Does.Not.Contain("enemy"));
    }

    [Test]
    public void DraftsSurviveChannelSwitchIncomingSocialRefreshAndReopen()
    {
        using var f = new Fixture();
        f.Open();
        TMP_InputField original = f.Composer;
        f.Composer.text = "Privado 😎";
        f.Hub.Select(f.Repository.ClanChannel);
        f.Composer.text = "Clã ❤️";
        f.Repository.ReceiveLocalMock(f.Private.Id, f.FriendId, "Chegou");
        f.Social.RemoveFriend(f.FriendId);
        Assert.That(f.Composer, Is.SameAs(original));
        Assert.That(f.Composer.text, Is.EqualTo("Clã ❤️"));
        f.Hub.Select(f.Private);
        Assert.That(f.Composer.text, Is.EqualTo("Privado 😎"));
        Assert.That(f.Navigation.TryBack(), Is.True);
        Assert.That(f.Hub.IsOpen, Is.False);
        f.Open(false);
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Private));
        Assert.That(f.Composer.text, Is.EqualTo("Privado 😎"));
        f.Hub.Select(f.Repository.ClanChannel);
        Assert.That(f.Composer.text, Is.EqualTo("Clã ❤️"));
    }

    [Test]
    public void OnlyVisibleSelectedConversationConsumesUnread()
    {
        using var f = new Fixture();
        WDChatChannel clan = f.Repository.ClanChannel;
        string ally = clan.Participants.First(p => p != f.Repository.LocalPlayerId);
        f.Repository.ReceiveLocalMock(f.Private.Id, f.FriendId, "Privado");
        f.Repository.ReceiveLocalMock(clan.Id, ally, "Clã");
        f.Open(false);
        Assert.That(f.Private.IsUnread, Is.True);
        Assert.That(clan.IsUnread, Is.True);
        f.Hub.Select(f.Private);
        Assert.That(f.Private.IsUnread, Is.False);
        Assert.That(clan.IsUnread, Is.True);
        f.Hub.HideFromNavigation();
        f.Repository.ReceiveLocalMock(f.Private.Id, f.FriendId, "Oculto");
        Assert.That(f.Private.IsUnread, Is.True);
        f.Navigation.TryBack();
        f.Open(false);
        Assert.That(f.Private.IsUnread, Is.False);
        Assert.That(clan.IsUnread, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EnterAndButtonSendOnceAndPreserveOtherDraft(bool enterFirst)
    {
        using var f = new Fixture();
        f.Open();
        f.Hub.Select(f.Repository.ClanChannel);
        f.Composer.text = "outro draft";
        f.Hub.Select(f.Private);
        f.Composer.text = "Boa 😁❤️✌️ <b>literal</b>";
        if (enterFirst) { f.Composer.onSubmit.Invoke(f.Composer.text); f.Send.onClick.Invoke(); }
        else { f.Send.onClick.Invoke(); f.Composer.onSubmit.Invoke(f.Composer.text); }
        Assert.That(f.Private.Messages.Count, Is.EqualTo(1));
        Assert.That(f.Private.Messages[0].Text, Is.EqualTo("Boa 😁❤️✌️ <b>literal</b>"));
        Assert.That(f.Composer.text, Is.Empty);
        Assert.That(f.Send.interactable, Is.False);
        f.Hub.Select(f.Repository.ClanChannel);
        Assert.That(f.Composer.text, Is.EqualTo("outro draft"));
    }

    [Test]
    public void EmptyAndRejectedMessagesDoNotSendOrEraseDraft()
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "   ";
        f.Send.onClick.Invoke();
        f.Composer.onSubmit.Invoke(f.Composer.text);
        Assert.That(f.Private.Messages, Is.Empty);
        f.Composer.text = "Preservar";
        f.Settings.AllowNonFriendMessages = false;
        f.Social.RemoveFriend(f.FriendId);
        f.Send.onClick.Invoke();
        Assert.That(f.Private.Messages, Is.Empty);
        Assert.That(f.Composer.text, Is.EqualTo("Preservar"));
        f.Social.Block(f.FriendId);
        Assert.That(f.Composer.interactable, Is.False);
        Assert.That(f.Send.interactable, Is.False);
    }

    // ============================================================
    // 03. NAVEGAÇÃO REAL, BUSCA, PRESENÇA E CONTROLES INDISPONÍVEIS
    // ============================================================
    [TestCase("profile")]
    [TestCase("friends")]
    public void GlobalNavigationHidesChatAndRestoresSelectionAndDraft(string destination)
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "Continuar depois";
        string path = destination == "profile" ? "Conversation/Header/ViewProfile" : "OnlineFriends/ViewAll";
        f.Columns.Find(path).GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Navigation.CurrentId, Is.EqualTo(destination));
        Assert.That(f.Hub.IsOpen, Is.False);
        f.Repository.ReceiveLocalMock(f.Private.Id, f.FriendId, "Enquanto estava fora");
        Assert.That(f.Private.IsUnread, Is.True);
        Assert.That(f.Navigation.TryBack(), Is.True);
        Assert.That(f.Hub.IsOpen, Is.True);
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Private));
        Assert.That(f.Composer.text, Is.EqualTo("Continuar depois"));
        Assert.That(f.Private.IsUnread, Is.False);
        Assert.That(destination == "profile" ? f.ProfileOpened == f.FriendId : f.FriendsOpened == 1, Is.True);
        f.Root.transform.Find("ChatContent/Header/Back").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Navigation.CurrentId, Is.EqualTo("home"));
        Assert.That(f.Hub.IsOpen, Is.False);
    }

    [Test]
    public void SearchUsesSocialIdentityAndOptionsUseRepositoryAuthorities()
    {
        using var f = new Fixture();
        f.Open(false);
        f.Columns.Find("Channels/NewConversation").GetComponent<Button>().onClick.Invoke();
        Transform search = f.Root.transform.Find("ChatContent/NewConversationPanel");
        search.Find("SearchInput").GetComponent<TMP_InputField>().text = f.FriendId;
        search.Find("Open").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Private));
        Assert.That(search.gameObject.activeSelf, Is.False);
        f.Columns.Find("Conversation/Header/Options").GetComponent<Button>().onClick.Invoke();
        Transform menu = f.Root.transform.Find("ChatContent/ConversationMenu");
        menu.Find("Mute").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Private.IsMuted, Is.True);
        f.Columns.Find("Conversation/Header/Options").GetComponent<Button>().onClick.Invoke();
        menu.Find("Block").GetComponent<Button>().onClick.Invoke();
        Assert.That(f.Private.IsAvailable, Is.False);
        Assert.That(f.Composer.interactable, Is.False);
    }

    [Test]
    public void FriendsComeFromSocialAndUnsupportedActionsRemainDisabled()
    {
        using var f = new Fixture();
        f.Open(false);
        Assert.That(f.Friends.content.GetComponentsInChildren<TMP_Text>().Any(t => t.text == f.Profiles.Find(f.FriendId).Nickname), Is.True);
        f.Friends.content.GetComponentInChildren<Button>().onClick.Invoke();
        Assert.That(f.Hub.SelectedChannel, Is.SameAs(f.Private));
        foreach (string path in new[] { "Channels/Support", "Conversation/Composer/Field/Attachment", "OnlineFriends/Discord/DiscordCommunity" })
            Assert.That(f.Columns.Find(path).GetComponent<Button>().interactable, Is.False, path);
        f.Social.RemoveFriend(f.FriendId);
        Assert.That(f.Friends.content.GetComponentsInChildren<Button>(), Is.Empty);
        Assert.That(f.Columns.Find("OnlineFriends/Count").GetComponent<TMP_Text>().text, Is.EqualTo("0 companheiros ativos"));
    }

    [Test]
    public void MissingClanAndFriendsDoNotFabricateData()
    {
        using var f = new Fixture(false);
        f.Repository.LeaveClan();
        f.Open(false);
        Assert.That(f.Columns.Find("Channels/Clan").GetComponent<Button>().interactable, Is.False);
        Assert.That(f.Friends.content.GetComponentsInChildren<Button>(), Is.Empty);
        Assert.That(f.Composer.interactable, Is.False);
        Assert.That(f.Repository.RecentPrivate, Is.Empty);
    }

    // ============================================================
    // 04. PICKER COMPARTILHADO, UNICODE E MALHA TMP REAL
    // ============================================================
    [Test]
    public void SharedPickerInsertsAtSelectionAndHistoryAndPreviewRenderColor()
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "AB";
        f.Composer.selectionStringAnchorPosition = 1;
        f.Composer.selectionStringFocusPosition = 2;
        f.Picker.CaptureSelection();
        f.Picker.Toggle();
        Assert.That(f.Picker.Choose("❤️"), Is.True);
        Assert.That(f.Composer.text, Is.EqualTo("A❤️"));
        Assert.That(f.Picker.Recents, Does.Contain("❤️"));
        Assert.That(f.Private.Messages, Is.Empty);
        f.Composer.text = "Olá 😁❤️ <b>literal</b>";
        f.Send.onClick.Invoke();
        Assert.That(f.Private.Messages.Single().Text, Is.EqualTo("Olá 😁❤️ <b>literal</b>"));
        // Canvas real para finalizar malha; nunca troca assertion por inspeção do texto lógico.
        f.Root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text label in f.Labels.Where(t => t.gameObject.activeInHierarchy && (t.name == "Body" || t.name == "Preview") && t.text.Contains("😁")))
        {
            Assert.That(label.richText, Is.False);
            Assert.That(label.spriteAsset, Is.SameAs(WDChatEmojiColor.SpriteAsset));
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(c => c.elementType == TMP_TextElementType.Sprite), Is.EqualTo(2));
        }
        Assert.That(f.Labels.Count(t => t.gameObject.activeInHierarchy && (t.name == "Body" || t.name == "Preview") && t.text.Contains("😁")), Is.EqualTo(2));
        f.Picker.Toggle();
        Assert.That(f.Hub.TryHandleLocalBack(), Is.True);
        Assert.That(f.Picker.IsOpen, Is.False);
        Assert.That(f.Hub.IsOpen, Is.True);
        f.Picker.Toggle();
        f.Hub.Select(f.Repository.ClanChannel);
        Assert.That(f.Picker.IsOpen, Is.False);
        f.Picker.Toggle();
        f.Hub.HideFromNavigation();
        Assert.That(f.Picker.IsOpen, Is.False);
    }

    // ============================================================
    // 05. SCROLL, POOL DE LINHAS E SAFE AREA 16:9 / 20:9
    // ============================================================
    [Test]
    public void HistoryGrowsWithoutCreatingOneObjectPerMessageAndScrollsIndependently()
    {
        using var f = new Fixture();
        for (int i = 0; i < 100; i++) f.Repository.ReceiveLocalMock(f.Private.Id, f.FriendId, "Mensagem " + i);
        f.Open();
        Assert.That(f.Messages.content.rect.height, Is.GreaterThan(f.Messages.viewport.rect.height));
        Assert.That(f.Messages.content.childCount, Is.LessThan(20));
        Assert.That(f.Labels.Any(t => t.gameObject.activeInHierarchy && t.text == "Mensagem 99"), Is.True);
        f.Messages.verticalNormalizedPosition = 1;
        f.Messages.onValueChanged.Invoke(Vector2.one);
        Assert.That(f.Labels.Any(t => t.gameObject.activeInHierarchy && t.text == "Mensagem 0"), Is.True);
        float y = f.Messages.content.anchoredPosition.y;
        f.Repository.ReceiveLocalMock(f.Private.Id, f.FriendId, "Mensagem 100");
        Assert.That(f.Messages.content.anchoredPosition.y, Is.EqualTo(y).Within(.01));
        Assert.That(f.Recents.content.anchoredPosition.y, Is.Zero);
        Assert.That(f.Friends.content.anchoredPosition.y, Is.Zero);
        Assert.That(new[] { f.Messages, f.Recents, f.Friends }.All(s => s.vertical && !s.horizontal && s.viewport.GetComponent<RectMask2D>() != null), Is.True);
    }

    [Test]
    public void RecentsAreNotLimitedToSixAndLastConversationRemainsReachable()
    {
        using var f = new Fixture();
        foreach (WDProfileData profile in f.Profiles.AllProfiles.Where(p => !p.IsLocalPlayer))
        {
            WDChatChannel channel = f.Repository.OpenPrivate(profile.PlayerId);
            if (channel != null) f.Repository.Send(channel.Id, f.Repository.LocalPlayerId, "Olá " + profile.Nickname);
        }
        Assert.That(f.Repository.RecentPrivate.Count, Is.GreaterThan(6), "Catálogo da fixture deve realmente exercitar mais de seis privados.");
        f.Open(false);
        f.Recents.verticalNormalizedPosition = 0;
        f.Recents.onValueChanged.Invoke(Vector2.zero);
        string oldest = f.Repository.RecentPrivate.Last().DisplayName;
        Assert.That(f.Recents.content.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Name" && t.text == oldest), Is.True);
    }

    [Test]
    public void OnlineScrollReachesLastFriendWithoutExpandingOrDuplicatingSocialList()
    {
        using var f = new Fixture(allFriends: true);
        f.Open(false);
        Assert.That(f.Social.Friends().Count, Is.GreaterThan(4));
        Assert.That(f.Friends.content.rect.height, Is.GreaterThan(f.Friends.viewport.rect.height));
        Assert.That(f.Friends.content.childCount, Is.LessThan(f.Social.Friends().Count));
        f.Friends.verticalNormalizedPosition = 0;
        f.Friends.onValueChanged.Invoke(Vector2.zero);
        WDProfileData last = f.Social.Friends().Last();
        TMP_Text label = f.Friends.content.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Name" && t.text == last.Nickname);
        label.GetComponentInParent<Button>().onClick.Invoke();
        Assert.That(f.Hub.SelectedChannel.Participants, Does.Contain(last.PlayerId));
        Assert.That(f.Social.Friends().Count, Is.EqualTo(f.Profiles.AllProfiles.Count - 1));
    }

    [TestCase(1920f, 0f)]
    [TestCase(2400f, 96f)]
    public void CanonicalContentAndInteractiveControlsStayInsideSafeArea(float width, float inset)
    {
        using var f = new Fixture();
        f.Open();
        ((RectTransform)f.Root.transform).sizeDelta = new Vector2(width, 1080);
        Rect safe = new(-width / 2 + inset, -540 + 48, width - inset * 2, 984);
        f.Hub.ApplySafeArea(safe);
        var corners = new Vector3[4];
        foreach (RectTransform rect in new[] { f.Hub.Content, (RectTransform)f.Composer.transform,
            (RectTransform)f.Root.transform.Find("ChatContent/Header/Back") })
        {
            rect.GetWorldCorners(corners);
            foreach (Vector3 world in corners)
            {
                Vector3 point = f.Root.transform.InverseTransformPoint(world);
                Assert.That(point.x, Is.InRange(safe.xMin - .01f, safe.xMax + .01f));
                Assert.That(point.y, Is.InRange(safe.yMin - .01f, safe.yMax + .01f));
            }
        }
        Assert.That(f.Hub.Content.rect.size, Is.EqualTo(WDChatHubPanel.CanonicalSize));
        Assert.That(f.Hub.Content.Find("Header/TitleAccent"), Is.Not.Null);
        Assert.That(f.Hub.Content.Find("Header/header-gold-line"), Is.Null);
        Assert.That(f.Hub.Content.Find("Header/Divider"), Is.Null);
    }

    [Test]
    public void DestroyedGeneralViewUnsubscribesWithoutDestroyingSharedEmojiResources()
    {
        var first = new Fixture();
        first.Open();
        TMP_SpriteAsset sprites = WDChatEmojiColor.SpriteAsset;
        WDChatRepository repository = first.Repository;
        string channelId = first.Private.Id, friendId = first.FriendId;
        first.Dispose();
        Assert.DoesNotThrow(() => repository.ReceiveLocalMock(channelId, friendId, "Após destruição"));
        using var second = new Fixture();
        second.Open();
        Assert.That(second.Composer.textComponent.spriteAsset, Is.SameAs(sprites));
        Assert.That(sprites.material != null, Is.True);
        Assert.That(sprites.spriteSheet != null, Is.True);
    }

    [Test]
    public void ComposerReflectsCanonicalMultilineMessageContract()
    {
        using var f = new Fixture();
        Assert.That(f.Composer.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineNewline));
        Assert.That(f.Composer.characterLimit,
            Is.EqualTo(WDUIContentContracts.ChatMessageMaxLength));
    }

    // ============================================================
    // 06. ARTE OFICIAL E CALIBRAÇÃO LOCAL, SEM MUDAR O ATLAS COMPARTILHADO
    // ============================================================
    [TestCase(1920f)]
    [TestCase(2400f)]
    public void OfficialBackgroundCoversViewportOutsideSafeAreaWithoutDistortion(float width)
    {
        Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Backgrounds/Chat/Chat.png");
        Assert.That(texture, Is.Not.Null);
        using var f = new Fixture(background: texture);
        f.Open();
        ((RectTransform)f.Root.transform).sizeDelta = new Vector2(width, 1080);
        f.Hub.FitSafeArea();
        f.Hub.ApplySafeArea(new Rect(-width / 2 + 96, -492, width - 192, 984));
        RawImage art = f.Root.transform.Find("BackgroundArtwork").GetComponent<RawImage>();
        Assert.That(art.texture, Is.SameAs(texture));
        Assert.That(art.transform.parent, Is.SameAs(f.Root.transform));
        Assert.That(art.rectTransform.rect.size, Is.EqualTo(new Vector2(width, 1080)));
        Assert.That(art.raycastTarget, Is.False);
        Assert.That(art.uvRect.width * texture.width / (art.uvRect.height * texture.height), Is.EqualTo(width / 1080).Within(.0001));
        Assert.That(Mathf.Max(art.uvRect.width, art.uvRect.height), Is.EqualTo(1).Within(.0001));
        string guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Art/Backgrounds/Chat/Chat.png");
        Assert.That(System.IO.File.ReadAllText("Assets/Scenes/Home.unity"), Does.Contain("chatBackgroundTexture: {fileID: 2800000, guid: " + guid));
    }

    [Test]
    public void ReadabilityUsesLocalFontSizesAndExistingColorMetrics()
    {
        using var f = new Fixture();
        f.Open();
        f.Composer.text = "Olá 😎";
        f.Send.onClick.Invoke();
        Assert.That(f.Composer.textComponent.fontSize, Is.EqualTo(20));
        TMP_Text body = f.Labels.Single(t => t.name == "Body" && t.gameObject.activeInHierarchy);
        TMP_Text preview = f.Labels.Single(t => t.name == "Preview" && t.text == "Olá 😎");
        Assert.That(body.fontSize, Is.EqualTo(20));
        Assert.That(preview.fontSize, Is.EqualTo(14));
        Assert.That(body.spriteAsset, Is.SameAs(WDChatEmojiColor.SpriteAsset));
        Assert.That(preview.spriteAsset, Is.SameAs(body.spriteAsset));
        Assert.That(body.spriteAsset.faceInfo.pointSize, Is.EqualTo(100));
        Assert.That(body.spriteAsset.faceInfo.ascentLine, Is.EqualTo(108));
        Assert.That(body.spriteAsset.faceInfo.descentLine, Is.EqualTo(-12));
        Assert.That(f.Columns.Find("Conversation/Composer/Field/Attachment").gameObject.activeInHierarchy, Is.True);
        Assert.That(f.Picker, Is.Not.Null);
    }

    [Test]
    public void FunctionalSurfacesIsolateArtworkWithoutChangingOuterTransparency()
    {
        using var f = new Fixture();
        Transform panel = f.Hub.Content.Find("Panel");
        Assert.That(panel.GetComponent<Image>().color.a, Is.EqualTo(.7f));
        Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.9f));
        Assert.That(f.Columns.GetComponent<Image>().color.a, Is.EqualTo(.65f));
        foreach (string path in new[] { "Channels", "Channels/Support", "Conversation",
            "Conversation/Header", "Conversation/Header/Options", "Conversation/Composer",
            "Conversation/Composer/Field", "OnlineFriends" })
            Assert.That(f.Columns.Find(path).GetComponent<Image>().color.a, Is.EqualTo(.95f), path);

        // CanvasGroup multiplies each graphic's alpha; the three existing layers
        // together must suppress the artwork, without changing text or border alpha.
        float groupAlpha = panel.GetComponent<CanvasGroup>().alpha;
        float transmission = (1 - panel.GetComponent<Image>().color.a * groupAlpha)
            * (1 - f.Columns.GetComponent<Image>().color.a * groupAlpha)
            * (1 - f.Columns.Find("Conversation").GetComponent<Image>().color.a * groupAlpha);
        Assert.That(transmission, Is.LessThan(.05f));
    }
}
#endif

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WarDominionHomeController : MonoBehaviour
{
    // ============================================================
    // 01. CONFIGURAÇÃO SERIALIZADA E ESTADO
    // ============================================================

    [SerializeField] private WarDominionUITheme theme;
    [SerializeField] private WarDominionHomeData mockData;
    [SerializeField] private Texture2D backgroundTexture;

    private WDHomeModal modal;
    private WDHomeMatchFlowPanel matchFlowPanel;
    private WDHomeArmoryPanel armoryPanel;
    private WDHomeProfilePanel profilePanel;
    private WDHomeSettingsPanel settingsPanel;
    private WDHomeFriendsPanel friendsPanel;
    private WDChatHubPanel chatPanel;
    private WDHomeRankingPanel rankingPanel;
    private WDHomeMissionsPanel missionsPanel;
    private WDHomeClanPanel clanPanel;
    private WDHomeStorePanel storePanel;
    private WDProfileRepository profileRepository;
    private WDSocialRepository socialRepository;
    private WDChatRepository chatRepository;
    private WDMissionRepository missionRepository;
    private WDLocalMatchResultService matchResults;
    private WDClanRepository clanRepository;
    private WDNavigationStack navigationStack;
    private WDNavigationBackRouter navigationBackRouter;
    private RectTransform safeArea;
    private GameObject homeUIRoot;
    private EventSystem homeEventSystem;
    private TextMeshProUGUI homeNicknameLabel;
    private RectTransform chatCardRect;
    private GameObject chatBadge;
    private Rect lastSafeArea;
    private Vector2Int lastResolution;

    private void Awake()
    {
        if (theme == null)
            theme = Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");
        if (mockData == null)
            mockData = Resources.Load<WarDominionHomeData>("UI/Home/WarDominionHomeMockData");

        if (theme == null || mockData == null)
        {
            Debug.LogError("Configuração da Home não encontrada.", this);
            enabled = false;
            return;
        }

        homeEventSystem = EnsureEventSystem();
        BuildHome();
        UpdateSafeArea(true);
    }

    private void Update()
    {
        UpdateSafeArea(false);
        if (Keyboard.current?.escapeKey.wasPressedThisFrame != true)
            return;

        navigationBackRouter?.TryHandleBack(Time.frameCount);
    }

    // ============================================================
    // 02. CANVAS, FUNDO E ÁREA SEGURA
    // ============================================================

    private void BuildHome()
    {
        var root = new GameObject(
            "HomeUIRoot", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        homeUIRoot = root;
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform background = WDHomeUIFactory.Rect("Background", root.transform);
        WDHomeUIFactory.Stretch(background);
        RawImage art = background.gameObject.AddComponent<RawImage>();
        art.texture = backgroundTexture;
        art.color = backgroundTexture != null ? Color.white : theme.BackgroundPrimary;
        art.raycastTarget = false;

        Image veil = WDHomeUIFactory.Image("Atmosphere", background, new Color(0.008f, 0.025f, 0.035f, 0.58f));
        WDHomeUIFactory.Stretch(veil.rectTransform);
        AddAmbientAccents(background);

        safeArea = WDHomeUIFactory.Rect("SafeArea", root.transform);
        chatRepository = WDChatRuntime.Current;
        if (chatRepository == null)
        {
            profileRepository = new WDProfileRepository(mockData);
            socialRepository = new WDSocialRepository(profileRepository);
            chatRepository = new WDChatRepository(profileRepository, socialRepository);
            WDChatRuntime.Initialize(chatRepository);
        }
        else
        {
            profileRepository = chatRepository.Profiles;
            socialRepository = chatRepository.Social;
        }
        matchResults = new WDLocalMatchResultService(new WDPlayerPrefsLocalMatchProgressStore());
        profileRepository.ApplyLocalMatchProgress(matchResults.Progress);
        chatRepository.Changed += RefreshChatBadge;
        missionRepository = matchResults.Missions;
        RectTransform content = WDHomeUIFactory.Rect("HomeContent", safeArea);
        WDHomeUIFactory.Stretch(content, 44f, 34f, 44f, 34f);
        BuildHeader(content);
        BuildColumns(content);

        RectTransform modalLayer = WDHomeUIFactory.Rect("ModalLayer", root.transform);
        WDHomeUIFactory.Stretch(modalLayer);
        modal = modalLayer.gameObject.AddComponent<WDHomeModal>();
        modal.Build(theme, CloseModal);

        WarDominionMatchFlowConfig matchConfig =
            Resources.Load<WarDominionMatchFlowConfig>("UI/Home/WarDominionMatchFlowConfig");
        WDArmoryCatalogConfig armoryCatalog =
            Resources.Load<WDArmoryCatalogConfig>("UI/Home/WDArmoryCatalogConfig");
        WDVipEntitlement vipEntitlement = new WDVipEntitlement(
            new WDSystemStoreClock(), new WDPlayerPrefsVipStateStore());
        var armoryRepository = new WDPlayerPrefsArmoryRepository(vipEntitlement);
        WDStoreCatalogConfig storeCatalog =
            Resources.Load<WDStoreCatalogConfig>("UI/Home/WDStoreCatalogConfig");
        WDStoreService storeService = storeCatalog == null ? null :
            new WDStoreService(storeCatalog, armoryCatalog, armoryRepository,
                profileRepository, vipEntitlement);
        RectTransform matchFlowLayer = WDHomeUIFactory.Rect("MatchFlowLayer", root.transform);
        WDHomeUIFactory.Stretch(matchFlowLayer);
        matchFlowPanel = matchFlowLayer.gameObject.AddComponent<WDHomeMatchFlowPanel>();
        navigationStack = new WDNavigationStack();
        navigationStack.SetRoot(new WDNavigationContext(
            "home", _ =>
            {
                matchFlowPanel?.HideFromNavigation();
                profilePanel?.HideFromNavigation();
                settingsPanel?.HideFromNavigation();
                friendsPanel?.HideFromNavigation();
                chatPanel?.HideFromNavigation();
                rankingPanel?.HideFromNavigation();
                missionsPanel?.HideFromNavigation();
                armoryPanel?.HideFromNavigation();
                clanPanel?.HideFromNavigation();
                storePanel?.HideFromNavigation();
            }));
        matchFlowPanel.Build(
            theme, matchConfig, mockData, armoryCatalog, armoryRepository,
            navigationStack, profileRepository, playerId => profilePanel?.OpenPublicProfile(playerId));
        navigationBackRouter = new WDNavigationBackRouter(
            navigationStack, TryHandleCancelableContext);

        RectTransform profileLayer = WDHomeUIFactory.Rect("ProfileLayer", root.transform);
        WDHomeUIFactory.Stretch(profileLayer);
        profilePanel = profileLayer.gameObject.AddComponent<WDHomeProfilePanel>();
        profilePanel.Build(theme, profileRepository, socialRepository, navigationStack,
            OpenModal, RefreshHomeIdentity, OpenDirectChat, OpenSocialConfirmation,
            item => { if (storePanel != null && storePanel.CatalogReady)
                storePanel.Open(item.Category == WDProfileCosmeticCategory.Avatar ? 2 : 0);
                else OpenModal("LOJA INDISPONÍVEL", "Catálogo local não encontrado."); });

        RectTransform friendsLayer = WDHomeUIFactory.Rect("FriendsLayer", root.transform);
        WDHomeUIFactory.Stretch(friendsLayer);
        friendsPanel = friendsLayer.gameObject.AddComponent<WDHomeFriendsPanel>();
        friendsPanel.Build(theme, socialRepository, navigationStack,
            profilePanel.OpenPublicProfile, OpenDirectChat, OpenSocialConfirmation);

        RectTransform chatLayer = WDHomeUIFactory.Rect("ChatLayer", root.transform);
        WDHomeUIFactory.Stretch(chatLayer);
        chatPanel = chatLayer.gameObject.AddComponent<WDChatHubPanel>();
        chatPanel.Build(theme, chatRepository, socialRepository, profileRepository,
            navigationStack, profilePanel.OpenPublicProfile, OpenSocialConfirmation);

        RectTransform rankingLayer = WDHomeUIFactory.Rect("RankingLayer", root.transform);
        WDHomeUIFactory.Stretch(rankingLayer);
        rankingPanel = rankingLayer.gameObject.AddComponent<WDHomeRankingPanel>();
        rankingPanel.Build(theme, new WDRankingRepository(profileRepository), navigationStack,
            profilePanel.OpenPublicProfile);

        RectTransform missionsLayer = WDHomeUIFactory.Rect("MissionsLayer", root.transform);
        WDHomeUIFactory.Stretch(missionsLayer);
        missionsPanel = missionsLayer.gameObject.AddComponent<WDHomeMissionsPanel>();
        missionsPanel.Build(theme, missionRepository, navigationStack);

        RectTransform settingsLayer = WDHomeUIFactory.Rect("SettingsLayer", root.transform);
        WDHomeUIFactory.Stretch(settingsLayer);
        settingsPanel = settingsLayer.gameObject.AddComponent<WDHomeSettingsPanel>();
        settingsPanel.Build(theme, profileRepository, navigationStack);

        RectTransform armoryLayer = WDHomeUIFactory.Rect("ArmoryLayer", root.transform);
        WDHomeUIFactory.Stretch(armoryLayer);
        armoryPanel = armoryLayer.gameObject.AddComponent<WDHomeArmoryPanel>();
        armoryPanel.Build(
            theme, armoryCatalog, armoryRepository,
            OpenLockedDeckPopup, navigationStack);

        RectTransform storeLayer = WDHomeUIFactory.Rect("StoreLayer", root.transform);
        WDHomeUIFactory.Stretch(storeLayer);
        storePanel = storeLayer.gameObject.AddComponent<WDHomeStorePanel>();
        if (storeService != null) storePanel.Build(theme, storeService, navigationStack);
        else storeLayer.gameObject.SetActive(false);

        // 03. Membership único de sessão, projetado no Perfil e no Chat existentes.
        clanRepository = new WDClanRepository(profileRepository, armoryCatalog,
            new WDClanArmoryProfiles(profileRepository.LocalProfile.PlayerId,
                armoryCatalog, armoryRepository));
        clanRepository.Changed += SyncClanMembership;
        chatRepository.SyncClanMembership(clanRepository.LocalClan);
        RectTransform clanLayer = WDHomeUIFactory.Rect("ClanLayer", root.transform);
        WDHomeUIFactory.Stretch(clanLayer);
        clanPanel = clanLayer.gameObject.AddComponent<WDHomeClanPanel>();
        clanPanel.Build(theme, clanRepository, profileRepository, socialRepository,
            navigationStack, profilePanel.OpenPublicProfile, OpenDirectChat,
            OpenSocialConfirmation);
    }

    private void AddAmbientAccents(Transform parent)
    {
        for (int i = 0; i < 7; i++)
        {
            Image line = WDHomeUIFactory.Image(
                $"AmbientLine_{i + 1:00}", parent,
                new Color(theme.Acento.r, theme.Acento.g, theme.Acento.b, 0.045f));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0.08f + i * 0.13f, 0f);
            rect.anchorMax = new Vector2(0.081f + i * 0.13f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
        }
    }

    private void BuildHeader(RectTransform content)
    {
        RectTransform header = WDHomeUIFactory.Rect("Header", content);
        header.anchorMin = new Vector2(0f, 0.9f);
        header.anchorMax = new Vector2(1f, 1f);
        header.offsetMin = header.offsetMax = Vector2.zero;

        TextMeshProUGUI brand = WDHomeUIFactory.Text(
            "Brand", header, theme, "WAR DOMINION", theme.TypeDisplay,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        brand.fontStyle = FontStyles.Bold;
        WDHomeUIFactory.Stretch(brand.rectTransform, 6f, 0f, 600f, 0f);

        TextMeshProUGUI pass = WDHomeUIFactory.Text(
            "BuildStatus", header, theme, "HOME · PASSADA 1 FUNCIONAL",
            theme.TypeMicro, theme.TextoSecundario, TextAlignmentOptions.Right);
        WDHomeUIFactory.Stretch(pass.rectTransform, 600f, 0f, 6f, 0f);

        RectTransform notifications = WDHomeUIFactory.Rect("NotificationCenter", header);
        notifications.anchorMin = notifications.anchorMax = new Vector2(1f, 0.5f);
        notifications.pivot = new Vector2(1f, 0.5f);
        notifications.anchoredPosition = new Vector2(-18f, 0f);
        notifications.sizeDelta = new Vector2(52f, 52f);
        notifications.gameObject.AddComponent<WDUIPremiumButton>().Build(
            theme, "!", OpenNotificationPlaceholder);
        pass.rectTransform.offsetMax = new Vector2(-82f, 0f);
    }

    // ============================================================
    // 03. COMPOSIÇÃO EM TRÊS ZONAS
    // ============================================================

    private void BuildColumns(RectTransform content)
    {
        RectTransform columns = WDHomeUIFactory.Rect("Columns", content);
        columns.anchorMin = new Vector2(0f, 0f);
        columns.anchorMax = new Vector2(1f, 0.89f);
        columns.offsetMin = columns.offsetMax = Vector2.zero;
        HorizontalLayoutGroup layout = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 22f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        RectTransform left = CreateColumn("LeftZone", columns, 0.9f);
        RectTransform center = CreateColumn("CenterZone", columns, 1.35f);
        RectTransform right = CreateColumn("RightZone", columns, 0.95f);
        BuildLeft(left);
        BuildCenter(center);
        BuildRight(right);
    }

    private RectTransform CreateColumn(string name, Transform parent, float flexibleWidth)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = flexibleWidth;
        WDHomeUIFactory.VerticalLayout(rect.gameObject, 14f, new RectOffset(0, 0, 0, 0));
        return rect;
    }

    private void BuildLeft(RectTransform parent)
    {
        BuildIdentity(parent);

        // O respiro flexível mantém o bloco secundário apoiado na base da coluna.
        RectTransform breathingRoom = WDHomeUIFactory.Rect("LeftBreathingRoom", parent);
        LayoutElement spacer = breathingRoom.gameObject.AddComponent<LayoutElement>();
        spacer.minHeight = 40f;
        spacer.flexibleHeight = 1f;

        RectTransform lowerGroup = WDHomeUIFactory.Rect("LeftLowerGroup", parent);
        LayoutElement lowerElement = lowerGroup.gameObject.AddComponent<LayoutElement>();
        lowerElement.preferredHeight = 352f;
        WDHomeUIFactory.VerticalLayout(
            lowerGroup.gameObject, 14f, new RectOffset(0, 0, 0, 0));
        RectTransform missionsRect = WDHomeUIFactory.Rect("MissionsCard", lowerGroup);
        missionsRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "M", "MISSÕES", MissionSummary(), OpenMissions);
        RectTransform rankingRect = WDHomeUIFactory.Rect("RankingCard", lowerGroup);
        rankingRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "R", "RANKING", "Classificação competitiva", OpenRanking);
        chatCardRect = WDHomeUIFactory.Rect("ChatCard", lowerGroup);
        chatCardRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "C", "CHAT", "Mensagens", () => chatPanel.OpenFromHome(), 132f);
        RefreshChatBadge();
    }

    private void RefreshChatBadge()
    {
        if (chatCardRect == null || chatRepository == null)
            return;
        if (chatBadge != null)
        {
            if (Application.isPlaying) Destroy(chatBadge); else DestroyImmediate(chatBadge);
            chatBadge = null;
        }
        int count = chatRepository.ExternalUnreadConversationCount;
        if (count <= 0)
            return;
        RectTransform badgeRect = WDHomeUIFactory.Rect("UnreadBadge", chatCardRect);
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.anchoredPosition = new Vector2(-12f, -12f);
        badgeRect.sizeDelta = new Vector2(34f, 28f);
        chatBadge = badgeRect.gameObject;
        chatBadge.AddComponent<WDHomeNotificationBadge>().Build(theme, count);
    }

    private void OnDestroy()
    {
        if (chatRepository != null)
            chatRepository.Changed -= RefreshChatBadge;
        if (clanRepository != null)
            clanRepository.Changed -= SyncClanMembership;
    }

    private void SyncClanMembership() =>
        chatRepository?.SyncClanMembership(clanRepository?.LocalClan);

    private void BuildIdentity(RectTransform parent)
    {
        RectTransform identity = WDHomeUIFactory.Rect("PlayerIdentityCard", parent);
        Image surface = identity.gameObject.AddComponent<Image>();
        surface.color = theme.BackgroundElevated;
        Outline outline = identity.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.BorderNeutral;
        LayoutElement element = identity.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 166f;

        Image avatar = WDHomeUIFactory.Image("Avatar", identity, mockData.PlayerColor);
        RectTransform avatarRect = avatar.rectTransform;
        avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
        avatarRect.pivot = new Vector2(0f, 0.5f);
        avatarRect.anchoredPosition = new Vector2(20f, 0f);
        avatarRect.sizeDelta = new Vector2(86f, 86f);
        Outline avatarOutline = avatar.gameObject.AddComponent<Outline>();
        avatarOutline.effectColor = Color.Lerp(mockData.PlayerColor, Color.white, 0.35f);
        TextMeshProUGUI initials = WDHomeUIFactory.Text(
            "Initials", avatar.transform, theme, "WD", 24f, Color.white, TextAlignmentOptions.Center);
        WDHomeUIFactory.Stretch(initials.rectTransform);

        homeNicknameLabel = WDHomeUIFactory.Text(
            "Nickname", identity, theme, profileRepository.LocalProfile.Nickname, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        homeNicknameLabel.fontStyle = FontStyles.Bold;
        homeNicknameLabel.rectTransform.anchorMin = new Vector2(0f, 0.53f);
        homeNicknameLabel.rectTransform.anchorMax = new Vector2(1f, 0.82f);
        homeNicknameLabel.rectTransform.offsetMin = new Vector2(126f, 0f);
        homeNicknameLabel.rectTransform.offsetMax = new Vector2(-54f, 0f);

        TextMeshProUGUI status = WDHomeUIFactory.Text(
            "Status", identity, theme, $"{mockData.Status}   |   Conta v",
            theme.TypeBody, theme.Success, TextAlignmentOptions.Left);
        status.rectTransform.anchorMin = new Vector2(0f, 0.22f);
        status.rectTransform.anchorMax = new Vector2(1f, 0.52f);
        status.rectTransform.offsetMin = new Vector2(126f, 0f);
        status.rectTransform.offsetMax = new Vector2(-20f, 0f);

        RectTransform badgeRect = WDHomeUIFactory.Rect("NotificationBadge", identity);
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.anchoredPosition = new Vector2(-14f, -14f);
        badgeRect.sizeDelta = new Vector2(34f, 28f);
        badgeRect.gameObject.AddComponent<WDHomeNotificationBadge>().Build(theme, mockData.Notifications);

        Button button = identity.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(OpenMyProfile);
    }

    private void BuildCenter(RectTransform parent)
    {
        RectTransform league = WDHomeUIFactory.Rect("LeagueSummary", parent);
        Image surface = league.gameObject.AddComponent<Image>();
        surface.color = theme.SurfaceGlass;
        LayoutElement element = league.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 112f;
        TextMeshProUGUI leagueText = WDHomeUIFactory.Text(
            "League", league, theme, profileRepository.LocalProfile.Division, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        leagueText.fontStyle = FontStyles.Bold;
        leagueText.rectTransform.anchorMin = new Vector2(0f, 0.38f);
        leagueText.rectTransform.anchorMax = Vector2.one;
        leagueText.rectTransform.offsetMin = leagueText.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI trophyText = WDHomeUIFactory.Text(
            "Trophies", league, theme, $"*  {matchResults.Progress.trophies} TROFÉUS",
            theme.TypeBody, theme.Acento, TextAlignmentOptions.Center);
        trophyText.rectTransform.anchorMin = Vector2.zero;
        trophyText.rectTransform.anchorMax = new Vector2(1f, 0.43f);
        trophyText.rectTransform.offsetMin = trophyText.rectTransform.offsetMax = Vector2.zero;

        RectTransform playRect = WDHomeUIFactory.Rect("MainPlayCard", parent);
        playRect.gameObject.AddComponent<WDHomePlayCard>().Build(
            theme, ">", "PLAY", "JOGAR PARTIDA", OpenMatchFlow, 300f);

        RectTransform lower = WDHomeUIFactory.Rect("CenterSecondaryRow", parent);
        LayoutElement lowerElement = lower.gameObject.AddComponent<LayoutElement>();
        lowerElement.preferredHeight = 150f;
        HorizontalLayoutGroup lowerLayout = lower.gameObject.AddComponent<HorizontalLayoutGroup>();
        lowerLayout.spacing = 14f;
        lowerLayout.childControlWidth = true;
        lowerLayout.childControlHeight = true;
        lowerLayout.childForceExpandWidth = true;
        lowerLayout.childForceExpandHeight = true;
        RectTransform armoryRect = WDHomeUIFactory.Rect("ArmoryCard", lower);
        armoryRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "A", "ARSENAL", "Decks, coleção e progressão",
            OpenArmory, 140f);
        RectTransform clanRect = WDHomeUIFactory.Rect("ClanCard", lower);
        clanRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "C", "CLÃ", "Comunidade competitiva", OpenClan, 140f);
    }

    private void BuildRight(RectTransform parent)
    {
        RectTransform profileRect = WDHomeUIFactory.Rect("ProfileCard", parent);
        profileRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "P", "PERFIL", "Identidade e personalização", OpenMyProfile, 112f);
        RectTransform friendsRect = WDHomeUIFactory.Rect("FriendsCard", parent);
        friendsRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "A", "AMIGOS", "Lista, presença e convites", OpenFriends, 104f);

        RectTransform storeRect = WDHomeUIFactory.Rect("StoreCard", parent);
        storeRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "L", "LOJA", "Coleções, avatares e VIP", OpenStore, 106f);
        RectTransform settingsRect = WDHomeUIFactory.Rect("SettingsCard", parent);
        settingsRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "S", "CONFIGURAÇÕES", "Som, gráficos, privacidade e sistema",
            OpenSettings, 106f);
    }

    private RectTransform CreateRow(string name, Transform parent, float height)
    {
        RectTransform row = WDHomeUIFactory.Rect(name, parent);
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    private WDHomeCard CreateCard(
        Transform parent, string icon, string title, string subtitle,
        string modalTitle, string modalBody, float height = 96f)
    {
        RectTransform rect = WDHomeUIFactory.Rect(title.Replace(" / ", "") + "Card", parent);
        WDHomeCard card = rect.gameObject.AddComponent<WDHomeCard>();
        card.Build(theme, icon, title, subtitle, () => OpenModal(modalTitle, modalBody), height);
        return card;
    }

    // ============================================================
    // 04. NAVEGAÇÃO MODAL E CONTEÚDO MOCK
    // ============================================================

    private void OpenModal(string title, string body)
    {
        modal.transform.SetAsLastSibling();
        modal.Open(title, body);
    }
    private void CloseModal() => modal.Close();

    private void OpenMatchFlow() => matchFlowPanel.Open();

    private bool TryHandleCancelableContext()
    {
        if (modal != null && modal.IsOpen)
        {
            modal.RequestClose();
            return true;
        }

        if (armoryPanel != null && armoryPanel.IsOpen)
        {
            armoryPanel.HandleEscape();
            return true;
        }

        if (profilePanel != null && profilePanel.IsOpen && profilePanel.TryHandleModalBack())
            return true;

        if (friendsPanel != null && friendsPanel.IsOpen && friendsPanel.TryHandleLocalBack())
            return true;

        if (chatPanel != null && chatPanel.IsOpen && chatPanel.TryHandleLocalBack())
            return true;

        if (rankingPanel != null && rankingPanel.IsOpen && rankingPanel.TryHandleLocalBack())
            return true;

        if (missionsPanel != null && missionsPanel.IsOpen && missionsPanel.TryHandleLocalBack())
            return true;

        if (storePanel != null && storePanel.IsOpen && storePanel.TryHandleLocalBack())
            return true;

        if (clanPanel != null && clanPanel.IsOpen && clanPanel.TryHandleLocalBack())
            return true;

        return matchFlowPanel != null &&
            matchFlowPanel.IsOpen &&
            matchFlowPanel.TryHandleLocalBack();
    }

    private void OpenNotificationPlaceholder()
    {
        OpenModal(
            "CENTRAL DE NOTIFICAÇÕES",
            "Destino provisório. O conteúdo, as categorias e o histórico da Central de Notificações ainda não foram definidos.");
    }

    private void OpenMyProfile() => profilePanel.OpenMyProfile();

    private void OpenSettings() => settingsPanel.OpenGeneral();

    private void OpenFriends() => friendsPanel.Open();

    private void OpenRanking() => rankingPanel.Open();

    private void OpenMissions() => missionsPanel.Open();

    private void OpenClan() => clanPanel.Open();

    private void OpenDirectChat(string playerId)
    {
        if (chatRepository.OpenPrivate(playerId) == null)
        {
            OpenModal("CHAT INDISPONÍVEL", "O relacionamento atual não permite iniciar esta conversa.");
            return;
        }
        chatPanel.OpenFromHome(playerId);
    }

    private void OpenSocialConfirmation(string title, string body, System.Action confirmed)
    {
        modal.transform.SetAsLastSibling();
        modal.OpenChoices(title, body, new[] { "CANCELAR", "CONFIRMAR" }, choice =>
        {
            modal.Close();
            if (choice == 1)
                confirmed?.Invoke();
        });
    }

    private void RefreshHomeIdentity()
    {
        if (homeNicknameLabel != null)
            homeNicknameLabel.text = profileRepository.LocalProfile.Nickname;
    }

    private void OpenArmory() => armoryPanel.Open();
    private void OpenStore() { if (storePanel != null && storePanel.CatalogReady) storePanel.Open();
        else OpenModal("LOJA INDISPONÍVEL", "Catálogo local não encontrado."); }

    private void OpenLockedDeckPopup()
    {
        modal.transform.SetAsLastSibling();
        modal.OpenChoices(
            "RECURSO VIP",
            "Este deck exige VIP. A composição permanece preservada mesmo quando o benefício não está ativo.",
            new[] { "ACESSAR LOJA / VIP" },
            choice =>
            {
                if (choice == 0)
                    OpenStoreVipDestination();
            },
            CloseArmoryVipModal);
    }

    private void CloseArmoryVipModal()
    {
        armoryPanel.CloseVipRequirementModal();
        modal.Close();
    }

    private void OpenStoreVipDestination()
    {
        armoryPanel.NavigateToStoreVip();
        modal.Close();
        if (storePanel != null && storePanel.CatalogReady) storePanel.Open(3);
        else OpenModal("LOJA INDISPONÍVEL", "Catálogo local não encontrado.");
    }

    private string ProfileBody() =>
        $"{profileRepository.LocalProfile.Nickname}\nStatus: {mockData.Status}\nLiga: {mockData.League}\nTroféus: {profileRepository.LocalProfile.DivisionPoints}\n\n" +
        "Avatar, skin, cor e demais opções de personalização serão centralizados aqui.";

    private string MissionSummary() => missionRepository == null
        ? "COLEÇÃO PERMANENTE"
        : $"DESCOBERTAS {missionRepository.DiscoveredCount}/{missionRepository.TotalCount}";
    private string RankingBody() =>
        $"Posição competitiva provisória.\nDivisão atual: {profileRepository.LocalProfile.Division}" +
        "\n\nThresholds e matchmaking não foram definidos.";
    private string EventBody() => $"PRÓXIMO: {mockData.NextEvent}\n{mockData.RegistrationState}\nCountdown: {mockData.Countdown}\n\nAGORA: {mockData.LiveEvent}";
    private string FriendsBody() => "Lista de amigos, presença, convites e ações sociais — dados mock nesta etapa.";
    private string ChatBody() => "Painel contextual de chat.\n\nBackend e persistência não fazem parte desta etapa.";
    private string StoreBody() => "Modelo da Loja pendente.\n\nNenhuma vantagem competitiva comprável será introduzida.";
    private string VipBody() => "Benefícios e limites de VIP ainda não definidos.\n\nWar Dominion não será pay-to-win.";
    private string DiscordBody() => "Ação externa provisória para a futura comunidade oficial do War Dominion.";

    // ============================================================
    // 05. INPUT E RESPONSIVIDADE
    // ============================================================

    public void PrepareForSceneTransition()
    {
        enabled = false;
        if (homeUIRoot != null)
            homeUIRoot.SetActive(false);
        if (homeEventSystem != null)
            homeEventSystem.gameObject.SetActive(false);
    }

    private static EventSystem EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return null;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        eventSystem.AddComponent<InputSystemUIInputModule>();
        return eventSystem.GetComponent<EventSystem>();
    }

    private void UpdateSafeArea(bool force)
    {
        if (safeArea == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect area = Screen.safeArea;
        var resolution = new Vector2Int(Screen.width, Screen.height);
        if (!force && area == lastSafeArea && resolution == lastResolution)
            return;

        lastSafeArea = area;
        lastResolution = resolution;
        safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
    }
}

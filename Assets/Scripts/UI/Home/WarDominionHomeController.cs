using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private Texture2D playBackgroundTexture;
    [SerializeField] private Texture2D matchmakingBackgroundTexture;
    [SerializeField] private Texture2D chatBackgroundTexture;
    [SerializeField] private Texture2D friendsBackgroundTexture;
    [SerializeField] private Texture2D settingsBackgroundTexture;
    [SerializeField] private Texture2D rankingBackgroundTexture;
    [SerializeField] private Texture2D storeBackgroundTexture;
    [SerializeField] private Texture2D warDominionOpening16x9Texture;
    [SerializeField] private Texture2D warDominionOpeningUltrawideTexture;
    [SerializeField] private TMP_FontAsset gabaritoBlack;
    [SerializeField] private TMP_FontAsset gabaritoMedium;
    [SerializeField] private TMP_FontAsset gabaritoBold;
    [SerializeField] private TMP_FontAsset gabaritoSemiBold;
    [SerializeField] private TMP_FontAsset gabaritoExtraBold;
    [SerializeField] private TMP_FontAsset geistMedium;

    private WDHomeModal modal;
    private WDHomeNotificationCenterPanel notificationCenterPanel;
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
    private RectTransform homeAccount;
    private RawImage homeAccountAvatar;
    private GameObject accountDropdownRoot;
    private RawImage accountDropdownAvatar;
    private TextMeshProUGUI accountDropdownNickname;
    private RectTransform homeResources;
    private RectTransform homeLeftZone;
    private RectTransform homeRightZone;
    private RectTransform homeCenterHub;
    private RectTransform homeSecondaryRow;
    private RectTransform homePlayButton;
    private RectTransform homeProfileCard;
    private RectTransform homeFriendsCard;
    private RectTransform homeStoreCard;
    private RectTransform homeSettingsCard;
    private GameObject homeUIRoot;
    private CanvasGroup homeInputGroup;
    private EventSystem homeEventSystem;
    private bool openingPresentationActive;
    private TextMeshProUGUI homeNicknameLabel;
    private TextMeshProUGUI homeTrophiesLabel;
    private TextMeshProUGUI heroNicknameLabel;
    private RectTransform chatCardRect;
    private GameObject chatBadge;
    private GameObject unreadIndicator;
    private TextMeshProUGUI onlineFriendsLabel;
    private RectTransform featuredTournamentCard;
    private TextMeshProUGUI featuredTournamentName;
    private TextMeshProUGUI featuredTournamentCountdown;
    private UnityEngine.Events.UnityAction featuredTournamentAction;
    private WDPlayerPrefsArmoryRepository economyRepository;
    private WDArmoryCatalogConfig economyCatalog;
    private TextMeshProUGUI coinsLabel;
    private TextMeshProUGUI emeraldsLabel;
    private RectTransform resourceNotification;
    private Rect lastSafeArea;
    private Vector2Int lastResolution;
    private float nextCurrencyRefresh;
    private static bool warDominionOpeningPresented;
    private const float WarDominionOpeningHoldSeconds = 3f;
    private const float WarDominionOpeningFadeSeconds = 0.6f;
    private const float WarDominionOpeningUltrawideThreshold = 2f;
    private const float HomeSecondaryOpticalOffsetX = 8f;
    private static readonly Color FigmaText = new Color(0.961f, 0.965f, 0.973f, 1f);
    private static readonly Color FigmaMuted = new Color(0.557f, 0.573f, 0.604f, 1f);
    private static readonly Color FigmaGold = new Color(0.773f, 0.608f, 0.388f, 1f);
    private static readonly Color FigmaBorder = new Color(0.773f, 0.608f, 0.388f, 0.2f);
    private static readonly Color FigmaPanel = new Color(0.067f, 0.075f, 0.09f, 0.90f);
    private static readonly Color FigmaPill = new Color(0.106f, 0.114f, 0.133f, 0.9f);
    private static readonly Color FigmaNavigationGold =
        new Color(0.91f, 0.67f, 0.18f, 0.88f);
    private static Texture2D profileGradientTexture;
    private static readonly WDLocalizedText AccountCaption =
        new WDLocalizedText("home.account.primary", "CONTA PRINCIPAL", "MAIN ACCOUNT");
    private static readonly WDLocalizedText TrophiesCaption =
        new WDLocalizedText("home.trophies", "TROF\u00c9US", "TROPHIES");

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

        // 01.1. A tipografia oficial do PLAY é construída dos arquivos de fonte
        // importados quando não há Font Assets serializados na cena.
        gabaritoBlack ??= WDHomeTypography.Load("Gabarito-Black");
        gabaritoMedium ??= WDHomeTypography.Load("Gabarito-Medium");
        gabaritoBold ??= WDHomeTypography.Load("Gabarito-Bold");
        gabaritoSemiBold ??= WDHomeTypography.Load("Gabarito-SemiBold");
        gabaritoExtraBold ??= WDHomeTypography.Load("Gabarito-ExtraBold");
        geistMedium ??= WDHomeTypography.Load("Geist-Medium");

        homeEventSystem = EnsureEventSystem();
        BuildHome();
        UpdateSafeArea(true);

        Texture2D openingTexture = SelectWarDominionOpeningTexture();
        if (!warDominionOpeningPresented && openingTexture != null)
        {
            warDominionOpeningPresented = true;
            openingPresentationActive = true;
            SetHomeInputEnabled(false);
            StartCoroutine(PresentWarDominionOpening(openingTexture));
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWarDominionOpeningSession()
    {
        // 01.2. Uma nova sessão do Player reabilita a apresentação; retornos à HOME
        // na mesma sessão preservam o estado já apresentado.
        warDominionOpeningPresented = false;
    }

    private Texture2D SelectWarDominionOpeningTexture()
    {
        // 01.3. O limiar 18:9 é o ponto médio entre os masters 16:9 e 20:9.
        // A seleção depende somente da proporção útil atual, sem modelo ou DPI.
        float viewportAspect = Screen.height > 0
            ? (float)Screen.width / Screen.height
            : 16f / 9f;
        bool preferUltrawide = viewportAspect >= WarDominionOpeningUltrawideThreshold;
        Texture2D preferred = preferUltrawide
            ? warDominionOpeningUltrawideTexture
            : warDominionOpening16x9Texture;
        return preferred != null
            ? preferred
            : (preferUltrawide
                ? warDominionOpening16x9Texture
                : warDominionOpeningUltrawideTexture);
    }

    private IEnumerator PresentWarDominionOpening(Texture2D openingTexture)
    {
        // 01.4. O overlay nasce no mesmo Awake que constrói a HOME, antes do
        // primeiro frame, e permanece como única autoridade do boot interno.
        var overlay = new GameObject(
            "WarDominionOpening", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        overlay.transform.SetParent(transform, false);

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        Image black = WDHomeUIFactory.Image("BlackBackground", overlay.transform, Color.black);
        WDHomeUIFactory.Stretch(black.rectTransform);

        RectTransform artRect = WDHomeUIFactory.Rect("WarDominionArt", overlay.transform);
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = Vector2.zero;
        RawImage art = artRect.gameObject.AddComponent<RawImage>();
        art.texture = openingTexture;
        art.color = Color.white;
        art.raycastTarget = false;
        AspectRatioFitter cover = artRect.gameObject.AddComponent<AspectRatioFitter>();
        // 01.5. A composição de marca é sempre integral: em proporções diferentes
        // de 16:9, FitInParent centraliza a arte e deixa o fundo preto aparente.
        cover.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        cover.aspectRatio = (float)openingTexture.width / openingTexture.height;

        // 01.6. A arte entra integral, sustenta 3 s e sai continuamente em 0,6 s.
        yield return new WaitForSecondsRealtime(WarDominionOpeningHoldSeconds);
        float elapsed = 0f;
        while (elapsed < WarDominionOpeningFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(elapsed / WarDominionOpeningFadeSeconds);
            yield return null;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        yield return null;
        Destroy(overlay);
        openingPresentationActive = false;
        SetHomeInputEnabled(true);
    }

    private void Update()
    {
        UpdateSafeArea(false);
        if (Time.unscaledTime >= nextCurrencyRefresh)
        {
            nextCurrencyRefresh = Time.unscaledTime + 1f;
            RefreshCurrencies();
        }
        if (openingPresentationActive ||
            Keyboard.current?.escapeKey.wasPressedThisFrame != true)
            return;

        navigationBackRouter?.TryHandleBack(Time.frameCount);
    }

    // ============================================================
    // 02. CANVAS, FUNDO E ÁREA SEGURA
    // ============================================================

    private void SetHomeInputEnabled(bool value)
    {
        if (homeInputGroup == null)
            return;
        homeInputGroup.interactable = value;
        homeInputGroup.blocksRaycasts = value;
    }

    private void BuildHome()
    {
        var root = new GameObject(
            "HomeUIRoot", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        homeUIRoot = root;
        homeInputGroup = root.GetComponent<CanvasGroup>();
        SetHomeInputEnabled(true);
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // 20:9 amplia a largura Ãºtil sem reduzir controles touch da composiÃ§Ã£o 16:9.
        scaler.matchWidthOrHeight = 1f;

        RectTransform background = WDHomeUIFactory.Rect("Background", root.transform);
        background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        background.anchoredPosition = Vector2.zero;
        RawImage art = background.gameObject.AddComponent<RawImage>();
        art.texture = backgroundTexture;
        art.color = art.texture != null ? Color.white : theme.BackgroundPrimary;
        art.raycastTarget = false;
        AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = art.texture != null ? (float)art.texture.width / art.texture.height : 16f / 9f;

        // 02. O master serializado na cena fica isolado da UI; o overlay e independente.
        Image veil = WDHomeUIFactory.Image("Atmosphere", root.transform,
            new Color(0.043f, 0.047f, 0.055f, art.texture != null ? 0.15f : 0.30f));
        WDHomeUIFactory.Stretch(veil.rectTransform);
        veil.raycastTarget = false;

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
        socialRepository.Changed += RefreshSocialIndicators;
        missionRepository = matchResults.Missions;
        RectTransform content = WDHomeUIFactory.Rect("HomeContent", safeArea);
        WDHomeUIFactory.Stretch(content);

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
        economyRepository = armoryRepository;
        economyCatalog = armoryCatalog;
        RefreshCurrencies();
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
                CloseAccountDropdown();
                RefreshHomeIdentity();
            }));
        matchFlowPanel.Build(
            theme, matchConfig, mockData, armoryCatalog, armoryRepository,
            navigationStack, profileRepository, playerId => profilePanel?.OpenPublicProfile(playerId),
            playBackgroundTexture, OpenNotificationCenter, matchmakingBackgroundTexture,
            gabaritoBlack, gabaritoMedium, gabaritoBold, gabaritoExtraBold, geistMedium,
            socialRepository, gabaritoSemiBold);
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
            profilePanel.OpenPublicProfile, OpenDirectChat, OpenSocialConfirmation,
            friendsBackgroundTexture, SetFriendsPresentationVisible);

        RectTransform chatLayer = WDHomeUIFactory.Rect("ChatLayer", root.transform);
        WDHomeUIFactory.Stretch(chatLayer);
        // 07. Chat Geral V2: instancia a apresentação serializada e mantém o repositório existente.
        GameObject chatPrefab = Resources.Load<GameObject>("UI/Chat/WDChatHubV2");
        if (chatPrefab == null)
            throw new System.InvalidOperationException(
                "Prefab do Chat V2 ausente. Execute War Dominion > UI > Chat V2 > Criar Prefab Serializado.");
        GameObject chatInstance = Instantiate(chatPrefab, chatLayer, false);
        chatInstance.name = "WDChatHubV2";
        WDHomeUIFactory.Stretch((RectTransform)chatInstance.transform);
        chatPanel = chatInstance.GetComponent<WDChatHubPanel>() ??
            throw new System.InvalidOperationException("WDChatHubV2.prefab não possui WDChatHubPanel.");
        chatPanel.Build(theme, chatRepository, socialRepository, profileRepository,
            navigationStack, profilePanel.OpenPublicProfile, OpenSocialConfirmation, OpenFriends,
            chatBackgroundTexture, () => economyRepository.Load(economyCatalog), OpenNotificationCenter);

        RectTransform rankingLayer = WDHomeUIFactory.Rect("RankingLayer", root.transform);
        WDHomeUIFactory.Stretch(rankingLayer);
        rankingPanel = rankingLayer.gameObject.AddComponent<WDHomeRankingPanel>();
        rankingPanel.Build(theme, new WDRankingRepository(profileRepository), navigationStack,
            profilePanel.OpenPublicProfile, rankingBackgroundTexture, SetHomeCompositionVisible);

        RectTransform missionsLayer = WDHomeUIFactory.Rect("MissionsLayer", root.transform);
        WDHomeUIFactory.Stretch(missionsLayer);
        missionsPanel = missionsLayer.gameObject.AddComponent<WDHomeMissionsPanel>();
        missionsPanel.Build(theme, missionRepository, navigationStack, SetHomeCompositionVisible);

        RectTransform settingsLayer = WDHomeUIFactory.Rect("SettingsLayer", root.transform);
        WDHomeUIFactory.Stretch(settingsLayer);
        settingsPanel = settingsLayer.gameObject.AddComponent<WDHomeSettingsPanel>();
        settingsPanel.Build(theme, profileRepository, navigationStack,
            settingsBackgroundTexture, SetSettingsPresentationVisible,
            () => economyRepository.Load(economyCatalog), OpenNotificationCenter);
        // O shell fica atrás da SafeArea: os recursos globais conservam exatamente
        // os anchors e o posicionamento responsivo já aprovados na Home.
        settingsLayer.SetSiblingIndex(safeArea.GetSiblingIndex());

        RectTransform armoryLayer = WDHomeUIFactory.Rect("ArmoryLayer", root.transform);
        WDHomeUIFactory.Stretch(armoryLayer);
        armoryPanel = armoryLayer.gameObject.AddComponent<WDHomeArmoryPanel>();
        armoryPanel.Build(
            theme, armoryCatalog, armoryRepository,
            OpenLockedDeckPopup, navigationStack, SetHomeCompositionVisible);

        RectTransform storeLayer = WDHomeUIFactory.Rect("StoreLayer", root.transform);
        WDHomeUIFactory.Stretch(storeLayer);
        storePanel = storeLayer.gameObject.AddComponent<WDHomeStorePanel>();
        if (storeService != null) storePanel.Build(theme, storeService, navigationStack,
            storeBackgroundTexture, SetStorePresentationVisible, OpenNotificationCenter);
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
            OpenSocialConfirmation, economyRepository.Load(economyCatalog),
            SetClanPresentationVisible, OpenNotificationCenter);
        BuildFigmaFoundation(content);

        RectTransform notificationsLayer = WDHomeUIFactory.Rect("NotificationCenterLayer", root.transform);
        WDHomeUIFactory.Stretch(notificationsLayer);
        notificationCenterPanel = notificationsLayer.gameObject.AddComponent<WDHomeNotificationCenterPanel>();
        notificationCenterPanel.Build(theme);
    }

    // ============================================================
    // 03. HOME FIGMA 1A - FUNDO, IDENTIDADE E ACOES PRINCIPAIS
    // ============================================================

    private void BuildFigmaFoundation(RectTransform content)
    {
        RectTransform header = WDHomeUIFactory.Rect("FigmaHeader", content);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.offsetMin = new Vector2(64f, -130f);
        header.offsetMax = new Vector2(-64f, -40f);

        RectTransform account = WDHomeUIFactory.Rect("PlayerAccount", header);
        homeAccount = account;
        Place(account, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(330f, 90f),
            new Vector2(0f, 0.5f));
        Image accountSurface = account.gameObject.AddComponent<Image>();
        accountSurface.color = FigmaPanel;
        WDHomeUIFactory.ApplyRounded(accountSurface, 18);
        AddBorder(account, FigmaBorder);
        Button accountButton = account.gameObject.AddComponent<Button>();
        accountButton.transition = Selectable.Transition.None;
        accountButton.onClick.AddListener(ToggleAccountDropdown);
        RectTransform avatarFrame = WDHomeUIFactory.Rect("AvatarFrame", account);
        Place(avatarFrame, new Vector2(0f, 0.5f), new Vector2(39f, 0f),
            new Vector2(58f, 58f));
        Image avatarBorder = avatarFrame.gameObject.AddComponent<Image>();
        avatarBorder.color = FigmaGold;
        avatarBorder.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(avatarBorder, 29);
        RectTransform avatarClip = WDHomeUIFactory.Rect("AvatarClip", avatarFrame);
        Place(avatarClip, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
        Image avatarMask = avatarClip.gameObject.AddComponent<Image>();
        avatarMask.color = Color.white;
        avatarMask.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(avatarMask, 26);
        avatarClip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RawImage avatar = FigmaTexture("Avatar", avatarClip,
            WDProfileIdentityVisuals.ResolveAvatar(profileRepository.LocalProfile));
        homeAccountAvatar = avatar;
        Place(avatar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(52f, 52f));
        PreserveTextureAspect(avatar);
        homeNicknameLabel = FigmaLabel("Nickname", account,
            profileRepository.LocalProfile.Nickname, 19f, FigmaText);
        Place(homeNicknameLabel.rectTransform, new Vector2(0f, 0.5f),
            new Vector2(190f, 12f), new Vector2(205f, 30f));
        TextMeshProUGUI accountType = FigmaLabel("AccountType", account,
            WDRegionalPreferences.Localize(AccountCaption), 13f, FigmaMuted);
        Place(accountType.rectTransform, new Vector2(0f, 0.5f),
            new Vector2(190f, -16f), new Vector2(205f, 22f));
        RawImage chevron = FigmaTexture("AccountChevron", account, "Chevron");
        Place(chevron.rectTransform, new Vector2(1f, 0.5f),
            new Vector2(-25f, 0f), new Vector2(22f, 22f));
        BuildAccountDropdown(content);

        RectTransform resourceGroup = WDHomeUIFactory.Rect("ResourceGroup", header);
        homeResources = resourceGroup;
        Place(resourceGroup, new Vector2(1f, 0.5f), new Vector2(0f, 10f),
            new Vector2(270f, 48f), new Vector2(1f, 0.5f));
        // Auto Layout do node 581:2: 16 px entre moeda, esmeralda e sino.
        coinsLabel = BuildResourcePill(resourceGroup, "Coins", "Coin", 110f);
        emeraldsLabel = BuildResourcePill(resourceGroup, "Emeralds", "Gem", 80f);
        RectTransform notifications = WDHomeUIFactory.BuildCanonicalNotification(
            resourceGroup, OpenNotificationCenter);
        resourceNotification = notifications;
        Place(notifications, new Vector2(1f, 0.5f), Vector2.zero,
            new Vector2(48f, 48f), new Vector2(1f, 0.5f));
        Image unreadDot = WDHomeUIFactory.Image("UnreadIndicator", notifications,
            new Color(1f, 0.231f, 0.188f, 1f));
        WDHomeUIFactory.ApplyRounded(unreadDot, 4);
        Place(unreadDot.rectTransform, new Vector2(1f, 1f),
            new Vector2(-5f, -5f), new Vector2(8f, 8f));
        unreadDot.gameObject.SetActive(false);
        unreadIndicator = unreadDot.gameObject;

        RectTransform left = WDHomeUIFactory.Rect("LeftZone", content);
        homeLeftZone = left;
        Place(left, new Vector2(0f, 0f), new Vector2(64f, 22f),
            new Vector2(302f, 545f), Vector2.zero);
        BuildFinalLeft(left);

        RectTransform right = WDHomeUIFactory.Rect("RightZone", content);
        homeRightZone = right;
        Place(right, new Vector2(1f, 0f), new Vector2(-29f, 22f),
            new Vector2(401f, 502f), new Vector2(1f, 0f));
        BuildFinalRight(right);

        RectTransform hub = WDHomeUIFactory.Rect("CenterPlayHub", content);
        homeCenterHub = hub;
        Place(hub, new Vector2(0.5f, 0.5f), new Vector2(10f, 4f),
            new Vector2(860f, 620f));
        RectTransform medal = WDHomeUIFactory.Rect("EmblemMedallion", hub);
        Place(medal, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 307f), new Vector2(280f, 280f));
        RawImage medallionGold = FigmaTexture("MedallionGold", medal,
            WDDivisionVisuals.ResolveMedallion());
        Place(medallionGold.rectTransform, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(280f, 280f));
        medallionGold.raycastTarget = true;
        Button medalButton = medal.gameObject.AddComponent<Button>();
        medalButton.targetGraphic = medallionGold;
        medalButton.onClick.AddListener(OpenMyProfile);

        Texture2D divisionEmblem = ResolveDivisionEmblemTexture(
            profileRepository.LocalProfile.DivisionDefinition);
        RawImage emblem = FigmaTexture("Emblem", medal, divisionEmblem);
        Place(emblem.rectTransform, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(250f, 250f));
        WDDivisionVisuals.ApplyCalibration(emblem.rectTransform,
            profileRepository.LocalProfile.DivisionDefinition, 280f);
        heroNicknameLabel = FigmaLabel("PlayerName", hub,
            profileRepository.LocalProfile.Nickname, 40f, FigmaText,
            TextAlignmentOptions.Center);
        heroNicknameLabel.raycastTarget = true;
        heroNicknameLabel.gameObject.AddComponent<Button>().onClick.AddListener(OpenMyProfile);
        Place(heroNicknameLabel.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 112.5f), new Vector2(820f, 56f));

        RectTransform league = WDHomeUIFactory.Rect("LeagueStrip", hub);
        Place(league, new Vector2(0.5f, 0.5f), new Vector2(0f, 11f),
            new Vector2(860f, 94f));
        Image leagueSurface = league.gameObject.AddComponent<Image>();
        leagueSurface.color = FigmaPanel;
        WDHomeUIFactory.ApplyRounded(leagueSurface, 24);
        leagueSurface.raycastTarget = false;
        AddBorder(league, FigmaBorder);
        TextMeshProUGUI division = FigmaLabel("Division", league,
            profileRepository.LocalProfile.Division, 30f, FigmaText);
        Place(division.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(-225f, 12f), new Vector2(350f, 42f));
        TextMeshProUGUI trophies = FigmaLabel("Trophies", league,
            profileRepository.LocalProfile.DivisionPoints.ToString() + " " +
                WDRegionalPreferences.Localize(TrophiesCaption),
            16f, FigmaGold, TextAlignmentOptions.Right);
        homeTrophiesLabel = trophies;
        Place(trophies.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(245f, 12f), new Vector2(330f, 36f));
        Image progressTrack = WDHomeUIFactory.Image("ProgressTrack", league,
            new Color(0.165f, 0.173f, 0.196f, 1f));
        WDHomeUIFactory.ApplyRounded(progressTrack, 6);
        Place(progressTrack.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, -25f), new Vector2(788f, 18f));
        progressTrack.raycastTarget = false;
        Image progressFill = WDHomeUIFactory.Image("ProgressFill", progressTrack.transform,
            FigmaGold);
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(0.546f, 1f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;
        WDHomeUIFactory.ApplyRounded(progressFill, 6);
        progressFill.raycastTarget = false;

        RectTransform play = WDHomeUIFactory.Rect("MainPlayButton", hub);
        homePlayButton = play;
        Place(play, new Vector2(0.5f, 0.5f), new Vector2(0f, -118f),
            new Vector2(520f, 135f));
        Image playBorder = play.gameObject.AddComponent<Image>();
        playBorder.color = FigmaGold;
        WDHomeUIFactory.ApplyRounded(playBorder, 67);
        Shadow playGlow = play.gameObject.AddComponent<Shadow>();
        playGlow.effectColor = new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.33f);
        playGlow.effectDistance = Vector2.zero;
        playGlow.useGraphicAlpha = true;
        RectTransform inner = WDHomeUIFactory.Rect("Inner", play);
        WDHomeUIFactory.Stretch(inner, 5f, 5f, 5f, 5f);
        Image innerSurface = inner.gameObject.AddComponent<Image>();
        innerSurface.color = new Color(0.095f, 0.102f, 0.122f, 1f);
        WDHomeUIFactory.ApplyRounded(innerSurface, 62);
        innerSurface.raycastTarget = false;
        RawImage swords = FigmaTexture("CrossedSwords", inner, "CrossedSwordsGlyph");
        Place(swords.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(-110f, 0f), new Vector2(64f, 64f));
        TextMeshProUGUI playLabel = FigmaLabel("PlayLabel", inner, "PLAY", 66f,
            FigmaText, TextAlignmentOptions.Center);
        Place(playLabel.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(48f, 0f), new Vector2(220f, 90f));
        play.gameObject.AddComponent<Button>().onClick.AddListener(OpenMatchFlow);

        // 03. Cards centrais finais mantem as autoridades Arsenal e Cla.
        RectTransform secondary = WDHomeUIFactory.Rect("CenterSecondaryRow", content);
        homeSecondaryRow = secondary;
        Place(secondary, new Vector2(0.5f, 0f), new Vector2(HomeSecondaryOpticalOffsetX, 148f),
            new Vector2(700f, 130f), new Vector2(0.5f, 0f));
        BuildFinalWideCard(secondary, "ArmoryCard", -180f, "Briefcase",
            "ARSENAL", "ARMAS & DECKS", OpenArmory);
        RectTransform clanCard = BuildFinalWideCard(secondary, "ClanCard", 180f, "Shield",
            "CL\u00c3", "EQUIPE", OpenClan);
        ApplyApprovedClanContentOffsets(clanCard);
        RefreshCurrencies();
        RefreshChatBadge();
        RefreshSocialIndicators();
    }

    // ============================================================
    // 04. CARDS FINAIS DA FAMILIA HOME E ESTADOS CONDICIONAIS
    // ============================================================

    private void BuildFinalLeft(RectTransform parent)
    {
        BuildFinalSquareCard(parent, "RankingCard", 279.5f, "Trophy",
            "RANKING", FigmaNavigationGold, OpenRanking);
        BuildFinalSquareCard(parent, "MissionsCard", 139.7f, "Flag",
            "MISS\u00d5ES", FigmaNavigationGold, OpenMissions);
        chatCardRect = BuildFinalSquareCard(parent, "ChatCard", 0f, "Chat",
            "CHAT", FigmaNavigationGold, () => chatPanel.OpenFromHome());

        featuredTournamentCard = WDHomeUIFactory.Rect("FeaturedTournamentCard", parent);
        Place(featuredTournamentCard, new Vector2(0f, 0f), new Vector2(0f, 419.3f),
            new Vector2(302f, 125.5f), Vector2.zero);
        Image surface = featuredTournamentCard.gameObject.AddComponent<Image>();
        surface.color = new Color(22f / 255f, 24f / 255f, 29f / 255f, 0.90f);
        WDHomeUIFactory.ApplyRounded(surface, 18);
        AddBorder(featuredTournamentCard, FigmaGold);
        Button button = featuredTournamentCard.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => featuredTournamentAction?.Invoke());
        RawImage trophy = FigmaTexture("DiagonalTrophy", featuredTournamentCard,
            "TournamentTrophy");
        Place(trophy.rectTransform, new Vector2(1f, 0.5f),
            new Vector2(-70f, 0f), new Vector2(118f, 118f));
        trophy.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        BuildCardText(featuredTournamentCard, "TournamentState",
            "PR\u00d3XIMO TORNEIO", 10f, FigmaGold,
            new Vector2(15f, -13f), new Vector2(270f, 16f));
        featuredTournamentName = BuildCardText(featuredTournamentCard,
            "TournamentName", string.Empty, 16f, FigmaText,
            new Vector2(15f, -32f), new Vector2(265f, 25f));
        BuildCardText(featuredTournamentCard, "CountdownCaption", "COME\u00c7A EM",
            10f, new Color(0f, 0.898f, 1f),
            new Vector2(15f, -68f), new Vector2(260f, 15f));
        featuredTournamentCountdown = BuildCardText(featuredTournamentCard,
            "Countdown", string.Empty, 20f, FigmaText,
            new Vector2(15f, -84f), new Vector2(265f, 29f));
        featuredTournamentCard.gameObject.SetActive(false);
    }

    private RectTransform BuildFinalSquareCard(Transform parent, string name, float bottom,
        string iconName, string title, Color accent, UnityEngine.Events.UnityAction action)
    {
        RectTransform card = WDHomeUIFactory.Rect(name, parent);
        Place(card, new Vector2(0f, 0f), new Vector2(0f, bottom),
            new Vector2(302f, 125.5f), Vector2.zero);
        Image surface = card.gameObject.AddComponent<Image>();
        surface.color = new Color(22f / 255f, 24f / 255f, 29f / 255f, 0.90f);
        WDHomeUIFactory.ApplyRounded(surface, 18);
        AddBorder(card, new Color(accent.r, accent.g, accent.b, 0.58f));
        Button cardButton = card.gameObject.AddComponent<Button>();
        cardButton.transition = Selectable.Transition.None;
        cardButton.onClick.AddListener(action);
        RawImage identityIcon = BuildCardIcon(card, iconName,
            new Vector2(0f, -43f), 50f, 28f, new Vector2(0.5f, 1f));
        identityIcon.color = accent;
        BuildCardText(card, "Title", title, 21.3f, accent,
            new Vector2(0f, -81f), new Vector2(302f, 32f),
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f));
        return card;
    }

    private void BuildFinalRight(RectTransform parent)
    {
        homeProfileCard = BuildFinalWideCard(parent, "ProfileCard", 390f, "User",
            "PERFIL", "IDENTIDADE E PERSONALIZA\u00c7\u00c3O", OpenMyProfile,
            401f, 110.5f, FigmaNavigationGold);
        ApplyCanonicalProfileTreatment(homeProfileCard);
        RectTransform friends = BuildFinalWideCard(parent, "FriendsCard", 260f,
            "Canonical/FriendsGold",
            "AMIGOS", "GERENCIAR CONTATOS", OpenFriends,
            401f, 110.5f, FigmaNavigationGold);
        homeFriendsCard = friends;
        onlineFriendsLabel = BuildCardText(friends, "OnlineCount", string.Empty, 10f,
            new Color(0f, 0.898f, 1f), new Vector2(-9f, -15f),
            new Vector2(80f, 19f), TextAlignmentOptions.Right, new Vector2(1f, 1f));
        homeStoreCard = BuildFinalWideCard(parent, "StoreCard", 130f, "ShoppingCart",
            "LOJA", "OFERTAS EXCLUSIVAS", OpenStore,
            401f, 110.5f, FigmaNavigationGold, true);
        ApplyPremiumStoreTreatment(homeStoreCard);
        homeSettingsCard = BuildFinalWideCard(parent, "SettingsCard", 0f,
            "Canonical/SettingsGold",
            "CONFIGURA\u00c7\u00d5ES", "SOM, GR\u00c1FICOS E SISTEMA", OpenSettings,
            401f, 110.5f, FigmaNavigationGold);
    }

    // ============================================================
    // 04.1. HIERARQUIA VISUAL CANÔNICA DOS CARDS ESPECIAIS
    // ============================================================

    private void ApplyCanonicalProfileTreatment(RectTransform card)
    {
        // O gradiente é uma textura pequena e compartilhada. Ele reproduz a
        // progressão navy -> petróleo/ciano sem materiais únicos por instância.
        Image surface = card.GetComponent<Image>();
        if (surface != null)
            surface.color = new Color(0.025f, 0.055f, 0.075f, 0.96f);

        RectTransform gradientRect = WDHomeUIFactory.Rect("ProfileNavyTealGradient", card);
        RawImage gradient = gradientRect.gameObject.AddComponent<RawImage>();
        gradient.texture = GetProfileGradientTexture();
        gradient.color = Color.white;
        WDHomeUIFactory.Stretch(gradient.rectTransform);
        gradient.raycastTarget = false;
        gradient.transform.SetAsFirstSibling();
        card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        Transform border = card.Find("Border");
        if (border != null)
            border.GetComponent<Image>().color =
                new Color(0.78f, 0.60f, 0.30f, 0.92f);

        Transform iconFrame = card.Find("IconFrame");
        if (iconFrame != null)
        {
            Image iconSurface = iconFrame.GetComponent<Image>();
            if (iconSurface != null)
                iconSurface.color = new Color(0.014f, 0.086f, 0.166f, 0.92f);
            Transform iconBorder = iconFrame.Find("Border");
            if (iconBorder != null)
                iconBorder.GetComponent<Image>().color =
                    new Color(0.91f, 0.67f, 0.18f, 0.72f);
            Shadow iconGlow = iconFrame.gameObject.AddComponent<Shadow>();
            iconGlow.effectColor = new Color(0.02f, 0.55f, 0.67f, 0.22f);
            iconGlow.effectDistance = Vector2.zero;
            iconGlow.useGraphicAlpha = true;
        }

        // A silhueta grande ocupa o lado direito como elemento ambiental,
        // mantendo o ícone pequeno como identificação funcional do card.
        // Assets separados, exportados diretamente dos nodes atuais do Figma:
        // Perfil nunca compartilha a representação de múltiplas pessoas de Amigos.
        Transform smallIcon = card.Find("IconFrame/Icon");
        if (smallIcon != null)
            smallIcon.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(
                "UI/Home/FigmaPhase1A/Canonical/ProfileUser");

        RawImage identity = FigmaTexture("DecorativeProfileIdentity", card,
            "Canonical/ProfileUserDecorative");
        identity.color = Color.white;
        Place(identity.rectTransform, new Vector2(1f, 0.5f),
            new Vector2(-53f, 0f), new Vector2(106f, 104f));
        identity.transform.SetSiblingIndex(1);

        Shadow depth = card.gameObject.AddComponent<Shadow>();
        depth.effectColor = new Color(0f, 0.64f, 0.78f, 0.22f);
        depth.effectDistance = new Vector2(0f, -2f);
        depth.useGraphicAlpha = true;

        Transform title = card.Find("Title");
        if (title != null)
            title.GetComponent<TextMeshProUGUI>().color = FigmaText;
    }

    private static void ApplyPremiumStoreTreatment(RectTransform card)
    {
        Image surface = card.GetComponent<Image>();
        if (surface != null)
            surface.color = new Color(0.12f, 0.105f, 0.07f, 0.90f);
        Transform border = card.Find("Border");
        if (border != null)
            border.GetComponent<Image>().color =
                new Color(0.91f, 0.67f, 0.18f, 1f);

        Shadow glow = card.gameObject.AddComponent<Shadow>();
        glow.effectColor = new Color(0.91f, 0.67f, 0.18f, 0.52f);
        glow.effectDistance = Vector2.zero;
        glow.useGraphicAlpha = true;
    }

    private static Texture2D GetProfileGradientTexture()
    {
        if (profileGradientTexture != null)
            return profileGradientTexture;

        const int width = 128;
        var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false, true)
        {
            name = "WDHomeProfileNavyTealGradient",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[width];
        Color[] stops =
        {
            new Color(0.018f, 0.035f, 0.075f, 0.98f),
            new Color(0.018f, 0.0466f, 0.0881f, 0.977f),
            new Color(0.018f, 0.075f, 0.12f, 0.97f),
            new Color(0.02f, 0.18f, 0.23f, 0.96f),
            new Color(0.015f, 0.29f, 0.35f, 0.96f)
        };
        float[] positions = { 0f, 0.122f, 0.42f, 0.74f, 1f };
        for (int x = 0; x < width; x++)
        {
            float t = x / (width - 1f);
            int segment = 0;
            while (segment < positions.Length - 2 && t > positions[segment + 1])
                segment++;
            float local = Mathf.InverseLerp(positions[segment], positions[segment + 1], t);
            pixels[x] = Color.Lerp(stops[segment], stops[segment + 1], local);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        profileGradientTexture = texture;
        return texture;
    }

    private RectTransform BuildFinalWideCard(Transform parent, string name, float bottom,
        string iconName, string title, string subtitle,
        UnityEngine.Events.UnityAction action, float width = 340f, float height = 130f,
        Color? accentOverride = null, bool vip = false)
    {
        bool centerCluster = width < 400f;
        RectTransform card = WDHomeUIFactory.Rect(name, parent);
        Place(card, centerCluster ? new Vector2(0.5f, 0f) : Vector2.zero,
            new Vector2(centerCluster ? bottom : 0f, centerCluster ? 0f : bottom),
            new Vector2(width, height), centerCluster ? new Vector2(0.5f, 0f) : Vector2.zero);
        Color accent = accentOverride ?? FigmaGold;
        Image surface = card.gameObject.AddComponent<Image>();
        surface.color = new Color(22f / 255f, 24f / 255f, 29f / 255f, 0.90f);
        WDHomeUIFactory.ApplyRounded(surface, 24);
        AddBorder(card, new Color(accent.r, accent.g, accent.b, 0.58f));
        Button cardButton = card.gameObject.AddComponent<Button>();
        cardButton.transition = Selectable.Transition.None;
        cardButton.onClick.AddListener(action);
        RectTransform contentGroup = card;
        if (centerCluster)
        {
            contentGroup = WDHomeUIFactory.Rect("ContentGroup", card);
            WDHomeUIFactory.Stretch(contentGroup);
        }
        float iconFrameSize = centerCluster ? 44f : 42f;
        float iconSize = 24f;
        Vector2 iconAnchor = new Vector2(0f, 0.5f);
        BuildCardIcon(contentGroup, iconName,
            centerCluster ? new Vector2(100f, 0f) : new Vector2(42f, 0f),
            iconFrameSize, iconSize, iconAnchor);
        float textLeft = centerCluster ? 142f : 85f;
        BuildCardText(contentGroup, "Title", title, centerCluster ? 24f : 17.58f, accent,
            new Vector2(textLeft, centerCluster ? -36f : -30f),
            new Vector2(width - textLeft - 20f, centerCluster ? 30f : 32f));
        BuildCardText(contentGroup, "Subtitle", subtitle, centerCluster ? 14f : 13.82f, FigmaMuted,
            new Vector2(textLeft, centerCluster ? -73f : -59f),
            new Vector2(width - textLeft - 20f, centerCluster ? 22f : 29f));
        if (vip)
            BuildCardText(card, "Vip", "VIP", 10f, FigmaGold,
                new Vector2(-12f, -15f), new Vector2(40f, 20f),
                TextAlignmentOptions.Right, new Vector2(1f, 1f));
        return card;
    }

    private static void ApplyApprovedClanContentOffsets(RectTransform card)
    {
        if (card == null)
            return;
        RectTransform contentGroup = card.Find("ContentGroup") as RectTransform;
        if (contentGroup != null)
        {
            // Valor aprovado no runtime: Left 30, Right -30, Top/Bottom 0.
            contentGroup.offsetMin = new Vector2(30f, 0f);
            contentGroup.offsetMax = new Vector2(-30f, 0f);
        }
    }

    private RawImage BuildCardIcon(RectTransform card, string asset, Vector2 position,
        float frameSize, float iconSize, Vector2 anchor)
    {
        RectTransform frame = WDHomeUIFactory.Rect("IconFrame", card);
        Place(frame, anchor, position, new Vector2(frameSize, frameSize));
        Image circle = frame.gameObject.AddComponent<Image>();
        circle.color = new Color(0.22f, 0.16f, 0.08f, 0.65f);
        WDHomeUIFactory.ApplyRounded(circle, 24);
        circle.raycastTarget = false;
        AddBorder(frame, new Color(0.96f, 0.78f, 0.37f, 0.5f));
        RawImage icon = FigmaTexture("Icon", frame, asset);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(iconSize, iconSize));
        return icon;
    }

    private TextMeshProUGUI BuildCardText(Transform parent, string name, string value,
        float size, Color color, Vector2 offset, Vector2 bounds,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left,
        Vector2? anchor = null)
    {
        TextMeshProUGUI text = FigmaLabel(name, parent, value, size, color, alignment);
        Vector2 placementAnchor = anchor ?? new Vector2(0f, 1f);
        Place(text.rectTransform, placementAnchor, offset, bounds,
            new Vector2(placementAnchor.x, 1f));
        return text;
    }

    public void BindFeaturedTournament(WDTournament tournament, string countdown,
        UnityEngine.Events.UnityAction openTournament)
    {
        // Somente o fornecedor do torneio define relevancia, tempo e destino.
        bool show = tournament != null && !string.IsNullOrWhiteSpace(countdown) &&
            openTournament != null;
        featuredTournamentAction = show ? openTournament : null;
        if (featuredTournamentCard == null) return;
        featuredTournamentCard.gameObject.SetActive(show);
        if (!show) return;
        featuredTournamentName.text = tournament.Name;
        featuredTournamentCountdown.text = countdown;
    }

    public void BindGlobalUnread(bool hasUnread)
    {
        // Binding visual somente: nenhuma persistencia ou autoridade paralela.
        if (unreadIndicator != null)
            unreadIndicator.SetActive(hasUnread);
    }

    private void RefreshSocialIndicators()
    {
        if (socialRepository == null) return;
        if (onlineFriendsLabel != null)
        {
            int count = socialRepository.Friends().Count(friend =>
                socialRepository.PresenceOf(friend.PlayerId) == WDSocialPresence.Online);
            onlineFriendsLabel.text = count > 0 ? count + " ONLINE" : string.Empty;
        }
    }

    private TextMeshProUGUI BuildResourcePill(Transform parent, string name,
        string iconName, float width)
    {
        TextMeshProUGUI number = WDHomeUIFactory.BuildCanonicalResourcePill(
            name, parent, iconName, width, out RectTransform pill);
        Place(pill, new Vector2(1f, 0.5f), Vector2.zero,
            new Vector2(width, 40f), new Vector2(1f, 0.5f));
        return number;
    }

    private void RefreshCurrencies()
    {
        if (economyRepository == null || economyCatalog == null ||
            coinsLabel == null || emeraldsLabel == null)
            return;
        WDArmoryProfile profile = economyRepository.Load(economyCatalog);
        WDHomeUIFactory.SetResourcePillValue(coinsLabel, profile.Coins, 110f, 180f);
        WDHomeUIFactory.SetResourcePillValue(emeraldsLabel, profile.Emeralds, 80f, 150f);
        WDHomeUIFactory.LayoutResourceGroup(coinsLabel, emeraldsLabel,
            resourceNotification, 110f, 80f);
    }

    private static void Place(RectTransform rect, Vector2 anchor,
        Vector2 offset, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private static void AddBorder(RectTransform target, Color color)
    {
        Image surface = target.GetComponent<Image>();
        int radius = surface != null && surface.sprite != null
            ? Mathf.RoundToInt((surface.sprite.rect.width - 4f) * 0.5f) : 12;
        WDHomeUIFactory.AddRoundedBorder(target, color, radius);
    }

    private static Texture2D ResolveDivisionEmblemTexture(WDDivisionDefinition division)
        => WDDivisionVisuals.ResolveEmblem(division);

    private RawImage FigmaTexture(string name, Transform parent, string asset)
    {
        return FigmaTexture(name, parent,
            Resources.Load<Texture2D>("UI/Home/FigmaPhase1A/" + asset));
    }

    private RawImage FigmaTexture(string name, Transform parent, Texture2D texture)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI FigmaLabel(string name, Transform parent, string value,
        float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme,
            value, size, color, alignment);
        // Tipografia do node 2319:143: Gabarito Black para identidade/ações
        // e Geist para informação secundária. O projeto possui Geist Medium
        // como fallback oficial para o Regular usado no Figma.
        bool secondary = name == "AccountType" || name == "Subtitle" ||
            name == "Trophies" || name == "CountdownCaption";
        label.font = secondary ? geistMedium : gabaritoBlack;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
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
            theme, "!", OpenNotificationCenter);
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
            theme, "C", "CHAT", "Mensagens", () => chatPanel.OpenFromHome(), 132f,
            new Color(0f, 0.898f, 1f), false);
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
        if (socialRepository != null)
            socialRepository.Changed -= RefreshSocialIndicators;
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
            OpenArmory, 140f, null, true);
        RectTransform clanRect = WDHomeUIFactory.Rect("ClanCard", lower);
        clanRect.gameObject.AddComponent<WDHomeCard>().Build(
            theme, "C", "CLÃ", "Comunidade competitiva", OpenClan, 140f,
            null, true);
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
        if (accountDropdownRoot != null && accountDropdownRoot.activeSelf)
        {
            CloseAccountDropdown();
            return true;
        }

        if (notificationCenterPanel != null && notificationCenterPanel.IsOpen)
        {
            notificationCenterPanel.Close();
            return true;
        }

        if (modal != null && modal.IsOpen)
        {
            modal.RequestClose();
            return true;
        }

        // 01. O Chat no topo usa a pilha existente; painéis cobertos não consomem
        // o comando de voltar destinado ao picker/menu/busca ou à própria tela.
        if (navigationStack?.CurrentId == "chat" && chatPanel != null && chatPanel.IsOpen)
            return chatPanel.TryHandleLocalBack();

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

    private void OpenNotificationCenter() => notificationCenterPanel?.Open();

    public void BindNotificationItems(IReadOnlyList<WDHomeNotificationItem> validatedItems)
        => notificationCenterPanel?.SetItems(validatedItems);

    private void OpenMyProfile()
    {
        CloseAccountDropdown();
        profilePanel.OpenMyProfile();
    }

    private void OpenSettings() => settingsPanel.OpenGeneral();

    private void SetHomeCompositionVisible(bool visible)
    {
        if (homeAccount != null) homeAccount.gameObject.SetActive(visible);
        if (homeLeftZone != null) homeLeftZone.gameObject.SetActive(visible);
        if (homeRightZone != null) homeRightZone.gameObject.SetActive(visible);
        if (homeCenterHub != null) homeCenterHub.gameObject.SetActive(visible);
        if (homeSecondaryRow != null) homeSecondaryRow.gameObject.SetActive(visible);
        if (!visible) CloseAccountDropdown();
    }

    private void SetSettingsPresentationVisible(bool visible)
    {
        SetHomeCompositionVisible(visible);
        if (homeResources != null)
            homeResources.gameObject.SetActive(visible);
    }

    // ============================================================
    // 12.1. APRESENTAÇÃO FULLSCREEN EXCLUSIVA DO CLÃ
    // ============================================================
    private void SetClanPresentationVisible(bool visible)
    {
        SetHomeCompositionVisible(visible);
        if (homeResources != null)
            homeResources.gameObject.SetActive(visible);
    }

    // ============================================================
    // 12.2. APRESENTAÇÃO FULLSCREEN EXCLUSIVA DE AMIGOS
    // ============================================================
    private void SetFriendsPresentationVisible(bool visible)
    {
        SetHomeCompositionVisible(visible);
        if (homeResources != null)
            homeResources.gameObject.SetActive(visible);
    }

    // ============================================================
    // 12.3. APRESENTAÇÃO FULLSCREEN EXCLUSIVA DA LOJA
    // ============================================================
    private void SetStorePresentationVisible(bool visible)
    {
        SetHomeCompositionVisible(visible);
        if (homeResources != null)
            homeResources.gameObject.SetActive(visible);
    }

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
        if (heroNicknameLabel != null)
            heroNicknameLabel.text = profileRepository.LocalProfile.Nickname;
        Texture2D avatar = WDProfileIdentityVisuals.ResolveAvatar(profileRepository.LocalProfile);
        if (homeAccountAvatar != null)
        {
            homeAccountAvatar.texture = avatar;
            PreserveTextureAspect(homeAccountAvatar);
        }
        if (accountDropdownAvatar != null)
        {
            accountDropdownAvatar.texture = avatar;
            PreserveTextureAspect(accountDropdownAvatar);
        }
        if (accountDropdownNickname != null)
            accountDropdownNickname.text = profileRepository.LocalProfile.Nickname;
    }

    private void BuildAccountDropdown(RectTransform content)
    {
        RectTransform root = WDHomeUIFactory.Rect("AccountDropdownLayer", content);
        WDHomeUIFactory.Stretch(root);
        accountDropdownRoot = root.gameObject;

        RectTransform outside = WDHomeUIFactory.Rect("OutsideDismiss", root);
        WDHomeUIFactory.Stretch(outside);
        Image outsideHit = outside.gameObject.AddComponent<Image>();
        outsideHit.color = new Color(0f, 0f, 0f, 0.001f);
        Button outsideButton = outside.gameObject.AddComponent<Button>();
        outsideButton.transition = Selectable.Transition.None;
        outsideButton.onClick.AddListener(CloseAccountDropdown);

        RectTransform panel = WDHomeUIFactory.Rect("AccountDropdown", root);
        Place(panel, new Vector2(0f, 1f), new Vector2(64f, -140f),
            new Vector2(330f, 176f), new Vector2(0f, 1f));
        Image surface = panel.gameObject.AddComponent<Image>();
        surface.color = new Color(0.025f, 0.041f, 0.070f, 0.98f);
        WDHomeUIFactory.ApplyRounded(surface, 14);
        AddBorder(panel, new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.72f));

        RectTransform current = WDHomeUIFactory.Rect("CurrentLocalIdentity", panel);
        Place(current, new Vector2(0.5f, 1f), new Vector2(0f, -49f),
            new Vector2(306f, 74f));
        Image currentSurface = current.gameObject.AddComponent<Image>();
        currentSurface.color = new Color(0.055f, 0.071f, 0.105f, 0.96f);
        WDHomeUIFactory.ApplyRounded(currentSurface, 10);

        RectTransform avatarClip = WDHomeUIFactory.Rect("AvatarClip", current);
        Place(avatarClip, new Vector2(0f, 0.5f), new Vector2(38f, 0f),
            new Vector2(52f, 52f));
        Image avatarMask = avatarClip.gameObject.AddComponent<Image>();
        avatarMask.color = Color.white;
        WDHomeUIFactory.ApplyRounded(avatarMask, 26);
        avatarClip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        accountDropdownAvatar = FigmaTexture("Avatar", avatarClip,
            WDProfileIdentityVisuals.ResolveAvatar(profileRepository.LocalProfile));
        Place(accountDropdownAvatar.rectTransform, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(52f, 52f));
        PreserveTextureAspect(accountDropdownAvatar);

        accountDropdownNickname = FigmaLabel("Nickname", current,
            profileRepository.LocalProfile.Nickname, 17f, FigmaText);
        Place(accountDropdownNickname.rectTransform, new Vector2(0f, 0.5f),
            new Vector2(174f, 9f), new Vector2(197f, 26f));
        TextMeshProUGUI localCaption = FigmaLabel("AccountType", current,
            "IDENTIDADE LOCAL", 11f, FigmaMuted);
        Place(localCaption.rectTransform, new Vector2(0f, 0.5f),
            new Vector2(174f, -14f), new Vector2(197f, 20f));
        Image activeDot = WDHomeUIFactory.Image("ActiveIndicator", current,
            new Color(0f, 0.941f, 1f, 1f));
        WDHomeUIFactory.ApplyRounded(activeDot, 4);
        Place(activeDot.rectTransform, new Vector2(1f, 0.5f),
            new Vector2(-17f, 0f), new Vector2(8f, 8f));

        RectTransform link = WDHomeUIFactory.Rect("LinkAnotherAccount", panel);
        Place(link, new Vector2(0.5f, 0f), new Vector2(0f, 15f),
            new Vector2(306f, 52f), new Vector2(0.5f, 0f));
        WDUIPremiumButton linkButton = link.gameObject.AddComponent<WDUIPremiumButton>();
        linkButton.Build(theme, "VINCULAR OUTRA CONTA  ·  EM BREVE", () =>
        {
            CloseAccountDropdown();
            OpenModal("VINCULAR OUTRA CONTA", "Integração de autenticação ainda não disponível.");
        });
        linkButton.SetPersistentColors(new Color(0.031f, 0.055f, 0.090f, 1f),
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.14f));
        linkButton.SetLabelColor(FigmaGold);
        accountDropdownRoot.SetActive(false);
    }

    private void ToggleAccountDropdown()
    {
        if (accountDropdownRoot == null)
            return;
        bool open = !accountDropdownRoot.activeSelf;
        accountDropdownRoot.SetActive(open);
        if (open)
        {
            RefreshHomeIdentity();
            accountDropdownRoot.transform.SetAsLastSibling();
        }
    }

    private void CloseAccountDropdown()
    {
        if (accountDropdownRoot != null)
            accountDropdownRoot.SetActive(false);
    }

    private static void PreserveTextureAspect(RawImage image)
    {
        if (image == null)
            return;
        AspectRatioFitter aspect = image.GetComponent<AspectRatioFitter>() ??
            image.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = image.texture != null
            ? (float)image.texture.width / image.texture.height : 1f;
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
        // 02.1. Pivôs nas bordas preservam a Safe Area; cada família recebe
        // somente seu ganho ergonômico compartilhado, sem escalar o hub central.
        if (homeAccount != null)
            homeAccount.localScale = Vector3.one * WDHomeMobileLayout.HomeAccountScale;
        WDHomeMobileLayout.ApplyHeaderResourceScale(homeResources);
        if (homeLeftZone != null)
            homeLeftZone.localScale = Vector3.one * WDHomeMobileLayout.HomeLeftCardScale;
        if (homeRightZone != null)
            homeRightZone.localScale = Vector3.one * WDHomeMobileLayout.HomeRightCardScale;
        if (homeSecondaryRow != null)
        {
            homeSecondaryRow.localScale = Vector3.one * WDHomeMobileLayout.HomeSecondaryCardScale;
            homeSecondaryRow.anchoredPosition =
                new Vector2(HomeSecondaryOpticalOffsetX, WDHomeMobileLayout.HomeSecondaryBottom);
        }
        if (homePlayButton != null)
        {
            homePlayButton.localScale = Vector3.one * WDHomeMobileLayout.HomePlayScale;
            homePlayButton.anchoredPosition =
                new Vector2(0f, WDHomeMobileLayout.HomePlayVerticalOffset);
        }
        if (homeTrophiesLabel != null)
            homeTrophiesLabel.fontSize = WDHomeMobileLayout.HomeTrophiesSize;
        SetRightSubtitleSize(homeProfileCard);
        SetRightSubtitleSize(homeFriendsCard);
        SetRightSubtitleSize(homeStoreCard);
        SetRightSubtitleSize(homeSettingsCard);
        // 02.2. O passo único comprime o bloco direito apenas no mobile.
        float rightStep = WDHomeMobileLayout.RightCardStep;
        if (homeProfileCard != null)
            homeProfileCard.anchoredPosition = new Vector2(0f, 3f * rightStep);
        if (homeFriendsCard != null)
            homeFriendsCard.anchoredPosition = new Vector2(0f, 2f * rightStep);
        if (homeStoreCard != null)
            homeStoreCard.anchoredPosition = new Vector2(0f, rightStep);
        if (homeSettingsCard != null)
            homeSettingsCard.anchoredPosition = Vector2.zero;
        notificationCenterPanel?.SetSafeArea(safeArea.anchorMin, safeArea.anchorMax);
    }

    // 02.3. Somente o subtítulo dos quatro cards direitos recebe ganho de leitura.
    private static void SetRightSubtitleSize(RectTransform card)
    {
        if (card == null) return;
        Transform subtitle = card.Find("Subtitle");
        if (subtitle == null) return;
        TextMeshProUGUI label = subtitle.GetComponent<TextMeshProUGUI>();
        if (label != null)
            label.fontSize = WDHomeMobileLayout.HomeRightSubtitleSize;
    }
}

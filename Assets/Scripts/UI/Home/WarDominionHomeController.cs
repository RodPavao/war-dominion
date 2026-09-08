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
    private RectTransform safeArea;
    private GameObject homeUIRoot;
    private EventSystem homeEventSystem;
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

        if (modal != null && modal.IsOpen)
            modal.RequestClose();
        else if (armoryPanel != null && armoryPanel.IsOpen)
            armoryPanel.HandleEscape();
        else if (matchFlowPanel != null && matchFlowPanel.IsOpen)
            matchFlowPanel.HandleEscape();
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
        var armoryRepository = new WDPlayerPrefsArmoryRepository();
        RectTransform matchFlowLayer = WDHomeUIFactory.Rect("MatchFlowLayer", root.transform);
        WDHomeUIFactory.Stretch(matchFlowLayer);
        matchFlowPanel = matchFlowLayer.gameObject.AddComponent<WDHomeMatchFlowPanel>();
        matchFlowPanel.Build(
            theme, matchConfig, mockData, armoryCatalog, armoryRepository);

        RectTransform armoryLayer = WDHomeUIFactory.Rect("ArmoryLayer", root.transform);
        WDHomeUIFactory.Stretch(armoryLayer);
        armoryPanel = armoryLayer.gameObject.AddComponent<WDHomeArmoryPanel>();
        armoryPanel.Build(
            theme, armoryCatalog, armoryRepository,
            OpenLockedDeckPopup);
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
        CreateCard(lowerGroup, "M", "MISSÕES", "Objetivos e recompensas provisórias", "MISSÕES", MissionBody());
        CreateCard(lowerGroup, "R", "RANKING", "Classificação competitiva", "RANKING", RankingBody());
        CreateCard(lowerGroup, "E", "EVENTOS / TORNEIOS", $"{mockData.RegistrationState} · {mockData.Countdown}", "EVENTOS / TORNEIOS", EventBody(), 132f);
    }

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

        TextMeshProUGUI nickname = WDHomeUIFactory.Text(
            "Nickname", identity, theme, mockData.Nickname, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        nickname.fontStyle = FontStyles.Bold;
        nickname.rectTransform.anchorMin = new Vector2(0f, 0.53f);
        nickname.rectTransform.anchorMax = new Vector2(1f, 0.82f);
        nickname.rectTransform.offsetMin = new Vector2(126f, 0f);
        nickname.rectTransform.offsetMax = new Vector2(-54f, 0f);

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
        button.onClick.AddListener(() => OpenModal("PERFIL", ProfileBody()));
    }

    private void BuildCenter(RectTransform parent)
    {
        RectTransform league = WDHomeUIFactory.Rect("LeagueSummary", parent);
        Image surface = league.gameObject.AddComponent<Image>();
        surface.color = theme.SurfaceGlass;
        LayoutElement element = league.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 112f;
        TextMeshProUGUI leagueText = WDHomeUIFactory.Text(
            "League", league, theme, mockData.League.ToUpperInvariant(), theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        leagueText.fontStyle = FontStyles.Bold;
        leagueText.rectTransform.anchorMin = new Vector2(0f, 0.38f);
        leagueText.rectTransform.anchorMax = Vector2.one;
        leagueText.rectTransform.offsetMin = leagueText.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI trophyText = WDHomeUIFactory.Text(
            "Trophies", league, theme, $"*  {mockData.Trophies} TROFÉUS",
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
            theme, "A", "ARMAS", "Decks, coleção e progressão",
            OpenArmory, 140f);
        CreateCard(lower, "C", "CLAN", "Comunidade competitiva", "CLAN", ClanBody(), 140f);
    }

    private void BuildRight(RectTransform parent)
    {
        CreateCard(parent, "P", "PERFIL", "Identidade e personalização", "PERFIL", ProfileBody(), 112f);
        CreateCard(parent, "A", "AMIGOS", "Lista, presença e convites", "AMIGOS", FriendsBody(), 104f);

        RectTransform smallRow = CreateRow("CommunicationRow", parent, 96f);
        CreateCard(smallRow, "C", "CHAT", "Mensagens", "CHAT", ChatBody(), 90f);
        CreateCard(smallRow, "S", "CONFIGURAÇÕES", "Preferências", "CONFIGURAÇÕES", SettingsBody(), 90f);

        RectTransform commerceRow = CreateRow("CommerceRow", parent, 112f);
        CreateCard(commerceRow, "L", "LOJA", "Modelo pendente", "LOJA", StoreBody(), 106f);
        CreateCard(commerceRow, "V", "VIP", "Detalhes pendentes", "VIP", VipBody(), 106f);

        CreateCard(parent, "D", "DISCORD", "Comunidade oficial · ação provisória", "DISCORD", DiscordBody(), 76f);
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

    private void OpenArmory() => armoryPanel.Open();

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
        OpenModal(
            "LOJA / VIP",
            "Destino funcional provisório da subseção VIP da Loja. Assinatura, pagamento e benefícios adicionais ainda não foram implementados.");
    }

    private string ProfileBody() =>
        $"{mockData.Nickname}\nStatus: {mockData.Status}\nLiga: {mockData.League}\nTroféus: {mockData.Trophies}\n\n" +
        "Avatar, skin, cor e demais opções de personalização serão centralizados aqui.";

    private string MissionBody() => "Objetivos e recompensas demonstrativos.\n\nO catálogo definitivo de missões permanece pendente.";
    private string RankingBody() => $"Posição competitiva provisória.\nLiga atual: {mockData.League}\n\nThresholds e matchmaking não foram definidos.";
    private string EventBody() => $"PRÓXIMO: {mockData.NextEvent}\n{mockData.RegistrationState}\nCountdown: {mockData.Countdown}\n\nAGORA: {mockData.LiveEvent}";
    private string ClanBody() => "Fundação para identidade, membros e atividades do Clan.";
    private string FriendsBody() => "Lista de amigos, presença, convites e ações sociais — dados mock nesta etapa.";
    private string ChatBody() => "Painel contextual de chat.\n\nBackend e persistência não fazem parte desta etapa.";
    private string SettingsBody() => "Preferências de áudio, vídeo, interface e controles serão centralizadas aqui.";
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

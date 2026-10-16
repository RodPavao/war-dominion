using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum WDSettingsSection { General, Audio, Graphics, Language, Chat, Privacy, Account }

[DisallowMultipleComponent]
public sealed class WDHomeSettingsPanel : MonoBehaviour
{
    // ============================================================
    // 01. REFERÊNCIAS, ESTADO E NAVEGAÇÃO
    // ============================================================

    private WarDominionUITheme theme;
    private WDProfileRepository repository;
    private WDNavigationStack navigation;
    private WDSettingsRepository settings;
    private RectTransform responsiveShell;
    private WDHomeSecondaryScreenFrame responsiveFrame;
    private RectTransform content;
    private RectTransform chrome;
    private Func<WDArmoryProfile> loadWallet;
    private Action openNotifications;
    private bool timeZonePickerOpen;
    private bool languagePickerOpen;
    private Action<bool> setHomeCompositionVisible;
    // 01.1. O conteúdo local compartilha o inset externo canônico do Header.
    private const float SettingsOuterMargin = 32f;

    private static readonly Color FigmaGold = new Color(0.773f, 0.608f, 0.388f, 1f);
    private static readonly Color FigmaInk = new Color(0.043f, 0.047f, 0.055f, 1f);
    private static readonly Color FigmaSurface = new Color(0.022f, 0.035f, 0.055f, 0.975f);
    private static readonly Color FigmaNavSurface = new Color(0.018f, 0.031f, 0.050f, 0.965f);
    private static readonly Color FigmaGoldBorder = new Color(0.773f, 0.608f, 0.388f, 0.20f);
    private static readonly Color FigmaDivider = new Color(1f, 1f, 1f, 0.07f);

    public bool IsOpen => gameObject.activeSelf;
    public WDSettingsSection CurrentSection { get; private set; } = WDSettingsSection.Privacy;
    public WDProfilePrivacy CurrentPrivacy => repository.LocalProfile.Privacy;

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDNavigationStack navigationStack) =>
        Build(newTheme, newRepository, navigationStack, null, null, null, null);

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDNavigationStack navigationStack, Texture2D backgroundTexture,
        Action<bool> homeCompositionVisibility) =>
        Build(newTheme, newRepository, navigationStack, backgroundTexture,
            homeCompositionVisibility, null, null);

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDNavigationStack navigationStack, Texture2D backgroundTexture,
        Action<bool> homeCompositionVisibility, Func<WDArmoryProfile> walletLoader,
        Action notificationAction)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        repository = newRepository ?? throw new ArgumentNullException(nameof(newRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        settings = new WDSettingsRepository();
        setHomeCompositionVisible = homeCompositionVisibility;
        loadWallet = walletLoader;
        openNotifications = notificationAction;

        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 1f);
        blocker.raycastTarget = true;

        RectTransform background = WDHomeUIFactory.Rect("SettingsBackground", transform);
        background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        RawImage art = background.gameObject.AddComponent<RawImage>();
        art.texture = backgroundTexture;
        art.color = art.texture != null ? Color.white : theme.BackgroundPrimary;
        art.raycastTarget = false;
        AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = art.texture != null
            ? (float)art.texture.width / art.texture.height : 16f / 9f;

        Image atmosphere = WDHomeUIFactory.Image("SettingsAtmosphere", transform,
            new Color(0.015f, 0.022f, 0.035f, 0.58f));
        WDHomeUIFactory.Stretch(atmosphere.rectTransform);
        atmosphere.raycastTarget = false;

        // 01.1. Header e conteúdo passam a compartilhar a mesma autoridade de
        // coordenadas. O cenário permanece full bleed fora deste shell.
        responsiveShell = WDHomeUIFactory.Rect("SettingsResponsiveShell", transform);
        responsiveFrame = WDHomeSecondaryScreenFrame.Configure(responsiveShell);
        content = WDHomeUIFactory.Rect("SettingsContent", responsiveShell);
        WDHomeUIFactory.Stretch(content);
        chrome = WDHomeUIFactory.Rect("SettingsChrome", responsiveShell);
        WDHomeUIFactory.Stretch(chrome);
        FitSafeArea();
        gameObject.SetActive(false);
    }

    public void OpenPrivacy()
    {
        setHomeCompositionVisible?.Invoke(false);
        CurrentSection = WDSettingsSection.Privacy;
        navigation.Push(new WDNavigationContext(
            "settings/privacy", state =>
            {
                if (state is WDSettingsSection restored)
                    CurrentSection = restored;
                Render();
            }, () => CurrentSection));
    }

    public void OpenGeneral()
    {
        setHomeCompositionVisible?.Invoke(false);
        CurrentSection = WDSettingsSection.General;
        navigation.Push(new WDNavigationContext(
            "settings/general", state =>
            {
                if (state is WDSettingsSection restored)
                    CurrentSection = restored;
                Render();
            }, () => CurrentSection));
    }

    public void HideFromNavigation()
    {
        gameObject.SetActive(false);
        setHomeCompositionVisible?.Invoke(true);
    }

    private void Update()
    {
        if ((!timeZonePickerOpen && !languagePickerOpen) ||
            Keyboard.current?.escapeKey.wasPressedThisFrame != true)
            return;
        timeZonePickerOpen = false;
        languagePickerOpen = false;
        Render();
    }

    // ============================================================
    // 02. HUB COMPARTILHADO DE CONFIGURAÇÕES
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        FitSafeArea();
        Clear(content);
        Clear(chrome);

        // 02.1. Header universal compartilhado com Arsenal e Chat. A fábrica
        // mantém geometria, tipografia, superfícies e contornos oficiais.
        WDUniversalHeaderContent header = WDUniversalHeaderFactory.BuildInset(
            chrome, theme, "CONFIGURAÇÕES", () => navigation.TryBack(),
            openNotifications == null ? null : () => openNotifications.Invoke());
        RefreshHeaderEconomy(header);

        // 02.2. Composição Figma 433:115 / 438:2: área X=64, Y=176,
        // menu de 280 px, gap de 32 px e painel principal flexível.
        RectTransform layout = WDHomeUIFactory.Rect("SettingsLayout", content);
        StretchBody(layout, SettingsOuterMargin, SettingsOuterMargin, 176f, 40f);
        RectTransform sidebar = WDHomeUIFactory.Rect("Sections", layout);
        sidebar.anchorMin = new Vector2(0f, 0f);
        sidebar.anchorMax = new Vector2(0f, 1f);
        sidebar.pivot = new Vector2(0f, 0.5f);
        sidebar.offsetMin = Vector2.zero;
        sidebar.offsetMax = new Vector2(280f, 0f);
        TextMeshProUGUI sidebarTitle = AddText(sidebar, "Title", "CONFIGURAÇÕES", 28f,
            theme.TextoPrincipal, Vector2.zero, Vector2.one);
        PlaceTop(sidebarTitle.rectTransform, 0f, 0f, 280f, 34f);
        sidebarTitle.fontStyle = FontStyles.Bold;
        TextMeshProUGUI sidebarSubtitle = AddText(sidebar, "Subtitle",
            "PERSONALIZE SUA EXPERIÊNCIA", 14f, theme.TextoSecundario,
            Vector2.zero, Vector2.one);
        PlaceTop(sidebarSubtitle.rectTransform, 0f, 40f, 280f, 20f);
        WDSettingsSection[] sections = (WDSettingsSection[])Enum.GetValues(typeof(WDSettingsSection));
        for (int i = 0; i < sections.Length; i++)
        {
            WDSettingsSection captured = sections[i];
            WDUIPremiumButton sectionButton = AddButton(sidebar, $"Section_{captured}",
                SectionLabel(captured),
                () => { CurrentSection = captured; timeZonePickerOpen = false; Render(); },
                Vector2.zero, Vector2.one,
                captured == CurrentSection);
            PlaceTop(sectionButton.transform as RectTransform, 0f, 76f + i * 84f, 280f, 72f);
            StyleSectionButton(sectionButton, captured == CurrentSection);
            AddSectionIcon(sectionButton.transform as RectTransform, captured,
                captured == CurrentSection);
        }

        RectTransform body = Panel("SectionBody", layout, Vector2.zero, Vector2.one);
        body.offsetMin = new Vector2(312f, 0f);
        body.offsetMax = Vector2.zero;
        switch (CurrentSection)
        {
            case WDSettingsSection.General: RenderGeneral(body); break;
            case WDSettingsSection.Audio: RenderAudio(body); break;
            case WDSettingsSection.Graphics: RenderGraphics(body); break;
            case WDSettingsSection.Language: RenderLanguage(body); break;
            case WDSettingsSection.Chat: RenderChat(body); break;
            case WDSettingsSection.Privacy: RenderPrivacy(body); break;
            default: RenderAccount(body); break;
        }
    }

    // ============================================================
    // 02.2. ECONOMIA CANÔNICA DO HEADER
    // ============================================================

    private void RefreshHeaderEconomy(WDUniversalHeaderContent header)
    {
        WDUniversalHeaderFactory.SetEconomy(header, loadWallet?.Invoke());
    }

    // ============================================================
    // 03. ABAS FUNCIONAIS E SINCRONIZAÇÃO DA UI
    // ============================================================

    private void RenderGeneral(RectTransform body)
    {
        AddHeading(body, "GERAL", "AJUSTES GERAIS DO JOGO");
        WDSettingsData data = settings.Current;
        AddBooleanRow(body, "BattleAnimations", "ANIMAÇÕES DE BATALHA",
            "Exibe as animações durante as partidas.", 0.68f, data.BattleAnimations,
            settings.SetBattleAnimations);
        AddSegmentedRow(body, "ResolutionSpeed", "VELOCIDADE PADRÃO DA RESOLUTION",
            "Define somente a velocidade visual inicial da Resolution.", 0.55f,
            new[] { "NORMAL", "ACELERADA" }, (int)data.ResolutionSpeed,
            index => settings.SetResolutionSpeed((WDResolutionPresentationSpeed)index), 0.15f);
        AddBooleanRow(body, "Vibration", "VIBRAÇÃO",
            "Preferência para dispositivos com feedback tátil.", 0.42f, data.Vibration,
            settings.SetVibration);
        bool territoryNames = MapaAtivo.NomesTerritoriaisHabilitados;
        AddBooleanRow(body, "TerritoryNames", "MOSTRAR NOMES DOS TERRITÓRIOS",
            "Exibe os nomes sobre o mapa quando o nível de zoom permitir.", 0.29f,
            territoryNames, MapaAtivo.DefinirNomesTerritoriaisHabilitados);

        TimeZoneInfo zone = WDRegionalPreferences.Current.ResolveTimeZone();
        AddDropdownRow(body, "TimeZone", "FUSO HORÁRIO",
            "Usado para apresentar datas e horários locais.", 0.16f,
            ShortTimeZoneLabel(zone), () => { timeZonePickerOpen = true; Render(); }, true, 0.32f);
        AddRestoreButton(body, settings.RestoreGeneralDefaults);
        if (timeZonePickerOpen)
            RenderTimeZonePicker(body);
    }

    private void RenderTimeZonePicker(RectTransform body)
    {
        AddOutsideDismiss(body, "TimeZoneOutsideDismiss", () =>
        { timeZonePickerOpen = false; Render(); });

        RectTransform scrollRoot = WDHomeUIFactory.Rect("TimeZoneScroll", body);
        scrollRoot.anchorMin = new Vector2(0.64f, 0.27f);
        scrollRoot.anchorMax = new Vector2(0.96f, 0.68f);
        scrollRoot.offsetMin = scrollRoot.offsetMax = Vector2.zero;
        Image scrollSurface = scrollRoot.gameObject.AddComponent<Image>();
        scrollSurface.color = new Color(0.012f, 0.023f, 0.039f, 0.99f);
        scrollSurface.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(scrollSurface, 12);
        AddThinBorder(scrollRoot, FigmaGoldBorder, 12);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 48f;

        RectTransform viewport = WDHomeUIFactory.Rect("Viewport", scrollRoot);
        WDHomeUIFactory.Stretch(viewport, 8f, 8f, 8f, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform list = WDHomeUIFactory.Rect("Options", viewport);
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = Vector2.one;
        list.pivot = new Vector2(0.5f, 1f);
        list.offsetMin = list.offsetMax = Vector2.zero;

        List<TimeZoneInfo> options = BuildTimeZoneOptions();
        const float rowHeight = 58f;
        const float rowSpacing = 8f;
        list.sizeDelta = new Vector2(0f, options.Count * (rowHeight + rowSpacing));
        int selectedIndex = -1;
        Button selectedButton = null;
        for (int i = 0; i < options.Count; i++)
        {
            TimeZoneInfo option = options[i];
            bool selected = option.Id == WDRegionalPreferences.Current.TimeZoneId;
            RectTransform optionRect = WDHomeUIFactory.Rect($"TimeZone_{i}", list);
            optionRect.anchorMin = new Vector2(0f, 1f);
            optionRect.anchorMax = Vector2.one;
            optionRect.pivot = new Vector2(0.5f, 1f);
            optionRect.anchoredPosition = new Vector2(0f, -i * (rowHeight + rowSpacing));
            optionRect.sizeDelta = new Vector2(-12f, rowHeight);
            WDUIPremiumButton optionButton = optionRect.gameObject.AddComponent<WDUIPremiumButton>();
            optionButton.Build(theme, ShortTimeZoneLabel(option), null);
            optionRect.gameObject.AddComponent<WDSettingsScrollableOption>().Build(scroll, () =>
            {
                WDRegionalPreferences.TrySetManualTimeZone(option.Id);
                timeZonePickerOpen = false;
                Render();
            });
            optionButton.SetSelected(selected);
            StyleSegmentOption(optionButton, selected);
            optionRect.gameObject.AddComponent<WDSettingsScrollSelection>()
                .Build(scroll, options.Count > 1 ? 1f - i / (float)(options.Count - 1) : 1f);
            if (selected)
            {
                selectedIndex = i;
                selectedButton = optionRect.GetComponent<Button>();
            }
        }

        scroll.viewport = viewport;
        scroll.content = list;
        if (selectedIndex >= 0 && options.Count > 1)
            scroll.verticalNormalizedPosition = 1f - selectedIndex / (float)(options.Count - 1);
        if (selectedButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
    }

    private void RenderLanguagePicker(RectTransform body)
    {
        AddOutsideDismiss(body, "LanguageOutsideDismiss", () =>
        { languagePickerOpen = false; Render(); });

        RectTransform popup = WDHomeUIFactory.Rect("LanguageDropdown", body);
        popup.anchorMin = new Vector2(0.69f, 0.20f);
        popup.anchorMax = new Vector2(0.96f, 0.60f);
        popup.offsetMin = popup.offsetMax = Vector2.zero;
        Image surface = popup.gameObject.AddComponent<Image>();
        surface.color = new Color(0.012f, 0.023f, 0.039f, 0.99f);
        surface.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(surface, 12);
        AddThinBorder(popup, FigmaGoldBorder, 12);

        WDLanguageOption[] options = WDRegionalPreferences.PlannedLanguages;
        for (int i = 0; i < options.Length; i++)
        {
            WDLanguageOption option = options[i];
            float top = 0.96f - i * 0.155f;
            string label = option.IsAvailable
                ? option.Label.ToUpperInvariant()
                : $"{option.Label.ToUpperInvariant()} · EM BREVE";
            WDUIPremiumButton button = AddButton(popup, $"Language_{option.Language}", label,
                option.IsAvailable ? (UnityAction)(() =>
                {
                    WDRegionalPreferences.TrySetLanguage(option.Language);
                    languagePickerOpen = false;
                    Render();
                }) : null,
                new Vector2(0.035f, top - 0.125f), new Vector2(0.965f, top),
                option.Language == WDRegionalPreferences.Current.Language);
            StyleSegmentOption(button,
                option.Language == WDRegionalPreferences.Current.Language);
            button.SetInteractable(option.IsAvailable);
        }
    }

    private void RenderAudio(RectTransform body)
    {
        AddHeading(body, "ÁUDIO", "AJUSTES DE SOM DO JOGO");
        WDSettingsData data = settings.Current;
        AddSlider(body, "MasterVolume", "VOLUME GERAL", data.MasterVolume, 0.62f,
            settings.SetMasterVolume);
        AddSlider(body, "MusicVolume", "MÚSICA", data.MusicVolume, 0.46f,
            settings.SetMusicVolume);
        AddSlider(body, "SfxVolume", "EFEITOS SONOROS", data.SfxVolume, 0.30f,
            settings.SetSfxVolume);
        AddRestoreButton(body, settings.RestoreAudioDefaults);
    }

    private void RenderGraphics(RectTransform body)
    {
        AddHeading(body, "GRÁFICOS", "AJUSTES DE DESEMPENHO E EXIBIÇÃO");
        WDSettingsData data = settings.Current;
        AddSegmentedRow(body, "FrameRate", "LIMITE DE FPS",
            "Define a quantidade máxima de quadros exibidos por segundo.", 0.60f,
            new[] { "30", "60", "120", "SEM LIMITE" }, (int)data.FrameRate,
            index => settings.SetFrameRate((WDFrameRateLimit)index), 0.34f, true);
        AddSegmentedRow(body, "Quality", "QUALIDADE GRÁFICA",
            "Ajusta os detalhes visuais e o desempenho do jogo.", 0.36f,
            new[] { "BAIXA", "MÉDIA", "ALTA" }, (int)data.GraphicsQuality,
            index => settings.SetGraphicsQuality((WDGraphicsQuality)index), 0.25f, true);
        AddRestoreButton(body, settings.RestoreGraphicsDefaults);
    }

    private void RenderLanguage(RectTransform body)
    {
        AddHeading(body, "IDIOMA", "IDIOMA DO JOGO");
        WDLanguageOption current = WDRegionalPreferences.PlannedLanguages.FirstOrDefault(option =>
            option.Language == WDRegionalPreferences.Current.Language && option.IsAvailable);
        if (!current.IsAvailable)
            current = WDRegionalPreferences.PlannedLanguages.First(option => option.IsAvailable);
        AddDropdownRow(body, "Language", "IDIOMA",
            "Idioma funcional atualmente disponível.", 0.61f,
            current.Label.ToUpperInvariant(), () =>
            { languagePickerOpen = true; Render(); }, true, 0.27f);
        AddRestoreButton(body, WDRegionalPreferences.RestoreLanguageDefault);
        if (languagePickerOpen)
            RenderLanguagePicker(body);
    }

    private void RenderChat(RectTransform body)
    {
        AddHeading(body, "CHAT", "PRIVACIDADE E COMUNICAÇÃO");
        WDSettingsData data = settings.Current;
        AddBooleanRow(body, "NonFriendMessages", "PERMITIR MENSAGENS DE NÃO AMIGOS",
            "Bloqueios sempre prevalecem; Clã não cria exceção.", 0.60f,
            data.AllowNonFriendMessages, settings.SetAllowNonFriendMessages);
        AddSegmentedRow(body, "OnlineStatus", "STATUS ONLINE",
            "Preferência local preparada para a futura presença online.", 0.44f,
            new[] { "ONLINE", "OCUPADO", "INVISÍVEL" }, (int)data.OnlineStatus,
            index => settings.SetOnlineStatus((WDOnlineStatus)index), 0.29f);
        AddBooleanRow(body, "ChatNotifications", "NOTIFICAÇÕES DO CHAT",
            "Preferência local; nenhuma notificação online é simulada.", 0.28f,
            data.ChatNotifications, settings.SetChatNotifications);
        AddRestoreButton(body, settings.RestoreChatDefaults);
    }

    private void RenderAccount(RectTransform body)
    {
        AddHeading(body, "CONTA", "GERENCIAMENTO DA SUA CONTA");
        AddSectionCaption(body, "Identification", "IDENTIFICAÇÃO DA CONTA", 0.745f);
        AddText(body, "AccountIdLabel", "ID DA CONTA", 14f, theme.TextoSecundario,
            new Vector2(0.04f, 0.645f), new Vector2(0.22f, 0.705f));
        AddReadOnlyField(body, "AccountId", repository.LocalProfile.PlayerId,
            new Vector2(0.22f, 0.635f), new Vector2(0.61f, 0.715f));
        WDUIPremiumButton copy = AddButton(body, "CopyAccountId", "COPIAR", () =>
            GUIUtility.systemCopyBuffer = repository.LocalProfile.PlayerId,
            new Vector2(0.63f, 0.635f), new Vector2(0.74f, 0.715f));
        StyleSecondaryButton(copy);

        AddSectionCaption(body, "LinkedAccounts", "CONTAS VINCULADAS", 0.555f);
        AddStatusRow(body, "GooglePlayGames", "GOOGLE PLAY GAMES", "EM BREVE", 0.475f);
        AddStatusRow(body, "GoogleAccount", "CONTA GOOGLE", "EM BREVE", 0.395f);

        AddSectionCaption(body, "Session", "SESSÃO", 0.315f);
        AddUnavailableActionRow(body, "Logout", "SAIR DA CONTA", string.Empty, 0.235f);

        AddSectionCaption(body, "Danger", "ZONA DE PERIGO", 0.155f, true);
        AddText(body, "DangerDescription", "A exclusão definitiva requer autenticação e confirmação segura.",
            13f, theme.TextoSecundario, new Vector2(0.04f, 0.095f), new Vector2(0.66f, 0.145f));
        WDUIPremiumButton delete = AddButton(body, "DeleteAccount", "EXCLUIR CONTA", null,
            new Vector2(0.78f, 0.090f), new Vector2(0.89f, 0.140f));
        StyleDangerButton(delete);
        delete.SetInteractable(false);
        AddText(body, "DeleteAccountStatus", "EM BREVE", 11f, theme.TextoSecundario,
            new Vector2(0.90f, 0.090f), new Vector2(0.96f, 0.140f));
    }

    // ============================================================
    // 04. PRIVACIDADE CANÔNICA POR BLOCOS
    // ============================================================

    private void RenderPrivacy(RectTransform body)
    {
        AddHeading(body, "PRIVACIDADE", "CONTROLE A VISIBILIDADE DAS SUAS INFORMAÇÕES");

        WDProfilePrivacy privacy = repository.LocalProfile.Privacy;
        AddText(body, "PrivacyIntro", "PRIVACIDADE DO PERFIL",
            17f, theme.TextoPrincipal, new Vector2(0.04f, 0.69f), new Vector2(0.72f, 0.78f));
        AddPrivacyRow(body, "Statistics", "ESTATÍSTICAS DE DESEMPENHO",
            "Partidas, vitórias, derrotas e taxa de vitória.", 0.56f,
            privacy.ShowStatistics, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithStatistics(value)));
        AddPrivacyRow(body, "ModePerformance", "DESEMPENHO POR MODO",
            "Resultados separados entre Batalha, 1V1, 2V2 e 3V3.", 0.43f,
            privacy.ShowModePerformance, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithModePerformance(value)));
        AddPrivacyRow(body, "MatchHistory", "HISTÓRICO DE PARTIDAS",
            "Registros, resultados, participantes e IDs das partidas.", 0.30f,
            privacy.ShowMatchHistory, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithMatchHistory(value)));
        AddPrivacyRow(body, "MissionsAchievements", "MISSÕES E CONQUISTAS",
            "Progresso e conclusão das missões e conquistas.", 0.17f,
            privacy.ShowMissionsAchievements, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithMissionsAchievements(value)));

        AddRestoreButton(body, () =>
        {
            repository.RestoreLocalPrivacyDefaults();
        });
    }

    private void AddPrivacyRow(RectTransform parent, string id, string title,
        string description, float bottom, bool value, Action<bool> apply)
    {
        AddText(parent, $"{id}_Label", title, 17f,
            theme.TextoPrincipal, new Vector2(0.04f, bottom + 0.050f),
            new Vector2(0.66f, bottom + 0.105f));
        AddText(parent, $"{id}_Description", description, 14f,
            theme.TextoSecundario, new Vector2(0.04f, bottom),
            new Vector2(0.70f, bottom + 0.048f));
        RectTransform control = AddSegmentedControl(parent, $"Toggle_{id}_Segmented",
            new[] { "ON", "OFF" }, value ? 0 : 1,
            index => apply(index == 0), new Vector2(0.81f, bottom + 0.018f),
            new Vector2(0.96f, bottom + 0.088f));
        int toggleIndex = value ? 1 : 0;
        Transform toggle = control != null ? control.Find($"Option_{toggleIndex}") : null;
        if (toggle != null)
            toggle.name = $"Toggle_{id}";
        AddRowSeparator(parent, $"{id}_RowDivider", bottom - 0.018f);
    }

    private void AddHeading(RectTransform body, string title, string description)
    {
        AddText(body, "Heading", title, 32f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.87f), new Vector2(0.96f, 0.96f)).fontStyle = FontStyles.Bold;
        AddText(body, "HeadingDescription", description, 15f, theme.TextoSecundario,
            new Vector2(0.04f, 0.825f), new Vector2(0.96f, 0.865f));
        AddSeparator(body, "HeadingDivider", 0.795f);
    }

    private void AddBooleanRow(RectTransform parent, string id, string title,
        string description, float bottom, bool value, Action<bool> apply)
    {
        AddSegmentedRow(parent, id, title, description, bottom, new[] { "ON", "OFF" },
            value ? 0 : 1, index => apply(index == 0), 0.15f);
    }

    private void AddSegmentedRow(RectTransform parent, string id, string title,
        string description, float bottom, string[] options, int selectedIndex,
        Action<int> apply, float controlWidth = 0.28f, bool showDescription = false)
    {
        AddText(parent, $"{id}_Label", title, 17f,
            theme.TextoPrincipal, new Vector2(0.04f, bottom + 0.050f),
            new Vector2(0.66f, bottom + 0.105f));
        AddText(parent, $"{id}_Description", description, 14f,
            theme.TextoSecundario, new Vector2(0.04f, bottom),
            new Vector2(0.70f, bottom + 0.048f));
        AddSegmentedControl(parent, $"{id}_Segmented", options, selectedIndex, apply,
            new Vector2(0.96f - controlWidth, bottom + 0.018f),
            new Vector2(0.96f, bottom + 0.088f));
        AddRowSeparator(parent, $"{id}_RowDivider", bottom - 0.018f);
    }

    private RectTransform AddSegmentedControl(RectTransform parent, string id, string[] options,
        int selectedIndex, Action<int> apply, Vector2 min, Vector2 max)
    {
        RectTransform group = WDHomeUIFactory.Rect(id, parent);
        group.anchorMin = min;
        group.anchorMax = max;
        group.offsetMin = group.offsetMax = Vector2.zero;
        const float gap = 0.010f;
        for (int i = 0; i < options.Length; i++)
        {
            int captured = i;
            float left = i / (float)options.Length + gap;
            float right = (i + 1f) / options.Length - gap;
            bool selected = i == selectedIndex;
            WDUIPremiumButton option = AddButton(group, $"Option_{i}", options[i], () =>
            {
                apply(captured);
                Render();
            }, new Vector2(left, 0.10f), new Vector2(right, 0.90f), selected);
            StyleSegmentOption(option, selected);
        }
        return group;
    }

    private void AddDropdownRow(RectTransform parent, string id, string title,
        string description, float bottom, string value, UnityAction action,
        bool interactable = true, float controlWidth = 0.30f)
    {
        AddText(parent, $"{id}_Label", title, 17f,
            theme.TextoPrincipal, new Vector2(0.04f, bottom + 0.050f),
            new Vector2(0.48f, bottom + 0.105f));
        AddText(parent, $"{id}_Description", description, 14f,
            theme.TextoSecundario, new Vector2(0.04f, bottom),
            new Vector2(0.60f, bottom + 0.048f));
        WDUIPremiumButton field = AddButton(parent, $"{id}_Field", value, action,
            new Vector2(0.96f - controlWidth, bottom + 0.012f),
            new Vector2(0.96f, bottom + 0.095f));
        StyleDropdownButton(field);
        field.SetInteractable(interactable);
        AddDropdownIndicator(field.transform as RectTransform,
            interactable ? FigmaGold : theme.TextoSecundario);
        AddRowSeparator(parent, $"{id}_RowDivider", bottom - 0.018f);
    }

    private void AddSlider(RectTransform parent, string id, string title,
        float value, float bottom, UnityAction<float> apply)
    {
        TextMeshProUGUI valueLabel = AddText(parent, $"{id}_Label", title,
            17f, theme.TextoPrincipal, new Vector2(0.04f, bottom), new Vector2(0.36f, bottom + 0.12f));
        TextMeshProUGUI percent = AddText(parent, $"{id}_Percent",
            $"{Mathf.RoundToInt(value * 100f)}%", 14f, FigmaGold,
            new Vector2(0.88f, bottom), new Vector2(0.96f, bottom + 0.12f),
            TextAlignmentOptions.Right);
        RectTransform sliderRect = WDHomeUIFactory.Rect(id, parent);
        sliderRect.anchorMin = new Vector2(0.57f, bottom + 0.02f);
        sliderRect.anchorMax = new Vector2(0.86f, bottom + 0.10f);
        sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;
        Image hitArea = sliderRect.gameObject.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0.001f);
        hitArea.raycastTarget = true;
        Image background = WDHomeUIFactory.Image("Background", sliderRect,
            new Color(1f, 1f, 1f, 0.10f));
        WDHomeUIFactory.Stretch(background.rectTransform, 0f, 24f, 0f, 24f);
        WDHomeUIFactory.ApplyRounded(background, 3);
        background.raycastTarget = false;
        RectTransform fillArea = WDHomeUIFactory.Rect("Fill Area", sliderRect);
        WDHomeUIFactory.Stretch(fillArea, 8f, 24f, 8f, 24f);
        Image fill = WDHomeUIFactory.Image("Fill", fillArea, FigmaGold);
        WDHomeUIFactory.Stretch(fill.rectTransform);
        WDHomeUIFactory.ApplyRounded(fill, 4);
        fill.raycastTarget = false;
        RectTransform handleArea = WDHomeUIFactory.Rect("Handle Slide Area", sliderRect);
        WDHomeUIFactory.Stretch(handleArea, 10f, 0f, 10f, 0f);
        Image handle = WDHomeUIFactory.Image("Handle", handleArea, theme.TextoPrincipal);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(13f, 13f);
        handle.color = new Color(0.012f, 0.020f, 0.030f, 1f);
        WDHomeUIFactory.ApplyRounded(handle, 7);
        AddThinBorder(handle.rectTransform,
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.55f), 7);
        handle.raycastTarget = false;
        WDSettingsVolumeSlider slider = sliderRect.gameObject.AddComponent<WDSettingsVolumeSlider>();
        slider.Build(fill.rectTransform, handle.rectTransform, value, updated =>
        {
            valueLabel.text = title;
            percent.text = $"{Mathf.RoundToInt(updated * 100f)}%";
            apply(updated);
        });
        AddRowSeparator(parent, $"{id}_RowDivider", bottom - 0.018f);
    }

    private void AddSectionCaption(RectTransform parent, string id, string title,
        float bottom, bool danger = false)
    {
        AddText(parent, $"{id}_Caption", title, 14f,
            danger ? new Color(0.95f, 0.29f, 0.31f, 1f) : FigmaGold,
            new Vector2(0.04f, bottom), new Vector2(0.96f, bottom + 0.055f));
        AddSeparator(parent, $"{id}_Divider", bottom - 0.012f);
    }

    private void AddReadOnlyField(RectTransform parent, string id, string value,
        Vector2 min, Vector2 max)
    {
        RectTransform field = WDHomeUIFactory.Rect(id, parent);
        field.anchorMin = min;
        field.anchorMax = max;
        field.offsetMin = field.offsetMax = Vector2.zero;
        Image surface = field.gameObject.AddComponent<Image>();
        surface.color = new Color(0.015f, 0.022f, 0.032f, 0.92f);
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, 10);
        AddThinBorder(field, FigmaGoldBorder, 10);
        TextMeshProUGUI label = AddText(field, "Value", value, 15f, theme.TextoPrincipal,
            Vector2.zero, Vector2.one, TextAlignmentOptions.Left);
        label.margin = new Vector4(16f, 0f, 16f, 0f);
    }

    private void AddStatusRow(RectTransform parent, string id, string label,
        string status, float bottom)
    {
        AddText(parent, $"{id}_Label", label, 15f, theme.TextoPrincipal,
            new Vector2(0.04f, bottom), new Vector2(0.70f, bottom + 0.06f));
        AddText(parent, $"{id}_Status", status, 13f, theme.TextoSecundario,
            new Vector2(0.76f, bottom), new Vector2(0.96f, bottom + 0.06f),
            TextAlignmentOptions.Right);
    }

    private void AddUnavailableActionRow(RectTransform parent, string id, string label,
        string helper, float bottom)
    {
        WDUIPremiumButton button = AddButton(parent, $"{id}_Action", label, null,
            new Vector2(0.04f, bottom - 0.005f), new Vector2(0.15f, bottom + 0.065f));
        StyleSecondaryButton(button);
        button.SetInteractable(false);
        AddText(parent, $"{id}_Status", "EM BREVE", 12f, theme.TextoSecundario,
            new Vector2(0.17f, bottom), new Vector2(0.29f, bottom + 0.06f));
    }

    private static void AddOutsideDismiss(RectTransform parent, string name, UnityAction dismiss)
    {
        Image blocker = WDHomeUIFactory.Image(name, parent,
            new Color(0f, 0f, 0f, 0.01f), true);
        WDHomeUIFactory.Stretch(blocker.rectTransform);
        blocker.transform.SetAsLastSibling();
        Button button = blocker.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(dismiss);
    }

    private void AddRestoreButton(RectTransform body, Action restore)
    {
        WDUIPremiumButton restoreButton = AddButton(body, "RestoreDefaults", "RESTAURAR PADRÕES", () =>
        { restore(); Render(); }, new Vector2(0.04f, 0.035f), new Vector2(0.205f, 0.092f));
        restoreButton.SetPersistentColors(FigmaNavSurface,
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.18f));
        restoreButton.SetLabelColor(FigmaGold);
        RoundButton(restoreButton, 15, FigmaGoldBorder);
    }

    private static List<TimeZoneInfo> BuildTimeZoneOptions()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(zone => zone.BaseUtcOffset)
            .ThenBy(zone => zone.DisplayName, StringComparer.CurrentCulture)
            .ToList();
    }

    private static string ShortTimeZoneLabel(TimeZoneInfo zone) =>
        zone == null ? "UTC" : $"UTC{zone.BaseUtcOffset:hh\\:mm}  {zone.StandardName}";

    // ============================================================
    // 05. CONSTRUÇÃO VISUAL REUTILIZÁVEL
    // ============================================================

    // 05.1. O shell usa a Safe Area real e limita o conteúdo à referência
    // canônica. Em ultrawide, o ganho fica nas margens externas; em recortes
    // físicos, Header e corpo encolhem a partir da mesma origem.
    private void FitSafeArea()
    {
        if (responsiveShell == null) return;
        responsiveFrame ??= WDHomeSecondaryScreenFrame.Configure(responsiveShell);
        responsiveFrame.RefreshNow();
    }

    private static void StretchBody(RectTransform rect, float left, float right,
        float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void PlaceTop(RectTransform rect, float x, float y,
        float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUniversalPanelFactory.Apply(rect, theme);
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.20f);
        shadow.effectDistance = new Vector2(0f, -12f);
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value,
        float size, Color color, Vector2 min, Vector2 max,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(
            name, parent, theme, value, size, color, alignment);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    private WDUIPremiumButton AddButton(Transform parent, string name, string label,
        UnityAction action, Vector2 min, Vector2 max, bool primary = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        button.SetSelected(primary);
        return button;
    }

    private void StyleBackButton(WDUIPremiumButton button)
    {
        if (button == null) return;
        button.SetPersistentColors(new Color(0.106f, 0.114f, 0.133f, 0.70f),
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.16f));
        button.SetLabelColor(FigmaGold);
        RoundButton(button, 22, FigmaGoldBorder);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = 16f;
            label.characterSpacing = 1.5f;
            label.fontStyle = FontStyles.Bold;
        }
    }

    private void StyleSectionButton(WDUIPremiumButton button, bool selected)
    {
        if (button == null) return;
        button.SetPersistentColors(selected ? FigmaGold : FigmaNavSurface,
            selected ? new Color(0.84f, 0.68f, 0.46f, 1f)
                : new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.16f));
        button.SetLabelColor(selected ? FigmaInk : theme.TextoPrincipal);
        RoundButton(button, 18, selected
            ? new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.38f)
            : new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.12f));
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = 18f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(70f, 0f, 10f, 0f);
        }
    }

    private static void AddSectionIcon(RectTransform button, WDSettingsSection section,
        bool selected)
    {
        if (button == null) return;
        RectTransform frame = WDHomeUIFactory.Rect("IconFrame", button);
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.anchoredPosition = new Vector2(16f, 0f);
        frame.sizeDelta = new Vector2(40f, 40f);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = selected ? FigmaInk
            : new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.11f);
        frameImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(frameImage, 12);

        string path = SectionIconPath(section);
        Texture2D texture = string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
        if (texture == null) return;
        RectTransform iconRect = WDHomeUIFactory.Rect("Icon", frame);
        RawImage icon = iconRect.gameObject.AddComponent<RawImage>();
        icon.texture = texture;
        icon.color = FigmaGold;
        icon.raycastTarget = false;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        iconRect.anchoredPosition = Vector2.zero;
    }

    private static string SectionIconPath(WDSettingsSection section) => section switch
    {
        WDSettingsSection.General => "UI/Common/Icons/Settings/icon_settings_general",
        WDSettingsSection.Audio => "UI/Common/Icons/Settings/icon_settings_audio",
        WDSettingsSection.Graphics => "UI/Common/Icons/Settings/icon_settings_graphics",
        WDSettingsSection.Language => "UI/Common/Icons/Settings/icon_settings_language",
        WDSettingsSection.Chat => "UI/Common/Icons/Settings/icon_settings_chat",
        WDSettingsSection.Privacy => "UI/Common/Icons/Settings/icon_settings_privacy",
        WDSettingsSection.Account => "UI/Common/Icons/Settings/icon_settings_account",
        _ => null
    };

    private void StyleSegmentOption(WDUIPremiumButton button, bool selected)
    {
        if (button == null) return;
        button.SetPersistentColors(selected ? FigmaGold : FigmaNavSurface,
            selected ? new Color(0.84f, 0.68f, 0.46f, 1f)
                : new Color(0.032f, 0.052f, 0.078f, 0.98f));
        button.SetLabelColor(selected ? FigmaInk : theme.TextoSecundario);
        RoundButton(button, 9, new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.10f));
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = 13f;
            label.fontStyle = FontStyles.Bold;
        }
    }

    private void StyleDropdownButton(WDUIPremiumButton button)
    {
        if (button == null) return;
        button.SetPersistentColors(new Color(0.015f, 0.022f, 0.032f, 0.92f),
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.12f));
        button.SetLabelColor(theme.TextoPrincipal);
        RoundButton(button, 10, FigmaGoldBorder);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(16f, 0f, 12f, 0f);
        }
    }

    private void StyleSecondaryButton(WDUIPremiumButton button)
    {
        if (button == null) return;
        button.SetPersistentColors(FigmaNavSurface,
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.14f));
        button.SetDisabledColor(new Color(0.07f, 0.08f, 0.10f, 0.88f));
        button.SetLabelColor(FigmaGold);
        RoundButton(button, 12, FigmaGoldBorder);
    }

    private void StyleDangerButton(WDUIPremiumButton button)
    {
        if (button == null) return;
        button.SetPersistentColors(new Color(0.42f, 0.07f, 0.09f, 0.72f),
            new Color(0.55f, 0.08f, 0.10f, 0.82f));
        button.SetDisabledColor(new Color(0.25f, 0.055f, 0.065f, 0.76f));
        button.SetLabelColor(new Color(1f, 0.70f, 0.70f, 1f));
        RoundButton(button, 12, new Color(0.80f, 0.16f, 0.18f, 0.48f));
    }

    private static void RoundButton(WDUIPremiumButton button, int radius, Color borderColor)
    {
        if (button == null) return;
        Image background = button.GetComponent<Image>();
        if (background != null) WDHomeUIFactory.ApplyRounded(background, radius);
        Image border = button.transform.Find("Border")?.GetComponent<Image>();
        if (border == null) return;

        border.enabled = false;
        AddThinBorder(button.transform as RectTransform, borderColor, radius);
    }

    private static void AddThinBorder(RectTransform target, Color color, int radius)
    {
        if (target == null) return;
        WDHomeUIFactory.AddThinRoundedBorder(target, color, radius);
    }

    private static void AddRowSeparator(Transform parent, string name, float normalizedY)
    {
        Image separator = WDHomeUIFactory.Image(name, parent,
            new Color(0.34f, 0.43f, 0.55f, 0.14f));
        RectTransform rect = separator.rectTransform;
        rect.anchorMin = new Vector2(0.04f, normalizedY);
        rect.anchorMax = new Vector2(0.96f, normalizedY);
        rect.offsetMin = new Vector2(0f, -0.35f);
        rect.offsetMax = new Vector2(0f, 0.35f);
        separator.raycastTarget = false;
    }

    private static void AddDropdownIndicator(RectTransform field, Color color)
    {
        if (field == null) return;
        RectTransform indicator = WDHomeUIFactory.Rect("DropdownIndicator", field);
        indicator.anchorMin = indicator.anchorMax = new Vector2(1f, 0.5f);
        indicator.pivot = new Vector2(1f, 0.5f);
        indicator.anchoredPosition = new Vector2(-14f, 0f);
        indicator.sizeDelta = new Vector2(14f, 10f);
        for (int i = 0; i < 2; i++)
        {
            Image stroke = WDHomeUIFactory.Image($"Stroke_{i}", indicator, color);
            RectTransform rect = stroke.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(8f, 1.5f);
            rect.anchoredPosition = new Vector2(i == 0 ? -2.7f : 2.7f, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -35f : 35f);
            stroke.raycastTarget = false;
        }
    }

    private static void AddSeparator(Transform parent, string name, float normalizedY)
    {
        Image separator = WDHomeUIFactory.Image(name, parent, FigmaDivider);
        RectTransform rect = separator.rectTransform;
        rect.anchorMin = new Vector2(0.04f, normalizedY);
        rect.anchorMax = new Vector2(0.92f, normalizedY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(0f, -0.5f);
        rect.offsetMax = new Vector2(0f, 0.5f);
        separator.raycastTarget = false;
    }

    private static string SectionLabel(WDSettingsSection section) => section switch
    {
        WDSettingsSection.General => "GERAL",
        WDSettingsSection.Audio => "ÁUDIO",
        WDSettingsSection.Graphics => "GRÁFICOS",
        WDSettingsSection.Language => "IDIOMA",
        WDSettingsSection.Chat => "CHAT",
        WDSettingsSection.Privacy => "PRIVACIDADE",
        _ => "CONTA"
    };

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyNow(parent.GetChild(i).gameObject);
    }

    private static void DestroyNow(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}

[DisallowMultipleComponent]
public sealed class WDSettingsVolumeSlider : MonoBehaviour,
    IPointerDownHandler, IDragHandler
{
    // ============================================================
    // 06. INTERAÇÃO TOUCH DIRETA PARA VOLUME
    // ============================================================

    private RectTransform track;
    private RectTransform fill;
    private RectTransform handle;
    private UnityAction<float> changed;

    public float Value { get; private set; }

    public void Build(RectTransform fillRect, RectTransform handleRect,
        float initialValue, UnityAction<float> onChanged)
    {
        track = transform as RectTransform;
        fill = fillRect;
        handle = handleRect;
        changed = onChanged;
        SetValue(initialValue, false);
    }

    public void OnPointerDown(PointerEventData eventData) => UpdateFromPointer(eventData);
    public void OnDrag(PointerEventData eventData) => UpdateFromPointer(eventData);

    private void UpdateFromPointer(PointerEventData eventData)
    {
        if (track == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                track, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        float normalized = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
        SetValue(normalized, true);
    }

    private void SetValue(float value, bool notify)
    {
        Value = Mathf.Clamp01(value);
        if (fill != null)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Value, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }
        if (handle != null)
            handle.anchorMin = handle.anchorMax = new Vector2(Value, 0.5f);
        if (notify)
            changed?.Invoke(Value);
    }
}

[DisallowMultipleComponent]
public sealed class WDSettingsScrollSelection : MonoBehaviour, ISelectHandler
{
    private ScrollRect scroll;
    private float normalizedPosition;

    public void Build(ScrollRect targetScroll, float targetNormalizedPosition)
    {
        scroll = targetScroll;
        normalizedPosition = Mathf.Clamp01(targetNormalizedPosition);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (scroll != null)
            scroll.verticalNormalizedPosition = normalizedPosition;
    }
}

[DisallowMultipleComponent]
public sealed class WDSettingsScrollableOption : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect scroll;
    private UnityAction selected;
    private Vector2 pointerDownPosition;
    private float maximumSqrDistance;
    private int pointerId = int.MinValue;

    public void Build(ScrollRect owner, UnityAction onSelected)
    {
        scroll = owner;
        selected = onSelected;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerId = eventData.pointerId;
        pointerDownPosition = eventData.position;
        maximumSqrDistance = 0f;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData) =>
        scroll?.OnInitializePotentialDrag(eventData);

    public void OnBeginDrag(PointerEventData eventData)
    {
        TrackDistance(eventData);
        scroll?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        TrackDistance(eventData);
        scroll?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData) => scroll?.OnEndDrag(eventData);

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId)
            return;
        TrackDistance(eventData);
        float systemThreshold = EventSystem.current != null
            ? EventSystem.current.pixelDragThreshold : 10f;
        float tapTolerance = Mathf.Max(10f, systemThreshold * 1.25f);
        pointerId = int.MinValue;
        if (maximumSqrDistance <= tapTolerance * tapTolerance)
            selected?.Invoke();
    }

    private void TrackDistance(PointerEventData eventData)
    {
        float distance = (eventData.position - pointerDownPosition).sqrMagnitude;
        if (distance > maximumSqrDistance)
            maximumSqrDistance = distance;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
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
    private RectTransform content;
    private bool timeZonePickerOpen;

    public bool IsOpen => gameObject.activeSelf;
    public WDSettingsSection CurrentSection { get; private set; } = WDSettingsSection.Privacy;
    public WDProfilePrivacy CurrentPrivacy => repository.LocalProfile.Privacy;

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDNavigationStack navigationStack)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        repository = newRepository ?? throw new ArgumentNullException(nameof(newRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        settings = new WDSettingsRepository();

        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.96f);
        blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("SettingsContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 44f, 54f, 44f);
        gameObject.SetActive(false);
    }

    public void OpenPrivacy()
    {
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
    }

    // ============================================================
    // 02. HUB COMPARTILHADO DE CONFIGURAÇÕES
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "<  VOLTAR", () => navigation.TryBack(),
            new Vector2(0.01f, 0.90f), new Vector2(0.13f, 0.98f));
        AddText(content, "Title", "CONFIGURAÇÕES", 30f, theme.TextoPrincipal,
            new Vector2(0.02f, 0.80f), new Vector2(0.25f, 0.89f)).fontStyle = FontStyles.Bold;

        RectTransform sidebar = Panel("Sections", content,
            new Vector2(0.02f, 0.08f), new Vector2(0.24f, 0.79f));
        WDSettingsSection[] sections = (WDSettingsSection[])Enum.GetValues(typeof(WDSettingsSection));
        for (int i = 0; i < sections.Length; i++)
        {
            WDSettingsSection captured = sections[i];
            float top = 0.96f - i * 0.13f;
            AddButton(sidebar, $"Section_{captured}", SectionLabel(captured),
                () => { CurrentSection = captured; timeZonePickerOpen = false; Render(); },
                new Vector2(0.06f, top - 0.10f), new Vector2(0.94f, top),
                captured == CurrentSection);
        }

        RectTransform body = Panel("SectionBody", content,
            new Vector2(0.26f, 0.08f), new Vector2(0.98f, 0.89f));
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
    // 03. ABAS FUNCIONAIS E SINCRONIZAÇÃO DA UI
    // ============================================================

    private void RenderGeneral(RectTransform body)
    {
        AddHeading(body, "GERAL", "AJUSTES GERAIS DO JOGO");
        if (timeZonePickerOpen)
        {
            RenderTimeZonePicker(body);
            return;
        }

        WDSettingsData data = settings.Current;
        AddChoiceRow(body, "BattleAnimations", "ANIMAÇÕES DE BATALHA",
            "Exibe as animações durante as partidas.", 0.68f,
            data.BattleAnimations ? "ON" : "OFF", () =>
            { settings.SetBattleAnimations(!data.BattleAnimations); Render(); }, data.BattleAnimations);
        AddChoiceRow(body, "ResolutionSpeed", "VELOCIDADE PADRÃO DA RESOLUTION",
            "Define somente a velocidade visual inicial da Resolution.", 0.53f,
            data.ResolutionSpeed == WDResolutionPresentationSpeed.Normal ? "NORMAL" : "ACELERADA", () =>
            {
                settings.SetResolutionSpeed(data.ResolutionSpeed == WDResolutionPresentationSpeed.Normal
                    ? WDResolutionPresentationSpeed.Accelerated : WDResolutionPresentationSpeed.Normal);
                Render();
            });
        AddChoiceRow(body, "Vibration", "VIBRAÇÃO",
            "Preferência para dispositivos com feedback tátil.", 0.38f,
            data.Vibration ? "ON" : "OFF", () =>
            { settings.SetVibration(!data.Vibration); Render(); }, data.Vibration);
        bool territoryNames = MapaAtivo.NomesTerritoriaisHabilitados;
        AddChoiceRow(body, "TerritoryNames", "MOSTRAR NOMES DOS TERRITÓRIOS",
            "Exibe os nomes sobre o mapa quando o nível de zoom permitir.", 0.23f,
            territoryNames ? "ON" : "OFF", () =>
            { MapaAtivo.DefinirNomesTerritoriaisHabilitados(!territoryNames); Render(); }, territoryNames);

        TimeZoneInfo zone = WDRegionalPreferences.Current.ResolveTimeZone();
        AddChoiceRow(body, "TimeZone", "FUSO HORÁRIO",
            "Usado para apresentar datas e horários locais.", 0.08f,
            ShortTimeZoneLabel(zone), () => { timeZonePickerOpen = true; Render(); });
        AddRestoreButton(body, settings.RestoreGeneralDefaults);
    }

    private void RenderTimeZonePicker(RectTransform body)
    {
        AddText(body, "TimeZonePrompt", "SELECIONE O FUSO HORÁRIO",
            18f, theme.TextoPrincipal, new Vector2(0.04f, 0.72f), new Vector2(0.70f, 0.80f));
        AddButton(body, "CloseTimeZonePicker", "CONCLUIR",
            () => { timeZonePickerOpen = false; Render(); },
            new Vector2(0.74f, 0.72f), new Vector2(0.96f, 0.80f));

        RectTransform scrollRoot = WDHomeUIFactory.Rect("TimeZoneScroll", body);
        scrollRoot.anchorMin = new Vector2(0.08f, 0.08f);
        scrollRoot.anchorMax = new Vector2(0.92f, 0.68f);
        scrollRoot.offsetMin = scrollRoot.offsetMax = Vector2.zero;
        Image scrollSurface = scrollRoot.gameObject.AddComponent<Image>();
        scrollSurface.color = theme.BackgroundPrimary;
        scrollSurface.raycastTarget = true;
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
            optionButton.Build(theme, ShortTimeZoneLabel(option), () =>
            {
                WDRegionalPreferences.TrySetManualTimeZone(option.Id);
                timeZonePickerOpen = false;
                Render();
            });
            optionButton.SetSelected(selected);
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

    private void RenderAudio(RectTransform body)
    {
        AddHeading(body, "ÁUDIO", "AJUSTES DE SOM DO JOGO");
        WDSettingsData data = settings.Current;
        AddSlider(body, "MasterVolume", "VOLUME GERAL", data.MasterVolume, 0.64f,
            settings.SetMasterVolume);
        AddSlider(body, "MusicVolume", "MÚSICA", data.MusicVolume, 0.44f,
            settings.SetMusicVolume);
        AddSlider(body, "SfxVolume", "EFEITOS SONOROS", data.SfxVolume, 0.24f,
            settings.SetSfxVolume);
        AddRestoreButton(body, settings.RestoreAudioDefaults);
    }

    private void RenderGraphics(RectTransform body)
    {
        AddHeading(body, "GRÁFICOS", "AJUSTES DE DESEMPENHO E EXIBIÇÃO");
        WDSettingsData data = settings.Current;
        AddChoiceRow(body, "FrameRate", "LIMITE DE FPS",
            "VSync é desativado para que o limite selecionado seja efetivo.", 0.62f,
            FrameRateLabel(data.FrameRate), () =>
            { settings.SetFrameRate(Next(data.FrameRate)); Render(); });
        AddChoiceRow(body, "Quality", "QUALIDADE GRÁFICA",
            "Mapeada com segurança para os níveis reais do projeto.", 0.42f,
            QualityLabel(data.GraphicsQuality), () =>
            { settings.SetGraphicsQuality(Next(data.GraphicsQuality)); Render(); });
        AddChoiceRow(body, "Fullscreen", "TELA CHEIA",
            "Aplicada apenas em build, sem interferir no Editor.", 0.22f,
            data.Fullscreen ? "ON" : "OFF", () =>
            { settings.SetFullscreen(!data.Fullscreen); Render(); }, data.Fullscreen);
        AddRestoreButton(body, settings.RestoreGraphicsDefaults);
    }

    private void RenderLanguage(RectTransform body)
    {
        AddHeading(body, "IDIOMA", "IDIOMA DO JOGO");
        WDLanguageOption[] options = WDRegionalPreferences.PlannedLanguages;
        for (int i = 0; i < options.Length; i++)
        {
            WDLanguageOption option = options[i];
            float top = 0.72f - i * 0.10f;
            string status = option.IsAvailable ? "SELECIONADO" : "EM BREVE";
            AddChoiceRow(body, $"Language_{option.Language}", option.Label.ToUpperInvariant(),
                option.IsAvailable ? "Idioma funcional atual." : "Localização ainda não disponível.",
                top, status, () =>
                {
                    if (option.IsAvailable)
                        WDRegionalPreferences.TrySetLanguage(option.Language);
                    Render();
                }, option.IsAvailable && WDRegionalPreferences.Current.Language == option.Language);
        }
        AddRestoreButton(body, WDRegionalPreferences.RestoreLanguageDefault);
    }

    private void RenderChat(RectTransform body)
    {
        AddHeading(body, "CHAT", "PRIVACIDADE E COMUNICAÇÃO");
        WDSettingsData data = settings.Current;
        AddChoiceRow(body, "NonFriendMessages", "PERMITIR MENSAGENS DE NÃO AMIGOS",
            "Bloqueios sempre prevalecem; Clã não cria exceção.", 0.60f,
            data.AllowNonFriendMessages ? "ON" : "OFF", () =>
            { settings.SetAllowNonFriendMessages(!data.AllowNonFriendMessages); Render(); },
            data.AllowNonFriendMessages);
        AddChoiceRow(body, "OnlineStatus", "STATUS ONLINE",
            "Preferência local preparada para a futura presença online.", 0.40f,
            StatusLabel(data.OnlineStatus), () =>
            { settings.SetOnlineStatus(Next(data.OnlineStatus)); Render(); });
        AddChoiceRow(body, "ChatNotifications", "NOTIFICAÇÕES DO CHAT",
            "Preferência local; nenhuma notificação online é simulada.", 0.20f,
            data.ChatNotifications ? "ON" : "OFF", () =>
            { settings.SetChatNotifications(!data.ChatNotifications); Render(); },
            data.ChatNotifications);
        AddRestoreButton(body, settings.RestoreChatDefaults);
    }

    private void RenderAccount(RectTransform body)
    {
        AddHeading(body, "CONTA", "GERENCIAMENTO DA SUA CONTA");
        AddText(body, "AccountId", $"IDENTIFICAÇÃO DA CONTA\n<size=80%>{repository.LocalProfile.PlayerId}</size>",
            18f, theme.TextoPrincipal, new Vector2(0.04f, 0.65f), new Vector2(0.70f, 0.78f));
        AddButton(body, "CopyAccountId", "COPIAR", () =>
            GUIUtility.systemCopyBuffer = repository.LocalProfile.PlayerId,
            new Vector2(0.76f, 0.67f), new Vector2(0.96f, 0.77f));
        AddText(body, "LinkedAccounts", "CONTAS VINCULADAS\n<size=80%>EM BREVE — depende de autenticação real.</size>",
            18f, theme.TextoSecundario, new Vector2(0.04f, 0.45f), new Vector2(0.96f, 0.58f));
        AddText(body, "Session", "SESSÃO / SAIR DA CONTA\n<size=80%>INDISPONÍVEL NESTA ETAPA — nenhuma sessão falsa será criada.</size>",
            18f, theme.TextoSecundario, new Vector2(0.04f, 0.27f), new Vector2(0.96f, 0.40f));
        AddText(body, "DangerZone", "ZONA DE PERIGO / EXCLUIR CONTA\n<size=80%>INDISPONÍVEL — requer backend e confirmação forte. Nenhum dado local será apagado.</size>",
            18f, theme.TextoSecundario, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.21f));
    }

    // ============================================================
    // 04. PRIVACIDADE CANÔNICA POR BLOCOS
    // ============================================================

    private void RenderPrivacy(RectTransform body)
    {
        AddText(body, "Heading", "PRIVACIDADE", 26f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.87f), new Vector2(0.96f, 0.96f)).fontStyle = FontStyles.Bold;
        AddText(body, "Description", "CONTROLE A VISIBILIDADE DAS SUAS INFORMAÇÕES",
            13f, theme.TextoSecundario, new Vector2(0.04f, 0.81f), new Vector2(0.96f, 0.87f));

        WDProfilePrivacy privacy = repository.LocalProfile.Privacy;
        AddPrivacyRow(body, "Statistics", "ESTATÍSTICAS DE DESEMPENHO",
            "Partidas, vitórias, derrotas e taxa de vitória.", 0.68f,
            privacy.ShowStatistics, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithStatistics(value)));
        AddPrivacyRow(body, "ModePerformance", "DESEMPENHO POR MODO",
            "Resultados separados entre Batalha, 1V1, 2V2 e 3V3.", 0.53f,
            privacy.ShowModePerformance, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithModePerformance(value)));
        AddPrivacyRow(body, "MatchHistory", "HISTÓRICO DE PARTIDAS",
            "Registros, resultados, participantes e IDs das partidas.", 0.38f,
            privacy.ShowMatchHistory, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithMatchHistory(value)));
        AddPrivacyRow(body, "MissionsAchievements", "MISSÕES E CONQUISTAS",
            "Progresso e conclusão das missões e conquistas.", 0.23f,
            privacy.ShowMissionsAchievements, value =>
                repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithMissionsAchievements(value)));

        AddText(body, "AlwaysVisible", "SEMPRE VISÍVEL\nNome, avatar, moldura, ID público, Clã, Divisão e emblema da Divisão.",
            14f, theme.TextoSecundario, new Vector2(0.04f, 0.08f), new Vector2(0.72f, 0.19f));
        AddButton(body, "RestoreDefaults", "RESTAURAR PADRÕES", () =>
        {
            repository.RestoreLocalPrivacyDefaults();
            Render();
        }, new Vector2(0.74f, 0.08f), new Vector2(0.96f, 0.17f));
    }

    private void AddPrivacyRow(RectTransform parent, string id, string title,
        string description, float bottom, bool value, Action<bool> apply)
    {
        AddText(parent, $"{id}_Label", $"{title}\n<size=75%>{description}</size>", 17f,
            theme.TextoPrincipal, new Vector2(0.04f, bottom), new Vector2(0.78f, bottom + 0.12f));
        AddButton(parent, $"Toggle_{id}", value ? "ON" : "OFF", () =>
        {
            apply(!value);
            Render();
        }, new Vector2(0.84f, bottom + 0.02f), new Vector2(0.96f, bottom + 0.10f), value);
    }

    private void AddHeading(RectTransform body, string title, string description)
    {
        AddText(body, "Heading", title, 26f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.87f), new Vector2(0.96f, 0.96f)).fontStyle = FontStyles.Bold;
        AddText(body, "Description", description, 13f, theme.TextoSecundario,
            new Vector2(0.04f, 0.81f), new Vector2(0.96f, 0.87f));
    }

    private void AddChoiceRow(RectTransform parent, string id, string title,
        string description, float bottom, string value, UnityAction action, bool selected = false)
    {
        AddText(parent, $"{id}_Label", $"{title}\n<size=75%>{description}</size>", 17f,
            theme.TextoPrincipal, new Vector2(0.04f, bottom), new Vector2(0.72f, bottom + 0.12f));
        AddButton(parent, $"{id}_Value", value, action,
            new Vector2(0.74f, bottom + 0.01f), new Vector2(0.96f, bottom + 0.11f), selected);
    }

    private void AddSlider(RectTransform parent, string id, string title,
        float value, float bottom, UnityAction<float> apply)
    {
        TextMeshProUGUI valueLabel = AddText(parent, $"{id}_Label",
            $"{title}\n<size=75%>{Mathf.RoundToInt(value * 100f)}%</size>",
            17f, theme.TextoPrincipal, new Vector2(0.04f, bottom), new Vector2(0.38f, bottom + 0.14f));
        RectTransform sliderRect = WDHomeUIFactory.Rect(id, parent);
        sliderRect.anchorMin = new Vector2(0.40f, bottom + 0.02f);
        sliderRect.anchorMax = new Vector2(0.96f, bottom + 0.12f);
        sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;
        Image hitArea = sliderRect.gameObject.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0.001f);
        hitArea.raycastTarget = true;
        Image background = WDHomeUIFactory.Image("Background", sliderRect, theme.BackgroundPrimary);
        WDHomeUIFactory.Stretch(background.rectTransform, 0f, 20f, 0f, 20f);
        background.raycastTarget = false;
        RectTransform fillArea = WDHomeUIFactory.Rect("Fill Area", sliderRect);
        WDHomeUIFactory.Stretch(fillArea, 10f, 20f, 10f, 20f);
        Image fill = WDHomeUIFactory.Image("Fill", fillArea, theme.Acento);
        WDHomeUIFactory.Stretch(fill.rectTransform);
        fill.raycastTarget = false;
        RectTransform handleArea = WDHomeUIFactory.Rect("Handle Slide Area", sliderRect);
        WDHomeUIFactory.Stretch(handleArea, 10f, 0f, 10f, 0f);
        Image handle = WDHomeUIFactory.Image("Handle", handleArea, theme.TextoPrincipal);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(34f, 34f);
        handle.raycastTarget = false;
        WDSettingsVolumeSlider slider = sliderRect.gameObject.AddComponent<WDSettingsVolumeSlider>();
        slider.Build(fill.rectTransform, handle.rectTransform, value, updated =>
        {
            valueLabel.text = $"{title}\n<size=75%>{Mathf.RoundToInt(updated * 100f)}%</size>";
            apply(updated);
        });
    }

    private void AddRestoreButton(RectTransform body, Action restore)
    {
        AddButton(body, "RestoreDefaults", "RESTAURAR PADRÕES", () =>
        { restore(); Render(); }, new Vector2(0.74f, 0.015f), new Vector2(0.96f, 0.075f));
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
    private static string FrameRateLabel(WDFrameRateLimit value) => value == WDFrameRateLimit.Unlimited
        ? "SEM LIMITE" : WDSettingsRepository.ResolveTargetFrameRate(value).ToString();
    private static string QualityLabel(WDGraphicsQuality value) => value switch
    { WDGraphicsQuality.Low => "BAIXA", WDGraphicsQuality.Medium => "MÉDIA", _ => "ALTA" };
    private static string StatusLabel(WDOnlineStatus value) => value switch
    { WDOnlineStatus.Busy => "OCUPADO", WDOnlineStatus.Invisible => "INVISÍVEL", _ => "ONLINE" };
    private static T Next<T>(T value) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int index = Array.IndexOf(values, value);
        return values[(index + 1) % values.Length];
    }

    // ============================================================
    // 05. CONSTRUÇÃO VISUAL REUTILIZÁVEL
    // ============================================================

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = theme.BackgroundElevated;
        image.raycastTarget = true;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.BorderNeutral;
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

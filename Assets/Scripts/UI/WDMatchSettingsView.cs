using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WDMatchSettingsView : MonoBehaviour
{
    // 01. Dependências: utility é a autoridade principal; confirmação é apenas estado filho.
    private static readonly Color Gold = new(197f / 255, 155f / 255, 99f / 255);
    private static readonly Color Dark = new(11f / 255, 14f / 255, 18f / 255);
    private static readonly Color Muted = new(142f / 255, 146f / 255, 154f / 255);
    private IWDMatchUtilityCommands utilities;
    private WDSettingsRepository repository;
    private Action leave;
    private RectTransform backdrop, panel, confirmation;
    private RectTransform[] targets;
    private WDMatchChatBackdropFilter backdropFilter;
    private Slider master, music, sfx;
    private Button confirmLeave, requestLeave, masterMute;
    private Image masterMuteCircle, masterMuteIcon;
    private bool confirming, leaving;
    private readonly List<Sprite> sprites = new();

    // 02. Construção canônica 139:65 / 139:130 no Canvas existente.
    public void Build(RectTransform[] utilityTargets)
    {
        targets = utilityTargets;
        backdrop = Surface("Backdrop", transform, 0, 0, 1920, 1080, new Color(0, 0, 0, .55f));
        var close = backdrop.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.targetGraphic = backdrop.GetComponent<Image>();
        close.onClick.AddListener(BackdropClicked);
        backdropFilter = backdrop.gameObject.AddComponent<WDMatchChatBackdropFilter>();
        // 02.1. Sem backdrop blur do Figma, fill opaco evita territórios legíveis no corpo.
        // O backdrop externo continua independente, preto com alpha .55.
        panel = Surface("SettingsPanel", transform, 0, 0, 720, 693.2f, Dark, 16, Gold);
        ShieldPanel(panel);
        Label(panel, "Title", "CONFIGURAÇÕES DA PARTIDA", 22.4f, 22.4f, 675.2f, 33, 27.2f, Gold);
        Label(panel, "Subtitle", "AJUSTES DA BATALHA ATUAL", 22.4f, 61.8f, 675.2f, 13, 11.2f, Muted, false);
        var audio = Surface("Audio", panel, 22.4f, 94, 675.2f, 271.4f, Dark, 13, Alpha(Gold, .25f));
        Label(audio, "Title", "ÁUDIO", 14.4f, 14.4f, 646.4f, 17, 14.4f, Gold);
        master = AudioRow(audio, "Master", "VOLUME GERAL", 42.6f, value => repository?.SetMasterVolume(value),
            () => { if (repository != null) repository.SetMasterMuted(!repository.Current.MasterMuted); });
        music = AudioRow(audio, "Music", "MÚSICA", 121.8f, null);
        sfx = AudioRow(audio, "Sfx", "EFEITOS SONOROS", 201, null);
        Surface("Divider1", audio, 14.4f, 109.8f, 646.4f, .8f, Alpha(Gold, .2f));
        Surface("Divider2", audio, 14.4f, 189, 646.4f, .8f, Alpha(Gold, .2f));
        var ui = Surface("Interface", panel, 22.4f, 384.6f, 675.2f, 222.2f, Dark, 13, Alpha(Gold, .25f));
        Label(ui, "Title", "INTERFACE", 14.4f, 14.4f, 646.4f, 17, 14.4f, Gold);
        Label(ui, "EmojisLabel", "EXIBIR EMOJIS", 14.4f, 42.6f, 646.4f, 15, 12.8f, Color.white);
        var emojis = Surface("Emojis", ui, 14.4f, 65.6f, 646.4f, 48, Dark, 11, Gold);
        MakeButton("Off", emojis, "OFF", 6.4f, 6.4f, 35.2f, 35.2f, 11.2f, Dark, Muted, null, 18, Alpha(Gold, .2f));
        MakeButton("On", emojis, "ON", 51.2f, 6.4f, 35.2f, 35.2f, 11.2f, Dark, Muted, null, 18, Alpha(Gold, .2f));
        DimUnavailable(emojis);
        Surface("Divider", ui, 14.4f, 124.8f, 646.4f, .8f, Alpha(Gold, .2f));
        Label(ui, "CombatLabel", "ANIMAÇÕES DE COMBATE", 14.4f, 136.8f, 646.4f, 15, 12.8f, Color.white);
        var combat = Surface("Combat", ui, 14.4f, 159.8f, 646.4f, 48, Dark, 11, Gold);
        MakeButton("Normal", combat, "NORMAL", 6.4f, 6.4f, 96, 35.2f, 11.2f, Dark, Muted, null, 10, Alpha(Gold, .2f));
        MakeButton("Fast", combat, "RÁPIDAS", 112, 6.4f, 96, 35.2f, 11.2f, Dark, Muted, null, 10, Alpha(Gold, .2f));
        DimUnavailable(combat);
        var manual = MakeButton("Manual", panel, "MANUAL", 22.4f, 626, 144, 44.8f, 14.4f, Dark, Gold, null, 11, Alpha(Gold, .2f));
        DimUnavailable(manual.transform);
        MakeButton("Return", panel, "VOLTAR À PARTIDA", 176, 626, 336, 44.8f, 16, Gold, Dark, () => utilities?.CloseActive());
        requestLeave = MakeButton("RequestLeave", panel, "ABANDONAR PARTIDA", 521.6f, 626, 176, 44.8f, 14.4f,
            new Color32(90, 31, 31, 255), new Color32(255, 184, 184, 255), RequestLeave, 11, new Color32(217, 76, 76, 255));
        confirmation = Surface("LeaveConfirmation", transform, 0, 0, 400, 220, Dark, 20, Gold);
        ShieldPanel(confirmation);
        Label(confirmation, "Title", "ABANDONAR PARTIDA?", 24, 24, 352, 29, 24, Color.white, true, TextAlignmentOptions.Center);
        Label(confirmation, "Subtitle", "Você será removido desta batalha.", 24, 69, 352, 22, 16, Muted, false, TextAlignmentOptions.Center);
        MakeButton("Cancel", confirmation, "CANCELAR", 24, 107, 172, 52, 18, Dark, Gold, CancelLeave, 12, Gold);
        confirmLeave = MakeButton("ConfirmLeave", confirmation, "ABANDONAR", 208, 107, 172, 52, 18,
            new Color32(122, 31, 31, 255), Color.white, ConfirmLeave, 12, new Color32(255, 90, 95, 255));
        var glow = Icon(confirmation, "LeaveGlow", 192, 99, 204, 84);
        glow.transform.SetSiblingIndex(0);
        FitSafeArea();
        SetOpen(false);
    }

    // 03. Binding sem callbacks durante refresh. Capacidades ausentes não simulam ações.
    public void Bind(WDSettingsRepository settings, Action exit)
    {
        if (repository != null) repository.AudioChanged -= Refresh;
        repository = settings;
        if (repository != null) repository.AudioChanged += Refresh;
        leave = exit;
        Refresh();
    }

    public void BindUtilities(IWDMatchUtilityCommands commands) => utilities = commands;

    private void Refresh()
    {
        if (master == null) return;
        master.interactable = repository != null;
        master.SetValueWithoutNotify(repository?.Current.MasterVolume ?? 0);
        masterMute.interactable = repository != null;
        bool muted = repository?.Current.MasterMuted == true;
        masterMuteCircle.color = muted ? Gold : Dark;
        masterMuteIcon.color = muted ? Dark : Color.white;
        music.SetValueWithoutNotify(repository?.Current.MusicVolume ?? 0);
        sfx.SetValueWithoutNotify(repository?.Current.SfxVolume ?? 0);
        confirmLeave.interactable = leave != null && !leaving;
        requestLeave.interactable = leave != null && !leaving;
        // A preferência histórica de Resolution não é aplicada à V2/Fast Forward.
        // Sem autoridade ativa, ambos os grupos permanecem neutros/desabilitados.
    }

    // 04. Navegação e confirmação; somente o botão final pode invocar a saída existente.
    public void SetOpen(bool open)
    {
        if (panel == null) return;
        if (!open || !gameObject.activeSelf)
        {
            confirming = false;
            leaving = false;
        }
        gameObject.SetActive(open);
        if (!open) return;
        transform.SetAsLastSibling();
        Refresh();
        ShowStage();
        FitSafeArea();
    }

    private void RequestLeave()
    {
        if (utilities?.ActiveModule != WDMatchUtilityModule.Settings || leaving || leave == null) return;
        confirming = true;
        ShowStage();
    }

    private void CancelLeave()
    {
        if (leaving) return;
        confirming = false;
        ShowStage();
    }

    private void ConfirmLeave()
    {
        if (!confirming || leaving || leave == null || utilities?.ActiveModule != WDMatchUtilityModule.Settings) return;
        leaving = true;
        confirmLeave.interactable = false;
        // A cena seguinte recebe a utility encerrada, sem outra rotina de cleanup.
        utilities.CloseActive();
        leave();
    }

    private void BackdropClicked()
    {
        if (leaving) return;
        if (confirming) CancelLeave();
        else if (utilities?.ActiveModule == WDMatchUtilityModule.Settings) utilities.CloseActive();
    }

    private void ShowStage()
    {
        panel.gameObject.SetActive(!confirming);
        confirmation.gameObject.SetActive(confirming);
        backdropFilter.Panel = confirming ? confirmation : panel;
        backdropFilter.UtilityTargets = confirming ? null : targets;
    }

    // 05. Safe Area para painéis; backdrop cobre integralmente o Canvas, incluindo recortes.
    private void LateUpdate() => FitSafeArea();

    public void FitSafeArea()
    {
        if (panel == null) return;
        var area = (RectTransform)transform;
        foreach (var target in new[] { panel, confirmation })
        {
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f, .5f);
            target.anchoredPosition = Vector2.zero;
            float scale = Mathf.Min(1, Mathf.Max(0, area.rect.width - 32) / target.sizeDelta.x,
                Mathf.Max(0, area.rect.height - 32) / target.sizeDelta.y);
            target.localScale = Vector3.one * scale;
        }
        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : area;
        var corners = new Vector3[4];
        canvasRect.GetWorldCorners(corners);
        Vector3 min = area.InverseTransformPoint(corners[0]);
        Vector3 max = area.InverseTransformPoint(corners[2]);
        backdrop.anchorMin = backdrop.anchorMax = backdrop.pivot = new Vector2(.5f, .5f);
        backdrop.anchoredPosition = (min + max) * .5f;
        backdrop.sizeDelta = max - min;
    }

    // 06. Sliders uGUI recebem pointer/drag de mouse e touch pelo EventSystem existente.
    private Slider AudioRow(Transform parent, string name, string caption, float y, UnityAction<float> changed,
        UnityAction muteChanged = null)
    {
        var row = Rect(name, parent, 14.4f, y, 646.4f, 56);
        Label(row, "Label", caption, 0, 20.5f, 144, 15, 12.8f, Color.white);
        var sliderRect = Surface("Slider", row, 156.8f, -12.4f, 448, 44, Color.clear);
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.targetGraphic = sliderRect.GetComponent<Image>();
        Surface("Track", sliderRect, 0, 18.8f, 448, 6.4f, Alpha(Gold, .2f), 3);
        var fillArea = Rect("FillArea", sliderRect, 0, 18.8f, 448, 6.4f);
        var fill = Surface("Fill", fillArea, 0, 0, 448, 6.4f, Gold, 3);
        WDHomeUIFactory.Stretch(fill);
        var handleArea = Rect("HandleArea", sliderRect, 0, 14, 448, 16);
        var handle = Icon(handleArea, "SliderHandle", -8, 0, 16, 16);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
        handle.rectTransform.pivot = new Vector2(.5f, .5f);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        slider.fillRect = fill;
        slider.handleRect = handle.rectTransform;
        slider.interactable = changed != null;
        if (changed != null) slider.onValueChanged.AddListener(changed);
        var mute = MakeButton("Mute", row, "", 610, 6, 44, 44, 1, Color.clear, Gold, muteChanged);
        var circle = Surface("GlyphCircle", mute.transform, 7.6f, 7.6f, 28.8f, 28.8f, Dark, 14, Gold);
        circle.GetComponent<Image>().raycastTarget = false;
        var icon = Icon(circle, "Volume", 7.2f, 7.2f, 14.4f, 14.4f);
        if (muteChanged != null)
        {
            masterMute = mute;
            masterMuteCircle = circle.GetComponent<Image>();
            masterMuteIcon = icon;
        }
        if (changed == null) DimUnavailable(row);
        return slider;
    }

    // 07. Primitivas locais e assets; nenhum Canvas, estado de áudio ou ícone Unicode paralelo.
    private static Color Alpha(Color c, float a) => new(c.r, c.g, c.b, a);

    // 07.1. Feedback visual apenas: interactable/callbacks seguem as autoridades existentes.
    private static void DimUnavailable(Transform target)
    {
        target.gameObject.AddComponent<CanvasGroup>().alpha = .45f;
    }
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private static RectTransform Surface(string name, Transform parent, float x, float y, float w, float h,
        Color color, int radius = 0, Color? border = null)
    {
        var rect = Rect(name, parent, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        if (border.HasValue) WDHomeUIFactory.AddRoundedBorder(rect, border.Value, radius);
        return rect;
    }

    private static void ShieldPanel(RectTransform rect) => rect.gameObject.AddComponent<EventTrigger>();

    private static Button MakeButton(string name, Transform parent, string text, float x, float y, float w, float h,
        float size, Color fill, Color ink, UnityAction action, int radius = 11, Color? border = null)
    {
        var rect = Surface(name, parent, x, y, w, h, fill, radius, border);
        Label(rect, "Text", text, 0, 0, w, h, size, ink, true, TextAlignmentOptions.Center);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        button.interactable = action != null;
        if (action != null) button.onClick.AddListener(action);
        return button;
    }

    private static void Label(Transform parent, string name, string value, float x, float y, float w, float h,
        float size, Color color, bool bold = true, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = WDHomeTypography.Load(bold ? "Gabarito-SemiBold" : "Gabarito-Medium");
        text.fontSize = size;
        text.text = value;
        text.color = color;
        text.richText = false;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = align;
        text.raycastTarget = false;
    }

    private Image Icon(Transform parent, string name, float x, float y, float w, float h)
    {
        var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        var texture = Resources.Load<Texture2D>("UI/Match/Settings/" + name);
        if (texture != null)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprite.hideFlags = HideFlags.DontSave;
            sprites.Add(sprite);
            image.sprite = sprite;
        }
        image.raycastTarget = false;
        return image;
    }

    private void OnDestroy()
    {
        if (repository != null) repository.AudioChanged -= Refresh;
        foreach (var sprite in sprites)
            if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
    }
}

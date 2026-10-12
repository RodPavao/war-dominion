using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WDChatEmojiPicker : MonoBehaviour
{
    // 01. Catálogo pequeno; sem IDs de conversa, backend ou dependência de Match.
    public static readonly string[][] Catalog = {
        new string[0],
        new[] { "😀", "😃", "😄", "😁", "😂", "🤣", "😊", "😎", "😍", "🤔", "😐", "😢", "😭", "😡", "🤯" },
        new[] { "👍", "👎", "👏", "🙌", "🤝", "👊", "💪", "👌", "✌️" },
        new[] { "❤️", "💙", "🔥", "⭐", "⚡", "💯", "✅", "❌", "🎮", "🏆" }
    };
    private static readonly List<string> sessionRecents = new();
    private static readonly Color Gold = new Color32(197, 155, 99, 255);
    private static readonly Color Cyan = new Color32(32, 199, 232, 255);
    [SerializeField] private TMP_InputField target;
    [SerializeField] private RectTransform layer, popup, grid;
    [SerializeField] private Button toggle, layerCloseButton;
    [SerializeField] private ScrollRect scroll;
    private List<string> recents;
    [SerializeField] private List<Image> categories = new();
    [SerializeField] private List<Button> categoryButtons = new();
    private int anchor, focus;
    private Sprite smileSprite;
    private bool ownsSmileSprite;
    private Coroutine pendingFocus;
    private bool savedSelectAll;
    public bool IsOpen => layer != null && layer.gameObject.activeSelf;
    public int Category { get; private set; } = 1;
    public IReadOnlyList<string> Recents => recents;

    // 02. O host fornece o campo, botão e painel delimitador; recentes de sessão são compartilhados.
    public void Build(TMP_InputField input, Button button, RectTransform host, List<string> recentStore = null)
    {
        target = input;
        toggle = button;
        recents = recentStore ?? sessionRecents;
        WDChatEmojiText.Apply(input.textComponent);
        BindToggle();
        var icon = Box("Smiley", button.transform, 10, 10, 24, 24, Color.white);
        smileSprite = Resources.Load<Sprite>("UI/ChatEmoji/Smiley");
        if (smileSprite == null)
        {
            var texture = Resources.Load<Texture2D>("UI/ChatEmoji/Smiley");
            if (texture != null)
            {
                smileSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
                ownsSmileSprite = true;
            }
        }
        icon.GetComponent<Image>().sprite = smileSprite;
        icon.GetComponent<Image>().raycastTarget = false;
        layer = WDUIFactory.Rect("EmojiPickerLayer", host);
        WDHomeUIFactory.Stretch(layer);
        var outside = layer.gameObject.AddComponent<Image>(); outside.color = Color.clear;
        layerCloseButton = layer.gameObject.AddComponent<Button>();
        layerCloseButton.transition = Selectable.Transition.None;
        layerCloseButton.onClick.AddListener(Close);
        // O botão original continua sendo toggle, mesmo quando a camada cobre o Chat.
        var filter = layer.gameObject.AddComponent<WDChatEmojiToggleFilter>(); filter.Target = (RectTransform)button.transform;
        popup = Box("Popup", layer, 0, 0, 336, 260, WDMatchVisualStyle.SurfaceMatchReadable, 12);
        WDHomeUIFactory.AddRoundedBorder(popup, Gold, 12);
        popup.gameObject.AddComponent<EventTrigger>();
        string[] names = { "RECENTES", "CARINHAS", "GESTOS", "SÍMBOLOS" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var tab = MakeButton("Category" + i, popup, 8 + 80 * i, 8, 80, 44, () => SelectCategory(index));
            categoryButtons.Add(tab);
            categories.Add(tab.GetComponent<Image>());
            Label(tab.transform, names[i], 10);
        }
        var viewport = Box("Viewport", popup, 8, 60, 320, 192, Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewport;
        grid = WDUIFactory.Rect("Grid", viewport);
        grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(0, 1);
        grid.sizeDelta = new Vector2(320, 192);
        scroll.content = grid;
        layer.gameObject.SetActive(false);
    }

    // ============================================================
    // 02.1. REBIND DA HIERARQUIA SERIALIZADA; SEM RECONSTRUÇÃO
    // ============================================================
    public void BindSerialized(TMP_InputField input, Button button, List<string> recentStore = null)
    {
        target = input;
        toggle = button;
        recents = recentStore ?? sessionRecents;
        WDChatEmojiText.Apply(target.textComponent);
        BindToggle();
        if (layerCloseButton != null)
        {
            layerCloseButton.onClick.RemoveAllListeners();
            layerCloseButton.onClick.AddListener(Close);
        }
        for (int i = 0; i < categoryButtons.Count; i++)
        {
            int index = i;
            Button category = categoryButtons[i];
            if (category == null) continue;
            category.onClick.RemoveAllListeners();
            category.onClick.AddListener(() => SelectCategory(index));
        }
        layer?.gameObject.SetActive(false);
    }

    private void BindToggle()
    {
        if (toggle == null) return;
        toggle.onClick.RemoveAllListeners();
        toggle.onClick.AddListener(Toggle);
    }

    // 03. Cursor é capturado antes de perder foco para botões/categorias.
    private void LateUpdate()
    {
        if (target == null) return;
        if (target.isFocused) CaptureSelection();
        toggle.interactable = target.interactable && !target.readOnly;
        if (!toggle.interactable) Close();
        if (IsOpen) PositionPopup();
    }
    public void CaptureSelection() { anchor = target.selectionStringAnchorPosition; focus = target.selectionStringFocusPosition; }
    public void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (target == null || !target.interactable || target.readOnly) return;
        if (target.isFocused) CaptureSelection();
        layer.gameObject.SetActive(true);
        layer.SetAsLastSibling();
        PositionPopup();
        SelectCategory(Category);
    }
    public void Close()
    {
        if (layer != null) layer.gameObject.SetActive(false);
        if (pendingFocus != null)
        {
            StopCoroutine(pendingFocus);
            pendingFocus = null;
            if (target != null) target.onFocusSelectAll = savedSelectAll;
        }
    }
    private void OnDisable() => Close();

    // 04. Categorias locais, scroll e estado vazio sem histórico inventado.
    public void SelectCategory(int category)
    {
        Category = Mathf.Clamp(category, 0, 3);
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            var child = grid.GetChild(i).gameObject; child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        for (int i = 0; i < categories.Count; i++) categories[i].color = i == Category ? new Color(Cyan.r, Cyan.g, Cyan.b, .2f) : Color.clear;
        IReadOnlyList<string> values = Category == 0 ? recents : Catalog[Category];
        if (values.Count == 0) Label(grid, "Nenhum emoji recente", 14);
        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            var cell = MakeButton("Emoji" + i, grid, (i % 6) * 53, (i / 6) * 52, 48, 48, () => Choose(value));
            Label(cell.transform, value, 28);
        }
        grid.sizeDelta = new Vector2(320, Mathf.Max(192, Mathf.Ceil(values.Count / 6f) * 52));
        scroll.verticalNormalizedPosition = 1;
    }

    // 05. Inserção atômica, sem envio; recentes registram somente uma escolha aceita.
    public bool Choose(string value)
    {
        if (!IsOpen || !IsCatalogEmoji(value) || !WDChatEmojiText.TryInsert(target, value, anchor, focus, out int caret)) return false;
        anchor = focus = caret;
        recents.Remove(value); recents.Insert(0, value);
        if (recents.Count > 16) recents.RemoveRange(16, recents.Count - 16);
        Close();
        if (Application.isPlaying) pendingFocus = StartCoroutine(RestoreFocus(caret));
        return true;
    }
    public static bool IsCatalogEmoji(string value)
    {
        for (int i = 1; i < Catalog.Length; i++) foreach (string emoji in Catalog[i]) if (emoji == value) return true;
        return false;
    }
    private IEnumerator RestoreFocus(int caret)
    {
        savedSelectAll = target.onFocusSelectAll;
        target.onFocusSelectAll = false;
        target.ActivateInputField();
        yield return null;
        if (target.isActiveAndEnabled && target.interactable && !target.readOnly) target.stringPosition = caret;
        target.onFocusSelectAll = savedSelectAll;
        pendingFocus = null;
    }

    // 06. Popup limitado ao painel fornecido pelo host, acima do composer e sem Canvas próprio.
    private void PositionPopup()
    {
        var corners = new Vector3[4]; toggle.GetComponent<RectTransform>().GetWorldCorners(corners);
        Vector3 point = layer.InverseTransformPoint(corners[1]);
        float x = Mathf.Clamp(point.x - layer.rect.xMin, 8, Mathf.Max(8, layer.rect.width - 344));
        float y = Mathf.Clamp(layer.rect.yMax - point.y - 268, 8, Mathf.Max(8, layer.rect.height - 268));
        popup.anchoredPosition = new Vector2(x, -y);
    }
    private static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color, int radius = 0)
    {
        var rect = WDUIFactory.Rect(name, parent); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        return rect;
    }
    private static Button MakeButton(string name, Transform parent, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        var rect = Box(name, parent, x, y, w, h, Color.clear, 8);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        button.transition = Selectable.Transition.None; button.onClick.AddListener(action); return button;
    }
    private static void Label(Transform parent, string value, float size)
    {
        var rect = WDUIFactory.Rect("Label", parent); WDHomeUIFactory.Stretch(rect);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = WDHomeTypography.Load("Inter-Regular");
        text.fontSize = size; text.text = value; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; WDChatEmojiText.Apply(text, picker: true);
    }
    private void OnDestroy()
    {
        if (toggle != null) toggle.onClick.RemoveListener(Toggle);
        if (ownsSmileSprite && smileSprite != null)
        { if (Application.isPlaying) Destroy(smileSprite); else DestroyImmediate(smileSprite); }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WDHomeNotificationKind
{
    FriendRequest,
    ClanInvitation,
    ClanJoinRequest,
    Information
}

public sealed class WDHomeNotificationItem
{
    // ============================================================
    // 01. PROJECAO VISUAL RECEBIDA DE UMA AUTORIDADE EXTERNA
    // ============================================================

    public WDHomeNotificationKind Kind { get; }
    public string Source { get; }
    public string Title { get; }
    public string Description { get; }
    public DateTimeOffset? CreatedAt { get; }
    public bool IsUnread { get; }
    public UnityAction PrimaryAction { get; }
    public UnityAction SecondaryAction { get; }

    public WDHomeNotificationItem(WDHomeNotificationKind kind, string source,
        string title, string description, DateTimeOffset? createdAt, bool isUnread,
        UnityAction primaryAction = null, UnityAction secondaryAction = null)
    {
        Kind = kind;
        Source = source ?? string.Empty;
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        CreatedAt = createdAt;
        IsUnread = isUnread;
        PrimaryAction = primaryAction;
        SecondaryAction = secondaryAction;
    }
}

[DisallowMultipleComponent]
public sealed class WDHomeNotificationCenterPanel : MonoBehaviour
{
    // ============================================================
    // 02. CAMADA SOBRE A HOME EXISTENTE E SAFE AREA
    // ============================================================

    private static readonly Color PanelColor = new Color(0.067f, 0.075f, 0.09f, 0.9f);
    private static readonly Color ItemColor = new Color(0.086f, 0.094f, 0.114f, 0.96f);
    private static readonly Color MainText = new Color(0.961f, 0.965f, 0.973f, 1f);
    private static readonly Color MutedText = new Color(0.557f, 0.573f, 0.604f, 1f);
    private static readonly Color Cyan = new Color(0f, 0.898f, 1f, 1f);
    private static readonly Color Gold = new Color(0.773f, 0.608f, 0.388f, 1f);
    private static readonly Color Red = new Color(1f, 0.231f, 0.188f, 1f);

    private WarDominionUITheme theme;
    private RectTransform safeFrame;
    private RectTransform frame;
    private RectTransform listContent;
    private ScrollRect scroll;
    private IReadOnlyList<WDHomeNotificationItem> items = Array.Empty<WDHomeNotificationItem>();

    public bool IsOpen => gameObject.activeSelf;

    public void Build(WarDominionUITheme currentTheme)
    {
        theme = currentTheme;
        Image dim = gameObject.AddComponent<Image>();
        dim.color = new Color(0.012f, 0.016f, 0.025f, 0.72f);
        dim.raycastTarget = true;
        Button outside = gameObject.AddComponent<Button>();
        outside.transition = Selectable.Transition.None;
        outside.onClick.AddListener(Close);

        safeFrame = WDHomeUIFactory.Rect("NotificationSafeArea", transform);
        WDHomeUIFactory.Stretch(safeFrame);
        frame = WDHomeUIFactory.Rect("NotificationFrame", safeFrame);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = new Vector2(1360f, 820f);

        RectTransform header = WDHomeUIFactory.Rect("Header", frame);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.offsetMin = new Vector2(0f, -76f);
        header.offsetMax = Vector2.zero;
        RectTransform back = WDHomeUIFactory.Rect("Back", header);
        back.anchorMin = back.anchorMax = new Vector2(0f, 0.5f);
        back.pivot = new Vector2(0f, 0.5f);
        back.anchoredPosition = new Vector2(0f, 0f);
        back.sizeDelta = new Vector2(118f, 42f);
        Image backSurface = back.gameObject.AddComponent<Image>();
        backSurface.color = new Color(0.09f, 0.11f, 0.14f, 0.9f);
        WDHomeUIFactory.ApplyRounded(backSurface, 20);
        AddBorder(back, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
        back.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        Text("BackLabel", back, "< VOLTAR", 16f, Gold,
            TextAlignmentOptions.Center);

        RectTransform title = WDHomeUIFactory.Rect("TitleArea", header);
        title.anchorMin = new Vector2(0f, 0f);
        title.anchorMax = Vector2.one;
        title.offsetMin = new Vector2(145f, 0f);
        title.offsetMax = Vector2.zero;
        Text("Title", title, "CENTRAL DE NOTIFICA\u00c7\u00d5ES", 34f,
            MainText, TextAlignmentOptions.MidlineLeft);

        RectTransform surface = WDHomeUIFactory.Rect("CenterSurface", frame);
        surface.anchorMin = Vector2.zero;
        surface.anchorMax = Vector2.one;
        surface.offsetMin = Vector2.zero;
        surface.offsetMax = new Vector2(0f, -84f);
        Image panel = surface.gameObject.AddComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(panel, 18);
        AddBorder(surface, new Color(Gold.r, Gold.g, Gold.b, 0.28f));

        RectTransform viewport = WDHomeUIFactory.Rect("Viewport", surface);
        WDHomeUIFactory.Stretch(viewport, 22f, 22f, 22f, 22f);
        viewport.gameObject.AddComponent<RectMask2D>();
        listContent = WDHomeUIFactory.Rect("Items", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = Vector2.one;
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = listContent.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(6, 6, 8, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = surface.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        Render();
        gameObject.SetActive(false);
    }

    public void SetSafeArea(Vector2 min, Vector2 max)
    {
        if (safeFrame == null) return;
        safeFrame.anchorMin = min;
        safeFrame.anchorMax = max;
        safeFrame.offsetMin = safeFrame.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (safeFrame == null || frame == null) return;
        frame.sizeDelta = new Vector2(
            Mathf.Min(1360f, Mathf.Max(320f, safeFrame.rect.width - 64f)),
            Mathf.Min(820f, Mathf.Max(300f, safeFrame.rect.height - 64f)));
    }

    public void Open()
    {
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        scroll.verticalNormalizedPosition = 1f;
    }

    public void Close() => gameObject.SetActive(false);

    // ============================================================
    // 03. BINDING SOMENTE DE LEITURA; NENHUM FEED OU READ-STATE LOCAL
    // ============================================================

    public void SetItems(IReadOnlyList<WDHomeNotificationItem> validatedItems)
    {
        items = validatedItems ?? Array.Empty<WDHomeNotificationItem>();
        if (listContent != null) Render();
    }

    private void Render()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = listContent.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        AddSection("NewSection", "NOVAS", Cyan);
        int newCount = 0;
        foreach (WDHomeNotificationItem item in items)
            if (item != null && item.IsUnread)
            {
                AddItem(item);
                newCount++;
            }
        if (newCount == 0)
            AddEmpty("Nenhuma nova notifica\u00e7\u00e3o.");

        AddSection("EarlierSection", "ANTERIORES", Gold);
        int earlierCount = 0;
        foreach (WDHomeNotificationItem item in items)
            if (item != null && !item.IsUnread)
            {
                AddItem(item);
                earlierCount++;
            }
        if (earlierCount == 0)
            AddEmpty("Nenhuma notifica\u00e7\u00e3o anterior.");
    }

    // ============================================================
    // 04. ITENS, TIMESTAMPS E ACOES EXPLICITAMENTE FORNECIDAS
    // ============================================================

    private void AddSection(string name, string label, Color color)
    {
        RectTransform row = WDHomeUIFactory.Rect(name, listContent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        Text("Label", row, label, 18f, color, TextAlignmentOptions.MidlineLeft);
    }

    private void AddEmpty(string message)
    {
        RectTransform row = WDHomeUIFactory.Rect("Empty", listContent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112f;
        Image surface = row.gameObject.AddComponent<Image>();
        surface.color = ItemColor;
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, 12);
        AddBorder(row, new Color(1f, 1f, 1f, 0.08f));
        Text("Message", row, message, 17f, MutedText,
            TextAlignmentOptions.Center);
    }

    private void AddItem(WDHomeNotificationItem item)
    {
        bool hasActions = item.PrimaryAction != null || item.SecondaryAction != null;
        RectTransform row = WDHomeUIFactory.Rect("NotificationItem", listContent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = hasActions ? 156f : 120f;
        Image surface = row.gameObject.AddComponent<Image>();
        surface.color = ItemColor;
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, 12);
        AddBorder(row, new Color(1f, 1f, 1f, 0.12f));
        Line("Source", row, item.Source, 13f, Cyan, 16f, 20f, 20f, 200f);
        Line("Title", row, item.Title, 19f, MainText, 39f, 28f, 20f, 20f);
        Line("Description", row, item.Description, 15f, MutedText, 71f, 39f, 20f, 20f);
        if (item.CreatedAt.HasValue)
        {
            RectTransform stamp = WDHomeUIFactory.Rect("Timestamp", row);
            stamp.anchorMin = stamp.anchorMax = Vector2.one;
            stamp.pivot = Vector2.one;
            stamp.anchoredPosition = new Vector2(-18f, -12f);
            stamp.sizeDelta = new Vector2(180f, 22f);
            Text("Value", stamp, item.CreatedAt.Value.ToLocalTime().ToString("dd/MM HH:mm"),
                12f, MutedText, TextAlignmentOptions.Right);
        }
        if (!hasActions) return;

        string primary = item.Kind == WDHomeNotificationKind.ClanJoinRequest
            ? "ANALISAR" : "ACEITAR";
        if (item.PrimaryAction != null)
            ActionButton(row, "Primary", primary, 20f, Cyan, item.PrimaryAction);
        if (item.SecondaryAction != null)
            ActionButton(row, "Secondary", "RECUSAR", 142f, Red, item.SecondaryAction);
    }

    private void ActionButton(Transform parent, string name, string label,
        float x, Color accent, UnityAction action)
    {
        RectTransform button = WDHomeUIFactory.Rect(name, parent);
        button.anchorMin = button.anchorMax = Vector2.zero;
        button.pivot = Vector2.zero;
        button.anchoredPosition = new Vector2(x, 12f);
        button.sizeDelta = new Vector2(110f, 32f);
        Image image = button.gameObject.AddComponent<Image>();
        image.color = new Color(accent.r, accent.g, accent.b, 0.16f);
        WDHomeUIFactory.ApplyRounded(image, 5);
        AddBorder(button, new Color(accent.r, accent.g, accent.b, 0.8f));
        button.gameObject.AddComponent<Button>().onClick.AddListener(action);
        Text("Label", button, label, 13f, accent, TextAlignmentOptions.Center);
    }

    private void Line(string name, Transform parent, string value, float size,
        Color color, float top, float height, float left, float right)
    {
        TextMeshProUGUI text = WDHomeUIFactory.Text(name, parent, theme,
            value, size, color);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void Text(string name, Transform parent, string value, float size,
        Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = WDHomeUIFactory.Text(name, parent, theme,
            value, size, color, alignment);
        WDHomeUIFactory.Stretch(text.rectTransform);
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void AddBorder(RectTransform target, Color color)
    {
        Outline border = target.gameObject.AddComponent<Outline>();
        border.effectColor = color;
        border.effectDistance = Vector2.one;
    }
}

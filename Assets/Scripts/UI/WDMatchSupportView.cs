using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Figma 2436:80 / 2028:31. A View coleta intenção; não possui regras ou efeitos.
public sealed class WDMatchSupportView : MonoBehaviour
{
    private WarDominionUITheme theme;
    private WDWeaponMatchUIController weapons;
    private RectTransform panel, outside;
    private readonly Vector3[] canvasCorners = new Vector3[4];
    private WDMatchSupportTransportView transport;
    private readonly WDUIPremiumButton[] slots = new WDUIPremiumButton[2];
    private readonly RawImage[] artworks = new RawImage[2];
    private readonly AspectRatioFitter[] artworkAspects = new AspectRatioFitter[2];
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[2];
    private readonly TextMeshProUGUI[] levels = new TextMeshProUGUI[2];
    private readonly Image[] energyIcons = new Image[2];
    private readonly TextMeshProUGUI[] energyCosts = new TextMeshProUGUI[2];
    private readonly GameObject[] cooldowns = new GameObject[2];
    private readonly TextMeshProUGUI[] cooldownCounts = new TextMeshProUGUI[2];
    private readonly Image[] selectedIndicators = new Image[2];
    private readonly Image[] programmedIndicators = new Image[2];
    private WDArmoryVisualCatalog visualCatalog;
    private WDArmoryV2VisualSettings visualSettings;
    private readonly Dictionary<int, Sprite> borders = new();
    private readonly List<Texture2D> textures = new();
    private bool suppressed;
    private Transform entry;
    private static readonly List<WDMatchSupportView> liveViews = new();
    private readonly Dictionary<string, TMP_FontAsset> fonts = new();
    private static WDMatchSupportView consumedBy;
    private static int consumedFrame = -1;
    internal static readonly Color Gold = new Color32(197, 155, 99, 255);
    internal static readonly Color White = new Color32(245, 246, 248, 255);
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;
    public event Action IntentChanged;

    public void Build(WarDominionUITheme value)
    {
        theme = value;
        visualCatalog = Resources.Load<WDArmoryVisualCatalog>("UI/Armory/WDArmoryVisualCatalog");
        visualSettings = Resources.Load<WDArmoryV2VisualSettings>("UI/ArmoryV2/WDArmoryV2VisualSettings");
        liveViews.Add(this);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ProtectFonts);
        outside = Rect("SupportOutside", transform, 0, 0, 0, 0);
        outside.anchorMin = Vector2.zero; outside.anchorMax = Vector2.one;
        outside.offsetMin = outside.offsetMax = Vector2.zero;
        outside.gameObject.AddComponent<Image>().color = Color.clear;
        Button dismiss = outside.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        // Fechar no click/release mantém a captura durante todo o press, como AÇÕES.
        dismiss.onClick.AddListener(Close);
        // Figma atual: 5 px à direita do rail, no referencial da Safe Area.
        panel = Surface("SupportHub", transform, 290, 826, 252, 145,
            new Color(11f / 255, 14f / 255, 18f / 255, .94f), 10, Gold);
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            RectTransform card = Rect("SupportSlot" + (i + 1), panel,
                11 + i * 121, 12, 109, 121);
            slots[i] = card.gameObject.AddComponent<WDUIPremiumButton>();
            slots[i].Build(theme, string.Empty, () => SelectSlot(index));
            slots[i].SetHoverFeedbackEnabled(false);
            slots[i].SetPersistentColors(new Color(6f / 255, 9f / 255, 14f / 255, .72f),
                new Color(Gold.r, Gold.g, Gold.b, .18f));
            slots[i].SetDisabledColor(new Color(6f / 255, 9f / 255, 14f / 255, .72f));
            TextMeshProUGUI label = card.GetComponentInChildren<TextMeshProUGUI>();
            label.gameObject.name = "WeaponName";
            label.fontSize = 9f; label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true; label.fontSizeMin = 7.5f; label.fontSizeMax = 9f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(.5f, 0f);
            label.rectTransform.offsetMin = new Vector2(6f, 24f);
            label.rectTransform.offsetMax = new Vector2(-6f, 55f);
            names[i] = label;

            // ============================================================
            // 01.1. CARTA DE APOIO ILUSTRADA COM O MESMO RODAPÉ DAS PRINCIPAIS
            // ============================================================
            RectTransform frame = Rect("ArtworkFrame", card, 4, 4, 101, 59);
            Image frameSurface = frame.gameObject.AddComponent<Image>();
            frameSurface.color = new Color32(3, 8, 14, 255); frameSurface.raycastTarget = false;
            WDHomeUIFactory.ApplyRounded(frameSurface, 4); frame.gameObject.AddComponent<RectMask2D>();
            RectTransform art = Rect("Artwork", frame, 2, 2, 97, 55);
            artworks[i] = art.gameObject.AddComponent<RawImage>(); artworks[i].raycastTarget = false;
            artworkAspects[i] = art.gameObject.AddComponent<AspectRatioFitter>();
            artworkAspects[i].aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            levels[i] = Label(card, "Level", string.Empty, 6, 99, 34, 17, 10, White, true);
            energyIcons[i] = Rect("EnergyIcon", card, 72, 102, 12, 12).gameObject.AddComponent<Image>();
            energyIcons[i].sprite = LoadSprite("UI/Match/Official/Energy");
            energyIcons[i].color = new Color32(13, 194, 219, 255);
            energyIcons[i].preserveAspect = true; energyIcons[i].raycastTarget = false;
            energyCosts[i] = Label(card, "EnergyCost", string.Empty, 86, 99, 17, 17, 10, White, true);
            energyCosts[i].alignment = TextAlignmentOptions.MidlineRight;

            RectTransform cooldown = Rect("Cooldown", card, 66, 5, 38, 22);
            Image cooldownSurface = cooldown.gameObject.AddComponent<Image>();
            // ============================================================
            // 01.2. COOLDOWN OPACO INDEPENDENTE DO DIM DA ILUSTRAÇÃO
            // ============================================================
            cooldownSurface.color = new Color32(5, 12, 22, 242); cooldownSurface.raycastTarget = false;
            WDHomeUIFactory.ApplyRounded(cooldownSurface, 4);
            Image clock = Rect("Clock", cooldown, 4, 4, 14, 14).gameObject.AddComponent<Image>();
            clock.sprite = LoadSprite("UI/Match/Official/Time"); clock.color = White;
            clock.preserveAspect = true; clock.raycastTarget = false;
            cooldownCounts[i] = Label(cooldown, "Count", string.Empty, 18, 0, 20, 22, 11, White, true);
            cooldownCounts[i].alignment = TextAlignmentOptions.Center;
            cooldown.gameObject.SetActive(false); cooldowns[i] = cooldown.gameObject;

            selectedIndicators[i] = Rect("SelectionIndicator", card, 0, 0, 3, 121).gameObject.AddComponent<Image>();
            selectedIndicators[i].color = new Color32(13, 194, 219, 255);
            selectedIndicators[i].raycastTarget = false; selectedIndicators[i].gameObject.SetActive(false);
            programmedIndicators[i] = Rect("ProgrammedIndicator", card, 6, 6, 16, 16).gameObject.AddComponent<Image>();
            programmedIndicators[i].color = Gold; programmedIndicators[i].raycastTarget = false;
            programmedIndicators[i].sprite = Resources.Load<Sprite>("UI/Store/Canonical/icon_check");
            programmedIndicators[i].preserveAspect = true;
            programmedIndicators[i].gameObject.SetActive(false);
            label.transform.SetAsLastSibling();
        }
        transport = gameObject.AddComponent<WDMatchSupportTransportView>();
        transport.Build(this);
        panel.gameObject.SetActive(false);
        outside.gameObject.SetActive(false);
        Refresh();
    }

    public void BindEntry(Transform button) => entry = button;

    public void Bind(WDWeaponMatchUIController controller)
    {
        if (ReferenceEquals(weapons, controller)) return;
        weapons = controller;
        Refresh();
    }

    public void Toggle()
    {
        if (suppressed || panel == null) return;
        ConsumeThisFrame();
        panel.gameObject.SetActive(!IsOpen);
        Refresh();
    }

    public void Close()
    {
        // Fechamento de lifecycle em painel já recolhido não representa input.
        // Registrar o latch nesse caso descartava o primeiro PointerDown do mapa
        // no mesmo frame de uma mudança de round/fase.
        if (IsOpen) ConsumeThisFrame();
        panel.gameObject.SetActive(false);
        outside.gameObject.SetActive(false);
        // Recolher o hub não cancela intenção nem ação programada.
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
        if (value && IsOpen) Close();
        Refresh();
    }

    private void SelectSlot(int index)
    {
        ConsumeThisFrame();
        IReadOnlyList<WDWeaponHudCardState> cards = weapons?.GetSupportCards();
        if (cards == null || index >= cards.Count || !CanSelect(cards[index])) return;
        weapons.SelectWeapon(cards[index].WeaponId);
        Changed();
    }

    private bool CanSelect(WDWeaponHudCardState card) => weapons?.CanProgramSupport == true &&
        !string.IsNullOrEmpty(card.WeaponId) && !card.Programmed &&
        card.AvailabilityReason == WDWeaponAvailabilityReason.Available;

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (panel == null) return;
        outside.gameObject.SetActive(IsOpen);
        if (IsOpen) FitOutsideToCanvas();
        IReadOnlyList<WDWeaponHudCardState> cards = weapons?.GetSupportCards();
        for (int i = 0; i < slots.Length; i++)
        {
            WDWeaponHudCardState card = cards != null && i < cards.Count ? cards[i] : null;
            bool occupied = card != null && !string.IsNullOrEmpty(card.WeaponId);
            slots[i].SetInteractable(occupied && CanSelect(card));
            slots[i].SetSelected(card?.Selected == true);
            slots[i].SetLabel(occupied ? card.DisplayName.ToUpperInvariant() : string.Empty);
            Texture artwork = occupied ? ResolveArtwork(card.WeaponId) : null;
            artworks[i].texture = artwork;
            artworks[i].gameObject.SetActive(occupied);
            artworks[i].color = !occupied ? Color.clear :
                card.AvailabilityReason == WDWeaponAvailabilityReason.Available
                    ? Color.white : new Color(1f, 1f, 1f, .52f);
            artworkAspects[i].aspectRatio = artwork != null && artwork.height > 0
                ? (float)artwork.width / artwork.height : 1f;
            levels[i].text = occupied ? $"N{Mathf.Max(1, card.Level)}" : string.Empty;
            energyCosts[i].text = occupied ? card.EnergyCost.ToString() : string.Empty;
            energyIcons[i].gameObject.SetActive(occupied);
            energyIcons[i].color = card?.AvailabilityReason == WDWeaponAvailabilityReason.InsufficientEnergy
                ? new Color(13f / 255f, 194f / 255f, 219f / 255f, .38f)
                : new Color32(13, 194, 219, 255);
            bool cooling = occupied && (card.AvailabilityReason == WDWeaponAvailabilityReason.Cooldown ||
                card.AvailabilityReason == WDWeaponAvailabilityReason.InitialRoundLock);
            cooldowns[i].SetActive(cooling);
            if (cooling) cooldownCounts[i].text = Mathf.Max(0, card.CooldownRemaining).ToString();
            selectedIndicators[i].gameObject.SetActive(false);
            programmedIndicators[i].gameObject.SetActive(occupied && (card.Programmed ||
                card.AvailabilityReason == WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation));
            SetBorderColor(slots[i].transform as RectTransform,
                occupied ? CardBorderColor(card) : new Color(1f, 1f, 1f, .08f));
        }
        transport.Refresh(weapons, suppressed);
    }

    private void FitOutsideToCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(canvasCorners);
        RectTransform parent = (RectTransform)transform;
        Vector3 min = parent.InverseTransformPoint(canvasCorners[0]);
        Vector3 max = parent.InverseTransformPoint(canvasCorners[2]);
        outside.offsetMin = (Vector2)min - parent.rect.min;
        outside.offsetMax = (Vector2)max - parent.rect.max;
    }

    internal void Changed() { ConsumeThisFrame(); Refresh(); IntentChanged?.Invoke(); }

    private Texture ResolveArtwork(string weaponId)
    {
        Texture fallback = visualCatalog?.Resolve(weaponId) ?? WDWeaponArtworkResolver.Resolve(weaponId);
        return visualSettings != null ? visualSettings.ResolveArtwork(weaponId, fallback) : fallback;
    }

    private static Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);

    private static Color CategoryColor(string category)
    {
        string value = (category ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("support") || value.Contains("apoio")) return new Color32(34, 184, 125, 230);
        if (value.Contains("tactical") || value.Contains("tática") || value.Contains("tatica")) return new Color32(25, 187, 212, 230);
        if (value.Contains("strategic") || value.Contains("estratégica") || value.Contains("estrategica")) return new Color32(183, 138, 69, 230);
        if (value.Contains("special") || value.Contains("especial")) return new Color32(168, 120, 219, 230);
        return new Color32(255, 255, 255, 40);
    }

    private static Color CardBorderColor(WDWeaponHudCardState card)
    {
        if (card.Selected) return new Color32(20, 220, 242, 255);
        if (card.Programmed || card.AvailabilityReason == WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation)
            return new Color32(214, 170, 98, 255);
        return CategoryColor(card.CategoryId);
    }

    private static void SetBorderColor(RectTransform root, Color color)
    {
        Transform border = root?.Find("Border");
        if (border != null && border.TryGetComponent(out Image image)) image.color = color;
    }
    internal void ConsumeThisFrame() { consumedBy = this; consumedFrame = Time.frameCount; }

    public static bool ConsumesMapPointer(Vector2 position) =>
        ConsumesMapPointerAtFrame(position, Time.frameCount);

    private static bool ConsumesMapPointerAtFrame(Vector2 position, int frame)
    {
        if (consumedBy != null && consumedFrame == frame) return true;
        if (EventSystem.current == null) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        foreach (RaycastResult hit in hits)
        {
            if (hit.gameObject.GetComponentInParent<WDMatchSupportView>() != null) return true;
            foreach (WDMatchSupportView view in liveViews)
                if (view != null && view.entry != null && hit.gameObject.transform.IsChildOf(view.entry)) return true;
        }
        return false;
    }

    internal static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    internal RectTransform Surface(string name, Transform parent, float x, float y,
        float w, float h, Color color, int radius, Color border)
    {
        RectTransform rect = Rect(name, parent, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>(); image.color = color;
        WDHomeUIFactory.ApplyRounded(image, radius);
        Image outline = WDHomeUIFactory.AddRoundedBorder(rect, border, radius);
        outline.sprite = Border(radius);
        return rect;
    }

    // Contorno de 1 px conforme Figma, sem modificar a fábrica dos módulos congelados.
    private Sprite Border(int radius)
    {
        if (borders.TryGetValue(radius, out Sprite sprite)) return sprite;
        int size = radius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "SupportBorder", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = size * .5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(0, Mathf.Abs(x + .5f - half) - (half - radius));
            float dy = Mathf.Max(0, Mathf.Abs(y + .5f - half) - (half - radius));
            float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            pixels[y * size + x] = new Color32(255, 255, 255,
                (byte)Mathf.RoundToInt((Mathf.Clamp01(.5f - distance) - Mathf.Clamp01(-.5f - distance)) * 255));
        }
        texture.SetPixels32(pixels); texture.Apply(false, true); textures.Add(texture);
        float edge = radius + 1;
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
        sprite.hideFlags = HideFlags.DontSave; borders.Add(radius, sprite);
        return sprite;
    }

    internal TextMeshProUGUI Label(Transform parent, string name, string text,
        float x, float y, float w, float h, float size, Color color, bool inter = false)
    {
        TextMeshProUGUI label = WDUIFactory.Text(name, parent, theme, size, color, TextAlignmentOptions.MidlineLeft);
        if (inter) label.font = Font("Inter-Bold");
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        label.richText = false; label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Ellipsis; label.text = text;
        return label;
    }

    internal Button Click(Transform parent, string name, string text, float x, float y,
        float w, float h, Action callback)
    {
        RectTransform rect = Rect(name, parent, x, y, w, h);
        rect.gameObject.AddComponent<Image>().color = Color.clear;
        Button button = rect.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { ConsumeThisFrame(); callback(); });
        if (!string.IsNullOrEmpty(text)) Label(rect, "Label", text, 0, 0, w, h, 16, White, true).alignment = TextAlignmentOptions.Center;
        return button;
    }

    internal TMP_FontAsset Font(string name)
    {
        if (fonts.TryGetValue(name, out TMP_FontAsset font)) return font;
        UnityEngine.Font source = Resources.Load<UnityEngine.Font>("UI/Fonts/" + name);
        if (source == null) return theme.FonteInterface;
        font = TMP_FontAsset.CreateFontAsset(source);
        font.name = "Support " + name;
        font.hideFlags = HideFlags.DontSave;
        fonts.Add(name, font);
        ProtectFonts(null);
        return font;
    }

    private void ProtectFonts(UnityEngine.Object changed)
    {
        foreach (TMP_FontAsset font in fonts.Values)
        {
            font.material.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < font.atlasTextureCount; i++)
                font.atlasTextures[i].hideFlags = HideFlags.DontSave;
        }
    }

    private void OnDestroy()
    {
        liveViews.Remove(this);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ProtectFonts);
        // Cada fonte pertence somente a esta View; TMP libera seus próprios atlas/material.
        foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
            if (fonts.ContainsValue(label.font)) label.font = null;
        foreach (TMP_FontAsset font in fonts.Values)
            if (Application.isPlaying) Destroy(font); else DestroyImmediate(font);
        if (consumedBy == this) { consumedBy = null; consumedFrame = -1; }
        foreach (Sprite sprite in borders.Values)
            if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
        foreach (Texture2D texture in textures)
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum WDHomeVisualVariant
{
    Original = 0,
    ChromePremium = 1,
    SapphireGlass = 2
}

public readonly struct WDHomeVisualPalette
{
    public readonly Color Surface;
    public readonly Color ElevatedSurface;
    public readonly Color Border;
    public readonly Color Gold;
    public readonly Color Highlight;
    public readonly Color IconFrame;
    public readonly int PlayOuterRadius;
    public readonly int PlayInnerRadius;

    public WDHomeVisualPalette(Color surface, Color elevatedSurface, Color border,
        Color gold, Color highlight, Color iconFrame, int playOuterRadius, int playInnerRadius)
    {
        Surface = surface;
        ElevatedSurface = elevatedSurface;
        Border = border;
        Gold = gold;
        Highlight = highlight;
        IconFrame = iconFrame;
        PlayOuterRadius = playOuterRadius;
        PlayInnerRadius = playInnerRadius;
    }

    public static WDHomeVisualPalette For(WDHomeVisualVariant variant)
    {
        return variant == WDHomeVisualVariant.SapphireGlass
            ? new WDHomeVisualPalette(
                new Color(0.025f, 0.095f, 0.17f, 0.70f),
                new Color(0.045f, 0.14f, 0.235f, 0.80f),
                new Color(0.64f, 0.86f, 1f, 0.62f),
                new Color(1f, 0.79f, 0.31f, 1f),
                new Color(0.72f, 0.91f, 1f, 0.16f),
                new Color(0.08f, 0.22f, 0.34f, 0.78f), 24, 20)
            : new WDHomeVisualPalette(
                new Color(0.025f, 0.075f, 0.135f, 0.93f),
                new Color(0.055f, 0.13f, 0.21f, 0.96f),
                new Color(0.76f, 0.86f, 0.94f, 0.76f),
                new Color(1f, 0.76f, 0.24f, 1f),
                new Color(0.92f, 0.97f, 1f, 0.20f),
                new Color(0.10f, 0.20f, 0.29f, 0.94f), 24, 20);
    }
}

[DisallowMultipleComponent]
public sealed class WDHomeVisualAppearanceController : MonoBehaviour
{
    public const string PlayerPrefsKey = "wd.home.visual.variant";

    private static readonly string[] SurfaceNames =
    {
        "PlayerAccount", "LeagueStrip", "RankingCard", "MissionsCard", "ChatCard",
        "FeaturedTournamentCard", "ProfileCard", "FriendsCard", "StoreCard", "SettingsCard",
        "ArmoryCard", "ClanCard", "MainPlayButton", "Inner", "Coins", "Emeralds",
        "CanonicalNotification"
    };

    private readonly Dictionary<Graphic, Color> originalGraphicColors = new();
    private readonly Dictionary<Image, Sprite> originalImageSprites = new();
    private readonly Dictionary<Image, Image.Type> originalImageTypes = new();
    private readonly Dictionary<Shadow, Color> originalEffectColors = new();
    private Transform homeContent;
    private WDHomeVisualVariant current;

    public WDHomeVisualVariant Current => current;

    public void Initialize(Transform content)
    {
        homeContent = content;
        CaptureOriginalState();
        int saved = PlayerPrefs.GetInt(PlayerPrefsKey, (int)WDHomeVisualVariant.ChromePremium);
        Apply((WDHomeVisualVariant)Mathf.Clamp(saved, 0, 2), false);
    }

    private void Update()
    {
        if (Keyboard.current?.f6Key.wasPressedThisFrame == true)
            Apply(current == WDHomeVisualVariant.ChromePremium
                ? WDHomeVisualVariant.SapphireGlass
                : WDHomeVisualVariant.ChromePremium);
    }

    public void Apply(WDHomeVisualVariant variant, bool persist = true)
    {
        if (homeContent == null)
            return;

        RestoreOriginalState();
        RemoveGeneratedHighlights();
        current = variant;
        if (persist)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)variant);
            PlayerPrefs.Save();
        }
        if (variant == WDHomeVisualVariant.Original)
            return;

        WDHomeVisualPalette palette = WDHomeVisualPalette.For(variant);
        ApplySurfaces(palette, variant);
        ApplyIcons(palette);
        ApplyPlayTreatment(palette);
    }

    private void CaptureOriginalState()
    {
        if (homeContent == null)
            return;
        foreach (Graphic graphic in homeContent.GetComponentsInChildren<Graphic>(true))
            originalGraphicColors[graphic] = graphic.color;
        foreach (Image image in homeContent.GetComponentsInChildren<Image>(true))
        {
            originalImageSprites[image] = image.sprite;
            originalImageTypes[image] = image.type;
        }
        foreach (Shadow effect in homeContent.GetComponentsInChildren<Shadow>(true))
            originalEffectColors[effect] = effect.effectColor;
    }

    private void RestoreOriginalState()
    {
        foreach (KeyValuePair<Graphic, Color> pair in originalGraphicColors)
            if (pair.Key != null) pair.Key.color = pair.Value;
        foreach (KeyValuePair<Image, Sprite> pair in originalImageSprites)
            if (pair.Key != null) pair.Key.sprite = pair.Value;
        foreach (KeyValuePair<Image, Image.Type> pair in originalImageTypes)
            if (pair.Key != null) pair.Key.type = pair.Value;
        foreach (KeyValuePair<Shadow, Color> pair in originalEffectColors)
            if (pair.Key != null) pair.Key.effectColor = pair.Value;
    }

    private void ApplySurfaces(WDHomeVisualPalette palette, WDHomeVisualVariant variant)
    {
        foreach (string nodeName in SurfaceNames)
        {
            foreach (Transform node in FindAll(homeContent, nodeName))
            {
                Image image = node.GetComponent<Image>();
                if (image == null)
                    continue;
                bool elevated = nodeName == "PlayerAccount" || nodeName == "LeagueStrip" ||
                    nodeName == "MainPlayButton" || nodeName == "Inner";
                image.color = elevated ? palette.ElevatedSurface : palette.Surface;
                Transform border = node.Find("Border");
                if (border != null && border.TryGetComponent(out Image borderImage))
                    borderImage.color = nodeName == "MainPlayButton" ? palette.Gold : palette.Border;
                AddHighlight(node as RectTransform, palette.Highlight,
                    variant == WDHomeVisualVariant.ChromePremium ? 3f : 2f);
            }
        }
    }

    private void ApplyIcons(WDHomeVisualPalette palette)
    {
        ApplyCardIdentity("RankingCard", new Color(1f, 0.27f, 0.31f, 1f), palette);
        ApplyCardIdentity("MissionsCard", new Color(0.20f, 0.92f, 0.56f, 1f), palette);
        ApplyCardIdentity("ChatCard", new Color(0.25f, 0.66f, 1f, 1f), palette);
        ApplyCardIdentity("ProfileCard", new Color(0.66f, 0.43f, 1f, 1f), palette);
        ApplyCardIdentity("FriendsCard", new Color(0.18f, 0.88f, 0.64f, 1f), palette);
        ApplyCardIdentity("StoreCard", palette.Gold, palette);
        ApplyCardIdentity("SettingsCard", new Color(0.92f, 0.97f, 1f, 1f), palette);
        ApplyCardIdentity("ArmoryCard", new Color(0.26f, 0.58f, 1f, 1f), palette);
        ApplyCardIdentity("ClanCard", new Color(0.88f, 0.27f, 0.42f, 1f), palette);
    }

    private void ApplyCardIdentity(string cardName, Color accent, WDHomeVisualPalette palette)
    {
        foreach (Transform card in FindAll(homeContent, cardName))
        {
            Transform frame = card.Find("IconFrame") ?? card.Find("ContentGroup/IconFrame");
            if (frame != null)
            {
                if (frame.TryGetComponent(out Image frameImage)) frameImage.color = palette.IconFrame;
                Transform frameBorder = frame.Find("Border");
                if (frameBorder != null && frameBorder.TryGetComponent(out Image frameBorderImage))
                    frameBorderImage.color = new Color(accent.r, accent.g, accent.b, 0.78f);
                Transform icon = frame.Find("Icon");
                if (icon != null && icon.TryGetComponent(out RawImage rawIcon)) rawIcon.color = accent;
            }
            Transform title = card.Find("Title") ?? card.Find("ContentGroup/Title");
            if (title != null && title.TryGetComponent(out TextMeshProUGUI titleText))
                titleText.color = accent;
        }
    }

    private void ApplyPlayTreatment(WDHomeVisualPalette palette)
    {
        Transform play = FindFirst(homeContent, "MainPlayButton");
        if (play == null)
            return;
        if (play.TryGetComponent(out Image border))
        {
            border.color = palette.Gold;
            WDHomeUIFactory.ApplyRounded(border, palette.PlayOuterRadius);
        }
        Transform inner = play.Find("Inner");
        if (inner != null && inner.TryGetComponent(out Image innerImage))
        {
            innerImage.color = new Color(0.035f, 0.10f, 0.18f, 0.97f);
            WDHomeUIFactory.ApplyRounded(innerImage, palette.PlayInnerRadius);
        }
        Transform swords = inner?.Find("CrossedSwords");
        if (swords != null && swords.TryGetComponent(out RawImage swordImage))
            swordImage.color = palette.Gold;
        Transform label = inner?.Find("PlayLabel");
        if (label != null && label.TryGetComponent(out TextMeshProUGUI text))
            text.color = new Color(1f, 0.96f, 0.84f, 1f);
        foreach (Shadow shadow in play.GetComponents<Shadow>())
            shadow.effectColor = new Color(palette.Gold.r, palette.Gold.g, palette.Gold.b, 0.30f);
    }

    private static void AddHighlight(RectTransform target, Color color, float height)
    {
        if (target == null)
            return;
        Transform existing = target.Find("WDVisualABHighlight");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            if (existing is RectTransform existingRect)
                existingRect.sizeDelta = new Vector2(0f, height);
            if (existing.TryGetComponent(out Image existingImage))
                existingImage.color = color;
            return;
        }
        RectTransform line = WDHomeUIFactory.Rect("WDVisualABHighlight", target);
        line.anchorMin = new Vector2(0.04f, 1f);
        line.anchorMax = new Vector2(0.96f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.anchoredPosition = new Vector2(0f, -2f);
        line.sizeDelta = new Vector2(0f, height);
        Image image = line.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, 2);
        line.SetAsLastSibling();
    }

    private void RemoveGeneratedHighlights()
    {
        foreach (Transform child in homeContent.GetComponentsInChildren<Transform>(true))
            if (child != null && child.name == "WDVisualABHighlight")
                child.gameObject.SetActive(false);
    }

    private static IEnumerable<Transform> FindAll(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) yield return child;
    }

    private static Transform FindFirst(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }
}

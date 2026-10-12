using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "WDArmoryV2VisualSettings", menuName = "War Dominion/UI/Armory V2 Visual Settings")]
public sealed class WDArmoryV2VisualSettings : ScriptableObject
{
    [Serializable]
    public sealed class ArtworkOverride
    {
        [SerializeField] private string weaponId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite sprite;
        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public Sprite Sprite { get => sprite; set => sprite = value; }
        public void SetIdentity(string id, string name) { weaponId = id; displayName = name; }
    }

    // ============================================================
    // 01. SUPERFÍCIES E ESTADOS DO ARSENAL V2
    // ============================================================
    [Header("Painéis e botões")]
    public Color panelColor = new(9f / 255f, 14f / 255f, 19f / 255f, .91f);
    public Color buttonColor = new(18f / 255f, 26f / 255f, 36f / 255f, .85f);
    public Color buttonPrimaryColor = new(197f / 255f, 155f / 255f, 99f / 255f, 1f);

    [Header("Slots")]
    public Color mainSlotColor = new(18f / 255f, 26f / 255f, 36f / 255f, .85f);
    public Color supportSlotColor = new(18f / 255f, 26f / 255f, 36f / 255f, .85f);
    public Color slotBorderColor = new(0f, 0f, 0f, .5f);
    public Color selectedColor = new(13f / 255f, 194f / 255f, 219f / 255f, 1f);

    [Header("Cards do catálogo")]
    public Color catalogCardColor = new(18f / 255f, 26f / 255f, 36f / 255f, .96f);
    public Color catalogBorderColor = new(68f / 255f, 83f / 255f, 94f / 255f, .70f);
    public Color equippedBorderColor = new(197f / 255f, 155f / 255f, 99f / 255f, .72f);
    public Color disabledColor = new(100f / 255f, 108f / 255f, 114f / 255f, .70f);

    // ============================================================
    // 02. TIPOGRAFIA E GEOMETRIA DECLARATIVA
    // ============================================================
    [Header("Tipografia")]
    public TMP_FontAsset titleFont;
    public TMP_FontAsset functionalFont;
    [Range(18f, 48f)] public float screenTitleSize = 34f;
    [Range(18f, 40f)] public float detailTitleSize = 30f;
    [Range(9f, 20f)] public float slotNameSize = 13f;
    public bool detailTitleWordWrap = true;
    public TextOverflowModes detailTitleOverflow = TextOverflowModes.Ellipsis;
    public bool detailTitleAutoSizing = true;

    [Header("Dimensões e espaçamentos")]
    public Vector2 mainSlotSize = new(124f, 184f);
    public Vector2 mainSlotSpacing = new(7.33f, 8f);
    public Vector2 supportSlotSize = new(124f, 184f);
    public Vector2 supportSlotSpacing = new(7.33f, 0f);
    public Vector2 catalogCardSize = new(161f, 184f);
    public Vector2 catalogSpacing = new(10f, 12f);

    // ============================================================
    // 03. ARTES CANÔNICAS COM OVERRIDE EDITORIAL OPCIONAL
    // ============================================================
    [SerializeField] private List<ArtworkOverride> artworkOverrides = new();
    public IReadOnlyList<ArtworkOverride> ArtworkOverrides => artworkOverrides;

    public Texture ResolveArtwork(string weaponId, Texture fallback)
    {
        ArtworkOverride entry = artworkOverrides.Find(item => item.WeaponId == weaponId);
        return entry?.Sprite != null ? entry.Sprite.texture : fallback;
    }

    public void SynchronizeArtworkEntries(WDArmoryCatalogConfig catalog)
    {
        if (catalog == null) return;
        foreach (WDWeaponDefinition weapon in catalog.Weapons)
        {
            ArtworkOverride entry = artworkOverrides.Find(item => item.WeaponId == weapon.Id);
            if (entry == null) { entry = new ArtworkOverride(); artworkOverrides.Add(entry); }
            entry.SetIdentity(weapon.Id, weapon.DisplayName);
        }
        artworkOverrides.RemoveAll(item => catalog.FindWeapon(item.WeaponId) == null);
    }

    public void RestoreOfficialValues()
    {
        panelColor = new Color(9f / 255f, 14f / 255f, 19f / 255f, .91f);
        buttonColor = new Color(18f / 255f, 26f / 255f, 36f / 255f, .85f);
        buttonPrimaryColor = new Color(197f / 255f, 155f / 255f, 99f / 255f, 1f);
        mainSlotColor = new Color(18f / 255f, 26f / 255f, 36f / 255f, .85f);
        supportSlotColor = new Color(18f / 255f, 26f / 255f, 36f / 255f, .85f);
        slotBorderColor = new Color(0f, 0f, 0f, .5f);
        selectedColor = new Color(13f / 255f, 194f / 255f, 219f / 255f, 1f);
        catalogCardColor = new Color(18f / 255f, 26f / 255f, 36f / 255f, .96f);
        catalogBorderColor = new Color(68f / 255f, 83f / 255f, 94f / 255f, .70f);
        equippedBorderColor = new Color(197f / 255f, 155f / 255f, 99f / 255f, .72f);
        disabledColor = new Color(100f / 255f, 108f / 255f, 114f / 255f, .70f);
        screenTitleSize = 34f; detailTitleSize = 30f; slotNameSize = 13f;
        detailTitleWordWrap = true; detailTitleOverflow = TextOverflowModes.Ellipsis; detailTitleAutoSizing = true;
        // 03.1 — A restauração oficial preserva o reequilíbrio dimensional aprovado da V2.
        mainSlotSize = supportSlotSize = new Vector2(124f, 184f);
        mainSlotSpacing = new Vector2(7.33f, 8f); supportSlotSpacing = new Vector2(7.33f, 0f);
        catalogCardSize = new Vector2(161f, 184f); catalogSpacing = new Vector2(10f, 12f);
        foreach (ArtworkOverride entry in artworkOverrides) entry.Sprite = null;
    }
}

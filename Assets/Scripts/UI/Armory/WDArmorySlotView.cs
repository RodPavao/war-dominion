using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WDArmorySlotView : MonoBehaviour
{
    // ============================================================
    // 01. REFERÊNCIAS SERIALIZADAS
    // ============================================================

    [SerializeField] private Button selectionButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private RawImage artwork;
    [SerializeField] private Image artworkFrame;
    [SerializeField] private AspectRatioFitter artworkAspect;
    [SerializeField] private TMP_Text weaponName;
    [SerializeField] private TMP_Text energy;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private TMP_Text emptyHint;
    [SerializeField] private TMP_Text emptyPlus;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private Graphic occupiedBorder;
    [SerializeField] private Image surface;
    [SerializeField] private Outline selectionOutline;

    private Action selected;
    private Action removed;
    private WDArmoryV2VisualSettings visualSettings;
    private bool supportSlot;

    // ============================================================
    // 02. API DE APRESENTAÇÃO
    // ============================================================

    public void Bind(Action onSelected, Action onRemoved)
    {
        selected = onSelected;
        removed = onRemoved;
        selectionButton.onClick.RemoveListener(Select);
        selectionButton.onClick.AddListener(Select);
        removeButton.onClick.RemoveListener(Remove);
        removeButton.onClick.AddListener(Remove);
        TMP_Text symbol = removeButton.GetComponentInChildren<TMP_Text>(true);
        if (symbol != null) symbol.fontSize = 13f;
    }

    public void SetEmpty(bool support, bool selected)
    {
        // 02.1 — Estado vazio independente evita compartilhar textos com armas equipadas.
        if (emptyState != null) emptyState.SetActive(true);
        if (occupiedBorder != null) occupiedBorder.gameObject.SetActive(false);
        if (selectionOutline != null) selectionOutline.enabled = false;
        if (artwork != null) artwork.gameObject.SetActive(false);
        if (artworkFrame != null) artworkFrame.gameObject.SetActive(false);
        if (weaponName != null && weaponName != emptyLabel) weaponName.gameObject.SetActive(false);
        if (energy != null && energy != emptyPlus) energy.gameObject.SetActive(false);
        removeButton.gameObject.SetActive(false);
        if (emptyLabel != null) emptyLabel.gameObject.SetActive(true);
        if (emptyHint != null) emptyHint.gameObject.SetActive(false);
        if (emptyPlus != null) emptyPlus.gameObject.SetActive(true);
        // 02.2 — O estado vazio fixa tipografia e composição em runtime para
        // impedir que serializações/editoriais antigas ampliem o rótulo.
        ConfigureEmptyText(emptyLabel, "LIVRE", 12f, new Color32(214, 220, 225, 255), -10f, new Vector2(108f, 20f));
        ConfigureEmptyText(emptyPlus, "+", 16f, new Color32(197, 155, 99, 255), 11f, new Vector2(30f, 20f));
        WDDashedRoundedBorderGraphic dashed = emptyState != null
            ? emptyState.GetComponent<WDDashedRoundedBorderGraphic>()
            : null;
        if (dashed != null)
        {
            dashed.enabled = true;
            dashed.Configure(1.5f, 5f, 4f, 10f, new Color32(197, 155, 99, 166));
        }
        ApplySelection(selected);
    }

    private static void ConfigureEmptyText(TMP_Text target, string value, float size, Color color,
        float y, Vector2 dimensions)
    {
        if (target == null) return;
        target.text = value;
        target.enableAutoSizing = false;
        target.fontSize = size;
        target.fontSizeMin = size;
        target.fontSizeMax = size;
        target.color = color;
        target.alignment = TextAlignmentOptions.Center;
        target.rectTransform.localScale = Vector3.one;
        target.rectTransform.anchoredPosition = new Vector2(0f, y);
        target.rectTransform.sizeDelta = dimensions;
    }

    public void SetWeapon(string displayName, int energyCost, Texture texture, bool selected) =>
        SetWeapon(displayName, energyCost, texture, string.Empty, selected);

    public void SetWeapon(string displayName, int energyCost, Texture texture, string category, bool selected)
    {
        if (emptyState != null) emptyState.SetActive(false);
        if (occupiedBorder != null) occupiedBorder.gameObject.SetActive(true);
        if (selectionOutline != null) selectionOutline.enabled = true;
        if (artwork != null) artwork.gameObject.SetActive(true);
        if (artworkFrame != null) artworkFrame.gameObject.SetActive(true);
        if (weaponName != null) weaponName.gameObject.SetActive(true);
        if (energy != null) energy.gameObject.SetActive(true);
        removeButton.gameObject.SetActive(true);
        if (emptyLabel != null && emptyLabel != weaponName) emptyLabel.gameObject.SetActive(false);
        if (emptyHint != null) emptyHint.gameObject.SetActive(false);
        if (emptyPlus != null && emptyPlus != energy) emptyPlus.gameObject.SetActive(false);
        if (artwork != null) artwork.texture = texture;
        if (artworkAspect != null) artworkAspect.aspectRatio = texture != null && texture.height > 0
            ? (float)texture.width / texture.height : 1f;
        if (weaponName != null)
        {
            weaponName.text = (displayName ?? string.Empty).ToUpperInvariant();
            weaponName.color = Color.white;
        }
        if (energy != null) energy.text = $"ϟ {energyCost}";
        if (occupiedBorder != null) occupiedBorder.color = CategoryColor(category);
        ApplySelection(selected);
    }

    // ============================================================
    // 03. IDENTIDADE DE CATEGORIA SEM ALTERAR A SUPERFÍCIE
    // ============================================================
    private static Color CategoryColor(string category)
    {
        string value = (category ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("support") || value.Contains("apoio")) return new Color32(34, 184, 125, 255);
        if (value.Contains("tactical") || value.Contains("tática") || value.Contains("tatica")) return new Color32(25, 187, 212, 255);
        if (value.Contains("strategic") || value.Contains("estratégica") || value.Contains("estrategica")) return new Color32(183, 138, 69, 255);
        if (value.Contains("special") || value.Contains("especial")) return new Color32(168, 120, 219, 255);
        return new Color32(255, 255, 255, 18);
    }

    private void ApplySelection(bool isSelected)
    {
        // ============================================================
        // 04. SELEÇÃO FUNCIONAL SEM SOBRESCRITA VISUAL
        // ============================================================
        // A seleção continua pertencendo ao fluxo funcional do Presenter. Fundo
        // e Outline permanecem exatamente como serializados no prefab V2.
    }

    private void Select() => selected?.Invoke();
    private void Remove() => removed?.Invoke();

    public void ApplyVisualSettings(WDArmoryV2VisualSettings settings, bool support)
    {
        // ============================================================
        // 05. SETTINGS DE ESTADO SEM SOBRESCRITA TIPOGRÁFICA
        // ============================================================
        // O prefab determina fonte, peso e tamanho de labels ocupados e vazios.
        visualSettings = settings;
        supportSlot = support;
    }
}

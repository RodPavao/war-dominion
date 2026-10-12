using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Apresentação isolada de uma arma do Arsenal. Recebe um view model pronto e
/// não conhece catálogo, repositório, perfil ou regras de equipagem.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class WDArmoryWeaponCardView : MonoBehaviour
{
    public enum VisualState
    {
        Available,
        Selected,
        Equipped,
        Locked
    }

    public readonly struct ViewData
    {
        public ViewData(string weaponId, string displayName, string category, int level, int energyCost,
            int tokens, int requiredTokens, VisualState state)
        {
            WeaponId = weaponId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Level = Mathf.Max(0, level);
            EnergyCost = Mathf.Max(0, energyCost);
            Tokens = Mathf.Max(0, tokens);
            RequiredTokens = Mathf.Max(0, requiredTokens);
            State = state;
        }

        public string WeaponId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Level { get; }
        public int EnergyCost { get; }
        public int Tokens { get; }
        public int RequiredTokens { get; }
        public VisualState State { get; }
    }

    // ============================================================
    // 01. REFERÊNCIAS SERIALIZADAS DO PREFAB
    // ============================================================

    [Header("Theme")]
    [SerializeField] private WarDominionUITheme theme;

    [Header("Interaction")]
    [SerializeField] private Button selectionButton;
    [SerializeField] private Button equipButton;

    [Header("Graphics")]
    [SerializeField] private Image surface;
    [SerializeField] private Image selectionBorder;
    [SerializeField] private Outline selectionOutline;
    [SerializeField] private RawImage artwork;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Image statePill;

    [Header("Typography")]
    [SerializeField] private TMP_Text weaponName;
    [SerializeField] private TMP_Text categoryAndLevel;
    [SerializeField] private TMP_Text energyLabel;
    [SerializeField] private TMP_Text tokenLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private Image tokenFill;

    private string weaponId = string.Empty;
    private string category = string.Empty;
    private Action<string> selected;
    private Action<string> equipped;
    private WDArmoryV2VisualSettings visualSettings;

    // ============================================================
    // 02. CICLO DE VIDA DA INTERAÇÃO
    // ============================================================

    private void Awake()
    {
        if (selectionButton == null) selectionButton = GetComponent<Button>();
        selectionButton.onClick.AddListener(HandleSelected);
        if (equipButton != null)
        {
            equipButton.onClick.AddListener(HandleEquipped);
            TMP_Text symbol = equipButton.GetComponentInChildren<TMP_Text>(true);
            if (symbol != null) symbol.fontSize = 14f;
        }
    }

    private void OnDestroy()
    {
        if (selectionButton != null) selectionButton.onClick.RemoveListener(HandleSelected);
        if (equipButton != null) equipButton.onClick.RemoveListener(HandleEquipped);
    }

    // ============================================================
    // 03. API DE APRESENTAÇÃO SEM DEPENDÊNCIA DE DOMÍNIO
    // ============================================================

    public void SetData(ViewData data, Texture artworkTexture, Action<string> onSelected = null,
        Action<string> onEquipped = null)
    {
        weaponId = data.WeaponId;
        category = data.Category;
        selected = onSelected;
        equipped = onEquipped;

        if (weaponName != null)
        {
            weaponName.text = data.DisplayName.ToUpperInvariant();
            weaponName.color = Color.white;
        }
        if (categoryAndLevel != null)
        {
            categoryAndLevel.text = data.Category.ToUpperInvariant();
            categoryAndLevel.overflowMode = TextOverflowModes.Overflow;
        }
        // ============================================================
        // 03.1 — ÍCONE OFICIAL DE ENERGIA SERIALIZADO NO CARD
        // ============================================================
        // O símbolo visual é um Sprite oficial do VisualKit. O TMP mantém
        // somente o valor funcional e não depende de glifo Unicode.
        if (energyLabel != null) energyLabel.text = data.EnergyCost > 0 ? data.EnergyCost.ToString() : "—";
        if (tokenLabel != null)
        {
            bool hasProgress = data.RequiredTokens > 0;
            tokenLabel.text = data.RequiredTokens > 0
                ? $"N{data.Level} · {data.Tokens}/{data.RequiredTokens}"
                : data.Level > 0 ? $"N{data.Level}" : "—";

            // ============================================================
            // 03.2 — NÍVEL ÚNICO SEGUE A ESCALA DO INDICADOR COMPLETO
            // ============================================================
            // O auto sizing expandia o texto curto N1 até o máximo do TMP,
            // enquanto indicadores com tokens eram reduzidos pelo espaço.
            tokenLabel.enableAutoSizing = hasProgress;
            tokenLabel.fontSizeMin = 7f;
            tokenLabel.fontSizeMax = 10f;
            if (!hasProgress) tokenLabel.fontSize = 7f;
        }
        if (tokenFill != null) tokenFill.rectTransform.anchorMax = new Vector2(
            data.RequiredTokens > 0 ? Mathf.Clamp01((float)data.Tokens / data.RequiredTokens) : 0f, 1f);
        if (artwork != null) artwork.texture = artworkTexture;
        if (equipButton != null)
        {
            bool canEquip = data.State == VisualState.Available || data.State == VisualState.Selected;
            equipButton.gameObject.SetActive(canEquip);
            equipButton.interactable = canEquip;
        }

        ApplyState(data.State);
    }

    public void ApplyState(VisualState state)
    {
        Color gold = visualSettings != null ? visualSettings.equippedBorderColor : theme != null ? theme.MasterGold : new Color32(197, 155, 99, 255);
        Color cyan = visualSettings != null ? visualSettings.selectedColor : theme != null ? theme.MasterCyan : new Color32(13, 194, 219, 255);
        Color disabled = visualSettings != null ? visualSettings.disabledColor : theme != null ? theme.MasterDisabled : new Color32(100, 108, 114, 180);

        // ============================================================
        // 04. SUPERFÍCIE E OUTLINE SERIALIZADOS SÃO A AUTORIDADE
        // ============================================================
        // Estados funcionais não reatribuem surface.color, effectColor ou
        // effectDistance. O prefab V2 preserva integralmente esse acabamento.
        if (selectionBorder != null) selectionBorder.color = CategoryColor(category);

        // ============================================================
        // 05. BLOQUEIO COM OS COMPONENTES EXISTENTES DO PREFAB
        // ============================================================
        if (lockOverlay != null) lockOverlay.SetActive(state == VisualState.Locked);
        if (artwork != null) artwork.color = state == VisualState.Locked ? new Color(1f, 1f, 1f, 0.38f) : Color.white;
        selectionButton.interactable = state != VisualState.Locked;

        bool showState = state != VisualState.Available;
        if (statePill != null) statePill.gameObject.SetActive(showState);
        if (stateLabel != null) stateLabel.gameObject.SetActive(showState);
        if (!showState) return;

        if (stateLabel != null) stateLabel.text = state switch
        {
            VisualState.Selected => "SELECIONADA",
            VisualState.Equipped => "EQUIPADA",
            VisualState.Locked => "BLOQUEADA",
            _ => string.Empty
        };
        if (statePill != null) statePill.color = state == VisualState.Selected ? new Color(cyan.r, cyan.g, cyan.b, 0.18f)
            : state == VisualState.Equipped ? new Color(gold.r, gold.g, gold.b, 0.18f)
            : new Color(disabled.r, disabled.g, disabled.b, 0.22f);
        if (stateLabel != null) stateLabel.color = state == VisualState.Selected ? cyan
            : state == VisualState.Equipped ? gold : disabled;
    }

    public void ApplyVisualSettings(WDArmoryV2VisualSettings settings)
    {
        // ============================================================
        // 06. SETTINGS DE ESTADO SEM SOBRESCRITA TIPOGRÁFICA
        // ============================================================
        // Nome, peso e tamanho permanecem serializados no prefab do card.
        visualSettings = settings;
    }

    // ============================================================
    // 07. IDENTIDADE DE CATEGORIA NA BORDA ESTRUTURAL
    // ============================================================
    private static Color CategoryColor(string categoryName)
    {
        string value = (categoryName ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("support") || value.Contains("apoio")) return new Color32(34, 184, 125, 255);
        if (value.Contains("tactical") || value.Contains("tática") || value.Contains("tatica")) return new Color32(25, 187, 212, 255);
        if (value.Contains("strategic") || value.Contains("estratégica") || value.Contains("estrategica")) return new Color32(183, 138, 69, 255);
        if (value.Contains("special") || value.Contains("especial")) return new Color32(168, 120, 219, 255);
        return new Color32(255, 255, 255, 18);
    }

    private void HandleSelected()
    {
        selected?.Invoke(weaponId);
    }

    private void HandleEquipped()
    {
        // ============================================================
        // 08. AÇÃO DIRETA REUTILIZA O FLUXO CANÔNICO DO PRESENTER
        // ============================================================
        equipped?.Invoke(weaponId);
    }
}

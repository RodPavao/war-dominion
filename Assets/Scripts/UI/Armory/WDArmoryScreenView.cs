using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WDArmoryScreenView : MonoBehaviour
{
    // ============================================================
    // 01. SHELL E DECK
    // ============================================================

    [Header("Shell")]
    [SerializeField] private RectTransform referenceFrame;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text coins;
    [SerializeField] private TMP_Text gems;
    [SerializeField] private RectTransform header;
    [SerializeField] private RectTransform deckPanel;
    [SerializeField] private RectTransform catalogPanel;
    [SerializeField] private RectTransform detailsPanel;

    [Header("Deck")]
    [SerializeField] private TMP_InputField deckNameInput;
    [SerializeField] private Button deckNameEditButton;
    [SerializeField] private TMP_Text deckMeta;
    [SerializeField] private TMP_Text mainMeta;
    [SerializeField] private TMP_Text supportMeta;
    [SerializeField] private TMP_Text feedback;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button[] deckButtons = new Button[5];
    [SerializeField] private TMP_Text[] deckLabels = new TMP_Text[5];
    [SerializeField] private Image[] deckLockIcons = new Image[5];
    [SerializeField] private WDArmorySlotView[] mainSlots = new WDArmorySlotView[8];
    [SerializeField] private WDArmorySlotView[] supportSlots = new WDArmorySlotView[2];

    // ============================================================
    // 02. CATÁLOGO E DETALHES
    // ============================================================

    [Header("Catalog")]
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private Button[] filterButtons = new Button[5];
    [SerializeField] private TMP_Text[] filterLabels = new TMP_Text[5];
    [SerializeField] private WDArmoryWeaponCardView[] catalogCards = new WDArmoryWeaponCardView[26];

    [Header("Details")]
    [SerializeField] private RawImage detailArtwork;
    [SerializeField] private TMP_Text detailCategory;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailFacts;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailEvolution;
    [SerializeField] private TMP_Text detailTokens;
    [SerializeField] private TMP_Text detailMissing;
    [SerializeField] private Image detailTokenFill;
    [SerializeField] private TMP_Text detailBenefit;
    [SerializeField] private TMP_Text detailUpgradeCost;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button addButton;
    [SerializeField] private TMP_Text detailHint;

    // ============================================================
    public Button BackButton => backButton;
    public RectTransform ReferenceFrame => referenceFrame;
    public TMP_Text Coins => coins;
    public TMP_Text Gems => gems;
    public RectTransform Header => header;
    public RectTransform DeckPanel => deckPanel;
    public RectTransform CatalogPanel => catalogPanel;
    public RectTransform DetailsPanel => detailsPanel;
    public TMP_InputField DeckNameInput => deckNameInput;
    public Button DeckNameEditButton => deckNameEditButton;
    public TMP_Text DeckMeta => deckMeta;
    public TMP_Text MainMeta => mainMeta;
    public TMP_Text SupportMeta => supportMeta;
    public TMP_Text Feedback => feedback;
    public Button SaveButton => saveButton;
    public Button[] DeckButtons => deckButtons;
    public TMP_Text[] DeckLabels => deckLabels;
    public Image[] DeckLockIcons => deckLockIcons;
    public WDArmorySlotView[] MainSlots => mainSlots;
    public WDArmorySlotView[] SupportSlots => supportSlots;
    public TMP_InputField SearchField => searchField;
    public Button[] FilterButtons => filterButtons;
    public TMP_Text[] FilterLabels => filterLabels;
    public WDArmoryWeaponCardView[] CatalogCards => catalogCards;
    public RawImage DetailArtwork => detailArtwork;
    public TMP_Text DetailCategory => detailCategory;
    public TMP_Text DetailName => detailName;
    public TMP_Text DetailFacts => detailFacts;
    public TMP_Text DetailDescription => detailDescription;
    public TMP_Text DetailEvolution => detailEvolution;
    public TMP_Text DetailTokens => detailTokens;
    public TMP_Text DetailMissing => detailMissing;
    public Image DetailTokenFill => detailTokenFill;
    public TMP_Text DetailBenefit => detailBenefit;
    public TMP_Text DetailUpgradeCost => detailUpgradeCost;
    public Button UpgradeButton => upgradeButton;
    public Button AddButton => addButton;
    public TMP_Text DetailHint => detailHint;

    protected virtual void Awake() => ApplyResponsiveLayout();

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) ApplyResponsiveLayout();
    }

    // ============================================================
    // 03. REGRA RESPONSIVA ÚNICA DO SHELL 3095:432
    // ============================================================

    public virtual void ApplyResponsiveLayout()
    {
        RectTransform root = transform as RectTransform;
        header ??= transform.Find("Header") as RectTransform;
        deckPanel ??= transform.Find("DeckPanel") as RectTransform;
        catalogPanel ??= transform.Find("CatalogPanel") as RectTransform;
        detailsPanel ??= transform.Find("DetailsPanel") as RectTransform;
        if (root == null || header == null || deckPanel == null || catalogPanel == null || detailsPanel == null)
            return;

        float width = root.rect.width;
        float height = root.rect.height;
        if (width <= 0f || height <= 0f) return;

        float outer = Mathf.Clamp(width * (32f / 1920f), 24f, 48f);
        float gap = Mathf.Clamp(width * (16f / 1920f), 12f, 24f);
        float usable = Mathf.Max(1200f, width - outer * 2f);
        float compression = Mathf.Clamp01((usable - 1200f) / (1856f - 1200f));
        float deckWidth = Mathf.Lerp(360f, 470f, compression);
        float detailsWidth = Mathf.Lerp(420f, 544f, compression);
        float catalogWidth = Mathf.Max(420f, usable - deckWidth - detailsWidth - gap * 2f);
        float top = Mathf.Clamp(height * (112f / 1080f), 88f, 128f);
        float panelHeight = Mathf.Max(560f, height - top - Mathf.Clamp(height * (24f / 1080f), 16f, 32f));

        SetTopLeft(header, outer, Mathf.Clamp(height * (24f / 1080f), 16f, 32f), width - outer * 2f, 72f);
        SetTopLeft(deckPanel, outer, top, deckWidth, panelHeight);
        SetTopLeft(catalogPanel, outer + deckWidth + gap, top, catalogWidth, panelHeight);
        SetTopLeft(detailsPanel, width - outer - detailsWidth, top, detailsWidth, panelHeight);
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeArmoryPanel : MonoBehaviour
{
    // ============================================================
    // 01. ESTADO E DEPENDÊNCIAS DA TELA FUNCIONAL
    // ============================================================

    private WarDominionUITheme theme;
    private WDArmoryService service;
    private IWDArmoryRepository repository;
    private Action lockedDeckAction;
    private readonly WDArmoryVipNavigation vipNavigation = new();
    private RectTransform deckTabs;
    private RectTransform mainSlots;
    private RectTransform supportSlots;
    private RectTransform collectionContent;
    private TextMeshProUGUI currencyLabel;
    private TextMeshProUGUI statusLabel;
    private TMP_InputField deckNameInput;
    private WDUIPremiumButton vipButton;
    private int selectedDeckIndex;
    private bool editingSupport;
    private int selectedSlot;

    public bool IsOpen => gameObject.activeSelf;
    public WDArmoryVipNavigation VipNavigation => vipNavigation;

    public void Build(
        WarDominionUITheme newTheme, WDArmoryCatalogConfig catalog,
        IWDArmoryRepository newRepository, Action onLockedDeck)
    {
        theme = newTheme;
        repository = newRepository;
        lockedDeckAction = onLockedDeck;
        WDArmoryProfile profile = repository.Load(catalog);
        service = new WDArmoryService(catalog, profile);
        BuildSurface();
        gameObject.SetActive(false);
    }

    public void Open()
    {
        vipNavigation.ReturnToArmory();
        gameObject.SetActive(true);
        selectedDeckIndex = Mathf.Clamp(selectedDeckIndex, 0, WDArmoryProfile.SavedDeckCount - 1);
        RebuildAll();
    }

    public void Close()
    {
        Persist();
        gameObject.SetActive(false);
    }

    public void HandleEscape() => Close();

    public void CloseVipRequirementModal() => vipNavigation.CloseRequirementModal();

    public void NavigateToStoreVip()
    {
        vipNavigation.NavigateToStoreVip();
        Close();
    }

    // ============================================================
    // 02. COMPOSIÇÃO RESPONSIVA E MOBILE-FIRST
    // ============================================================

    private void BuildSurface()
    {
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.022f, 0.97f);
        blocker.raycastTarget = true;

        RectTransform panel = WDHomeUIFactory.Rect("ArmoryPanel", transform);
        WDHomeUIFactory.Stretch(panel, 34f, 26f, 34f, 26f);
        Image surface = panel.gameObject.AddComponent<Image>();
        surface.color = theme.BackgroundElevated;

        TextMeshProUGUI title = WDHomeUIFactory.Text(
            "Title", panel, theme, "ARMAS", theme.TypeDisplay,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        SetAnchored(title.rectTransform, new Vector2(0f, 0.91f), new Vector2(0.5f, 1f), 24f, 8f, -8f, -8f);

        currencyLabel = WDHomeUIFactory.Text(
            "Currencies", panel, theme, string.Empty, theme.TypeBody,
            theme.TextoSecundario, TextAlignmentOptions.Right);
        SetAnchored(currencyLabel.rectTransform, new Vector2(0.48f, 0.92f), new Vector2(0.84f, 1f), 0f, 8f, -8f, -8f);

        RectTransform vipRect = WDHomeUIFactory.Rect("DevelopmentVip", panel);
        SetAnchored(vipRect, new Vector2(0.84f, 0.92f), new Vector2(0.95f, 0.99f), 4f, 0f, -4f, 0f);
        vipButton = vipRect.gameObject.AddComponent<WDUIPremiumButton>();
        vipButton.Build(theme, string.Empty, ToggleDevelopmentVip);

        RectTransform closeRect = WDHomeUIFactory.Rect("Close", panel);
        SetAnchored(closeRect, new Vector2(0.95f, 0.92f), Vector2.one, 5f, 0f, -5f, 0f);
        closeRect.gameObject.AddComponent<WDUIPremiumButton>().Build(theme, "X", Close);

        deckTabs = WDHomeUIFactory.Rect("DeckTabs", panel);
        SetAnchored(deckTabs, new Vector2(0f, 0.82f), new Vector2(1f, 0.91f), 22f, 2f, -22f, -2f);
        HorizontalLayoutGroup tabsLayout = deckTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 10f;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = true;

        RectTransform composition = CreateSection("DeckComposition", panel, new Vector2(0f, 0.08f), new Vector2(0.56f, 0.81f));
        BuildComposition(composition);

        RectTransform collection = CreateSection("Collection", panel, new Vector2(0.565f, 0.08f), new Vector2(1f, 0.81f));
        BuildCollection(collection);

        statusLabel = WDHomeUIFactory.Text(
            "Status", panel, theme, string.Empty, theme.TypeMicro,
            theme.TextoSecundario, TextAlignmentOptions.Left);
        SetAnchored(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.075f), 24f, 4f, -24f, -4f);
    }

    private void BuildComposition(RectTransform parent)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(
            "SectionTitle", parent, theme, "DECK SELECIONADO", theme.TypeSection,
            theme.Acento, TextAlignmentOptions.Left);
        SetAnchored(label.rectTransform, new Vector2(0f, 0.9f), Vector2.one, 18f, 0f, -18f, 0f);

        deckNameInput = CreateDeckNameInput(parent);
        SetAnchored(deckNameInput.GetComponent<RectTransform>(), new Vector2(0f, 0.8f), new Vector2(1f, 0.9f), 18f, 4f, -18f, -4f);

        TextMeshProUGUI mainTitle = WDHomeUIFactory.Text(
            "MainTitle", parent, theme, "8 ARMAS PRINCIPAIS", theme.TypeBody,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        SetAnchored(mainTitle.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.8f), 18f, 0f, -18f, 0f);

        mainSlots = WDHomeUIFactory.Rect("MainSlots", parent);
        SetAnchored(mainSlots, new Vector2(0f, 0.36f), new Vector2(1f, 0.72f), 18f, 0f, -18f, 0f);
        GridLayoutGroup mainGrid = mainSlots.gameObject.AddComponent<GridLayoutGroup>();
        mainGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        mainGrid.constraintCount = 4;
        mainGrid.spacing = new Vector2(8f, 8f);
        mainGrid.cellSize = new Vector2(126f, 76f);

        TextMeshProUGUI supportTitle = WDHomeUIFactory.Text(
            "SupportTitle", parent, theme, "2 ARMAS DE APOIO · 1 USO CADA", theme.TypeBody,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        SetAnchored(supportTitle.rectTransform, new Vector2(0f, 0.27f), new Vector2(1f, 0.36f), 18f, 0f, -18f, 0f);

        supportSlots = WDHomeUIFactory.Rect("SupportSlots", parent);
        SetAnchored(supportSlots, new Vector2(0f, 0.08f), new Vector2(1f, 0.27f), 18f, 0f, -18f, 0f);
        HorizontalLayoutGroup supportLayout = supportSlots.gameObject.AddComponent<HorizontalLayoutGroup>();
        supportLayout.spacing = 10f;
        supportLayout.childControlWidth = true;
        supportLayout.childControlHeight = true;
        supportLayout.childForceExpandWidth = true;
        supportLayout.childForceExpandHeight = true;
    }

    private void BuildCollection(RectTransform parent)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(
            "SectionTitle", parent, theme, "COLEÇÃO", theme.TypeSection,
            theme.Acento, TextAlignmentOptions.Left);
        SetAnchored(label.rectTransform, new Vector2(0f, 0.9f), Vector2.one, 18f, 0f, -18f, 0f);

        RectTransform viewport = WDHomeUIFactory.Rect("Viewport", parent);
        SetAnchored(viewport, Vector2.zero, new Vector2(1f, 0.9f), 14f, 14f, -14f, -6f);
        Image maskImage = viewport.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.12f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        collectionContent = WDHomeUIFactory.Rect("Content", viewport);
        collectionContent.anchorMin = new Vector2(0f, 1f);
        collectionContent.anchorMax = new Vector2(1f, 1f);
        collectionContent.pivot = new Vector2(0.5f, 1f);
        collectionContent.offsetMin = Vector2.zero;
        collectionContent.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = collectionContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = collectionContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = parent.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = collectionContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    private TMP_InputField CreateDeckNameInput(Transform parent)
    {
        RectTransform rect = WDHomeUIFactory.Rect("DeckName", parent);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = theme.SurfaceGlass;
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        TextMeshProUGUI text = WDHomeUIFactory.Text(
            "Text", rect, theme, string.Empty, theme.TypeBody,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        WDHomeUIFactory.Stretch(text.rectTransform, 14f, 4f, 14f, 4f);
        input.textComponent = text;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 24;
        input.onEndEdit.AddListener(RenameCurrentDeck);
        return input;
    }

    // ============================================================
    // 03. RECONSTRUÇÃO DOS ESTADOS INTERATIVOS
    // ============================================================

    private void RebuildAll()
    {
        RebuildTabs();
        RebuildComposition();
        RebuildCollection();
        currencyLabel.text = $"MOEDAS {service.Profile.Coins}   TOKENS POR ARMA   ESMERALDAS {service.Profile.Emeralds}";
        vipButton.SetLabel(service.Profile.IsVip ? "VIP DEV: ON" : "VIP DEV: OFF");
    }

    private void RebuildTabs()
    {
        ClearChildren(deckTabs);
        for (int i = 0; i < WDArmoryProfile.SavedDeckCount; i++)
        {
            int deckIndex = i;
            bool unlocked = service.Profile.IsDeckUnlocked(i);
            WDSavedWeaponDeck deck = service.Profile.GetDeck(i);
            RectTransform rect = WDHomeUIFactory.Rect($"Deck_{i + 1}", deckTabs);
            WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
            button.Build(theme, unlocked ? deck.Name : $"DECK {i + 1} · BLOQUEADO", () => SelectDeck(deckIndex));
            button.SetSelected(i == selectedDeckIndex);
        }
    }

    private void RebuildComposition()
    {
        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        deckNameInput.text = deck?.Name ?? string.Empty;
        deckNameInput.interactable = service.Profile.IsDeckUnlocked(selectedDeckIndex);
        ClearChildren(mainSlots);
        ClearChildren(supportSlots);

        for (int i = 0; i < WDSavedWeaponDeck.MainSlotCount; i++)
            CreateSlot(mainSlots, false, i, deck?.GetMain(i));
        for (int i = 0; i < WDSavedWeaponDeck.SupportSlotCount; i++)
            CreateSlot(supportSlots, true, i, deck?.GetSupport(i));
    }

    private void CreateSlot(Transform parent, bool support, int slot, string weaponId)
    {
        RectTransform rect = WDHomeUIFactory.Rect($"{(support ? "Support" : "Main")}_{slot + 1}", parent);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        WDWeaponDefinition weapon = service.Catalog.FindWeapon(weaponId);
        string label = weapon != null ? $"{slot + 1}. {weapon.DisplayName}\nTOQUE PARA REMOVER" : $"{slot + 1}. VAZIO\nSELECIONAR";
        button.Build(theme, label, () => SelectOrRemoveSlot(support, slot, weaponId));
        button.SetSelected(editingSupport == support && selectedSlot == slot && string.IsNullOrEmpty(weaponId));
        button.SetInteractable(service.Profile.IsDeckUnlocked(selectedDeckIndex));
    }

    private void RebuildCollection()
    {
        ClearChildren(collectionContent);
        foreach (WDWeaponDefinition weapon in service.Catalog.Weapons)
            CreateWeaponRow(weapon);
    }

    private void CreateWeaponRow(WDWeaponDefinition weapon)
    {
        RectTransform row = WDHomeUIFactory.Rect(weapon.Id, collectionContent);
        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 92f;
        Image background = row.gameObject.AddComponent<Image>();
        background.color = theme.SurfaceGlass;

        WDWeaponProgress progress = service.Profile.GetProgress(weapon.Id);
        WDWeaponCollectionState state = service.GetCollectionState(weapon.Id);
        string category = service.Catalog.Categories.FirstOrDefault(item => item.Id == weapon.CategoryId)?.DisplayName ?? weapon.CategoryId;
        string progression = FormatProgress(weapon, progress, state);
        TextMeshProUGUI description = WDHomeUIFactory.Text(
            "Description", row, theme,
            $"{weapon.DisplayName}\n{category} · {progression}", theme.TypeMicro,
            state == WDWeaponCollectionState.NotObtained ? theme.TextoSecundario : theme.TextoPrincipal,
            TextAlignmentOptions.Left);
        SetAnchored(description.rectTransform, Vector2.zero, new Vector2(0.58f, 1f), 12f, 8f, -4f, -8f);

        RectTransform equipRect = WDHomeUIFactory.Rect("Equip", row);
        SetAnchored(equipRect, new Vector2(0.59f, 0.12f), new Vector2(0.79f, 0.88f), 2f, 0f, -2f, 0f);
        WDUIPremiumButton equip = equipRect.gameObject.AddComponent<WDUIPremiumButton>();
        equip.Build(theme, "USAR", () => AssignSelected(weapon.Id));
        equip.SetInteractable(progress != null && progress.Obtained && service.Profile.IsDeckUnlocked(selectedDeckIndex));

        RectTransform upgradeRect = WDHomeUIFactory.Rect("Upgrade", row);
        SetAnchored(upgradeRect, new Vector2(0.8f, 0.12f), new Vector2(0.99f, 0.88f), 2f, 0f, -2f, 0f);
        WDUIPremiumButton upgrade = upgradeRect.gameObject.AddComponent<WDUIPremiumButton>();
        upgrade.Build(theme, state == WDWeaponCollectionState.MaxLevel ? "MAX" : "UPGRADE", () => Upgrade(weapon.Id));
        upgrade.SetInteractable(state == WDWeaponCollectionState.UpgradeAvailable);
    }

    private static string FormatProgress(
        WDWeaponDefinition weapon, WDWeaponProgress progress,
        WDWeaponCollectionState state)
    {
        if (progress == null || !progress.Obtained)
            return "NÃO OBTIDA";
        if (state == WDWeaponCollectionState.MaxLevel)
            return $"NÍVEL {WDWeaponDefinition.MaxLevel} · MAX";
        WDWeaponLevelDefinition next = weapon.GetUpgradeToLevel(progress.Level + 1);
        string costs = next == null
            ? "CONFIGURAÇÃO PENDENTE"
            : $"{progress.Tokens}/{next.RequiredTokens} TOKENS · {next.CoinCost} MOEDAS";
        return $"NÍVEL {progress.Level} · {costs} · {FormatState(state)}";
    }

    private static string FormatState(WDWeaponCollectionState state) => state switch
    {
        WDWeaponCollectionState.NeedsCoins => "FALTAM MOEDAS",
        WDWeaponCollectionState.UpgradeAvailable => "UPGRADE DISPONÍVEL",
        _ => "EM PROGRESSÃO"
    };

    // ============================================================
    // 04. COMANDOS, VALIDAÇÃO E PERSISTÊNCIA
    // ============================================================

    private void SelectDeck(int deckIndex)
    {
        if (!service.Profile.IsDeckUnlocked(deckIndex))
        {
            vipNavigation.OpenRequirementModal();
            lockedDeckAction?.Invoke();
            return;
        }
        selectedDeckIndex = deckIndex;
        editingSupport = false;
        selectedSlot = 0;
        RebuildAll();
    }

    private void SelectOrRemoveSlot(bool support, int slot, string weaponId)
    {
        if (!string.IsNullOrEmpty(weaponId))
        {
            WDArmoryEditResult result = support
                ? service.RemoveSupport(selectedDeckIndex, slot)
                : service.RemoveMain(selectedDeckIndex, slot);
            ShowResult(result, "Arma removida.");
            PersistAndRefresh();
            return;
        }
        editingSupport = support;
        selectedSlot = slot;
        statusLabel.text = support
            ? $"Apoio {slot + 1} selecionado. Escolha uma arma elegível na coleção."
            : $"Slot principal {slot + 1} selecionado. Escolha uma arma obtida na coleção.";
        RebuildComposition();
    }

    private void AssignSelected(string weaponId)
    {
        WDArmoryEditResult result = editingSupport
            ? service.AssignSupport(selectedDeckIndex, selectedSlot, weaponId)
            : service.AssignMain(selectedDeckIndex, selectedSlot, weaponId);
        ShowResult(result, "Composição atualizada.");
        if (result == WDArmoryEditResult.Success)
            AdvanceSelection();
        PersistAndRefresh();
    }

    private void AdvanceSelection()
    {
        int limit = editingSupport ? WDSavedWeaponDeck.SupportSlotCount : WDSavedWeaponDeck.MainSlotCount;
        selectedSlot = Mathf.Min(selectedSlot + 1, limit - 1);
    }

    private void RenameCurrentDeck(string newName)
    {
        WDArmoryEditResult result = service.RenameDeck(selectedDeckIndex, newName);
        ShowResult(result, "Deck renomeado.");
        PersistAndRefresh();
    }

    private void Upgrade(string weaponId)
    {
        WDArmoryEditResult result = service.Upgrade(weaponId);
        ShowResult(result, "Upgrade realizado.");
        PersistAndRefresh();
    }

    private void ToggleDevelopmentVip()
    {
        service.SetVipForDevelopment(!service.Profile.IsVip);
        if (!service.Profile.IsDeckUnlocked(selectedDeckIndex))
            selectedDeckIndex = 0;
        statusLabel.text = service.Profile.IsVip
            ? "Estado VIP de desenvolvimento ativado: cinco decks disponíveis."
            : "Estado VIP de desenvolvimento desativado: decks 3–5 bloqueados sem apagar dados.";
        PersistAndRefresh();
    }

    private void ShowResult(WDArmoryEditResult result, string success)
    {
        statusLabel.text = result == WDArmoryEditResult.Success
            ? success
            : result switch
            {
                WDArmoryEditResult.LockedDeck => "Deck bloqueado para conta não-VIP.",
                WDArmoryEditResult.WeaponNotObtained => "A arma ainda não foi obtida.",
                WDArmoryEditResult.DuplicateWeapon => "A mesma arma não pode se repetir no deck.",
                WDArmoryEditResult.NotSupportEligible => "A categoria desta arma não é elegível para Apoio.",
                WDArmoryEditResult.InsufficientTokens => "Tokens insuficientes.",
                WDArmoryEditResult.InsufficientCoins => "Moedas insuficientes.",
                WDArmoryEditResult.AlreadyMaxLevel => "A arma já está no nível máximo.",
                _ => $"Operação indisponível: {result}."
            };
    }

    private void PersistAndRefresh()
    {
        Persist();
        RebuildAll();
    }

    private void Persist() => repository?.Save(service?.Profile);

    // ============================================================
    // 05. UTILITÁRIOS DE LAYOUT
    // ============================================================

    private RectTransform CreateSection(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        SetAnchored(rect, min, max, 8f, 8f, -8f, -8f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = theme.BackgroundPrimary;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.BorderNeutral;
        return rect;
    }

    private static void SetAnchored(
        RectTransform rect, Vector2 min, Vector2 max,
        float left, float bottom, float right, float top)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}

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
    // 01. AUTORIDADES FUNCIONAIS PRESERVADAS
    // ============================================================

    private const string V2ProviderPath = "UI/ArmoryV2/WDArmoryV2PrefabProvider";
    private static readonly string[] FilterIds = { "", "support", "tactical", "strategic", "special" };

    private WarDominionUITheme theme;
    private WDArmoryService service;
    private WDArmoryVisualCatalog visualCatalog;
    private WDArmoryV2VisualSettings visualSettings;
    private IWDArmoryRepository repository;
    private WDNavigationStack navigation;
    private Action<bool> homeVisibility;
    private Action lockedDeckAction;
    private readonly WDArmoryVipNavigation vipNavigation = new();

    private WDArmoryScreenView view;
    private string selectedWeaponId;
    private string categoryFilter;
    private string search = string.Empty;
    private int selectedDeckIndex;
    private int selectedMainSlot = -1;
    private int selectedSupportSlot = -1;
    private bool restorationNoticeShown;
    private bool deckNameEditing;
    private string deckNameBeforeEdit = string.Empty;
    private int deckNameEditDeckIndex = -1;

    public bool IsOpen => gameObject.activeSelf;
    public WDArmoryVipNavigation VipNavigation => vipNavigation;
    public WDArmoryService Service => service;
    public bool ShowingDecks => true;
    public string SelectedWeaponId => selectedWeaponId;
    public string CategoryFilter => categoryFilter;
    public int SelectedDeckIndex => selectedDeckIndex;

    // ============================================================
    // 02. INSTANCIAÇÃO ÚNICA DO PREFAB SERIALIZADO
    // ============================================================

    public void Build(WarDominionUITheme newTheme, WDArmoryCatalogConfig catalog,
        IWDArmoryRepository newRepository, Action onLockedDeck,
        WDNavigationStack navigationStack = null, Action<bool> homeCompositionVisibility = null)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        repository = newRepository ?? throw new ArgumentNullException(nameof(newRepository));
        navigation = navigationStack;
        homeVisibility = homeCompositionVisibility;
        lockedDeckAction = onLockedDeck;
        service = new WDArmoryService(catalog, repository.Load(catalog));
        visualCatalog = Resources.Load<WDArmoryVisualCatalog>("UI/Armory/WDArmoryVisualCatalog");
        visualSettings = Resources.Load<WDArmoryV2VisualSettings>("UI/ArmoryV2/WDArmoryV2VisualSettings");
        if (visualCatalog == null)
            throw new MissingReferenceException("Catálogo visual oficial do Arsenal não encontrado.");
        selectedWeaponId = catalog.Weapons.FirstOrDefault()?.Id;
        selectedDeckIndex = service.Profile.ActiveDeckIndex;

        gameObject.SetActive(false);
        ClearChildren(transform);

        WDArmoryV2PrefabProvider provider = Resources.Load<WDArmoryV2PrefabProvider>(V2ProviderPath);
        WDArmoryScreenView prefab = provider != null ? provider.ScreenPrefab : null;
        if (prefab == null)
            throw new MissingReferenceException($"Provider oficial do Arsenal V2 inválido em Resources/{V2ProviderPath}. Não há fallback para a interface antiga.");

        view = Instantiate(prefab, transform, false);
        RectTransform rect = view.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        (view as WDArmoryScreenV2View)?.ApplyVisualSettings(visualSettings);

        BindInteractions();
        RefreshAll();
    }

    private void BindInteractions()
    {
        view.BackButton.onClick.AddListener(Close);
        view.SaveButton.onClick.AddListener(SaveDeck);
        view.UpgradeButton.onClick.AddListener(UpgradeSelected);
        view.AddButton.onClick.AddListener(AddSelected);
        view.SearchField.onValueChanged.AddListener(OnSearchChanged);
        view.DeckNameInput.onEndEdit.AddListener(FinishDeckNameEdit);
        if (view.DeckNameEditButton != null)
            view.DeckNameEditButton.onClick.AddListener(BeginDeckNameEdit);

        for (int i = 0; i < view.DeckButtons.Length; i++)
        {
            int deck = i;
            view.DeckButtons[i].onClick.AddListener(() => SelectDeck(deck));
        }

        for (int i = 0; i < view.FilterButtons.Length; i++)
        {
            string filter = i < FilterIds.Length ? FilterIds[i] : string.Empty;
            view.FilterButtons[i].onClick.AddListener(() => SelectFilter(filter));
        }

        for (int i = 0; i < view.MainSlots.Length; i++)
        {
            int slot = i;
            view.MainSlots[i].Bind(() => SlotClicked(false, slot), () => RemoveSlot(false, slot));
        }

        for (int i = 0; i < view.SupportSlots.Length; i++)
        {
            int slot = i;
            view.SupportSlots[i].Bind(() => SlotClicked(true, slot), () => RemoveSlot(true, slot));
        }

    }

    // ============================================================
    // 03. NAVEGAÇÃO E LIFECYCLE
    // ============================================================

    public void Open()
    {
        service = new WDArmoryService(service.Catalog, repository.Load(service.Catalog));
        restorationNoticeShown = false;
        selectedDeckIndex = service.Profile.ActiveDeckIndex;
        if (navigation != null)
        {
            navigation.Push(new WDNavigationContext("armory", _ => Restore()));
            return;
        }
        Restore();
    }

    private void Restore()
    {
        homeVisibility?.Invoke(false);
        vipNavigation.ReturnToArmory();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        selectedMainSlot = -1;
        selectedSupportSlot = -1;
        RefreshAll();
    }

    public void Close()
    {
        if (navigation != null && navigation.CurrentId == "armory")
        {
            navigation.TryBack();
            return;
        }
        HideFromNavigation();
    }

    public void HideFromNavigation()
    {
        if (service?.Profile != null) repository?.Save(service.Profile);
        gameObject.SetActive(false);
        homeVisibility?.Invoke(true);
    }

    public void HandleEscape() => Close();
    public void CloseVipRequirementModal() => vipNavigation.CloseRequirementModal();
    public void NavigateToStoreVip() { vipNavigation.NavigateToStoreVip(); Close(); }

    // ============================================================
    // 04. REFRESH DE DADOS SEM RECONSTRUÇÃO DE HIERARQUIA
    // ============================================================

    private void RefreshAll()
    {
        if (service == null || view == null) return;
        (view as WDArmoryScreenV2View)?.ApplyVisualSettings(visualSettings);
        view.Coins.text = service.Profile.Coins.ToString("N0");
        view.Gems.text = service.Profile.Emeralds.ToString("N0");
        RefreshDeck();
        RefreshCatalog();
        RefreshDetails();
        if (!restorationNoticeShown && !string.IsNullOrWhiteSpace(service.RestorationNotice))
        {
            view.Feedback.text = service.RestorationNotice;
            restorationNoticeShown = true;
        }
    }

    private void RefreshDeck()
    {
        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        int mainCount = deck.MainWeaponIds.Count(id => !string.IsNullOrEmpty(id));
        int supportCount = deck.SupportWeaponIds.Count(id => !string.IsNullOrEmpty(id));
        view.DeckNameInput.SetTextWithoutNotify(deck.Name.ToUpperInvariant());
        view.DeckMeta.text = $"DECK {selectedDeckIndex + 1} · {mainCount}/8 PRINCIPAIS · {supportCount}/2 APOIO";
        view.MainMeta.text = $"PRINCIPAIS · {mainCount}/8";
        view.SupportMeta.text = $"APOIO · {supportCount}/2";

        for (int i = 0; i < view.DeckButtons.Length; i++)
        {
            bool unlocked = service.Profile.IsDeckUnlocked(i);
            bool selected = i == selectedDeckIndex;
            view.DeckLabels[i].text = $"DECK {i + 1}";
            if (i < view.DeckLockIcons.Length && view.DeckLockIcons[i] != null)
                view.DeckLockIcons[i].gameObject.SetActive(!unlocked);
            StyleOption(view.DeckButtons[i], view.DeckLabels[i], selected, unlocked);
        }

        for (int i = 0; i < view.MainSlots.Length; i++)
            RefreshSlot(view.MainSlots[i], deck.GetMain(i), false, i == selectedMainSlot);
        for (int i = 0; i < view.SupportSlots.Length; i++)
            RefreshSlot(view.SupportSlots[i], deck.GetSupport(i), true, i == selectedSupportSlot);
    }

    private void RefreshSlot(WDArmorySlotView slot, string weaponId, bool support, bool selected)
    {
        if (string.IsNullOrEmpty(weaponId))
        {
            slot.SetEmpty(support, selected);
            return;
        }

        WDWeaponDefinition weapon = service.Catalog.FindWeapon(weaponId);
        slot.SetWeapon(weapon.DisplayName, weapon.EnergyCost,
            ResolveArtwork(weaponId), weapon.CategoryId, selected);
    }

    private void RefreshCatalog()
    {
        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        List<WDWeaponDefinition> visibleWeapons = service.Catalog.Weapons
            .Where(weapon => !deck.Contains(weapon.Id))
            .Where(weapon => string.IsNullOrEmpty(categoryFilter) || weapon.CategoryId == categoryFilter)
            .Where(weapon => string.IsNullOrWhiteSpace(search)
                || weapon.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        int count = Mathf.Min(visibleWeapons.Count, view.CatalogCards.Length);
        for (int i = 0; i < view.CatalogCards.Length; i++)
        {
            WDArmoryWeaponCardView card = view.CatalogCards[i];
            bool hasWeapon = i < count;
            card.gameObject.SetActive(hasWeapon);
            if (!hasWeapon) continue;

            WDWeaponDefinition weapon = visibleWeapons[i];
            WDWeaponProgress progress = service.Profile.GetProgress(weapon.Id);
            bool obtained = progress?.Obtained == true;
            WDWeaponLevelDefinition next = obtained
                ? service.Catalog.FindUpgrade(weapon, progress.Level + 1) : null;
            WDArmoryWeaponCardView.VisualState state = !obtained
                ? WDArmoryWeaponCardView.VisualState.Locked
                : weapon.Id == selectedWeaponId ? WDArmoryWeaponCardView.VisualState.Selected
                : WDArmoryWeaponCardView.VisualState.Available;
            WDWeaponCategoryDefinition category = service.Catalog.Categories.FirstOrDefault(c => c.Id == weapon.CategoryId);
            card.SetData(new WDArmoryWeaponCardView.ViewData(
                weapon.Id, weapon.DisplayName, category?.DisplayName ?? weapon.CategoryId,
                obtained ? progress.Level : 0, weapon.EnergyCost, progress?.Tokens ?? 0,
                next?.RequiredTokens ?? 0, state), ResolveArtwork(weapon.Id), SelectCatalogWeapon,
                EquipCatalogWeapon);
        }

        for (int i = 0; i < view.FilterButtons.Length; i++)
            StyleFilterOption(view.FilterButtons[i], view.FilterLabels[i],
                FilterIds[i] == (categoryFilter ?? string.Empty), true);
    }

    private void RefreshDetails()
    {
        WDWeaponDefinition weapon = service.Catalog.FindWeapon(selectedWeaponId);
        if (weapon == null) return;

        WDWeaponProgress progress = service.Profile.GetProgress(weapon.Id);
        bool obtained = progress?.Obtained == true;
        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        bool equipped = deck.Contains(weapon.Id);
        WDWeaponCategoryDefinition category = service.Catalog.Categories.FirstOrDefault(c => c.Id == weapon.CategoryId);

        view.DetailArtwork.texture = ResolveArtwork(weapon.Id);
        view.DetailArtwork.color = obtained ? Color.white : new Color(1f, 1f, 1f, .5f);
        // ============================================================
        // 04.2 — HIERARQUIA CROMÁTICA DO PREVIEW APROVADO
        // ============================================================
        // O mesmo TMP preserva o binding funcional, enquanto rich text separa
        // categoria premium (dourado) do estado interativo (ciano).
        view.DetailCategory.richText = true;
        view.DetailCategory.text = $"<color=#C59B63>{(category?.DisplayName ?? weapon.CategoryId).ToUpperInvariant()}</color>"
            + $"   ·   <color=#0DC2DB>{(equipped ? "EQUIPADA" : "SELECIONADA")}</color>";
        view.DetailName.text = weapon.DisplayName.ToUpperInvariant();
        view.DetailName.color = Color.white;
        view.DetailFacts.richText = true;
        view.DetailFacts.text = $"<color=#C59B63>{(obtained ? $"NÍVEL {progress.Level}" : "NÃO OBTIDA")}</color>"
            + $"          <color=#0DC2DB>CUSTO {weapon.EnergyCost}</color>";
        view.DetailDescription.text = EffectSummary(weapon);

        int level = progress?.Level ?? 1;
        WDWeaponLevelDefinition next = obtained ? service.Catalog.FindUpgrade(weapon, level + 1) : null;
        int required = next?.RequiredTokens ?? 0;
        int owned = progress?.Tokens ?? 0;
        view.DetailEvolution.richText = true;
        view.DetailEvolution.text = next != null
            ? $"<color=#C59B63>ATUAL · {level}</color>                 <color=#F5F7FA>PRÓXIMO · {next.TargetLevel}</color>"
            : "<color=#C59B63>NÍVEL MÁXIMO</color>";
        view.DetailTokens.text = next != null ? $"{owned:N0} / {required:N0} TOKENS" : "EVOLUÇÃO CONCLUÍDA";
        view.DetailMissing.text = next != null ? $"FALTAM {Mathf.Max(0, required - owned):N0} TOKENS" : string.Empty;
        view.DetailTokenFill.rectTransform.anchorMax = new Vector2(
            next != null && required > 0 ? Mathf.Clamp01((float)owned / required) : 1f, 1f);
        // 04.1 — O painel apresenta somente estado funcional comprovado.
        // Textos internos de configuração não fazem parte da interface comercial.
        view.DetailBenefit.text = next != null
            ? $"PRÓXIMO NÍVEL · {next.TargetLevel}" : "NÍVEL MÁXIMO";
        view.DetailUpgradeCost.color = new Color32(245, 247, 250, 255);
        view.DetailUpgradeCost.text = next != null
            ? $"{required:N0} TOKENS + {next.CoinCost:N0} MOEDAS" : "—";
        view.UpgradeButton.interactable = obtained && next != null
            && owned >= required && service.Profile.Coins >= next.CoinCost;
        bool supportWeapon = service.Catalog.IsSupportEligible(weapon);
        // 08. O CTA usa a regra canônica: principais aceitam qualquer categoria;
        // slots exclusivos de Apoio rejeitam todas as demais categorias.
        bool compatibleTarget = selectedMainSlot >= 0 || (supportWeapon && selectedSupportSlot >= 0);
        int emptySlot = supportWeapon ? FirstEmptySupport(deck) : FirstEmptyMain(deck);
        if (supportWeapon && emptySlot < 0) emptySlot = FirstEmptyMain(deck);
        view.AddButton.interactable = obtained && !equipped && (compatibleTarget || emptySlot >= 0);
        int mainRemaining = Mathf.Max(0, 8 - deck.MainWeaponIds.Count(id => !string.IsNullOrEmpty(id)));
        int supportRemaining = Mathf.Max(0, 2 - deck.SupportWeaponIds.Count(id => !string.IsNullOrEmpty(id)));
        view.DetailHint.text = supportWeapon
            ? $"DECK {selectedDeckIndex + 1} · {deck.Name.ToUpperInvariant()} · {mainRemaining} principais · {supportRemaining} apoio"
            : $"DECK {selectedDeckIndex + 1} · {deck.Name.ToUpperInvariant()} · {mainRemaining} vagas principais";
    }

    // ============================================================
    // 05. AÇÕES CANÔNICAS E MODAL
    // ============================================================

    private void SelectDeck(int index)
    {
        if (!service.Profile.IsDeckUnlocked(index))
        {
            vipNavigation.OpenRequirementModal();
            lockedDeckAction?.Invoke();
            return;
        }
        selectedDeckIndex = index;
        service.Profile.SetActiveDeck(index);
        selectedMainSlot = -1;
        selectedSupportSlot = -1;
        view.Feedback.text = $"Deck {index + 1} selecionado";
        RefreshAll();
    }

    private void SelectFilter(string id) { categoryFilter = string.IsNullOrEmpty(id) ? null : id; RefreshCatalog(); }
    private void OnSearchChanged(string value) { search = value ?? string.Empty; RefreshCatalog(); }
    private void SelectCatalogWeapon(string id)
    {
        selectedWeaponId = id;
        RefreshCatalog();
        RefreshDetails();
    }

    private void EquipCatalogWeapon(string id)
    {
        selectedWeaponId = id;
        TryEquipWeapon(id, false);
    }

    private void SlotClicked(bool support, int slot)
    {
        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        string id = support ? deck.GetSupport(slot) : deck.GetMain(slot);
        selectedSupportSlot = support ? slot : -1;
        selectedMainSlot = support ? -1 : slot;
        if (!string.IsNullOrEmpty(id)) selectedWeaponId = id;
        view.Feedback.text = support ? $"Slot de apoio {slot + 1} selecionado" : $"Slot principal {slot + 1} selecionado";
        RefreshAll();
    }

    private void RemoveSlot(bool support, int slot)
    {
        WDArmoryEditResult result = support ? service.RemoveSupport(selectedDeckIndex, slot)
            : service.RemoveMain(selectedDeckIndex, slot);
        if (support && selectedSupportSlot == slot) selectedSupportSlot = -1;
        if (!support && selectedMainSlot == slot) selectedMainSlot = -1;
        view.Feedback.text = result == WDArmoryEditResult.Success ? "Arma removida do deck" : ResultText(result);
        RefreshAll();
    }

    private void AddSelected()
    {
        TryEquipWeapon(selectedWeaponId, true);
    }

    private void UpgradeSelected()
    {
        WDArmoryEditResult result = service.Upgrade(selectedWeaponId);
        view.Feedback.text = result == WDArmoryEditResult.Success ? "Arma evoluída" : ResultText(result);
        if (result == WDArmoryEditResult.Success) repository.Save(service.Profile);
        RefreshAll();
    }

    private void SaveDeck() { repository.Save(service.Profile); view.Feedback.text = "Deck salvo"; }

    // ============================================================
    // 05.1 EDIÇÃO EXPLÍCITA E PERSISTENTE DO NOME DO DECK
    // ============================================================
    private void BeginDeckNameEdit()
    {
        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        if (deck == null) return;
        deckNameBeforeEdit = deck.Name;
        deckNameEditDeckIndex = selectedDeckIndex;
        deckNameEditing = true;
        view.DeckNameInput.ActivateInputField();
        view.DeckNameInput.Select();
        view.DeckNameInput.MoveTextEnd(false);
    }

    private void FinishDeckNameEdit(string newName)
    {
        if (!deckNameEditing) return;
        deckNameEditing = false;
        if (view.DeckNameInput.wasCanceled)
        {
            view.DeckNameInput.SetTextWithoutNotify(deckNameBeforeEdit.ToUpperInvariant());
            view.Feedback.text = "Edição do nome cancelada";
            deckNameEditDeckIndex = -1;
            return;
        }

        WDArmoryEditResult result = service.RenameDeck(deckNameEditDeckIndex, newName);
        view.Feedback.text = result == WDArmoryEditResult.Success ? "Nome do deck atualizado" : ResultText(result);
        if (result == WDArmoryEditResult.Success)
            repository.Save(service.Profile);
        else
            view.DeckNameInput.SetTextWithoutNotify(deckNameBeforeEdit.ToUpperInvariant());
        deckNameEditDeckIndex = -1;
        RefreshDeck();
    }

    private void TryEquipWeapon(string weaponId, bool preferSelectedSlot)
    {
        WDWeaponDefinition weapon = service.Catalog.FindWeapon(weaponId);
        if (weapon == null)
        {
            view.Feedback.text = ResultText(WDArmoryEditResult.UnknownWeapon);
            return;
        }

        WDSavedWeaponDeck deck = service.Profile.GetDeck(selectedDeckIndex);
        bool supportWeapon = service.Catalog.IsSupportEligible(weapon);
        bool targetSupport;
        int slot;

        if (preferSelectedSlot && (selectedMainSlot >= 0 || selectedSupportSlot >= 0))
        {
            targetSupport = selectedSupportSlot >= 0;
            if (targetSupport && !supportWeapon)
            {
                view.Feedback.text = "Somente armas de apoio podem ocupar slots de apoio";
                RefreshDetails();
                return;
            }
            slot = targetSupport ? selectedSupportSlot : selectedMainSlot;
        }
        else
        {
            targetSupport = supportWeapon && FirstEmptySupport(deck) >= 0;
            slot = targetSupport ? FirstEmptySupport(deck) : FirstEmptyMain(deck);
            if (slot < 0)
            {
                view.Feedback.text = supportWeapon
                    ? "Não há vaga compatível neste deck"
                    : "Não há vaga principal neste deck";
                RefreshDetails();
                return;
            }
        }

        WDArmoryEditResult result = targetSupport
            ? service.AssignSupport(selectedDeckIndex, slot, weaponId)
            : service.AssignMain(selectedDeckIndex, slot, weaponId);
        view.Feedback.text = result == WDArmoryEditResult.Success
            ? (deck.Contains(weaponId) ? "Arma equipada no deck" : "Arma adicionada ao deck")
            : ResultText(result);
        if (result == WDArmoryEditResult.Success)
        {
            selectedMainSlot = -1;
            selectedSupportSlot = -1;
        }
        RefreshAll();
    }

    // ============================================================
    // 06. HELPERS FUNCIONAIS SEM AUTORIDADE DE GEOMETRIA
    // ============================================================

    private void StyleOption(Button button, TMP_Text label, bool selected, bool enabled)
    {
        // Decks VIP continuam clicáveis para abrir a explicação/requisito.
        // O estado bloqueado é visual e a autoridade funcional permanece em SelectDeck.
        button.interactable = true;
        Color cyan = visualSettings != null ? visualSettings.selectedColor : theme.MasterCyan;
        if (button.targetGraphic is Image surface)
            surface.color = visualSettings != null ? visualSettings.buttonColor : theme.MasterSurfaceElevated;

        // O estado selecionado pertence ao contorno de 1 px. A superfície
        // permanece escura para não produzir preenchimento ciano integral.
        Transform borderTransform = button.transform.Find("Border");
        if (borderTransform != null && borderTransform.TryGetComponent(out Image border))
            border.color = selected ? cyan : new Color(1f, 1f, 1f, .07f);

        label.color = selected ? cyan
            : enabled ? theme.MasterTextSecondary : theme.MasterGold;
    }

    // ============================================================
    // 06.1 FILTROS PREMIUM TACTICAL SEM ALTERAR ABAS DE DECK
    // ============================================================
    private void StyleFilterOption(Button button, TMP_Text label, bool selected, bool enabled)
    {
        button.interactable = true;
        if (button.targetGraphic is Image surface)
            surface.color = visualSettings != null ? visualSettings.buttonColor : theme.MasterSurfaceElevated;

        Transform borderTransform = button.transform.Find("Border");
        if (borderTransform != null && borderTransform.TryGetComponent(out Image border))
            border.color = selected
                ? theme.MasterGold
                : new Color(theme.MasterTextSecondary.r, theme.MasterTextSecondary.g, theme.MasterTextSecondary.b, .09f);

        label.color = selected ? theme.MasterGold : theme.MasterTextSecondary;
    }

    private static int FirstEmptyMain(WDSavedWeaponDeck deck)
    {
        for (int i = 0; i < WDSavedWeaponDeck.MainSlotCount; i++)
            if (string.IsNullOrEmpty(deck.GetMain(i))) return i;
        return -1;
    }

    private static int FirstEmptySupport(WDSavedWeaponDeck deck)
    {
        for (int i = 0; i < WDSavedWeaponDeck.SupportSlotCount; i++)
            if (string.IsNullOrEmpty(deck.GetSupport(i))) return i;
        return -1;
    }

    private static string EffectSummary(WDWeaponDefinition weapon)
    {
        if (!string.IsNullOrWhiteSpace(weapon.Description)) return weapon.Description;
        List<string> facts = new();
        if (weapon.CooldownRounds > 0) facts.Add($"Recarga de {weapon.CooldownRounds} rodadas");
        if (weapon.Range > 0) facts.Add($"alcance {weapon.Range}");
        if (weapon.DurationRounds > 0) facts.Add($"duração de {weapon.DurationRounds} rodadas");
        if (weapon.IsInstallation) facts.Add("instalação territorial");
        return facts.Count > 0 ? string.Join(" · ", facts) : string.Empty;
    }

    private Texture ResolveArtwork(string weaponId)
    {
        Texture fallback = visualCatalog.Resolve(weaponId);
        return visualSettings != null ? visualSettings.ResolveArtwork(weaponId, fallback) : fallback;
    }

    private static string ResultText(WDArmoryEditResult result) => result switch
    {
        WDArmoryEditResult.LockedDeck => "Este deck requer VIP",
        WDArmoryEditResult.WeaponNotObtained => "Esta arma ainda não foi obtida",
        WDArmoryEditResult.DuplicateWeapon => "Esta arma já está equipada neste deck",
        WDArmoryEditResult.NotSupportEligible => "Armas principais não podem ocupar slots de apoio",
        WDArmoryEditResult.InvalidName => "Use um nome de deck válido",
        WDArmoryEditResult.InsufficientTokens => "Tokens insuficientes",
        WDArmoryEditResult.InsufficientCoins => "Moedas insuficientes",
        WDArmoryEditResult.AlreadyMaxLevel => "Nível máximo já atingido",
        _ => "A ação não pôde ser concluída"
    };

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
}

public static class WDWeaponArtworkResolver
{
    private const string Root = "UI/Arsenal/Canonical/";
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["drone"]="weapon-artwork-card-drone", ["atomic_bomb"]="weapon-artwork-card-bomba-atomica",
        ["war_propaganda"]="weapon-artwork-card-propaganda-de-guerra", ["chemical_bomb"]="weapon-artwork-card-bomba-quimica",
        ["infiltrator"]="weapon-artwork-card-infiltrado", ["sabotage"]="weapon-artwork-card-sabotagem",
        ["energy_plant"]="weapon-artwork-card-central-de-energia", ["long_range_bomb"]="weapon-artwork-card-bomba-de-longo-alcance",
        ["peace"]="weapon-artwork-card-paz", ["artillery"]="weapon-artwork-card-artilharia",
        ["rockets"]="weapon-artwork-card-foguetes", ["minefield"]="weapon-artwork-card-campo-minado",
        ["blitzkrieg"]="weapon-artwork-card-blitzkrieg", ["transfer_nullifier"]="weapon-artwork-card-anulador-de-transferencia",
        ["sniper"]="weapon-artwork-card-sniper", ["kamikaze"]="weapon-artwork-card-kamikaze",
        ["paratroopers"]="weapon-artwork-card-paraquedistas", ["radar"]="weapon-artwork-card-radar",
        ["air_strike"]="weapon-artwork-ataque-aereo", ["reinforcements"]="weapon-artwork-card-reforcos",
        ["air_transport"]="weapon-artwork-card-transporte-aereo", ["flak_tower"]="weapon-artwork-card-torre-antiaerea",
        ["bunker"]="weapon-artwork-card-bunker", ["reserve_troops"]="weapon-artwork-card-tropas-reservas",
        ["troop_recovery"]="weapon-artwork-card-recuperacao-de-tropas", ["mercenaries"]="weapon-artwork-card-mercenarios"
    };
    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);
    public static int MappingCount => Paths.Count;
    public static Texture2D Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Paths.TryGetValue(id, out string path)) return null;
        if (!Cache.TryGetValue(id, out Texture2D texture) || texture == null)
        {
            texture = Resources.Load<Texture2D>(Root + path);
            Cache[id] = texture;
        }
        return texture;
    }
}

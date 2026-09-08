using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WDWeaponCollectionState
{
    NotObtained,
    CollectingTokens,
    NeedsCoins,
    UpgradeAvailable,
    MaxLevel
}

[Serializable]
public sealed class WDWeaponCategoryDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private bool supportEligible;

    public string Id => id ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public bool SupportEligible => supportEligible;

    public WDWeaponCategoryDefinition(string id, string displayName, bool supportEligible)
    {
        this.id = id;
        this.displayName = displayName;
        this.supportEligible = supportEligible;
    }
}

[Serializable]
public sealed class WDWeaponLevelDefinition
{
    [SerializeField, Range(2, WDWeaponDefinition.MaxLevel)] private int targetLevel = 2;
    [SerializeField, Min(0)] private int requiredTokens;
    [SerializeField, Min(0)] private int coinCost;
    [SerializeField] private string futureLevelConfigId;

    public int TargetLevel => targetLevel;
    public int RequiredTokens => requiredTokens;
    public int CoinCost => coinCost;
    public string FutureLevelConfigId => futureLevelConfigId ?? string.Empty;

    public WDWeaponLevelDefinition(
        int targetLevel, int requiredTokens, int coinCost,
        string futureLevelConfigId = "")
    {
        this.targetLevel = Mathf.Clamp(targetLevel, 2, WDWeaponDefinition.MaxLevel);
        this.requiredTokens = Mathf.Max(0, requiredTokens);
        this.coinCost = Mathf.Max(0, coinCost);
        this.futureLevelConfigId = futureLevelConfigId;
    }
}

[Serializable]
public sealed class WDWeaponProgressionProfile
{
    [SerializeField, Range(1, WDWeaponDefinition.MaxLevel)] private int maximumLevel = 1;
    [SerializeField] private List<WDWeaponLevelDefinition> upgrades = new();

    public int MaximumLevel => Mathf.Clamp(maximumLevel, 1, WDWeaponDefinition.MaxLevel);
    public IReadOnlyList<WDWeaponLevelDefinition> Upgrades => upgrades;

    public WDWeaponProgressionProfile(
        int maximumLevel, IEnumerable<WDWeaponLevelDefinition> upgrades)
    {
        this.maximumLevel = Mathf.Clamp(maximumLevel, 1, WDWeaponDefinition.MaxLevel);
        this.upgrades = upgrades?.ToList() ?? new List<WDWeaponLevelDefinition>();
    }

    public WDWeaponLevelDefinition GetUpgradeToLevel(int targetLevel) =>
        upgrades?.FirstOrDefault(item => item != null && item.TargetLevel == targetLevel);
}

[Serializable]
public sealed class WDWeaponDefinition
{
    public const int MaxLevel = 6;

    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string categoryId;
    [SerializeField, Range(1, MaxLevel)] private int maximumLevel = MaxLevel;
    [SerializeField, Min(0)] private int energyCost;
    [SerializeField, Min(0)] private int cooldownRounds;
    [SerializeField, Min(0)] private int useLimit;
    [SerializeField] private WDWeaponBehaviorType behaviorType;
    [SerializeField] private WDWeaponResolutionPhase resolutionPhase;
    [SerializeField] private WDWeaponCostTiming costTiming;
    [SerializeField] private bool requiresOriginTerritory;
    [SerializeField] private bool requiresTargetTerritory;
    [SerializeField] private bool requiresTargetPlayer;
    [SerializeField] private bool canTargetEmptyTerritory;
    [SerializeField] private bool installation;
    [SerializeField, Min(0)] private int range;
    [SerializeField, Min(0)] private int durationRounds;
    [SerializeField, Min(0)] private int charges;
    [SerializeField, TextArea] private string description;
    [SerializeField] private string iconKey;
    [SerializeField] private List<WDWeaponLevelDefinition> levels = new();

    public string Id => id ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public string CategoryId => categoryId ?? string.Empty;
    public int MaximumLevel => maximumLevel > 0
        ? Mathf.Clamp(maximumLevel, 1, MaxLevel)
        : InferMaximumLevel(levels);
    public int EnergyCost => Mathf.Max(0, energyCost);
    public int CooldownRounds => Mathf.Max(0, cooldownRounds);
    public int UseLimit => Mathf.Max(0, useLimit);
    public WDWeaponBehaviorType BehaviorType => behaviorType;
    public WDWeaponResolutionPhase ResolutionPhase => resolutionPhase;
    public WDWeaponCostTiming CostTiming => costTiming;
    public bool RequiresOriginTerritory => requiresOriginTerritory;
    public bool RequiresTargetTerritory => requiresTargetTerritory;
    public bool RequiresTargetPlayer => requiresTargetPlayer;
    public bool CanTargetEmptyTerritory => canTargetEmptyTerritory;
    public bool IsInstallation => installation;
    public int Range => Mathf.Max(0, range);
    public int DurationRounds => Mathf.Max(0, durationRounds);
    public int Charges => Mathf.Max(0, charges);
    public string Description => description ?? string.Empty;
    public string IconKey => iconKey ?? string.Empty;
    public IReadOnlyList<WDWeaponLevelDefinition> Levels => levels;
    public bool HasSupportedLevelCount =>
        MaximumLevel == 1 || MaximumLevel == 4 || MaximumLevel == 6;

    public WDWeaponDefinition(
        string id, string displayName, string categoryId,
        IEnumerable<WDWeaponLevelDefinition> levels)
    {
        this.id = id;
        this.displayName = displayName;
        this.categoryId = categoryId;
        this.levels = levels?.ToList() ?? new List<WDWeaponLevelDefinition>();
        maximumLevel = InferMaximumLevel(this.levels);
    }

    public void ConfigureRuntime(
        int maxLevel, int energy, int cooldown, int maxUses,
        WDWeaponBehaviorType behavior, WDWeaponResolutionPhase phase,
        WDWeaponCostTiming timing = WDWeaponCostTiming.OnExecution,
        bool needsOrigin = false, bool needsTerritoryTarget = false,
        bool needsPlayerTarget = false, bool targetsEmpty = false,
        bool isInstallation = false, int maxRange = 0,
        int duration = 0, int initialCharges = 0,
        string uiDescription = "", string uiIconKey = "")
    {
        if (maxLevel != 1 && maxLevel != 4 && maxLevel != 6)
            throw new ArgumentOutOfRangeException(
                nameof(maxLevel), "Armas V1 aceitam 1, 4 ou 6 níveis.");
        maximumLevel = Mathf.Clamp(maxLevel, 1, MaxLevel);
        energyCost = Mathf.Max(0, energy);
        cooldownRounds = Mathf.Max(0, cooldown);
        useLimit = Mathf.Max(0, maxUses);
        behaviorType = behavior;
        resolutionPhase = phase;
        costTiming = timing;
        requiresOriginTerritory = needsOrigin;
        requiresTargetTerritory = needsTerritoryTarget;
        requiresTargetPlayer = needsPlayerTarget;
        canTargetEmptyTerritory = targetsEmpty;
        installation = isInstallation;
        range = Mathf.Max(0, maxRange);
        durationRounds = Mathf.Max(0, duration);
        charges = Mathf.Max(0, initialCharges);
        description = uiDescription;
        iconKey = uiIconKey;
    }

    public WDWeaponLevelDefinition GetUpgradeToLevel(int targetLevel) =>
        levels?.FirstOrDefault(item => item != null && item.TargetLevel == targetLevel);

    private static int InferMaximumLevel(IEnumerable<WDWeaponLevelDefinition> upgrades)
    {
        int inferred = upgrades?.Where(item => item != null)
            .Select(item => item.TargetLevel).DefaultIfEmpty(1).Max() ?? 1;
        return Mathf.Clamp(inferred, 1, MaxLevel);
    }
}

[Serializable]
public sealed class WDWeaponProgress
{
    [SerializeField] private string weaponId;
    [SerializeField] private bool obtained;
    [SerializeField, Range(1, WDWeaponDefinition.MaxLevel)] private int level = 1;
    [SerializeField, Min(0)] private int tokens;

    public string WeaponId => weaponId ?? string.Empty;
    public bool Obtained => obtained;
    public int Level => Mathf.Clamp(level, 1, WDWeaponDefinition.MaxLevel);
    public int Tokens => Mathf.Max(0, tokens);

    public WDWeaponProgress(string weaponId, bool obtained, int level = 1, int tokens = 0)
    {
        this.weaponId = weaponId;
        this.obtained = obtained;
        this.level = Mathf.Clamp(level, 1, WDWeaponDefinition.MaxLevel);
        this.tokens = Mathf.Max(0, tokens);
    }

    public void SetProgress(bool isObtained, int newLevel, int newTokens)
    {
        obtained = isObtained;
        level = Mathf.Clamp(newLevel, 1, WDWeaponDefinition.MaxLevel);
        tokens = Mathf.Max(0, newTokens);
    }

    public void SpendTokens(int amount) => tokens = Mathf.Max(0, tokens - Mathf.Max(0, amount));
    public void AdvanceLevel(int maximumLevel = WDWeaponDefinition.MaxLevel) =>
        level = Mathf.Min(Mathf.Clamp(maximumLevel, 1, WDWeaponDefinition.MaxLevel), level + 1);
}

[Serializable]
public sealed class WDSavedWeaponDeck
{
    public const int MainSlotCount = 8;
    public const int SupportSlotCount = 2;
    public const int SupportUsesPerWeaponPerMatch = 1;

    [SerializeField] private string name;
    [SerializeField] private string[] mainWeaponIds = new string[MainSlotCount];
    [SerializeField] private string[] supportWeaponIds = new string[SupportSlotCount];

    public string Name => string.IsNullOrWhiteSpace(name) ? "DECK" : name;
    public IReadOnlyList<string> MainWeaponIds => mainWeaponIds;
    public IReadOnlyList<string> SupportWeaponIds => supportWeaponIds;

    public WDSavedWeaponDeck(string name)
    {
        this.name = name;
        mainWeaponIds = new string[MainSlotCount];
        supportWeaponIds = new string[SupportSlotCount];
    }

    public void Normalize(string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = fallbackName;
        if (mainWeaponIds == null || mainWeaponIds.Length != MainSlotCount)
            Array.Resize(ref mainWeaponIds, MainSlotCount);
        if (supportWeaponIds == null || supportWeaponIds.Length != SupportSlotCount)
            Array.Resize(ref supportWeaponIds, SupportSlotCount);
    }

    public void Rename(string newName)
    {
        string normalized = newName?.Trim();
        if (!string.IsNullOrEmpty(normalized))
            name = normalized;
    }

    public string GetMain(int slot) => IsMainSlot(slot) ? mainWeaponIds[slot] : string.Empty;
    public string GetSupport(int slot) => IsSupportSlot(slot) ? supportWeaponIds[slot] : string.Empty;
    public void SetMain(int slot, string weaponId) { if (IsMainSlot(slot)) mainWeaponIds[slot] = weaponId ?? string.Empty; }
    public void SetSupport(int slot, string weaponId) { if (IsSupportSlot(slot)) supportWeaponIds[slot] = weaponId ?? string.Empty; }

    public bool Contains(string weaponId) => !string.IsNullOrEmpty(weaponId) &&
        (mainWeaponIds.Contains(weaponId) || supportWeaponIds.Contains(weaponId));

    public bool RemoveLegacyCardPlaceholders()
    {
        bool changed = RemoveLegacyCardPlaceholders(mainWeaponIds);
        return RemoveLegacyCardPlaceholders(supportWeaponIds) || changed;
    }

    private static bool RemoveLegacyCardPlaceholders(string[] weaponIds)
    {
        bool changed = false;
        if (weaponIds == null)
            return false;
        for (int i = 0; i < weaponIds.Length; i++)
        {
            string id = weaponIds[i];
            if (id == null || id.Length != 7 || !id.StartsWith("card_", StringComparison.Ordinal) ||
                !int.TryParse(id.Substring(5), out int number) || number < 1 || number > 24)
                continue;
            weaponIds[i] = string.Empty;
            changed = true;
        }
        return changed;
    }

    private static bool IsMainSlot(int slot) => slot >= 0 && slot < MainSlotCount;
    private static bool IsSupportSlot(int slot) => slot >= 0 && slot < SupportSlotCount;
}

[Serializable]
public sealed class WDArmoryProfile
{
    public const int SavedDeckCount = 5;
    public const int FreeDeckCount = 2;

    [SerializeField] private bool vip;
    [SerializeField, Min(0)] private int coins;
    [SerializeField, Min(0)] private int emeralds;
    [SerializeField] private List<WDWeaponProgress> weaponProgress = new();
    [SerializeField] private List<WDSavedWeaponDeck> decks = new();

    public bool IsVip => vip;
    public int Coins => Mathf.Max(0, coins);
    public int Emeralds => Mathf.Max(0, emeralds);
    public IReadOnlyList<WDWeaponProgress> WeaponProgress => weaponProgress;
    public IReadOnlyList<WDSavedWeaponDeck> Decks => decks;

    public WDArmoryProfile(bool vip = false, int coins = 0, int emeralds = 0)
    {
        this.vip = vip;
        this.coins = Mathf.Max(0, coins);
        this.emeralds = Mathf.Max(0, emeralds);
        Normalize();
    }

    public void Normalize()
    {
        weaponProgress ??= new List<WDWeaponProgress>();
        decks ??= new List<WDSavedWeaponDeck>();
        while (decks.Count < SavedDeckCount)
            decks.Add(new WDSavedWeaponDeck($"DECK {decks.Count + 1}"));
        if (decks.Count > SavedDeckCount)
            decks.RemoveRange(SavedDeckCount, decks.Count - SavedDeckCount);
        for (int i = 0; i < decks.Count; i++)
        {
            decks[i] ??= new WDSavedWeaponDeck($"DECK {i + 1}");
            decks[i].Normalize($"DECK {i + 1}");
        }
    }

    public bool IsDeckUnlocked(int deckIndex) =>
        deckIndex >= 0 && deckIndex < SavedDeckCount && (vip || deckIndex < FreeDeckCount);

    public WDSavedWeaponDeck GetDeck(int deckIndex) =>
        deckIndex >= 0 && deckIndex < decks.Count ? decks[deckIndex] : null;

    public WDWeaponProgress GetProgress(string weaponId) =>
        weaponProgress.FirstOrDefault(item => item != null && item.WeaponId == weaponId);

    public void AddProgress(WDWeaponProgress progress)
    {
        if (progress != null && GetProgress(progress.WeaponId) == null)
            weaponProgress.Add(progress);
    }

    public bool RemoveLegacyCardPlaceholders()
    {
        bool changed = false;
        foreach (WDSavedWeaponDeck deck in decks)
            if (deck != null && deck.RemoveLegacyCardPlaceholders())
                changed = true;
        return changed;
    }

    public void SetVip(bool value) => vip = value;
    public void SetCurrencies(int newCoins, int newEmeralds)
    {
        coins = Mathf.Max(0, newCoins);
        emeralds = Mathf.Max(0, newEmeralds);
    }

    public bool SpendCoins(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (coins < safeAmount)
            return false;
        coins -= safeAmount;
        return true;
    }
}

public enum WDArmoryEditResult
{
    Success,
    InvalidDeck,
    LockedDeck,
    InvalidSlot,
    UnknownWeapon,
    WeaponNotObtained,
    DuplicateWeapon,
    NotSupportEligible,
    MissingLevelConfiguration,
    InsufficientTokens,
    InsufficientCoins,
    AlreadyMaxLevel
}

public enum WDHomeDestination
{
    Armory,
    StoreVip
}

public sealed class WDArmoryVipNavigation
{
    // ============================================================
    // 02. ROTA REUTILIZÁVEL PARA REQUISITOS VIP DA SEÇÃO ARMAS
    // ============================================================

    public WDHomeDestination Destination { get; private set; } = WDHomeDestination.Armory;
    public bool IsRequirementModalOpen { get; private set; }

    public void ReturnToArmory()
    {
        Destination = WDHomeDestination.Armory;
        IsRequirementModalOpen = false;
    }

    public void OpenRequirementModal()
    {
        Destination = WDHomeDestination.Armory;
        IsRequirementModalOpen = true;
    }

    public void CloseRequirementModal() => IsRequirementModalOpen = false;

    public void NavigateToStoreVip()
    {
        IsRequirementModalOpen = false;
        Destination = WDHomeDestination.StoreVip;
    }
}

public sealed class WDArmoryService
{
    // ============================================================
    // 03. REGRAS DE COMPOSIÇÃO, VIP E PROGRESSÃO
    // ============================================================

    private readonly WDArmoryCatalogConfig catalog;
    private readonly WDArmoryProfile profile;

    public WDArmoryProfile Profile => profile;
    public WDArmoryCatalogConfig Catalog => catalog;

    public WDArmoryService(WDArmoryCatalogConfig catalog, WDArmoryProfile profile)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        profile.Normalize();
    }

    public void SetVipForDevelopment(bool enabled) => profile.SetVip(enabled);

    public WDArmoryEditResult RenameDeck(int deckIndex, string newName)
    {
        WDArmoryEditResult access = ValidateDeckAccess(deckIndex);
        if (access != WDArmoryEditResult.Success)
            return access;
        profile.GetDeck(deckIndex).Rename(newName);
        return WDArmoryEditResult.Success;
    }

    public WDArmoryEditResult AssignMain(int deckIndex, int slot, string weaponId)
    {
        WDArmoryEditResult access = ValidateDeckAccess(deckIndex);
        if (access != WDArmoryEditResult.Success)
            return access;
        if (slot < 0 || slot >= WDSavedWeaponDeck.MainSlotCount)
            return WDArmoryEditResult.InvalidSlot;
        WDArmoryEditResult weaponResult = ValidateWeapon(deckIndex, weaponId);
        if (weaponResult != WDArmoryEditResult.Success)
            return weaponResult;
        profile.GetDeck(deckIndex).SetMain(slot, weaponId);
        return WDArmoryEditResult.Success;
    }

    public WDArmoryEditResult AssignSupport(int deckIndex, int slot, string weaponId)
    {
        WDArmoryEditResult access = ValidateDeckAccess(deckIndex);
        if (access != WDArmoryEditResult.Success)
            return access;
        if (slot < 0 || slot >= WDSavedWeaponDeck.SupportSlotCount)
            return WDArmoryEditResult.InvalidSlot;
        WDArmoryEditResult weaponResult = ValidateWeapon(deckIndex, weaponId);
        if (weaponResult != WDArmoryEditResult.Success)
            return weaponResult;
        if (!catalog.IsSupportEligible(catalog.FindWeapon(weaponId)))
            return WDArmoryEditResult.NotSupportEligible;
        profile.GetDeck(deckIndex).SetSupport(slot, weaponId);
        return WDArmoryEditResult.Success;
    }

    public WDArmoryEditResult RemoveMain(int deckIndex, int slot)
    {
        WDArmoryEditResult access = ValidateDeckAccess(deckIndex);
        if (access != WDArmoryEditResult.Success)
            return access;
        if (slot < 0 || slot >= WDSavedWeaponDeck.MainSlotCount)
            return WDArmoryEditResult.InvalidSlot;
        profile.GetDeck(deckIndex).SetMain(slot, string.Empty);
        return WDArmoryEditResult.Success;
    }

    public WDArmoryEditResult RemoveSupport(int deckIndex, int slot)
    {
        WDArmoryEditResult access = ValidateDeckAccess(deckIndex);
        if (access != WDArmoryEditResult.Success)
            return access;
        if (slot < 0 || slot >= WDSavedWeaponDeck.SupportSlotCount)
            return WDArmoryEditResult.InvalidSlot;
        profile.GetDeck(deckIndex).SetSupport(slot, string.Empty);
        return WDArmoryEditResult.Success;
    }

    public WDWeaponCollectionState GetCollectionState(string weaponId)
    {
        WDWeaponDefinition weapon = catalog.FindWeapon(weaponId);
        WDWeaponProgress progress = profile.GetProgress(weaponId);
        if (progress == null || !progress.Obtained)
            return WDWeaponCollectionState.NotObtained;
        if (weapon != null && progress.Level >= weapon.MaximumLevel)
            return WDWeaponCollectionState.MaxLevel;
        WDWeaponLevelDefinition next = catalog.FindUpgrade(weapon, progress.Level + 1);
        if (next == null || progress.Tokens < next.RequiredTokens)
            return WDWeaponCollectionState.CollectingTokens;
        return profile.Coins < next.CoinCost
            ? WDWeaponCollectionState.NeedsCoins
            : WDWeaponCollectionState.UpgradeAvailable;
    }

    public WDArmoryEditResult Upgrade(string weaponId)
    {
        WDWeaponDefinition weapon = catalog.FindWeapon(weaponId);
        WDWeaponProgress progress = profile.GetProgress(weaponId);
        if (weapon == null)
            return WDArmoryEditResult.UnknownWeapon;
        if (progress == null || !progress.Obtained)
            return WDArmoryEditResult.WeaponNotObtained;
        if (progress.Level >= weapon.MaximumLevel)
            return WDArmoryEditResult.AlreadyMaxLevel;
        WDWeaponLevelDefinition next = catalog.FindUpgrade(weapon, progress.Level + 1);
        if (next == null)
            return WDArmoryEditResult.MissingLevelConfiguration;
        if (progress.Tokens < next.RequiredTokens)
            return WDArmoryEditResult.InsufficientTokens;
        if (profile.Coins < next.CoinCost)
            return WDArmoryEditResult.InsufficientCoins;

        profile.SpendCoins(next.CoinCost);
        progress.SpendTokens(next.RequiredTokens);
        progress.AdvanceLevel(weapon.MaximumLevel);
        return WDArmoryEditResult.Success;
    }

    private WDArmoryEditResult ValidateDeckAccess(int deckIndex)
    {
        if (profile.GetDeck(deckIndex) == null)
            return WDArmoryEditResult.InvalidDeck;
        return profile.IsDeckUnlocked(deckIndex)
            ? WDArmoryEditResult.Success
            : WDArmoryEditResult.LockedDeck;
    }

    private WDArmoryEditResult ValidateWeapon(int deckIndex, string weaponId)
    {
        WDWeaponDefinition weapon = catalog.FindWeapon(weaponId);
        if (weapon == null)
            return WDArmoryEditResult.UnknownWeapon;
        WDWeaponProgress progress = profile.GetProgress(weaponId);
        if (progress == null || !progress.Obtained)
            return WDArmoryEditResult.WeaponNotObtained;
        return profile.GetDeck(deckIndex).Contains(weaponId)
            ? WDArmoryEditResult.DuplicateWeapon
            : WDArmoryEditResult.Success;
    }
}

[Serializable]
public sealed class WDMatchSupportUsage
{
    // ============================================================
    // 04. CONTRATO FUTURO DE USO INDEPENDENTE DOS DOIS APOIOS
    // ============================================================

    [SerializeField] private bool[] consumed = new bool[WDSavedWeaponDeck.SupportSlotCount];

    public bool CanUse(int slot) => consumed != null &&
        slot >= 0 && slot < consumed.Length && !consumed[slot];

    public bool TryConsume(int slot)
    {
        if (!CanUse(slot))
            return false;
        consumed[slot] = true;
        return true;
    }
}

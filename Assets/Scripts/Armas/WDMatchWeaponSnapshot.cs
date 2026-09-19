using System;
using System.Collections.Generic;
using System.Linq;

public enum WDMatchWeaponSlotKind
{
    Main,
    Support,
    Universal
}

public enum WDMatchWeaponSnapshotStatus
{
    Valid,
    SourceUnavailable,
    InvalidDeck,
    LockedDeck,
    IncompleteDeck,
    InvalidIdentity,
    DuplicateWeapon,
    WeaponNotObtained,
    InvalidLevel
}

public sealed class WDMatchWeaponSlotSnapshot
{
    public string WeaponId { get; }
    public int Level { get; }
    public WDMatchWeaponSlotKind Kind { get; }
    public int SlotIndex { get; }

    public WDMatchWeaponSlotSnapshot(
        string weaponId, int level, WDMatchWeaponSlotKind kind, int slotIndex)
    {
        WeaponId = weaponId ?? string.Empty;
        Level = level;
        Kind = kind;
        SlotIndex = slotIndex;
    }
}

public sealed class WDMatchWeaponLoadoutSnapshot
{
    // ============================================================
    // 01. DTO IMUTÁVEL DA ARMARIA NO INÍCIO DA PARTIDA
    // ============================================================

    private readonly WDMatchWeaponSlotSnapshot[] mainSlots;
    private readonly WDMatchWeaponSlotSnapshot[] supportSlots;
    private readonly WDMatchWeaponSlotSnapshot[] universalWeapons;
    private readonly Dictionary<string, int> effectiveLevels;

    public string DeckId { get; }
    public WDMatchWeaponSnapshotStatus Status { get; }
    public IReadOnlyList<WDMatchWeaponSlotSnapshot> MainSlots =>
        Array.AsReadOnly(mainSlots);
    public IReadOnlyList<WDMatchWeaponSlotSnapshot> SupportSlots =>
        Array.AsReadOnly(supportSlots);
    public IReadOnlyList<WDMatchWeaponSlotSnapshot> UniversalWeapons =>
        Array.AsReadOnly(universalWeapons);
    public bool IsValid => Status == WDMatchWeaponSnapshotStatus.Valid;

    public WDMatchWeaponLoadoutSnapshot(
        string deckId, WDMatchWeaponSnapshotStatus status,
        IEnumerable<WDMatchWeaponSlotSnapshot> mainSlots,
        IEnumerable<WDMatchWeaponSlotSnapshot> supportSlots,
        IEnumerable<WDMatchWeaponSlotSnapshot> universalWeapons,
        IEnumerable<KeyValuePair<string, int>> levels)
    {
        DeckId = deckId ?? string.Empty;
        Status = status;
        this.mainSlots = mainSlots?.ToArray() ?? Array.Empty<WDMatchWeaponSlotSnapshot>();
        this.supportSlots = supportSlots?.ToArray() ?? Array.Empty<WDMatchWeaponSlotSnapshot>();
        this.universalWeapons = universalWeapons?.ToArray() ??
            Array.Empty<WDMatchWeaponSlotSnapshot>();
        effectiveLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        if (levels == null)
            return;
        foreach (KeyValuePair<string, int> item in levels)
            if (!string.IsNullOrWhiteSpace(item.Key) && !effectiveLevels.ContainsKey(item.Key))
                effectiveLevels.Add(item.Key, item.Value);
    }

    public IReadOnlyList<string> GetNormalWeaponIds() =>
        mainSlots.Concat(supportSlots).Concat(universalWeapons)
            .Select(item => item.WeaponId).ToArray();

    public IReadOnlyList<string> GetUniversalWeaponIds() =>
        universalWeapons.Select(item => item.WeaponId).ToArray();

    public bool TryGetLevel(string weaponId, out int level) =>
        effectiveLevels.TryGetValue(weaponId ?? string.Empty, out level);
}

public static class WDMatchWeaponSnapshotFactory
{
    public const string UniversalEnergyPlantId = "energy_plant";

    // ============================================================
    // 02. CAPTURA NORMAL 8+2 SEM REFERÊNCIA MUTÁVEL AO PERFIL
    // ============================================================

    public static WDMatchWeaponLoadoutSnapshot CreateNormal(
        string deckId, int deckIndex, WDArmoryCatalogConfig catalog,
        WDArmoryProfile profile)
    {
        if (catalog == null || profile == null)
            return Empty(deckId, WDMatchWeaponSnapshotStatus.SourceUnavailable);
        var levels = CapturePermanentLevels(catalog, profile, false);
        WDMatchWeaponSlotSnapshot central = CaptureUniversal(catalog, profile, levels);
        if (string.IsNullOrWhiteSpace(deckId))
            return WithUniversal(
                deckId, WDMatchWeaponSnapshotStatus.InvalidDeck, central, levels);
        WDSavedWeaponDeck deck = profile.GetDeck(deckIndex);
        if (deck == null)
            return WithUniversal(
                deckId, WDMatchWeaponSnapshotStatus.InvalidDeck, central, levels);
        if (!profile.IsDeckUnlocked(deckIndex))
            return WithUniversal(
                deckId, WDMatchWeaponSnapshotStatus.LockedDeck, central, levels);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var main = CaptureSlots(
            deck.MainWeaponIds, WDMatchWeaponSlotKind.Main, catalog, profile,
            seen, out WDMatchWeaponSnapshotStatus mainStatus);
        var support = CaptureSlots(
            deck.SupportWeaponIds, WDMatchWeaponSlotKind.Support, catalog, profile,
            seen, out WDMatchWeaponSnapshotStatus supportStatus);
        WDMatchWeaponSnapshotStatus status = mainStatus != WDMatchWeaponSnapshotStatus.Valid
            ? mainStatus : supportStatus;
        if (central.Level < 1 && status == WDMatchWeaponSnapshotStatus.Valid)
            status = WDMatchWeaponSnapshotStatus.InvalidLevel;

        return new WDMatchWeaponLoadoutSnapshot(
            deckId, status, main, support, new[] { central }, levels);
    }

    // ============================================================
    // 03. NÍVEIS RANDOM CAPTURADOS E CENTRAL UNIVERSAL
    // ============================================================

    public static WDMatchWeaponLoadoutSnapshot CreateRandomCards(
        WDArmoryCatalogConfig catalog, WDArmoryProfile profile)
    {
        if (catalog == null || profile == null)
            return Empty(string.Empty, WDMatchWeaponSnapshotStatus.SourceUnavailable);
        var levels = CapturePermanentLevels(catalog, profile, true);
        WDMatchWeaponSlotSnapshot central = CaptureUniversal(catalog, profile, levels);
        WDMatchWeaponSnapshotStatus status = central.Level > 0
            ? WDMatchWeaponSnapshotStatus.Valid
            : WDMatchWeaponSnapshotStatus.InvalidLevel;
        return new WDMatchWeaponLoadoutSnapshot(
            string.Empty, status, null, null, new[] { central }, levels);
    }

    private static List<WDMatchWeaponSlotSnapshot> CaptureSlots(
        IReadOnlyList<string> ids, WDMatchWeaponSlotKind kind,
        WDArmoryCatalogConfig catalog, WDArmoryProfile profile,
        HashSet<string> seen, out WDMatchWeaponSnapshotStatus status)
    {
        int expected = kind == WDMatchWeaponSlotKind.Main
            ? WDSavedWeaponDeck.MainSlotCount : WDSavedWeaponDeck.SupportSlotCount;
        var result = new List<WDMatchWeaponSlotSnapshot>(expected);
        status = ids != null && ids.Count == expected
            ? WDMatchWeaponSnapshotStatus.Valid
            : WDMatchWeaponSnapshotStatus.IncompleteDeck;
        for (int i = 0; i < expected; i++)
        {
            string id = ids != null && i < ids.Count ? ids[i] ?? string.Empty : string.Empty;
            WDWeaponDefinition weapon = catalog.FindWeapon(id);
            WDWeaponProgress progress = profile.GetProgress(id);
            int level = progress != null && progress.Obtained ? progress.Level : 0;
            WDMatchWeaponSnapshotStatus itemStatus = ValidateSlot(
                id, weapon, progress, level, kind, catalog, seen);
            if (status == WDMatchWeaponSnapshotStatus.Valid &&
                itemStatus != WDMatchWeaponSnapshotStatus.Valid)
                status = itemStatus;
            result.Add(new WDMatchWeaponSlotSnapshot(id, level, kind, i));
        }
        return result;
    }

    private static WDMatchWeaponSnapshotStatus ValidateSlot(
        string id, WDWeaponDefinition weapon, WDWeaponProgress progress, int level,
        WDMatchWeaponSlotKind kind, WDArmoryCatalogConfig catalog, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(id))
            return WDMatchWeaponSnapshotStatus.IncompleteDeck;
        // 04. A Central de Energia também pode ocupar um slot principal no Arsenal.
        if (weapon == null || (id == UniversalEnergyPlantId && kind != WDMatchWeaponSlotKind.Main) ||
            kind == WDMatchWeaponSlotKind.Support && !catalog.IsSupportEligible(weapon))
            return WDMatchWeaponSnapshotStatus.InvalidIdentity;
        if (!seen.Add(id))
            return WDMatchWeaponSnapshotStatus.DuplicateWeapon;
        if (progress == null || !progress.Obtained)
            return WDMatchWeaponSnapshotStatus.WeaponNotObtained;
        return level >= 1 && level <= weapon.MaximumLevel
            ? WDMatchWeaponSnapshotStatus.Valid
            : WDMatchWeaponSnapshotStatus.InvalidLevel;
    }

    private static Dictionary<string, int> CapturePermanentLevels(
        WDArmoryCatalogConfig catalog, WDArmoryProfile profile, bool temporaryLevelOne)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WDWeaponDefinition weapon in catalog.Weapons)
        {
            if (weapon == null || string.IsNullOrWhiteSpace(weapon.Id))
                continue;
            WDWeaponProgress progress = profile.GetProgress(weapon.Id);
            int level = progress != null && progress.Obtained ? progress.Level :
                temporaryLevelOne ? 1 : 0;
            result[weapon.Id] = level >= 1 && level <= weapon.MaximumLevel ? level : 0;
        }
        return result;
    }

    private static WDMatchWeaponSlotSnapshot CaptureUniversal(
        WDArmoryCatalogConfig catalog, WDArmoryProfile profile,
        IDictionary<string, int> levels)
    {
        WDWeaponDefinition central = catalog.FindWeapon(UniversalEnergyPlantId);
        WDWeaponProgress progress = profile.GetProgress(UniversalEnergyPlantId);
        int level = progress != null && progress.Obtained ? progress.Level : 1;
        if (central == null || level < 1 || level > central.MaximumLevel)
            level = 0;
        levels[UniversalEnergyPlantId] = level;
        return new WDMatchWeaponSlotSnapshot(
            UniversalEnergyPlantId, level, WDMatchWeaponSlotKind.Universal, 0);
    }

    private static WDMatchWeaponLoadoutSnapshot Empty(
        string deckId, WDMatchWeaponSnapshotStatus status) =>
        new(deckId, status, null, null, null, null);

    private static WDMatchWeaponLoadoutSnapshot WithUniversal(
        string deckId, WDMatchWeaponSnapshotStatus status,
        WDMatchWeaponSlotSnapshot central, IDictionary<string, int> levels) =>
        new(deckId, status, null, null, new[] { central }, levels);
}

using System;
using System.Collections.Generic;
using System.Linq;

public enum WDWeaponAvailabilityReason
{
    Available,
    PlayerNotFound,
    WeaponNotFound,
    LoadoutUnavailable,
    NotInLoadout,
    LevelUnavailable,
    InvalidLoadoutIdentity,
    InvalidLevel,
    InsufficientEnergy,
    Cooldown,
    UsageLimit,
    InitialRoundLock,
    AlreadyProgrammedThisPreparation
}

public readonly struct WDWeaponAvailability
{
    public WDWeaponAvailabilityReason Reason { get; }
    public bool IsAvailable => Reason == WDWeaponAvailabilityReason.Available;
    public int Level { get; }
    public int Energy { get; }
    public int EnergyCost { get; }
    public int CooldownRemaining { get; }
    public int Uses { get; }

    public WDWeaponAvailability(
        WDWeaponAvailabilityReason reason, int level = 0, int energy = 0,
        int energyCost = 0, int cooldownRemaining = 0, int uses = 0)
    {
        Reason = reason;
        Level = level;
        Energy = energy;
        EnergyCost = energyCost;
        CooldownRemaining = cooldownRemaining;
        Uses = uses;
    }
}

public interface IWDMatchWeaponLoadoutSource
{
    bool TryGetWeaponIds(string playerId, out IReadOnlyList<string> weaponIds);
}

public interface IWDWeaponLevelSource
{
    bool TryGetLevel(string playerId, string weaponId, out int level);
}

public sealed class WDMatchSetupWeaponLoadoutSource : IWDMatchWeaponLoadoutSource
{
    private readonly WDMatchSetup setup;
    private readonly Func<string, IReadOnlyList<string>> randomHandProvider;

    // ============================================================
    // 01. LOADOUT NORMAL, MÃO RANDOM E CENTRAL UNIVERSAL
    // ============================================================

    public WDMatchSetupWeaponLoadoutSource(
        WDMatchSetup setup,
        Func<string, IReadOnlyList<string>> randomHandProvider)
    {
        this.setup = setup;
        this.randomHandProvider = randomHandProvider;
    }

    public bool TryGetWeaponIds(
        string playerId, out IReadOnlyList<string> weaponIds)
    {
        weaponIds = Array.Empty<string>();
        if (setup == null || setup.Participants.All(player => player.PlayerId != playerId))
            return false;

        WDMatchParticipant participant = setup.Participants.First(
            player => player.PlayerId == playerId);
        WDMatchWeaponLoadoutSnapshot snapshot = participant.WeaponLoadout;
        if (snapshot == null)
            return setup.UsesSymmetricRandomCards && TryLegacyRandomHand(
                playerId, out weaponIds);
        if (snapshot.Status == WDMatchWeaponSnapshotStatus.SourceUnavailable)
            return false;

        if (!setup.UsesSymmetricRandomCards)
        {
            weaponIds = snapshot.GetNormalWeaponIds();
            return true;
        }

        weaponIds = (randomHandProvider?.Invoke(playerId) ?? Array.Empty<string>())
            .Concat(snapshot.GetUniversalWeaponIds())
            .Distinct(StringComparer.Ordinal).ToArray();
        return true;
    }

    private bool TryLegacyRandomHand(
        string playerId, out IReadOnlyList<string> weaponIds)
    {
        weaponIds = randomHandProvider?.Invoke(playerId) ?? Array.Empty<string>();
        return true;
    }
}

public sealed class WDMatchSetupWeaponLevelSource : IWDWeaponLevelSource
{
    private readonly WDMatchSetup setup;

    public WDMatchSetupWeaponLevelSource(WDMatchSetup setup) => this.setup = setup;

    public bool TryGetLevel(string playerId, string weaponId, out int level)
    {
        level = 0;
        WDMatchParticipant participant = setup?.Participants.FirstOrDefault(
            item => item.PlayerId == playerId);
        return participant?.WeaponLoadout != null &&
            participant.WeaponLoadout.TryGetLevel(weaponId, out level);
    }
}

public sealed class WDUnavailableWeaponLevelSource : IWDWeaponLevelSource
{
    public bool TryGetLevel(string playerId, string weaponId, out int level)
    {
        level = 0;
        return false;
    }
}

public sealed class WDWeaponMatchRuntime
{
    private readonly WDArmoryCatalogConfig catalog;
    private readonly IWDMatchWeaponLoadoutSource loadoutSource;
    private readonly IWDWeaponLevelSource levelSource;
    private readonly Dictionary<string, WDWeaponPlayerRuntimeState> players =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> loadouts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> unresolvedLevels =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> invalidLevels =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> invalidLoadoutIds =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> unresolvedLoadouts = new(StringComparer.Ordinal);

    // ============================================================
    // 02. SESSÃO VOLÁTIL SEPARADA DA PROGRESSÃO PERMANENTE
    // ============================================================

    public IReadOnlyCollection<WDWeaponPlayerRuntimeState> Players => players.Values;

    public WDWeaponMatchRuntime(
        WDArmoryCatalogConfig catalog, IEnumerable<string> playerIds,
        IWDMatchWeaponLoadoutSource loadoutSource,
        IWDWeaponLevelSource levelSource)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.loadoutSource = loadoutSource ??
            throw new ArgumentNullException(nameof(loadoutSource));
        this.levelSource = levelSource ??
            throw new ArgumentNullException(nameof(levelSource));

        foreach (string playerId in playerIds ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(playerId) || players.ContainsKey(playerId))
                continue;
            players.Add(playerId, new WDWeaponPlayerRuntimeState(playerId, 0));
            loadouts.Add(playerId, new HashSet<string>(StringComparer.Ordinal));
            unresolvedLevels.Add(playerId, new HashSet<string>(StringComparer.Ordinal));
            invalidLevels.Add(playerId, new HashSet<string>(StringComparer.Ordinal));
            invalidLoadoutIds.Add(playerId, new HashSet<string>(StringComparer.Ordinal));
        }
    }

    public WDWeaponPlayerRuntimeState FindPlayer(string playerId) =>
        playerId != null && players.TryGetValue(playerId, out WDWeaponPlayerRuntimeState player)
            ? player : null;

    // ============================================================
    // 03. ATUALIZAÇÃO DO LOADOUT SEM EXECUTAR ARMAS
    // ============================================================

    public void RefreshLoadouts()
    {
        foreach (string playerId in players.Keys)
            RefreshLoadout(playerId);
    }

    public void RefreshLoadout(string playerId)
    {
        WDWeaponPlayerRuntimeState player = FindPlayer(playerId);
        if (player == null)
            return;

        HashSet<string> active = loadouts[playerId];
        HashSet<string> levelsMissing = unresolvedLevels[playerId];
        HashSet<string> levelsInvalid = invalidLevels[playerId];
        HashSet<string> identitiesInvalid = invalidLoadoutIds[playerId];
        active.Clear();
        levelsMissing.Clear();
        levelsInvalid.Clear();
        identitiesInvalid.Clear();

        if (!loadoutSource.TryGetWeaponIds(playerId, out IReadOnlyList<string> ids))
        {
            unresolvedLoadouts.Add(playerId);
            foreach (WDWeaponRuntimeEntry entry in player.Weapons)
                entry.SetAvailable(false);
            return;
        }

        unresolvedLoadouts.Remove(playerId);
        foreach (string weaponId in ids ?? Array.Empty<string>())
        {
            WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
            if (definition == null)
            {
                if (!string.IsNullOrWhiteSpace(weaponId))
                    identitiesInvalid.Add(weaponId);
                continue;
            }
            if (!active.Add(weaponId))
                continue;

            if (!levelSource.TryGetLevel(playerId, weaponId, out int level))
            {
                levelsMissing.Add(weaponId);
                continue;
            }
            if (level < 1 || level > definition.MaximumLevel)
            {
                levelsInvalid.Add(weaponId);
                continue;
            }

            WDWeaponRuntimeEntry entry = player.GetWeapon(weaponId) ??
                player.RegisterWeapon(weaponId, level);
            entry.SetLevel(level, definition.MaximumLevel);
            entry.SetAvailable(true);
        }

        foreach (WDWeaponRuntimeEntry entry in player.Weapons)
            if (!active.Contains(entry.WeaponId) || levelsMissing.Contains(entry.WeaponId) ||
                levelsInvalid.Contains(entry.WeaponId))
                entry.SetAvailable(false);
    }

    // ============================================================
    // 04. ENERGY E COOLDOWN NO CICLO REAL DA SESSÃO
    // ============================================================

    public bool AddEnergy(string playerId, int amount)
    {
        WDWeaponPlayerRuntimeState player = FindPlayer(playerId);
        if (player == null || amount < 0)
            return false;
        player.AddEnergy(amount);
        return true;
    }

    public bool TrySpendEnergy(string playerId, int amount)
    {
        WDWeaponPlayerRuntimeState player = FindPlayer(playerId);
        return player != null && amount >= 0 && player.TrySpendEnergy(amount);
    }

    public void AdvanceCooldownsForRound(int round)
    {
        foreach (WDWeaponPlayerRuntimeState player in players.Values)
            player.AdvanceCooldownsForRound(round);
    }

    public bool ForceCooldown(string playerId, string weaponId, int rounds)
    {
        WDWeaponRuntimeEntry entry = FindPlayer(playerId)?.GetWeapon(weaponId);
        if (entry == null)
            return false;
        entry.StartCooldown(Math.Max(entry.CooldownRemaining, Math.Max(0, rounds)));
        return true;
    }

    public IReadOnlyList<string> GetInvalidLoadoutIds(string playerId) =>
        playerId != null && invalidLoadoutIds.TryGetValue(
            playerId, out HashSet<string> invalid)
            ? invalid.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    // ============================================================
    // 05. DISPONIBILIDADE ESTRUTURADA PARA PREPARATION/UI FUTURA
    // ============================================================

    public WDWeaponAvailability GetAvailability(string playerId, string weaponId)
    {
        WDWeaponPlayerRuntimeState player = FindPlayer(playerId);
        if (player == null)
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.PlayerNotFound);

        if (invalidLoadoutIds[playerId].Contains(weaponId))
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.InvalidLoadoutIdentity);
        WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
        if (definition == null)
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.WeaponNotFound);
        if (unresolvedLoadouts.Contains(playerId))
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.LoadoutUnavailable);
        if (!loadouts[playerId].Contains(weaponId))
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.NotInLoadout);
        if (unresolvedLevels[playerId].Contains(weaponId))
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.LevelUnavailable);
        if (invalidLevels[playerId].Contains(weaponId))
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.InvalidLevel);

        WDWeaponRuntimeEntry entry = player.GetWeapon(weaponId);
        if (entry == null || entry.Level < 1 || entry.Level > definition.MaximumLevel)
            return new WDWeaponAvailability(WDWeaponAvailabilityReason.InvalidLevel);

        WDWeaponAvailabilityReason reason = !entry.Available
            ? WDWeaponAvailabilityReason.NotInLoadout
            : player.Energy < definition.EnergyCost
                ? WDWeaponAvailabilityReason.InsufficientEnergy
                : entry.CooldownRemaining > 0
                    ? WDWeaponAvailabilityReason.Cooldown
                    : definition.UseLimit > 0 && entry.Uses >= definition.UseLimit
                        ? WDWeaponAvailabilityReason.UsageLimit
                        : WDWeaponAvailabilityReason.Available;
        return new WDWeaponAvailability(
            reason, entry.Level, player.Energy, definition.EnergyCost,
            entry.CooldownRemaining, entry.Uses);
    }
}

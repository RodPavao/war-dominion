using System;
using System.Collections.Generic;
using System.Linq;

public enum WDWeaponBehaviorType
{
    Specialized,
    Installation,
    AutomaticInstallation,
    Logistics,
    Interference,
    Reinforcement,
    TerrestrialModifier
}

public enum WDWeaponCostTiming
{
    OnExecution,
    OnProgramming
}

public enum WDWeaponResolutionPhase
{
    RoundStart = 0,
    Peace = 10,
    TransferNullifier = 20,
    TerritoryTransfer = 30,
    Interference = 40,
    Reinforcement = 50,
    InstallationAndLogistics = 60,
    Sniper = 70,
    Propaganda = 80,
    ProgrammedOffense = 90,
    AutomaticArtillery = 91,
    Paratroopers = 100,
    TerrestrialActions = 110,
    Consolidation = 120
}

public enum WDWeaponActionState
{
    Programmed,
    Validated,
    Executed,
    Annulled,
    Failed
}

public enum WDWeaponRevalidationOutcome
{
    Valid,
    Impossible,
    Annulled
}

public enum WDWeaponExecutionOutcome
{
    Executed,
    Intercepted,
    FailedAfterLaunch
}

[Serializable]
public sealed class WDWeaponActionParameter
{
    public string Key { get; }
    public string Value { get; }

    public WDWeaponActionParameter(string key, string value)
    {
        Key = key ?? string.Empty;
        Value = value ?? string.Empty;
    }
}

public sealed class WDWeaponAction
{
    // ============================================================
    // 01. INTENÇÃO IMUTÁVEL E ESTADO DE EXECUÇÃO
    // ============================================================

    private readonly List<WDWeaponActionParameter> parameters;

    public string ActionId { get; }
    public string AuthorPlayerId { get; }
    public string WeaponId { get; }
    public int Round { get; }
    public string OriginTerritoryId { get; }
    public string TargetTerritoryId { get; }
    public string TargetPlayerId { get; }
    public string InstallationId { get; }
    public int ProgramOrder { get; }
    public WDWeaponResolutionPhase Phase { get; }
    public WDWeaponActionState State { get; private set; }
    public bool CostCommitted { get; private set; }
    public IReadOnlyList<WDWeaponActionParameter> Parameters => parameters;

    public WDWeaponAction(
        string actionId, string authorPlayerId, string weaponId, int round,
        WDWeaponResolutionPhase phase, int programOrder,
        string originTerritoryId = "", string targetTerritoryId = "",
        string targetPlayerId = "", string installationId = "",
        IEnumerable<WDWeaponActionParameter> parameters = null)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            throw new ArgumentException("ActionId estável é obrigatório.", nameof(actionId));
        ActionId = actionId;
        AuthorPlayerId = authorPlayerId ?? string.Empty;
        WeaponId = weaponId ?? string.Empty;
        Round = Math.Max(0, round);
        Phase = phase;
        ProgramOrder = Math.Max(0, programOrder);
        OriginTerritoryId = originTerritoryId ?? string.Empty;
        TargetTerritoryId = targetTerritoryId ?? string.Empty;
        TargetPlayerId = targetPlayerId ?? string.Empty;
        InstallationId = installationId ?? string.Empty;
        this.parameters = parameters?.ToList() ?? new List<WDWeaponActionParameter>();
        State = WDWeaponActionState.Programmed;
    }

    internal void MarkValidated() => State = WDWeaponActionState.Validated;
    internal void MarkExecuted() => State = WDWeaponActionState.Executed;
    internal void MarkAnnulled() => State = WDWeaponActionState.Annulled;
    internal void MarkFailed() => State = WDWeaponActionState.Failed;
    internal void MarkCostCommitted() => CostCommitted = true;
}

public sealed class WDWeaponRuntimeEntry
{
    public string WeaponId { get; }
    public int Level { get; private set; }
    public int CooldownRemaining { get; private set; }
    public int Uses { get; private set; }
    public bool Available { get; private set; }

    public WDWeaponRuntimeEntry(string weaponId, int level, bool available = true)
    {
        WeaponId = weaponId ?? string.Empty;
        Level = Math.Max(1, level);
        Available = available;
    }

    public void SetLevel(int level, int maximumLevel) =>
        Level = Math.Max(1, Math.Min(Math.Max(1, maximumLevel), level));
    public void StartCooldown(int rounds) => CooldownRemaining = Math.Max(0, rounds);
    public void AdvanceRound() => CooldownRemaining = Math.Max(0, CooldownRemaining - 1);
    public void RecordUse() => Uses++;
    public void SetAvailable(bool available) => Available = available;
}

public sealed class WDWeaponPlayerRuntimeState
{
    // ============================================================
    // 02. ESTADO VOLÁTIL POR JOGADOR, SEPARADO DA PROGRESSÃO
    // ============================================================

    private readonly Dictionary<string, WDWeaponRuntimeEntry> weapons =
        new(StringComparer.Ordinal);
    private readonly List<WDWeaponAction> actions = new();
    private int lastCooldownRound = -1;

    public string PlayerId { get; }
    public int Energy { get; private set; }
    public IReadOnlyList<WDWeaponAction> Actions => actions;
    public IReadOnlyCollection<WDWeaponRuntimeEntry> Weapons => weapons.Values;
    public int LastCooldownRound => lastCooldownRound;

    public WDWeaponPlayerRuntimeState(string playerId, int initialEnergy = 0)
    {
        PlayerId = playerId ?? string.Empty;
        Energy = Math.Max(0, initialEnergy);
    }

    public WDWeaponRuntimeEntry RegisterWeapon(
        string weaponId, int level, bool available = true)
    {
        var entry = new WDWeaponRuntimeEntry(weaponId, level, available);
        weapons[entry.WeaponId] = entry;
        return entry;
    }

    public WDWeaponRuntimeEntry GetWeapon(string weaponId) =>
        weaponId != null && weapons.TryGetValue(weaponId, out WDWeaponRuntimeEntry entry)
            ? entry
            : null;

    public void AddEnergy(int amount) => Energy = Math.Max(0, Energy + Math.Max(0, amount));

    public bool TrySpendEnergy(int amount)
    {
        int safeAmount = Math.Max(0, amount);
        if (Energy < safeAmount)
            return false;
        Energy -= safeAmount;
        return true;
    }

    public void AddAction(WDWeaponAction action)
    {
        if (action != null && actions.All(item => item.ActionId != action.ActionId))
            actions.Add(action);
    }

    // ============================================================
    // 03. AVANÇO ÚNICO E IDEMPOTENTE DE COOLDOWN POR ROUND
    // ============================================================

    public bool AdvanceCooldownsForRound(int round)
    {
        int normalizedRound = Math.Max(1, round);
        if (normalizedRound <= lastCooldownRound)
            return false;

        if (lastCooldownRound >= 1)
            for (int elapsed = lastCooldownRound; elapsed < normalizedRound; elapsed++)
                foreach (WDWeaponRuntimeEntry weapon in weapons.Values)
                    weapon.AdvanceRound();

        lastCooldownRound = normalizedRound;
        return true;
    }
}

public sealed class WDWeaponInstallationState
{
    // ============================================================
    // 03. ESTADO GENÉRICO PRESERVÁVEL DE INSTALAÇÃO
    // ============================================================

    public string InstallationId { get; }
    public string WeaponId { get; }
    public string TerritoryId { get; private set; }
    public string OwnerPlayerId { get; private set; }
    public string OriginalInstallerPlayerId { get; }
    public int InstalledRound { get; }
    public int LastAutomaticRound { get; private set; } = -1;
    public int Level { get; }
    public bool IsTransportable { get; }
    public int DurationRemaining { get; private set; }
    public int BarrierRemaining { get; private set; }
    public int ChargesRemaining { get; private set; }
    public int PaymentsRemaining { get; private set; }
    public bool IsTemporarilyDisabled { get; private set; }
    public bool IsAbandoned => string.IsNullOrEmpty(OwnerPlayerId);
    public bool IsDestroyed { get; private set; }

    public WDWeaponInstallationState(
        string installationId, string weaponId, string territoryId,
        string ownerPlayerId, int duration, int barrier, int charges, int payments,
        int level = 1, bool isTransportable = false, int installedRound = 0)
    {
        InstallationId = installationId ?? string.Empty;
        WeaponId = weaponId ?? string.Empty;
        TerritoryId = territoryId ?? string.Empty;
        OwnerPlayerId = ownerPlayerId ?? string.Empty;
        OriginalInstallerPlayerId = OwnerPlayerId;
        InstalledRound = Math.Max(0, installedRound);
        DurationRemaining = Math.Max(0, duration);
        BarrierRemaining = Math.Max(0, barrier);
        ChargesRemaining = Math.Max(0, charges);
        PaymentsRemaining = Math.Max(0, payments);
        Level = Math.Max(1, level);
        IsTransportable = isTransportable;
    }

    public void SetTemporarilyDisabled(bool disabled) => IsTemporarilyDisabled = disabled;
    public void Abandon() => OwnerPlayerId = string.Empty;
    public void Capture(string newOwnerPlayerId) => OwnerPlayerId = newOwnerPlayerId ?? string.Empty;
    public void MoveTo(string territoryId) => TerritoryId = territoryId ?? string.Empty;
    public void ConsumeDuration() => DurationRemaining = Math.Max(0, DurationRemaining - 1);
    public void ConsumeCharge() => ChargesRemaining = Math.Max(0, ChargesRemaining - 1);
    public void ConsumePayment() => PaymentsRemaining = Math.Max(0, PaymentsRemaining - 1);
    public void ApplyBarrierDamage(int amount) =>
        BarrierRemaining = Math.Max(0, BarrierRemaining - Math.Max(0, amount));
    public void Destroy() => IsDestroyed = true;
    public bool TryMarkAutomaticRound(int round)
    {
        if (round < 0 || LastAutomaticRound == round)
            return false;
        LastAutomaticRound = round;
        return true;
    }
}

public readonly struct WDWeaponRevalidationResult
{
    public WDWeaponRevalidationOutcome Outcome { get; }
    public string Reason { get; }

    public WDWeaponRevalidationResult(
        WDWeaponRevalidationOutcome outcome, string reason = "")
    {
        Outcome = outcome;
        Reason = reason ?? string.Empty;
    }

    public static WDWeaponRevalidationResult Valid() =>
        new(WDWeaponRevalidationOutcome.Valid);
}

public readonly struct WDWeaponExecutionResult
{
    public WDWeaponExecutionOutcome Outcome { get; }
    public string Detail { get; }

    public WDWeaponExecutionResult(WDWeaponExecutionOutcome outcome, string detail = "")
    {
        Outcome = outcome;
        Detail = detail ?? string.Empty;
    }
}

public sealed class WDWeaponActionResult
{
    public string ActionId { get; }
    public WDWeaponActionState State { get; }
    public WDWeaponExecutionOutcome? ExecutionOutcome { get; }
    public string Detail { get; }

    public WDWeaponActionResult(
        string actionId, WDWeaponActionState state,
        WDWeaponExecutionOutcome? executionOutcome, string detail)
    {
        ActionId = actionId ?? string.Empty;
        State = state;
        ExecutionOutcome = executionOutcome;
        Detail = detail ?? string.Empty;
    }
}

public sealed class WDWeaponCatalogValidationIssue
{
    public string WeaponId { get; }
    public string Message { get; }

    public WDWeaponCatalogValidationIssue(string weaponId, string message)
    {
        WeaponId = weaponId ?? string.Empty;
        Message = message ?? string.Empty;
    }
}

public static class WDWeaponCatalogValidator
{
    // ============================================================
    // 04. VALIDAÇÃO ESTRUTURAL SEM LIMITAR O TAMANHO DO CATÁLOGO
    // ============================================================

    public static IReadOnlyList<WDWeaponCatalogValidationIssue> Validate(
        WDArmoryCatalogConfig catalog)
    {
        var issues = new List<WDWeaponCatalogValidationIssue>();
        if (catalog == null)
        {
            issues.Add(new WDWeaponCatalogValidationIssue("", "Catálogo ausente."));
            return issues;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (WDWeaponDefinition weapon in catalog.Weapons.Where(item => item != null))
        {
            if (string.IsNullOrWhiteSpace(weapon.Id))
                issues.Add(new WDWeaponCatalogValidationIssue("", "Arma sem ID estável."));
            else if (!ids.Add(weapon.Id))
                issues.Add(new WDWeaponCatalogValidationIssue(
                    weapon.Id, "ID de arma duplicado."));

            if (!weapon.HasSupportedLevelCount)
                issues.Add(new WDWeaponCatalogValidationIssue(
                    weapon.Id, "Quantidade de níveis deve ser 1, 4 ou 6."));

            for (int target = 2; target <= weapon.MaximumLevel; target++)
            {
                if (catalog.FindUpgrade(weapon, target) == null)
                    issues.Add(new WDWeaponCatalogValidationIssue(
                        weapon.Id, $"Progressão ausente para o nível {target}."));
            }
        }
        return issues;
    }
}

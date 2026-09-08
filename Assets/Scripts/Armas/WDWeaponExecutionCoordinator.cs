using System;
using System.Collections.Generic;
using System.Linq;

public enum WDIntegratedWeaponExecutionStatus
{
    Executed,
    Invalidated,
    Deferred,
    AlreadyProcessed,
    MutationFailed
}

public sealed class WDIntegratedWeaponExecutionResult
{
    public string ActionId { get; }
    public string WeaponId { get; }
    public WDIntegratedWeaponExecutionStatus Status { get; }
    public WDGameWeaponMutationReason? MutationReason { get; }

    public WDIntegratedWeaponExecutionResult(
        WDWeaponAction action, WDIntegratedWeaponExecutionStatus status,
        WDGameWeaponMutationReason? mutationReason = null)
    {
        ActionId = action?.ActionId ?? string.Empty;
        WeaponId = action?.WeaponId ?? string.Empty;
        Status = status;
        MutationReason = mutationReason;
    }
}

public sealed class WDWeaponExecutionCoordinator
{
    private static readonly HashSet<string> SupportedWeaponIds = new(StringComparer.Ordinal)
    {
        WDSupportWeaponIds.Reinforcements,
        WDSupportWeaponIds.Bunker,
        WDSupportWeaponIds.FlakTower,
        WDStrategicWeaponIds.EnergyPlant
    };

    private readonly WDGameWeaponContext context;
    private readonly WDWeaponMatchRuntime runtime;
    private readonly IWDGameWeaponMutationPort mutations;
    private readonly WDWeaponInstallationRegistry installations;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly Dictionary<string, WDIntegratedWeaponExecutionResult> processed =
        new(StringComparer.Ordinal);

    // ============================================================
    // 01. COORDENADOR INCREMENTAL SOBRE AS FONTES AUTORITATIVAS
    // ============================================================

    public WDWeaponExecutionCoordinator(
        WDGameWeaponContext context, WDWeaponMatchRuntime runtime,
        IWDGameWeaponMutationPort mutations,
        WDWeaponInstallationRegistry installations,
        WDArmoryCatalogConfig catalog)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
        this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<WDIntegratedWeaponExecutionResult> ExecuteFrozenActions(
        IEnumerable<WDWeaponAction> actions)
    {
        return (actions ?? Array.Empty<WDWeaponAction>())
            .Where(action => action != null)
            .OrderBy(action => action.Phase)
            .ThenBy(action => action.ProgramOrder)
            .ThenBy(action => action.ActionId, StringComparer.Ordinal)
            .Select(Execute).ToArray();
    }

    // ============================================================
    // 02. REVALIDAÇÃO FINAL E DESPACHO DAS QUATRO ARMAS
    // ============================================================

    public WDIntegratedWeaponExecutionResult Execute(WDWeaponAction action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (processed.TryGetValue(action.ActionId, out WDIntegratedWeaponExecutionResult prior))
            return new WDIntegratedWeaponExecutionResult(
                action, WDIntegratedWeaponExecutionStatus.AlreadyProcessed,
                prior.MutationReason);
        if (!SupportedWeaponIds.Contains(action.WeaponId))
            return Remember(action, WDIntegratedWeaponExecutionStatus.Deferred);

        WDWeaponDefinition definition = catalog.FindWeapon(action.WeaponId);
        WDWeaponAvailability availability = runtime.GetAvailability(
            action.AuthorPlayerId, action.WeaponId);
        WDGameWeaponTerritoryView target = context.FindTerritory(action.TargetTerritoryId);
        if (definition == null || action.Round != context.CurrentRound ||
            !availability.IsAvailable || target == null ||
            !IsValidTarget(action, target))
        {
            action.MarkFailed();
            return Remember(action, WDIntegratedWeaponExecutionStatus.Invalidated);
        }

        WDGameWeaponMutationResult effect = action.WeaponId switch
        {
            WDSupportWeaponIds.Reinforcements => mutations.AddTroops(
                Mutation(action, "effect"), action.TargetTerritoryId,
                WDSupportWeaponTables.Reinforcement(availability.Level)),
            WDSupportWeaponIds.Bunker => Install(action, availability.Level,
                duration: 0, barrier: WDSupportWeaponTables.BunkerBarrier(availability.Level),
                payments: 0, transportable: false),
            WDSupportWeaponIds.FlakTower => Install(action, availability.Level,
                duration: 3, barrier: 0, payments: 0, transportable: true),
            WDStrategicWeaponIds.EnergyPlant => Install(action, availability.Level,
                duration: 0, barrier: 0, payments: 3, transportable: true),
            _ => default
        };
        if (!effect.Succeeded)
        {
            action.MarkFailed();
            return Remember(action, WDIntegratedWeaponExecutionStatus.MutationFailed,
                effect.Reason);
        }

        CommitCosts(action, definition);
        action.MarkValidated();
        action.MarkExecuted();
        return Remember(action, WDIntegratedWeaponExecutionStatus.Executed);
    }

    private bool IsValidTarget(
        WDWeaponAction action, WDGameWeaponTerritoryView target)
    {
        if (action.WeaponId == WDSupportWeaponIds.Reinforcements)
            return target.State == WDWeaponTerritoryStateKind.Occupied &&
                (target.OwnerPlayerId == action.AuthorPlayerId ||
                 context.AreAllied(action.AuthorPlayerId, target.OwnerPlayerId));

        return target.State == WDWeaponTerritoryStateKind.Occupied &&
            target.OwnerPlayerId == action.AuthorPlayerId &&
            !string.IsNullOrWhiteSpace(action.InstallationId) &&
            installations.GetByTerritory(target.TerritoryId)
                .All(item => item.WeaponId != action.WeaponId);
    }

    // ============================================================
    // 03. INSTALAÇÕES REAIS E COMMIT DE CUSTO APÓS O EFEITO
    // ============================================================

    private WDGameWeaponMutationResult Install(
        WDWeaponAction action, int level, int duration, int barrier,
        int payments, bool transportable)
    {
        var installation = new WDWeaponInstallationState(
            action.InstallationId, action.WeaponId, action.TargetTerritoryId,
            action.AuthorPlayerId, duration, barrier, 0, payments, level,
            transportable, action.Round);
        return mutations.Install(Mutation(action, "effect"), installation);
    }

    private void CommitCosts(WDWeaponAction action, WDWeaponDefinition definition)
    {
        if (definition.EnergyCost > 0)
            mutations.ConsumeEnergy(Mutation(action, "energy"),
                action.AuthorPlayerId, definition.EnergyCost);
        mutations.StartCooldown(Mutation(action, "cooldown"),
            action.AuthorPlayerId, action.WeaponId, definition.CooldownRounds);
        mutations.RegisterUse(Mutation(action, "use"),
            action.AuthorPlayerId, action.WeaponId);
        action.MarkCostCommitted();
    }

    // ============================================================
    // 04. FASE 0: PAGAMENTOS IDPOTENTES DAS CENTRAIS ATIVAS
    // ============================================================

    public int ProcessEnergyPlantPayments(int round)
    {
        int normalizedRound = Math.Max(1, round);
        int paid = 0;
        foreach (WDWeaponInstallationState installation in installations.GetAll()
            .Where(item => item.WeaponId == WDStrategicWeaponIds.EnergyPlant)
            .OrderBy(item => item.InstallationId, StringComparer.Ordinal))
        {
            if (installation.IsDestroyed || installation.IsAbandoned ||
                installation.PaymentsRemaining <= 0 ||
                installation.InstalledRound >= normalizedRound ||
                installation.LastAutomaticRound == normalizedRound)
                continue;

            int amount = WDStrategicWeaponTables.EnergyProduction(installation.Level);
            WDGameWeaponMutationResult result = mutations.AddEnergy(
                $"central:{installation.InstallationId}:round:{normalizedRound}",
                installation.OwnerPlayerId, amount);
            if (!result.Succeeded)
                continue;
            installation.TryMarkAutomaticRound(normalizedRound);
            installation.ConsumePayment();
            paid++;
        }
        return paid;
    }

    private WDIntegratedWeaponExecutionResult Remember(
        WDWeaponAction action, WDIntegratedWeaponExecutionStatus status,
        WDGameWeaponMutationReason? reason = null)
    {
        var result = new WDIntegratedWeaponExecutionResult(action, status, reason);
        processed[action.ActionId] = result;
        return result;
    }

    private static string Mutation(WDWeaponAction action, string suffix) =>
        $"execute:{action.ActionId}:{suffix}";
}

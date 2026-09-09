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
    ,Intercepted
    ,NoValidTargets
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
        ,WDSupportWeaponIds.AirStrike
        ,WDStrategicWeaponIds.LongRangeBomb
        ,WDSpecialWeaponIds.ChemicalBomb
        ,WDTacticalWeaponIds.Kamikaze
        ,WDStrategicWeaponIds.Rockets
        ,WDStrategicWeaponIds.Artillery
    };

    private readonly WDGameWeaponContext context;
    private readonly WDWeaponMatchRuntime runtime;
    private readonly IWDGameWeaponMutationPort mutations;
    private readonly WDWeaponInstallationRegistry installations;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly WDWeaponInstallationLifecycle lifecycle;
    private readonly WDRealWeaponDamageService damage;
    private readonly Dictionary<string, WDIntegratedWeaponExecutionResult> processed =
        new(StringComparer.Ordinal);

    // ============================================================
    // 01. COORDENADOR INCREMENTAL SOBRE AS FONTES AUTORITATIVAS
    // ============================================================

    public WDWeaponExecutionCoordinator(
        WDGameWeaponContext context, WDWeaponMatchRuntime runtime,
        IWDGameWeaponMutationPort mutations,
        WDWeaponInstallationRegistry installations,
        WDArmoryCatalogConfig catalog, IWDWeaponRandomSource random = null)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
        this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        lifecycle = new WDWeaponInstallationLifecycle(installations, mutations);
        damage = new WDRealWeaponDamageService(context, mutations, installations,
            lifecycle, random ?? new WDDeterministicWeaponRandom(0));
    }

    public IReadOnlyList<WDIntegratedWeaponExecutionResult> ExecuteFrozenActions(
        IEnumerable<WDWeaponAction> actions)
    {
        var playerOrder = context.OfficialPlayerOrder.Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        WDIntegratedWeaponExecutionResult[] results = (actions ?? Array.Empty<WDWeaponAction>())
            .Where(action => action != null)
            .OrderBy(action => action.Phase)
            .ThenBy(action => playerOrder.TryGetValue(action.AuthorPlayerId, out int order)
                ? order : int.MaxValue)
            .ThenBy(action => action.ProgramOrder)
            .ThenBy(action => action.ActionId, StringComparer.Ordinal)
            .Select(Execute).ToArray();
        ProcessArtillery(context.CurrentRound);
        return results;
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
        bool rocketLaunch = action.WeaponId == WDStrategicWeaponIds.Rockets &&
            action.Phase == WDWeaponResolutionPhase.ProgrammedOffense;
        WDWeaponAvailability availability = runtime.GetAvailability(action.AuthorPlayerId, action.WeaponId);
        WDGameWeaponTerritoryView target = context.FindTerritory(action.TargetTerritoryId);
        if (definition == null || action.Round != context.CurrentRound ||
            (!rocketLaunch && !availability.IsAvailable) || target == null ||
            !IsValidTarget(action, target))
        {
            action.MarkFailed();
            return Remember(action, WDIntegratedWeaponExecutionStatus.Invalidated);
        }

        if (IsOffensive(action.WeaponId) || rocketLaunch)
            return ExecuteOffensive(action, definition, availability, rocketLaunch);

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
            WDStrategicWeaponIds.Rockets => Install(action, availability.Level,
                duration: 0, barrier: 0, payments: 0, transportable: false, charges: 2),
            WDStrategicWeaponIds.Artillery => Install(action, availability.Level,
                duration: WDStrategicWeaponTables.ArtilleryDuration(availability.Level),
                barrier: 0, payments: 0, transportable: false),
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

        if (IsOffensive(action.WeaponId)) return ValidateOffensive(action, target);
        if (action.WeaponId == WDStrategicWeaponIds.Rockets &&
            action.Phase == WDWeaponResolutionPhase.ProgrammedOffense)
            return ValidateRocketLaunch(action, target);
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
        int payments, bool transportable, int charges = 0)
    {
        var installation = new WDWeaponInstallationState(
            action.InstallationId, action.WeaponId, action.TargetTerritoryId,
            action.AuthorPlayerId, duration, barrier, charges, payments, level,
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
    // 04. LOTE OFENSIVO REAL E LANÇAMENTOS
    // ============================================================

    private WDIntegratedWeaponExecutionResult ExecuteOffensive(
        WDWeaponAction action, WDWeaponDefinition definition,
        WDWeaponAvailability availability, bool rocketLaunch)
    {
        int level = rocketLaunch
            ? installations.Find(action.InstallationId)?.Level ?? 0 : availability.Level;
        if (level <= 0)
            return Invalid(action);
        if (action.WeaponId == WDTacticalWeaponIds.Kamikaze)
        {
            WDGameWeaponMutationResult sacrifice = mutations.RemoveTroops(
                Mutation(action, "launch"), action.OriginTerritoryId, 1);
            if (!sacrifice.Succeeded) return MutationFailed(action, sacrifice.Reason);
        }
        WDWeaponInstallationState rocket = rocketLaunch ? installations.Find(action.InstallationId) : null;
        if (rocketLaunch) rocket.ConsumeCharge();

        int amount = action.WeaponId switch
        {
            WDSupportWeaponIds.AirStrike => WDSupportWeaponTables.AirStrikeDamage(level),
            WDStrategicWeaponIds.LongRangeBomb => WDStrategicWeaponTables.LongRangeBombDamage(level),
            WDSpecialWeaponIds.ChemicalBomb => Math.Max(1, (int)Math.Ceiling(
                context.FindTerritory(action.TargetTerritoryId).Troops *
                WDSpecialWeaponTables.ChemicalRate(level))),
            WDTacticalWeaponIds.Kamikaze => WDTacticalWeaponTables.KamikazeDamage(level),
            WDStrategicWeaponIds.Rockets => WDStrategicWeaponTables.RocketDamage(level),
            _ => 0
        };
        bool chemical = action.WeaponId == WDSpecialWeaponIds.ChemicalBomb;
        WDRealWeaponDamageResult result = damage.Apply(action.ActionId,
            action.TargetTerritoryId, amount, true, !chemical);
        if (rocketLaunch && rocket.ChargesRemaining == 0)
            mutations.RemoveInstallation(Mutation(action, "empty-launcher"), rocket.InstallationId);
        if (!rocketLaunch) CommitCosts(action, definition);
        action.MarkValidated(); action.MarkExecuted();
        return Remember(action, result.Intercepted
            ? WDIntegratedWeaponExecutionStatus.Intercepted
            : WDIntegratedWeaponExecutionStatus.Executed);
    }

    private bool ValidateOffensive(WDWeaponAction action, WDGameWeaponTerritoryView target)
    {
        WDGameWeaponTerritoryView origin = context.FindTerritory(action.OriginTerritoryId);
        if (action.WeaponId == WDStrategicWeaponIds.LongRangeBomb)
            return target.State == WDWeaponTerritoryStateKind.Empty ||
                context.AreOpponents(action.AuthorPlayerId, target.OwnerPlayerId);
        if (origin == null || origin.OwnerPlayerId != action.AuthorPlayerId) return false;
        if (action.WeaponId == WDTacticalWeaponIds.Kamikaze)
            return origin.Troops >= 2 && GraphDistance(origin.TerritoryId,
                target.TerritoryId, 3) <= 3 &&
                context.AreOpponents(action.AuthorPlayerId, target.OwnerPlayerId);
        return context.GetNeighbours(origin.TerritoryId).Contains(target.TerritoryId) &&
            context.AreOpponents(action.AuthorPlayerId, target.OwnerPlayerId);
    }

    private bool ValidateRocketLaunch(WDWeaponAction action, WDGameWeaponTerritoryView target)
    {
        WDWeaponInstallationState rocket = installations.Find(action.InstallationId);
        return rocket != null && rocket.WeaponId == WDStrategicWeaponIds.Rockets &&
            rocket.OwnerPlayerId == action.AuthorPlayerId && rocket.ChargesRemaining > 0 &&
            GraphDistance(rocket.TerritoryId, target.TerritoryId, 3) <= 3 &&
            (target.State == WDWeaponTerritoryStateKind.Empty ||
             context.AreOpponents(action.AuthorPlayerId, target.OwnerPlayerId));
    }

    private int GraphDistance(string origin, string target, int limit)
    {
        if (origin == target) return 0;
        var visited = new HashSet<string>(StringComparer.Ordinal) { origin };
        var queue = new Queue<(string id, int distance)>(); queue.Enqueue((origin, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.distance >= limit) continue;
            foreach (string next in context.GetNeighbours(current.id))
            {
                if (next == target) return current.distance + 1;
                if (visited.Add(next)) queue.Enqueue((next, current.distance + 1));
            }
        }
        return int.MaxValue;
    }

    private static bool IsOffensive(string id) =>
        id == WDSupportWeaponIds.AirStrike || id == WDStrategicWeaponIds.LongRangeBomb ||
        id == WDSpecialWeaponIds.ChemicalBomb || id == WDTacticalWeaponIds.Kamikaze;

    private WDIntegratedWeaponExecutionResult Invalid(WDWeaponAction action)
    { action.MarkFailed(); return Remember(action, WDIntegratedWeaponExecutionStatus.Invalidated); }
    private WDIntegratedWeaponExecutionResult MutationFailed(
        WDWeaponAction action, WDGameWeaponMutationReason reason)
    { action.MarkFailed(); return Remember(action, WDIntegratedWeaponExecutionStatus.MutationFailed, reason); }

    // ============================================================
    // 05. FASE 0: LIFECYCLE, PAGAMENTOS E ARTILHARIA AUTOMÁTICA
    // ============================================================

    public void ProcessRoundStart(int round)
    {
        lifecycle.AdvanceFlakLifecycle(round);
        ProcessEnergyPlantPayments(round);
        ProcessArtillery(round);
    }

    public int ProcessArtillery(int round)
    {
        int operated = 0;
        foreach (WDWeaponInstallationState artillery in installations.GetAll()
            .Where(x => x.WeaponId == WDStrategicWeaponIds.Artillery && !x.IsAbandoned &&
                x.DurationRemaining > 0 && x.InstalledRound <= round)
            .OrderBy(x => x.OwnerPlayerId, StringComparer.Ordinal)
            .ThenBy(x => x.InstallationId, StringComparer.Ordinal).ToArray())
        {
            if (artillery.LastAutomaticRound == round) continue;
            string[] targets = context.GetNeighbours(artillery.TerritoryId)
                .Select(context.FindTerritory).Where(x => x != null &&
                    x.OwnerPlayerId != artillery.OwnerPlayerId &&
                    !context.AreAllied(artillery.OwnerPlayerId, x.OwnerPlayerId))
                .OrderByDescending(x => x.Troops).ThenBy(x => x.TerritoryId, StringComparer.Ordinal)
                .Take(3).Select(x => x.TerritoryId).ToArray();
            if (targets.Length == 0) continue;
            artillery.TryMarkAutomaticRound(round);
            foreach (string target in targets)
                damage.Apply($"artillery:{artillery.InstallationId}:{round}:{target}", target,
                    WDStrategicWeaponTables.ArtilleryDamage(artillery.Level), false, true);
            artillery.ConsumeDuration(); operated++;
            if (artillery.DurationRemaining == 0)
                mutations.RemoveInstallation($"artillery:{artillery.InstallationId}:complete", artillery.InstallationId);
        }
        return operated;
    }

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

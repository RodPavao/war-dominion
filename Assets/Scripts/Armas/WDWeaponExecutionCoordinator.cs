using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WDWeaponRoundRuntime
{
    private readonly Dictionary<int, Dictionary<string, HashSet<string>>> losses = new();
    private readonly Dictionary<string, double> sabotage = new(StringComparer.Ordinal);
    private readonly HashSet<string> transferBlocked = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> terrestrialLimits = new(StringComparer.Ordinal);
    private readonly HashSet<string> blockedOrigins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> reinforcementPool = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<WDDroneDiscovery>> droneRecon = new(StringComparer.Ordinal);
    private readonly HashSet<string> peace = new(StringComparer.Ordinal);
    private readonly HashSet<string> programmedSnipers = new(StringComparer.Ordinal);
    private readonly List<WDMercenaryPayment> mercenaryPayments = new();
    private readonly Dictionary<int, Dictionary<string, WDPreviousRoundTerritoryRecord>> territoryHistory = new();
    private Dictionary<string, (string owner, int troops)> territorySnapshot = new(StringComparer.Ordinal);
    private Dictionary<string, string> ownershipSnapshot = new(StringComparer.Ordinal);
    public int ActiveRound { get; private set; }

    // ============================================================
    // 01. ESTADO AUTORITATIVO TEMPORÁRIO E HISTÓRICO DE ROUND
    // ============================================================
    public void BeginRound(int round)
    {
        int normalized = Math.Max(1, round); if (normalized <= ActiveRound) return;
        ActiveRound = normalized; transferBlocked.Clear();
        terrestrialLimits.Clear(); blockedOrigins.Clear(); droneRecon.Clear(); peace.Clear();
        programmedSnipers.Clear();
    }
    public bool ApplySabotage(string id, double rate)
    { rate=Math.Clamp(rate,0d,1d); if(sabotage.TryGetValue(id,out double old)&&old>=rate)return false;sabotage[id]=rate;return true; }
    public int ApplyNaturalReinforcement(string id,int amount)
    {string key=id??"";double r=sabotage.TryGetValue(key,out double value)?value:0d;sabotage.Remove(key);return Math.Max(0,amount-(int)Math.Floor(Math.Max(0,amount)*r));}
    public bool TryBlockTransfer(string id)=>transferBlocked.Add(id??"");
    public bool IsTransferBlocked(string id)=>transferBlocked.Contains(id??"");
    public void SetTerrestrialLimit(string id,int value)=>terrestrialLimits[id??""]=Math.Max(3,value);
    public int GetTerrestrialLimit(string id)=>terrestrialLimits.TryGetValue(id??"",out int v)?v:3;
    public void BlockOrigin(string id)=>blockedOrigins.Add(id??"");
    public bool IsOriginBlocked(string id)=>blockedOrigins.Contains(id??"");
    public void AddReinforcementPool(string id,int value)=>reinforcementPool[id??""]=GetReinforcementPool(id)+Math.Max(0,value);
    public int GetReinforcementPool(string id)=>reinforcementPool.TryGetValue(id??"",out int v)?v:0;
    public int TakeReinforcementPool(string id)
    {string key=id??"";int value=GetReinforcementPool(key);reinforcementPool.Remove(key);return value;}
    public void RecordOwnershipChange(int round,string territory,string oldOwner,string newOwner)
    {if(string.IsNullOrEmpty(oldOwner)||oldOwner==newOwner)return;if(!losses.TryGetValue(round,out var p))losses[round]=p=new();if(!p.TryGetValue(oldOwner,out var ids))p[oldOwner]=ids=new(StringComparer.Ordinal);ids.Add(territory??"");}
    public int GetTerritoriesLostPreviousRound(string id)=>losses.TryGetValue(ActiveRound-1,out var p)&&p.TryGetValue(id??"",out var ids)?ids.Count:0;
    public void CaptureOwnershipSnapshot(IEnumerable<WDGameWeaponTerritoryView> territories)
    {ownershipSnapshot=(territories??Array.Empty<WDGameWeaponTerritoryView>()).ToDictionary(x=>x.TerritoryId,x=>x.OwnerPlayerId,StringComparer.Ordinal);}
    public void ObserveOwnershipChanges(int round,IEnumerable<WDGameWeaponTerritoryView> territories)
    {foreach(var t in territories??Array.Empty<WDGameWeaponTerritoryView>())if(ownershipSnapshot.TryGetValue(t.TerritoryId,out string oldOwner))RecordOwnershipChange(round,t.TerritoryId,oldOwner,t.OwnerPlayerId);ownershipSnapshot.Clear();}
    public bool StoreDroneRecon(string id,IReadOnlyList<WDDroneDiscovery> value)=>droneRecon.TryAdd(id??"",value??Array.Empty<WDDroneDiscovery>());
    public IReadOnlyList<WDDroneDiscovery> GetDroneRecon(string id)=>droneRecon.TryGetValue(id??"",out var v)?v:Array.Empty<WDDroneDiscovery>();
    public bool EstablishPeace(string a,string b)=>peace.Add(Pair(a,b));
    public bool HasPeace(string a,string b)=>peace.Contains(Pair(a,b));
    public void RegisterProgrammedSniper(string p,string t)=>programmedSnipers.Add((p??"")+"|"+(t??""));
    public bool HasProgrammedSniper(string p,string t)=>programmedSnipers.Contains((p??"")+"|"+(t??""));
    public void AddMercenaryPayments(IEnumerable<WDMercenaryPayment> values)=>mercenaryPayments.AddRange(values);
    public int PayMercenaries(int round){int paid=0;foreach(var p in mercenaryPayments.Where(x=>!x.Paid&&x.DueRound<=round).OrderBy(x=>x.DueRound).ThenBy(x=>x.PlayerId,StringComparer.Ordinal)){AddReinforcementPool(p.PlayerId,p.Amount);p.MarkPaid();paid++;}return paid;}
    public void CaptureTerritorySnapshot(IEnumerable<WDGameWeaponTerritoryView> values)=>territorySnapshot=(values??Array.Empty<WDGameWeaponTerritoryView>()).ToDictionary(x=>x.TerritoryId,x=>(x.OwnerPlayerId,x.Troops),StringComparer.Ordinal);
    public void ObserveTerritoryHistory(int round,IEnumerable<WDGameWeaponTerritoryView> values){var records=new Dictionary<string,WDPreviousRoundTerritoryRecord>(StringComparer.Ordinal);foreach(var t in values??Array.Empty<WDGameWeaponTerritoryView>())if(territorySnapshot.TryGetValue(t.TerritoryId,out var b)){int c=Math.Max(0,b.troops-t.Troops);records[t.TerritoryId]=new WDPreviousRoundTerritoryRecord(t.TerritoryId,b.owner,t.OwnerPlayerId,c,c>0);}territoryHistory[round]=records;territorySnapshot.Clear();}
    public WDPreviousRoundTerritoryRecord FindPreviousRoundRecord(string t)=>territoryHistory.TryGetValue(ActiveRound-1,out var records)&&records.TryGetValue(t??"",out var value)?value:null;
    private static string Pair(string a,string b)=>string.CompareOrdinal(a,b)<=0?(a??"")+"|"+(b??""):(b??"")+"|"+(a??"");
}

public enum WDIntegratedWeaponExecutionStatus
{
    Executed,
    Invalidated,
    Deferred,
    AlreadyProcessed,
    MutationFailed
    ,Intercepted
    ,NoValidTargets
    ,ThresholdFailed
    ,ReconCompleted
    ,ReconCompletedNoAttack
    ,Superseded
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
        ,WDSpecialWeaponIds.AtomicBomb
        ,WDSpecialWeaponIds.Drone
        ,WDTacticalWeaponIds.Sniper
        ,WDSpecialWeaponIds.WarPropaganda
        ,WDStrategicWeaponIds.Sabotage
        ,WDTacticalWeaponIds.TransferNullifier
        ,WDTacticalWeaponIds.Blitzkrieg
        ,WDSupportWeaponIds.ReserveTroops
        ,WDSupportWeaponIds.Minefield
        ,WDStrategicWeaponIds.Peace
        ,WDSupportWeaponIds.AirTransport
        ,WDTacticalWeaponIds.Paratroopers
        ,WDSpecialWeaponIds.Infiltrator
        ,WDSupportWeaponIds.TroopRecovery
        ,WDSupportWeaponIds.Mercenaries
    };

    private readonly WDGameWeaponContext context;
    private readonly WDWeaponMatchRuntime runtime;
    private readonly IWDGameWeaponMutationPort mutations;
    private readonly WDWeaponInstallationRegistry installations;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly WDWeaponInstallationLifecycle lifecycle;
    private readonly WDRealWeaponDamageService damage;
    private readonly WDWeaponRoundRuntime roundRuntime;
    private readonly Dictionary<string, WDIntegratedWeaponExecutionResult> processed =
        new(StringComparer.Ordinal);

    // ============================================================
    // 01. COORDENADOR INCREMENTAL SOBRE AS FONTES AUTORITATIVAS
    // ============================================================

    public WDWeaponExecutionCoordinator(
        WDGameWeaponContext context, WDWeaponMatchRuntime runtime,
        IWDGameWeaponMutationPort mutations,
        WDWeaponInstallationRegistry installations,
        WDArmoryCatalogConfig catalog, IWDWeaponRandomSource random = null,
        WDWeaponRoundRuntime roundRuntime = null)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
        this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.roundRuntime = roundRuntime ?? new WDWeaponRoundRuntime();
        this.roundRuntime.BeginRound(context.CurrentRound);
        lifecycle = new WDWeaponInstallationLifecycle(installations, mutations);
        damage = new WDRealWeaponDamageService(context, mutations, installations,
            lifecycle, random ?? new WDDeterministicWeaponRandom(0));
    }

    public IReadOnlyList<WDIntegratedWeaponExecutionResult> ExecuteFrozenActions(
        IEnumerable<WDWeaponAction> actions)
    {
        var playerOrder = context.OfficialPlayerOrder.Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        WDWeaponAction[] ordered = (actions ?? Array.Empty<WDWeaponAction>())
            .Where(action => action != null)
            .OrderBy(action => action.Phase)
            .ThenBy(action => playerOrder.TryGetValue(action.AuthorPlayerId, out int order)
                ? order : int.MaxValue)
            .ThenBy(action => action.ProgramOrder)
            .ThenBy(action => action.ActionId, StringComparer.Ordinal).ToArray();
        foreach (WDWeaponAction sniper in ordered.Where(x => x.WeaponId == WDTacticalWeaponIds.Sniper))
            roundRuntime.RegisterProgrammedSniper(sniper.AuthorPlayerId, sniper.TargetTerritoryId);
        var sabotageWinners = ordered.Where(x => x.WeaponId == WDStrategicWeaponIds.Sabotage)
            .GroupBy(x => x.TargetPlayerId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x =>
                runtime.FindPlayer(x.AuthorPlayerId)?.GetWeapon(x.WeaponId)?.Level ?? 0)
                .ThenBy(x => playerOrder.TryGetValue(x.AuthorPlayerId, out int order)
                    ? order : int.MaxValue)
                .ThenBy(x => x.ActionId, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        WDIntegratedWeaponExecutionResult[] results = ordered.Select(action =>
            action.WeaponId == WDStrategicWeaponIds.Sabotage &&
            sabotageWinners.TryGetValue(action.TargetPlayerId, out WDWeaponAction winner) &&
            winner != action
                ? Remember(action, WDIntegratedWeaponExecutionStatus.Superseded)
                : Execute(action)).ToArray();
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
        roundRuntime.BeginRound(action.Round);
        if (IsSecondBatch(action.WeaponId))
            return ExecuteSecondBatch(action, definition, availability);
        if (IsFinalBatch(action.WeaponId))
            return ExecuteFinalBatch(action, definition, availability);
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
        WDGameWeaponTerritoryView peaceTarget = context.FindTerritory(action.TargetTerritoryId);
        if (peaceTarget != null && peaceTarget.State == WDWeaponTerritoryStateKind.Occupied &&
            roundRuntime.HasPeace(action.AuthorPlayerId, peaceTarget.OwnerPlayerId))
            return Invalid(action);
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
            !roundRuntime.IsOriginBlocked(rocket.TerritoryId) &&
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
        roundRuntime.BeginRound(round);
        roundRuntime.PayMercenaries(round);
        foreach (WDWeaponInstallationState item in installations.GetAll())
            item.SetTemporarilyDisabled(false);
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
            if (artillery.LastAutomaticRound == round ||
                roundRuntime.IsOriginBlocked(artillery.TerritoryId)) continue;
            string[] targets = context.GetNeighbours(artillery.TerritoryId)
                .Select(context.FindTerritory).Where(x => x != null &&
                    x.OwnerPlayerId != artillery.OwnerPlayerId &&
                    !context.AreAllied(artillery.OwnerPlayerId, x.OwnerPlayerId) &&
                    (x.State != WDWeaponTerritoryStateKind.Occupied ||
                     !roundRuntime.HasPeace(artillery.OwnerPlayerId, x.OwnerPlayerId)))
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

    // ============================================================
    // 06.1. ENTRADA TERRESTRE E CAMPO MINADO AUTORITATIVO
    // ============================================================

    public int PrepareHostileTerrestrialEntry(OrdemTerrestre order, int incoming)
    {
        if (order == null || incoming < 1 || order.Destino == null)
            return incoming;

        string authorId = context.FindTerritory(order.IdOrigem)?.OwnerPlayerId ?? string.Empty;
        string targetOwnerId = context.FindTerritory(order.IdDestino)?.OwnerPlayerId ?? string.Empty;
        if (authorId == targetOwnerId || context.AreAllied(authorId, targetOwnerId))
            return incoming;

        WDWeaponInstallationState mine = installations
            .GetByTerritory(order.IdDestino)
            .FirstOrDefault(item => item.WeaponId == WDSupportWeaponIds.Minefield &&
                !item.IsDestroyed && !item.IsTemporarilyDisabled);
        if (mine == null)
            return incoming;

        int losses = Math.Min(incoming, WDTacticalWeaponTables.MineDamage(mine.Level));
        string operationId = $"mine:terrestrial:{context.CurrentRound}:{order.Jogador}:" +
            $"{order.PosicaoNaFila}:{order.IdOrigem}:{order.IdDestino}";
        WDGameWeaponMutationResult casualty = mutations.RemoveTroops(
            operationId + ":casualties", order.IdOrigem, losses);
        if (!casualty.Succeeded)
            return incoming;

        mutations.RemoveInstallation(operationId + ":consume", mine.InstallationId);
        return incoming - losses;
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

    // ============================================================
    // 06. SEGUNDO LOTE REAL SOBRE O RUNTIME AUTORITATIVO DO ROUND
    // ============================================================
    public WDWeaponRoundRuntime RoundRuntime => roundRuntime;

    private WDIntegratedWeaponExecutionResult ExecuteSecondBatch(
        WDWeaponAction action, WDWeaponDefinition definition,
        WDWeaponAvailability availability)
    {
        string droneMode = action.WeaponId == WDSpecialWeaponIds.Drone
            ? WDSpecialWeaponParameters.Get(action, "mode") : string.Empty;
        bool droneFollowUp = action.WeaponId == WDSpecialWeaponIds.Drone &&
            (droneMode == "attack" || droneMode == "abandon");
        if (definition == null || action.Round != context.CurrentRound ||
            (!droneFollowUp && !availability.IsAvailable)) return Invalid(action);
        int effectiveLevel = droneFollowUp
            ? runtime.FindPlayer(action.AuthorPlayerId)?.GetWeapon(action.WeaponId)?.Level ?? 1
            : availability.Level;

        if (action.WeaponId == WDSpecialWeaponIds.AtomicBomb)
            return ExecuteAtomic(action, definition, effectiveLevel);
        if (action.WeaponId == WDSpecialWeaponIds.Drone)
            return ExecuteDrone(action, definition, effectiveLevel);

        WDGameWeaponTerritoryView target = context.FindTerritory(action.TargetTerritoryId);
        if (action.WeaponId == WDTacticalWeaponIds.Sniper)
        {
            if (!IsEnemy(action.AuthorPlayerId, target)) return Invalid(action);
            damage.Apply(action.ActionId, target.TerritoryId, 1, false, false);
            roundRuntime.BlockOrigin(target.TerritoryId);
        }
        else if (action.WeaponId == WDSpecialWeaponIds.WarPropaganda)
        {
            if (!IsEnemy(action.AuthorPlayerId, target)) return Invalid(action);
            if (target.Troops > WDSpecialWeaponTables.PropagandaLimit(availability.Level))
                return Remember(action, WDIntegratedWeaponExecutionStatus.ThresholdFailed);
            mutations.MakeNeutral(Mutation(action, "neutralize"), target.TerritoryId,
                Math.Max(1, target.Troops / 2));
        }
        else if (action.WeaponId == WDStrategicWeaponIds.Sabotage)
        {
            if (!IsOpponent(action.AuthorPlayerId, action.TargetPlayerId)) return Invalid(action);
            if (roundRuntime.HasPeace(action.AuthorPlayerId, action.TargetPlayerId)) return Invalid(action);
            if (!roundRuntime.ApplySabotage(action.TargetPlayerId,
                WDStrategicWeaponTables.SabotageRate(availability.Level)))
                return Remember(action, WDIntegratedWeaponExecutionStatus.Superseded);
        }
        else if (action.WeaponId == WDTacticalWeaponIds.TransferNullifier)
        {
            if (!IsOpponent(action.AuthorPlayerId, action.TargetPlayerId)) return Invalid(action);
            if (!roundRuntime.TryBlockTransfer(action.TargetPlayerId))
                return Remember(action, WDIntegratedWeaponExecutionStatus.Superseded);
        }
        else if (action.WeaponId == WDTacticalWeaponIds.Blitzkrieg)
            roundRuntime.SetTerrestrialLimit(action.AuthorPlayerId,
                WDTacticalWeaponTables.BlitzkriegLimit(availability.Level));
        else if (action.WeaponId == WDSupportWeaponIds.ReserveTroops)
        {
            int lost = roundRuntime.GetTerritoriesLostPreviousRound(action.AuthorPlayerId);
            int amount = WDSupportWeaponTables.ReserveTroops(availability.Level, lost);
            if (amount <= 0) return Invalid(action);
            roundRuntime.AddReinforcementPool(action.AuthorPlayerId, amount);
        }

        CommitCosts(action, definition); action.MarkValidated(); action.MarkExecuted();
        return Remember(action, WDIntegratedWeaponExecutionStatus.Executed);
    }

    private WDIntegratedWeaponExecutionResult ExecuteAtomic(
        WDWeaponAction action, WDWeaponDefinition definition, int level)
    {
        WDGameWeaponTerritoryView center = context.FindTerritory(action.TargetTerritoryId);
        if (center == null || center.State != WDWeaponTerritoryStateKind.Empty &&
            !context.AreOpponents(action.AuthorPlayerId, center.OwnerPlayerId)) return Invalid(action);
        if (center.State == WDWeaponTerritoryStateKind.Occupied &&
            roundRuntime.HasPeace(action.AuthorPlayerId, center.OwnerPlayerId)) return Invalid(action);
        string[] neighbours = context.GetNeighbours(center.TerritoryId).ToArray();
        WDRealWeaponDamageResult central = damage.Apply(action.ActionId + ":center",
            center.TerritoryId, WDSpecialWeaponTables.AtomicDamage(level), true, true);
        CommitCosts(action, definition); action.MarkValidated(); action.MarkExecuted();
        if (central.Intercepted)
            return Remember(action, WDIntegratedWeaponExecutionStatus.Intercepted);
        foreach (string id in neighbours)
            damage.Apply(action.ActionId + ":collateral:" + id, id, 2, false, true);
        return Remember(action, WDIntegratedWeaponExecutionStatus.Executed);
    }

    private WDIntegratedWeaponExecutionResult ExecuteDrone(
        WDWeaponAction action, WDWeaponDefinition definition, int level)
    {
        string mode = WDSpecialWeaponParameters.Get(action, "mode");
        if (mode == "recon")
        {
            var discoveries = WDSpecialWeaponParameters.List(action, "options")
                .Select(context.FindTerritory).Where(x => x != null)
                .Take(WDSpecialWeaponTables.DroneDiscoveries(level))
                .Select(x => new WDDroneDiscovery(x.TerritoryId,
                    damage.SelectCoveringFlak(x.TerritoryId))).ToArray();
            if (discoveries.Length == 0) return Invalid(action);
            roundRuntime.StoreDroneRecon(action.ActionId, discoveries);
            CommitCosts(action, definition); action.MarkValidated(); action.MarkExecuted();
            return Remember(action, WDIntegratedWeaponExecutionStatus.ReconCompleted);
        }
        string reconId = WDSpecialWeaponParameters.Get(action, "recon");
        if (mode == "abandon")
            return roundRuntime.GetDroneRecon(reconId).Count > 0
                ? Remember(action, WDIntegratedWeaponExecutionStatus.ReconCompletedNoAttack)
                : Invalid(action);
        if (mode != "attack" || !roundRuntime.GetDroneRecon(reconId)
            .Any(x => x.TerritoryId == action.TargetTerritoryId)) return Invalid(action);
        WDGameWeaponTerritoryView target = context.FindTerritory(action.TargetTerritoryId);
        if (target != null && target.State == WDWeaponTerritoryStateKind.Occupied &&
            roundRuntime.HasPeace(action.AuthorPlayerId, target.OwnerPlayerId)) return Invalid(action);
        WDRealWeaponDamageResult result = damage.Apply(action.ActionId,
            action.TargetTerritoryId, WDSpecialWeaponTables.DroneDamage(level), true, false);
        action.MarkValidated(); action.MarkExecuted();
        return Remember(action, result.Intercepted
            ? WDIntegratedWeaponExecutionStatus.Intercepted
            : WDIntegratedWeaponExecutionStatus.Executed);
    }

    private bool IsEnemy(string author, WDGameWeaponTerritoryView target) =>
        target != null && target.State == WDWeaponTerritoryStateKind.Occupied &&
        context.AreOpponents(author, target.OwnerPlayerId);
    private bool IsOpponent(string author, string target) =>
        context.AreOpponents(author, target);
    private static bool IsSecondBatch(string id) =>
        id == WDSpecialWeaponIds.AtomicBomb || id == WDSpecialWeaponIds.Drone ||
        id == WDTacticalWeaponIds.Sniper || id == WDSpecialWeaponIds.WarPropaganda ||
        id == WDStrategicWeaponIds.Sabotage || id == WDTacticalWeaponIds.TransferNullifier ||
        id == WDTacticalWeaponIds.Blitzkrieg || id == WDSupportWeaponIds.ReserveTroops;

    // ============================================================
    // 07. ÚLTIMO LOTE CANÔNICO: LOGÍSTICA, INTERFERÊNCIA E SUPORTE
    // ============================================================
    private WDIntegratedWeaponExecutionResult ExecuteFinalBatch(
        WDWeaponAction action, WDWeaponDefinition definition, WDWeaponAvailability availability)
    {
        if (definition == null || action.Round != context.CurrentRound || !availability.IsAvailable)
            return Invalid(action);
        if (action.WeaponId == WDStrategicWeaponIds.Peace)
        {
            if (!IsOpponent(action.AuthorPlayerId, action.TargetPlayerId)) return Invalid(action);
            if (!roundRuntime.EstablishPeace(action.AuthorPlayerId, action.TargetPlayerId))
                return Remember(action, WDIntegratedWeaponExecutionStatus.Superseded);
        }
        else if (action.WeaponId == WDSupportWeaponIds.Minefield)
        {
            WDGameWeaponTerritoryView target = context.FindTerritory(action.TargetTerritoryId);
            if (target == null || target.OwnerPlayerId != action.AuthorPlayerId ||
                installations.GetByTerritory(target.TerritoryId).Any(x=>x.WeaponId==action.WeaponId)) return Invalid(action);
            var mine = new WDWeaponInstallationState(action.InstallationId, action.WeaponId,
                target.TerritoryId, action.AuthorPlayerId, 0, 0, 1, 0,
                availability.Level, false, action.Round);
            if (!mutations.Install(Mutation(action,"mine"),mine).Succeeded) return Invalid(action);
        }
        else if (action.WeaponId == WDSupportWeaponIds.AirTransport)
        {
            if (!ExecuteTransport(action, availability.Level)) return Invalid(action);
        }
        else if (action.WeaponId == WDTacticalWeaponIds.Paratroopers)
            return ExecuteParatroopers(action, definition, availability.Level);
        else if (action.WeaponId == WDSpecialWeaponIds.Infiltrator)
        {
            WDGameWeaponTerritoryView target=context.FindTerritory(action.TargetTerritoryId);
            if(!IsEnemy(action.AuthorPlayerId,target)||roundRuntime.HasPeace(action.AuthorPlayerId,target.OwnerPlayerId)||
                roundRuntime.HasProgrammedSniper(action.AuthorPlayerId,target.TerritoryId)) return Invalid(action);
            roundRuntime.BlockOrigin(target.TerritoryId);
            foreach(var item in installations.GetByTerritory(target.TerritoryId).Where(x=>x.WeaponId==WDSupportWeaponIds.Bunker||x.WeaponId==WDSupportWeaponIds.Minefield))
                item.SetTemporarilyDisabled(true);
        }
        else if (action.WeaponId == WDSupportWeaponIds.TroopRecovery)
        {
            WDGameWeaponTerritoryView target=context.FindTerritory(action.TargetTerritoryId);
            WDPreviousRoundTerritoryRecord record=roundRuntime.FindPreviousRoundRecord(action.TargetTerritoryId);
            if(target==null||record==null||!record.WasAttacked||record.Casualties<=0||
                record.OwnerAtStart!=action.AuthorPlayerId||record.OwnerAtEnd!=action.AuthorPlayerId||
                target.OwnerPlayerId!=action.AuthorPlayerId) return Invalid(action);
            mutations.AddTroops(Mutation(action,"recovery"),target.TerritoryId,
                Math.Min(record.Casualties,WDSupportWeaponTables.RecoveryLimit(availability.Level)));
        }
        else if (action.WeaponId == WDSupportWeaponIds.Mercenaries)
        {
            if(!string.IsNullOrEmpty(action.TargetPlayerId)&&action.TargetPlayerId!=action.AuthorPlayerId)return Invalid(action);
            int[] payments=WDSupportWeaponTables.MercenaryPayments(availability.Level);
            roundRuntime.AddReinforcementPool(action.AuthorPlayerId,payments[0]);
            roundRuntime.AddMercenaryPayments(new[]{new WDMercenaryPayment(action.AuthorPlayerId,action.Round+1,payments[1]),new WDMercenaryPayment(action.AuthorPlayerId,action.Round+2,payments[2])});
        }
        CommitCosts(action,definition);action.MarkValidated();action.MarkExecuted();
        return Remember(action,WDIntegratedWeaponExecutionStatus.Executed);
    }

    private bool ExecuteTransport(WDWeaponAction action,int level)
    {
        WDGameWeaponTerritoryView origin=context.FindTerritory(action.OriginTerritoryId),target=context.FindTerritory(action.TargetTerritoryId);
        int troops=WDWeaponParameters.Int(action,"troops");
        if(origin==null||target==null||origin.OwnerPlayerId!=action.AuthorPlayerId||target.State!=WDWeaponTerritoryStateKind.Occupied||
            target.OwnerPlayerId!=action.AuthorPlayerId&&!context.AreAllied(action.AuthorPlayerId,target.OwnerPlayerId)||troops<=0||
            troops>WDSupportWeaponTables.AirTransportCapacity(level)||origin.Troops-troops<1)return false;
        WDWeaponInstallationState item=string.IsNullOrEmpty(action.InstallationId)?null:installations.Find(action.InstallationId);
        if(item!=null&&(item.WeaponId!=WDSupportWeaponIds.FlakTower||item.TerritoryId!=origin.TerritoryId||
            installations.GetByTerritory(target.TerritoryId).Any(x=>x.WeaponId==item.WeaponId)))return false;
        if(!string.IsNullOrEmpty(action.InstallationId)&&item==null)return false;
        if(!mutations.RemoveTroops(Mutation(action,"transport-out"),origin.TerritoryId,troops).Succeeded)return false;
        if(!mutations.AddTroops(Mutation(action,"transport-in"),target.TerritoryId,troops).Succeeded)return false;
        if(item!=null)mutations.MoveInstallation(Mutation(action,"transport-installation"),item.InstallationId,target.TerritoryId);
        return true;
    }

    private WDIntegratedWeaponExecutionResult ExecuteParatroopers(WDWeaponAction action,WDWeaponDefinition definition,int level)
    {
        WDGameWeaponTerritoryView target=context.FindTerritory(action.TargetTerritoryId);if(target==null)return Invalid(action);
        bool hostile=target.State==WDWeaponTerritoryStateKind.Empty||target.OwnerPlayerId!=action.AuthorPlayerId&&!context.AreAllied(action.AuthorPlayerId,target.OwnerPlayerId);
        if(target.State==WDWeaponTerritoryStateKind.Occupied&&hostile&&roundRuntime.HasPeace(action.AuthorPlayerId,target.OwnerPlayerId))return Invalid(action);
        WDRealWeaponDamageResult interception=damage.Apply(action.ActionId+":flak",target.TerritoryId,0,true,false);
        CommitCosts(action,definition);action.MarkValidated();action.MarkExecuted();
        if(interception.Intercepted)return Remember(action,WDIntegratedWeaponExecutionStatus.Intercepted);
        int incoming=WDTacticalWeaponTables.ParatrooperCount(level);
        WDWeaponInstallationState mine=installations.GetByTerritory(target.TerritoryId).FirstOrDefault(x=>x.WeaponId==WDSupportWeaponIds.Minefield&&!x.IsTemporarilyDisabled);
        if(hostile&&mine!=null){incoming=Math.Max(0,incoming-WDTacticalWeaponTables.MineDamage(mine.Level));mutations.RemoveInstallation(Mutation(action,"mine-trigger"),mine.InstallationId);}
        if(incoming<=0)return Remember(action,WDIntegratedWeaponExecutionStatus.Executed);
        target=context.FindTerritory(target.TerritoryId);
        if(target.State==WDWeaponTerritoryStateKind.Empty)mutations.MakeOccupied(Mutation(action,"occupy"),target.TerritoryId,action.AuthorPlayerId,incoming);
        else if(!hostile)mutations.AddTroops(Mutation(action,"reinforce"),target.TerritoryId,incoming);
        else
        {
            WDWeaponInstallationState bunker=installations.GetByTerritory(target.TerritoryId).FirstOrDefault(x=>x.WeaponId==WDSupportWeaponIds.Bunker&&!x.IsTemporarilyDisabled);
            if(bunker!=null){int absorbed=Math.Min(incoming,bunker.BarrierRemaining);bunker.ApplyBarrierDamage(absorbed);incoming-=absorbed;if(bunker.BarrierRemaining==0)mutations.RemoveInstallation(Mutation(action,"bunker"),bunker.InstallationId);}
            if(incoming>=WDTacticalWeaponTables.MinimumToConquer(target.Troops))mutations.MakeOccupied(Mutation(action,"conquer"),target.TerritoryId,action.AuthorPlayerId,incoming-target.Troops);
            else if(incoming>0)mutations.RemoveTroops(Mutation(action,"combat"),target.TerritoryId,Math.Min(incoming,Math.Max(0,target.Troops-1)));
        }
        return Remember(action,WDIntegratedWeaponExecutionStatus.Executed);
    }

    private static bool IsFinalBatch(string id)=>id==WDSupportWeaponIds.Minefield||id==WDStrategicWeaponIds.Peace||id==WDSupportWeaponIds.AirTransport||id==WDTacticalWeaponIds.Paratroopers||id==WDSpecialWeaponIds.Infiltrator||id==WDSupportWeaponIds.TroopRecovery||id==WDSupportWeaponIds.Mercenaries;
}

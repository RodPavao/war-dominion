using System;
using System.Collections.Generic;
using System.Linq;

public interface IWDWeaponResolutionContext
{
    WDWeaponDefinition FindDefinition(string weaponId);
    WDWeaponPlayerRuntimeState FindPlayer(string playerId);
    int GetPlayerOrder(string playerId);
}

public interface IWDWeaponActionHandler
{
    bool CanHandle(WDWeaponAction action, WDWeaponDefinition definition);
    WDWeaponRevalidationResult Revalidate(
        IWDWeaponResolutionContext context, WDWeaponAction action,
        WDWeaponDefinition definition);
    WDWeaponExecutionResult Execute(
        IWDWeaponResolutionContext context, WDWeaponAction action,
        WDWeaponDefinition definition);
}

public interface IWDWeaponMultiPhaseHandler
{
    bool IsPhaseAllowed(WDWeaponResolutionPhase phase);
}

public interface IWDWeaponActionCostPolicy
{
    bool UsesActivationCost(WDWeaponAction action);
}

public interface IWDWeaponPhaseHandler
{
    WDWeaponResolutionPhase Phase { get; }
    int Order { get; }
    string StableId { get; }
    void Execute(IWDWeaponResolutionContext context);
}

public sealed class WDWeaponActionScheduler
{
    // ============================================================
    // 01. PROGRAMAÇÃO E EXCEÇÕES DE CUSTO ANTECIPADO
    // ============================================================

    public bool TrySchedule(
        WDWeaponAction action, WDWeaponDefinition definition,
        WDWeaponPlayerRuntimeState player)
    {
        if (action == null || definition == null || player == null ||
            action.AuthorPlayerId != player.PlayerId ||
            action.WeaponId != definition.Id ||
            !WDWeaponActionValidator.HasRequiredReferences(action, definition) ||
            player.Actions.Any(item => item.ActionId == action.ActionId))
            return false;

        WDWeaponRuntimeEntry entry = player.GetWeapon(definition.Id);
        if (entry == null || !entry.Available || entry.CooldownRemaining > 0 ||
            (definition.UseLimit > 0 && entry.Uses >= definition.UseLimit))
            return false;

        if (definition.CostTiming == WDWeaponCostTiming.OnProgramming)
        {
            if (!CommitCost(definition, player, entry))
                return false;
            action.MarkCostCommitted();
        }

        player.AddAction(action);
        return true;
    }

    internal static bool CommitCost(
        WDWeaponDefinition definition, WDWeaponPlayerRuntimeState player,
        WDWeaponRuntimeEntry entry)
    {
        if (!player.TrySpendEnergy(definition.EnergyCost))
            return false;
        entry.RecordUse();
        entry.StartCooldown(definition.CooldownRounds);
        return true;
    }

    internal static bool CommitRuntimeUse(
        WDWeaponDefinition definition, WDWeaponRuntimeEntry entry)
    {
        if (definition == null || entry == null)
            return false;
        entry.RecordUse();
        entry.StartCooldown(definition.CooldownRounds);
        return true;
    }
}

public static class WDWeaponActionValidator
{
    public static bool HasRequiredReferences(
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        if (action == null || definition == null)
            return false;
        return (!definition.RequiresOriginTerritory ||
                !string.IsNullOrWhiteSpace(action.OriginTerritoryId)) &&
               (!definition.RequiresTargetTerritory ||
                !string.IsNullOrWhiteSpace(action.TargetTerritoryId)) &&
               (!definition.RequiresTargetPlayer ||
                !string.IsNullOrWhiteSpace(action.TargetPlayerId));
    }
}

public sealed class WDWeaponResolutionPipeline
{
    // ============================================================
    // 02. PIPELINE V2 DETERMINÍSTICO E EXTENSÍVEL
    // ============================================================

    private static readonly WDWeaponResolutionPhase[] OrderedPhases =
        Enum.GetValues(typeof(WDWeaponResolutionPhase))
            .Cast<WDWeaponResolutionPhase>()
            .OrderBy(value => (int)value)
            .ToArray();

    private readonly List<IWDWeaponActionHandler> actionHandlers = new();
    private readonly List<IWDWeaponPhaseHandler> phaseHandlers = new();
    private readonly Dictionary<string, WDWeaponActionResult> processedActions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, WDGlobalResolutionResult> processedRounds =
        new(StringComparer.Ordinal);

    public IReadOnlyList<WDWeaponResolutionPhase> Phases => OrderedPhases;
    public bool HasResolvedRound(string resolutionId) =>
        !string.IsNullOrWhiteSpace(resolutionId) && processedRounds.ContainsKey(resolutionId);

    public void RegisterActionHandler(IWDWeaponActionHandler handler)
    {
        if (handler != null && !actionHandlers.Contains(handler))
            actionHandlers.Add(handler);
    }

    public void RegisterPhaseHandler(IWDWeaponPhaseHandler handler)
    {
        if (handler != null && !phaseHandlers.Contains(handler))
            phaseHandlers.Add(handler);
    }

    public IReadOnlyList<WDWeaponActionResult> Resolve(
        IEnumerable<WDWeaponAction> actions, IWDWeaponResolutionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var results = new List<WDWeaponActionResult>();
        List<WDWeaponAction> pending = actions?.Where(item => item != null)
            .OrderBy(item => (int)item.Phase)
            .ThenBy(item => context.GetPlayerOrder(item.AuthorPlayerId))
            .ThenBy(item => item.ProgramOrder)
            .ThenBy(item => item.ActionId, StringComparer.Ordinal)
            .ToList() ?? new List<WDWeaponAction>();

        foreach (WDWeaponResolutionPhase phase in OrderedPhases)
        {
            foreach (IWDWeaponPhaseHandler handler in phaseHandlers
                .Where(item => item.Phase == phase)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.StableId, StringComparer.Ordinal))
                handler.Execute(context);

            foreach (WDWeaponAction action in pending.Where(item => item.Phase == phase))
            {
                if (!processedActions.TryGetValue(
                        action.ActionId, out WDWeaponActionResult result))
                {
                    result = ResolveAction(action, context);
                    processedActions.Add(action.ActionId, result);
                }
                results.Add(result);
            }
        }

        return results;
    }

    // ============================================================
    // 04. AUTORIDADE GLOBAL DE RESOLUÇÃO DO ROUND — V2
    // ============================================================

    public WDGlobalResolutionResult ResolveRound(WDGlobalResolutionRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ResolutionId))
            throw new ArgumentException("ResolutionId estável é obrigatório.", nameof(request));
        if (request.Coordinator == null)
            throw new ArgumentException("Coordenador de armas é obrigatório.", nameof(request));
        if (processedRounds.TryGetValue(request.ResolutionId, out WDGlobalResolutionResult prior))
            return prior;

        WDWeaponAction[] frozen = request.Coordinator.PrepareFrozenActions(request.FrozenActions);
        var entries = new List<WDGlobalResolutionEntry>();
        foreach (WDWeaponResolutionPhase phase in OrderedPhases)
        {
            entries.Add(WDGlobalResolutionEntry.PhaseStarted(phase));
            if (phase == WDWeaponResolutionPhase.RoundStart)
            {
                request.BeginRound?.Invoke();
                request.Coordinator.ProcessRoundStart(request.Round);
            }

            if (phase == WDWeaponResolutionPhase.Reinforcement)
                entries.Add(WDGlobalResolutionEntry.ForSystem(
                    phase, "natural-reinforcements",
                    request.ResolveNaturalReinforcements?.Invoke() ?? 0));

            foreach (WDIntegratedWeaponExecutionResult weaponResult in
                request.Coordinator.ExecutePhase(frozen, phase))
                entries.Add(WDGlobalResolutionEntry.ForWeapon(phase, weaponResult));

            if (phase == WDWeaponResolutionPhase.TerritoryTransfer)
                entries.Add(WDGlobalResolutionEntry.ForSystem(
                    phase, "territory-transfers", request.ResolveTransfers?.Invoke() ?? 0));
            else if (phase == WDWeaponResolutionPhase.AutomaticArtillery)
                entries.Add(WDGlobalResolutionEntry.ForSystem(
                    phase, "automatic-artillery",
                    request.Coordinator.ProcessArtillery(request.Round)));
            else if (phase == WDWeaponResolutionPhase.TerrestrialActions)
                entries.Add(WDGlobalResolutionEntry.ForSystem(
                    phase, "terrestrial-actions", request.ResolveTerrestrial?.Invoke() ?? 0));
            else if (phase == WDWeaponResolutionPhase.Consolidation)
            {
                request.Consolidate?.Invoke();
                entries.Add(WDGlobalResolutionEntry.ForSystem(phase, "consolidation", 1));
            }

            entries.Add(WDGlobalResolutionEntry.PhaseCompleted(phase));
        }

        var result = new WDGlobalResolutionResult(
            request.ResolutionId, request.Round, entries, frozen);
        processedRounds.Add(request.ResolutionId, result);
        return result;
    }

    // ============================================================
    // 03. REVALIDAÇÃO, CUSTO E RESULTADO AUDITÁVEL
    // ============================================================

    private WDWeaponActionResult ResolveAction(
        WDWeaponAction action, IWDWeaponResolutionContext context)
    {
        WDWeaponDefinition definition = context.FindDefinition(action.WeaponId);
        WDWeaponPlayerRuntimeState player = context.FindPlayer(action.AuthorPlayerId);
        IWDWeaponActionHandler handler = definition == null
            ? null
            : actionHandlers.FirstOrDefault(item => item.CanHandle(action, definition));

        if (definition == null || player == null || handler == null)
            return Fail(action, "Definição, jogador ou resolver não disponível.");
        if (!WDWeaponActionValidator.HasRequiredReferences(action, definition))
            return Fail(action, "Referências obrigatórias da ação estão ausentes.");
        if (action.Phase != definition.ResolutionPhase &&
            (handler is not IWDWeaponMultiPhaseHandler multiPhase ||
             !multiPhase.IsPhaseAllowed(action.Phase)))
            return Fail(action, "Fase da ação diverge da definição da arma.");
        WDWeaponRuntimeEntry runtimeEntry = player.GetWeapon(definition.Id);
        bool usesActivationCost = handler is not IWDWeaponActionCostPolicy costPolicy ||
            costPolicy.UsesActivationCost(action);
        if (usesActivationCost && definition.CostTiming == WDWeaponCostTiming.OnProgramming &&
            !action.CostCommitted)
            return Fail(action, "Ação de custo antecipado não foi comprometida na programação.");
        if (runtimeEntry == null || !runtimeEntry.Available ||
            (usesActivationCost && !action.CostCommitted && runtimeEntry.CooldownRemaining > 0) ||
            (usesActivationCost && !action.CostCommitted && definition.UseLimit > 0 &&
             runtimeEntry.Uses >= definition.UseLimit) ||
             (usesActivationCost && !action.CostCommitted && !action.EnergyCommitted &&
              player.Energy < definition.EnergyCost))
            return Fail(action, "Arma indisponível, em cooldown, sem uso ou sem Energy.");

        WDWeaponRevalidationResult validation =
            handler.Revalidate(context, action, definition);
        if (validation.Outcome == WDWeaponRevalidationOutcome.Annulled)
        {
            action.MarkAnnulled();
            return new WDWeaponActionResult(
                action.ActionId, action.State, null, validation.Reason);
        }
        if (validation.Outcome == WDWeaponRevalidationOutcome.Impossible)
            return Fail(action, validation.Reason);

        action.MarkValidated();
        WDWeaponExecutionResult execution = handler.Execute(context, action, definition);

        if (usesActivationCost && !action.CostCommitted)
        {
            bool committed = action.EnergyCommitted
                ? WDWeaponActionScheduler.CommitRuntimeUse(definition, runtimeEntry)
                : WDWeaponActionScheduler.CommitCost(definition, player, runtimeEntry);
            if (!committed)
                return Fail(action, "Energy ou estado runtime insuficiente na execução.");
            action.MarkCostCommitted();
        }

        if (execution.Outcome == WDWeaponExecutionOutcome.FailedAfterLaunch)
            action.MarkFailed();
        else
            action.MarkExecuted();

        return new WDWeaponActionResult(
            action.ActionId, action.State, execution.Outcome, execution.Detail);
    }

    private static WDWeaponActionResult Fail(WDWeaponAction action, string reason)
    {
        action.MarkFailed();
        return new WDWeaponActionResult(action.ActionId, action.State, null, reason);
    }
}

public sealed class WDGlobalResolutionRequest
{
    // ============================================================
    // 05. ENTRADAS CONGELADAS E PORTAS PARA O ESTADO VIVO
    // ============================================================

    public string ResolutionId { get; }
    public int Round { get; }
    public IReadOnlyList<WDWeaponAction> FrozenActions { get; }
    public WDWeaponExecutionCoordinator Coordinator { get; }
    public Action BeginRound { get; }
    public Func<int> ResolveTransfers { get; }
    public Func<int> ResolveNaturalReinforcements { get; }
    public Func<int> ResolveTerrestrial { get; }
    public Action Consolidate { get; }

    public WDGlobalResolutionRequest(
        string resolutionId, int round, IEnumerable<WDWeaponAction> frozenActions,
        WDWeaponExecutionCoordinator coordinator, Action beginRound = null,
        Func<int> resolveTransfers = null,
        Func<int> resolveNaturalReinforcements = null,
        Func<int> resolveTerrestrial = null,
        Action consolidate = null)
    {
        ResolutionId = resolutionId ?? string.Empty;
        Round = Math.Max(1, round);
        FrozenActions = (frozenActions ?? Array.Empty<WDWeaponAction>()).ToArray();
        Coordinator = coordinator;
        BeginRound = beginRound;
        ResolveTransfers = resolveTransfers;
        ResolveNaturalReinforcements = resolveNaturalReinforcements;
        ResolveTerrestrial = resolveTerrestrial;
        Consolidate = consolidate;
    }
}

public enum WDGlobalResolutionEntryKind
{
    PhaseStarted,
    WeaponAction,
    SystemOperation,
    PhaseCompleted
}

public sealed class WDGlobalResolutionEntry
{
    // ============================================================
    // 06. LOG TÉCNICO ESTRUTURADO PARA TESTES E VFX FUTURO
    // ============================================================

    public WDWeaponResolutionPhase Phase { get; }
    public WDGlobalResolutionEntryKind Kind { get; }
    public string StableId { get; }
    public string AuthorPlayerId { get; }
    public string WeaponId { get; }
    public WDIntegratedWeaponExecutionStatus? WeaponStatus { get; }
    public int AffectedCount { get; }

    private WDGlobalResolutionEntry(
        WDWeaponResolutionPhase phase, WDGlobalResolutionEntryKind kind,
        string stableId, string authorPlayerId = "", string weaponId = "",
        WDIntegratedWeaponExecutionStatus? status = null, int affectedCount = 0)
    {
        Phase = phase;
        Kind = kind;
        StableId = stableId ?? string.Empty;
        AuthorPlayerId = authorPlayerId ?? string.Empty;
        WeaponId = weaponId ?? string.Empty;
        WeaponStatus = status;
        AffectedCount = affectedCount;
    }

    public static WDGlobalResolutionEntry PhaseStarted(WDWeaponResolutionPhase phase) =>
        new(phase, WDGlobalResolutionEntryKind.PhaseStarted, $"phase:{(int)phase}:start");
    public static WDGlobalResolutionEntry PhaseCompleted(WDWeaponResolutionPhase phase) =>
        new(phase, WDGlobalResolutionEntryKind.PhaseCompleted, $"phase:{(int)phase}:complete");
    public static WDGlobalResolutionEntry ForWeapon(
        WDWeaponResolutionPhase phase, WDIntegratedWeaponExecutionResult result) =>
        new(phase, WDGlobalResolutionEntryKind.WeaponAction, result.ActionId,
            result.AuthorPlayerId, result.WeaponId, result.Status);
    public static WDGlobalResolutionEntry ForSystem(
        WDWeaponResolutionPhase phase, string stableId, int affectedCount) =>
        new(phase, WDGlobalResolutionEntryKind.SystemOperation, stableId,
            affectedCount: affectedCount);
}

public sealed class WDGlobalResolutionResult
{
    // ============================================================
    // 07. RESULTADO IMUTÁVEL E IDEMPOTENTE DO ROUND
    // ============================================================

    public string ResolutionId { get; }
    public int Round { get; }
    public IReadOnlyList<WDGlobalResolutionEntry> Entries { get; }
    public IReadOnlyList<WDWeaponAction> FrozenActions { get; }

    public WDGlobalResolutionResult(
        string resolutionId, int round, IEnumerable<WDGlobalResolutionEntry> entries,
        IEnumerable<WDWeaponAction> frozenActions)
    {
        ResolutionId = resolutionId ?? string.Empty;
        Round = Math.Max(1, round);
        Entries = (entries ?? Array.Empty<WDGlobalResolutionEntry>()).ToArray();
        FrozenActions = (frozenActions ?? Array.Empty<WDWeaponAction>()).ToArray();
    }
}

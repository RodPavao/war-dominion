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

    public IReadOnlyList<WDWeaponResolutionPhase> Phases => OrderedPhases;

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
            (usesActivationCost && !action.CostCommitted && player.Energy < definition.EnergyCost))
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
            if (!WDWeaponActionScheduler.CommitCost(
                    definition, player, runtimeEntry))
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

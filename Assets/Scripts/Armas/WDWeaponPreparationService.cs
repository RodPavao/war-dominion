using System;
using System.Collections.Generic;
using System.Linq;

[Flags]
public enum WDWeaponSelectionFields
{
    None = 0,
    OriginTerritory = 1,
    TargetTerritory = 2,
    TargetPlayer = 4,
    Installation = 8,
    Parameters = 16,
    SpecialFlow = 32
}

public enum WDWeaponIntentKind
{
    Standard,
    Installation,
    RocketLaunch,
    DroneShot
}

public enum WDWeaponTerritoryRelation
{
    Any,
    Own,
    OwnOrAllied,
    Enemy,
    NonAllied
}

public enum WDWeaponPlayerRelation
{
    Opponent,
    AnyOther
}

public enum WDWeaponProgrammingReason
{
    Success,
    PreparationClosed,
    RoundFrozen,
    PlayerNotFound,
    WeaponNotFound,
    WeaponUnavailable,
    ProgrammingUsageLimit,
    MissingOriginTerritory,
    MissingTargetTerritory,
    MissingTargetPlayer,
    MissingInstallation,
    MissingParameters,
    InvalidOriginTerritory,
    InvalidTargetTerritory,
    InvalidTargetPlayer,
    TargetNotAdjacent,
    TargetOutOfRange,
    EmptyTargetNotAllowed,
    DuplicateIntent,
    IrreversibleAction,
    ActionNotFound,
    ActionNotOwned
}

public sealed class WDWeaponSelectionRequirement
{
    public WDWeaponSelectionFields Fields { get; }
    public WDWeaponTerritoryRelation OriginRelation { get; }
    public WDWeaponTerritoryRelation TargetRelation { get; }
    public int MaximumGraphDistance { get; }
    public bool RequiresDirectConnection { get; }
    public bool AllowsEmptyTarget { get; }
    public WDWeaponPlayerRelation PlayerRelation { get; }

    public WDWeaponSelectionRequirement(
        WDWeaponSelectionFields fields,
        WDWeaponTerritoryRelation originRelation = WDWeaponTerritoryRelation.Any,
        WDWeaponTerritoryRelation targetRelation = WDWeaponTerritoryRelation.Any,
        int maximumGraphDistance = 0, bool requiresDirectConnection = false,
        bool allowsEmptyTarget = false,
        WDWeaponPlayerRelation playerRelation = WDWeaponPlayerRelation.Opponent)
    {
        Fields = fields;
        OriginRelation = originRelation;
        TargetRelation = targetRelation;
        MaximumGraphDistance = Math.Max(0, maximumGraphDistance);
        RequiresDirectConnection = requiresDirectConnection;
        AllowsEmptyTarget = allowsEmptyTarget;
        PlayerRelation = playerRelation;
    }
}

public sealed class WDWeaponProgrammingRequest
{
    public string PlayerId { get; }
    public string WeaponId { get; }
    public WDWeaponIntentKind IntentKind { get; }
    public string OriginTerritoryId { get; }
    public string TargetTerritoryId { get; }
    public string TargetPlayerId { get; }
    public string InstallationId { get; }
    public IReadOnlyList<WDWeaponActionParameter> Parameters { get; }

    public WDWeaponProgrammingRequest(
        string playerId, string weaponId,
        WDWeaponIntentKind intentKind = WDWeaponIntentKind.Standard,
        string originTerritoryId = "", string targetTerritoryId = "",
        string targetPlayerId = "", string installationId = "",
        IEnumerable<WDWeaponActionParameter> parameters = null)
    {
        PlayerId = playerId ?? string.Empty;
        WeaponId = weaponId ?? string.Empty;
        IntentKind = intentKind;
        OriginTerritoryId = originTerritoryId ?? string.Empty;
        TargetTerritoryId = targetTerritoryId ?? string.Empty;
        TargetPlayerId = targetPlayerId ?? string.Empty;
        InstallationId = installationId ?? string.Empty;
        Parameters = parameters?.ToArray() ?? Array.Empty<WDWeaponActionParameter>();
    }
}

public readonly struct WDWeaponProgrammingResult
{
    public WDWeaponProgrammingReason Reason { get; }
    public WDWeaponAvailability Availability { get; }
    public WDWeaponAction Action { get; }
    public bool Succeeded => Reason == WDWeaponProgrammingReason.Success;

    public WDWeaponProgrammingResult(
        WDWeaponProgrammingReason reason, WDWeaponAvailability availability,
        WDWeaponAction action = null)
    {
        Reason = reason;
        Availability = availability;
        Action = action;
    }
}

public sealed class WDWeaponProgrammingOption
{
    public string WeaponId { get; }
    public WDWeaponAvailability Availability { get; }
    public WDWeaponSelectionRequirement Requirement { get; }

    public WDWeaponProgrammingOption(
        string weaponId, WDWeaponAvailability availability,
        WDWeaponSelectionRequirement requirement)
    {
        WeaponId = weaponId ?? string.Empty;
        Availability = availability;
        Requirement = requirement;
    }
}

public sealed class WDInfiltratorWeaponTarget
{
    public string PlayerId { get; }
    public string WeaponId { get; }
    public string DisplayName { get; }
    public int Level { get; }

    public WDInfiltratorWeaponTarget(string playerId, string weaponId, string displayName,
        int level)
    {
        PlayerId = playerId ?? string.Empty;
        WeaponId = weaponId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Level = Math.Max(1, level);
    }
}

public static class WDWeaponActionSemantics
{
    public static bool IsPersistentInstallationOperation(WDWeaponAction action)
    {
        if (action == null)
            return false;
        if (action.WeaponId == WDSpecialWeaponIds.Drone)
            return string.Equals(WDSpecialWeaponParameters.Get(action, "mode"), "shot",
                StringComparison.Ordinal);
        return action.WeaponId == WDStrategicWeaponIds.Rockets &&
            action.Phase == WDWeaponResolutionPhase.ProgrammedOffense &&
            !string.IsNullOrWhiteSpace(action.InstallationId);
    }
}

public sealed class WDWeaponPreparationService
{
    private readonly WDWeaponMatchRuntime runtime;
    private readonly WDGameWeaponContext context;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly List<WDWeaponAction> actions = new();
    private readonly Dictionary<string, string> selectedWeapons =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> irreversibleActions = new(StringComparer.Ordinal);
    private readonly HashSet<string> cardProgrammingActionIds = new(StringComparer.Ordinal);
    private int activeRound;
    private int nextProgramOrder;
    private bool preparationOpen;
    private bool roundFrozen;

    // ============================================================
    // 01. CICLO ISOLADO DA PREPARATION, COM RESERVA CANÔNICA DE ENERGY
    // ============================================================

    public int ActiveRound => activeRound;
    public bool IsOpen => preparationOpen && !roundFrozen;

    public WDWeaponPreparationService(
        WDWeaponMatchRuntime runtime, WDGameWeaponContext context,
        WDArmoryCatalogConfig catalog)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public void BeginRound(int round)
    {
        activeRound = Math.Max(1, round);
        nextProgramOrder = 0;
        preparationOpen = true;
        roundFrozen = false;
        selectedWeapons.Clear();
        irreversibleActions.Clear();
        cardProgrammingActionIds.Clear();
    }

    public void CloseWithoutResolution()
    {
        preparationOpen = false;
        roundFrozen = true;
    }

    // ============================================================
    // 02. CONSULTA ESTRUTURADA PARA A UI FUTURA
    // ============================================================

    public WDWeaponProgrammingOption GetOption(string playerId, string weaponId)
    {
        WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
        WDWeaponAvailability availability = runtime.GetAvailability(playerId, weaponId);
        if (weaponId == WDSpecialWeaponIds.Infiltrator && activeRound < 3 &&
            availability.Reason != WDWeaponAvailabilityReason.PlayerNotFound &&
            availability.Reason != WDWeaponAvailabilityReason.WeaponNotFound &&
            availability.Reason != WDWeaponAvailabilityReason.NotInLoadout)
        {
            availability = new WDWeaponAvailability(
                WDWeaponAvailabilityReason.InitialRoundLock, availability.Level,
                availability.Energy, availability.EnergyCost, 3 - activeRound,
                availability.Uses);
        }
        if (availability.IsAvailable && HasCardProgramming(playerId, weaponId))
            availability = new WDWeaponAvailability(
                WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation,
                availability.Level, availability.Energy, availability.EnergyCost,
                availability.CooldownRemaining, availability.Uses);
        return new WDWeaponProgrammingOption(
            weaponId, availability,
            definition != null ? ResolveRequirement(definition, WDWeaponIntentKind.Standard) : null);
    }

    public IReadOnlyList<WDWeaponProgrammingOption> GetOptions(string playerId) =>
        catalog.Weapons.Where(item => item != null)
            .Select(item => GetOption(playerId, item.Id))
            .OrderBy(item => item.WeaponId, StringComparer.Ordinal).ToArray();

    public WDWeaponProgrammingOption SelectWeapon(string playerId, string weaponId)
    {
        WDWeaponProgrammingOption option = GetOption(playerId, weaponId);
        if (context.FindPlayer(playerId) != null && option.Requirement != null)
            selectedWeapons[playerId] = weaponId;
        return option;
    }

    public WDWeaponProgrammingOption GetSelectedOption(string playerId) =>
        playerId != null && selectedWeapons.TryGetValue(playerId, out string weaponId)
            ? GetOption(playerId, weaponId)
            : null;

    public void ClearSelection(string playerId)
    {
        if (playerId != null)
            selectedWeapons.Remove(playerId);
    }

    public WDWeaponSelectionRequirement GetRequirement(
        string weaponId, WDWeaponIntentKind intentKind = WDWeaponIntentKind.Standard)
    {
        WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
        return definition != null ? ResolveRequirement(definition, intentKind) : null;
    }

    public WDWeaponProgrammingReason GetSelectionReason(
        WDWeaponProgrammingRequest request)
    {
        if (request == null)
            return WDWeaponProgrammingReason.WeaponNotFound;
        WDWeaponDefinition definition = catalog.FindWeapon(request.WeaponId);
        return definition == null
            ? WDWeaponProgrammingReason.WeaponNotFound
            : ValidateSelection(request, ResolveRequirement(definition, request.IntentKind));
    }

    public bool IsValidOriginTerritory(
        string playerId, string weaponId, WDWeaponIntentKind intentKind,
        string territoryId)
    {
        WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
        if (definition == null)
            return false;
        WDWeaponSelectionRequirement requirement = ResolveRequirement(definition, intentKind);
        return Has(requirement, WDWeaponSelectionFields.OriginTerritory) &&
            Matches(playerId, context.FindTerritory(territoryId), requirement.OriginRelation);
    }

    public bool IsValidTargetTerritory(
        string playerId, string weaponId, WDWeaponIntentKind intentKind,
        string originTerritoryId, string targetTerritoryId)
    {
        WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
        if (definition == null)
            return false;
        WDWeaponSelectionRequirement requirement = ResolveRequirement(definition, intentKind);
        WDGameWeaponTerritoryView target = context.FindTerritory(targetTerritoryId);
        if (!Has(requirement, WDWeaponSelectionFields.TargetTerritory) || target == null ||
            target.State == WDWeaponTerritoryStateKind.Empty && !requirement.AllowsEmptyTarget ||
            !Matches(playerId, target, requirement.TargetRelation))
            return false;
        if (requirement.RequiresDirectConnection &&
            !context.GetNeighbours(originTerritoryId).Contains(targetTerritoryId))
            return false;
        return requirement.MaximumGraphDistance <= 0 ||
            GraphDistance(originTerritoryId, targetTerritoryId,
                requirement.MaximumGraphDistance) <= requirement.MaximumGraphDistance;
    }

    // ============================================================
    // 03. PROGRAMAÇÃO, CANCELAMENTO E SUBSTITUIÇÃO EXPLÍCITA
    // ============================================================

    public WDWeaponProgrammingResult Program(WDWeaponProgrammingRequest request) =>
        ProgramInternal(request, string.Empty);

    public WDWeaponProgrammingResult Cancel(string playerId, string actionId)
    {
        WDWeaponProgrammingReason reason = GetCancellationReason(playerId, actionId);
        if (reason != WDWeaponProgrammingReason.Success)
            return Failure(reason);
        WDWeaponAction action = actions.First(item => item.ActionId == actionId);
        string installationId = action.InstallationId;
        WDWeaponAction[] removed = actions.Where(item => item.ActionId == action.ActionId ||
            (!string.IsNullOrEmpty(installationId) &&
             item.InstallationId == installationId)).ToArray();
        actions.RemoveAll(item => removed.Contains(item));
        foreach (WDWeaponAction removedAction in removed)
        {
            cardProgrammingActionIds.Remove(removedAction.ActionId);
            RefundProgrammingEnergy(removedAction);
        }
        return new WDWeaponProgrammingResult(
            WDWeaponProgrammingReason.Success,
            runtime.GetAvailability(playerId, action.WeaponId), action);
    }

    // A consulta e o comando compartilham as mesmas regras, sem estado visual paralelo.
    public WDWeaponProgrammingReason GetCancellationReason(string playerId, string actionId)
    {
        if (roundFrozen)
            return WDWeaponProgrammingReason.RoundFrozen;
        if (!preparationOpen)
            return WDWeaponProgrammingReason.PreparationClosed;

        WDWeaponAction action = actions.FirstOrDefault(item => item.ActionId == actionId);
        if (action == null || action.Round != activeRound)
            return WDWeaponProgrammingReason.ActionNotFound;
        if (action.AuthorPlayerId != playerId)
            return WDWeaponProgrammingReason.ActionNotOwned;
        if (irreversibleActions.Contains(action.ActionId))
            return WDWeaponProgrammingReason.IrreversibleAction;
        return WDWeaponProgrammingReason.Success;
    }

    public bool MarkIrreversible(string actionId) =>
        !string.IsNullOrWhiteSpace(actionId) &&
        actions.Any(item => item.ActionId == actionId && item.Round == activeRound) &&
        irreversibleActions.Add(actionId);

    public bool IsIrreversible(string actionId) =>
        !string.IsNullOrWhiteSpace(actionId) && irreversibleActions.Contains(actionId);

    public WDWeaponProgrammingResult Replace(
        string playerId, string actionId, WDWeaponProgrammingRequest replacement)
    {
        WDWeaponAction current = actions.FirstOrDefault(item => item.ActionId == actionId);
        if (current == null || current.Round != activeRound)
            return Failure(WDWeaponProgrammingReason.ActionNotFound);
        if (current.AuthorPlayerId != playerId || replacement == null ||
            replacement.PlayerId != playerId)
            return Failure(WDWeaponProgrammingReason.ActionNotOwned);

        WDWeaponProgrammingResult result = ProgramInternal(replacement, actionId);
        if (result.Succeeded)
        {
            actions.Remove(current);
            cardProgrammingActionIds.Remove(current.ActionId);
            RefundProgrammingEnergy(current);
        }
        return result;
    }

    // ============================================================
    // 04. SNAPSHOT IMUTÁVEL E ORDEM DETERMINÍSTICA
    // ============================================================

    public IReadOnlyList<WDWeaponAction> GetPlayerActions(string playerId, int round) =>
        Order(actions.Where(item => item.Round == round && item.AuthorPlayerId == playerId));

    public IReadOnlyList<WDWeaponAction> GetRoundActions(int round) =>
        Order(actions.Where(item => item.Round == round));

    public IReadOnlyList<WDInfiltratorWeaponTarget> GetEligibleInfiltratorWeapons(
        string authorPlayerId, string opponentPlayerId)
    {
        if (!MatchesPlayer(authorPlayerId, opponentPlayerId, WDWeaponPlayerRelation.Opponent))
            return Array.Empty<WDInfiltratorWeaponTarget>();
        WDWeaponPlayerRuntimeState opponent = runtime.FindPlayer(opponentPlayerId);
        if (opponent == null)
            return Array.Empty<WDInfiltratorWeaponTarget>();
        return opponent.Weapons.Where(item => item.Available)
            .Select(item => new WDInfiltratorWeaponTarget(opponentPlayerId, item.WeaponId,
                catalog.FindWeapon(item.WeaponId)?.DisplayName ?? item.WeaponId,
                item.Level))
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.WeaponId, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<WDWeaponAction> FreezeRound()
    {
        preparationOpen = false;
        roundFrozen = true;
        return Order(actions.Where(item => item.Round == activeRound &&
            item.State == WDWeaponActionState.Programmed));
    }

    private IReadOnlyList<WDWeaponAction> Order(IEnumerable<WDWeaponAction> source)
    {
        var playerOrder = context.OfficialPlayerOrder
            .Select((playerId, index) => new { playerId, index })
            .ToDictionary(item => item.playerId, item => item.index, StringComparer.Ordinal);
        WDWeaponAction[] ordered = source.OrderBy(item => item.Round)
            .ThenBy(item => item.Phase)
            .ThenBy(item => playerOrder.TryGetValue(item.AuthorPlayerId, out int order)
                ? order : int.MaxValue)
            .ThenBy(item => item.ProgramOrder)
            .ThenBy(item => item.ActionId, StringComparer.Ordinal).ToArray();
        return Array.AsReadOnly(ordered);
    }

    // ============================================================
    // 05. VALIDAÇÃO DA INTENÇÃO CONTRA O ESTADO READ-ONLY REAL
    // ============================================================

    private WDWeaponProgrammingResult ProgramInternal(
        WDWeaponProgrammingRequest request, string ignoredActionId)
    {
        if (roundFrozen)
            return Failure(WDWeaponProgrammingReason.RoundFrozen);
        if (!preparationOpen)
            return Failure(WDWeaponProgrammingReason.PreparationClosed);
        if (request == null || context.FindPlayer(request.PlayerId) == null)
            return Failure(WDWeaponProgrammingReason.PlayerNotFound);

        WDWeaponDefinition definition = catalog.FindWeapon(request.WeaponId);
        if (definition == null)
            return Failure(WDWeaponProgrammingReason.WeaponNotFound);
        WDWeaponAvailability availability = runtime.GetAvailability(
            request.PlayerId, request.WeaponId);
        if (request.WeaponId == WDSpecialWeaponIds.Infiltrator && activeRound < 3)
            availability = new WDWeaponAvailability(
                WDWeaponAvailabilityReason.InitialRoundLock, availability.Level,
                availability.Energy, availability.EnergyCost, 3 - activeRound,
                availability.Uses);
        if (!availability.IsAvailable)
            return new WDWeaponProgrammingResult(
                WDWeaponProgrammingReason.WeaponUnavailable, availability);
        int pendingUses = actions.Count(item => item.ActionId != ignoredActionId &&
            item.AuthorPlayerId == request.PlayerId && item.WeaponId == request.WeaponId &&
            item.State == WDWeaponActionState.Programmed);
        WDWeaponSelectionRequirement requirement = ResolveRequirement(
            definition, request.IntentKind);
        WDWeaponProgrammingReason validation = ValidateSelection(request, requirement);
        bool reachedUseLimit = definition.UseLimit > 0 &&
            availability.Uses + pendingUses >= definition.UseLimit;
        bool reachedPreparationLimit = request.IntentKind == WDWeaponIntentKind.Standard &&
            HasCardProgramming(request.PlayerId, request.WeaponId, ignoredActionId);
        if (validation != WDWeaponProgrammingReason.Success &&
            ((!reachedUseLimit && !reachedPreparationLimit) ||
             MustPrecedeProgrammingLimit(validation)))
            return new WDWeaponProgrammingResult(validation, availability);
        if (reachedUseLimit)
            return new WDWeaponProgrammingResult(
                WDWeaponProgrammingReason.ProgrammingUsageLimit, availability);
        if (reachedPreparationLimit)
            return new WDWeaponProgrammingResult(
                WDWeaponProgrammingReason.ProgrammingUsageLimit,
                new WDWeaponAvailability(
                    WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation,
                    availability.Level, availability.Energy, availability.EnergyCost,
                    availability.CooldownRemaining, availability.Uses));
        if (actions.Any(item => item.ActionId != ignoredActionId &&
            item.Round == activeRound && SameIntent(item, request)))
            return new WDWeaponProgrammingResult(
                WDWeaponProgrammingReason.DuplicateIntent, availability);

        int order = nextProgramOrder++;
        string actionId = $"weapon:{activeRound:D3}:{request.PlayerId}:{order:D4}";
        WDWeaponResolutionPhase phase = request.WeaponId == WDSpecialWeaponIds.Drone &&
            request.IntentKind == WDWeaponIntentKind.DroneShot
                ? WDWeaponResolutionPhase.ProgrammedOffense
                : request.WeaponId == WDStrategicWeaponIds.Rockets &&
            request.IntentKind == WDWeaponIntentKind.RocketLaunch
                ? WDWeaponResolutionPhase.ProgrammedOffense
                : definition.ResolutionPhase;
        var action = new WDWeaponAction(
            actionId, request.PlayerId, request.WeaponId, activeRound,
            phase, order, request.OriginTerritoryId, request.TargetTerritoryId,
            request.TargetPlayerId, request.InstallationId, request.Parameters);
        if (!WDWeaponActionSemantics.IsPersistentInstallationOperation(action) &&
            definition.EnergyCost > 0)
        {
            if (!runtime.TrySpendEnergy(request.PlayerId, definition.EnergyCost))
                return new WDWeaponProgrammingResult(
                    WDWeaponProgrammingReason.WeaponUnavailable,
                    runtime.GetAvailability(request.PlayerId, request.WeaponId));
            action.MarkEnergyCommitted();
        }
        actions.Add(action);
        if (request.IntentKind == WDWeaponIntentKind.Standard)
            cardProgrammingActionIds.Add(action.ActionId);
        return new WDWeaponProgrammingResult(
            WDWeaponProgrammingReason.Success, availability, action);
    }

    private void RefundProgrammingEnergy(WDWeaponAction action)
    {
        if (action == null || !action.EnergyCommitted)
            return;
        int amount = catalog.FindWeapon(action.WeaponId)?.EnergyCost ?? 0;
        if (amount > 0 && runtime.AddEnergy(action.AuthorPlayerId, amount))
            action.MarkEnergyRefunded();
    }

    private static bool MustPrecedeProgrammingLimit(
        WDWeaponProgrammingReason reason) => reason ==
            WDWeaponProgrammingReason.TargetOutOfRange ||
        reason == WDWeaponProgrammingReason.InvalidTargetPlayer;

    private WDWeaponProgrammingReason ValidateSelection(
        WDWeaponProgrammingRequest request, WDWeaponSelectionRequirement requirement)
    {
        if (Has(requirement, WDWeaponSelectionFields.OriginTerritory) &&
            string.IsNullOrWhiteSpace(request.OriginTerritoryId))
            return WDWeaponProgrammingReason.MissingOriginTerritory;
        if (Has(requirement, WDWeaponSelectionFields.TargetTerritory) &&
            string.IsNullOrWhiteSpace(request.TargetTerritoryId))
            return WDWeaponProgrammingReason.MissingTargetTerritory;
        if (Has(requirement, WDWeaponSelectionFields.TargetPlayer) &&
            string.IsNullOrWhiteSpace(request.TargetPlayerId))
            return WDWeaponProgrammingReason.MissingTargetPlayer;
        if (Has(requirement, WDWeaponSelectionFields.Installation) &&
            string.IsNullOrWhiteSpace(request.InstallationId))
            return WDWeaponProgrammingReason.MissingInstallation;
        if (Has(requirement, WDWeaponSelectionFields.Parameters) &&
            request.Parameters.Count == 0)
            return WDWeaponProgrammingReason.MissingParameters;

        WDGameWeaponTerritoryView origin = string.IsNullOrEmpty(request.OriginTerritoryId)
            ? null : context.FindTerritory(request.OriginTerritoryId);
        WDGameWeaponTerritoryView target = string.IsNullOrEmpty(request.TargetTerritoryId)
            ? null : context.FindTerritory(request.TargetTerritoryId);
        if (Has(requirement, WDWeaponSelectionFields.OriginTerritory) &&
            !Matches(request.PlayerId, origin, requirement.OriginRelation))
            return WDWeaponProgrammingReason.InvalidOriginTerritory;
        if (Has(requirement, WDWeaponSelectionFields.TargetTerritory))
        {
            if (target == null)
                return WDWeaponProgrammingReason.InvalidTargetTerritory;
            if (target.State == WDWeaponTerritoryStateKind.Empty &&
                !requirement.AllowsEmptyTarget)
                return WDWeaponProgrammingReason.EmptyTargetNotAllowed;
            if (!Matches(request.PlayerId, target, requirement.TargetRelation))
                return WDWeaponProgrammingReason.InvalidTargetTerritory;
        }
        if (Has(requirement, WDWeaponSelectionFields.TargetPlayer) &&
            !MatchesPlayer(request.PlayerId, request.TargetPlayerId,
                requirement.PlayerRelation))
            return WDWeaponProgrammingReason.InvalidTargetPlayer;
        if (requirement.RequiresDirectConnection &&
            !context.GetNeighbours(request.OriginTerritoryId).Contains(request.TargetTerritoryId))
            return WDWeaponProgrammingReason.TargetNotAdjacent;
        if (requirement.MaximumGraphDistance > 0 &&
            GraphDistance(request.OriginTerritoryId, request.TargetTerritoryId,
                requirement.MaximumGraphDistance) > requirement.MaximumGraphDistance)
            return WDWeaponProgrammingReason.TargetOutOfRange;
        return WDWeaponProgrammingReason.Success;
    }

    // ============================================================
    // 06. MATRIZ CANÔNICA DE SELEÇÃO, SEPARADA DOS EFEITOS
    // ============================================================

    private static WDWeaponSelectionRequirement ResolveRequirement(
        WDWeaponDefinition definition, WDWeaponIntentKind intentKind)
    {
        string id = definition.Id;
        if (id == WDTacticalWeaponIds.Radar)
            return Target(WDWeaponTerritoryRelation.NonAllied, true);
        if (id == WDSpecialWeaponIds.Drone && intentKind == WDWeaponIntentKind.DroneShot)
            return new WDWeaponSelectionRequirement(
                WDWeaponSelectionFields.TargetTerritory | WDWeaponSelectionFields.Installation,
                targetRelation: WDWeaponTerritoryRelation.NonAllied, allowsEmptyTarget: true);
        if (id == WDStrategicWeaponIds.Rockets && intentKind == WDWeaponIntentKind.RocketLaunch)
            return new WDWeaponSelectionRequirement(
                WDWeaponSelectionFields.OriginTerritory |
                WDWeaponSelectionFields.TargetTerritory |
                WDWeaponSelectionFields.Installation,
                WDWeaponTerritoryRelation.Own, WDWeaponTerritoryRelation.NonAllied,
                3, false, true);
        if (id == WDSupportWeaponIds.AirStrike || id == WDSpecialWeaponIds.ChemicalBomb)
            return OriginTarget(WDWeaponTerritoryRelation.Enemy, 0, true, false);
        if (id == WDTacticalWeaponIds.Kamikaze)
            return OriginTarget(WDWeaponTerritoryRelation.Enemy, 3, false, false);
        if (id == WDSupportWeaponIds.AirTransport)
            return OriginTarget(WDWeaponTerritoryRelation.OwnOrAllied, 0, false, false,
                WDWeaponSelectionFields.Parameters);
        if (id == WDSupportWeaponIds.Reinforcements)
            return Target(WDWeaponTerritoryRelation.OwnOrAllied, false);
        if (id == WDTacticalWeaponIds.Paratroopers || id == WDSpecialWeaponIds.AtomicBomb)
            return Target(WDWeaponTerritoryRelation.Any, true);
        if (id == WDStrategicWeaponIds.LongRangeBomb)
            return Target(WDWeaponTerritoryRelation.NonAllied, true);
        if (id == WDSpecialWeaponIds.Infiltrator)
            return new WDWeaponSelectionRequirement(
                WDWeaponSelectionFields.TargetPlayer | WDWeaponSelectionFields.Parameters);
        if (id == WDTacticalWeaponIds.Sniper ||
            id == WDSpecialWeaponIds.WarPropaganda)
            return Target(WDWeaponTerritoryRelation.Enemy, false);
        if (id == WDStrategicWeaponIds.Sabotage || id == WDStrategicWeaponIds.Peace ||
            id == WDTacticalWeaponIds.TransferNullifier)
            return new WDWeaponSelectionRequirement(WDWeaponSelectionFields.TargetPlayer);
        if (id == WDSupportWeaponIds.TroopRecovery)
            return Target(WDWeaponTerritoryRelation.Own, false);
        if (definition.IsInstallation)
            return new WDWeaponSelectionRequirement(
                WDWeaponSelectionFields.TargetTerritory |
                WDWeaponSelectionFields.Installation,
                targetRelation: WDWeaponTerritoryRelation.Own);
        if (definition.RequiresOriginTerritory && definition.RequiresTargetTerritory)
            return OriginTarget(WDWeaponTerritoryRelation.Any, definition.Range,
                false, definition.CanTargetEmptyTerritory);
        if (definition.RequiresTargetTerritory)
            return Target(WDWeaponTerritoryRelation.Any, definition.CanTargetEmptyTerritory);
        if (definition.RequiresTargetPlayer)
            return new WDWeaponSelectionRequirement(WDWeaponSelectionFields.TargetPlayer);
        return new WDWeaponSelectionRequirement(WDWeaponSelectionFields.None);
    }

    private static WDWeaponSelectionRequirement OriginTarget(
        WDWeaponTerritoryRelation target, int distance, bool adjacent,
        bool empty, WDWeaponSelectionFields additional = WDWeaponSelectionFields.None) =>
        new(WDWeaponSelectionFields.OriginTerritory |
            WDWeaponSelectionFields.TargetTerritory | additional,
            WDWeaponTerritoryRelation.Own, target, distance, adjacent, empty);

    private static WDWeaponSelectionRequirement Target(
        WDWeaponTerritoryRelation relation, bool empty) =>
        new(WDWeaponSelectionFields.TargetTerritory,
            targetRelation: relation, allowsEmptyTarget: empty);

    private static bool Has(
        WDWeaponSelectionRequirement requirement, WDWeaponSelectionFields field) =>
        (requirement.Fields & field) != 0;

    private bool Matches(
        string authorId, WDGameWeaponTerritoryView territory,
        WDWeaponTerritoryRelation relation)
    {
        if (territory == null)
            return false;
        return relation switch
        {
            WDWeaponTerritoryRelation.Any => true,
            WDWeaponTerritoryRelation.Own => territory.OwnerPlayerId == authorId,
            WDWeaponTerritoryRelation.OwnOrAllied => territory.OwnerPlayerId == authorId ||
                context.AreAllied(authorId, territory.OwnerPlayerId),
            WDWeaponTerritoryRelation.Enemy => territory.State == WDWeaponTerritoryStateKind.Occupied &&
                context.AreOpponents(authorId, territory.OwnerPlayerId),
            WDWeaponTerritoryRelation.NonAllied => territory.OwnerPlayerId != authorId &&
                !context.AreAllied(authorId, territory.OwnerPlayerId),
            _ => false
        };
    }

    private bool MatchesPlayer(
        string authorId, string targetId, WDWeaponPlayerRelation relation) =>
        context.FindPlayer(targetId) != null && targetId != authorId &&
        (relation == WDWeaponPlayerRelation.AnyOther ||
            context.AreOpponents(authorId, targetId));

    private int GraphDistance(string originId, string targetId, int limit)
    {
        if (originId == targetId)
            return 0;
        var visited = new HashSet<string>(StringComparer.Ordinal) { originId };
        var queue = new Queue<(string id, int distance)>();
        queue.Enqueue((originId, 0));
        while (queue.Count > 0)
        {
            (string id, int distance) = queue.Dequeue();
            if (distance >= limit)
                continue;
            foreach (string neighbour in context.GetNeighbours(id))
            {
                if (neighbour == targetId)
                    return distance + 1;
                if (visited.Add(neighbour))
                    queue.Enqueue((neighbour, distance + 1));
            }
        }
        return int.MaxValue;
    }

    private static bool SameIntent(
        WDWeaponAction action, WDWeaponProgrammingRequest request) =>
        action.AuthorPlayerId == request.PlayerId && action.WeaponId == request.WeaponId &&
        action.OriginTerritoryId == request.OriginTerritoryId &&
        action.TargetTerritoryId == request.TargetTerritoryId &&
        action.TargetPlayerId == request.TargetPlayerId &&
        action.InstallationId == request.InstallationId &&
        ParametersEqual(action.Parameters, request.Parameters);

    private bool HasCardProgramming(
        string playerId, string weaponId, string ignoredActionId = "") =>
        actions.Any(item => item.Round == activeRound &&
            item.ActionId != ignoredActionId && item.AuthorPlayerId == playerId &&
            item.WeaponId == weaponId && item.State == WDWeaponActionState.Programmed &&
            cardProgrammingActionIds.Contains(item.ActionId));

    private static bool ParametersEqual(
        IReadOnlyList<WDWeaponActionParameter> left,
        IReadOnlyList<WDWeaponActionParameter> right) =>
        left.Count == right.Count && left.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .Zip(right.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ThenBy(item => item.Value, StringComparer.Ordinal),
                (a, b) => a.Key == b.Key && a.Value == b.Value).All(equal => equal);

    private WDWeaponProgrammingResult Failure(WDWeaponProgrammingReason reason) =>
        new(reason, default);
}

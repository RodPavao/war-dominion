using System;

public abstract class WDTacticalWeaponHandlerBase : IWDWeaponActionHandler
{
    private readonly string weaponId;
    protected WDTacticalWeaponHandlerBase(string weaponId) => this.weaponId = weaponId;
    public bool CanHandle(WDWeaponAction action, WDWeaponDefinition definition) =>
        action != null && definition != null && definition.Id == weaponId;
    public abstract WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext context,
        WDWeaponAction action, WDWeaponDefinition definition);
    public abstract WDWeaponExecutionResult Execute(IWDWeaponResolutionContext context,
        WDWeaponAction action, WDWeaponDefinition definition);
    protected static IWDTacticalWeaponContext Tactical(IWDWeaponResolutionContext context) =>
        context as IWDTacticalWeaponContext ?? throw new InvalidOperationException("Contexto tático ausente.");
    protected static int Level(IWDTacticalWeaponContext context, WDWeaponAction action) =>
        context.FindPlayer(action.AuthorPlayerId)?.GetWeapon(action.WeaponId)?.Level ?? 1;
    protected static WDWeaponRevalidationResult Invalid(string reason) =>
        new(WDWeaponRevalidationOutcome.Impossible, reason);
}

public sealed class WDMinefieldHandler : WDTacticalWeaponHandlerBase
{
    public WDMinefieldHandler() : base(WDTacticalWeaponIds.Minefield) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        return target != null && target.OwnerPlayerId == action.AuthorPlayerId &&
            target.FindInstallation(action.WeaponId) == null
            ? WDWeaponRevalidationResult.Valid() : Invalid("Campo Minado exige território próprio sem outra mina.");
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        context.FindTerritory(action.TargetTerritoryId).AddInstallation(
            new WDWeaponInstallationState(action.InstallationId, action.WeaponId,
                action.TargetTerritoryId, action.AuthorPlayerId, 0, 0, 1, 0,
                Level(context, action), false));
        return new(WDWeaponExecutionOutcome.Executed, "Campo Minado instalado secretamente.");
    }
}

public sealed class WDBlitzkriegHandler : WDTacticalWeaponHandlerBase
{
    public WDBlitzkriegHandler() : base(WDTacticalWeaponIds.Blitzkrieg) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition) => WDWeaponRevalidationResult.Valid();
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        context.SetTerrestrialActionLimit(action.AuthorPlayerId,
            WDTacticalWeaponTables.BlitzkriegLimit(Level(context, action)));
        return new(WDWeaponExecutionOutcome.Executed, "Limite terrestre total ampliado.");
    }
}

public sealed class WDTransferNullifierHandler : WDTacticalWeaponHandlerBase
{
    public WDTransferNullifierHandler() : base(WDTacticalWeaponIds.TransferNullifier) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        if (action.TargetPlayerId == action.AuthorPlayerId || context.FindPlayer(action.TargetPlayerId) == null)
            return Invalid("Anulador exige jogador adversário.");
        return context.AreTransfersBlocked(action.TargetPlayerId)
            ? new(WDWeaponRevalidationOutcome.Annulled, "Outro Anulador válido já prevaleceu.")
            : WDWeaponRevalidationResult.Valid();
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        bool blocked = Tactical(raw).TryBlockTransfers(action.TargetPlayerId, action.ActionId);
        return new(blocked ? WDWeaponExecutionOutcome.Executed : WDWeaponExecutionOutcome.FailedAfterLaunch,
            blocked ? "Transferências bloqueadas neste round." : "Bloqueio já existente.");
    }
}

public sealed class WDSniperHandler : WDTacticalWeaponHandlerBase
{
    public WDSniperHandler() : base(WDTacticalWeaponIds.Sniper) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        return target != null && target.State == WDWeaponTerritoryStateKind.Occupied && target.Troops > 0 &&
            target.OwnerPlayerId != action.AuthorPlayerId && !context.AreAllied(action.AuthorPlayerId, target.OwnerPlayerId)
            ? WDWeaponRevalidationResult.Valid() : Invalid("Sniper exige território inimigo ocupado.");
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        WDWeaponTerritoryState target = Tactical(raw).FindTerritory(action.TargetTerritoryId);
        target.RemoveTroops(1); target.Immobilize();
        if (target.Troops == 0) target.MakeEmpty();
        return new(WDWeaponExecutionOutcome.Executed, "Uma tropa eliminada; território imobilizado.");
    }
}

public sealed class WDKamikazeHandler : WDTacticalWeaponHandlerBase
{
    public WDKamikazeHandler() : base(WDTacticalWeaponIds.Kamikaze) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        WDWeaponTerritoryState origin = context.FindTerritory(action.OriginTerritoryId);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        if (origin == null || target == null || origin.OwnerPlayerId != action.AuthorPlayerId || origin.Troops < 2)
            return Invalid("Origem própria precisa de ao menos duas tropas.");
        if (target.State != WDWeaponTerritoryStateKind.Occupied || target.OwnerPlayerId == action.AuthorPlayerId ||
            context.AreAllied(action.AuthorPlayerId, target.OwnerPlayerId)) return Invalid("Kamikaze exige alvo inimigo.");
        if (context is not IWDWeaponTerritoryEnumerable board ||
            WDTacticalWeaponTables.Distance(board, origin.TerritoryId, target.TerritoryId) > 3)
            return Invalid("Alvo fora do alcance de três conexões.");
        return WDWeaponRevalidationResult.Valid();
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        WDWeaponTerritoryState origin = context.FindTerritory(action.OriginTerritoryId);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        origin.RemoveTroops(1);
        if (WDSupportWeaponInteractionService.TryIntercept(context, action, target))
            return new(WDWeaponExecutionOutcome.Intercepted, "Kamikaze sacrificado e interceptado.");
        int casualties = WDSupportWeaponInteractionService.ApplyDamage(target,
            WDTacticalWeaponTables.KamikazeDamage(Level(context, action)), true);
        return new(WDWeaponExecutionOutcome.Executed, $"Kamikaze sacrificado; {casualties} baixa(s), sem conquista.");
    }
}

public sealed class WDParatroopersHandler : WDTacticalWeaponHandlerBase
{
    public WDParatroopersHandler() : base(WDTacticalWeaponIds.Paratroopers) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition) =>
        Tactical(raw).FindTerritory(action.TargetTerritoryId) != null
            ? WDWeaponRevalidationResult.Valid() : Invalid("Alvo territorial ausente.");
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDTacticalWeaponContext context = Tactical(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        if (WDSupportWeaponInteractionService.TryIntercept(context, action, target))
            return new(WDWeaponExecutionOutcome.Intercepted, "Operação de Paraquedistas interceptada.");
        int count = WDTacticalWeaponTables.ParatrooperCount(Level(context, action));
        bool arrived = WDTacticalWeaponInteractionService.ResolveParatrooperArrival(
            context, target, action.AuthorPlayerId, count);
        return new(arrived ? WDWeaponExecutionOutcome.Executed : WDWeaponExecutionOutcome.FailedAfterLaunch,
            arrived ? "Paraquedistas resolveram conforme o estado atual do alvo." : "Força de Paraquedistas eliminada.");
    }
}

public static class WDTacticalWeaponPipelineExtensions
{
    // ============================================================
    // 03. REGISTRO MODULAR NAS FASES V2 EXISTENTES
    // ============================================================

    public static void RegisterTacticalWeapons(this WDWeaponResolutionPipeline pipeline)
    {
        pipeline.RegisterActionHandler(new WDMinefieldHandler());
        pipeline.RegisterActionHandler(new WDBlitzkriegHandler());
        pipeline.RegisterActionHandler(new WDTransferNullifierHandler());
        pipeline.RegisterActionHandler(new WDSniperHandler());
        pipeline.RegisterActionHandler(new WDKamikazeHandler());
        pipeline.RegisterActionHandler(new WDParatroopersHandler());
    }
}

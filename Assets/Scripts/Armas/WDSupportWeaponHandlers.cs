using System;
using System.Linq;

public abstract class WDSupportWeaponHandlerBase : IWDWeaponActionHandler
{
    private readonly string weaponId;
    protected WDSupportWeaponHandlerBase(string weaponId) => this.weaponId = weaponId;
    public bool CanHandle(WDWeaponAction action, WDWeaponDefinition definition) =>
        action != null && definition != null && definition.Id == weaponId;
    public abstract WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext context,
        WDWeaponAction action, WDWeaponDefinition definition);
    public abstract WDWeaponExecutionResult Execute(IWDWeaponResolutionContext context,
        WDWeaponAction action, WDWeaponDefinition definition);
    protected static IWDSupportWeaponContext Support(IWDWeaponResolutionContext context) =>
        context as IWDSupportWeaponContext ?? throw new InvalidOperationException("Contexto de armas de Apoio ausente.");
    protected static int Level(IWDSupportWeaponContext context, WDWeaponAction action) =>
        context.FindPlayer(action.AuthorPlayerId)?.GetWeapon(action.WeaponId)?.Level ?? 1;
    protected static WDWeaponRevalidationResult Invalid(string reason) =>
        new(WDWeaponRevalidationOutcome.Impossible, reason);
}

public sealed class WDAirStrikeHandler : WDSupportWeaponHandlerBase
{
    public WDAirStrikeHandler() : base(WDSupportWeaponIds.AirStrike) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState origin = context.FindTerritory(action.OriginTerritoryId);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        if (origin == null || target == null || origin.OwnerPlayerId != action.AuthorPlayerId)
            return Invalid("Origem própria ou alvo ausente.");
        if (!origin.Neighbours.Contains(target.TerritoryId)) return Invalid("Alvo não é vizinho direto.");
        if (target.State != WDWeaponTerritoryStateKind.Empty &&
            (target.OwnerPlayerId == action.AuthorPlayerId || context.AreAllied(action.AuthorPlayerId, target.OwnerPlayerId)))
            return Invalid("Ataque Aéreo exige alvo inimigo ou alvo programado já Vazio.");
        return WDWeaponRevalidationResult.Valid();
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        if (WDSupportWeaponInteractionService.TryIntercept(context, action, target))
            return new(WDWeaponExecutionOutcome.Intercepted, "Ataque Aéreo interceptado pela Flak.");
        int casualties = WDSupportWeaponInteractionService.ApplyDamage(target,
            WDSupportWeaponTables.AirStrikeDamage(Level(context, action)), true);
        return new(WDWeaponExecutionOutcome.Executed, $"{casualties} baixa(s); sem conquista.");
    }
}

public sealed class WDDirectReinforcementHandler : WDSupportWeaponHandlerBase
{
    public WDDirectReinforcementHandler() : base(WDSupportWeaponIds.Reinforcements) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        return target != null && target.State == WDWeaponTerritoryStateKind.Occupied &&
            (target.OwnerPlayerId == action.AuthorPlayerId || context.AreAllied(action.AuthorPlayerId, target.OwnerPlayerId))
            ? WDWeaponRevalidationResult.Valid() : Invalid("Alvo deve ser próprio ou aliado ocupado.");
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        int amount = WDSupportWeaponTables.Reinforcement(Level(context, action));
        context.FindTerritory(action.TargetTerritoryId).AddTroops(amount);
        return new(WDWeaponExecutionOutcome.Executed, $"{amount} reforços diretos.");
    }
}

public sealed class WDAirTransportHandler : WDSupportWeaponHandlerBase
{
    public WDAirTransportHandler() : base(WDSupportWeaponIds.AirTransport) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState origin = context.FindTerritory(action.OriginTerritoryId);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        if (origin == null || target == null || origin.OwnerPlayerId != action.AuthorPlayerId ||
            target.State != WDWeaponTerritoryStateKind.Occupied ||
            (target.OwnerPlayerId != action.AuthorPlayerId && !context.AreAllied(action.AuthorPlayerId, target.OwnerPlayerId)))
            return Invalid("Origem própria e destino próprio/aliado são obrigatórios.");
        int amount = WDWeaponParameters.Int(action, "troops");
        if (amount <= 0 || amount > WDSupportWeaponTables.AirTransportCapacity(Level(context, action)) || origin.Troops - amount < 1)
            return Invalid("Quantidade excede capacidade ou guarnição mínima.");
        if (!string.IsNullOrEmpty(action.InstallationId))
        {
            WDWeaponInstallationState installation = origin.Installations.FirstOrDefault(item => item.InstallationId == action.InstallationId);
            if (installation == null || !installation.IsTransportable) return Invalid("Instalação não transportável.");
            if (target.FindInstallation(installation.WeaponId) != null) return Invalid("Destino já possui instalação do mesmo tipo.");
        }
        return WDWeaponRevalidationResult.Valid();
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState origin = context.FindTerritory(action.OriginTerritoryId);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        int amount = WDWeaponParameters.Int(action, "troops");
        origin.RemoveTroops(amount); target.AddTroops(amount);
        if (!string.IsNullOrEmpty(action.InstallationId))
        {
            WDWeaponInstallationState installation = origin.Installations.First(item => item.InstallationId == action.InstallationId);
            origin.RemoveInstallation(installation); installation.MoveTo(target.TerritoryId); target.AddInstallation(installation);
        }
        return new(WDWeaponExecutionOutcome.Executed, $"{amount} tropas transportadas.");
    }
}

public sealed class WDInstallationHandler : WDSupportWeaponHandlerBase
{
    private readonly bool flak;
    public WDInstallationHandler(string weaponId, bool isFlak) : base(weaponId) => flak = isFlak;
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        return target != null && target.OwnerPlayerId == action.AuthorPlayerId && target.FindInstallation(action.WeaponId) == null
            ? WDWeaponRevalidationResult.Valid() : Invalid("Território próprio e instalação única são obrigatórios.");
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,
        WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        int level = Level(context, action);
        var installation = new WDWeaponInstallationState(action.InstallationId, action.WeaponId,
            action.TargetTerritoryId, action.AuthorPlayerId, flak ? 3 : 0,
            flak ? 0 : WDSupportWeaponTables.BunkerBarrier(level), 0, 0, level, flak);
        context.FindTerritory(action.TargetTerritoryId).AddInstallation(installation);
        return new(WDWeaponExecutionOutcome.Executed, flak ? "Flak instalada por 3 rounds." : "Bunker instalado.");
    }
}

public sealed class WDReserveTroopsHandler : WDSupportWeaponHandlerBase
{
    public WDReserveTroopsHandler() : base(WDSupportWeaponIds.ReserveTroops) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw, WDWeaponAction action, WDWeaponDefinition definition) =>
        Support(raw).GetTerritoriesLostLastRound(action.AuthorPlayerId) > 0 ? WDWeaponRevalidationResult.Valid() : Invalid("Nenhum território perdido no round anterior.");
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw, WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        int amount = WDSupportWeaponTables.ReserveTroops(Level(context, action), context.GetTerritoriesLostLastRound(action.AuthorPlayerId));
        context.AddReinforcementPool(action.AuthorPlayerId, amount, action.WeaponId);
        return new(WDWeaponExecutionOutcome.Executed, $"{amount} tropas no pool.");
    }
}

public sealed class WDTroopRecoveryHandler : WDSupportWeaponHandlerBase
{
    public WDTroopRecoveryHandler() : base(WDSupportWeaponIds.TroopRecovery) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw, WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        WDWeaponTerritoryState target = context.FindTerritory(action.TargetTerritoryId);
        WDPreviousRoundTerritoryRecord record = context.FindPreviousRoundRecord(action.TargetTerritoryId);
        return target != null && record != null && record.WasAttacked && record.Casualties > 0 &&
            record.OwnerAtStart == action.AuthorPlayerId && record.OwnerAtEnd == action.AuthorPlayerId && target.OwnerPlayerId == action.AuthorPlayerId
            ? WDWeaponRevalidationResult.Valid() : Invalid("Histórico autoritativo não torna o território elegível.");
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw, WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        int amount = Math.Min(WDSupportWeaponTables.RecoveryLimit(Level(context, action)), context.FindPreviousRoundRecord(action.TargetTerritoryId).Casualties);
        context.FindTerritory(action.TargetTerritoryId).AddTroops(amount);
        return new(WDWeaponExecutionOutcome.Executed, $"{amount} tropas recuperadas.");
    }
}

public sealed class WDMercenariesHandler : WDSupportWeaponHandlerBase
{
    public WDMercenariesHandler() : base(WDSupportWeaponIds.Mercenaries) { }
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw, WDWeaponAction action, WDWeaponDefinition definition) =>
        string.IsNullOrEmpty(action.TargetPlayerId) || action.TargetPlayerId == action.AuthorPlayerId
            ? WDWeaponRevalidationResult.Valid() : Invalid("Mercenários beneficiam somente o autor.");
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw, WDWeaponAction action, WDWeaponDefinition definition)
    {
        IWDSupportWeaponContext context = Support(raw);
        int[] payments = WDSupportWeaponTables.MercenaryPayments(Level(context, action));
        context.AddReinforcementPool(action.AuthorPlayerId, payments[0], action.WeaponId);
        context.AddMercenaryPayment(new WDMercenaryPayment(action.AuthorPlayerId, context.CurrentRound + 1, payments[1]));
        context.AddMercenaryPayment(new WDMercenaryPayment(action.AuthorPlayerId, context.CurrentRound + 2, payments[2]));
        return new(WDWeaponExecutionOutcome.Executed, $"Parcelas {payments[0]}/{payments[1]}/{payments[2]}.");
    }
}

public sealed class WDMercenaryPaymentPhaseHandler : IWDWeaponPhaseHandler
{
    public WDWeaponResolutionPhase Phase => WDWeaponResolutionPhase.RoundStart;
    public int Order => 10;
    public string StableId => "support.mercenary-payments";
    public void Execute(IWDWeaponResolutionContext raw)
    {
        IWDSupportWeaponContext context = raw as IWDSupportWeaponContext;
        if (context == null) return;
        foreach (WDMercenaryPayment payment in context.MercenaryPayments
            .Where(item => !item.Paid && item.DueRound <= context.CurrentRound)
            .OrderBy(item => item.DueRound).ThenBy(item => item.PlayerId, StringComparer.Ordinal))
        { context.AddReinforcementPool(payment.PlayerId, payment.Amount, WDSupportWeaponIds.Mercenaries); payment.MarkPaid(); }
    }
}

public sealed class WDSupportInstallationMaintenanceHandler : IWDWeaponPhaseHandler
{
    public WDWeaponResolutionPhase Phase => WDWeaponResolutionPhase.Consolidation;
    public int Order => 90;
    public string StableId => "support.installation-maintenance";
    public void Execute(IWDWeaponResolutionContext raw)
    {
        if (raw is not IWDSupportWeaponContext support ||
            raw is not IWDWeaponTerritoryEnumerable context) return;
        foreach (WDWeaponInstallationState installation in context.Territories
            .SelectMany(item => item.Installations)
            .Where(item => !item.IsDestroyed && item.WeaponId == WDSupportWeaponIds.FlakTower)
            .OrderBy(item => item.InstallationId, StringComparer.Ordinal))
        {
            if (!installation.TryMarkAutomaticRound(support.CurrentRound))
                continue;
            installation.ConsumeDuration();
            if (installation.DurationRemaining == 0) installation.Destroy();
        }
    }
}

public static class WDWeaponParameters
{
    public static int Int(WDWeaponAction action, string key, int fallback = 0)
    {
        string value = action?.Parameters.FirstOrDefault(item => item.Key == key)?.Value;
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }
}

public static class WDSupportWeaponPipelineExtensions
{
    // ============================================================
    // 04. REGISTRO MODULAR DOS OITO RESOLVERS DE APOIO
    // ============================================================

    public static void RegisterSupportWeapons(this WDWeaponResolutionPipeline pipeline)
    {
        pipeline.RegisterActionHandler(new WDAirStrikeHandler());
        pipeline.RegisterActionHandler(new WDDirectReinforcementHandler());
        pipeline.RegisterActionHandler(new WDAirTransportHandler());
        pipeline.RegisterActionHandler(new WDInstallationHandler(WDSupportWeaponIds.FlakTower, true));
        pipeline.RegisterActionHandler(new WDInstallationHandler(WDSupportWeaponIds.Bunker, false));
        pipeline.RegisterActionHandler(new WDReserveTroopsHandler());
        pipeline.RegisterActionHandler(new WDTroopRecoveryHandler());
        pipeline.RegisterActionHandler(new WDMercenariesHandler());
        pipeline.RegisterPhaseHandler(new WDMercenaryPaymentPhaseHandler());
        pipeline.RegisterPhaseHandler(new WDSupportInstallationMaintenanceHandler());
    }
}

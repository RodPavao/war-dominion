using System;
using System.Linq;

public abstract class WDStrategicWeaponHandlerBase : IWDWeaponActionHandler
{
    private readonly string weaponId;
    protected WDStrategicWeaponHandlerBase(string weaponId) => this.weaponId=weaponId;
    public bool CanHandle(WDWeaponAction action,WDWeaponDefinition definition)=>action!=null&&definition!=null&&definition.Id==weaponId;
    public abstract WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext context,WDWeaponAction action,WDWeaponDefinition definition);
    public abstract WDWeaponExecutionResult Execute(IWDWeaponResolutionContext context,WDWeaponAction action,WDWeaponDefinition definition);
    protected static IWDStrategicWeaponContext Strategic(IWDWeaponResolutionContext context)=>context as IWDStrategicWeaponContext??throw new InvalidOperationException("Contexto estratégico ausente.");
    protected static int Level(IWDStrategicWeaponContext context,WDWeaponAction action)=>context.FindPlayer(action.AuthorPlayerId)?.GetWeapon(action.WeaponId)?.Level??1;
    protected static WDWeaponRevalidationResult Invalid(string reason)=>new(WDWeaponRevalidationOutcome.Impossible,reason);
    protected static WDWeaponRevalidationResult Annulled(string reason)=>new(WDWeaponRevalidationOutcome.Annulled,reason);
}

// ============================================================
// 01. INTERFERÊNCIAS ENTRE JOGADORES
// ============================================================

public sealed class WDPeaceHandler:WDStrategicWeaponHandlerBase
{
    public WDPeaceHandler():base(WDStrategicWeaponIds.Peace){}
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);if(action.TargetPlayerId==action.AuthorPlayerId||c.FindPlayer(action.TargetPlayerId)==null)return Invalid("Paz exige outro jogador.");return c.HasPeace(action.AuthorPlayerId,action.TargetPlayerId)?Annulled("Paz recíproca já estabelecida."):WDWeaponRevalidationResult.Valid();}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {Strategic(raw).EstablishPeace(action.AuthorPlayerId,action.TargetPlayerId);return new(WDWeaponExecutionOutcome.Executed,"Paz mútua estabelecida neste round.");}
}

public sealed class WDSabotageHandler:WDStrategicWeaponHandlerBase
{
    public WDSabotageHandler():base(WDStrategicWeaponIds.Sabotage){}
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);if(action.TargetPlayerId==action.AuthorPlayerId||c.FindPlayer(action.TargetPlayerId)==null)return Invalid("Sabotagem exige adversário.");if(c.HasPeace(action.AuthorPlayerId,action.TargetPlayerId))return Annulled("Paz impediu Sabotagem.");return c.TryClaimSabotage(action.TargetPlayerId,action.ActionId)?WDWeaponRevalidationResult.Valid():Annulled("Outra Sabotagem válida já prevaleceu.");}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);int natural=c.GetNaturalReinforcement(action.TargetPlayerId);int removed=(int)Math.Floor(natural*WDStrategicWeaponTables.SabotageRate(Level(c,action)));c.SetNaturalReinforcement(action.TargetPlayerId,natural-removed);return new(WDWeaponExecutionOutcome.Executed,$"{removed} reforços naturais sabotados.");}
}

// ============================================================
// 02. INSTALAÇÕES E ATAQUES ESTRATÉGICOS PROGRAMADOS
// ============================================================

public sealed class WDStrategicInstallationHandler:WDStrategicWeaponHandlerBase
{
    private readonly int kind;
    public WDStrategicInstallationHandler(string id,int kind):base(id)=>this.kind=kind;
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);var t=c.FindTerritory(action.TargetTerritoryId);if(t==null||t.OwnerPlayerId!=action.AuthorPlayerId||t.FindInstallation(action.WeaponId)!=null)return Invalid("Instalação exige território próprio sem stacking.");if(kind==0&&c.GetOwnEnergyPlantActivations(action.AuthorPlayerId)>=2)return Invalid("Limite de duas Centrais próprias atingido.");return WDWeaponRevalidationResult.Valid();}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);int level=Level(c,action);int duration=kind==1?WDStrategicWeaponTables.ArtilleryDuration(level):0;int charges=kind==2?2:0;int payments=kind==0?3:0;bool movable=true;var i=new WDWeaponInstallationState(action.InstallationId,action.WeaponId,action.TargetTerritoryId,action.AuthorPlayerId,duration,0,charges,payments,level,movable,c.CurrentRound);c.FindTerritory(action.TargetTerritoryId).AddInstallation(i);if(kind==0)c.RecordOwnEnergyPlantActivation(action.AuthorPlayerId);return new(WDWeaponExecutionOutcome.Executed,"Instalação estratégica criada.");}
}

public sealed class WDLongRangeBombHandler:WDStrategicWeaponHandlerBase
{
    public WDLongRangeBombHandler():base(WDStrategicWeaponIds.LongRangeBomb){}
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);var t=c.FindTerritory(action.TargetTerritoryId);if(t==null)return Invalid("Alvo ausente.");if(t.State!=WDWeaponTerritoryStateKind.Empty&&(t.OwnerPlayerId==action.AuthorPlayerId||c.AreAllied(action.AuthorPlayerId,t.OwnerPlayerId)))return Invalid("Bomba exige alvo inimigo ou bombardeio já programado contra Vazio.");return c.HasPeace(action.AuthorPlayerId,t.OwnerPlayerId)?Annulled("Paz impediu lançamento."):WDWeaponRevalidationResult.Valid();}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {var c=Strategic(raw);var t=c.FindTerritory(action.TargetTerritoryId);if(WDSupportWeaponInteractionService.TryIntercept(c,action,t))return new(WDWeaponExecutionOutcome.Intercepted,"Bomba interceptada.");int casualties=WDSupportWeaponInteractionService.ApplyDamage(t,WDStrategicWeaponTables.LongRangeBombDamage(Level(c,action)),true);return new(WDWeaponExecutionOutcome.Executed,$"{casualties} baixa(s), sem conquista.");}
}

public sealed class WDRocketHandler:WDStrategicWeaponHandlerBase,IWDWeaponMultiPhaseHandler,IWDWeaponActionCostPolicy
{
    public WDRocketHandler():base(WDStrategicWeaponIds.Rockets){}
    public bool IsPhaseAllowed(WDWeaponResolutionPhase phase)=>phase==WDWeaponResolutionPhase.InstallationAndLogistics||phase==WDWeaponResolutionPhase.ProgrammedOffense;
    public bool UsesActivationCost(WDWeaponAction action)=>action.Phase==WDWeaponResolutionPhase.InstallationAndLogistics;
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {
        var c=Strategic(raw);if(action.Phase==WDWeaponResolutionPhase.InstallationAndLogistics)return new WDStrategicInstallationHandler(WDStrategicWeaponIds.Rockets,2).Revalidate(raw,action,definition);
        var origin=c.FindTerritory(action.OriginTerritoryId);var target=c.FindTerritory(action.TargetTerritoryId);var rocket=origin?.Installations.FirstOrDefault(i=>i.InstallationId==action.InstallationId&&!i.IsDestroyed&&i.WeaponId==WDStrategicWeaponIds.Rockets);
        if(origin==null||target==null||rocket==null||rocket.OwnerPlayerId!=action.AuthorPlayerId||rocket.ChargesRemaining<=0)return Invalid("Instalação/disparo inválido.");if(origin.IsImmobilized)return Annulled("Sniper impediu lançamento sem consumir tiro.");if(target.State!=WDWeaponTerritoryStateKind.Empty&&c.HasPeace(action.AuthorPlayerId,target.OwnerPlayerId))return Annulled("Paz impediu lançamento sem consumir tiro.");if(c is not IWDWeaponTerritoryEnumerable board||WDTacticalWeaponTables.Distance(board,origin.TerritoryId,target.TerritoryId)>3)return Invalid("Alvo fora do alcance.");return WDWeaponRevalidationResult.Valid();
    }
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction action,WDWeaponDefinition definition)
    {
        if(action.Phase==WDWeaponResolutionPhase.InstallationAndLogistics)return new WDStrategicInstallationHandler(WDStrategicWeaponIds.Rockets,2).Execute(raw,action,definition);
        var c=Strategic(raw);var origin=c.FindTerritory(action.OriginTerritoryId);var target=c.FindTerritory(action.TargetTerritoryId);var rocket=origin.Installations.First(i=>i.InstallationId==action.InstallationId&&!i.IsDestroyed);rocket.ConsumeCharge();if(rocket.ChargesRemaining==0)rocket.Destroy();if(WDSupportWeaponInteractionService.TryIntercept(c,action,target))return new(WDWeaponExecutionOutcome.Intercepted,"Foguete consumido e interceptado.");int casualties=WDSupportWeaponInteractionService.ApplyDamage(target,WDStrategicWeaponTables.RocketDamage(rocket.Level),true);return new(WDWeaponExecutionOutcome.Executed,$"Foguete consumido; {casualties} baixa(s).");
    }
}

// ============================================================
// 03. AUTOMAÇÕES DE INÍCIO, ARTILHARIA E ENCERRAMENTO DO ROUND
// ============================================================

public sealed class WDEnergyPlantPaymentHandler:IWDWeaponPhaseHandler
{
    public WDWeaponResolutionPhase Phase=>WDWeaponResolutionPhase.RoundStart;public int Order=>0;public string StableId=>"strategic.energy-payments";
    public void Execute(IWDWeaponResolutionContext raw){if(raw is not IWDStrategicWeaponContext c||raw is not IWDWeaponTerritoryEnumerable board)return;foreach(var pair in WDStrategicInstallationService.Active(board,WDStrategicWeaponIds.EnergyPlant)){var i=pair.installation;if(i.PaymentsRemaining<=0||i.InstalledRound>=c.CurrentRound||string.IsNullOrEmpty(i.OwnerPlayerId)||!i.TryMarkAutomaticRound(c.CurrentRound))continue;c.FindPlayer(i.OwnerPlayerId)?.AddEnergy(WDStrategicWeaponTables.EnergyProduction(i.Level));i.ConsumePayment();if(i.PaymentsRemaining==0)i.Destroy();}}
}

public sealed class WDArtilleryOperationHandler:IWDWeaponPhaseHandler
{
    public WDWeaponResolutionPhase Phase=>WDWeaponResolutionPhase.AutomaticArtillery;public int Order=>0;public string StableId=>"strategic.artillery-operation";
    public void Execute(IWDWeaponResolutionContext raw)
    {
        if(raw is not IWDStrategicWeaponContext c||raw is not IWDWeaponTerritoryEnumerable board)return;
        foreach(var pair in WDStrategicInstallationService.Active(board,WDStrategicWeaponIds.Artillery).OrderBy(x=>c.GetPlayerOrder(x.installation.OwnerPlayerId)).ThenBy(x=>x.territory.TerritoryId,StringComparer.Ordinal))
        {
            var origin=pair.territory;var artillery=pair.installation;
            if(string.IsNullOrEmpty(artillery.OwnerPlayerId)||origin.IsImmobilized)continue;
            var targets=origin.Neighbours.Select(c.FindTerritory).Where(t=>IsAttackable(c,artillery.OwnerPlayerId,t)).OrderByDescending(t=>t.Troops).ThenBy(t=>t.TerritoryId,StringComparer.Ordinal).Take(3).ToArray();
            if(targets.Length==0||!artillery.TryMarkAutomaticRound(c.CurrentRound))continue;
            foreach(var target in targets){WDSupportWeaponInteractionService.ApplyDamage(target,WDStrategicWeaponTables.ArtilleryDamage(artillery.Level),true);if(target.Troops==0&&target.State==WDWeaponTerritoryStateKind.Neutral)target.MakeEmpty();}
            artillery.ConsumeDuration();if(artillery.DurationRemaining==0)artillery.Destroy();
        }
    }

    private static bool IsAttackable(IWDStrategicWeaponContext context,string owner,WDWeaponTerritoryState target)
    {
        if(target==null||target.OwnerPlayerId==owner)return false;
        if(target.State==WDWeaponTerritoryStateKind.Neutral||target.State==WDWeaponTerritoryStateKind.Empty)return true;
        return !context.AreAllied(owner,target.OwnerPlayerId)&&!context.HasPeace(owner,target.OwnerPlayerId);
    }
}

public sealed class WDPeaceCleanupHandler:IWDWeaponPhaseHandler
{public WDWeaponResolutionPhase Phase=>WDWeaponResolutionPhase.Consolidation;public int Order=>100;public string StableId=>"strategic.peace-cleanup";public void Execute(IWDWeaponResolutionContext raw){if(raw is IWDStrategicWeaponContext c)c.ClearPeace();}}

public static class WDStrategicWeaponPipelineExtensions
{
    // ============================================================
    // 04. REGISTRO MODULAR DAS SEIS ARMAS E AUTOMAÇÕES
    // ============================================================
    public static void RegisterStrategicWeapons(this WDWeaponResolutionPipeline pipeline)
    {pipeline.RegisterActionHandler(new WDPeaceHandler());pipeline.RegisterActionHandler(new WDSabotageHandler());pipeline.RegisterActionHandler(new WDStrategicInstallationHandler(WDStrategicWeaponIds.EnergyPlant,0));pipeline.RegisterActionHandler(new WDLongRangeBombHandler());pipeline.RegisterActionHandler(new WDStrategicInstallationHandler(WDStrategicWeaponIds.Artillery,1));pipeline.RegisterActionHandler(new WDRocketHandler());pipeline.RegisterPhaseHandler(new WDEnergyPlantPaymentHandler());pipeline.RegisterPhaseHandler(new WDArtilleryOperationHandler());pipeline.RegisterPhaseHandler(new WDPeaceCleanupHandler());}
}

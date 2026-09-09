using System;
using System.Linq;

public abstract class WDSpecialWeaponHandlerBase:IWDWeaponActionHandler
{
    readonly string id;protected WDSpecialWeaponHandlerBase(string value)=>id=value;
    public bool CanHandle(WDWeaponAction a,WDWeaponDefinition d)=>a!=null&&d?.Id==id;
    public abstract WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext c,WDWeaponAction a,WDWeaponDefinition d);
    public abstract WDWeaponExecutionResult Execute(IWDWeaponResolutionContext c,WDWeaponAction a,WDWeaponDefinition d);
    protected static IWDSpecialWeaponContext C(IWDWeaponResolutionContext c)=>c as IWDSpecialWeaponContext??throw new InvalidOperationException("Contexto especial ausente.");
    protected static int L(IWDSpecialWeaponContext c,WDWeaponAction a)=>c.FindPlayer(a.AuthorPlayerId)?.GetWeapon(a.WeaponId)?.Level??1;
    protected static WDWeaponRevalidationResult Bad(string s)=>new(WDWeaponRevalidationOutcome.Impossible,s);
    protected static WDWeaponRevalidationResult No(string s)=>new(WDWeaponRevalidationOutcome.Annulled,s);
}

// ============================================================
// 02. BOMBAS ATÔMICA E QUÍMICA
// ============================================================
public sealed class WDAtomicBombHandler:WDSpecialWeaponHandlerBase
{
    public WDAtomicBombHandler():base(WDSpecialWeaponIds.AtomicBomb){}
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction a,WDWeaponDefinition d){var c=C(raw);var t=c.FindTerritory(a.TargetTerritoryId);if(t==null)return Bad("Alvo central ausente.");return t.State!=WDWeaponTerritoryStateKind.Empty&&c.HasPeace(a.AuthorPlayerId,t.OwnerPlayerId)?No("Paz protegeu alvo central."):WDWeaponRevalidationResult.Valid();}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction a,WDWeaponDefinition d){var c=C(raw);var center=c.FindTerritory(a.TargetTerritoryId);if(WDSupportWeaponInteractionService.TryIntercept(c,a,center))return new(WDWeaponExecutionOutcome.Intercepted,"Flak central interceptou toda a explosão.");WDSupportWeaponInteractionService.ApplyDamage(center,WDSpecialWeaponTables.AtomicDamage(L(c,a)),true);if(c is IWDWeaponTerritoryEnumerable board)foreach(var t in center.Neighbours.OrderBy(x=>x,StringComparer.Ordinal).Select(c.FindTerritory).Where(x=>x!=null))WDSupportWeaponInteractionService.ApplyDamage(t,2,true);return new(WDWeaponExecutionOutcome.Executed,"Detonação central e dano colateral geográfico.");}
}
public sealed class WDChemicalBombHandler:WDSpecialWeaponHandlerBase
{
    public WDChemicalBombHandler():base(WDSpecialWeaponIds.ChemicalBomb){}
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction a,WDWeaponDefinition d){var c=C(raw);var o=c.FindTerritory(a.OriginTerritoryId);var t=c.FindTerritory(a.TargetTerritoryId);if(o==null||t==null||o.OwnerPlayerId!=a.AuthorPlayerId||!o.Neighbours.Contains(t.TerritoryId)||t.State!=WDWeaponTerritoryStateKind.Occupied||t.OwnerPlayerId==a.AuthorPlayerId||c.AreAllied(a.AuthorPlayerId,t.OwnerPlayerId))return Bad("Química exige inimigo diretamente vizinho.");return c.HasPeace(a.AuthorPlayerId,t.OwnerPlayerId)?No("Paz impediu lançamento."):WDWeaponRevalidationResult.Valid();}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction a,WDWeaponDefinition d){var c=C(raw);var t=c.FindTerritory(a.TargetTerritoryId);if(WDSupportWeaponInteractionService.TryIntercept(c,a,t))return new(WDWeaponExecutionOutcome.Intercepted,"Química interceptada.");int loss=Math.Max(1,(int)Math.Ceiling(t.Troops*WDSpecialWeaponTables.ChemicalRate(L(c,a))));int removed=t.RemoveTroops(loss);if(t.Troops==0)t.MakeEmpty();return new(WDWeaponExecutionOutcome.Executed,$"{removed} baixa(s) químicas; Bunker ignorado.");}
}

// ============================================================
// 03. PROPAGANDA E INFILTRAÇÃO TEMPORÁRIA
// ============================================================
public sealed class WDWarPropagandaHandler:WDSpecialWeaponHandlerBase
{
    public WDWarPropagandaHandler():base(WDSpecialWeaponIds.WarPropaganda){}
    public override WDWeaponRevalidationResult Revalidate(IWDWeaponResolutionContext raw,WDWeaponAction a,WDWeaponDefinition d){var c=C(raw);var t=c.FindTerritory(a.TargetTerritoryId);return t!=null&&t.State==WDWeaponTerritoryStateKind.Occupied&&t.OwnerPlayerId!=a.AuthorPlayerId&&!c.AreAllied(a.AuthorPlayerId,t.OwnerPlayerId)&&t.Troops<=WDSpecialWeaponTables.PropagandaLimit(L(c,a))?WDWeaponRevalidationResult.Valid():Bad("Estado atual excede limite ou alvo inválido.");}
    public override WDWeaponExecutionResult Execute(IWDWeaponResolutionContext raw,WDWeaponAction a,WDWeaponDefinition d){var t=C(raw).FindTerritory(a.TargetTerritoryId);int neutral=Math.Max(1,t.Troops/2);t.MakeNeutral(neutral);return new(WDWeaponExecutionOutcome.Executed,$"Território neutralizado com {neutral} tropa(s).");}
}
public sealed class WDSpecialCleanupHandler:IWDWeaponPhaseHandler
{public WDWeaponResolutionPhase Phase=>WDWeaponResolutionPhase.Consolidation;public int Order=>90;public string StableId=>"special.temporary-cleanup";public void Execute(IWDWeaponResolutionContext raw){if(raw is not IWDWeaponTerritoryEnumerable board)return;foreach(var t in board.Territories){t.ClearImmobilization();foreach(var i in t.Installations)i.SetTemporarilyDisabled(false);}}}
public static class WDSpecialWeaponPipelineExtensions
{
    // ============================================================
    // 04. REGISTRO MODULAR DA CATEGORIA ESPECIAL
    // ============================================================
    public static void RegisterSpecialWeapons(this WDWeaponResolutionPipeline p){p.RegisterActionHandler(new WDAtomicBombHandler());p.RegisterActionHandler(new WDWarPropagandaHandler());p.RegisterActionHandler(new WDChemicalBombHandler());p.RegisterPhaseHandler(new WDSpecialCleanupHandler());}
}

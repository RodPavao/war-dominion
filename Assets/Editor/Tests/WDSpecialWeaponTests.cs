using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDSpecialWeaponTests
{
    [TestCase(1,1,1)] [TestCase(2,2,1)] [TestCase(3,2,2)]
    [TestCase(4,3,2)] [TestCase(5,3,3)] [TestCase(6,4,3)]
    public void CanonicalDroneHasSequentialShotDamage(int level,int first,int second)
    {
        Assert.That(WDSpecialWeaponTables.DroneShotDamage(level,1),Is.EqualTo(first));
        Assert.That(WDSpecialWeaponTables.DroneShotDamage(level,2),Is.EqualTo(second));
    }

    // ============================================================
    // 01. TABELAS E DEFINIÇÕES OFICIAIS
    // ============================================================
    [TestCase(1,3)][TestCase(2,4)][TestCase(3,5)][TestCase(4,6)][TestCase(5,7)][TestCase(6,8)] public void AtomicDamage(int level,int value)=>Assert.That(WDSpecialWeaponTables.AtomicDamage(level),Is.EqualTo(value));
    [TestCase(1,3)][TestCase(2,4)][TestCase(3,5)][TestCase(4,6)][TestCase(5,7)][TestCase(6,8)] public void PropagandaLimits(int level,int value)=>Assert.That(WDSpecialWeaponTables.PropagandaLimit(level),Is.EqualTo(value));
    [TestCase(1,.25)][TestCase(2,.30)][TestCase(3,.35)][TestCase(4,.40)][TestCase(5,.45)][TestCase(6,.50)] public void ChemicalRates(int level,double value)=>Assert.That(WDSpecialWeaponTables.ChemicalRate(level),Is.EqualTo(value).Within(.001));

    // ============================================================
    // 03. EXPLOSÕES, PROPAGANDA E INFILTRAÇÃO
    // ============================================================
    [Test]
    public void AtomicCollateralHitsEveryNeighbourWithoutSeparatePeaceOrFlak()
    {var c=Ctx(.99);var center=T(c,"c","p2",8);foreach(var x in new[]{("own","p1",3),("ally","p3",3),("enemy","p2",3),("neutral","",1)}){var t=new WDWeaponTerritoryState(x.Item1,x.Item2,x.Item3,x.Item1=="neutral"?WDWeaponTerritoryStateKind.Neutral:WDWeaponTerritoryStateKind.Occupied);c.Add(t);Link(center,t);}var empty=new WDWeaponTerritoryState("empty","",0,WDWeaponTerritoryStateKind.Empty);c.Add(empty);Link(center,empty);c.Allies("p1","p3");c.EstablishPeace("p1","p2");new WDAtomicBombHandler().Execute(c,A(Def(WDSpecialWeaponIds.AtomicBomb),"atom",target:"c"),Def(WDSpecialWeaponIds.AtomicBomb));Assert.That(c.FindTerritory("own").Troops,Is.EqualTo(1));Assert.That(c.FindTerritory("ally").Troops,Is.EqualTo(1));Assert.That(c.FindTerritory("enemy").Troops,Is.EqualTo(1));Assert.That(c.FindTerritory("neutral").State,Is.EqualTo(WDWeaponTerritoryStateKind.Empty));Assert.That(c.FindTerritory("neutral").Troops,Is.Zero);Assert.That(c.FindTerritory("neutral").OwnerPlayerId,Is.Empty);Assert.That(empty.State,Is.EqualTo(WDWeaponTerritoryStateKind.Empty));Assert.That(empty.Troops,Is.Zero);}
    [Test]
    public void AtomicCentralFlakCancelsWholeExplosion()
    {var c=Ctx(0);var center=T(c,"c","p2",8);var n=T(c,"n","p3",4);Link(center,n);center.AddInstallation(I("f",WDSupportWeaponIds.FlakTower,"c","p2",4));var r=new WDAtomicBombHandler().Execute(c,A(Def(WDSpecialWeaponIds.AtomicBomb),"atom",target:"c"),Def(WDSpecialWeaponIds.AtomicBomb));Assert.That(r.Outcome,Is.EqualTo(WDWeaponExecutionOutcome.Intercepted));Assert.That(center.Troops,Is.EqualTo(8));Assert.That(n.Troops,Is.EqualTo(4));}
    [Test]
    public void AtomicAlreadyEmptyCentralStillDetonatesAndBunkersAbsorb()
    {var c=Ctx(.99);var center=new WDWeaponTerritoryState("c","",0,WDWeaponTerritoryStateKind.Empty);var n=T(c,"n","p2",4);c.Add(center);Link(center,n);n.AddInstallation(I("b",WDSupportWeaponIds.Bunker,"n","p2",1,3));var a=A(Def(WDSpecialWeaponIds.AtomicBomb),"atom",target:"c");Assert.That(new WDAtomicBombHandler().Revalidate(c,a,Def(a.WeaponId)).Outcome,Is.EqualTo(WDWeaponRevalidationOutcome.Valid));new WDAtomicBombHandler().Execute(c,a,Def(a.WeaponId));Assert.That(n.Troops,Is.EqualTo(4));Assert.That(n.FindInstallation(WDSupportWeaponIds.Bunker).BarrierRemaining,Is.EqualTo(1));}
    [TestCase(8,4)][TestCase(7,3)][TestCase(3,1)][TestCase(1,1)]
    public void PropagandaMakesNeutralWithFloorAndMinimum(int troops,int expected)
    {var c=Ctx();var t=T(c,"t","p2",troops);var a=A(Def(WDSpecialWeaponIds.WarPropaganda),"p",target:"t");c.FindPlayer("p1").GetWeapon(a.WeaponId).SetLevel(6,6);new WDWarPropagandaHandler().Execute(c,a,Def(a.WeaponId));Assert.That(t.State,Is.EqualTo(WDWeaponTerritoryStateKind.Neutral));Assert.That(t.OwnerPlayerId,Is.Empty);Assert.That(t.Troops,Is.EqualTo(expected));}
    [Test]
    public void PropagandaUsesCurrentLimitAndIgnoresDefensesAndPeace()
    {var c=Ctx();var t=T(c,"t","p2",7);t.AddInstallation(I("b",WDSupportWeaponIds.Bunker,"t","p2",1,5));t.AddInstallation(I("f",WDSupportWeaponIds.FlakTower,"t","p2",4));c.EstablishPeace("p1","p2");var a=A(Def(WDSpecialWeaponIds.WarPropaganda),"p",target:"t");Assert.That(new WDWarPropagandaHandler().Revalidate(c,a,Def(a.WeaponId)).Outcome,Is.EqualTo(WDWeaponRevalidationOutcome.Impossible));t.RemoveTroops(1);c.FindPlayer("p1").GetWeapon(a.WeaponId).SetLevel(4,6);Assert.That(new WDWarPropagandaHandler().Revalidate(c,a,Def(a.WeaponId)).Outcome,Is.EqualTo(WDWeaponRevalidationOutcome.Valid));}
    [Test]
    public void ChemicalCeilsIgnoresBunkerAndCanEmpty()
    {var c=Ctx(.99);var o=T(c,"o","p1",1);var t=T(c,"t","p2",7);Link(o,t);t.AddInstallation(I("b",WDSupportWeaponIds.Bunker,"t","p2",1,5));var a=A(Def(WDSpecialWeaponIds.ChemicalBomb),"q",origin:"o",target:"t");c.FindPlayer("p1").GetWeapon(a.WeaponId).SetLevel(6,6);new WDChemicalBombHandler().Execute(c,a,Def(a.WeaponId));Assert.That(t.Troops,Is.EqualTo(3));Assert.That(t.FindInstallation(WDSupportWeaponIds.Bunker).BarrierRemaining,Is.EqualTo(5));}
    [Test]
    public void DisabledMineIsCapturedWithoutExplodingAndRearmsAtCleanup()
    {var c=Ctx();var t=new WDWeaponTerritoryState("t","",0,WDWeaponTerritoryStateKind.Empty);c.Add(t);var mine=I("m",WDSupportWeaponIds.Minefield,"t","p1",1);mine.SetTemporarilyDisabled(true);t.AddInstallation(mine);Assert.That(WDTacticalWeaponInteractionService.TriggerMineIfApplicable(t,"p2",2),Is.EqualTo(2));t.Capture("p2");Assert.That(mine.OwnerPlayerId,Is.EqualTo("p1"));new WDSpecialCleanupHandler().Execute(c);Assert.That(mine.IsTemporarilyDisabled,Is.False);}

    // ============================================================
    // 04. FIXTURE ISOLADO DO DOMÍNIO
    // ============================================================
    static IReadOnlyList<WDWeaponDefinition> Ds()=>new[]{D(WDSpecialWeaponIds.AtomicBomb,6,40,3,WDWeaponResolutionPhase.ProgrammedOffense),D(WDSpecialWeaponIds.WarPropaganda,6,25,3,WDWeaponResolutionPhase.Propaganda),D(WDSpecialWeaponIds.ChemicalBomb,6,25,3,WDWeaponResolutionPhase.ProgrammedOffense,origin:true)};
    static WDWeaponDefinition D(string id,int levels,int energy,int cooldown,WDWeaponResolutionPhase phase,WDWeaponCostTiming timing=WDWeaponCostTiming.OnExecution,bool origin=false){var d=new WDWeaponDefinition(id,id,"special",null);d.ConfigureRuntime(levels,energy,cooldown,0,WDWeaponBehaviorType.Specialized,phase,timing,origin,id!=WDSpecialWeaponIds.Drone,false,id==WDSpecialWeaponIds.AtomicBomb,false);return d;}static WDWeaponDefinition Def(string id)=>Ds().Single(x=>x.Id==id);
    static CtxImpl Ctx(double roll=.99)=>new(Ds(),roll);static WDWeaponTerritoryState T(CtxImpl c,string id,string owner,int troops){var t=new WDWeaponTerritoryState(id,owner,troops);c.Add(t);return t;}static void Link(WDWeaponTerritoryState a,WDWeaponTerritoryState b){a.AddNeighbour(b.TerritoryId);b.AddNeighbour(a.TerritoryId);}static WDWeaponInstallationState I(string id,string weapon,string territory,string owner,int level,int barrier=0)=>new(id,weapon,territory,owner,3,barrier,2,3,level,true,1);
    static IEnumerable<WDWeaponActionParameter> P(params (string,string)[] p)=>p.Select(x=>new WDWeaponActionParameter(x.Item1,x.Item2));static WDWeaponAction A(WDWeaponDefinition d,string id,string origin="",string target="",IEnumerable<WDWeaponActionParameter> pars=null)=>new(id,"p1",d.Id,1,d.ResolutionPhase,0,origin,target,"","",pars);static WDWeaponResolutionPipeline Pip(){var p=new WDWeaponResolutionPipeline();p.RegisterSupportWeapons();p.RegisterTacticalWeapons();p.RegisterStrategicWeapons();p.RegisterSpecialWeapons();return p;}
    sealed class R:IWDWeaponRandomSource{readonly double v;public R(double x)=>v=x;public double NextUnit(string k)=>v;}
    sealed class CtxImpl:IWDSpecialWeaponContext,IWDWeaponTerritoryEnumerable
    {
        readonly Dictionary<string,WDWeaponDefinition> ds;readonly Dictionary<string,WDWeaponPlayerRuntimeState> ps=new();readonly Dictionary<string,WDWeaponTerritoryState> ts=new();readonly HashSet<string> peace=new(),allies=new(),snipers=new(),blocked=new(),sabotaged=new();readonly Dictionary<string,int> limits=new(),used=new(),natural=new(),independent=new(),plants=new();public CtxImpl(IEnumerable<WDWeaponDefinition>d,double roll){ds=d.ToDictionary(x=>x.Id);Random=new R(roll);foreach(var id in new[]{"p1","p2","p3"}){var p=new WDWeaponPlayerRuntimeState(id,100);foreach(var x in ds.Values)p.RegisterWeapon(x.Id,1);ps[id]=p;}}public int CurrentRound=>1;public IWDWeaponRandomSource Random{get;}public IEnumerable<WDWeaponTerritoryState> Territories=>ts.Values;public IEnumerable<WDMercenaryPayment> MercenaryPayments=>Array.Empty<WDMercenaryPayment>();string Pair(string a,string b)=>string.CompareOrdinal(a,b)<0?a+":"+b:b+":"+a;public void Add(WDWeaponTerritoryState t)=>ts[t.TerritoryId]=t;public void Allies(string a,string b)=>allies.Add(Pair(a,b));public void Sniper(string p,string t)=>snipers.Add(p+":"+t);
        public WDWeaponDefinition FindDefinition(string id)=>ds.TryGetValue(id,out var d)?d:null;public WDWeaponPlayerRuntimeState FindPlayer(string id)=>ps.TryGetValue(id,out var p)?p:null;public int GetPlayerOrder(string id)=>id=="p1"?0:id=="p2"?1:2;public WDWeaponTerritoryState FindTerritory(string id)=>ts.TryGetValue(id??"",out var t)?t:null;public bool AreAllied(string a,string b)=>allies.Contains(Pair(a,b));public bool EstablishPeace(string a,string b)=>peace.Add(Pair(a,b));public bool HasPeace(string a,string b)=>!string.IsNullOrEmpty(a)&&!string.IsNullOrEmpty(b)&&peace.Contains(Pair(a,b));public void ClearPeace()=>peace.Clear();public bool TryClaimSabotage(string p,string a)=>sabotaged.Add(p);public int GetNaturalReinforcement(string p)=>natural.TryGetValue(p,out var v)?v:0;public void SetNaturalReinforcement(string p,int a)=>natural[p]=a;public int GetIndependentReinforcement(string p)=>independent.TryGetValue(p,out var v)?v:0;public int GetOwnEnergyPlantActivations(string p)=>plants.TryGetValue(p,out var v)?v:0;public void RecordOwnEnergyPlantActivation(string p)=>plants[p]=GetOwnEnergyPlantActivations(p)+1;
        public bool TryBlockTransfers(string p,string a)=>blocked.Add(p);public bool AreTransfersBlocked(string p)=>blocked.Contains(p);public void SetTerrestrialActionLimit(string p,int v)=>limits[p]=v;public int GetTerrestrialActionLimit(string p)=>limits.TryGetValue(p,out var v)?v:3;public void ConsumeTerrestrialActionSlot(string p)=>used[p]=GetConsumedTerrestrialActionSlots(p)+1;public int GetConsumedTerrestrialActionSlots(string p)=>used.TryGetValue(p,out var v)?v:0;public int GetTerritoriesLostLastRound(string p)=>0;public WDPreviousRoundTerritoryRecord FindPreviousRoundRecord(string t)=>null;public void AddReinforcementPool(string p,int a,string s)=>independent[p]=GetIndependentReinforcement(p)+a;public void AddMercenaryPayment(WDMercenaryPayment p){}
    }
}

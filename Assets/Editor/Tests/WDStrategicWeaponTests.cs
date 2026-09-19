using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDStrategicWeaponTests
{
    // ============================================================
    // 01. TABELAS E DEFINIÇÕES
    // ============================================================

    [TestCase(1,.15)] [TestCase(2,.20)] [TestCase(3,.25)] [TestCase(4,.30)] [TestCase(5,.35)] [TestCase(6,.40)]
    public void SabotageRatesMatchLevels(int level,double expected)=>Assert.That(WDStrategicWeaponTables.SabotageRate(level),Is.EqualTo(expected).Within(.001));
    [TestCase(1,5)] [TestCase(2,10)] [TestCase(3,15)] [TestCase(4,20)]
    public void EnergyPlantProductionMatchesLevels(int level,int expected)=>Assert.That(WDStrategicWeaponTables.EnergyProduction(level),Is.EqualTo(expected));
    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)] [TestCase(5,6)] [TestCase(6,7)]
    public void LongRangeBombDamageMatchesLevels(int level,int expected)=>Assert.That(WDStrategicWeaponTables.LongRangeBombDamage(level),Is.EqualTo(expected));
    [TestCase(1,1,6)] [TestCase(2,2,6)] [TestCase(3,2,9)] [TestCase(4,3,9)]
    public void ArtilleryStatsMatchLevels(int level,int damage,int ammunition){Assert.That(WDStrategicWeaponTables.ArtilleryDamage(level),Is.EqualTo(damage));Assert.That(WDStrategicWeaponTables.ArtilleryAmmunition(level),Is.EqualTo(ammunition));}
    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)] [TestCase(5,6)] [TestCase(6,7)]
    public void RocketDamageMatchesLevels(int level,int expected)=>Assert.That(WDStrategicWeaponTables.RocketDamage(level),Is.EqualTo(expected));

    [Test]
    public void DefinitionsHaveOfficialCostsCooldownsAndPhases()
    {
        var expected=new Dictionary<string,(int,int,WDWeaponResolutionPhase)>{{WDStrategicWeaponIds.Sabotage,(20,3,WDWeaponResolutionPhase.Interference)},{WDStrategicWeaponIds.EnergyPlant,(0,5,WDWeaponResolutionPhase.InstallationAndLogistics)},{WDStrategicWeaponIds.LongRangeBomb,(25,3,WDWeaponResolutionPhase.ProgrammedOffense)},{WDStrategicWeaponIds.Peace,(15,3,WDWeaponResolutionPhase.Peace)},{WDStrategicWeaponIds.Artillery,(30,3,WDWeaponResolutionPhase.InstallationAndLogistics)},{WDStrategicWeaponIds.Rockets,(25,3,WDWeaponResolutionPhase.InstallationAndLogistics)}};
        foreach(var d in Definitions()){Assert.That(d.EnergyCost,Is.EqualTo(expected[d.Id].Item1));Assert.That(d.CooldownRounds,Is.EqualTo(expected[d.Id].Item2));Assert.That(d.ResolutionPhase,Is.EqualTo(expected[d.Id].Item3));}
    }

    // ============================================================
    // 02. PAZ E SABOTAGEM
    // ============================================================

    [Test]
    public void PeaceIsSymmetricAndSupportsMultiplePairs()
    {var c=Context();new WDPeaceHandler().Execute(c,Action(WDStrategicWeaponIds.Peace,targetPlayer:"p2"),Definition(WDStrategicWeaponIds.Peace));new WDPeaceHandler().Execute(c,Action(WDStrategicWeaponIds.Peace,targetPlayer:"p3",actionId:"p13"),Definition(WDStrategicWeaponIds.Peace));Assert.That(c.HasPeace("p2","p1"),Is.True);Assert.That(c.HasPeace("p1","p3"),Is.True);}

    [Test]
    public void ReciprocalPeaceIsAnnulledWithoutCostAndExpiresAtRoundEnd()
    {var c=Context();var a=Action(WDStrategicWeaponIds.Peace,author:"p1",targetPlayer:"p2",actionId:"a");var b=Action(WDStrategicWeaponIds.Peace,author:"p2",targetPlayer:"p1",actionId:"b");var results=Pipeline().Resolve(new[]{b,a},c);Assert.That(results.Single(x=>x.ActionId=="a").State,Is.EqualTo(WDWeaponActionState.Executed));Assert.That(results.Single(x=>x.ActionId=="b").State,Is.EqualTo(WDWeaponActionState.Annulled));Assert.That(c.FindPlayer("p2").Energy,Is.EqualTo(100));Assert.That(c.HasPeace("p1","p2"),Is.False);}

    [Test]
    public void SabotageFloorsAndOnlyChangesNaturalReinforcement()
    {var c=Context();c.Natural["p2"]=19;c.Independent["p2"]=12;Resolve(c,Action(WDStrategicWeaponIds.Sabotage,targetPlayer:"p2"),6);Assert.That(c.GetNaturalReinforcement("p2"),Is.EqualTo(12));Assert.That(c.GetIndependentReinforcement("p2"),Is.EqualTo(12));}

    [Test]
    public void FirstValidSabotageWinsAndLaterIsAnnulledWithoutCost()
    {var c=Context();c.Natural["p3"]=20;var a=Action(WDStrategicWeaponIds.Sabotage,author:"p1",targetPlayer:"p3",actionId:"a");var b=Action(WDStrategicWeaponIds.Sabotage,author:"p2",targetPlayer:"p3",actionId:"b");var results=Pipeline().Resolve(new[]{b,a},c);Assert.That(results.Single(x=>x.ActionId=="b").State,Is.EqualTo(WDWeaponActionState.Annulled));Assert.That(c.FindPlayer("p2").Energy,Is.EqualTo(100));}

    [Test]
    public void PeaceAnnulsEarlierSabotageAndAllowsUnprotectedThirdPlayer()
    {var c=Context();c.Natural["p2"]=20;var peace=Action(WDStrategicWeaponIds.Peace,author:"p1",targetPlayer:"p2",actionId:"peace");var blocked=Action(WDStrategicWeaponIds.Sabotage,author:"p1",targetPlayer:"p2",actionId:"blocked");var valid=Action(WDStrategicWeaponIds.Sabotage,author:"p3",targetPlayer:"p2",actionId:"valid");var r=Pipeline().Resolve(new[]{valid,blocked,peace},c);Assert.That(r.Single(x=>x.ActionId=="blocked").State,Is.EqualTo(WDWeaponActionState.Annulled));Assert.That(r.Single(x=>x.ActionId=="valid").State,Is.EqualTo(WDWeaponActionState.Executed));}

    [Test]
    public void PeaceDoesNotInterfereWithTransferNullifierState()
    {var c=Context();c.EstablishPeace("p1","p2");Assert.That(c.TryBlockTransfers("p2","n"),Is.True);Assert.That(c.AreTransfersBlocked("p2"),Is.True);}

    // ============================================================
    // 03. CENTRAL E INSTALAÇÕES ABANDONADAS
    // ============================================================

    [Test]
    public void EnergyPlantInstallsAtZeroEnergyAndStartsPayingNextRound()
    {var c=Context();c.ReplacePlayer("p1",0,4);Territory(c,"t","p1",1);var pipeline=Pipeline();pipeline.Resolve(new[]{Action(WDStrategicWeaponIds.EnergyPlant,target:"t",installation:"plant")},c);Assert.That(c.FindPlayer("p1").Energy,Is.Zero);c.Round=2;pipeline.Resolve(Array.Empty<WDWeaponAction>(),c);Assert.That(c.FindPlayer("p1").Energy,Is.EqualTo(20));}

    [Test]
    public void EnergyPlantPaysExactlyThreeTimesIdempotently()
    {var c=Context();c.ReplacePlayer("p1",0,1);Territory(c,"t","p1",1);var p=Pipeline();p.Resolve(new[]{Action(WDStrategicWeaponIds.EnergyPlant,target:"t",installation:"plant")},c);for(int round=2;round<=5;round++){c.Round=round;p.Resolve(Array.Empty<WDWeaponAction>(),c);p.Resolve(Array.Empty<WDWeaponAction>(),c);}Assert.That(c.FindPlayer("p1").Energy,Is.EqualTo(15));}

    [Test]
    public void EnergyPlantLimitBelongsToOriginalInstallerAndCapturePreservesPayments()
    {var c=Context();for(int i=0;i<3;i++)Territory(c,"t"+i,"p1",1);var p=Pipeline();p.Resolve(new[]{Action(WDStrategicWeaponIds.EnergyPlant,target:"t0",installation:"a")},c);AdvanceCooldown(c,"p1",WDStrategicWeaponIds.EnergyPlant,5);p.Resolve(new[]{Action(WDStrategicWeaponIds.EnergyPlant,target:"t1",installation:"b",actionId:"b")},c);AdvanceCooldown(c,"p1",WDStrategicWeaponIds.EnergyPlant,5);Assert.That(p.Resolve(new[]{Action(WDStrategicWeaponIds.EnergyPlant,target:"t2",installation:"c",actionId:"c")},c).Single().State,Is.EqualTo(WDWeaponActionState.Failed));var plant=c.FindTerritory("t0").FindInstallation(WDStrategicWeaponIds.EnergyPlant);c.FindTerritory("t0").Capture("p2");Assert.That(plant.OwnerPlayerId,Is.EqualTo("p2"));Assert.That(c.GetOwnEnergyPlantActivations("p2"),Is.Zero);}

    [Test]
    public void EmptyTerritoryAbandonsStrategicInstallationsAndLaterOccupationCapturesThem()
    {var t=new WDWeaponTerritoryState("t","p1",1);var plant=Installation("p",WDStrategicWeaponIds.EnergyPlant,"t","p1",2,0,0,2);t.AddInstallation(plant);t.MakeEmpty();Assert.That(plant.IsAbandoned,Is.True);Assert.That(plant.IsDestroyed,Is.False);t.Capture("p2");Assert.That(plant.OwnerPlayerId,Is.EqualTo("p2"));Assert.That(plant.PaymentsRemaining,Is.EqualTo(2));}

    // ============================================================
    // 04. BOMBA, ARTILHARIA E FOGUETES
    // ============================================================

    [Test]
    public void LongRangeBombWorksAtAnyDistanceUsesFlakBunkerAndNeverConquers()
    {var c=Context(0.99);Territory(c,"origin","p1",1);var t=Territory(c,"far","p2",5);t.AddInstallation(Installation("b",WDSupportWeaponIds.Bunker,"far","p2",4,5));Resolve(c,Action(WDStrategicWeaponIds.LongRangeBomb,target:"far"),6);Assert.That(t.OwnerPlayerId,Is.EqualTo("p2"));Assert.That(t.Troops,Is.EqualTo(3));}

    [TestCase(0.0,WDWeaponExecutionOutcome.Intercepted)] [TestCase(.99,WDWeaponExecutionOutcome.Executed)]
    public void LongRangeBombFlakOutcomeIsDeterministic(double roll,WDWeaponExecutionOutcome expected)
    {var c=Context(roll);var t=Territory(c,"t","p2",5);t.AddInstallation(Installation("f",WDSupportWeaponIds.FlakTower,"t","p2",4));Assert.That(Resolve(c,Action(WDStrategicWeaponIds.LongRangeBomb,target:"t"),1).ExecutionOutcome,Is.EqualTo(expected));}

    [Test]
    public void PeacePreventsLongRangeBombWithoutCost()
    {var c=Context();Territory(c,"t","p2",5);var peace=Action(WDStrategicWeaponIds.Peace,targetPlayer:"p2");var bomb=Action(WDStrategicWeaponIds.LongRangeBomb,target:"t",actionId:"bomb");var r=Pipeline().Resolve(new[]{bomb,peace},c);Assert.That(r.Single(x=>x.ActionId=="bomb").State,Is.EqualTo(WDWeaponActionState.Annulled));Assert.That(c.FindPlayer("p1").Energy,Is.EqualTo(85));}

    [Test]
    public void BombProgrammedAgainstAlreadyEmptyTargetStillExecutes()
    {var c=Context();var t=new WDWeaponTerritoryState("t","",0,WDWeaponTerritoryStateKind.Empty);c.AddTerritory(t);Assert.That(Resolve(c,Action(WDStrategicWeaponIds.LongRangeBomb,target:"t"),1).State,Is.EqualTo(WDWeaponActionState.Executed));}

    [Test]
    public void FreshArtilleryFiresSameRoundAtTopThreeEnemiesInStableOrder()
    {var c=Context();var o=Territory(c,"o","p1",1);foreach(var x in new[]{("d",4),("b",7),("a",7),("c",5)}){var t=Territory(c,x.Item1,"p2",x.Item2);Link(o,t);}Pipeline().Resolve(new[]{Action(WDStrategicWeaponIds.Artillery,target:"o",installation:"art")},c);Assert.That(c.FindTerritory("a").Troops,Is.EqualTo(6));Assert.That(c.FindTerritory("b").Troops,Is.EqualTo(6));Assert.That(c.FindTerritory("c").Troops,Is.EqualTo(4));Assert.That(c.FindTerritory("d").Troops,Is.EqualTo(4));}

    [Test]
    public void SniperImmobilizationPreventsShotWithoutConsumingOperation()
    {var c=Context();var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",4);Link(o,t);o.Immobilize();var art=Installation("a",WDStrategicWeaponIds.Artillery,"o","p1",2);o.AddInstallation(art);Pipeline().Resolve(Array.Empty<WDWeaponAction>(),c);Assert.That(t.Troops,Is.EqualTo(4));Assert.That(art.ChargesRemaining,Is.EqualTo(6));}

    [Test]
    public void FutureInfiltratorImmobilizationPreventsShotWithoutConsumingOperation()
    {var c=Context();var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",4);Link(o,t);o.Immobilize();var art=Installation("a",WDStrategicWeaponIds.Artillery,"o","p1",3);o.AddInstallation(art);new WDArtilleryOperationHandler().Execute(c);Assert.That(t.Troops,Is.EqualTo(4));Assert.That(art.ChargesRemaining,Is.EqualTo(9));}

    [Test]
    public void AlliedNeighboursDoNotConsumeArtilleryOperation()
    {var c=Context();c.SetAllied("p1","p2");var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",4);Link(o,t);var art=Installation("a",WDStrategicWeaponIds.Artillery,"o","p1",2);o.AddInstallation(art);new WDArtilleryOperationHandler().Execute(c);Assert.That(t.Troops,Is.EqualTo(4));Assert.That(art.ChargesRemaining,Is.EqualTo(6));}

    [Test]
    public void PeaceProtectedOnlyNeighbourDoesNotConsumeArtilleryOperation()
    {var c=Context();var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",4);Link(o,t);c.EstablishPeace("p1","p2");var art=Installation("a",WDStrategicWeaponIds.Artillery,"o","p1",2);o.AddInstallation(art);new WDArtilleryOperationHandler().Execute(c);Assert.That(t.Troops,Is.EqualTo(4));Assert.That(art.ChargesRemaining,Is.EqualTo(6));}

    [TestCase(WDWeaponTerritoryStateKind.Neutral,1)]
    [TestCase(WDWeaponTerritoryStateKind.Empty,0)]
    public void NeutralOrEmptyNeighbourConsumesOperation(WDWeaponTerritoryStateKind state,int troops)
    {var c=Context();var o=Territory(c,"o","p1",1);var t=new WDWeaponTerritoryState("t","",troops,state);c.AddTerritory(t);Link(o,t);var art=Installation("a",WDStrategicWeaponIds.Artillery,"o","p1",2);o.AddInstallation(art);new WDArtilleryOperationHandler().Execute(c);Assert.That(art.ChargesRemaining,Is.EqualTo(5));Assert.That(t.State,Is.EqualTo(WDWeaponTerritoryStateKind.Empty));}

    [Test]
    public void ArtilleryCardCooldownIsIndependentFromOperationalDuration()
    {var c=Context();c.FindPlayer("p1").GetWeapon(WDStrategicWeaponIds.Artillery).SetLevel(4,4);var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",10);Link(o,t);var p=Pipeline();p.Resolve(new[]{Action(WDStrategicWeaponIds.Artillery,target:"o",installation:"art")},c);var entry=c.FindPlayer("p1").GetWeapon(WDStrategicWeaponIds.Artillery);var art=o.FindInstallation(WDStrategicWeaponIds.Artillery);Assert.That(entry.CooldownRemaining,Is.EqualTo(3));Assert.That(art.ChargesRemaining,Is.EqualTo(8));o.Immobilize();for(int round=2;round<=4;round++){c.Round=round;entry.AdvanceRound();p.Resolve(Array.Empty<WDWeaponAction>(),c);}Assert.That(entry.CooldownRemaining,Is.Zero);Assert.That(art.ChargesRemaining,Is.EqualTo(8));o.ClearImmobilization();c.Round=5;p.Resolve(Array.Empty<WDWeaponAction>(),c);Assert.That(t.Troops,Is.EqualTo(4));Assert.That(art.ChargesRemaining,Is.EqualTo(7));}

    [Test]
    public void ArtilleryRespectsPeaceAndBunker()
    {var c=Context();var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",4);Link(o,t);t.AddInstallation(Installation("b",WDSupportWeaponIds.Bunker,"t","p2",1,2));o.AddInstallation(Installation("a",WDStrategicWeaponIds.Artillery,"o","p1",4,0,0,2));c.EstablishPeace("p1","p2");new WDArtilleryOperationHandler().Execute(c);Assert.That(t.Troops,Is.EqualTo(4));Assert.That(t.FindInstallation(WDSupportWeaponIds.Bunker).BarrierRemaining,Is.EqualTo(2));}

    [Test]
    public void RocketsCanInstallAndFireSameRoundWithoutSecondEnergyCharge()
    {var c=Context(.99);var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",5);Link(o,t);var install=Action(WDStrategicWeaponIds.Rockets,target:"o",installation:"r");var fire=Action(WDStrategicWeaponIds.Rockets,origin:"o",target:"t",installation:"r",phase:WDWeaponResolutionPhase.ProgrammedOffense,actionId:"fire");Pipeline().Resolve(new[]{fire,install},c);Assert.That(c.FindPlayer("p1").Energy,Is.EqualTo(75));Assert.That(o.FindInstallation(WDStrategicWeaponIds.Rockets).ChargesRemaining,Is.EqualTo(1));Assert.That(t.Troops,Is.EqualTo(3));}

    [Test]
    public void RocketPeaceOrSniperPreventsLaunchWithoutConsumingShot()
    {var c=Context();var o=Territory(c,"o","p1",2);var t=Territory(c,"t","p2",5);Link(o,t);var rocket=Installation("r",WDStrategicWeaponIds.Rockets,"o","p1",1,0,2);o.AddInstallation(rocket);c.EstablishPeace("p1","p2");var handler=new WDRocketHandler();var action=Action(WDStrategicWeaponIds.Rockets,origin:"o",target:"t",installation:"r",phase:WDWeaponResolutionPhase.ProgrammedOffense);Assert.That(handler.Revalidate(c,action,Definition(WDStrategicWeaponIds.Rockets)).Outcome,Is.EqualTo(WDWeaponRevalidationOutcome.Annulled));Assert.That(rocket.ChargesRemaining,Is.EqualTo(2));c.ClearPeace();o.Immobilize();Assert.That(handler.Revalidate(c,action,Definition(WDStrategicWeaponIds.Rockets)).Outcome,Is.EqualTo(WDWeaponRevalidationOutcome.Annulled));Assert.That(rocket.ChargesRemaining,Is.EqualTo(2));}

    [TestCase(0.0,WDWeaponExecutionOutcome.Intercepted)] [TestCase(.99,WDWeaponExecutionOutcome.Executed)]
    public void LaunchedRocketConsumesShotWhetherFlakInterceptsOrNot(double roll,WDWeaponExecutionOutcome expected)
    {var c=Context(roll);var o=Territory(c,"o","p1",1);var t=Territory(c,"t","p2",5);Link(o,t);var r=Installation("r",WDStrategicWeaponIds.Rockets,"o","p1",1,0,2);o.AddInstallation(r);t.AddInstallation(Installation("f",WDSupportWeaponIds.FlakTower,"t","p2",4));var result=Pipeline().Resolve(new[]{Action(WDStrategicWeaponIds.Rockets,origin:"o",target:"t",installation:"r",phase:WDWeaponResolutionPhase.ProgrammedOffense)},c).Single();Assert.That(result.ExecutionOutcome,Is.EqualTo(expected));Assert.That(r.ChargesRemaining,Is.EqualTo(1));}

    [Test]
    public void SecondRocketShotDestroysInstallationAndCapturePreservesCharges()
    {var o=new WDWeaponTerritoryState("o","p1",1);var r=Installation("r",WDStrategicWeaponIds.Rockets,"o","p1",2,0,1);o.AddInstallation(r);o.Capture("p2");Assert.That(r.OwnerPlayerId,Is.EqualTo("p2"));Assert.That(r.ChargesRemaining,Is.EqualTo(1));r.ConsumeCharge();if(r.ChargesRemaining==0)r.Destroy();Assert.That(r.IsDestroyed,Is.True);}

    // ============================================================
    // 05. FIXTURE AUTORITATIVO
    // ============================================================

    private static IReadOnlyList<WDWeaponDefinition> Definitions()=>new[]{Make(WDStrategicWeaponIds.Sabotage,6,20,3,1,WDWeaponBehaviorType.Interference,WDWeaponResolutionPhase.Interference,false,false,true),Make(WDStrategicWeaponIds.EnergyPlant,4,0,5,2,WDWeaponBehaviorType.Installation,WDWeaponResolutionPhase.InstallationAndLogistics,false,true),Make(WDStrategicWeaponIds.LongRangeBomb,6,25,3,0,WDWeaponBehaviorType.Specialized,WDWeaponResolutionPhase.ProgrammedOffense,false,true),Make(WDStrategicWeaponIds.Peace,1,15,3,0,WDWeaponBehaviorType.Interference,WDWeaponResolutionPhase.Peace,false,false,true),Make(WDStrategicWeaponIds.Artillery,4,30,3,0,WDWeaponBehaviorType.AutomaticInstallation,WDWeaponResolutionPhase.InstallationAndLogistics,false,true),Make(WDStrategicWeaponIds.Rockets,6,25,3,0,WDWeaponBehaviorType.Installation,WDWeaponResolutionPhase.InstallationAndLogistics,false,true)};
    private static WDWeaponDefinition Definition(string id)=>Definitions().Single(x=>x.Id==id);
    private static WDWeaponDefinition Make(string id,int levels,int energy,int cooldown,int uses,WDWeaponBehaviorType behavior,WDWeaponResolutionPhase phase,bool origin=false,bool target=false,bool player=false){var d=new WDWeaponDefinition(id,id,"strategic",null);d.ConfigureRuntime(levels,energy,cooldown,uses,behavior,phase,WDWeaponCostTiming.OnExecution,origin,target,player,id==WDStrategicWeaponIds.LongRangeBomb,id==WDStrategicWeaponIds.EnergyPlant||id==WDStrategicWeaponIds.Artillery||id==WDStrategicWeaponIds.Rockets);return d;}
    private static TestContext Context(double roll=.99){var c=new TestContext(Definitions(),roll);c.AddPlayer("p1",100,1);c.AddPlayer("p2",100,1);c.AddPlayer("p3",100,1);return c;}
    private static WDWeaponAction Action(string id,string origin="",string target="",string installation="",string author="p1",string targetPlayer="",string actionId="action",WDWeaponResolutionPhase? phase=null){var d=Definition(id);return new WDWeaponAction(actionId,author,id,1,phase??d.ResolutionPhase,0,origin,target,targetPlayer,installation);}
    private static WDWeaponTerritoryState Territory(TestContext c,string id,string owner,int troops){var t=new WDWeaponTerritoryState(id,owner,troops);c.AddTerritory(t);return t;}private static void Link(WDWeaponTerritoryState a,WDWeaponTerritoryState b){a.AddNeighbour(b.TerritoryId);b.AddNeighbour(a.TerritoryId);}private static WDWeaponInstallationState Installation(string id,string weapon,string territory,string owner,int level,int barrier=0,int charges=0,int payments=0){int duration=weapon==WDStrategicWeaponIds.Artillery?0:3;if(weapon==WDStrategicWeaponIds.Artillery&&charges==0)charges=WDStrategicWeaponTables.ArtilleryAmmunition(level);return new(id,weapon,territory,owner,duration,barrier,charges,payments,level,true,1);}
    private static WDWeaponResolutionPipeline Pipeline(){var p=new WDWeaponResolutionPipeline();p.RegisterSupportWeapons();p.RegisterTacticalWeapons();p.RegisterStrategicWeapons();return p;}private static WDWeaponActionResult Resolve(TestContext c,WDWeaponAction a,int level){c.FindPlayer(a.AuthorPlayerId).GetWeapon(a.WeaponId).SetLevel(level,6);return Pipeline().Resolve(new[]{a},c).Single();}
    private static void AdvanceCooldown(TestContext c,string player,string weapon,int rounds){for(int i=0;i<rounds;i++)c.FindPlayer(player).GetWeapon(weapon).AdvanceRound();}
    private sealed class FixedRandom:IWDWeaponRandomSource{readonly double v;public FixedRandom(double v)=>this.v=v;public double NextUnit(string key)=>v;}
    private sealed class TestContext:IWDStrategicWeaponContext,IWDWeaponTerritoryEnumerable
    {
        readonly Dictionary<string,WDWeaponDefinition> defs;readonly Dictionary<string,WDWeaponPlayerRuntimeState> players=new();readonly Dictionary<string,WDWeaponTerritoryState> territories=new();readonly HashSet<string> peace=new(),blocked=new(),sabotaged=new(),allies=new();readonly Dictionary<string,int> limits=new(),consumed=new(),activations=new();public readonly Dictionary<string,int> Natural=new(),Independent=new();public int Round=1;
        public TestContext(IEnumerable<WDWeaponDefinition>d,double roll){defs=d.ToDictionary(x=>x.Id);Random=new FixedRandom(roll);}public int CurrentRound=>Round;public IWDWeaponRandomSource Random{get;}public IEnumerable<WDWeaponTerritoryState> Territories=>territories.Values;public IEnumerable<WDMercenaryPayment> MercenaryPayments=>Array.Empty<WDMercenaryPayment>();
        public void AddPlayer(string id,int energy,int level){var p=new WDWeaponPlayerRuntimeState(id,energy);foreach(var d in defs.Values)p.RegisterWeapon(d.Id,level);players[id]=p;}public void ReplacePlayer(string id,int energy,int level)=>AddPlayer(id,energy,level);public void AddTerritory(WDWeaponTerritoryState t)=>territories[t.TerritoryId]=t;public void SetAllied(string a,string b)=>allies.Add(Pair(a,b));string Pair(string a,string b)=>string.CompareOrdinal(a,b)<0?a+":"+b:b+":"+a;
        public WDWeaponDefinition FindDefinition(string id)=>defs.TryGetValue(id,out var d)?d:null;public WDWeaponPlayerRuntimeState FindPlayer(string id)=>players.TryGetValue(id,out var p)?p:null;public int GetPlayerOrder(string id)=>id=="p1"?0:id=="p2"?1:2;public WDWeaponTerritoryState FindTerritory(string id)=>territories.TryGetValue(id??"",out var t)?t:null;public bool AreAllied(string a,string b)=>allies.Contains(Pair(a,b));public bool EstablishPeace(string a,string b)=>peace.Add(Pair(a,b));public bool HasPeace(string a,string b)=>!string.IsNullOrEmpty(a)&&!string.IsNullOrEmpty(b)&&peace.Contains(Pair(a,b));public void ClearPeace()=>peace.Clear();public bool TryClaimSabotage(string id,string action)=>sabotaged.Add(id);
        public int GetNaturalReinforcement(string id)=>Natural.TryGetValue(id,out int v)?v:0;public void SetNaturalReinforcement(string id,int amount)=>Natural[id]=amount;public int GetIndependentReinforcement(string id)=>Independent.TryGetValue(id,out int v)?v:0;public int GetOwnEnergyPlantActivations(string id)=>activations.TryGetValue(id,out int v)?v:0;public void RecordOwnEnergyPlantActivation(string id)=>activations[id]=GetOwnEnergyPlantActivations(id)+1;
        public bool TryBlockTransfers(string id,string action)=>blocked.Add(id);public bool AreTransfersBlocked(string id)=>blocked.Contains(id);public void SetTerrestrialActionLimit(string id,int value)=>limits[id]=value;public int GetTerrestrialActionLimit(string id)=>limits.TryGetValue(id,out int v)?v:3;public void ConsumeTerrestrialActionSlot(string id)=>consumed[id]=GetConsumedTerrestrialActionSlots(id)+1;public int GetConsumedTerrestrialActionSlots(string id)=>consumed.TryGetValue(id,out int v)?v:0;public int GetTerritoriesLostLastRound(string id)=>0;public WDPreviousRoundTerritoryRecord FindPreviousRoundRecord(string id)=>null;public void AddReinforcementPool(string id,int amount,string source)=>Independent[id]=GetIndependentReinforcement(id)+amount;public void AddMercenaryPayment(WDMercenaryPayment p){}
    }
}

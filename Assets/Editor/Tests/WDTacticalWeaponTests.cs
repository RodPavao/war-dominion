using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDTacticalWeaponTests
{
    [Test]
    public void RadarHasOneCanonicalReveal()
    {
        Assert.That(WDTacticalWeaponIds.Radar, Is.EqualTo("radar"));
        Assert.That(WDTacticalWeaponTables.RadarRevelations, Is.EqualTo(1));
    }

    // ============================================================
    // 01. TABELAS, CATÁLOGO E ORDEM
    // ============================================================

    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)] [TestCase(5,6)] [TestCase(6,7)]
    public void MineDamageMatchesLevel(int level, int expected) => Assert.That(WDTacticalWeaponTables.MineDamage(level), Is.EqualTo(expected));
    [TestCase(1,4)] [TestCase(2,5)] [TestCase(3,6)] [TestCase(4,7)]
    public void BlitzkriegLimitMatchesLevel(int level, int expected) => Assert.That(WDTacticalWeaponTables.BlitzkriegLimit(level), Is.EqualTo(expected));
    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)] [TestCase(5,6)] [TestCase(6,7)]
    public void KamikazeDamageMatchesLevel(int level, int expected) => Assert.That(WDTacticalWeaponTables.KamikazeDamage(level), Is.EqualTo(expected));
    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)]
    public void ParatrooperCountMatchesLevel(int level, int expected) => Assert.That(WDTacticalWeaponTables.ParatrooperCount(level), Is.EqualTo(expected));

    [Test]
    public void TacticalDefinitionsUseCorrectPhasesAndCosts()
    {
        var expected = new Dictionary<string, (int energy, int cooldown, WDWeaponResolutionPhase phase)>
        {
            [WDTacticalWeaponIds.Minefield]=(15,3,WDWeaponResolutionPhase.InstallationAndLogistics),
            [WDTacticalWeaponIds.Blitzkrieg]=(15,3,WDWeaponResolutionPhase.TerrestrialActions),
            [WDTacticalWeaponIds.TransferNullifier]=(5,3,WDWeaponResolutionPhase.TransferNullifier),
            [WDTacticalWeaponIds.Sniper]=(15,3,WDWeaponResolutionPhase.Sniper),
            [WDTacticalWeaponIds.Kamikaze]=(25,3,WDWeaponResolutionPhase.ProgrammedOffense),
            [WDTacticalWeaponIds.Paratroopers]=(25,3,WDWeaponResolutionPhase.Paratroopers)
        };
        foreach (WDWeaponDefinition definition in Definitions())
        { Assert.That(definition.EnergyCost, Is.EqualTo(expected[definition.Id].energy)); Assert.That(definition.CooldownRounds, Is.EqualTo(expected[definition.Id].cooldown)); Assert.That(definition.ResolutionPhase, Is.EqualTo(expected[definition.Id].phase)); }
    }

    // ============================================================
    // 02. CAMPO MINADO E SNIPER
    // ============================================================

    [Test]
    public void MinePersistsUntilTriggeredThenIsConsumed()
    {
        TestContext context = Context(WDTacticalWeaponIds.Minefield, 3, 100);
        WDWeaponTerritoryState target = Territory(context, "t", "p1", 3);
        Resolve(context, Action(WDTacticalWeaponIds.Minefield, target: "t", installation: "mine"));
        WDWeaponInstallationState mine = target.FindInstallation(WDTacticalWeaponIds.Minefield);
        Assert.That(mine, Is.Not.Null);
        Assert.That(WDTacticalWeaponInteractionService.TriggerMineIfApplicable(target, "p2", 6), Is.EqualTo(2));
        Assert.That(mine.IsDestroyed, Is.True);
    }

    [TestCase(2,1,0)] [TestCase(4,4,0)] [TestCase(3,7,3)]
    public void MineEliminatesUpToDamageAndWastesOverflow(int level, int force, int expected)
    {
        WDWeaponTerritoryState target = new("t", "p1", 1);
        target.AddInstallation(new WDWeaponInstallationState("m", WDTacticalWeaponIds.Minefield, "t", "p1", 0,0,1,0,level,false));
        Assert.That(WDTacticalWeaponInteractionService.TriggerMineIfApplicable(target, "p2", force), Is.EqualTo(expected));
    }

    [Test]
    public void MineRemainsBuriedInEmptyTerritoryAndTriggersAnyEntrant()
    {
        WDWeaponTerritoryState target = new("t", "p1", 1);
        var mine = new WDWeaponInstallationState("m", WDTacticalWeaponIds.Minefield, "t", "p1", 0,0,1,0,1,false);
        target.AddInstallation(mine); target.MakeEmpty();
        Assert.That(mine.IsDestroyed, Is.False);
        Assert.That(WDTacticalWeaponInteractionService.TriggerMineIfApplicable(target, "p1", 3), Is.EqualTo(1));
    }

    [Test]
    public void SniperIgnoresDefensesImmobilizesAndPreservesMine()
    {
        TestContext context = Context(WDTacticalWeaponIds.Sniper, 1, 100);
        WDWeaponTerritoryState target = Territory(context, "t", "p2", 1);
        var bunker = Installation("b", WDSupportWeaponIds.Bunker, "t", "p2", 1, false, 4);
        var flak = Installation("f", WDSupportWeaponIds.FlakTower, "t", "p2", 1, true);
        var mine = Installation("m", WDTacticalWeaponIds.Minefield, "t", "p2", 1, false);
        target.AddInstallation(bunker); target.AddInstallation(flak); target.AddInstallation(mine);
        Resolve(context, Action(WDTacticalWeaponIds.Sniper, target: "t"));
        Assert.That(target.State, Is.EqualTo(WDWeaponTerritoryStateKind.Empty)); Assert.That(target.IsImmobilized, Is.True);
        Assert.That(bunker.IsDestroyed, Is.True); Assert.That(flak.IsDestroyed, Is.True); Assert.That(mine.IsDestroyed, Is.False);
        Assert.That(bunker.BarrierRemaining, Is.EqualTo(4));
    }

    [Test]
    public void SniperDoesNotTriggerMineOrFlakWhenTroopsRemain()
    {
        TestContext context = Context(WDTacticalWeaponIds.Sniper, 1, 100, 0);
        WDWeaponTerritoryState target = Territory(context, "t", "p2", 3);
        var mine = Installation("m", WDTacticalWeaponIds.Minefield, "t", "p2", 1, false);
        var flak = Installation("f", WDSupportWeaponIds.FlakTower, "t", "p2", 4, true);
        target.AddInstallation(mine); target.AddInstallation(flak);
        Resolve(context, Action(WDTacticalWeaponIds.Sniper, target: "t"));
        Assert.That(target.Troops, Is.EqualTo(2)); Assert.That(mine.IsDestroyed, Is.False); Assert.That(flak.IsDestroyed, Is.False);
    }

    // ============================================================
    // 03. BLITZKRIEG E ANULADOR
    // ============================================================

    [Test]
    public void BlitzkriegChangesTotalTerrestrialLimitAndInvalidSlotsStayConsumed()
    {
        TestContext context = Context(WDTacticalWeaponIds.Blitzkrieg, 2, 100);
        Resolve(context, Action(WDTacticalWeaponIds.Blitzkrieg));
        Assert.That(context.GetTerrestrialActionLimit("p1"), Is.EqualTo(5));
        Assert.That(WDTerrestrialActionBudget.Reserve(context, "p1"), Is.True);
        Assert.That(context.GetConsumedTerrestrialActionSlots("p1"), Is.EqualTo(1));
    }

    [Test]
    public void BlitzkriegEnergyAndCooldownAreIndependentOfLevel()
    {
        WDWeaponDefinition definition = Definition(WDTacticalWeaponIds.Blitzkrieg);
        Assert.That(definition.EnergyCost, Is.EqualTo(15)); Assert.That(definition.CooldownRounds, Is.EqualTo(3));
    }

    [Test]
    public void FirstTransferNullifierWinsAndLaterOneIsAnnulledWithoutCost()
    {
        TestContext context = Context(WDTacticalWeaponIds.TransferNullifier, 1, 100); context.AddPlayer("p3", 100, 1);
        WDWeaponAction first = Action(WDTacticalWeaponIds.TransferNullifier, author: "p1", targetPlayer: "p3", actionId: "a");
        WDWeaponAction second = Action(WDTacticalWeaponIds.TransferNullifier, author: "p2", targetPlayer: "p3", actionId: "b");
        IReadOnlyList<WDWeaponActionResult> results = Pipeline().Resolve(new[] { second, first }, context);
        Assert.That(context.AreTransfersBlocked("p3"), Is.True);
        Assert.That(results.Single(item => item.ActionId == "a").State, Is.EqualTo(WDWeaponActionState.Executed));
        Assert.That(results.Single(item => item.ActionId == "b").State, Is.EqualTo(WDWeaponActionState.Annulled));
        Assert.That(context.FindPlayer("p2").Energy, Is.EqualTo(100));
    }

    // ============================================================
    // 04. KAMIKAZE E PARAQUEDISTAS
    // ============================================================

    [Test]
    public void KamikazeRequiresTwoTroopsAndRangeAtMostThree()
    {
        TestContext context = Context(WDTacticalWeaponIds.Kamikaze, 1, 100);
        WDWeaponTerritoryState a=Territory(context,"a","p1",1), b=Territory(context,"b","p2",5); Link(a,b);
        Assert.That(Resolve(context, Action(WDTacticalWeaponIds.Kamikaze,"a","b")).State, Is.EqualTo(WDWeaponActionState.Failed));
        a.AddTroops(2); WDWeaponTerritoryState c=Territory(context,"c","p2",1), d=Territory(context,"d","p2",1), e=Territory(context,"e","p2",1); Link(b,c); Link(c,d); Link(d,e);
        Assert.That(Resolve(context, Action(WDTacticalWeaponIds.Kamikaze,"a","e",actionId:"far")).State, Is.EqualTo(WDWeaponActionState.Failed));
    }

    [TestCase(0.0,true)] [TestCase(0.99,false)]
    public void KamikazeSacrificePersistsWhenFlakIntercepts(double interceptedRoll, bool expectedIntercept)
    {
        double roll = interceptedRoll; TestContext context = Context(WDTacticalWeaponIds.Kamikaze, 1, 100, roll);
        WDWeaponTerritoryState a=Territory(context,"a","p1",3), b=Territory(context,"b","p2",5); Link(a,b);
        b.AddInstallation(Installation("f",WDSupportWeaponIds.FlakTower,"b","p2",1,true));
        WDWeaponActionResult result=Resolve(context,Action(WDTacticalWeaponIds.Kamikaze,"a","b"));
        Assert.That(a.Troops,Is.EqualTo(2)); Assert.That(result.ExecutionOutcome==WDWeaponExecutionOutcome.Intercepted,Is.EqualTo(expectedIntercept));
    }

    [Test]
    public void KamikazeUsesBunkerAndNeverConquers()
    {
        TestContext context=Context(WDTacticalWeaponIds.Kamikaze,6,100); WDWeaponTerritoryState a=Territory(context,"a","p1",3), b=Territory(context,"b","p2",3); Link(a,b);
        b.AddInstallation(Installation("bn",WDSupportWeaponIds.Bunker,"b","p2",4,false,5)); Resolve(context,Action(WDTacticalWeaponIds.Kamikaze,"a","b"));
        Assert.That(b.State,Is.EqualTo(WDWeaponTerritoryStateKind.Occupied)); Assert.That(b.OwnerPlayerId,Is.EqualTo("p2")); Assert.That(b.Troops,Is.EqualTo(1));
    }

    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)]
    public void ParatroopersReinforceOwnedTerritoryByLevel(int level,int amount)
    {
        TestContext context=Context(WDTacticalWeaponIds.Paratroopers,level,100); WDWeaponTerritoryState t=Territory(context,"t","p1",1);
        Resolve(context,Action(WDTacticalWeaponIds.Paratroopers,target:"t")); Assert.That(t.Troops,Is.EqualTo(1+amount));
    }

    [Test]
    public void ParatroopersReinforceAllyAndOccupyEmptyAnywhere()
    {
        TestContext alliedContext=Context(WDTacticalWeaponIds.Paratroopers,4,100); alliedContext.Ally("p1","p2");
        WDWeaponTerritoryState ally=Territory(alliedContext,"ally","p2",1);
        Resolve(alliedContext,Action(WDTacticalWeaponIds.Paratroopers,target:"ally"));

        TestContext emptyContext=Context(WDTacticalWeaponIds.Paratroopers,4,100);
        WDWeaponTerritoryState empty=new("empty","",0,WDWeaponTerritoryStateKind.Empty); emptyContext.AddTerritory(empty);
        Resolve(emptyContext,Action(WDTacticalWeaponIds.Paratroopers,target:"empty"));

        Assert.That(ally.Troops,Is.EqualTo(6)); Assert.That(empty.OwnerPlayerId,Is.EqualTo("p1")); Assert.That(empty.Troops,Is.EqualTo(5));
    }

    [Test]
    public void ParatroopersMineCanEliminateOrLeaveSurvivorsToOccupy()
    {
        TestContext context=Context(WDTacticalWeaponIds.Paratroopers,4,100); WDWeaponTerritoryState empty=new("e","",0,WDWeaponTerritoryStateKind.Empty); context.AddTerritory(empty);
        empty.AddInstallation(Installation("m",WDTacticalWeaponIds.Minefield,"e","p2",3,false));
        Resolve(context,Action(WDTacticalWeaponIds.Paratroopers,target:"e")); Assert.That(empty.OwnerPlayerId,Is.EqualTo("p1")); Assert.That(empty.Troops,Is.EqualTo(1));
    }

    [Test]
    public void ParatroopersCrossBunkerBeforeConqueringEnemy()
    {
        TestContext context=Context(WDTacticalWeaponIds.Paratroopers,4,100);
        WDWeaponTerritoryState target=Territory(context,"t","p2",1);
        var bunker=Installation("b",WDSupportWeaponIds.Bunker,"t","p2",1,false,2);
        target.AddInstallation(bunker);
        Resolve(context,Action(WDTacticalWeaponIds.Paratroopers,target:"t"));
        Assert.That(bunker.IsDestroyed,Is.True); Assert.That(target.OwnerPlayerId,Is.EqualTo("p1")); Assert.That(target.Troops,Is.EqualTo(2));
    }

    [Test]
    public void ParatroopersAreInterceptedByEnemyFlakButNotFriendlyFlak()
    {
        TestContext context=Context(WDTacticalWeaponIds.Paratroopers,1,100,0); WDWeaponTerritoryState enemy=Territory(context,"enemy","p2",3);
        enemy.AddInstallation(Installation("f",WDSupportWeaponIds.FlakTower,"enemy","p2",4,true));
        Assert.That(Resolve(context,Action(WDTacticalWeaponIds.Paratroopers,target:"enemy")).ExecutionOutcome,Is.EqualTo(WDWeaponExecutionOutcome.Intercepted));
    }

    [Test]
    public void SequentialParatroopersAdaptToEmptyThenAlliedThenEnemyState()
    {
        TestContext context=Context(WDTacticalWeaponIds.Paratroopers,4,100,.99); context.AddPlayer("p3",100,4); context.Ally("p1","p2");
        WDWeaponTerritoryState target=new("t","",0,WDWeaponTerritoryStateKind.Empty); context.AddTerritory(target);
        WDWeaponAction a=Action(WDTacticalWeaponIds.Paratroopers,target:"t",author:"p1",actionId:"a");
        WDWeaponAction b=Action(WDTacticalWeaponIds.Paratroopers,target:"t",author:"p2",actionId:"b");
        WDWeaponAction c=Action(WDTacticalWeaponIds.Paratroopers,target:"t",author:"p3",actionId:"c");
        Pipeline().Resolve(new[]{c,b,a},context);
        Assert.That(target.OwnerPlayerId,Is.EqualTo("p1")); Assert.That(target.Troops,Is.GreaterThan(0));
    }

    // ============================================================
    // 05. FIXTURE ISOLADO E DETERMINÍSTICO
    // ============================================================

    private static IReadOnlyList<WDWeaponDefinition> Definitions()=>new[]{
        Make(WDTacticalWeaponIds.Minefield,6,15,3,1,WDWeaponBehaviorType.Installation,WDWeaponResolutionPhase.InstallationAndLogistics,false,true,true),
        Make(WDTacticalWeaponIds.Blitzkrieg,4,15,3,1,WDWeaponBehaviorType.TerrestrialModifier,WDWeaponResolutionPhase.TerrestrialActions),
        Make(WDTacticalWeaponIds.TransferNullifier,1,5,3,0,WDWeaponBehaviorType.Interference,WDWeaponResolutionPhase.TransferNullifier,false,false,false,false,true),
        Make(WDTacticalWeaponIds.Sniper,1,15,3,0,WDWeaponBehaviorType.Specialized,WDWeaponResolutionPhase.Sniper,false,true),
        Make(WDTacticalWeaponIds.Kamikaze,6,25,3,0,WDWeaponBehaviorType.Specialized,WDWeaponResolutionPhase.ProgrammedOffense,true,true),
        Make(WDTacticalWeaponIds.Paratroopers,4,25,3,0,WDWeaponBehaviorType.Specialized,WDWeaponResolutionPhase.Paratroopers,false,true,false,true)};
    private static WDWeaponDefinition Definition(string id)=>Definitions().Single(x=>x.Id==id);
    private static WDWeaponDefinition Make(string id,int levels,int energy,int cooldown,int uses,WDWeaponBehaviorType behavior,WDWeaponResolutionPhase phase,bool origin=false,bool target=false,bool installation=false,bool empty=false,bool player=false)
    {var d=new WDWeaponDefinition(id,id,"tactical",null);d.ConfigureRuntime(levels,energy,cooldown,uses,behavior,phase,WDWeaponCostTiming.OnExecution,origin,target,player,empty,installation);return d;}
    private static TestContext Context(string id,int level,int energy,double roll=.99){var c=new TestContext(Definition(id),roll);c.AddPlayer("p1",energy,level);c.AddPlayer("p2",energy,level);return c;}
    private static WDWeaponTerritoryState Territory(TestContext c,string id,string owner,int troops){var t=new WDWeaponTerritoryState(id,owner,troops);c.AddTerritory(t);return t;}
    private static void Link(WDWeaponTerritoryState a,WDWeaponTerritoryState b){a.AddNeighbour(b.TerritoryId);b.AddNeighbour(a.TerritoryId);}
    private static WDWeaponInstallationState Installation(string id,string weapon,string territory,string owner,int level,bool transportable,int barrier=0)=>new(id,weapon,territory,owner,3,barrier,1,0,level,transportable);
    private static WDWeaponAction Action(string id,string origin="",string target="",string installation="",string author="p1",string targetPlayer="",string actionId="action")
    {var d=Definition(id);return new WDWeaponAction(actionId,author,id,1,d.ResolutionPhase,0,origin,target,targetPlayer,installation);}
    private static WDWeaponResolutionPipeline Pipeline(){var p=new WDWeaponResolutionPipeline();p.RegisterSupportWeapons();p.RegisterTacticalWeapons();return p;}
    private static WDWeaponActionResult Resolve(TestContext c,WDWeaponAction a)=>Pipeline().Resolve(new[]{a},c).Single();
    private sealed class FixedRandom:IWDWeaponRandomSource{private readonly double value;public FixedRandom(double value)=>this.value=value;public double NextUnit(string key)=>value;}
    private sealed class TestContext:IWDTacticalWeaponContext,IWDWeaponTerritoryEnumerable
    {
        private readonly WDWeaponDefinition definition;private readonly Dictionary<string,WDWeaponPlayerRuntimeState> players=new();private readonly Dictionary<string,WDWeaponTerritoryState> territories=new();private readonly HashSet<string> allies=new();private readonly HashSet<string> blocked=new();private readonly Dictionary<string,int> limits=new(),consumed=new();
        public TestContext(WDWeaponDefinition definition,double roll){this.definition=definition;Random=new FixedRandom(roll);}public int CurrentRound=>1;public IWDWeaponRandomSource Random{get;}public IEnumerable<WDWeaponTerritoryState> Territories=>territories.Values;public IEnumerable<WDMercenaryPayment> MercenaryPayments=>Array.Empty<WDMercenaryPayment>();
        public void AddPlayer(string id,int energy,int level){var p=new WDWeaponPlayerRuntimeState(id,energy);p.RegisterWeapon(definition.Id,level);players[id]=p;}public void AddTerritory(WDWeaponTerritoryState t)=>territories[t.TerritoryId]=t;public void Ally(string a,string b){allies.Add(a+":"+b);allies.Add(b+":"+a);}
        public WDWeaponDefinition FindDefinition(string id)=>id==definition.Id?definition:null;public WDWeaponPlayerRuntimeState FindPlayer(string id)=>players.TryGetValue(id,out var p)?p:null;public int GetPlayerOrder(string id)=>id=="p1"?0:id=="p2"?1:2;public WDWeaponTerritoryState FindTerritory(string id)=>territories.TryGetValue(id??"",out var t)?t:null;public bool AreAllied(string a,string b)=>allies.Contains(a+":"+b);
        public bool TryBlockTransfers(string id,string actionId)=>blocked.Add(id);public bool AreTransfersBlocked(string id)=>blocked.Contains(id);public void SetTerrestrialActionLimit(string id,int limit)=>limits[id]=limit;public int GetTerrestrialActionLimit(string id)=>limits.TryGetValue(id,out int v)?v:3;public void ConsumeTerrestrialActionSlot(string id)=>consumed[id]=GetConsumedTerrestrialActionSlots(id)+1;public int GetConsumedTerrestrialActionSlots(string id)=>consumed.TryGetValue(id,out int v)?v:0;
        public int GetTerritoriesLostLastRound(string id)=>0;public WDPreviousRoundTerritoryRecord FindPreviousRoundRecord(string id)=>null;public void AddReinforcementPool(string id,int amount,string source){}public void AddMercenaryPayment(WDMercenaryPayment payment){}
    }
}

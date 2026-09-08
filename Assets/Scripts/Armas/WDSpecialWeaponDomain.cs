using System;
using System.Collections.Generic;
using System.Linq;

public static class WDSpecialWeaponIds
{
    public const string Drone="drone"; public const string AtomicBomb="atomic_bomb";
    public const string WarPropaganda="war_propaganda"; public const string ChemicalBomb="chemical_bomb";
    public const string Infiltrator="infiltrator";
}

public interface IWDSpecialWeaponContext:IWDStrategicWeaponContext
{
    bool TryStoreDroneRecon(string actionId,string playerId,IEnumerable<WDDroneDiscovery> discoveries);
    IReadOnlyList<WDDroneDiscovery> GetDroneRecon(string actionId,string playerId);
    bool HasProgrammedSniper(string playerId,string territoryId);
    bool TryClaimInfiltration(string territoryId,string actionId);
}

public sealed class WDDroneDiscovery
{
    public string TerritoryId{get;} public string CoveringFlakInstallationId{get;} public int CoveringFlakLevel{get;}
    public bool HasFlakCoverage=>!string.IsNullOrEmpty(CoveringFlakInstallationId);
    public WDDroneDiscovery(string territoryId,WDWeaponInstallationState flak){TerritoryId=territoryId??string.Empty;CoveringFlakInstallationId=flak?.InstallationId??string.Empty;CoveringFlakLevel=flak?.Level??0;}
}

public static class WDSpecialWeaponTables
{
    // ============================================================
    // 01. VALORES CANÔNICOS DAS CINCO ARMAS ESPECIAIS
    // ============================================================
    private static int From(int[] values,int level)=>values[Math.Clamp(level,1,values.Length)-1];
    public static int DroneDiscoveries(int level)=>level<=3?1:2;
    public static int DroneDamage(int level)=>From(new[]{2,3,4,3,4,5},level);
    public static int AtomicDamage(int level)=>From(new[]{3,4,5,6,7,8},level);
    public static int PropagandaLimit(int level)=>From(new[]{3,4,5,6,7,8},level);
    public static double ChemicalRate(int level)=>From(new[]{25,30,35,40,45,50},level)/100d;
}

public static class WDSpecialWeaponParameters
{
    // ============================================================
    // 02. PARÂMETROS AUDITÁVEIS DE RECONHECIMENTO
    // ============================================================
    public static string Get(WDWeaponAction action,string key)=>action?.Parameters.FirstOrDefault(x=>x.Key==key)?.Value??string.Empty;
    public static string[] List(WDWeaponAction action,string key)=>Get(action,key).Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x.Length>0).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
}

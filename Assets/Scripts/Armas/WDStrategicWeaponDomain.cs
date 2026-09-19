using System;
using System.Collections.Generic;
using System.Linq;

public static class WDStrategicWeaponIds
{
    public const string Sabotage = "sabotage";
    public const string EnergyPlant = "energy_plant";
    public const string LongRangeBomb = "long_range_bomb";
    public const string Peace = "peace";
    public const string Artillery = "artillery";
    public const string Rockets = "rockets";
}

public interface IWDStrategicWeaponContext : IWDTacticalWeaponContext
{
    bool EstablishPeace(string firstPlayerId, string secondPlayerId);
    bool HasPeace(string firstPlayerId, string secondPlayerId);
    void ClearPeace();
    bool TryClaimSabotage(string targetPlayerId, string actionId);
    int GetNaturalReinforcement(string playerId);
    void SetNaturalReinforcement(string playerId, int amount);
    int GetIndependentReinforcement(string playerId);
    int GetOwnEnergyPlantActivations(string playerId);
    void RecordOwnEnergyPlantActivation(string playerId);
}

public static class WDStrategicWeaponTables
{
    // ============================================================
    // 01. VALORES CANÔNICOS DA CATEGORIA ESTRATÉGICA
    // ============================================================

    public static double SabotageRate(int level) => From(new[] { 15,20,25,30,35,40 }, level) / 100d;
    public static int EnergyProduction(int level) => From(new[] { 5,10,15,20 }, level);
    public static int LongRangeBombDamage(int level) => From(new[] { 2,3,4,5,6,7 }, level);
    public static int RocketDamage(int level) => From(new[] { 2,3,4,5,6,7 }, level);
    public static int ArtilleryDamage(int level) => From(new[] { 1,2,2,3 }, level);
    public static int ArtilleryAmmunition(int level) => level <= 2 ? 6 : 9;
    private static int From(int[] values, int level) => values[Math.Clamp(level,1,values.Length)-1];
}

public static class WDStrategicInstallationService
{
    // ============================================================
    // 02. CONSULTA ORDENADA DE INSTALAÇÕES PERSISTENTES
    // ============================================================

    public static IEnumerable<(WDWeaponTerritoryState territory, WDWeaponInstallationState installation)> Active(
        IWDWeaponTerritoryEnumerable board, string weaponId) =>
        board.Territories.SelectMany(territory => territory.Installations
            .Where(item => !item.IsDestroyed && item.WeaponId == weaponId)
            .Select(item => (territory: territory, installation: item)))
        .OrderBy(item => item.installation.OwnerPlayerId, StringComparer.Ordinal)
        .ThenBy(item => item.territory.TerritoryId, StringComparer.Ordinal)
        .ThenBy(item => item.installation.InstallationId, StringComparer.Ordinal);
}

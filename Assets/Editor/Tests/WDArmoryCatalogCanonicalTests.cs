using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDArmoryCatalogCanonicalTests
{
    private WDArmoryCatalogConfig catalog;

    // ============================================================
    // 01. CARREGAMENTO DO MESMO CATÁLOGO USADO PELA HOME
    // ============================================================

    [SetUp]
    public void SetUp()
    {
        catalog = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        Assert.That(catalog, Is.Not.Null);
    }

    // ============================================================
    // 02. IDENTIDADE E DISTRIBUIÇÃO CANÔNICAS
    // ============================================================

    [Test]
    public void RuntimeCatalogContainsExactlyTwentySixOfficialWeapons()
    {
        Assert.That(catalog.Weapons.Count, Is.EqualTo(26));
        Assert.That(catalog.Weapons.Select(weapon => weapon.Id).Distinct().Count(),
            Is.EqualTo(26));
        Assert.That(catalog.Weapons.Any(weapon =>
            weapon.Id.StartsWith("mock_") || weapon.DisplayName.Contains("(Mock)")),
            Is.False);
    }

    [TestCase("support", 8)]
    [TestCase("tactical", 7)]
    [TestCase("strategic", 6)]
    [TestCase("special", 5)]
    public void RuntimeCatalogHasOfficialCategoryCount(string categoryId, int expected)
    {
        Assert.That(catalog.Weapons.Count(weapon => weapon.CategoryId == categoryId),
            Is.EqualTo(expected));
    }

    [Test]
    public void EveryWeaponUsesADeclaredCategoryAndSupportedMaximumLevel()
    {
        HashSet<string> categories = catalog.Categories
            .Select(category => category.Id).ToHashSet();

        Assert.That(catalog.Weapons.All(weapon => categories.Contains(weapon.CategoryId)),
            Is.True);
        Assert.That(catalog.Weapons.All(weapon => weapon.HasSupportedLevelCount),
            Is.True);
    }

    [Test]
    public void RuntimeCatalogContainsEveryOfficialIdAndName()
    {
        var expected = new Dictionary<string, string>
        {
            ["air_strike"] = "Ataque Aéreo", ["reinforcements"] = "Reforços",
            ["air_transport"] = "Transporte Aéreo", ["flak_tower"] = "Flak Tower",
            ["bunker"] = "Bunker", ["reserve_troops"] = "Tropas Reservas",
            ["troop_recovery"] = "Recuperação de Tropas", ["mercenaries"] = "Mercenários",
            ["minefield"] = "Campo Minado", ["blitzkrieg"] = "Blitzkrieg",
            ["transfer_nullifier"] = "Anulador de Transferência", ["sniper"] = "Sniper",
            ["kamikaze"] = "Kamikaze", ["paratroopers"] = "Paraquedistas",
            ["radar"] = "Radar",
            ["sabotage"] = "Sabotagem", ["energy_plant"] = "Central de Energia",
            ["long_range_bomb"] = "Bomba de Longo Alcance", ["peace"] = "Paz",
            ["artillery"] = "Artilharia", ["rockets"] = "Foguetes",
            ["drone"] = "Drone", ["atomic_bomb"] = "Bomba Atômica",
            ["war_propaganda"] = "Propaganda de Guerra", ["chemical_bomb"] = "Bomba Química",
            ["infiltrator"] = "Infiltrado"
        };

        Assert.That(catalog.Weapons.ToDictionary(weapon => weapon.Id,
            weapon => weapon.DisplayName), Is.EquivalentTo(expected));
    }

    // ============================================================
    // 03. PARÂMETROS DE EXECUÇÃO E LOOKUP
    // ============================================================

    [TestCase("drone", 30, 3, 6)]
    [TestCase("radar", 25, 3, 1)]
    [TestCase("atomic_bomb", 40, 3, 6)]
    [TestCase("war_propaganda", 25, 3, 6)]
    [TestCase("chemical_bomb", 25, 3, 6)]
    [TestCase("infiltrator", 20, 4, 1)]
    [TestCase("sabotage", 20, 3, 6)]
    [TestCase("energy_plant", 0, 5, 4)]
    [TestCase("long_range_bomb", 25, 3, 6)]
    [TestCase("peace", 15, 3, 1)]
    [TestCase("artillery", 30, 3, 4)]
    [TestCase("rockets", 25, 3, 6)]
    [TestCase("minefield", 15, 3, 6)]
    [TestCase("blitzkrieg", 15, 3, 4)]
    [TestCase("transfer_nullifier", 5, 3, 1)]
    [TestCase("sniper", 15, 3, 1)]
    [TestCase("kamikaze", 25, 3, 6)]
    [TestCase("paratroopers", 25, 3, 4)]
    [TestCase("air_strike", 20, 3, 6)]
    [TestCase("reinforcements", 25, 3, 6)]
    [TestCase("air_transport", 5, 3, 6)]
    [TestCase("flak_tower", 5, 3, 4)]
    [TestCase("bunker", 10, 3, 4)]
    [TestCase("reserve_troops", 15, 3, 6)]
    [TestCase("troop_recovery", 15, 3, 6)]
    [TestCase("mercenaries", 20, 3, 6)]
    public void OfficialWeaponHasCanonicalRuntimeParameters(
        string id, int energy, int cooldown, int maximumLevel)
    {
        WDWeaponDefinition weapon = catalog.FindWeapon(id);

        Assert.That(weapon, Is.Not.Null, id);
        Assert.That(weapon.EnergyCost, Is.EqualTo(energy), id);
        Assert.That(weapon.CooldownRounds, Is.EqualTo(cooldown), id);
        Assert.That(weapon.MaximumLevel, Is.EqualTo(maximumLevel), id);
    }

    [Test]
    public void LookupReturnsOfficialEntriesAndRejectsRemovedMocks()
    {
        Assert.That(catalog.FindWeapon("artillery")?.DisplayName,
            Is.EqualTo("Artilharia"));
        Assert.That(catalog.FindWeapon("mock_field_support"), Is.Null);
        Assert.That(catalog.FindWeapon("unknown_weapon"), Is.Null);
    }

    [Test]
    public void DroneAndRadarUseCanonicalInstallationContracts()
    {
        WDWeaponDefinition drone = catalog.FindWeapon(WDSpecialWeaponIds.Drone);
        WDWeaponDefinition radar = catalog.FindWeapon(WDTacticalWeaponIds.Radar);

        Assert.That(drone.IsInstallation, Is.True);
        Assert.That(drone.Charges, Is.EqualTo(2));
        Assert.That(drone.CanTargetEmptyTerritory, Is.True);
        Assert.That(radar.CategoryId, Is.EqualTo("tactical"));
        Assert.That(radar.MaximumLevel, Is.EqualTo(1));
        Assert.That(radar.IsInstallation, Is.True);
        Assert.That(radar.Charges, Is.EqualTo(1));
    }
}

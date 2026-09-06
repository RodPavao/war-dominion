using System;
using System.Linq;
using NUnit.Framework;

public sealed class WDProductFoundationTests
{
    // ============================================================
    // 01. DISPONIBILIDADE ADMINISTRATIVA DE MODOS
    // ============================================================

    [Test]
    public void ModeAvailabilityRecognizesAvailableComingSoonAndHidden()
    {
        Assert.That(WDContentAvailabilityRules.IsAvailable(
            WDContentAvailability.Available), Is.True);
        Assert.That(WDContentAvailabilityRules.IsAvailable(
            WDContentAvailability.ComingSoon), Is.False);
        Assert.That(WDContentAvailabilityRules.IsVisible(
            WDContentAvailability.ComingSoon), Is.True);
        Assert.That(WDContentAvailabilityRules.IsVisible(
            WDContentAvailability.Hidden), Is.False);
    }

    // ============================================================
    // 02. DISPONIBILIDADE E COMPATIBILIDADE DE MAPAS
    // ============================================================

    [Test]
    public void EnabledMapIsAllowedAndDisabledMapIsNeverEligible()
    {
        WDContentPolicyService service = Service(
            new[]
            {
                new WDMapAvailabilityDefinition("enabled", true),
                new WDMapAvailabilityDefinition("disabled", false)
            },
            new WDModeMapCompatibilityDefinition(
                "mode", WDMapCompatibilityPolicy.All, Array.Empty<string>()));

        Assert.That(service.IsMapAllowedForMode("mode", "enabled"), Is.True);
        Assert.That(service.IsMapAllowedForMode("mode", "disabled"), Is.False);
        Assert.That(service.GetEligibleMaps("mode", new[]
        {
            new WDMapModeRule("enabled", 1f),
            new WDMapModeRule("disabled", 1f)
        }).Select(item => item.MapId), Is.EqualTo(new[] { "enabled" }));
    }

    [Test]
    public void CompatibilitySupportsAllAllowListAndDenyList()
    {
        WDMapAvailabilityDefinition[] maps =
        {
            new("a", true), new("b", true), new("c", true)
        };
        WDContentPolicyService service = Service(
            maps,
            new WDModeMapCompatibilityDefinition(
                "all", WDMapCompatibilityPolicy.All, Array.Empty<string>()),
            new WDModeMapCompatibilityDefinition(
                "allow", WDMapCompatibilityPolicy.AllowList, new[] { "a" }),
            new WDModeMapCompatibilityDefinition(
                "deny", WDMapCompatibilityPolicy.DenyList, new[] { "b" }));

        Assert.That(service.IsMapAllowedForMode("all", "a"), Is.True);
        Assert.That(service.IsMapAllowedForMode("all", "b"), Is.True);
        Assert.That(service.IsMapAllowedForMode("allow", "a"), Is.True);
        Assert.That(service.IsMapAllowedForMode("allow", "b"), Is.False);
        Assert.That(service.IsMapAllowedForMode("deny", "a"), Is.True);
        Assert.That(service.IsMapAllowedForMode("deny", "b"), Is.False);
    }

    [Test]
    public void GlobalDisableOverridesAllowList()
    {
        WDContentPolicyService service = Service(
            new[] { new WDMapAvailabilityDefinition("a", false) },
            new WDModeMapCompatibilityDefinition(
                "allow", WDMapCompatibilityPolicy.AllowList, new[] { "a" }));

        Assert.That(service.IsMapAllowedForMode("allow", "a"), Is.False);
    }

    // ============================================================
    // 03. IDIOMA E FUSO HORÁRIO INDEPENDENTES
    // ============================================================

    [Test]
    public void ChangingLanguageDoesNotChangeTimeZone()
    {
        var preferences = new WDRegionalPreferenceState(
            WDLanguage.Portuguese, "UTC", false);
        string zoneBefore = preferences.TimeZoneId;

        preferences.SetLanguage(WDLanguage.English);

        Assert.That(preferences.Language, Is.EqualTo(WDLanguage.English));
        Assert.That(preferences.TimeZoneId, Is.EqualTo(zoneBefore));
    }

    [Test]
    public void LocalizedTextContractFallsBackSafelyWithoutChangingRegion()
    {
        var preferences = new WDRegionalPreferenceState(
            WDLanguage.German, "UTC", false);
        var text = new WDLocalizedText();

        Assert.That(text.Resolve(preferences.Language), Is.EqualTo(string.Empty));
        Assert.That(preferences.TimeZoneId, Is.EqualTo("UTC"));
    }

    [Test]
    public void ChangingTimeZoneDoesNotChangeLanguage()
    {
        var preferences = new WDRegionalPreferenceState(
            WDLanguage.Italian, TimeZoneInfo.Local.Id, true);

        Assert.That(preferences.TrySetManualTimeZone("UTC"), Is.True);
        Assert.That(preferences.Language, Is.EqualTo(WDLanguage.Italian));
        Assert.That(preferences.TimeZoneId, Is.EqualTo("UTC"));
        Assert.That(preferences.UsesAutomaticTimeZone, Is.False);
    }

    [Test]
    public void EventConversionUsesConfiguredZoneIdentifier()
    {
        var preferences = new WDRegionalPreferenceState(
            WDLanguage.Spanish, "UTC", false);
        var absolute = new DateTimeOffset(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

        DateTimeOffset converted = preferences.ConvertUtc(absolute);

        Assert.That(converted.Offset, Is.EqualTo(TimeSpan.Zero));
        Assert.That(converted.UtcDateTime, Is.EqualTo(absolute.UtcDateTime));
    }

    private static WDContentPolicyService Service(
        WDMapAvailabilityDefinition[] maps,
        params WDModeMapCompatibilityDefinition[] rules) =>
        new(maps, rules);
}

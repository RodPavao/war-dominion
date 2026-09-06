using System;
using UnityEngine;

public enum WDLanguage
{
    Portuguese,
    English,
    Spanish,
    Italian,
    German
}

[Serializable]
public sealed class WDLocalizedText
{
    // ============================================================
    // 01. CONTEÚDO LOCALIZÁVEL SEM ACOPLAMENTO COM UMA TELA
    // ============================================================

    [SerializeField] private string key;
    [SerializeField, TextArea] private string portuguese;
    [SerializeField, TextArea] private string english;
    [SerializeField, TextArea] private string spanish;
    [SerializeField, TextArea] private string italian;
    [SerializeField, TextArea] private string german;

    public string Key => key;

    public string Resolve(WDLanguage language)
    {
        string localized = language switch
        {
            WDLanguage.English => english,
            WDLanguage.Spanish => spanish,
            WDLanguage.Italian => italian,
            WDLanguage.German => german,
            _ => portuguese
        };

        if (!string.IsNullOrEmpty(localized))
            return localized;
        if (!string.IsNullOrEmpty(portuguese))
            return portuguese;
        return key ?? string.Empty;
    }
}

[Serializable]
public sealed class WDRegionalPreferenceState
{
    // ============================================================
    // 02. ESTADO INDEPENDENTE DE IDIOMA E FUSO HORÁRIO
    // ============================================================

    public WDLanguage Language { get; private set; }
    public string TimeZoneId { get; private set; }
    public bool UsesAutomaticTimeZone { get; private set; }

    public WDRegionalPreferenceState(
        WDLanguage language,
        string timeZoneId,
        bool usesAutomaticTimeZone)
    {
        Language = language;
        TimeZoneId = NormalizeTimeZoneId(timeZoneId);
        UsesAutomaticTimeZone = usesAutomaticTimeZone;
    }

    public void SetLanguage(WDLanguage language) => Language = language;

    public bool TrySetManualTimeZone(string timeZoneId)
    {
        if (!TryResolveTimeZone(timeZoneId, out TimeZoneInfo zone))
            return false;

        TimeZoneId = zone.Id;
        UsesAutomaticTimeZone = false;
        return true;
    }

    public void UseAutomaticTimeZone()
    {
        TimeZoneId = TimeZoneInfo.Local.Id;
        UsesAutomaticTimeZone = true;
    }

    public DateTimeOffset ConvertUtc(DateTimeOffset absoluteTime)
    {
        DateTimeOffset utc = absoluteTime.ToUniversalTime();
        return TimeZoneInfo.ConvertTime(utc, ResolveTimeZone());
    }

    public TimeZoneInfo ResolveTimeZone()
    {
        return TryResolveTimeZone(TimeZoneId, out TimeZoneInfo zone)
            ? zone
            : TimeZoneInfo.Local;
    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        return TryResolveTimeZone(timeZoneId, out TimeZoneInfo zone)
            ? zone.Id
            : TimeZoneInfo.Local.Id;
    }

    private static bool TryResolveTimeZone(
        string timeZoneId,
        out TimeZoneInfo zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return zone != null;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

[CreateAssetMenu(
    fileName = "WDRegionalSettingsConfig",
    menuName = "War Dominion/Config/Regional Settings")]
public sealed class WDRegionalSettingsConfig : ScriptableObject
{
    // ============================================================
    // 03. DEFAULTS EDITÁVEIS SEM ACOPLAMENTO COM UI
    // ============================================================

    [SerializeField] private WDLanguage defaultLanguage = WDLanguage.Portuguese;
    [SerializeField] private bool useAutomaticTimeZone = true;
    [SerializeField] private string defaultManualTimeZoneId = "UTC";

    public WDLanguage DefaultLanguage => defaultLanguage;
    public bool UseAutomaticTimeZone => useAutomaticTimeZone;
    public string DefaultManualTimeZoneId => defaultManualTimeZoneId;
}

public static class WDRegionalPreferences
{
    // ============================================================
    // 04. PERSISTÊNCIA LOCAL E CONVERSÃO CENTRAL DE EVENTOS
    // ============================================================

    private const string ResourcePath = "Config/WDRegionalSettingsConfig";
    private const string LanguageKey = "WD.Regional.Language";
    private const string TimeZoneKey = "WD.Regional.TimeZoneId";
    private const string AutomaticTimeZoneKey = "WD.Regional.AutomaticTimeZone";
    private static WDRegionalPreferenceState current;

    public static WDRegionalPreferenceState Current => current ??= Load();

    public static void SetLanguage(WDLanguage language)
    {
        Current.SetLanguage(language);
        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.Save();
    }

    public static bool TrySetManualTimeZone(string timeZoneId)
    {
        if (!Current.TrySetManualTimeZone(timeZoneId))
            return false;

        SaveTimeZone();
        return true;
    }

    public static void UseAutomaticTimeZone()
    {
        Current.UseAutomaticTimeZone();
        SaveTimeZone();
    }

    public static DateTimeOffset ConvertEventUtcToLocal(
        DateTimeOffset absoluteUtcTime) =>
        Current.ConvertUtc(absoluteUtcTime);

    public static string Localize(WDLocalizedText text) =>
        text != null ? text.Resolve(Current.Language) : string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCache() => current = null;

    private static WDRegionalPreferenceState Load()
    {
        WDRegionalSettingsConfig config =
            Resources.Load<WDRegionalSettingsConfig>(ResourcePath);
        WDLanguage defaultLanguage = config != null
            ? config.DefaultLanguage
            : WDLanguage.Portuguese;
        bool automaticDefault = config == null || config.UseAutomaticTimeZone;
        string configuredDefault = automaticDefault
            ? TimeZoneInfo.Local.Id
            : config.DefaultManualTimeZoneId;

        var language = (WDLanguage)PlayerPrefs.GetInt(
            LanguageKey, (int)defaultLanguage);
        bool automatic = PlayerPrefs.GetInt(
            AutomaticTimeZoneKey, automaticDefault ? 1 : 0) == 1;
        string zoneId = automatic
            ? TimeZoneInfo.Local.Id
            : PlayerPrefs.GetString(TimeZoneKey, configuredDefault);
        return new WDRegionalPreferenceState(language, zoneId, automatic);
    }

    private static void SaveTimeZone()
    {
        PlayerPrefs.SetString(TimeZoneKey, Current.TimeZoneId);
        PlayerPrefs.SetInt(
            AutomaticTimeZoneKey, Current.UsesAutomaticTimeZone ? 1 : 0);
        PlayerPrefs.Save();
    }
}

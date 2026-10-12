using System;
using System.Collections.Generic;
using UnityEngine;

public enum WDResolutionPresentationSpeed { Normal, Accelerated }
public enum WDFrameRateLimit { Fps30, Fps60, Fps120, Unlimited }
public enum WDGraphicsQuality { Low, Medium, High }
public enum WDOnlineStatus { Online, Busy, Invisible }

[Serializable]
public sealed class WDSettingsData
{
    // ============================================================
    // 01. MODELO PERSISTENTE E DEFAULTS CENTRALIZADOS
    // ============================================================

    public bool BattleAnimations = true;
    public WDResolutionPresentationSpeed ResolutionSpeed = WDResolutionPresentationSpeed.Normal;
    public bool Vibration = true;
    public float MasterVolume = 0.8f;
    public bool MasterMuted;
    public float MusicVolume = 0.7f;
    public float SfxVolume = 0.9f;
    public WDFrameRateLimit FrameRate = WDFrameRateLimit.Fps60;
    public WDGraphicsQuality GraphicsQuality = WDGraphicsQuality.High;
    public bool Fullscreen = true;
    public bool AllowNonFriendMessages = true;
    public WDOnlineStatus OnlineStatus = WDOnlineStatus.Online;
    public bool ChatNotifications = true;

    public static WDSettingsData Defaults() => new WDSettingsData();

    public void Normalize()
    {
        MasterVolume = Mathf.Clamp01(MasterVolume);
        MusicVolume = Mathf.Clamp01(MusicVolume);
        SfxVolume = Mathf.Clamp01(SfxVolume);
        if (!Enum.IsDefined(typeof(WDResolutionPresentationSpeed), ResolutionSpeed))
            ResolutionSpeed = WDResolutionPresentationSpeed.Normal;
        if (!Enum.IsDefined(typeof(WDFrameRateLimit), FrameRate))
            FrameRate = WDFrameRateLimit.Fps60;
        if (!Enum.IsDefined(typeof(WDGraphicsQuality), GraphicsQuality))
            GraphicsQuality = WDGraphicsQuality.High;
        if (!Enum.IsDefined(typeof(WDOnlineStatus), OnlineStatus))
            OnlineStatus = WDOnlineStatus.Online;
    }
}

public interface IWDSettingsStore
{
    WDSettingsData Load();
    void Save(WDSettingsData data);
}

public sealed class WDPlayerPrefsSettingsStore : IWDSettingsStore
{
    private const string StorageKey = "WD.Settings.Local.V1";

    public WDSettingsData Load()
    {
        if (!PlayerPrefs.HasKey(StorageKey))
            return WDSettingsData.Defaults();

        WDSettingsData data = JsonUtility.FromJson<WDSettingsData>(
            PlayerPrefs.GetString(StorageKey));
        data ??= WDSettingsData.Defaults();
        data.Normalize();
        return data;
    }

    public void Save(WDSettingsData data)
    {
        data.Normalize();
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}

public interface IWDSettingsRuntime
{
    void ApplyAudio(float master, float music, float sfx);
    void ApplyGraphics(WDFrameRateLimit frameRate, WDGraphicsQuality quality, bool fullscreen);
}

public sealed class WDUnitySettingsRuntime : IWDSettingsRuntime
{
    // ============================================================
    // 02. APLICAÇÃO UNITY SEM RECONFIGURAR FONTES INDIVIDUAIS
    // ============================================================

    public void ApplyAudio(float master, float music, float sfx)
    {
        AudioListener.volume = Mathf.Clamp01(master);
        // Música e SFX ficam persistidos para o futuro AudioMixer global.
    }

    public void ApplyGraphics(
        WDFrameRateLimit frameRate, WDGraphicsQuality quality, bool fullscreen)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = WDSettingsRepository.ResolveTargetFrameRate(frameRate);
        int qualityIndex = WDSettingsRepository.ResolveQualityIndex(
            QualitySettings.names, quality);
        if (qualityIndex >= 0 && qualityIndex != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(qualityIndex, true);

        if (Application.isPlaying && !Application.isEditor)
            Screen.fullScreen = fullscreen;
    }
}

public sealed class WDSettingsRepository
{
    // ============================================================
    // 03. AUTORIDADE ÚNICA DAS NOVAS PREFERÊNCIAS
    // ============================================================

    private readonly IWDSettingsStore store;
    private readonly IWDSettingsRuntime runtime;

    public WDSettingsData Current { get; private set; }
    public event Action AudioChanged;
    public float EffectiveMasterVolume => Current.MasterMuted ? 0f : Current.MasterVolume;
    public float ResolutionPlaybackSpeed =>
        Current.ResolutionSpeed == WDResolutionPresentationSpeed.Accelerated ? 2f : 1f;

    public WDSettingsRepository(
        IWDSettingsStore newStore = null,
        IWDSettingsRuntime newRuntime = null)
    {
        store = newStore ?? new WDPlayerPrefsSettingsStore();
        runtime = newRuntime ?? new WDUnitySettingsRuntime();
        Current = store.Load() ?? WDSettingsData.Defaults();
        Current.Normalize();
        ApplyRuntime();
    }

    public void SetBattleAnimations(bool value) => Save(data => data.BattleAnimations = value);
    public void SetResolutionSpeed(WDResolutionPresentationSpeed value) =>
        Save(data => data.ResolutionSpeed = value);
    public void SetVibration(bool value) => Save(data => data.Vibration = value);
    public void SetMasterVolume(float value) => SaveAudio(data => data.MasterVolume = value);
    public void SetMasterMuted(bool value) => SaveAudio(data => data.MasterMuted = value);
    public void SetMusicVolume(float value) => SaveAudio(data => data.MusicVolume = value);
    public void SetSfxVolume(float value) => SaveAudio(data => data.SfxVolume = value);
    public void SetFrameRate(WDFrameRateLimit value) => SaveGraphics(data => data.FrameRate = value);
    public void SetGraphicsQuality(WDGraphicsQuality value) => SaveGraphics(data => data.GraphicsQuality = value);
    public void SetFullscreen(bool value) => SaveGraphics(data => data.Fullscreen = value);
    public void SetAllowNonFriendMessages(bool value) => Save(data => data.AllowNonFriendMessages = value);
    public void SetOnlineStatus(WDOnlineStatus value) => Save(data => data.OnlineStatus = value);
    public void SetChatNotifications(bool value) => Save(data => data.ChatNotifications = value);

    public void RestoreGeneralDefaults()
    {
        WDSettingsData defaults = WDSettingsData.Defaults();
        Save(data =>
        {
            data.BattleAnimations = defaults.BattleAnimations;
            data.ResolutionSpeed = defaults.ResolutionSpeed;
            data.Vibration = defaults.Vibration;
        });
        MapaAtivo.DefinirNomesTerritoriaisHabilitados(false);
        WDRegionalPreferences.RestoreTimeZoneDefault();
    }

    public void RestoreAudioDefaults()
    {
        WDSettingsData defaults = WDSettingsData.Defaults();
        SaveAudio(data =>
        {
            data.MasterVolume = defaults.MasterVolume;
            data.MasterMuted = defaults.MasterMuted;
            data.MusicVolume = defaults.MusicVolume;
            data.SfxVolume = defaults.SfxVolume;
        });
    }

    public void RestoreGraphicsDefaults()
    {
        WDSettingsData defaults = WDSettingsData.Defaults();
        SaveGraphics(data =>
        {
            data.FrameRate = defaults.FrameRate;
            data.GraphicsQuality = defaults.GraphicsQuality;
            data.Fullscreen = defaults.Fullscreen;
        });
    }

    public void RestoreChatDefaults()
    {
        WDSettingsData defaults = WDSettingsData.Defaults();
        Save(data =>
        {
            data.AllowNonFriendMessages = defaults.AllowNonFriendMessages;
            data.OnlineStatus = defaults.OnlineStatus;
            data.ChatNotifications = defaults.ChatNotifications;
        });
    }

    public static int ResolveTargetFrameRate(WDFrameRateLimit value) => value switch
    {
        WDFrameRateLimit.Fps30 => 30,
        WDFrameRateLimit.Fps120 => 120,
        WDFrameRateLimit.Unlimited => -1,
        _ => 60
    };

    public static WDSettingsData LoadPersisted() =>
        new WDPlayerPrefsSettingsStore().Load();

    public static int ResolveQualityIndex(
        IReadOnlyList<string> qualityNames, WDGraphicsQuality quality)
    {
        if (qualityNames == null || qualityNames.Count == 0)
            return -1;

        string[] preferred = quality switch
        {
            WDGraphicsQuality.Low => new[] { "Low", "Very Low" },
            WDGraphicsQuality.Medium => new[] { "Medium" },
            _ => new[] { "High", "Very High", "Ultra" }
        };
        foreach (string candidate in preferred)
            for (int i = 0; i < qualityNames.Count; i++)
                if (string.Equals(qualityNames[i], candidate, StringComparison.OrdinalIgnoreCase))
                    return i;

        return quality switch
        {
            WDGraphicsQuality.Low => 0,
            WDGraphicsQuality.Medium => qualityNames.Count / 2,
            _ => qualityNames.Count - 1
        };
    }

    private void ApplyRuntime()
    {
        runtime.ApplyAudio(EffectiveMasterVolume, Current.MusicVolume, Current.SfxVolume);
        runtime.ApplyGraphics(Current.FrameRate, Current.GraphicsQuality, Current.Fullscreen);
    }

    private void Save(Action<WDSettingsData> mutate)
    {
        mutate(Current);
        Current.Normalize();
        store.Save(Current);
    }

    private void SaveAudio(Action<WDSettingsData> mutate)
    {
        Save(mutate);
        runtime.ApplyAudio(EffectiveMasterVolume, Current.MusicVolume, Current.SfxVolume);
        AudioChanged?.Invoke();
    }

    private void SaveGraphics(Action<WDSettingsData> mutate)
    {
        Save(mutate);
        runtime.ApplyGraphics(Current.FrameRate, Current.GraphicsQuality, Current.Fullscreen);
    }
}

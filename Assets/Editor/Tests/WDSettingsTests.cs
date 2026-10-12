using System.Linq;
using NUnit.Framework;

public sealed class WDSettingsTests
{
    private sealed class MemoryStore : IWDSettingsStore
    {
        private WDSettingsData saved;
        public WDSettingsData Load() => saved ?? WDSettingsData.Defaults();
        public void Save(WDSettingsData data)
        {
            saved = UnityEngine.JsonUtility.FromJson<WDSettingsData>(
                UnityEngine.JsonUtility.ToJson(data));
        }
    }

    private sealed class RuntimeSpy : IWDSettingsRuntime
    {
        public int AudioApplications;
        public int GraphicsApplications;
        public float Master;
        public WDFrameRateLimit FrameRate;
        public WDGraphicsQuality Quality;
        public bool Fullscreen;

        public void ApplyAudio(float master, float music, float sfx)
        {
            AudioApplications++;
            Master = master;
        }

        public void ApplyGraphics(
            WDFrameRateLimit frameRate, WDGraphicsQuality quality, bool fullscreen)
        {
            GraphicsApplications++;
            FrameRate = frameRate;
            Quality = quality;
            Fullscreen = fullscreen;
        }
    }

    // ============================================================
    // 01. DEFAULTS, PERSISTÊNCIA E INDEPENDÊNCIA
    // ============================================================

    [Test]
    public void DefaultsAreCentralizedAndValid()
    {
        WDSettingsData data = WDSettingsData.Defaults();
        Assert.That(data.BattleAnimations, Is.True);
        Assert.That(data.ResolutionSpeed, Is.EqualTo(WDResolutionPresentationSpeed.Normal));
        Assert.That(data.MasterVolume, Is.InRange(0f, 1f));
        Assert.That(data.FrameRate, Is.EqualTo(WDFrameRateLimit.Fps60));
        Assert.That(data.OnlineStatus, Is.EqualTo(WDOnlineStatus.Online));
    }

    [Test]
    public void GeneralPreferencesPersistIndependently()
    {
        var store = new MemoryStore();
        var repository = new WDSettingsRepository(store, new RuntimeSpy());
        repository.SetBattleAnimations(false);
        repository.SetVibration(false);
        repository.SetResolutionSpeed(WDResolutionPresentationSpeed.Accelerated);

        var restored = new WDSettingsRepository(store, new RuntimeSpy());
        Assert.That(restored.Current.BattleAnimations, Is.False);
        Assert.That(restored.Current.Vibration, Is.False);
        Assert.That(restored.ResolutionPlaybackSpeed, Is.EqualTo(2f));
        Assert.That(restored.Current.MasterVolume, Is.EqualTo(0.8f));
    }

    [Test]
    public void ResolutionPreferenceOnlyProducesPresentationMultiplier()
    {
        var repository = new WDSettingsRepository(new MemoryStore(), new RuntimeSpy());
        repository.SetResolutionSpeed(WDResolutionPresentationSpeed.Accelerated);
        Assert.That(repository.ResolutionPlaybackSpeed, Is.EqualTo(2f));
    }

    // ============================================================
    // 02. ÁUDIO E GRÁFICOS
    // ============================================================

    [Test]
    public void AudioValuesAreClampedAndApplied()
    {
        var runtime = new RuntimeSpy();
        var repository = new WDSettingsRepository(new MemoryStore(), runtime);
        repository.SetMasterVolume(2f);
        Assert.That(repository.Current.MasterVolume, Is.EqualTo(1f));
        Assert.That(runtime.Master, Is.EqualTo(1f));
        Assert.That(runtime.AudioApplications, Is.GreaterThan(1));
    }

    [Test]
    public void MutePersistsSeparatelyFromVolumeAndAudioDefaultsRestoreBoth()
    {
        var store = new MemoryStore();
        var runtime = new RuntimeSpy();
        var repository = new WDSettingsRepository(store, runtime);
        repository.SetMasterVolume(.43f);
        repository.SetMasterMuted(true);
        Assert.That(runtime.Master, Is.Zero);
        Assert.That(repository.Current.MasterVolume, Is.EqualTo(.43f));
        var restoredRuntime = new RuntimeSpy();
        var restored = new WDSettingsRepository(store, restoredRuntime);
        Assert.That(restored.Current.MasterMuted, Is.True);
        Assert.That(restoredRuntime.Master, Is.Zero);
        restored.SetMasterVolume(2f);
        Assert.That(restored.Current.MasterVolume, Is.EqualTo(1));
        Assert.That(restoredRuntime.Master, Is.Zero);
        restored.SetMasterMuted(false);
        Assert.That(restoredRuntime.Master, Is.EqualTo(1));
        restored.SetMasterVolume(0);
        restored.SetMasterMuted(true);
        restored.SetMasterMuted(false);
        Assert.That(restoredRuntime.Master, Is.Zero);
        restored.SetMasterMuted(true);
        restored.RestoreAudioDefaults();
        Assert.That(restored.Current.MasterMuted, Is.False);
        Assert.That(restoredRuntime.Master, Is.EqualTo(.8f));
    }

    [Test]
    public void OlderSettingsDefaultToUnmutedAndMatchRuntimeAppliesEffectiveVolume()
    {
        var legacy = UnityEngine.JsonUtility.FromJson<WDSettingsData>("{\"MasterVolume\":0.37}");
        Assert.That(legacy.MasterMuted, Is.False);
        float previous = UnityEngine.AudioListener.volume;
        try
        {
            var repository = new WDSettingsRepository(new MemoryStore(), new WDMatchSettingsRuntime());
            repository.SetMasterVolume(.37f);
            Assert.That(UnityEngine.AudioListener.volume, Is.EqualTo(.37f));
            repository.SetMasterMuted(true);
            Assert.That(UnityEngine.AudioListener.volume, Is.Zero);
            repository.SetMasterMuted(false);
            Assert.That(UnityEngine.AudioListener.volume, Is.EqualTo(.37f));
        }
        finally { UnityEngine.AudioListener.volume = previous; }
    }

    [TestCase(WDFrameRateLimit.Fps30, 30)]
    [TestCase(WDFrameRateLimit.Fps60, 60)]
    [TestCase(WDFrameRateLimit.Fps120, 120)]
    [TestCase(WDFrameRateLimit.Unlimited, -1)]
    public void FrameRateMapsToSupportedRuntimeValue(WDFrameRateLimit value, int expected)
    {
        Assert.That(WDSettingsRepository.ResolveTargetFrameRate(value), Is.EqualTo(expected));
    }

    [Test]
    public void QualityMapsByRealNameWithoutMagicIndices()
    {
        string[] names = { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };
        Assert.That(WDSettingsRepository.ResolveQualityIndex(names, WDGraphicsQuality.Low), Is.EqualTo(1));
        Assert.That(WDSettingsRepository.ResolveQualityIndex(names, WDGraphicsQuality.Medium), Is.EqualTo(2));
        Assert.That(WDSettingsRepository.ResolveQualityIndex(names, WDGraphicsQuality.High), Is.EqualTo(3));
    }

    [Test]
    public void FullscreenPreferencePersistsAndUsesRuntimeBoundary()
    {
        var store = new MemoryStore();
        var runtime = new RuntimeSpy();
        var repository = new WDSettingsRepository(store, runtime);
        repository.SetFullscreen(false);
        Assert.That(new WDSettingsRepository(store, new RuntimeSpy()).Current.Fullscreen, Is.False);
        Assert.That(runtime.Fullscreen, Is.False);
    }

    [Test]
    public void AudioAndGraphicsRestoreOnlyTheirOwnGroups()
    {
        var repository = new WDSettingsRepository(new MemoryStore(), new RuntimeSpy());
        repository.SetMasterVolume(0.1f);
        repository.SetFrameRate(WDFrameRateLimit.Fps120);
        repository.RestoreAudioDefaults();
        Assert.That(repository.Current.MasterVolume, Is.EqualTo(0.8f));
        Assert.That(repository.Current.FrameRate, Is.EqualTo(WDFrameRateLimit.Fps120));
        repository.RestoreGraphicsDefaults();
        Assert.That(repository.Current.FrameRate, Is.EqualTo(WDFrameRateLimit.Fps60));
    }

    // ============================================================
    // 03. IDIOMA, CHAT E LIMITES DE CONTA
    // ============================================================

    [Test]
    public void PlannedLanguagesAreExactlyTheApprovedSix()
    {
        CollectionAssert.AreEqual(
            new[] { "Português (Brasil)", "English", "Español", "Français", "Deutsch", "Italiano" },
            WDRegionalPreferences.PlannedLanguages.Select(option => option.Label).ToArray());
    }

    [Test]
    public void OnlyPortugueseIsCurrentlyAvailable()
    {
        WDLanguageOption[] available = WDRegionalPreferences.PlannedLanguages
            .Where(option => option.IsAvailable).ToArray();
        Assert.That(available, Has.Length.EqualTo(1));
        Assert.That(available[0].Language, Is.EqualTo(WDLanguage.Portuguese));
    }

    [Test]
    public void ChatPreferencesPersistWithoutSimulatingOnlineState()
    {
        var store = new MemoryStore();
        var repository = new WDSettingsRepository(store, new RuntimeSpy());
        repository.SetAllowNonFriendMessages(false);
        repository.SetOnlineStatus(WDOnlineStatus.Invisible);
        repository.SetChatNotifications(false);
        var restored = new WDSettingsRepository(store, new RuntimeSpy()).Current;
        Assert.That(restored.AllowNonFriendMessages, Is.False);
        Assert.That(restored.OnlineStatus, Is.EqualTo(WDOnlineStatus.Invisible));
        Assert.That(restored.ChatNotifications, Is.False);
    }

    [Test]
    public void ChatRestoreDoesNotChangeOtherGroups()
    {
        var repository = new WDSettingsRepository(new MemoryStore(), new RuntimeSpy());
        repository.SetAllowNonFriendMessages(false);
        repository.SetMasterVolume(0.25f);
        repository.RestoreChatDefaults();
        Assert.That(repository.Current.AllowNonFriendMessages, Is.True);
        Assert.That(repository.Current.MasterVolume, Is.EqualTo(0.25f));
    }

    [Test]
    public void SettingsDomainContainsNoFakeAccountOrDestructiveAction()
    {
        string[] methods = typeof(WDSettingsRepository).GetMethods()
            .Select(method => method.Name).ToArray();
        Assert.That(methods, Does.Not.Contain("Logout"));
        Assert.That(methods, Does.Not.Contain("DeleteAccount"));
        Assert.That(methods, Does.Not.Contain("LinkAccount"));
    }
}

#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class WDMatchSettingsViewTests
{
    // 01. Autoridades reais, store/runtime isolados; nenhuma cena ou PlayerPrefs é alterada.
    private sealed class Store : IWDSettingsStore
    {
        public WDSettingsData Data = new() { MasterVolume = .31f, MusicVolume = .42f, SfxVolume = .63f };
        public int Saves;
        public WDSettingsData Load() => JsonUtility.FromJson<WDSettingsData>(JsonUtility.ToJson(Data));
        public void Save(WDSettingsData data) { Data = JsonUtility.FromJson<WDSettingsData>(JsonUtility.ToJson(data)); Saves++; }
    }

    private sealed class Runtime : IWDSettingsRuntime
    {
        public int Calls;
        public float Master;
        public void ApplyAudio(float master, float music, float sfx) { Calls++; Master = master; }
        public void ApplyGraphics(WDFrameRateLimit frameRate, WDGraphicsQuality quality, bool fullscreen) { }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new("SafeArea", typeof(RectTransform));
        public readonly GameObject Events = new("Events", typeof(EventSystem));
        public readonly WarDominionUITheme Theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        public readonly Store Store = new();
        public readonly Runtime Runtime = new();
        public readonly WDSettingsRepository Repository;
        public readonly WDMatchUIV2 View;
        public readonly WDMatchSettingsView Settings;
        public readonly WDMatchUtilityCommands Utilities;
        public int Leaves;
        public bool Regions;
        public Fixture()
        {
            ((RectTransform)Root.transform).sizeDelta = new Vector2(1920, 1080);
            var rect = WDUIFactory.Rect("V2", Root.transform);
            View = rect.gameObject.AddComponent<WDMatchUIV2>();
            View.Build(Theme, null, null, null);
            Repository = new WDSettingsRepository(Store, Runtime);
            View.BindSettings(Repository, () => Leaves++);
            Utilities = new WDMatchUtilityCommands(() => Regions = !Regions, () => Regions);
            View.BindUtilities(Utilities);
            Settings = View.GetComponentInChildren<WDMatchSettingsView>(true);
        }
        public Transform Panel => Settings.transform.Find("SettingsPanel");
        public Transform Confirmation => Settings.transform.Find("LeaveConfirmation");
        public Button Toggle => View.transform.Find("UtilityControls/Settings").GetComponent<Button>();
        public Slider Slider(string id) => Panel.Find("Audio/" + id + "/Slider").GetComponent<Slider>();
        public void Click(Transform t) => ExecuteEvents.ExecuteHierarchy(t.gameObject,
            new PointerEventData(Events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        public void Open() => Click(Toggle.transform);
        public void Request() => Click(Panel.Find("RequestLeave"));
        public void Backdrop() => Click(Settings.transform.Find("Backdrop"));
        public void Dispose() { Object.DestroyImmediate(Root); Object.DestroyImmediate(Events); Object.DestroyImmediate(Theme); }
    }

    // 02. Utility única, callbacks idempotentes e exclusividade nos dois sentidos.
    [Test]
    public void RebindingRegistersSettingsOnceAndToggleClosesToNone()
    {
        using var f = new Fixture();
        f.View.BindUtilities(f.Utilities);
        f.View.BindUtilities(f.Utilities);
        int opens = 0;
        f.Utilities.ModuleRequested += module => { if (module == WDMatchUtilityModule.Settings) opens++; };
        f.Open();
        Assert.That(opens, Is.EqualTo(1));
        Assert.That(f.Settings.gameObject.activeSelf, Is.True);
        f.Open();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        Assert.That(f.Settings.gameObject.activeSelf, Is.False);
    }

    [TestCase(WDMatchUtilityModule.Menu)]
    [TestCase(WDMatchUtilityModule.Regions)]
    [TestCase(WDMatchUtilityModule.Chat)]
    public void SettingsAndOtherUtilitiesAreBidirectionallyExclusive(WDMatchUtilityModule other)
    {
        using var f = new Fixture();
        f.Utilities.Toggle(other);
        f.Open();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Settings));
        Assert.That(f.Regions, Is.False);
        Assert.That(f.View.GetComponentInChildren<WDMatchChatView>(true).IsOpen, Is.False);
        f.Utilities.Toggle(other);
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(other));
        Assert.That(f.Settings.gameObject.activeSelf, Is.False);
        f.Utilities.CloseActive();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
    }

    [Test]
    public void SettingsPanelConsumesClickWhileBackdropAndReturnClose()
    {
        using var f = new Fixture();
        f.Open();
        f.Click(f.Panel.Find("Title"));
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Settings));
        f.Backdrop();
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        f.Open();
        f.Click(f.Panel.Find("Return"));
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        Assert.That(f.Leaves, Is.Zero);
    }

    // 03. Valores reais, envio de volume uma vez, persistência e lacunas explicitamente inativas.
    [Test]
    public void PanelBodiesAreOpaqueWhileExternalBackdropRemainsTranslucent()
    {
        using var f = new Fixture();
        f.Open();
        foreach (var body in new[] { f.Panel, f.Panel.Find("Audio"), f.Panel.Find("Interface"), f.Confirmation })
        {
            var image = body.GetComponent<Image>();
            Assert.That(image.fillCenter, Is.True, body.name);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), body.name);
            Assert.That(image.color, Is.EqualTo((Color)new Color32(11, 14, 18, 255)), body.name);
            Assert.That(EffectiveOpacity(body), Is.EqualTo(1f), body.name);
        }
        Assert.That(f.Settings.transform.Find("Backdrop").GetComponent<Image>().color.a, Is.EqualTo(.55f));
        f.Request();
        Assert.That(EffectiveOpacity(f.Confirmation), Is.EqualTo(1f));
    }

    [Test]
    public void UnavailableControlsLookDimmedButMasterAndNavigationRemainFullyVisible()
    {
        using var f = new Fixture();
        f.Open();
        Assert.That(f.Slider("Master").interactable, Is.True);
        foreach (var path in new[] { "Audio/Master/Slider", "Audio/Master/Mute", "Return", "RequestLeave" })
            Assert.That(EffectiveOpacity(f.Panel.Find(path)), Is.EqualTo(1f), path);
        foreach (var path in new[] { "Audio/Music/Slider", "Audio/Music/Mute",
            "Audio/Sfx/Slider", "Audio/Sfx/Mute", "Interface/Emojis/On", "Interface/Emojis/Off",
            "Interface/Combat/Normal", "Interface/Combat/Fast", "Manual" })
        {
            var target = f.Panel.Find(path);
            Assert.That(EffectiveOpacity(target), Is.EqualTo(.45f).Within(.001f), path);
            Assert.That(target.GetComponent<Selectable>().interactable, Is.False, path);
            f.Click(target);
        }
        Assert.That(f.Store.Saves, Is.Zero);
        Assert.That(f.Leaves, Is.Zero);
    }

    private static float EffectiveOpacity(Transform target)
    {
        float opacity = 1;
        for (var current = target; current != null; current = current.parent)
            foreach (var group in current.GetComponents<CanvasGroup>()) opacity *= group.alpha;
        return opacity;
    }

    [Test]
    public void AudioBindsRepositoryWithoutWritingOnOpenAndPreservesPersistedVolume()
    {
        using var f = new Fixture();
        int initialCalls = f.Runtime.Calls;
        f.Open();
        Assert.That(f.Slider("Master").value, Is.EqualTo(.31f));
        Assert.That(f.Slider("Music").value, Is.EqualTo(.42f));
        Assert.That(f.Slider("Sfx").value, Is.EqualTo(.63f));
        Assert.That(f.Runtime.Calls, Is.EqualTo(initialCalls));
        f.View.BindSettings(f.Repository, () => f.Leaves++);
        f.Slider("Master").value = .57f;
        Assert.That(f.Runtime.Master, Is.EqualTo(.57f));
        Assert.That(f.Runtime.Calls, Is.EqualTo(initialCalls + 1));
        Assert.That(f.Store.Saves, Is.EqualTo(1));
        f.Backdrop(); f.Open();
        Assert.That(f.Slider("Master").value, Is.EqualTo(.57f));
        Assert.That(new WDSettingsRepository(f.Store, new Runtime()).Current.MasterVolume, Is.EqualTo(.57f));
        Assert.That(f.Runtime.Calls, Is.EqualTo(initialCalls + 1));
    }

    [Test]
    public void MissingCapabilitiesStayDisabledWithoutFakePreferencesOrFastForward()
    {
        using var f = new Fixture();
        f.Open();
        Assert.That(f.Slider("Music").interactable, Is.False);
        Assert.That(f.Slider("Sfx").interactable, Is.False);
        foreach (var path in new[] { "Audio/Music/Mute", "Audio/Sfx/Mute", "Manual",
            "Interface/Emojis/On", "Interface/Emojis/Off", "Interface/Combat/Normal", "Interface/Combat/Fast" })
        {
            var target = f.Panel.Find(path);
            Assert.That(target.GetComponent<Button>().interactable, Is.False, path);
            f.Click(target);
        }
        Assert.That(f.Store.Saves, Is.Zero);
        Assert.That(f.Repository.Current.ResolutionSpeed, Is.EqualTo(WDResolutionPresentationSpeed.Normal));
    }

    [Test]
    public void MasterMuteRestoresVolumePersistsAndRebindingDoesNotDuplicateWrites()
    {
        using var f = new Fixture();
        f.View.BindSettings(f.Repository, () => f.Leaves++);
        f.Open();
        var mute = f.Panel.Find("Audio/Master/Mute");
        Assert.That(mute.GetComponent<Button>().interactable, Is.True);
        f.Click(mute);
        Assert.That(f.Runtime.Master, Is.Zero);
        Assert.That(f.Repository.Current.MasterMuted, Is.True);
        Assert.That(f.Repository.Current.MasterVolume, Is.EqualTo(.31f));
        Assert.That(f.Slider("Master").value, Is.EqualTo(.31f));
        Assert.That(f.Store.Saves, Is.EqualTo(1));
        Assert.That(new WDSettingsRepository(f.Store, new Runtime()).Current.MasterMuted, Is.True);
        f.Backdrop(); f.Open();
        Assert.That(f.Store.Saves, Is.EqualTo(1));
        f.Click(mute);
        Assert.That(f.Runtime.Master, Is.EqualTo(.31f));
        Assert.That(f.Repository.Current.MasterMuted, Is.False);
        Assert.That(f.Store.Saves, Is.EqualTo(2));
        f.Repository.SetMasterVolume(.62f);
        Assert.That(f.Slider("Master").value, Is.EqualTo(.62f));
    }

    [Test]
    public void MissingSettingsAndExitAuthoritiesDisableTheirControls()
    {
        using var f = new Fixture();
        f.View.BindSettings(null, null);
        f.Open();
        foreach (var path in new[] { "Audio/Master/Slider", "Audio/Master/Mute", "RequestLeave" })
        {
            Assert.That(f.Panel.Find(path).GetComponent<Selectable>().interactable, Is.False, path);
            f.Click(f.Panel.Find(path));
        }
        Assert.That(f.Leaves, Is.Zero);
        Assert.That(f.Store.Saves, Is.Zero);
        Assert.That(f.Confirmation.gameObject.activeSelf, Is.False);
    }

    // 04. Confirmação não executa saída até o comando vermelho final; proteção contra duplo toque.
    [Test]
    public void ConfirmationCancelBackdropAndPanelNeverLeaveAndKeepSettingsValues()
    {
        using var f = new Fixture();
        f.Open();
        f.Slider("Master").value = .27f;
        f.Request();
        Assert.That(f.Leaves, Is.Zero);
        Assert.That(f.Panel.gameObject.activeSelf, Is.False);
        Assert.That(f.Confirmation.gameObject.activeSelf, Is.True);
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Settings));
        var filter = f.Settings.transform.Find("Backdrop").GetComponent<WDMatchChatBackdropFilter>();
        Assert.That(filter.UtilityTargets, Is.Null);
        Assert.That(filter.IsRaycastLocationValid(Vector2.zero, null), Is.True);
        f.Click(f.Confirmation.Find("Title"));
        Assert.That(f.Confirmation.gameObject.activeSelf, Is.True);
        f.Click(f.Confirmation.Find("Cancel"));
        Assert.That(f.Panel.gameObject.activeSelf, Is.True);
        Assert.That(f.Slider("Master").value, Is.EqualTo(.27f));
        f.Request(); f.Backdrop();
        Assert.That(f.Panel.gameObject.activeSelf, Is.True);
        Assert.That(f.Confirmation.gameObject.activeSelf, Is.False);
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Settings));
        Assert.That(f.Leaves, Is.Zero);
    }

    [Test]
    public void OnlyFinalConfirmationInvokesLeaveExactlyOnce()
    {
        using var f = new Fixture();
        f.Open(); f.Request();
        var button = f.Confirmation.Find("ConfirmLeave").GetComponent<Button>();
        f.Click(button.transform);
        button.onClick.Invoke(); // Evento já enfileirado também é ignorado.
        f.Backdrop();
        Assert.That(f.Leaves, Is.EqualTo(1));
        Assert.That(button.interactable, Is.False);
        Assert.That(f.Utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
    }

    [Test]
    public void NoExitAuthorityDisablesConfirmationAndClosingResetsChildState()
    {
        using var f = new Fixture();
        f.View.BindSettings(f.Repository, null);
        f.Open(); f.Request();
        Assert.That(f.Confirmation.Find("ConfirmLeave").GetComponent<Button>().interactable, Is.False);
        f.Utilities.CloseActive(); f.Open();
        Assert.That(f.Panel.gameObject.activeSelf, Is.True);
        Assert.That(f.Confirmation.gameObject.activeSelf, Is.False);
    }

    // 05. Geometria essencial em 16:9, 20:9 e área segura reduzida, sem Canvas adicional.
    [TestCase(1920, 1080)]
    [TestCase(2400, 1080)]
    [TestCase(900, 620)]
    public void PanelsStayCenteredInsideSafeArea(float width, float height)
    {
        using var f = new Fixture();
        var area = (RectTransform)f.Root.transform;
        area.sizeDelta = new Vector2(width, height);
        f.Open(); f.Settings.FitSafeArea();
        foreach (RectTransform panel in new[] { (RectTransform)f.Panel, (RectTransform)f.Confirmation })
        {
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector3 local = area.InverseTransformPoint(corner);
                Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(width / 2));
                Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(height / 2));
            }
            Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
        Assert.That(f.Settings.GetComponentsInChildren<Canvas>(true), Is.Empty);
    }
}
#endif

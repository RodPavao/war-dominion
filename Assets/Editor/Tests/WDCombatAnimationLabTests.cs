using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public sealed class WDCombatAnimationLabTests
{
    // ============================================================
    // 01. PRESET — garante autoridade isolada e valores válidos
    // ============================================================
    [Test]
    public void PresetHasUsableNativeDefaults()
    {
        WDCombatMotionPreset preset = ScriptableObject.CreateInstance<WDCombatMotionPreset>();
        try
        {
            Assert.That(preset.TravelDuration, Is.GreaterThan(0f));
            Assert.That(preset.TravelEase, Is.Not.Null);
            Assert.That(preset.SpeedMultiplier, Is.GreaterThan(0f));
            Assert.That(preset.ImpactDuration, Is.GreaterThanOrEqualTo(0f));
        }
        finally { Object.DestroyImmediate(preset); }
    }

    // ============================================================
    // 02. ADAPTADOR — início/fim e curvatura sem lógica de combate
    // ============================================================
    [Test]
    public void AdapterEvaluatesDynamicWorldTrajectory()
    {
        GameObject root = new("LabAdapterTest");
        GameObject origin = new("Origin");
        GameObject destination = new("Destination");
        GameObject traveller = new("Traveller");
        WDCombatMotionPreset preset = ScriptableObject.CreateInstance<WDCombatMotionPreset>();
        try
        {
            origin.transform.position = new Vector3(-3f, 1f, 0f);
            destination.transform.position = new Vector3(4f, -1f, 0f);
            WDCombatMotionAdapter adapter = root.AddComponent<WDCombatMotionAdapter>();
            adapter.Configure(origin.transform, destination.transform, traveller.transform, preset);
            Assert.That(adapter.TryEvaluate(0f, out Vector3 start), Is.True);
            Assert.That(adapter.TryEvaluate(1f, out Vector3 end), Is.True);
            Assert.That(adapter.TryEvaluate(0.5f, out Vector3 middle), Is.True);
            Assert.That(start, Is.EqualTo(origin.transform.position).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(end, Is.EqualTo(destination.transform.position).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(middle, Is.Not.EqualTo(Vector3.Lerp(start, end, 0.5f)));
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(origin);
            Object.DestroyImmediate(destination); Object.DestroyImmediate(traveller);
            Object.DestroyImmediate(preset);
        }
    }

    [Test]
    public void LabAssetsUseDevelopmentPathsOnly()
    {
        Assert.That(WDCombatAnimationLabAuthoring.ScenePath,
            Is.EqualTo("Assets/Scenes/Development/WDCombatAnimationLab.unity"));
        Assert.That(WDCombatAnimationLabAuthoring.PresetPath, Does.Contain("Resources/Development"));
        Assert.That(WDCombatAnimationLabAuthoring.ParticlePrefabPath, Does.Contain("Prefabs/Development"));
    }
}

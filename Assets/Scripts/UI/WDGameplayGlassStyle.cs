using UnityEngine;

// ============================================================
// 01. CONFIGURAÇÃO VISUAL EXPERIMENTAL COMPARTILHADA PELO LABORATÓRIO
// ============================================================
public enum WDGameplayVisualFinish
{
    ApprovedClassic,
    GlassPremium
}

public enum WDGameplayVisualOverride
{
    UseGlobal,
    ApprovedClassic,
    GlassPremium
}

public enum WDGameplayGlassVariant
{
    Current,
    Clean,
    Enhanced
}

public readonly struct WDGameplayGlassStyle
{
    public readonly bool Enabled;
    public readonly float GlassIntensity;
    public readonly float GradientStrength;
    public readonly float ReflectionIntensity;
    public readonly float BevelStrength;
    public readonly float GlowIntensity;
    public readonly WDGameplayGlassVariant Variant;

    public float ProfiledGlassIntensity => Profile(GlassIntensity, .94f, 1f);
    public float ProfiledGradientStrength => Profile(GradientStrength, .78f, 1.22f);
    public float ProfiledReflectionIntensity => Profile(ReflectionIntensity, .72f, 1.38f);
    public float ProfiledBevelStrength => Profile(BevelStrength, .62f, 1.14f);
    public float ProfiledGlowIntensity => Profile(GlowIntensity, .68f, 1f);

    public WDGameplayGlassStyle(bool enabled, float glassIntensity,
        float gradientStrength, float reflectionIntensity,
        float bevelStrength, float glowIntensity,
        WDGameplayGlassVariant variant = WDGameplayGlassVariant.Current)
    {
        Enabled = enabled;
        GlassIntensity = Mathf.Clamp01(glassIntensity);
        GradientStrength = Mathf.Clamp01(gradientStrength);
        ReflectionIntensity = Mathf.Clamp01(reflectionIntensity);
        BevelStrength = Mathf.Clamp01(bevelStrength);
        GlowIntensity = Mathf.Clamp01(glowIntensity);
        Variant = variant;
    }

    private float Profile(float value, float cleanMultiplier,
        float enhancedMultiplier)
    {
        float multiplier = Variant == WDGameplayGlassVariant.Clean
            ? cleanMultiplier
            : Variant == WDGameplayGlassVariant.Enhanced
                ? enhancedMultiplier : 1f;
        return Mathf.Clamp01(value * multiplier);
    }
}

using UnityEngine;

/// <summary>Fonte única dos parâmetros experimentais do laboratório de combate.</summary>
[CreateAssetMenu(fileName = "WDCombatMotionPreset", menuName = "War Dominion/Development/Combat Motion Preset")]
public sealed class WDCombatMotionPreset : ScriptableObject
{
    // ============================================================
    // 01. TRAJETÓRIA — valores independentes do gameplay oficial
    // ============================================================
    [Header("Trajetória")]
    [SerializeField, Min(0.05f)] private float travelDuration = 1.10f;
    [SerializeField, Min(0f)] private float curveHeight = 1.35f;
    [SerializeField] private AnimationCurve travelEase = new(
        new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private Vector2 travellingScale = new(0.92f, 1.12f);
    [SerializeField, Min(0f)] private float approachDuration = 0.14f;

    // ============================================================
    // 02. IMPACTO E LEITURA — ajustes manuais compartilhados
    // ============================================================
    [Header("Impacto")]
    [SerializeField, Min(0f)] private float impactDuration = 0.42f;
    [SerializeField, Range(0f, 0.5f)] private float impactRebound = 0.12f;
    [SerializeField, Min(0f)] private float glowDuration = 0.36f;
    [SerializeField, Range(0f, 2f)] private float glowIntensity = 0.75f;
    [SerializeField] private ParticleSystem impactParticles;

    // ============================================================
    // 03. REPRODUÇÃO — velocidade e movimento reduzido
    // ============================================================
    [Header("Reprodução")]
    [SerializeField, Range(0.1f, 4f)] private float speedMultiplier = 1f;
    [SerializeField] private bool reducedMotion;
    [SerializeField, Range(0f, 1f)] private float reducedMotionAmount = 0.35f;

    public float TravelDuration => travelDuration;
    public float CurveHeight => reducedMotion ? curveHeight * reducedMotionAmount : curveHeight;
    public AnimationCurve TravelEase => travelEase;
    public Vector2 TravellingScale => reducedMotion ? Vector2.one : travellingScale;
    public float ApproachDuration => approachDuration;
    public float ImpactDuration => impactDuration;
    public float ImpactRebound => reducedMotion ? impactRebound * reducedMotionAmount : impactRebound;
    public float GlowDuration => glowDuration;
    public float GlowIntensity => glowIntensity;
    public ParticleSystem ImpactParticles => impactParticles;
    public float SpeedMultiplier => speedMultiplier;
    public bool ReducedMotion => reducedMotion;
}

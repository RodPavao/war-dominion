using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Executa apenas a demonstração isolada; não referencia sistemas de partida.</summary>
[DisallowMultipleComponent]
public sealed class WDCombatAnimationLabController : MonoBehaviour
{
    [Header("Autoridade experimental")]
    [SerializeField] private WDCombatMotionPreset preset;
    [SerializeField] private WDCombatMotionAdapter spatialAdapter;
    [Header("Elementos persistentes")]
    [SerializeField] private Transform travellingCounter;
    [SerializeField] private Transform destinationCounter;
    [SerializeField] private SpriteRenderer destinationGlow;
    [SerializeField] private ParticleSystem impactParticles;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button speedButton;
    [SerializeField] private Button stopButton;

    private Coroutine routine;
    private Vector3 travellerInitialPosition;
    private Vector3 travellerInitialScale;
    private Vector3 destinationInitialScale;
    private Color glowInitialColor;
    private float playbackSpeed = 1f;

    // ============================================================
    // 01. CICLO DE VIDA — uma única animação controla o contador
    // ============================================================
    private void Awake()
    {
        if (travellingCounter != null)
        {
            travellerInitialPosition = travellingCounter.position;
            travellerInitialScale = travellingCounter.localScale;
        }
        if (destinationCounter != null) destinationInitialScale = destinationCounter.localScale;
        if (destinationGlow != null) glowInitialColor = destinationGlow.color;
        BindButtons();
        ResetLab();
    }

    private void OnDisable() => StopAndRestore();

    private void BindButtons()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (resetButton != null) resetButton.onClick.AddListener(ResetLab);
        if (speedButton != null) speedButton.onClick.AddListener(ToggleSpeed);
        if (stopButton != null) stopButton.onClick.AddListener(StopAndRestore);
    }

    // ============================================================
    // 02. CONTROLES PÚBLICOS — adequados a Button e futuro DOTween
    // ============================================================
    public void Play()
    {
        StopAndRestore();
        if (isActiveAndEnabled) routine = StartCoroutine(PlayNativePreview());
    }

    public void ResetLab() => StopAndRestore();

    public void ToggleSpeed()
    {
        playbackSpeed = playbackSpeed < 1.5f ? 2f : 1f;
        UpdateSpeedLabel();
    }

    public void StopAndRestore()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (impactParticles != null) impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (travellingCounter != null)
        {
            travellingCounter.position = travellerInitialPosition;
            travellingCounter.localScale = travellerInitialScale;
            travellingCounter.gameObject.SetActive(true);
        }
        if (destinationCounter != null) destinationCounter.localScale = destinationInitialScale;
        if (destinationGlow != null) destinationGlow.color = glowInitialColor;
        UpdateSpeedLabel();
    }

    // ============================================================
    // 03. PREVIEW NATIVA — substituível por uma única ponte DOTween
    // ============================================================
    private IEnumerator PlayNativePreview()
    {
        if (preset == null || spatialAdapter == null || travellingCounter == null) yield break;
        float duration = preset.TravelDuration / Mathf.Max(0.1f, preset.SpeedMultiplier * playbackSpeed);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            if (spatialAdapter.TryEvaluate(progress, out Vector3 position))
                spatialAdapter.ApplyPosition(position);
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float scale = Mathf.Lerp(preset.TravellingScale.x, preset.TravellingScale.y, envelope);
            travellingCounter.localScale = travellerInitialScale * scale;
            yield return null;
        }
        if (spatialAdapter.TryEvaluate(1f, out Vector3 finalPosition))
            spatialAdapter.ApplyPosition(finalPosition);
        travellingCounter.gameObject.SetActive(false);
        if (impactParticles != null) impactParticles.Play(true);
        yield return AnimateImpact();
        routine = null;
    }

    private IEnumerator AnimateImpact()
    {
        float duration = preset.ImpactDuration / Mathf.Max(0.1f, playbackSpeed);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float envelope = Mathf.Sin(progress * Mathf.PI);
            if (destinationCounter != null)
                destinationCounter.localScale = destinationInitialScale *
                    (1f + preset.ImpactRebound * envelope);
            if (destinationGlow != null)
            {
                Color color = glowInitialColor;
                color.a = Mathf.Clamp01(preset.GlowIntensity * envelope);
                destinationGlow.color = color;
            }
            yield return null;
        }
        if (destinationCounter != null) destinationCounter.localScale = destinationInitialScale;
        if (destinationGlow != null) destinationGlow.color = glowInitialColor;
    }

    private void UpdateSpeedLabel()
    {
        if (speedLabel != null) speedLabel.text = playbackSpeed > 1f ? "VELOCIDADE 2×" : "VELOCIDADE 1×";
    }
}

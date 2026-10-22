using UnityEngine;

/// <summary>Resolve uma trajetória visual sem conhecer combate, territórios ou propriedade.</summary>
[DisallowMultipleComponent]
public sealed class WDCombatMotionAdapter : MonoBehaviour
{
    [SerializeField] private Transform origin;
    [SerializeField] private Transform destination;
    [SerializeField] private Transform traveller;
    [SerializeField] private WDCombatMotionPreset preset;

    public Transform Origin => origin;
    public Transform Destination => destination;
    public Transform Traveller => traveller;
    public WDCombatMotionPreset Preset => preset;

    // ============================================================
    // 01. CONFIGURAÇÃO — ponto único para futura ponte com DOTween
    // ============================================================
    public void Configure(Transform newOrigin, Transform newDestination,
        Transform newTraveller, WDCombatMotionPreset newPreset)
    {
        origin = newOrigin;
        destination = newDestination;
        traveller = newTraveller;
        preset = newPreset;
    }

    // ============================================================
    // 02. AMOSTRAGEM — suporta Transform de mundo e RectTransform
    // ============================================================
    public bool TryEvaluate(float progress, out Vector3 position)
    {
        position = default;
        if (origin == null || destination == null || traveller == null || preset == null)
            return false;

        Vector3 start = ResolvePoint(origin);
        Vector3 end = ResolvePoint(destination);
        Vector3 delta = end - start;
        Vector3 perpendicular = new(-delta.y, delta.x, 0f);
        if (perpendicular.sqrMagnitude > 0.0001f) perpendicular.Normalize();
        Vector3 control = Vector3.Lerp(start, end, 0.5f) + perpendicular * preset.CurveHeight;
        float t = Mathf.Clamp01(preset.TravelEase.Evaluate(Mathf.Clamp01(progress)));
        float inverse = 1f - t;
        position = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        return true;
    }

    public void ApplyPosition(Vector3 position)
    {
        if (traveller == null) return;
        // A posição é amostrada no espaço mundial para ambos os casos. O
        // RectTransform converte esse valor para seu Canvas sem assumir anchors.
        traveller.position = position;
    }

    private Vector3 ResolvePoint(Transform point)
    {
        if (point is not RectTransform rect) return point.position;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }
}

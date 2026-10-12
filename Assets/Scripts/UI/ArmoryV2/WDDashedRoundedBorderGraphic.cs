using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WDDashedRoundedBorderGraphic : MaskableGraphic
{
    [SerializeField, Min(.5f)] private float thickness = 1f;
    [SerializeField, Min(1f)] private float dashLength = 4f;
    [SerializeField, Min(1f)] private float gapLength = 4f;
    [SerializeField, Min(0f)] private float cornerRadius = 10f;

    // ============================================================
    // 01. CONFIGURAÇÃO E INVALIDAÇÃO DA GEOMETRIA
    // ============================================================
    public void Configure(float lineThickness, float dash, float gap, float radius, Color lineColor)
    {
        thickness = Mathf.Max(.5f, lineThickness);
        dashLength = Mathf.Max(1f, dash);
        gapLength = Mathf.Max(1f, gap);
        cornerRadius = Mathf.Max(0f, radius);
        color = lineColor;
        SetAllDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetAllDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }

    // ============================================================
    // 02. BORDA PONTILHADA VETORIAL SEM TEXTURA DUPLICADA
    // ============================================================
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        // Mantém toda a espessura dentro do Graphic. Antes, metade da linha
        // ficava fora do limite e desaparecia por clipping/subpixel no Canvas.
        float inset = Mathf.Max(1f, thickness * .5f + .5f);
        rect.xMin += inset;
        rect.xMax -= inset;
        rect.yMin += inset;
        rect.yMax -= inset;
        if (rect.width <= 0f || rect.height <= 0f) return;
        float radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * .5f);
        const int cornerSteps = 8;
        var points = new System.Collections.Generic.List<Vector2>(cornerSteps * 4 + 4);
        AddCorner(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, cornerSteps);
        AddCorner(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, cornerSteps);
        AddCorner(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, cornerSteps);
        AddCorner(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, cornerSteps);

        float cycle = dashLength + gapLength;
        float cursor = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            float segmentLength = Vector2.Distance(a, b);
            if (segmentLength <= Mathf.Epsilon) continue;
            Vector2 direction = (b - a) / segmentLength;
            float local = 0f;
            while (local < segmentLength)
            {
                float phase = cursor % cycle;
                float step = Mathf.Min(segmentLength - local, phase < dashLength ? dashLength - phase : cycle - phase);
                if (phase < dashLength)
                    AddQuad(vh, a + direction * local, a + direction * (local + step), thickness);
                local += step;
                cursor += step;
            }
        }
    }

    private static void AddCorner(System.Collections.Generic.ICollection<Vector2> points, Vector2 center,
        float radius, float startDegrees, float endDegrees, int steps)
    {
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)steps) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private void AddQuad(VertexHelper vh, Vector2 from, Vector2 to, float width)
    {
        Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * (width * .5f);
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = from - normal; vh.AddVert(vertex);
        vertex.position = from + normal; vh.AddVert(vertex);
        vertex.position = to + normal; vh.AddVert(vertex);
        vertex.position = to - normal; vh.AddVert(vertex);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}

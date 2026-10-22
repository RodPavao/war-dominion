using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ResolutionVisualMapUtility
{
    // ============================================================
    // 00. AUTORIDADE ESPACIAL ÚNICA DAS AÇÕES TERRESTRES
    // ============================================================
    public static Vector3 GetActionWorldAnchor(TerritorioClique territory)
    {
        if (territory == null)
            return Vector3.zero;
        return MapOverlayProjection.GetTerritoryPresentationWorldAnchor(territory);
    }

    public static Camera GetMapCamera()
    {
        if (LayoutPartida.CameraRuntime != null)
            return LayoutPartida.CameraRuntime;
        return Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
    }

    // ============================================================
    // 01. TERRITÓRIO E PROJEÇÃO GENÉRICA
    // ============================================================

    public static bool TryGetTerritory(string id, out TerritorioClique territory)
    {
        territory = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (MapaAtivo.Instance != null &&
            MapaAtivo.Instance.TentarObterTerritorio(id, out territory))
        {
            return territory != null;
        }

        foreach (TerritorioClique candidate in MapaAtivo.ObterTerritoriosOuCena())
        {
            if (candidate != null && candidate.idTerritorio == id)
            {
                territory = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetPosition(
        TerritorioClique territory,
        ResolutionVisualPresentationContext context,
        out Vector2 position)
    {
        position = default;
        if (territory == null || context == null)
            return false;

        Vector3 world = GetActionWorldAnchor(territory);
        Camera camera = GetMapCamera();
        return context.TryWorldToOverlay(world, camera, out position);
    }

    // ============================================================
    // 02. ELEMENTOS TEMPORÁRIOS SEM RAYCAST
    // ============================================================

    public static RectTransform CreateRoot(string name, RectTransform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image CreateImage(
        string name,
        Transform parent,
        Color color,
        float alpha)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        Image image = gameObject.GetComponent<Image>();
        color.a = alpha;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float size,
        Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 44f);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    public static void LayoutLine(
        RectTransform line,
        Vector2 start,
        Vector2 end,
        float thickness)
    {
        Vector2 delta = end - start;
        line.anchoredPosition = (start + end) * 0.5f;
        line.sizeDelta = new Vector2(delta.magnitude, thickness);
        line.localEulerAngles = new Vector3(
            0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }
}

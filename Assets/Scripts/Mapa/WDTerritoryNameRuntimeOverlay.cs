using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WDTerritoryNameRuntimeOverlay : MonoBehaviour
{
    public const string RootName = "TerritoryNamesRuntimeRoot";
    public const string LabelPrefix = "TerritoryName_";
    public const float FontSize = 16f;
    public const float WorldScale = 0.1f;
    public const float OutlineWidth = 0.38f;
    public const int SortingOffset = 30;

    private readonly Dictionary<string, TextMeshPro> labels =
        new Dictionary<string, TextMeshPro>(StringComparer.Ordinal);
    private readonly List<Material> materials = new List<Material>();
    private Transform root;
    private Camera mapCamera;
    private float initialFramingSize;
    private bool? lastVisibility;

    public int LabelCount => labels.Count;
    public bool RootActive => root != null && root.gameObject.activeSelf;
    public IReadOnlyDictionary<string, TextMeshPro> Labels => labels;

    private void LateUpdate()
    {
        RefreshVisibility();
    }

    private void OnDestroy()
    {
        ReleaseMaterials();
    }

    public void RebuildForMap(MapaAtivo map)
    {
        if (map == null || map.Definicao == null)
        {
            ClearMap();
            return;
        }

        Rebuild(map.TerritoriosRuntime, territory =>
        {
            DefinicaoTerritorioMapa definition =
                map.ObterDefinicaoTerritorio(territory.idTerritorio);
            return !string.IsNullOrWhiteSpace(definition?.nomeExibido)
                ? definition.nomeExibido
                : territory.idTerritorio;
        }, LayoutPartida.CameraRuntime, map.TamanhoOrtograficoEnquadrado,
        territory => ResolveWorldAnchor(territory,
            map.ObterDefinicaoTerritorio(territory.idTerritorio),
            map.TransformArte, LayoutPartida.EhArteIntegral16x9(map.Definicao)));
    }

    public void Rebuild(IEnumerable<TerritorioClique> territories,
        Func<TerritorioClique, string> resolveName, Camera camera,
        float framingSize,
        Func<TerritorioClique, Vector3> resolvePosition = null)
    {
        ClearMap();
        mapCamera = camera;
        initialFramingSize = Mathf.Max(0f, framingSize);

        GameObject rootObject = new GameObject(RootName);
        rootObject.transform.SetParent(transform, false);
        root = rootObject.transform;

        foreach (TerritorioClique territory in territories ??
            Array.Empty<TerritorioClique>())
        {
            if (territory == null || !territory.gameObject.activeInHierarchy ||
                string.IsNullOrWhiteSpace(territory.idTerritorio) ||
                labels.ContainsKey(territory.idTerritorio))
                continue;

            TextMeshPro label = CreateLabel(territory,
                resolveName?.Invoke(territory), resolvePosition?.Invoke(territory));
            labels.Add(territory.idTerritorio, label);
        }

        lastVisibility = null;
        RefreshVisibility();
    }

    public void RefreshVisibility()
    {
        if (root == null)
            return;
        bool recoveredCamera = false;
        if (mapCamera == null)
        {
            mapCamera = LayoutPartida.CameraRuntime;
            recoveredCamera = mapCamera != null;
        }
        bool visible = ShouldShowNames(MapaAtivo.NomesTerritoriaisHabilitados,
            mapCamera, initialFramingSize);
        bool visibilityChanged = lastVisibility != visible;
        if (visibilityChanged)
        {
            root.gameObject.SetActive(visible);
            lastVisibility = visible;
        }
        if (Application.isPlaying && mapCamera != null &&
            (recoveredCamera || visibilityChanged))
            LogRuntimeDiagnostic();
    }

    public void SetInitialFramingSize(float framingSize)
    {
        initialFramingSize = Mathf.Max(0f, framingSize);
        lastVisibility = null;
        RefreshVisibility();
    }

    public static bool ShouldShowNames(bool preferenceEnabled, Camera camera,
        float initialFramingSize)
    {
        return preferenceEnabled && camera != null && initialFramingSize > 0f &&
            camera.orthographicSize <= initialFramingSize *
                MapaAtivo.ProporcaoZoomParaExibirNomes;
    }

    public static Vector3 ResolveWorldAnchor(TerritorioClique territory)
    {
        if (territory == null)
            return Vector3.zero;
        Collider2D collider = territory.GetComponent<PolygonCollider2D>() ??
            territory.GetComponent<Collider2D>();
        Vector3 position = collider != null
            ? collider.bounds.center
            : territory.transform.position;
        position.z = territory.transform.position.z - 0.08f;
        return position;
    }

    public static Vector3 ResolveWorldAnchor(TerritorioClique territory,
        DefinicaoTerritorioMapa definition, Transform art,
        bool fullCanvas16x9)
    {
        if (territory != null && definition != null &&
            definition.possuiPosicaoNomeManual)
        {
            Vector3 manual = MapaAtivo.ResolverPosicaoNomeManualParaMundo(
                art, territory.transform, definition.posicaoNomeManual,
                fullCanvas16x9);
            manual.z = territory.transform.position.z - 0.08f;
            return manual;
        }
        return ResolveWorldAnchor(territory);
    }

    private void LogRuntimeDiagnostic()
    {
        if (!Application.isPlaying)
            return;
        bool preference = MapaAtivo.NomesTerritoriaisHabilitados;
        float zoom = mapCamera != null ? mapCamera.orthographicSize : -1f;
        float threshold = initialFramingSize *
            MapaAtivo.ProporcaoZoomParaExibirNomes;
        bool shouldShow = ShouldShowNames(preference, mapCamera,
            initialFramingSize);
        Debug.Log($"[TerritoryNames] preference={preference} " +
            $"zoom={zoom:0.###} threshold={threshold:0.###} " +
            $"shouldShow={shouldShow} rootActive={RootActive} " +
            $"labels={LabelCount}");
    }

    private TextMeshPro CreateLabel(TerritorioClique territory,
        string displayName, Vector3? resolvedPosition)
    {
        GameObject labelObject = new GameObject(LabelPrefix + territory.idTerritorio);
        labelObject.layer = territory.gameObject.layer;
        labelObject.transform.SetParent(root, true);
        labelObject.transform.position = resolvedPosition ??
            ResolveWorldAnchor(territory);
        labelObject.transform.rotation = Quaternion.identity;
        labelObject.transform.localScale = Vector3.one * WorldScale;
        labelObject.SetActive(true);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = string.IsNullOrWhiteSpace(displayName)
            ? territory.idTerritorio
            : displayName;
        label.enabled = true;
        label.alpha = 1f;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = FontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.rectTransform.sizeDelta = new Vector2(8f, 1.5f);

        Material material = label.fontSharedMaterial != null
            ? new Material(label.fontSharedMaterial)
            : null;
        if (material != null)
        {
            material.name = "WD Territory Name Material (Runtime Overlay)";
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            label.fontSharedMaterial = material;
            materials.Add(material);
        }

        SpriteRenderer territoryRenderer = territory.GetComponent<SpriteRenderer>();
        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        renderer.enabled = true;
        if (territoryRenderer != null)
        {
            renderer.sortingLayerID = territoryRenderer.sortingLayerID;
            renderer.sortingOrder = territoryRenderer.sortingOrder + SortingOffset;
        }
        else
        {
            renderer.sortingOrder = SortingOffset;
        }
        label.ForceMeshUpdate(true, true);
        return label;
    }

    private void ClearMap()
    {
        labels.Clear();
        ReleaseMaterials();
        if (root != null)
        {
            root.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(root.gameObject);
            else
                DestroyImmediate(root.gameObject);
        }
        root = null;
        lastVisibility = null;
    }

    private void ReleaseMaterials()
    {
        foreach (Material material in materials)
        {
            if (material == null)
                continue;
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
        materials.Clear();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum WDTacticalPingKind
{
    Attack,
    Defend,
    Transfer,
    Attention,
    Focus
}

public readonly struct WDTacticalPing
{
    public WDTacticalPingKind Kind { get; }
    public string TerritoryId { get; }
    public TerritorioClique.Dono Author { get; }
    public TerritorioClique.Dono Recipient { get; }

    public WDTacticalPing(WDTacticalPingKind kind, string territoryId,
        TerritorioClique.Dono author, TerritorioClique.Dono recipient)
    {
        Kind = kind;
        TerritoryId = territoryId ?? string.Empty;
        Author = author;
        Recipient = recipient;
    }
}

[DisallowMultipleComponent]
public sealed class WDMatchInformationalOverlayController : MonoBehaviour
{
    private static readonly string[] TacticalLabels =
        { "ATACAR", "DEFENDER", "TRANSFERIR", "ATENÇÃO", "FOCO" };
    private static readonly string[] TacticalIcons = { "!", "#", ">", "?", "+" };

    private RectTransform overlay;
    private RectTransform regionInfoOverlay;
    private WarDominionUITheme theme;
    private MatchUIState state;
    private WDTacticalPingKind? pendingKind;
    private TerritorioClique.Dono pendingRecipient;
    private bool regionsActive;
    private readonly List<WDTacticalPing> pings = new();
    private readonly Dictionary<GameObject, bool> counterVisibility = new();

    public bool RegionsActive => regionsActive;
    public bool IsSelectingTacticalTerritory => pendingKind.HasValue;
    public IReadOnlyList<WDTacticalPing> Pings => pings;
    public int RegionLabelCount => regionInfoOverlay != null
        ? regionInfoOverlay.childCount : 0;

    public void Configure(RectTransform root, WarDominionUITheme newTheme,
        RectTransform regionRoot = null)
    {
        overlay = root;
        regionInfoOverlay = regionRoot != null ? regionRoot : root;
        theme = newTheme;
    }

    private void OnDisable()
    {
        if (!regionsActive)
            return;
        regionsActive = false;
        foreach (TerritorioClique territory in MapaAtivo.ObterTerritoriosOuCena())
            territory?.GetComponent<TerritorioVisual>()?.RestaurarCor();
        SetTerritoryCountersForRegions(null, false);
        ClearRegionLabels();
    }

    // ============================================================
    // 01. ESTADO INFORMATIVO SEM MUTAÇÃO DE GAMEPLAY
    // ============================================================

    public void Present(MatchUIState newState)
    {
        bool leftPreparation = state != null &&
            state.Fase == GameManager.FaseTurno.Preparacao &&
            newState?.Fase != GameManager.FaseTurno.Preparacao;
        state = newState;
        if (leftPreparation)
        {
            pendingKind = null;
            pings.Clear();
            RebuildPingMarkers();
        }
    }

    public bool BeginTacticalPing(TerritorioClique.Dono recipient, WDTacticalPingKind kind)
    {
        if (state == null || state.Fase != GameManager.FaseTurno.Preparacao ||
            recipient == state.JogadorLocal ||
            !EquipesJogadores.SaoAliados(state.JogadorLocal, recipient))
            return false;
        pendingRecipient = recipient;
        pendingKind = kind;
        return true;
    }

    public void CancelTacticalSelection() => pendingKind = null;

    public bool HandleTerritory(string territoryId)
    {
        if (regionsActive)
            return true;
        if (!pendingKind.HasValue || state?.Fase != GameManager.FaseTurno.Preparacao)
            return false;
        pings.Add(new WDTacticalPing(pendingKind.Value, territoryId,
            state.JogadorLocal, pendingRecipient));
        pendingKind = null;
        RebuildPingMarkers();
        return true;
    }

    public static bool IsVisibleTo(WDTacticalPing ping, TerritorioClique.Dono viewer,
        bool spectator) => IsVisibleTo(ping, viewer, spectator,
            EquipesJogadores.SaoAliados(ping.Author, viewer));

    public static bool IsVisibleTo(WDTacticalPing ping, TerritorioClique.Dono viewer,
        bool spectator, bool viewerIsAllied) => !spectator &&
        (viewer == ping.Author || viewer == ping.Recipient || viewerIsAllied);

    // ============================================================
    // 02. REGIÕES CANÔNICAS E RESTAURAÇÃO VISUAL
    // ============================================================

    public void ToggleRegions()
    {
        regionsActive = !regionsActive;
        pendingKind = null;
        if (!regionsActive)
        {
            foreach (TerritorioClique territory in MapaAtivo.ObterTerritoriosOuCena())
                territory?.GetComponent<TerritorioVisual>()?.RestaurarCor();
            SetTerritoryCountersForRegions(null, false);
            ClearRegionLabels();
            return;
        }

        DefinicaoMapa map = MapaAtivo.Instance?.Definicao;
        if (map == null)
        {
            regionsActive = false;
            return;
        }
        SetTerritoryCountersForRegions(MapaAtivo.ObterTerritoriosOuCena(), true);
        ClearRegionLabels();
        var occupiedLabels = new List<Rect>();
        for (int index = 0; index < map.Regioes.Count; index++)
        {
            DefinicaoRegiaoMapa region = map.Regioes[index];
            Color color = ResolveRegionPresentationColor(region, index,
                map.Regioes.Count);
            foreach (string id in region.territorioIds)
                if (TryTerritory(id, out TerritorioClique territory))
                    territory.GetComponent<TerritorioVisual>()?.DestacarContinente(color);
            CreateRegionLabel(region, color, occupiedLabels);
        }
    }

    public static string DescribeRegion(DefinicaoRegiaoMapa region) => region == null
        ? string.Empty : $"{region.nomeExibido}\nBÔNUS +{region.bonus}";

    public static string DescribeRegionBonus(DefinicaoRegiaoMapa region) => region == null
        ? string.Empty : $"+{region.bonus}";

    public static string DescribeRegionInline(DefinicaoRegiaoMapa region) => region == null
        ? string.Empty : $"{region.nomeExibido} +{region.bonus}";

    public static Color ResolveRegionPresentationColor(
        DefinicaoRegiaoMapa region, int index = 0, int regionCount = 1)
    {
        if (region == null)
            return Color.white;

        // A paleta Classic reproduz os seis acentos aprovados no Figma.
        // A lista, os nomes e os bônus continuam vindo de DefinicaoMapa.Regioes.
        switch (region.id)
        {
            case "america_norte": return new Color(77f / 255f, 143f / 255f, 209f / 255f);
            case "america_sul": return new Color(88f / 255f, 168f / 255f, 115f / 255f);
            case "europa": return new Color(146f / 255f, 112f / 255f, 200f / 255f);
            case "africa": return new Color(211f / 255f, 154f / 255f, 74f / 255f);
            case "asia": return new Color(213f / 255f, 107f / 255f, 99f / 255f);
            case "oceania": return new Color(76f / 255f, 183f / 255f, 197f / 255f);
        }

        Color source = region.corDestaque;
        if (source.a <= 0.05f || source.grayscale > 0.92f)
            source = Color.HSVToRGB(index / Mathf.Max(1f, regionCount), 0.52f, 0.92f);
        source.a = 1f;
        return source;
    }

    public static Vector2 CalculateRegionBadgeSize(Vector2 preferredTextSize)
    {
        const float horizontalPadding = 28f;
        const float verticalPadding = 14f;
        return new Vector2(
            Mathf.Max(84f, Mathf.Ceil(preferredTextSize.x) + horizontalPadding),
            Mathf.Max(34f, Mathf.Ceil(preferredTextSize.y) + verticalPadding));
    }

    // ============================================================
    // 03. MARCADORES PRIVADOS DA EQUIPE
    // ============================================================

    private void RebuildPingMarkers()
    {
        ClearOverlay("Tactical_");
        foreach (WDTacticalPing ping in pings)
        {
            if (!IsVisibleTo(ping, state?.JogadorLocal ?? TerritorioClique.Dono.Neutro, false) ||
                !TryTerritory(ping.TerritoryId, out TerritorioClique territory))
                continue;
            int index = (int)ping.Kind;
            CreateWorldLabel($"Tactical_{ping.TerritoryId}_{index}", territory.transform.position,
                $"{TacticalIcons[index]}  {TacticalLabels[index]}", Color.yellow);
        }
    }

    private void CreateRegionLabel(DefinicaoRegiaoMapa region, Color color,
        IList<Rect> occupiedLabels)
    {
        var positions = new List<Vector3>();
        foreach (string id in region.territorioIds)
            if (TryTerritory(id, out TerritorioClique territory))
            {
                Transform counter = territory.transform.Find("ContadorModerno");
                Vector3 world = counter != null
                    ? counter.position
                    : territory.transform.position;
                positions.Add(MapaAtivo.Instance != null
                    ? (Vector3)MapaAtivo.Instance.ConverterPosicaoMundoParaArte(world)
                    : world);
            }
        if (TryCalculateRegionPosition(positions,
                region.possuiDeslocamentoLabelManual,
                region.deslocamentoLabelManual, out Vector3 artLocal))
        {
            Vector3 world = MapaAtivo.Instance != null
                ? MapaAtivo.Instance.ConverterPosicaoArteParaMundo(artLocal)
                : artLocal;
            CreateRegionBadge("Region_" + region.id, world,
                region.nomeExibido, DescribeRegionBonus(region), color, occupiedLabels,
                !region.possuiDeslocamentoLabelManual);
        }
    }

    public static bool TryCalculateRegionPosition(
        IEnumerable<Vector3> territoryPositions, bool hasManualOffset,
        Vector2 manualOffset, out Vector3 position)
    {
        if (!TryCalculateRegionAnchor(territoryPositions, out position))
            return false;
        if (hasManualOffset)
            position += (Vector3)manualOffset;
        return true;
    }

    public static bool TryCalculateRegionCenter(
        IEnumerable<Vector3> territoryPositions, out Vector3 center)
    {
        Vector3 total = Vector3.zero;
        int count = 0;
        foreach (Vector3 position in territoryPositions ?? Array.Empty<Vector3>())
        {
            total += position;
            count++;
        }
        center = count > 0 ? total / count : Vector3.zero;
        return count > 0;
    }

    public static bool TryCalculateRegionAnchor(
        IEnumerable<Vector3> territoryPositions, out Vector3 anchor)
    {
        Vector3[] positions = (territoryPositions ?? Array.Empty<Vector3>()).ToArray();
        if (!TryCalculateRegionCenter(positions, out Vector3 centroid))
        {
            anchor = Vector3.zero;
            return false;
        }

        anchor = positions
            .OrderBy(position => (position - centroid).sqrMagnitude)
            .ThenBy(position => position.x)
            .ThenBy(position => position.y)
            .First();
        return true;
    }

    private void CreateRegionBadge(
        string name, Vector3 world, string title, string bonus, Color color,
        IList<Rect> occupiedLabels, bool allowAutomaticAvoidance)
    {
        Camera camera = Camera.main != null
            ? Camera.main
            : FindAnyObjectByType<Camera>();
        if (regionInfoOverlay == null || camera == null ||
            !MapOverlayProjection.TryWorldToOverlay(
                regionInfoOverlay, world, camera, out Vector2 overlayPosition))
            return;
        RectTransform group = WDUIFactory.Rect(name, regionInfoOverlay);
        group.anchorMin = group.anchorMax = new Vector2(0.5f, 0.5f);
        group.pivot = new Vector2(0.5f, 0.5f);
        group.localScale = Vector3.one;
        group.gameObject.SetActive(true);
        group.SetAsLastSibling();

        Image plate = group.gameObject.AddComponent<Image>();
        plate.color = new Color(8f / 255f, 16f / 255f, 24f / 255f, 0.88f);
        plate.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(plate, 10);
        WDHomeUIFactory.AddRoundedBorder(group,
            new Color(220f / 255f, 230f / 255f, 239f / 255f, 0.18f), 10);
        Shadow dropShadow = group.gameObject.AddComponent<Shadow>();
        dropShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        dropShadow.effectDistance = new Vector2(0f, -6f);
        dropShadow.useGraphicAlpha = true;

        RectTransform accent = WDUIFactory.Rect("Accent", group);
        accent.anchorMin = accent.anchorMax = new Vector2(0f, 0.5f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(4f, 36f);
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = color;
        accentImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(accentImage, 2);

        TextMeshProUGUI titleLabel = WDUIFactory.Text("Title", group, theme,
            11.5f, new Color(241f / 255f, 244f / 255f, 247f / 255f),
            TextAlignmentOptions.Left);
        titleLabel.rectTransform.anchorMin = titleLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchoredPosition = new Vector2(16f, -8f);
        titleLabel.rectTransform.sizeDelta = new Vector2(140f, 18f);
        titleLabel.text = (title ?? string.Empty).ToUpperInvariant();
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.raycastTarget = false;

        TextMeshProUGUI bonusLabel = WDUIFactory.Text("Bonus", group, theme,
            20f, color, TextAlignmentOptions.Left);
        bonusLabel.rectTransform.anchorMin = bonusLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        bonusLabel.rectTransform.pivot = new Vector2(0f, 1f);
        bonusLabel.rectTransform.anchoredPosition = new Vector2(16f, -29f);
        bonusLabel.rectTransform.sizeDelta = new Vector2(140f, 27f);
        bonusLabel.text = bonus ?? string.Empty;
        bonusLabel.fontStyle = FontStyles.Bold;
        bonusLabel.textWrappingMode = TextWrappingModes.NoWrap;
        bonusLabel.raycastTarget = false;

        group.sizeDelta = new Vector2(160f, 60f);
        Rect viewport = regionInfoOverlay.rect;
        if (viewport.width <= 1f || viewport.height <= 1f)
            viewport = new Rect(-Screen.width * 0.5f, -Screen.height * 0.5f,
                Screen.width, Screen.height);
        group.anchoredPosition = ResolveRegionLabelPositionForMode(
            overlayPosition, viewport, group.sizeDelta, occupiedLabels,
            allowAutomaticAvoidance);
        occupiedLabels?.Add(new Rect(group.anchoredPosition - group.sizeDelta * 0.5f,
            group.sizeDelta));
    }

    public static Vector2 ClampToViewport(
        Vector2 position, Rect viewport, Vector2 labelSize)
    {
        float halfWidth = Mathf.Min(labelSize.x * 0.5f, viewport.width * 0.5f);
        float halfHeight = Mathf.Min(labelSize.y * 0.5f, viewport.height * 0.5f);
        return new Vector2(
            Mathf.Clamp(position.x, viewport.xMin + halfWidth,
                viewport.xMax - halfWidth),
            Mathf.Clamp(position.y, viewport.yMin + halfHeight,
                viewport.yMax - halfHeight));
    }

    public static Vector2 ResolveRegionLabelPosition(Vector2 preferred, Rect viewport,
        Vector2 labelSize, IEnumerable<Rect> occupiedLabels)
    {
        float horizontalStep = labelSize.x + 8f;
        float verticalStep = labelSize.y + 8f;
        Vector2[] offsets =
        {
            Vector2.zero,
            new Vector2(horizontalStep, 0f), new Vector2(-horizontalStep, 0f),
            new Vector2(0f, verticalStep), new Vector2(0f, -verticalStep),
            new Vector2(horizontalStep, verticalStep),
            new Vector2(-horizontalStep, verticalStep),
            new Vector2(horizontalStep, -verticalStep),
            new Vector2(-horizontalStep, -verticalStep)
        };
        Rect[] occupied = (occupiedLabels ?? Array.Empty<Rect>()).ToArray();
        foreach (Vector2 offset in offsets)
        {
            Vector2 candidate = ClampToViewport(preferred + offset, viewport, labelSize);
            Rect candidateRect = new Rect(candidate - labelSize * 0.5f, labelSize);
            if (!occupied.Any(candidateRect.Overlaps))
                return candidate;
        }
        return ClampToViewport(preferred, viewport, labelSize);
    }

    public static Vector2 ResolveRegionLabelPositionForMode(Vector2 preferred,
        Rect viewport, Vector2 labelSize, IEnumerable<Rect> occupiedLabels,
        bool allowAutomaticAvoidance)
    {
        return allowAutomaticAvoidance
            ? ResolveRegionLabelPosition(preferred, viewport, labelSize,
                occupiedLabels)
            : ClampToViewport(preferred, viewport, labelSize);
    }

    public void SetTerritoryCountersForRegions(
        IEnumerable<TerritorioClique> territories, bool regionsVisible)
    {
        if (!regionsVisible)
        {
            RestoreTroopCounters();
            return;
        }
        counterVisibility.Clear();
        foreach (TerritorioClique territory in territories ?? Array.Empty<TerritorioClique>())
        {
            ContadorTropas counter = territory?.GetComponentInChildren<ContadorTropas>(true);
            if (counter == null || counterVisibility.ContainsKey(counter.gameObject))
                continue;
            counterVisibility[counter.gameObject] = counter.gameObject.activeSelf;
            counter.gameObject.SetActive(false);
        }
    }

    private void RestoreTroopCounters()
    {
        foreach (KeyValuePair<GameObject, bool> item in counterVisibility)
            if (item.Key != null)
                item.Key.SetActive(item.Value);
        counterVisibility.Clear();
    }

    private void CreateWorldLabel(string name, Vector3 world, string value, Color color)
    {
        if (overlay == null || Camera.main == null ||
            !MapOverlayProjection.TryWorldToOverlay(
                overlay, world, Camera.main, out Vector2 overlayPosition))
            return;
        TextMeshProUGUI label = WDUIFactory.Text(name, overlay, theme,
            theme.TypeMicro, color, TextAlignmentOptions.Center);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = overlayPosition;
        rect.sizeDelta = new Vector2(180f, 54f);
        label.text = value;
        label.raycastTarget = false;
    }

    private void ClearOverlay(string prefix)
    {
        if (overlay == null)
            return;
        for (int i = overlay.childCount - 1; i >= 0; i--)
            if (overlay.GetChild(i).name.StartsWith(prefix, StringComparison.Ordinal))
                Destroy(overlay.GetChild(i).gameObject);
    }

    private void ClearRegionLabels()
    {
        if (regionInfoOverlay == null)
            return;
        for (int index = regionInfoOverlay.childCount - 1; index >= 0; index--)
        {
            GameObject child = regionInfoOverlay.GetChild(index).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private static bool TryTerritory(string id, out TerritorioClique result)
    {
        result = MapaAtivo.ObterTerritoriosOuCena().FirstOrDefault(item =>
            item != null && string.Equals(item.idTerritorio, id, StringComparison.Ordinal));
        return result != null;
    }
}

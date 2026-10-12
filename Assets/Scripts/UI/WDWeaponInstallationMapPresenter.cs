using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDInstallationMarkerPointer : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private Action pressed;
    private int activePointerId = int.MinValue;

    public void Configure(Action callback) => pressed = callback;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        activePointerId = eventData.pointerId;
        pressed?.Invoke();
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData) => eventData?.Use();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;
        // Alguns módulos sintetizam click sem encaminhar pointer-down ao mesmo
        // objeto. O fallback ainda abre uma única vez no mesmo gesto.
        if (eventData.pointerId != activePointerId)
            pressed?.Invoke();
        activePointerId = int.MinValue;
        eventData.Use();
    }
}

[DisallowMultipleComponent]
public sealed class WDWeaponInstallationMapPresenter : MonoBehaviour,
    IResolutionVisualStateCoordinator
{
    private const string AssetRoot = "UI/Match/FigmaCurrent/InstallationTerritoryIcons/";
    private const float ReferenceBadgeSize = 44f;
    private const float StandardBadgeCounterRatio = .6272f;
    private const float SymbolOccupiedRatio = .68f;
    private const float CollisionPaddingRatio = .08f;
    private const float TerritoryBorderClearanceRatio = .10f;

    private static readonly Vector2 BadgeTouchSize = new(56f, 56f);
    private readonly Dictionary<string, Marker> markers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);
    private static readonly List<WDWeaponInstallationMapPresenter> livePresenters = new();
    private WDWeaponMatchUIController weaponUI;
    private RectTransform content;
    private string snapshotKey = string.Empty;
    private bool projectionErrorLogged;
    private Action<WDWeaponHudInstallationState> openInstallationHub;
    private readonly HashSet<string> pendingResolutionFades = new(StringComparer.Ordinal);
    private readonly HashSet<string> presentedResolutionFades = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> transportedFinalPositions =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> frozenTransportMarkers = new(StringComparer.Ordinal);

    private sealed class Marker
    {
        public RectTransform Rect;
        public RectTransform Badge;
        public RectTransform Icon;
        public CanvasGroup VisualGroup;
        public WDWeaponHudInstallationState State;
        public Vector2 IconCanvasRatio;
        public bool FallbackWarningLogged;
        public bool HasPlacement;
        public int PlacementContextHash;
    }

    // ============================================================
    // 01. SNAPSHOT VISÍVEL E MARCADORES TOUCH-FRIENDLY
    // ============================================================

    public void Configure(RectTransform targetOverlay, WarDominionUITheme value,
        WDWeaponMatchUIController controller,
        Action<WDWeaponHudInstallationState> installationHubAction = null)
    {
        weaponUI = controller;
        if (!livePresenters.Contains(this)) livePresenters.Add(this);
        openInstallationHub = installationHubAction;
        content = WDUIFactory.Rect("VisibleWeaponInstallations", targetOverlay);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;
        // Marcadores ficam abaixo da Match UI V2: um outside overlay aberto deve
        // receber o primeiro toque antes de qualquer instalação subjacente.
        content.SetSiblingIndex(Math.Max(0, targetOverlay.childCount - 2));
        StartCoroutine(RefreshLoop());
    }

    private IEnumerator RefreshLoop()
    {
        var wait = new WaitForSecondsRealtime(0.25f);
        while (enabled)
        {
            RefreshVisibleInstallations();
            yield return wait;
        }
    }

    private void LateUpdate() => UpdateMarkerPositions();

    internal void RefreshVisibleInstallations()
    {
        if (weaponUI == null || content == null) return;
        WDWeaponHudInstallationState[] visible = weaponUI.GetVisibleInstallations()
            .Where(item => WDInstallationHubFormatter.IsCanonicalInstallation(item.WeaponId))
            .ToArray();
        string key = string.Join("|", visible.Select(item =>
            $"{item.InstallationId}:{item.WeaponId}:{item.TerritoryId}:{item.OwnerPlayerId}:" +
            $"{item.Charges}:{item.Duration}:{item.Barrier}:{item.PaymentsRemaining}:" +
            $"{item.IsDisabled}:{item.IsAbandoned}:{item.CanOperate}"));
        if (key != snapshotKey)
        {
            snapshotKey = key;
            Rebuild(visible);
        }
        UpdateMarkerPositions();
    }

    private void Rebuild(IReadOnlyList<WDWeaponHudInstallationState> visible)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        markers.Clear();

        foreach (IGrouping<string, WDWeaponHudInstallationState> territory in visible
            .GroupBy(item => item.TerritoryId, StringComparer.Ordinal))
        {
            WDWeaponHudInstallationState[] ordered = territory
                .OrderBy(item => item.WeaponId, StringComparer.Ordinal)
                .ThenBy(item => item.InstallationId, StringComparer.Ordinal).ToArray();
            foreach (WDWeaponHudInstallationState installation in ordered)
                BuildMarker(installation);
        }
    }

    private void BuildMarker(WDWeaponHudInstallationState installation)
    {
        RectTransform hit = WDUIFactory.Rect("Installation_" + installation.InstallationId,
            content);
        hit.anchorMin = hit.anchorMax = new Vector2(.5f, .5f);
        hit.sizeDelta = new Vector2(56f, 56f);
        Image hitImage = hit.gameObject.AddComponent<Image>();
        hitImage.color = Color.clear;
        hit.gameObject.AddComponent<WDInstallationMarkerPointer>().Configure(() =>
            openInstallationHub?.Invoke(
            markers.TryGetValue(installation.InstallationId, out Marker marker)
                ? marker.State : installation));

        // O root de 56 px permanece exclusivamente como touch target. O badge
        // visual compacto e o símbolo oficial têm geometria independente.
        RectTransform badge = WDUIFactory.Rect("InstallationBadge", hit);
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(.5f, .5f);
        badge.sizeDelta = Vector2.one * ReferenceBadgeSize;
        Image badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.color = new Color32(5, 7, 10, 245);
        badgeImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(badgeImage, 8);
        WDHomeUIFactory.AddRoundedBorder(badge, Color.white, 8);
        CanvasGroup visualGroup = badge.gameObject.AddComponent<CanvasGroup>();
        visualGroup.blocksRaycasts = false;
        visualGroup.interactable = false;
        visualGroup.alpha = pendingResolutionFades.Contains(
            FadeKey(installation.WeaponId, installation.TerritoryId)) ? 0f : 1f;

        RectTransform iconRect = WDUIFactory.Rect("InstallationIcon", badge);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, .5f);
        Vector2 iconSize = TerritoryIconSizeFor(installation.WeaponId);
        iconRect.sizeDelta = iconSize;
        Image icon = iconRect.gameObject.AddComponent<Image>();
        string assetName = AssetNameFor(installation.WeaponId);
        Texture2D iconTexture = Resources.Load<Texture2D>(AssetRoot + assetName);
        icon.sprite = LoadSprite(assetName, iconTexture);
        if (icon.sprite == null)
            Debug.LogError($"[InstallationMap] Asset oficial ausente: {assetName} " +
                $"({installation.WeaponId}).", this);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = installation.IsAbandoned || installation.IsDisabled
            ? new Color(1f, 1f, 1f, .5f) : Color.white;

        markers[installation.InstallationId] = new Marker
        {
            Rect = hit, Badge = badge, Icon = iconRect, VisualGroup = visualGroup,
            State = installation,
            IconCanvasRatio = VisibleContentCanvasRatio(iconTexture, iconSize)
        };
    }

    // ============================================================
    // 02. EMPILHAMENTO ESTÁVEL, LOCAL E INDEPENDENTE DO MAPA
    // ============================================================

    public static Vector2 StackOffset(int index, int count)
    {
        // Compatibilidade do contrato estático anterior. O runtime atual usa
        // candidatos validados pelo collider em TryFindIconPosition.
        if (count <= 1) return new Vector2(0f, 32f);
        int column = index % 3;
        int row = index / 3;
        int rows = (count + 2) / 3;
        int columnsInRow = Math.Min(3, count - row * 3);
        const float spacing = 40f;
        float width = (columnsInRow - 1) * spacing;
        return new Vector2(column * spacing - width * .5f,
            48f + (row - (rows - 1) * .5f) * spacing);
    }

    private void UpdateMarkerPositions()
    {
        if (content == null) return;
        List<Rect> tacticalExclusions = GetTacticalExclusions();
        foreach (IGrouping<string, Marker> territoryGroup in markers.Values
            .GroupBy(item => item.State.TerritoryId, StringComparer.Ordinal))
        {
            var placed = new List<Rect>();
            var placedTouches = new List<Rect>();
            foreach (Marker marker in territoryGroup
                .OrderBy(item => item.State.WeaponId, StringComparer.Ordinal)
                .ThenBy(item => item.State.InstallationId, StringComparer.Ordinal))
            {
                if (frozenTransportMarkers.Contains(marker.State.InstallationId))
                    continue;
                if (!MapOverlayProjection.TryTerritoryToOverlay(marker.State.TerritoryId,
                        content, out Vector2 counterAnchor, out TerritorioClique territory))
                {
                    marker.Rect.gameObject.SetActive(false);
                    if (!projectionErrorLogged)
                    {
                        projectionErrorLogged = true;
                        Debug.LogError("[InstallationMap] Falha na projeção do território " +
                            $"'{marker.State.TerritoryId}'.", this);
                    }
                    continue;
                }

                CounterFootprintInOverlay(territory, out Vector2 counterVisualSize,
                    out Vector2 counterTouchSize);
                float badgeExtent = Mathf.Min(counterVisualSize.x, counterVisualSize.y) *
                    StandardBadgeCounterRatio;
                Vector2 badgeVisualSize = Vector2.one * badgeExtent;
                TryProjectedTerritoryBounds(territory, out Rect projectedBounds);
                int placementContext = PlacementContextHash(counterAnchor,
                    counterVisualSize, counterTouchSize, badgeVisualSize, projectedBounds,
                    placed, placedTouches, tacticalExclusions);
                if (marker.HasPlacement && marker.PlacementContextHash == placementContext)
                {
                    placed.Add(IconBounds(marker.Rect.anchoredPosition,
                        marker.Badge.sizeDelta));
                    placedTouches.Add(IconBounds(marker.Rect.anchoredPosition,
                        BadgeTouchSize));
                    continue;
                }
                if (!TryFindIconPosition(territory, counterAnchor, placed, placedTouches,
                    tacticalExclusions, counterVisualSize, counterTouchSize, badgeVisualSize,
                    out Vector2 position, out Vector2 visualSize,
                    out bool usedFallback))
                {
                    // Projeções podem ficar transitoriamente indisponíveis durante
                    // alterações de câmera/layout. Nunca descartar uma posição já
                    // validada por causa desse frame intermediário.
                    if (marker.HasPlacement)
                    {
                        placed.Add(IconBounds(marker.Rect.anchoredPosition,
                            marker.Badge.sizeDelta));
                        placedTouches.Add(IconBounds(marker.Rect.anchoredPosition,
                            BadgeTouchSize));
                        continue;
                    }
                    marker.Rect.gameObject.SetActive(false);
                    if (!marker.FallbackWarningLogged)
                    {
                        marker.FallbackWarningLogged = true;
                        Debug.LogError("[InstallationMap] Nenhuma posição sem colisão " +
                            $"encontrada no território '{marker.State.TerritoryId}'.", this);
                    }
                    continue;
                }
                projectionErrorLogged = false;
                marker.Rect.gameObject.SetActive(true);
                marker.Rect.anchoredPosition = position;
                marker.HasPlacement = true;
                marker.PlacementContextHash = placementContext;
                marker.Badge.sizeDelta = visualSize;
                float occupiedExtent = Mathf.Min(visualSize.x, visualSize.y) *
                    SymbolOccupiedRatio;
                marker.Icon.sizeDelta = marker.IconCanvasRatio * occupiedExtent;
                placed.Add(IconBounds(position, visualSize));
                placedTouches.Add(IconBounds(position, BadgeTouchSize));
                if (usedFallback && !marker.FallbackWarningLogged &&
                    (Application.isEditor || Debug.isDebugBuild))
                {
                    marker.FallbackWarningLogged = true;
                    Debug.LogWarning("[InstallationMap] Posicionamento visual aproximado " +
                        $"usado no território '{marker.State.TerritoryId}'.", this);
                }
                else if (!usedFallback)
                {
                    marker.FallbackWarningLogged = false;
                }
            }
        }
    }

    // ============================================================
    // 03. POSIÇÃO LOCAL VALIDADA PELO COLLIDER DO TERRITÓRIO
    // ============================================================

    private bool TryFindIconPosition(TerritorioClique territory, Vector2 counterAnchor,
        IReadOnlyList<Rect> placed, IReadOnlyList<Rect> placedTouches,
        IReadOnlyList<Rect> tactical, Vector2 counterVisualSize,
        Vector2 counterTouchSize, Vector2 baseVisualSize,
        out Vector2 position, out Vector2 visualSize, out bool usedFallback)
    {
        position = default;
        visualSize = baseVisualSize;
        usedFallback = false;
        Vector2[] directions =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new(1f, 1f), new(-1f, 1f), new(1f, -1f), new(-1f, -1f)
        };
        float minimumDistance =
            (Mathf.Max(counterVisualSize.x, counterVisualSize.y) +
             Mathf.Max(baseVisualSize.x, baseVisualSize.y)) * .5f +
            Mathf.Min(baseVisualSize.x, baseVisualSize.y) * CollisionPaddingRatio;
        float[] distances =
        {
            minimumDistance, minimumDistance + 10f, minimumDistance + 20f,
            minimumDistance + 34f, minimumDistance + 50f
        };
        float[] scales = { 1f };
        Rect counter = IconBounds(counterAnchor, counterVisualSize);
        Rect counterTouch = IconBounds(counterAnchor, counterTouchSize);
        foreach (float scale in scales)
        {
            Vector2 size = baseVisualSize * scale;
            foreach (float distance in distances)
            foreach (Vector2 rawDirection in directions)
            {
                Vector2 candidate = counterAnchor + rawDirection.normalized * distance;
                Rect bounds = IconBounds(candidate, size);
                Rect collisionBounds = Expand(bounds,
                    Mathf.Min(size.x, size.y) * CollisionPaddingRatio);
                if (collisionBounds.Overlaps(counter) ||
                    placed.Any(other => collisionBounds.Overlaps(Expand(other,
                        Mathf.Min(other.width, other.height) * CollisionPaddingRatio))) ||
                    tactical.Any(collisionBounds.Overlaps) ||
                    !IsFullyInsideTerritory(territory, candidate, size))
                    continue;
                position = candidate;
                visualSize = size;
                return true;
            }
        }

        // 04. A busca integral percorre primeiro toda a área projetada erodida pelo
        // badge. Overflow só é considerado depois que esta etapa se esgota.
        visualSize = baseVisualSize;
        if (TryProjectedTerritoryBounds(territory, out Rect territoryBounds))
        {
            float halfWidth = visualSize.x * .5f;
            float halfHeight = visualSize.y * .5f;
            float step = Mathf.Max(4f, Mathf.Min(visualSize.x, visualSize.y) * .2f);
            var candidates = new List<Vector2>();
            for (float y = territoryBounds.yMin + halfHeight;
                 y <= territoryBounds.yMax - halfHeight; y += step)
            for (float x = territoryBounds.xMin + halfWidth;
                 x <= territoryBounds.xMax - halfWidth; x += step)
                candidates.Add(new Vector2(x, y));

            Vector2[] orderedCandidates = candidates
                .OrderBy(value => Vector2.SqrMagnitude(value - counterAnchor))
                .ThenBy(value => value.y)
                .ThenBy(value => value.x)
                .ToArray();
            Vector2 integralSize = visualSize;

            bool IsIntegralCandidate(Vector2 candidate)
            {
                if (!IsFullyInsideTerritory(territory, candidate,
                        ExpandedTerritoryFitSize(integralSize)))
                    return false;
                Rect candidateBounds = IconBounds(candidate, integralSize);
                Rect collisionBounds = Expand(candidateBounds,
                    Mathf.Min(integralSize.x, integralSize.y) * CollisionPaddingRatio);
                if (collisionBounds.Overlaps(counter) ||
                    placed.Any(other => collisionBounds.Overlaps(Expand(other,
                        Mathf.Min(other.width, other.height) * CollisionPaddingRatio))) ||
                    tactical.Any(collisionBounds.Overlaps))
                    return false;
                return true;
            }

            foreach (Vector2 candidate in orderedCandidates)
            {
                if (!IsIntegralCandidate(candidate))
                    continue;
                position = candidate;
                return true;
            }

            // Uma segunda malha proporcional, deslocada, cobre as lacunas da primeira
            // sem criar a varredura de milhões de pontos que rodava no LateUpdate.
            float refinedStep = Mathf.Max(2f, step * .5f);
            var refinedCandidates = new List<Vector2>();
            for (float y = territoryBounds.yMin + halfHeight;
                 y <= territoryBounds.yMax - halfHeight; y += refinedStep)
            for (float x = territoryBounds.xMin + halfWidth;
                 x <= territoryBounds.xMax - halfWidth; x += refinedStep)
                refinedCandidates.Add(new Vector2(x, y));
            foreach (Vector2 candidate in refinedCandidates
                .OrderBy(value => Vector2.SqrMagnitude(value - counterAnchor))
                .ThenBy(value => value.y)
                .ThenBy(value => value.x))
            {
                if (!IsIntegralCandidate(candidate))
                    continue;
                position = candidate;
                return true;
            }

            // 05. Territórios menores que o badge não geram candidatos na área
            // erodida acima. A reserva percorre toda a projeção e permite que o
            // visual ultrapasse a borda, mantendo seu centro no território real.
            usedFallback = true;
            var partialCandidates = new List<Vector2> { territoryBounds.center };
            for (float y = territoryBounds.yMin; y <= territoryBounds.yMax; y += step)
            for (float x = territoryBounds.xMin; x <= territoryBounds.xMax; x += step)
                partialCandidates.Add(new Vector2(x, y));
            Vector2 bestCandidate = default;
            int bestCoverage = -1;
            foreach (Vector2 candidate in partialCandidates
                .Distinct()
                .OrderBy(value => Vector2.SqrMagnitude(value - counterAnchor))
                .ThenBy(value => value.y)
                .ThenBy(value => value.x))
            {
                int coverage = TerritoryCoverage(territory, candidate, visualSize,
                    out bool centerInside);
                if (!centerInside || coverage <= bestCoverage)
                    continue;
                Rect candidateBounds = IconBounds(candidate, visualSize);
                Rect candidateTouch = IconBounds(candidate, BadgeTouchSize);
                if (candidateBounds.Overlaps(counter) ||
                    placed.Any(candidateBounds.Overlaps) ||
                    tactical.Any(candidateBounds.Overlaps) ||
                    candidateTouch.Overlaps(counterTouch) ||
                    placedTouches.Any(candidateTouch.Overlaps) ||
                    tactical.Any(candidateTouch.Overlaps))
                    continue;
                bestCoverage = coverage;
                bestCandidate = candidate;
            }
            if (bestCoverage >= 0)
            {
                position = bestCandidate;
                return true;
            }

            // Última reserva visual: touch targets grandes podem não caber juntos
            // embora os badges caibam. Mantém os visuais distintos e associados ao
            // território, sem ocultar uma instalação que existe no registry.
            bestCoverage = -1;
            foreach (Vector2 candidate in partialCandidates
                .Distinct()
                .OrderBy(value => Vector2.SqrMagnitude(value - counterAnchor))
                .ThenBy(value => value.y)
                .ThenBy(value => value.x))
            {
                int coverage = TerritoryCoverage(territory, candidate, visualSize,
                    out bool centerInside);
                if (!centerInside || coverage <= bestCoverage)
                    continue;
                Rect candidateBounds = Expand(IconBounds(candidate, visualSize),
                    Mathf.Min(visualSize.x, visualSize.y) * CollisionPaddingRatio);
                if (candidateBounds.Overlaps(counter) ||
                    placed.Any(other => candidateBounds.Overlaps(Expand(other,
                        Mathf.Min(other.width, other.height) * CollisionPaddingRatio))) ||
                    tactical.Any(candidateBounds.Overlaps))
                    continue;
                bestCoverage = coverage;
                bestCandidate = candidate;
            }
            if (bestCoverage >= 0)
            {
                position = bestCandidate;
                return true;
            }
        }

        // Geometria sem solução integral: nunca autoriza (0,0) global. O caller
        // conserva a última posição válida enquanto a geometria transitória muda.
        return false;
    }

    private bool TryProjectedTerritoryBounds(
        TerritorioClique territory, out Rect projectedBounds)
    {
        projectedBounds = default;
        if (territory == null || LayoutPartida.CameraRuntime == null)
            return false;
        Collider2D[] colliders = territory.GetComponents<Collider2D>();
        if (colliders.Length == 0)
            return false;
        Vector2 min = new(float.MaxValue, float.MaxValue);
        Vector2 max = new(float.MinValue, float.MinValue);
        bool projected = false;
        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;
            Bounds bounds = collider.bounds;
            foreach (Vector3 world in BoundsCorners(bounds))
            {
                if (!MapOverlayProjection.TryWorldToOverlay(content, world,
                        LayoutPartida.CameraRuntime, out Vector2 local))
                    continue;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                projected = true;
            }
        }
        if (!projected || max.x <= min.x || max.y <= min.y)
            return false;
        projectedBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private void CounterFootprintInOverlay(TerritorioClique territory,
        out Vector2 visualSize, out Vector2 touchSize)
    {
        visualSize = touchSize = Vector2.one * 54f;
        WDArmyCounterView counter = territory != null
            ? territory.GetComponentInChildren<WDArmyCounterView>(true) : null;
        SpriteRenderer body = counter?.BodyRenderer;
        if (body == null || body.sprite == null || counter.VisualRoot == null ||
            counter.VisualRoot.parent == null || LayoutPartida.CameraRuntime == null)
            return;

        Canvas canvas = content.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Transform counterTransform = counter.VisualRoot.parent;
        Vector3 canonicalCounterScale = CanonicalCounterScale(territory,
            counterTransform);
        Bounds spriteBounds = body.sprite.bounds;
        Vector3[] localCorners = BoundsCorners(spriteBounds);
        if (!TryProjectCanonicalCounterBounds(localCorners, body.transform,
                counter, counterTransform, canonicalCounterScale, canvasCamera,
                out visualSize))
            visualSize = Vector2.one * 54f;

        BoxCollider2D counterCollider = counterTransform.GetComponent<BoxCollider2D>();
        if (counterCollider == null)
        {
            touchSize = visualSize;
            return;
        }
        Bounds touchBounds = new(counterCollider.offset, counterCollider.size);
        if (!TryProjectCanonicalCounterBounds(BoundsCorners(touchBounds), null,
                counter, counterTransform, canonicalCounterScale, canvasCamera,
                out touchSize))
            touchSize = visualSize;
    }

    private bool TryProjectCanonicalCounterBounds(Vector3[] localCorners,
        Transform localTransform, WDArmyCounterView counter, Transform counterTransform,
        Vector3 canonicalCounterScale, Camera canvasCamera, out Vector2 size)
    {
        Vector2 min = new(float.MaxValue, float.MaxValue);
        Vector2 max = new(float.MinValue, float.MinValue);
        foreach (Vector3 spriteLocal in localCorners)
        {
            // O sprite está sob ArmyCounterSpriteRoot; o BoxCollider2D está no
            // próprio ContadorModerno. Cada footprint deve partir do seu espaço
            // local real antes de aplicar a escala estrutural canônica.
            Vector3 canonicalCounterLocal;
            if (localTransform == null)
            {
                canonicalCounterLocal = spriteLocal;
            }
            else
            {
                Vector3 bodyInVisualRoot = localTransform.localPosition +
                    localTransform.localRotation *
                    Vector3.Scale(localTransform.localScale, spriteLocal);
                canonicalCounterLocal = counter.VisualRoot.localPosition +
                    counter.VisualRoot.localRotation * bodyInVisualRoot;
            }
            // ContadorTropas anima o próprio ContadorModerno até 114% ao adicionar
            // tropas. A projeção usa a escala estrutural do mapa e mantém posição e
            // rotação canônicas, portanto o feedback visual não altera o placement.
            Vector3 canonicalInParent = counterTransform.localPosition +
                counterTransform.localRotation *
                Vector3.Scale(canonicalCounterScale, canonicalCounterLocal);
            Vector3 world = counterTransform.parent.TransformPoint(canonicalInParent);
            Vector2 screen = LayoutPartida.CameraRuntime.WorldToScreenPoint(world);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, canvasCamera, out Vector2 overlayLocal))
            {
                size = default;
                return false;
            }
            min = Vector2.Min(min, overlayLocal);
            max = Vector2.Max(max, overlayLocal);
        }
        size = max - min;
        return size.x > 0f && size.y > 0f;
    }

    private static Vector3[] BoundsCorners(Bounds bounds) => new[]
    {
        new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
        new Vector3(bounds.min.x, bounds.max.y, bounds.center.z),
        new Vector3(bounds.max.x, bounds.min.y, bounds.center.z),
        new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)
    };

    private static Vector3 CanonicalCounterScale(TerritorioClique territory,
        Transform counterTransform)
    {
        DefinicaoMapa map = MapaAtivo.Instance?.Definicao;
        DefinicaoTerritorioMapa definition = map != null && territory != null
            ? MapaAtivo.Instance.ObterDefinicaoTerritorio(territory.idTerritorio)
            : null;
        if (map == null || definition == null)
            return counterTransform.localScale;
        return TerritorioContador.ResolverEscalaBase(map) *
            definition.EscalaContadorEfetiva;
    }

    // ============================================================
    // 03.1. NORMALIZAÇÃO PELO CONTEÚDO VISÍVEL DO SÍMBOLO
    // ============================================================

    private static Vector2 VisibleContentCanvasRatio(Texture2D texture,
        Vector2 fallbackSize)
    {
        if (texture == null || !texture.isReadable)
            return fallbackSize / Mathf.Max(fallbackSize.x, fallbackSize.y);
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            if (pixels[y * texture.width + x].a <= 16) continue;
            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < minX || maxY < minY)
            return fallbackSize / Mathf.Max(fallbackSize.x, fallbackSize.y);
        float occupiedWidth = maxX - minX + 1f;
        float occupiedHeight = maxY - minY + 1f;
        float occupiedMax = Mathf.Max(occupiedWidth, occupiedHeight);
        return new Vector2(texture.width / occupiedMax, texture.height / occupiedMax);
    }

    private int TerritoryCoverage(TerritorioClique territory, Vector2 candidate,
        Vector2 visualSize, out bool centerInside)
    {
        centerInside = false;
        if (territory == null || LayoutPartida.CameraRuntime == null) return 0;
        Collider2D[] colliders = territory.GetComponents<Collider2D>();
        if (colliders.Length == 0) return 0;
        float insetX = Mathf.Max(0f, visualSize.x * .5f - 1f);
        float insetY = Mathf.Max(0f, visualSize.y * .5f - 1f);
        Vector2[] samples =
        {
            Vector2.zero, new(-insetX, -insetY), new(-insetX, insetY),
            new(insetX, -insetY), new(insetX, insetY),
            new(-insetX, 0f), new(insetX, 0f), new(0f, -insetY), new(0f, insetY)
        };
        Canvas canvas = content.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Plane territoryPlane = new(Vector3.forward, territory.transform.position);
        int inside = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvasCamera,
                content.TransformPoint(candidate + samples[i]));
            Ray ray = LayoutPartida.CameraRuntime.ScreenPointToRay(screen);
            if (!territoryPlane.Raycast(ray, out float distance)) continue;
            Vector3 world = ray.GetPoint(distance);
            if (!colliders.Any(collider => collider != null && collider.OverlapPoint(world)))
                continue;
            inside++;
            if (i == 0) centerInside = true;
        }
        return inside;
    }

    private bool IsFullyInsideTerritory(TerritorioClique territory,
        Vector2 candidate, Vector2 visualSize)
    {
        float halfX = Mathf.Max(0f, visualSize.x * .5f - 1f);
        float halfY = Mathf.Max(0f, visualSize.y * .5f - 1f);
        const int divisions = 4;
        for (int i = 0; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float x = Mathf.Lerp(-halfX, halfX, t);
            float y = Mathf.Lerp(-halfY, halfY, t);
            if (!TerritoryContainsOverlayPoint(territory, candidate + new Vector2(x, -halfY)) ||
                !TerritoryContainsOverlayPoint(territory, candidate + new Vector2(x, halfY)) ||
                !TerritoryContainsOverlayPoint(territory, candidate + new Vector2(-halfX, y)) ||
                !TerritoryContainsOverlayPoint(territory, candidate + new Vector2(halfX, y)))
                return false;
        }
        return TerritoryContainsOverlayPoint(territory, candidate);
    }

    private static Vector2 ExpandedTerritoryFitSize(Vector2 visualSize)
    {
        float clearance = Mathf.Min(visualSize.x, visualSize.y) *
            TerritoryBorderClearanceRatio * 2f;
        return visualSize + Vector2.one * clearance;
    }

    private bool TerritoryContainsOverlayPoint(TerritorioClique territory, Vector2 point)
    {
        if (territory == null || LayoutPartida.CameraRuntime == null) return false;
        Collider2D[] colliders = territory.GetComponents<Collider2D>();
        if (colliders.Length == 0) return false;
        Canvas canvas = content.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvasCamera,
            content.TransformPoint(point));
        Ray ray = LayoutPartida.CameraRuntime.ScreenPointToRay(screen);
        Plane territoryPlane = new(Vector3.forward, territory.transform.position);
        if (!territoryPlane.Raycast(ray, out float distance)) return false;
        Vector3 world = ray.GetPoint(distance);
        return colliders.Any(collider => collider != null && collider.OverlapPoint(world));
    }

    private static int PlacementContextHash(Vector2 counterAnchor,
        Vector2 counterVisualSize, Vector2 counterTouchSize, Vector2 badgeVisualSize,
        Rect territoryBounds, IReadOnlyList<Rect> placed,
        IReadOnlyList<Rect> placedTouches, IReadOnlyList<Rect> tactical)
    {
        unchecked
        {
            int hash = 17;
            void Add(float value) => hash = hash * 31 + Mathf.RoundToInt(value * 10f);
            void AddRect(Rect rect)
            {
                Add(rect.xMin); Add(rect.yMin); Add(rect.xMax); Add(rect.yMax);
            }
            Add(counterAnchor.x); Add(counterAnchor.y);
            Add(counterVisualSize.x); Add(counterVisualSize.y);
            Add(counterTouchSize.x); Add(counterTouchSize.y);
            Add(badgeVisualSize.x); Add(badgeVisualSize.y);
            AddRect(territoryBounds);
            foreach (Rect rect in placed) AddRect(rect);
            foreach (Rect rect in placedTouches) AddRect(rect);
            foreach (Rect rect in tactical) AddRect(rect);
            return hash;
        }
    }

    private List<Rect> GetTacticalExclusions()
    {
        var result = new List<Rect>();
        Canvas canvas = content.GetComponentInParent<Canvas>();
        if (canvas == null) return result;
        foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(false))
        {
            if (!rect.name.StartsWith("Tactical_", StringComparison.Ordinal)) continue;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = content.InverseTransformPoint(corners[0]);
            Vector2 max = content.InverseTransformPoint(corners[2]);
            result.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }
        return result;
    }

    private static Rect IconBounds(Vector2 center, Vector2 size) =>
        new(center - size * .5f, size);

    private static Rect Expand(Rect rect, float padding)
    {
        float safePadding = Mathf.Max(0f, padding);
        return new Rect(rect.xMin - safePadding, rect.yMin - safePadding,
            rect.width + safePadding * 2f, rect.height + safePadding * 2f);
    }

    // ============================================================
    // 05. FADE DO BADGE REAL, IDENTIFICADO PELA INSTALAÇÃO
    // ============================================================

    public void PrepareVisualState(IReadOnlyList<ResolutionVisualEvent> events)
    {
        RestoreTransportedPositions();
        pendingResolutionFades.Clear();
        presentedResolutionFades.Clear();
        RefreshVisibleInstallations();
        if (events != null)
            foreach (ResolutionVisualEvent visualEvent in events)
                if (visualEvent?.Type == ResolutionVisualEventType.Installation ||
                    visualEvent?.Type == ResolutionVisualEventType.Movement &&
                    !string.IsNullOrEmpty(visualEvent.InstallationId))
                {
                    if (!string.IsNullOrEmpty(visualEvent.OriginTerritoryId) &&
                        markers.TryGetValue(visualEvent.InstallationId, out Marker marker) &&
                        MapOverlayProjection.TryTerritoryToOverlay(
                            visualEvent.OriginTerritoryId, content,
                            out Vector2 originAnchor, out _) &&
                        MapOverlayProjection.TryTerritoryToOverlay(
                            visualEvent.DestinationTerritoryId, content,
                            out Vector2 destinationAnchor, out _))
                    {
                        transportedFinalPositions[marker.State.InstallationId] =
                            marker.Rect.anchoredPosition;
                        marker.Rect.anchoredPosition += originAnchor - destinationAnchor;
                        frozenTransportMarkers.Add(marker.State.InstallationId);
                        if (marker.VisualGroup != null) marker.VisualGroup.alpha = 1f;
                    }
                    else
                    {
                        pendingResolutionFades.Add(FadeKey(visualEvent.ContentId,
                            visualEvent.DestinationTerritoryId));
                    }
                }
        ApplyPendingFadeVisibility();
    }

    public void CompleteVisualState()
    {
        RestoreTransportedPositions();
        pendingResolutionFades.Clear();
        foreach (Marker marker in markers.Values)
            if (marker.VisualGroup != null) marker.VisualGroup.alpha = 1f;
    }

    public static bool TryBeginInstallationFade(string expectedInstallationId,
        string weaponId, string territoryId, out string installationId)
    {
        installationId = string.Empty;
        foreach (WDWeaponInstallationMapPresenter presenter in livePresenters)
        {
            if (presenter == null) continue;
            presenter.RefreshVisibleInstallations();
            Marker marker = presenter.markers.Values
                .Where(item => (string.IsNullOrEmpty(expectedInstallationId) ||
                    item.State.InstallationId == expectedInstallationId) &&
                    (string.IsNullOrEmpty(weaponId) || item.State.WeaponId == weaponId) &&
                    item.State.TerritoryId == territoryId)
                .OrderBy(item => item.State.InstallationId, StringComparer.Ordinal)
                .FirstOrDefault(item => !presenter.presentedResolutionFades.Contains(
                    item.State.InstallationId));
            if (marker == null) continue;
            installationId = marker.State.InstallationId;
            if (presenter.transportedFinalPositions.TryGetValue(installationId,
                    out Vector2 finalPosition))
            {
                marker.Rect.anchoredPosition = finalPosition;
                presenter.transportedFinalPositions.Remove(installationId);
            }
            presenter.frozenTransportMarkers.Remove(installationId);
            presenter.presentedResolutionFades.Add(installationId);
            presenter.pendingResolutionFades.Remove(FadeKey(weaponId, territoryId));
            if (marker.VisualGroup != null) marker.VisualGroup.alpha = 0f;
            return true;
        }
        return false;
    }

    private void RestoreTransportedPositions()
    {
        foreach (KeyValuePair<string, Vector2> item in transportedFinalPositions)
            if (markers.TryGetValue(item.Key, out Marker marker) && marker.Rect != null)
                marker.Rect.anchoredPosition = item.Value;
        transportedFinalPositions.Clear();
        frozenTransportMarkers.Clear();
    }

    public static void SetInstallationFadeAlpha(string installationId, float alpha)
    {
        foreach (WDWeaponInstallationMapPresenter presenter in livePresenters)
            if (presenter != null && presenter.markers.TryGetValue(installationId,
                    out Marker marker) && marker.VisualGroup != null)
                marker.VisualGroup.alpha = Mathf.Clamp01(alpha);
    }

    public static bool TryBeginTransportTravel(string installationId,
        RectTransform animationOverlay, out RectTransform travellingIcon,
        out Vector2 originPosition, out Vector2 destinationPosition)
    {
        travellingIcon = null;
        originPosition = destinationPosition = default;
        if (animationOverlay == null || string.IsNullOrEmpty(installationId))
            return false;
        foreach (WDWeaponInstallationMapPresenter presenter in livePresenters)
        {
            if (presenter == null || !presenter.markers.TryGetValue(installationId,
                    out Marker marker) || marker.Icon == null || marker.VisualGroup == null)
                continue;
            GameObject copy = Instantiate(marker.Icon.gameObject, animationOverlay, false);
            copy.name = "TransportedInstallationIcon";
            travellingIcon = copy.transform as RectTransform;
            travellingIcon.anchorMin = travellingIcon.anchorMax =
                travellingIcon.pivot = new Vector2(.5f, .5f);
            originPosition = animationOverlay.InverseTransformPoint(marker.Icon.position);
            destinationPosition = presenter.transportedFinalPositions.TryGetValue(
                    installationId, out Vector2 finalLocal)
                ? animationOverlay.InverseTransformPoint(
                    presenter.content.TransformPoint(finalLocal))
                : originPosition;
            travellingIcon.anchoredPosition = originPosition;
            marker.VisualGroup.alpha = 0f;
            return true;
        }
        return false;
    }

    private void ApplyPendingFadeVisibility()
    {
        foreach (Marker marker in markers.Values)
            if (marker.VisualGroup != null && pendingResolutionFades.Contains(
                    FadeKey(marker.State.WeaponId, marker.State.TerritoryId)) &&
                !presentedResolutionFades.Contains(marker.State.InstallationId))
                marker.VisualGroup.alpha = 0f;
    }

    private static string FadeKey(string weaponId, string territoryId) =>
        (weaponId ?? string.Empty) + "\u001f" + (territoryId ?? string.Empty);

    public static Vector2 TerritoryIconSizeFor(string weaponId) => weaponId switch
    {
        WDStrategicWeaponIds.Artillery => new Vector2(36f, 18.81f),
        WDStrategicWeaponIds.Rockets => new Vector2(29.66f, 29.66f),
        WDSupportWeaponIds.Bunker => new Vector2(30f, 24f),
        WDSpecialWeaponIds.Drone => new Vector2(30f, 28.5f),
        WDSupportWeaponIds.Minefield => new Vector2(28.13f, 32.51f),
        WDSupportWeaponIds.FlakTower => new Vector2(25.2f, 30.15f),
        WDStrategicWeaponIds.EnergyPlant => new Vector2(30f, 30f),
        _ => new Vector2(30f, 30f)
    };

    public static bool TryGetMarkerPosition(string installationId,
        RectTransform target, out Vector2 position)
    {
        position = default;
        if (target == null || string.IsNullOrEmpty(installationId)) return false;
        foreach (WDWeaponInstallationMapPresenter presenter in livePresenters)
            if (presenter != null && presenter.markers.TryGetValue(installationId,
                    out Marker marker) && marker.Rect != null &&
                marker.Rect.gameObject.activeInHierarchy)
            {
                position = target.InverseTransformPoint(
                    marker.Rect.TransformPoint(marker.Rect.rect.center));
                return true;
            }
        return false;
    }

    public static string AssetNameFor(string weaponId) => weaponId switch
    {
        WDSupportWeaponIds.Bunker => "Bunker",
        WDSupportWeaponIds.Minefield => "Minefield",
        WDSupportWeaponIds.FlakTower => "FlakTower",
        WDStrategicWeaponIds.EnergyPlant => "PowerPlant",
        WDStrategicWeaponIds.Artillery => "Artillery",
        WDStrategicWeaponIds.Rockets => "Rockets",
        WDSpecialWeaponIds.Drone => "Drone",
        _ => string.Empty
    };

    private Sprite LoadSprite(string asset, Texture2D texture)
    {
        if (sprites.TryGetValue(asset, out Sprite sprite)) return sprite;
        if (texture == null) return null;
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f);
        sprite.name = "InstallationMarker-" + asset;
        sprite.hideFlags = HideFlags.DontSave;
        sprites.Add(asset, sprite);
        return sprite;
    }

    public static bool ConsumesMapPointer(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = position }, hits);
        foreach (RaycastResult hit in hits)
            foreach (WDWeaponInstallationMapPresenter presenter in livePresenters)
                if (presenter != null && presenter.content != null &&
                    hit.gameObject.transform.IsChildOf(presenter.content))
                    return true;
        return false;
    }

    private void OnDestroy()
    {
        livePresenters.Remove(this);
        foreach (Sprite sprite in sprites.Values)
            if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
    }
}

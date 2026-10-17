using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
internal sealed class WDInstallationHubOutsidePointer : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private Action dismiss;
    private bool suppressOpeningGesture;

    public void Configure(Action callback) => dismiss = callback;
    public void SuppressOpeningGesture() => suppressOpeningGesture = true;

    public void OnPointerDown(PointerEventData eventData)
    {
        // Se o backdrop foi ativado depois do down que abriu o Hub, não recebeu
        // esse down. O próximo down é necessariamente um gesto posterior e arma
        // novamente o fechamento normal por click-outside.
        suppressOpeningGesture = false;
    }

    public void OnPointerUp(PointerEventData eventData) => eventData?.Use();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!suppressOpeningGesture)
            dismiss?.Invoke();
        suppressOpeningGesture = false;
        eventData?.Use();
    }
}

public readonly struct WDInstallationHubPresentation
{
    public string Title { get; }
    public string Subtitle { get; }
    public string[] Sectors { get; }
    public string AssetName { get; }
    public bool IsOperational { get; }
    public bool CanOperate { get; }

    public WDInstallationHubPresentation(string title, string subtitle,
        string assetName, bool isOperational, bool canOperate, params string[] sectors)
    {
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        AssetName = assetName ?? string.Empty;
        IsOperational = isOperational;
        CanOperate = canOperate;
        Sectors = sectors ?? Array.Empty<string>();
    }
}

public static class WDInstallationHubFormatter
{
    // ============================================================
    // 01. DTO VISUAL DERIVADO EXCLUSIVAMENTE DO SNAPSHOT CANÔNICO
    // ============================================================

    public static WDInstallationHubPresentation Format(WDWeaponHudInstallationState item)
    {
        if (item == null) return default;
        string state = item.IsDisabled ? "DESABILITADO" :
            item.IsAbandoned ? "ABANDONADO" : "OPERACIONAL";
        int total = Math.Max(item.InitialCharges, item.Charges);

        return item.WeaponId switch
        {
            WDSupportWeaponIds.Bunker => new("BUNKER", state, "Bunker", false, false,
                $"BARREIRA\n{item.Barrier} RESTANTE", "PROTEÇÃO\nAUTOMÁTICA", $"ESTADO\n{state}"),
            WDSupportWeaponIds.Minefield => new("CAMPO MINADO",
                item.IsDisabled ? "DESABILITADO" : "ARMADO", "Minefield", false, false,
                $"DANO\n{WDTacticalWeaponTables.MineDamage(item.Level)}", "ATIVAÇÕES\n1",
                $"ESTADO\n{(item.IsDisabled ? "DESABILITADO" : "ARMADO")}"),
            WDSupportWeaponIds.FlakTower => new("FLAK TOWER", state, "FlakTower", false, false,
                $"DURAÇÃO\n{item.Duration} ROUNDS", "INTERCEPTAÇÃO\nAUTOMÁTICA", $"ESTADO\n{state}"),
            WDStrategicWeaponIds.EnergyPlant => new("CENTRAL DE\nENERGIA", state,
                "PowerPlant", false, false,
                $"ENERGIA\n/ ROUND\n+{WDStrategicWeaponTables.EnergyProduction(item.Level)}",
                $"PAGAMENTOS\n{item.PaymentsRemaining} RESTANTES",
                item.PaymentsRemaining > 0
                    ? $"PRÓXIMO\nROUND {NextEnergyRound(item)}"
                    : "PAGAMENTOS\nCONCLUÍDOS"),
            WDStrategicWeaponIds.Artillery => new("ARTILHARIA", "AUTOMÁTICA",
                "Artillery", false, false, $"MUNIÇÃO\n{item.Charges} / {total}",
                "DISPAROS\n/ ROUND\nATÉ 3", "MODO\nAUTOMÁTICO"),
            WDStrategicWeaponIds.Rockets => new("FOGUETES", "DISPARAR", "Rockets",
                true, item.CanOperate, $"DISPAROS\n{item.Charges} / {Math.Max(2, total)}",
                "ALCANCE\n3", $"ESTADO\n{state}"),
            WDSpecialWeaponIds.Drone => new("DRONE", item.CanOperate ? "DISPARAR" : "INDISPONÍVEL",
                "Drone", true, item.CanOperate,
                $"DISPAROS\n{item.Charges} / {Math.Max(2, total)}", "LIMITE\n1 / ROUND",
                "ALCANCE\nGLOBAL"),
            _ => default
        };
    }

    public static bool IsCanonicalInstallation(string weaponId) =>
        weaponId == WDSupportWeaponIds.Bunker ||
        weaponId == WDSupportWeaponIds.Minefield ||
        weaponId == WDSupportWeaponIds.FlakTower ||
        weaponId == WDStrategicWeaponIds.EnergyPlant ||
        weaponId == WDStrategicWeaponIds.Artillery ||
        weaponId == WDStrategicWeaponIds.Rockets ||
        weaponId == WDSpecialWeaponIds.Drone;

    private static int NextEnergyRound(WDWeaponHudInstallationState item) =>
        Math.Max(item.CurrentRound,
            Math.Max(item.InstalledRound + 1, item.LastOperationRound + 1));
}

internal readonly struct WDInstallationHubVisualConfig
{
    public readonly Rect TopInfo, MiddleInfo, BottomInfo;
    public readonly Rect Icon, Title, Subtitle;
    public readonly string BackgroundAsset;
    public readonly Color SubtitleColor;
    public readonly float TitleSize;

    public WDInstallationHubVisualConfig(Rect top, Rect middle, Rect bottom,
        Rect icon, Rect title, Rect subtitle, string backgroundAsset,
        Color subtitleColor, float titleSize = 14f)
    {
        TopInfo = top; MiddleInfo = middle; BottomInfo = bottom;
        Icon = icon; Title = title; Subtitle = subtitle;
        BackgroundAsset = backgroundAsset; SubtitleColor = subtitleColor;
        TitleSize = titleSize;
    }
}

internal static class WDInstallationHubVisualCatalog
{
    private static readonly Color Green = new Color32(120, 201, 154, 255);
    private static readonly Color Cyan = new Color32(85, 200, 223, 255);
    private static readonly Color Gold = new Color32(209, 164, 95, 255);

    // ============================================================
    // 02. SETE CONFIGURAÇÕES DIRETAS DO FRAME FIGMA 2495:339
    // ============================================================

    public static WDInstallationHubVisualConfig Resolve(string weaponId) => weaponId switch
    {
        WDStrategicWeaponIds.Artillery => Config(
            new Rect(61f, 80.3f, 113f, 54f), new Rect(45f, 183.3f, 131f, 55f),
            new Rect(61f, 287.3f, 113f, 54f), new Rect(321f, 157.3f, 24f, 24f),
            new Rect(270f, 183.3f, 126f, 38f), new Rect(272f, 224.3f, 122f, 22f),
            "Artillery", Green),
        WDSpecialWeaponIds.Drone => Config(
            new Rect(64f, 80.3f, 103f, 56f), new Rect(50f, 185.3f, 119f, 58f),
            new Rect(64f, 292.3f, 103f, 56f), new Rect(320f, 166.7f, 24f, 24f),
            new Rect(269f, 192.7f, 126f, 38f), new Rect(272f, 231.3f, 122f, 22f),
            "Drone", Cyan),
        WDStrategicWeaponIds.EnergyPlant => Config(
            new Rect(63f, 84.3f, 115f, 53f), new Rect(48f, 184.3f, 133f, 53f),
            new Rect(63f, 284.3f, 115f, 53f), new Rect(321f, 163.3f, 24f, 24f),
            new Rect(270f, 189.3f, 126f, 38f), new Rect(272f, 230.3f, 122f, 22f),
            "PowerPlant", Gold, 12.5f),
        WDSupportWeaponIds.Bunker => Config(
            new Rect(48f, 85.3f, 149f, 53f), new Rect(29f, 185.3f, 171f, 53f),
            new Rect(48f, 285.3f, 149f, 53f), new Rect(318f, 164.3f, 28f, 24f),
            new Rect(260f, 190.3f, 145f, 38f), new Rect(263f, 231.3f, 140f, 22f),
            "Bunker", Green),
        WDSupportWeaponIds.Minefield => Config(
            new Rect(60f, 85.3f, 130f, 54f), new Rect(42f, 188.3f, 151f, 55f),
            new Rect(60f, 292.3f, 130f, 54f), new Rect(322f, 166.3f, 24f, 24f),
            new Rect(271f, 192.3f, 126f, 38f), new Rect(273f, 233.3f, 122f, 22f),
            "Minefield", Green, 12.5f),
        WDStrategicWeaponIds.Rockets => Config(
            new Rect(62f, 80.3f, 124f, 53f), new Rect(44f, 181.3f, 144f, 55f),
            new Rect(62f, 284.3f, 124f, 53f), new Rect(321f, 164.3f, 24f, 24f),
            new Rect(270f, 190.3f, 126f, 38f), new Rect(272f, 231.3f, 122f, 22f),
            "Rockets", Cyan),
        WDSupportWeaponIds.FlakTower => Config(
            new Rect(78f, 83.3f, 96f, 53f), new Rect(65f, 185.3f, 111f, 53f),
            new Rect(78f, 287.3f, 96f, 53f), new Rect(321f, 162.3f, 24f, 24f),
            new Rect(270f, 188.3f, 126f, 38f), new Rect(272f, 229.3f, 122f, 22f),
            "FlakTower", Green),
        _ => default
    };

    private static WDInstallationHubVisualConfig Config(Rect top, Rect middle,
        Rect bottom, Rect icon, Rect title, Rect subtitle, string center,
        Color subtitleColor, float titleSize = 14f) =>
        new(top, middle, bottom, icon, title, subtitle, center, subtitleColor, titleSize);
}

// Figma 189:12 / instances 190:424..430. A View exibe snapshot e encaminha
// somente a intenção DISPARAR para o controller canônico.
public sealed class WDMatchInstallationHubView : MonoBehaviour
{
    private const float CompositionWidth = 448f;
    private const float HubWidth = 480f;
    private const float CenterGap = 12f;
    private const float CenterIconScale = 1.5f;
    private static readonly Rect ApprovedCenterCircle = new(220f, 94.9f, 227f, 227f);
    private const string ArtworkRoot = "UI/Match/FigmaCurrent/InstallationHubCompositions/";
    private const string IconRoot = "UI/Match/FigmaCurrent/InstallationHubs/";
    private static readonly Color Cyan = new Color32(85, 200, 223, 255);
    private static readonly Color White = new Color32(244, 248, 250, 255);
    private static readonly Color Muted = new Color32(184, 199, 209, 255);
    private static readonly Color Green = new Color32(120, 201, 154, 255);
    private static readonly List<WDMatchInstallationHubView> liveViews = new();
    private static WDMatchInstallationHubView consumedBy;
    private static int consumedFrame = -1;

    private WarDominionUITheme theme;
    private WDWeaponMatchUIController weapons;
    private RectTransform outside, hub, composition;
    private WDInstallationHubOutsidePointer outsidePointer;
    private Image icon, artwork, center;
    private TextMeshProUGUI title, subtitle;
    private readonly TextMeshProUGUI[] sectors = new TextMeshProUGUI[3];
    private readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);
    private Button action;
    private WDWeaponHudInstallationState current;
    private bool mirrored;
    private readonly Vector3[] canvasCorners = new Vector3[4];
    public bool IsOpen => hub != null && hub.gameObject.activeSelf;
    public event Action IntentChanged;

    // ============================================================
    // 02. CONSTRUÇÃO RADIAL 333.9 × 414.2, SEM ESTADO DE GAMEPLAY
    // ============================================================

    public void Build(WarDominionUITheme value)
    {
        theme = value;
        liveViews.Add(this);
        outside = Rect("InstallationHubOutside", transform, 0, 0, 0, 0);
        outside.anchorMin = Vector2.zero; outside.anchorMax = Vector2.one;
        outside.offsetMin = outside.offsetMax = Vector2.zero;
        Image outsideHit = outside.gameObject.AddComponent<Image>();
        outsideHit.color = Color.clear;
        // ============================================================
        // 02.1 BLOQUEADOR UI COMPLETO, SEM SUBSTITUIR O GESTO PROTEGIDO
        // ============================================================
        // O Button mantém o contrato de captura do modal e o componente abaixo
        // continua responsável por ignorar somente o gesto que abriu o Hub.
        Button outsideGuard = outside.gameObject.AddComponent<Button>();
        outsideGuard.targetGraphic = outsideHit;
        outsideGuard.transition = Selectable.Transition.None;
        outsidePointer = outside.gameObject.AddComponent<WDInstallationHubOutsidePointer>();
        outsidePointer.Configure(Close);

        hub = Rect("InstallationHubRoot", transform, 0, 0, HubWidth, 413f);
        hub.anchorMin = hub.anchorMax = hub.pivot = new Vector2(.5f, .5f);
        hub.anchoredPosition = Vector2.zero;
        hub.localScale = Vector3.one;
        hub.localRotation = Quaternion.identity;
        Image hubHit = hub.gameObject.AddComponent<Image>();
        hubHit.color = Color.clear;
        hubHit.raycastTarget = true;
        Button hubGuard = hub.gameObject.AddComponent<Button>();
        hubGuard.transition = Selectable.Transition.None;
        hubGuard.onClick.AddListener(ConsumeThisFrame);
        BuildLocalPresentation();
        action = center.gameObject.AddComponent<Button>();
        center.raycastTarget = true;
        action.transition = Selectable.Transition.None;
        action.onClick.AddListener(Operate);
        SetOpen(false);
    }

    // ============================================================
    // 03. ARTE RADIAL ESTÁTICA 1:1 + OVERLAYS DINÂMICOS
    // ============================================================

    private void BuildLocalPresentation()
    {
        composition = Rect("ApprovedHubComposition", hub, 16f, 0f,
            CompositionWidth, 413f);
        artwork = AddArtwork("HubArtwork", composition, 0f, 0f,
            CompositionWidth, 413f);

        // Superfície transparente independente: a geometria visual do centro
        // pertence ao PNG único; somente o target interativo permanece em Unity.
        // Centro real da circunferência no frame 2495:339. Não corresponde ao
        // centro do retângulo total 448 x 413.
        RectTransform centerRect = Rect("CenterInfo", composition,
            ApprovedCenterCircle.x, ApprovedCenterCircle.y,
            ApprovedCenterCircle.width, ApprovedCenterCircle.height);
        center = centerRect.gameObject.AddComponent<Image>();
        center.color = Color.clear;
        center.raycastTarget = true;

        sectors[0] = BuildInfoGroup("SectorTop", 0f, 0f, 1f, 1f, Muted);
        sectors[1] = BuildInfoGroup("SectorMiddle", 0f, 0f, 1f, 1f, White);
        sectors[2] = BuildInfoGroup("SectorBottom", 0f, 0f, 1f, 1f, Muted);
        icon = AddIcon("InstallationIcon", composition, 0f, 0f, 24f, 24f);
        title = Label(composition, "Title", 0f, 0f, 1f, 1f, 14f, White);
        subtitle = Label(composition, "Subtitle", 0f, 0f, 1f, 1f, 11f, Green);
    }

    private TextMeshProUGUI BuildInfoGroup(string name, float x, float y,
        float width, float height, Color color)
    {
        RectTransform group = Rect(name, composition, x, y, width, height);
        return Label(group, "Value", 0f, 0f, width, height, 10.5f, color);
    }

    public void Bind(WDWeaponMatchUIController controller) => weapons = controller;

    public void Show(WDWeaponHudInstallationState installation)
    {
        if (installation == null || !WDInstallationHubFormatter.IsCanonicalInstallation(
            installation.WeaponId)) return;
        current = installation;
        Refresh();
        if (!PositionAtTerritory())
        {
            Debug.LogError("[InstallationHub] Não foi possível projetar a instalação " +
                $"'{installation.InstallationId}' no território " +
                $"'{installation.TerritoryId}'.", this);
            current = null;
            SetOpen(false);
            return;
        }
        outsidePointer?.SuppressOpeningGesture();
        SetOpen(true);
        ConsumeThisFrame();
    }

    public void Close()
    {
        // O cleanup de mudança de estado chama Close mesmo quando o Hub já está
        // fechado. Somente um Hub visível pode ter capturado o gesto corrente.
        if (IsOpen) ConsumeThisFrame();
        current = null;
        SetOpen(false);
    }

    private void Operate()
    {
        ConsumeThisFrame();
        if (current?.CanOperate != true || weapons?.SelectInstallation(current.InstallationId) != true)
            return;
        SetOpen(false);
        IntentChanged?.Invoke();
    }

    private void LateUpdate()
    {
        if (!IsOpen || current == null) return;
        WDWeaponHudInstallationState refreshed = FindCurrentSnapshot();
        if (refreshed == null) { Close(); return; }
        current = refreshed;
        Refresh();
        if (!PositionAtTerritory()) Close();
    }

    private WDWeaponHudInstallationState FindCurrentSnapshot()
    {
        foreach (WDWeaponHudInstallationState item in weapons?.GetVisibleInstallations() ??
                 Array.Empty<WDWeaponHudInstallationState>())
            if (item.InstallationId == current.InstallationId) return item;
        return null;
    }

    private void Refresh()
    {
        WDInstallationHubPresentation model = WDInstallationHubFormatter.Format(current);
        WDInstallationHubVisualConfig visual =
            WDInstallationHubVisualCatalog.Resolve(current.WeaponId);
        title.text = model.Title;
        subtitle.text = model.Subtitle;
        subtitle.color = visual.SubtitleColor;
        for (int i = 0; i < sectors.Length; i++)
            sectors[i].text = i < model.Sectors.Length ? model.Sectors[i] : string.Empty;
        icon.sprite = LoadSprite(IconRoot, model.AssetName);
        icon.color = Color.white;
        artwork.sprite = LoadSprite(ArtworkRoot, visual.BackgroundAsset);
        ApplyPresentationRects(visual);
        title.fontSize = visual.TitleSize;
        action.interactable = model.CanOperate;
    }

    // ============================================================
    // 03.1. ORIENTAÇÃO CONTROLADA: ARTE ESPELHADA, CONTEÚDO LEGÍVEL
    // ============================================================

    private void ApplyPresentationRects(WDInstallationHubVisualConfig visual)
    {
        artwork.rectTransform.localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
        ApplyRect(center.rectTransform, ApprovedCenterCircle, mirrored);
        ApplyRect(sectors[0].rectTransform.parent as RectTransform, visual.TopInfo, mirrored);
        ApplyRect(sectors[1].rectTransform.parent as RectTransform, visual.MiddleInfo, mirrored);
        ApplyRect(sectors[2].rectTransform.parent as RectTransform, visual.BottomInfo, mirrored);
        ApplyRect(icon.rectTransform, ScaleAroundCenter(visual.Icon, CenterIconScale), mirrored);
        ApplyRect(title.rectTransform, visual.Title, mirrored);
        ApplyRect(subtitle.rectTransform, visual.Subtitle, mirrored);
    }

    private static Rect ScaleAroundCenter(Rect value, float scale)
    {
        Vector2 size = value.size * scale;
        return new Rect(value.center - size * .5f, size);
    }

    private static void ApplyRect(RectTransform target, Rect value, bool mirror = false)
    {
        if (target == null) return;
        if (mirror) value.x = CompositionWidth - value.xMax;
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0f, 1f);
        target.anchoredPosition = new Vector2(value.x, -value.y);
        target.sizeDelta = value.size;
        if (target.childCount != 1) return;
        RectTransform child = target.GetChild(0) as RectTransform;
        if (child == null) return;
        child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one;
        child.offsetMin = child.offsetMax = Vector2.zero;
    }

    // ============================================================
    // 03. POSIÇÃO RESPONSIVA E BLOQUEIO DO MESMO EVENTO
    // ============================================================

    private bool PositionAtTerritory()
    {
        if (current == null || !MapOverlayProjection.TryTerritoryToOverlay(
                current.TerritoryId, transform as RectTransform,
                out Vector2 point, out _)) return false;
        RectTransform root = transform as RectTransform;
        if (WDWeaponInstallationMapPresenter.TryGetMarkerPosition(
                current.InstallationId, root, out Vector2 markerPoint))
            point = markerPoint;
        UnityEngine.Rect bounds = root.rect;
        UnityEngine.Rect leftHud = LocalBounds(root, root.parent.Find("LeftCommandRail") as RectTransform);
        UnityEngine.Rect rightHud = LocalBounds(root, root.parent.Find("PlayersContrast") as RectTransform);
        Vector2 desired = ChooseOrientedPosition(point, hub.sizeDelta, bounds,
            leftHud, rightHud, out bool shouldMirror);
        if (mirrored != shouldMirror)
        {
            mirrored = shouldMirror;
            ApplyPresentationRects(WDInstallationHubVisualCatalog.Resolve(current.WeaponId));
        }
        hub.localScale = Vector3.one;
        hub.localRotation = Quaternion.identity;
        hub.anchoredPosition = desired;
        return true;
    }

    private static Vector2 ChooseOrientedPosition(Vector2 badge, Vector2 size,
        UnityEngine.Rect safeBounds, UnityEngine.Rect leftHud,
        UnityEngine.Rect rightHud, out bool mirror)
    {
        float halfW = size.x * .5f;
        float halfH = size.y * .5f;
        float circleRadius = ApprovedCenterCircle.width * .5f;
        float originalCircleOffset = 16f + ApprovedCenterCircle.center.x - halfW;
        var candidates = new List<(Vector2 position, bool mirrored)>();
        AddOrientedCandidates(false);
        AddOrientedCandidates(true);

        Vector2 best = default;
        mirror = false;
        float bestPenalty = float.MaxValue;
        foreach ((Vector2 requested, bool candidateMirror) in candidates)
        {
            Vector2 clamped = new(
                Mathf.Clamp(requested.x, safeBounds.xMin + halfW + 12f,
                    safeBounds.xMax - halfW - 12f),
                Mathf.Clamp(requested.y, safeBounds.yMin + halfH + 12f,
                    safeBounds.yMax - halfH - 12f));
            UnityEngine.Rect placed = new(clamped - size * .5f, size);
            float overlap = OverlapArea(placed, leftHud) + OverlapArea(placed, rightHud);
            float localCircleX = candidateMirror ? -originalCircleOffset : originalCircleOffset;
            Vector2 circle = clamped + new Vector2(localCircleX, 0f);
            float expectedCircleX = badge.x + (candidateMirror ? 1f : -1f) *
                (circleRadius + CenterGap);
            float attachmentError = Vector2.SqrMagnitude(circle -
                new Vector2(expectedCircleX, badge.y));
            float penalty = (overlap > 0f ? 1000000000f + overlap : 0f) +
                attachmentError;
            if (penalty >= bestPenalty) continue;
            bestPenalty = penalty;
            best = clamped;
            mirror = candidateMirror;
        }
        return best;

        void AddOrientedCandidates(bool candidateMirror)
        {
            float localCircleX = candidateMirror ? -originalCircleOffset : originalCircleOffset;
            float circleX = badge.x + (candidateMirror ? 1f : -1f) *
                (circleRadius + CenterGap);
            float x = circleX - localCircleX;
            candidates.Add((new Vector2(x, badge.y), candidateMirror));
            foreach (UnityEngine.Rect exclusion in new[] { leftHud, rightHud })
            {
                if (exclusion.width <= 0f || exclusion.height <= 0f) continue;
                candidates.Add((new Vector2(x, exclusion.yMax + halfH + CenterGap),
                    candidateMirror));
                candidates.Add((new Vector2(x, exclusion.yMin - halfH - CenterGap),
                    candidateMirror));
            }
        }
    }

    public static Vector2 ChooseContextualPosition(Vector2 anchor, Vector2 size,
        UnityEngine.Rect safeBounds, UnityEngine.Rect leftHud,
        UnityEngine.Rect rightHud, float gap)
    {
        float halfW = size.x * .5f;
        float halfH = size.y * .5f;
        bool preferRight = anchor.x <= safeBounds.center.x;
        var candidates = new List<Vector2>
        {
            anchor + new Vector2((preferRight ? 1f : -1f) * (halfW + gap), 0f),
            anchor + new Vector2((preferRight ? -1f : 1f) * (halfW + gap), 0f)
        };
        AddExclusionEdgeCandidates(candidates, anchor, leftHud, halfW, halfH, gap);
        AddExclusionEdgeCandidates(candidates, anchor, rightHud, halfW, halfH, gap);
        Vector2 best = default;
        float bestPenalty = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 candidate = candidates[i];
            Vector2 clamped = new(
                Mathf.Clamp(candidate.x, safeBounds.xMin + halfW + 12f,
                    safeBounds.xMax - halfW - 12f),
                Mathf.Clamp(candidate.y, safeBounds.yMin + halfH + 12f,
                    safeBounds.yMax - halfH - 12f));
            UnityEngine.Rect placed = new(clamped - size * .5f, size);
            float overlap = OverlapArea(placed, leftHud) + OverlapArea(placed, rightHud);
            // Havendo uma posição livre, proximidade nunca pode vencer a exclusão do HUD.
            float markerOverlap = OverlapArea(placed,
                new UnityEngine.Rect(anchor - Vector2.one * 22f, Vector2.one * 44f));
            float penalty = overlap > 0f || markerOverlap > 0f
                ? 1000000f + overlap + markerOverlap
                : i * 10000f + Vector2.SqrMagnitude(clamped - candidate);
            if (penalty >= bestPenalty) continue;
            bestPenalty = penalty;
            best = clamped;
        }
        return best;
    }

    private static void AddExclusionEdgeCandidates(List<Vector2> candidates,
        Vector2 anchor, UnityEngine.Rect exclusion, float halfW, float halfH, float gap)
    {
        if (exclusion.width <= 0f || exclusion.height <= 0f) return;
        candidates.Add(new Vector2(exclusion.xMax + halfW + gap, anchor.y));
        candidates.Add(new Vector2(exclusion.xMin - halfW - gap, anchor.y));
        candidates.Add(new Vector2(anchor.x, exclusion.yMax + halfH + gap));
        candidates.Add(new Vector2(anchor.x, exclusion.yMin - halfH - gap));
    }

    private static UnityEngine.Rect LocalBounds(RectTransform root, RectTransform target)
    {
        if (root == null || target == null || !target.gameObject.activeInHierarchy)
            return new UnityEngine.Rect(float.MaxValue, float.MaxValue, 0f, 0f);
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = root.InverseTransformPoint(corners[0]);
        Vector2 max = root.InverseTransformPoint(corners[2]);
        return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static float OverlapArea(UnityEngine.Rect a, UnityEngine.Rect b)
    {
        float width = Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
        float height = Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
        return width * height;
    }

    private void SetOpen(bool open)
    {
        hub.gameObject.SetActive(open);
        outside.gameObject.SetActive(open);
        if (open) FitOutsideToCanvas();
    }

    private void FitOutsideToCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(canvasCorners);
        RectTransform parent = (RectTransform)transform;
        Vector3 min = parent.InverseTransformPoint(canvasCorners[0]);
        Vector3 max = parent.InverseTransformPoint(canvasCorners[2]);
        outside.offsetMin = (Vector2)min - parent.rect.min;
        outside.offsetMax = (Vector2)max - parent.rect.max;
    }

    private void ConsumeThisFrame() { consumedBy = this; consumedFrame = Time.frameCount; }

    public static bool ConsumesMapPointer(Vector2 position)
    {
        if (consumedBy != null && consumedFrame == Time.frameCount) return true;
        if (EventSystem.current == null) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = position }, hits);
        foreach (RaycastResult hit in hits)
            foreach (WDMatchInstallationHubView view in liveViews)
                if (view != null && view.IsOpen &&
                    hit.gameObject.GetComponentInParent<WDMatchInstallationHubView>() == view)
                    return true;
        return false;
    }

    private static RectTransform Rect(string name, Transform parent,
        float x, float y, float width, float height)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private Image AddArtwork(string name, Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
        Image image = rect.gameObject.AddComponent<Image>();
        // Alpha, glow, proporções e transforms já estão incorporados na arte.
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return image;
    }

    private Image AddIcon(string name, Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Sprite LoadSprite(string root, string asset)
    {
        string key = root + asset;
        if (sprites.TryGetValue(key, out Sprite sprite)) return sprite;
        Texture2D texture = Resources.Load<Texture2D>(key);
        if (texture == null) return null;
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f);
        sprite.name = "InstallationHub-" + asset;
        sprite.hideFlags = HideFlags.DontSave;
        sprites.Add(key, sprite);
        return sprite;
    }

    private TextMeshProUGUI Label(Transform parent, string name, float x, float y,
        float width, float height, float size, Color color)
    {
        TextMeshProUGUI label = WDUIFactory.Text(name, parent, theme, size, color,
            TextAlignmentOptions.Center);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        label.font = WDHomeTypography.Load("Gabarito-SemiBold") ?? theme.FonteInterface;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private void OnDestroy()
    {
        liveViews.Remove(this);
        if (consumedBy == this) { consumedBy = null; consumedFrame = -1; }
        foreach (Sprite sprite in sprites.Values)
            if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
    }
}

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
    public string SenderPlayerId { get; }
    public string TargetPlayerId { get; }
    public int Round { get; }
    public GameManager.FaseTurno Phase { get; }
    public string MatchId { get; }
    // 00.1. Âncora no espaço local do território identificado por TerritoryId.
    // Ausência mantém compatibilidade com eventos/programação anteriores, sem gesto.
    public Vector3? TerritoryLocalAnchor { get; }

    public WDTacticalPing(WDTacticalPingKind kind, string territoryId,
        TerritorioClique.Dono author, TerritorioClique.Dono recipient,
        string senderPlayerId = "", string targetPlayerId = "", int round = 0,
        GameManager.FaseTurno phase = GameManager.FaseTurno.Preparacao, string matchId = "", Vector3? territoryLocalAnchor = null)
    {
        Kind = kind;
        TerritoryId = territoryId ?? string.Empty;
        Author = author;
        Recipient = recipient;
        SenderPlayerId = senderPlayerId; TargetPlayerId = targetPlayerId;
        Round = round; Phase = phase; MatchId = matchId;
        TerritoryLocalAnchor = territoryLocalAnchor;
    }
}

[DisallowMultipleComponent]
public sealed class WDMatchInformationalOverlayController : MonoBehaviour
{
    private RectTransform overlay;
    private RectTransform regionInfoOverlay;
    private WarDominionUITheme theme;
    private MatchUIState state;
    private WDTacticalPingKind? pendingKind;
    private TerritorioClique.Dono pendingRecipient;
    private bool regionsActive;
    private readonly List<WDTacticalPing> pings = new();
    public const float TacticalSignalDuration = 4f;
    private readonly List<float> pingExpiry = new();
    private readonly List<TacticalMarker> markers = new();
    private TMP_FontAsset markerFont;
    private string selectionMatchId;
    private string presentedMatchId;
    private sealed class TacticalMarker
    {
        public RectTransform Root;
        public TerritorioClique Territory;
        public CanvasGroup Group;
        public float Expires;
        public Vector3? LocalAnchor;
        public readonly List<Sprite> Sprites = new();
        public Material TextMaterial;
    }
    private readonly Dictionary<GameObject, bool> counterVisibility = new();

    public bool RegionsActive => regionsActive;
    public bool HasPendingTacticalIntent => pendingKind.HasValue;
    public IReadOnlyList<WDTacticalPing> Pings => pings;
    public event Action<WDTacticalPing> TacticalPingRequested;
    public event Action<WDTacticalPing> TacticalPingPresented;
    public static string DescribeTacticalSignal(WDTacticalPingKind kind) => WDTacticalVisualStyle.Label(kind);
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
        CancelTacticalSelection();
        pings.Clear(); pingExpiry.Clear(); RebuildPingMarkers();
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
        bool contextChanged = state != null && (newState == null ||
            newState.Fase != state.Fase || newState.Round != state.Round ||
            newState.JogadorLocal != state.JogadorLocal || newState.EmSelecaoInicial ||
            !newState.UsaEquipes || presentedMatchId != (WDMatchSetupContext.Current?.MatchId ?? string.Empty));
        state = newState;
        presentedMatchId = WDMatchSetupContext.Current?.MatchId ?? string.Empty;
        if (contextChanged)
        {
            CancelTacticalSelection();
            pings.Clear(); pingExpiry.Clear(); RebuildPingMarkers();
        }
        ValidateSelectionContext();
    }

    public bool BeginTacticalPing(TerritorioClique.Dono recipient, WDTacticalPingKind kind)
    {
        if (!CanBeginTacticalPing(state, recipient) || state.EmModoTransferencia || regionsActive ||
            !Enum.IsDefined(typeof(WDTacticalPingKind), kind)) return false;
        CancelTacticalSelection();
        pendingRecipient = recipient;
        pendingKind = kind;
        selectionMatchId = WDMatchSetupContext.Current?.MatchId ?? string.Empty;
        return true;
    }

    public void CancelTacticalSelection()
    {
        pendingKind = null;
        pendingRecipient = TerritorioClique.Dono.Neutro;
        selectionMatchId = null;
    }

    private void ValidateSelectionContext()
    {
        if (pendingKind.HasValue && (!CanBeginTacticalPing(state, pendingRecipient) ||
            state.EmModoTransferencia || selectionMatchId != (WDMatchSetupContext.Current?.MatchId ?? string.Empty)))
            CancelTacticalSelection();
    }

    // Identidades reais do Match; neutros não são inimigos nem destinatários elegíveis.
    public static bool IsEligibleTerritory(WDTacticalPingKind kind, TerritorioClique.Dono author,
        TerritorioClique.Dono recipient, TerritorioClique territory)
    {
        if (territory == null || territory.dono == TerritorioClique.Dono.Neutro || author == recipient ||
            !EquipesJogadores.SaoAliados(author, recipient)) return false;
        bool team = territory.dono == author || EquipesJogadores.SaoAliados(author, territory.dono);
        switch (kind)
        {
            case WDTacticalPingKind.Attack:
            case WDTacticalPingKind.Focus: return !team;
            case WDTacticalPingKind.Defend: return team;
            case WDTacticalPingKind.Transfer:
            case WDTacticalPingKind.Attention: return territory.dono == recipient;
            default: return false;
        }
    }

    public static bool CanBeginTacticalPing(MatchUIState snapshot, TerritorioClique.Dono recipient) =>
        snapshot != null && snapshot.Fase == GameManager.FaseTurno.Preparacao &&
        !snapshot.EmSelecaoInicial && snapshot.UsaEquipes && recipient != snapshot.JogadorLocal &&
        snapshot.Jogadores.Any(p => p.Jogador == recipient) &&
        EquipesJogadores.SaoAliados(snapshot.JogadorLocal, recipient);

    // 01.1. Observação passiva de um único clique: false mantém o pipeline normal.
    // Regiões conserva seu contrato próprio; tática não cria modo de seleção.
    public bool HandleTerritory(string territoryId) => regionsActive || TryConsumeTacticalIntent(territoryId);

    public bool TryConsumeTacticalIntent(string territoryId, Vector2? gestureWorldPoint = null)
    {
        if (!pendingKind.HasValue) return false;
        ValidateSelectionContext();
        if (!pendingKind.HasValue) return false;
        var kind = pendingKind.Value;
        var recipient = pendingRecipient;
        // Qualquer tentativa territorial encerra a intenção. Inválido não cria payload
        // nem marcador e não exige replay/segundo clique para o gameplay continuar.
        CancelTacticalSelection();
        if (!TryTerritory(territoryId, out var territory) ||
            !IsEligibleTerritory(kind, state.JogadorLocal, recipient, territory)) return false;
        // 01.2. O ponto vem do mesmo gesto mouse/touch que resolveu o collider.
        // A coordenada local acompanha transformações do território e é transportável.
        Vector3? localAnchor = gestureWorldPoint.HasValue
            ? territory.transform.InverseTransformPoint(new Vector3(gestureWorldPoint.Value.x,
                gestureWorldPoint.Value.y, territory.transform.position.z)) : (Vector3?)null;
        var intent = new WDTacticalPing(kind, territoryId,
            state.JogadorLocal, recipient, PlayerId(state.JogadorLocal), PlayerId(recipient),
            state.Round, state.Fase, WDMatchSetupContext.Current?.MatchId ?? string.Empty, localAnchor);
        if (!ReceiveTacticalPing(intent)) return false;
        TacticalPingRequested?.Invoke(intent);
        return true;
    }

    private static string PlayerId(TerritorioClique.Dono owner) => WDMatchSetupContext.Current?.Participants
        .FirstOrDefault(p => p.SlotIndex == (int)owner - 1)?.PlayerId ?? owner.ToString();

    // Entrada sem transporte: a prévia local e uma futura entrega autorizada usam
    // o mesmo payload. Não programa armas/transferências nem chama Resolution.
    public bool ReceiveTacticalPing(WDTacticalPing ping)
    {
        if (state == null || state.Fase != GameManager.FaseTurno.Preparacao || state.EmSelecaoInicial ||
            ping.Phase != state.Fase || ping.Round != state.Round || !state.UsaEquipes ||
            ping.MatchId != (WDMatchSetupContext.Current?.MatchId ?? string.Empty) ||
            !Enum.IsDefined(typeof(WDTacticalPingKind), ping.Kind) || ping.Author == ping.Recipient ||
            !state.Jogadores.Any(p => p.Jogador == ping.Author) || !state.Jogadores.Any(p => p.Jogador == ping.Recipient) ||
            !EquipesJogadores.SaoAliados(ping.Author, ping.Recipient) ||
            ping.SenderPlayerId != PlayerId(ping.Author) || ping.TargetPlayerId != PlayerId(ping.Recipient) ||
            (state.JogadorLocal != ping.Author && state.JogadorLocal != ping.Recipient)) return false;
        if (!TryTerritory(ping.TerritoryId, out var territory) ||
            !IsEligibleTerritory(ping.Kind, ping.Author, ping.Recipient, territory)) return false;
        pings.Add(ping); pingExpiry.Add(Time.unscaledTime + TacticalSignalDuration);
        RebuildPingMarkers(); TacticalPingPresented?.Invoke(ping);
        return true;
    }
    private void Update()
    {
        if (presentedMatchId != (WDMatchSetupContext.Current?.MatchId ?? string.Empty))
        {
            CancelTacticalSelection();
            pings.Clear(); pingExpiry.Clear(); RebuildPingMarkers();
            presentedMatchId = WDMatchSetupContext.Current?.MatchId ?? string.Empty;
        }
        ValidateSelectionContext();
        ExpireTacticalSignals(Time.unscaledTime);
    }
    private void LateUpdate() => PositionMarkers(Time.unscaledTime);
    private void OnDestroy() { CancelTacticalSelection(); ClearTacticalMarkers(); }
    private void ExpireTacticalSignals(float now)
    {
        bool changed = false;
        for (int i = pings.Count - 1; i >= 0; i--)
            if (now >= pingExpiry[i]) { pings.RemoveAt(i); pingExpiry.RemoveAt(i); changed = true; }
        if (changed) RebuildPingMarkers();
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
        CancelTacticalSelection();
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
        ClearTacticalMarkers();
        if (overlay == null) return;
        for (int i = 0; i < pings.Count; i++)
        {
            var ping = pings[i];
            if (!TryTerritory(ping.TerritoryId, out var territory)) continue;
            var item = new TacticalMarker { Territory = territory, Expires = pingExpiry[i], LocalAnchor = ping.TerritoryLocalAnchor };
            item.Root = WDUIFactory.Rect($"Tactical_{ping.TerritoryId}_{i}", overlay);
            item.Root.anchorMin = item.Root.anchorMax = item.Root.pivot = new Vector2(.5f, .5f);
            item.Root.sizeDelta = WDTacticalVisualStyle.MarkerSize(ping.Kind);
            item.Group = item.Root.gameObject.AddComponent<CanvasGroup>();
            item.Group.blocksRaycasts = false; item.Group.interactable = false;
            // 03.1. Exportações territoriais do mesmo símbolo: halo/glow atuais do
            // Figma, sem modificar as texturas compartilhadas pelo radial aprovado.
            float core = WDTacticalVisualStyle.MarkerIconCoreSize(ping.Kind);
            float iconY = (item.Root.sizeDelta.y - core) * .5f;
            AddMarkerIcon(item, WDTacticalVisualStyle.IconName(ping.Kind), new Vector2(0, iconY),
                Vector2.one * WDTacticalVisualStyle.MarkerIconRenderSize(ping.Kind));
            if (ping.Kind == WDTacticalPingKind.Attention)
                AddMarkerIcon(item, "TacticAttentionDot", new Vector2(0, iconY - 6.125f), Vector2.one * 18.5f);
            Color color = WDTacticalVisualStyle.Color(ping.Kind);
            var label = WDUIFactory.Text("ActionName", item.Root, theme,
                WDTacticalVisualStyle.MarkerFontSize, color, TextAlignmentOptions.Center);
            label.font = ResolveMarkerFont();
            label.characterSpacing = WDTacticalVisualStyle.MarkerCharacterSpacing;
            label.text = WDTacticalVisualStyle.Label(ping.Kind);
            label.richText = false; label.raycastTarget = false; label.enableAutoSizing = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.sizeDelta = new Vector2(item.Root.sizeDelta.x, 15.75f);
            label.rectTransform.anchoredPosition = new Vector2(0, (15.75f - item.Root.sizeDelta.y) * .5f);
            ConfigureMarkerTextMaterial(item, label);
            markers.Add(item);
        }
        PositionMarkers(Time.unscaledTime);
    }

    // 03.1.1. Contraste do texto no shader SDF existente (sem Outline de uGUI).
    private static void ConfigureMarkerTextMaterial(TacticalMarker item, TMP_Text label)
    {
        var material = new Material(label.fontSharedMaterial) { hideFlags = HideFlags.DontSave };
        // O preset já incluído em Resources mantém o shader completo no build.
        // Estilo territorial próprio; nenhum efeito é alterado no material compartilhado.
        var shaderSource = Resources.Load<TMP_FontAsset>("Fonts & Materials/BebasNeue-Regular SDF");
        if (shaderSource != null) material.shader = shaderSource.material.shader;
        item.TextMaterial = material;
        Color dark = new Color(.012f, .02f, .035f, 1f);
        // Largura normalizada a partir de aproximadamente 0,5 px na escala de desenho (1080p).
        float sdfPixels = material.GetFloat(ShaderUtilities.ID_GradientScale) *
            label.fontSize / label.font.faceInfo.pointSize;
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Clamp01(1f / Mathf.Max(.01f, sdfPixels)));
        material.SetColor(ShaderUtilities.ID_OutlineColor, dark);
        // Texto sólido + contorno nítido. Glow pertence exclusivamente aos ícones.
        material.SetFloat("_OutlineSoftness", 0f);
        material.SetFloat("_FaceDilate", 0f);
        material.DisableKeyword("GLOW_ON");
        material.SetColor("_GlowColor", Color.clear);
        material.SetFloat("_GlowInner", 0f);
        material.SetFloat("_GlowOffset", 0f);
        material.SetFloat("_GlowOuter", 0f);
        // Sem sombra/halo difuso atrás dos glifos: evita unir letras vizinhas.
        material.DisableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        material.SetColor("_UnderlayColor", Color.clear);
        material.SetFloat("_UnderlayOffsetX", 0f); material.SetFloat("_UnderlayOffsetY", 0f);
        material.SetFloat("_UnderlayDilate", 0f); material.SetFloat("_UnderlaySoftness", 0f);
        label.fontSharedMaterial = material;
        label.UpdateMeshPadding();
    }

    private void AddMarkerIcon(TacticalMarker marker, string asset, Vector2 position, Vector2 size)
    {
        var rect = WDUIFactory.Rect(asset, marker.Root);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
        var texture = Resources.Load<Texture2D>(WDTacticalVisualStyle.MarkerAssetRoot + asset);
        if (texture == null) return;
        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        marker.Sprites.Add(image.sprite);
    }

    private void PositionMarkers(float now)
    {
        // 03.2. As cenas da partida têm câmera Untagged. Mesmo fallback usado pelo
        // InputPartida/PreparedActionsOverlay: nome "MainCamera" não equivale à tag.
        Camera worldCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        foreach (var item in markers)
        {
            Vector2 point = default;
            bool visible = item.Territory != null && item.Territory.gameObject.activeInHierarchy &&
                worldCamera != null && MapOverlayProjection.TryWorldToOverlay(overlay,
                    item.LocalAnchor.HasValue ? item.Territory.transform.TransformPoint(item.LocalAnchor.Value) :
                        MapOverlayProjection.GetTerritoryWorldAnchor(item.Territory), worldCamera, out point);
            item.Root.gameObject.SetActive(visible);
            if (!visible) continue;
            // 03.2.1. Gesto real: exatamente o ponto escolhido, sem offset/clamp.
            // Eventos antigos sem âncora conservam somente seu fallback compatível.
            item.Root.anchoredPosition = item.LocalAnchor.HasValue ? point : point + new Vector2(0, 52);
            item.Group.alpha = Mathf.Clamp01((item.Expires - now) / .5f);
            // O timestamp original impede reinício ao reconstruir após outro ping/expiração.
            float age = now - (item.Expires - TacticalSignalDuration);
            item.Root.localScale = Vector3.one * WDTacticalVisualStyle.MarkerEntryScale(age);
        }
    }

    private void ClearTacticalMarkers()
    {
        foreach (var item in markers)
        {
            if (item.Root != null)
            {
                item.Root.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(item.Root.gameObject); else DestroyImmediate(item.Root.gameObject);
            }
            foreach (var sprite in item.Sprites)
                if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
            if (item.TextMaterial != null)
            { if (Application.isPlaying) Destroy(item.TextMaterial); else DestroyImmediate(item.TextMaterial); }
        }
        markers.Clear();
        // 03.3. Fonte/atlas próprios da apresentação temporária, nunca do tema.
        if (markerFont != null)
        {
            foreach (var atlas in markerFont.atlasTextures) ReleaseMarkerResource(atlas);
            ReleaseMarkerResource(markerFont.material);
            ReleaseMarkerResource(markerFont);
            markerFont = null;
        }
    }

    private TMP_FontAsset ResolveMarkerFont()
    {
        if (markerFont != null) return markerFont;
        var source = Resources.Load<Font>("UI/Fonts/Gabarito-Medium");
        if (source == null) return theme.FonteInterface;
        // Padding suficiente para halo e glow em fonte pequena, sem cortar o SDF.
        markerFont = TMP_FontAsset.CreateFontAsset(source, 90, 32,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
        markerFont.hideFlags = HideFlags.DontSave;
        markerFont.material.hideFlags = HideFlags.DontSave;
        foreach (var atlas in markerFont.atlasTextures) atlas.hideFlags = HideFlags.DontSave;
        return markerFont;
    }

    private static void ReleaseMarkerResource(UnityEngine.Object resource)
    {
        if (resource == null) return;
        if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
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

// 04. Identidade compartilhada. A cor vem do próprio ícone monocromático com glow,
// sem segunda paleta manual. A leitura é pequena, uma vez por ação, com cache de cor.
public static class WDTacticalVisualStyle
{
    public const string AssetRoot = "UI/Match/FigmaCurrent/Hubs/";
    public const string MarkerAssetRoot = AssetRoot + "Territorial/";
    public const float MarkerFontSize = 13.125f;
    public const float MarkerCharacterSpacing = 4f; // TMP: 4% em ≈ 0,525 px por avanço a 13,125 px.
    public const float MarkerEntryDuration = .22f;

    // 04.1. Geometria territorial, separada da escala do radial. Medidas sem halo.
    public static Vector2 MarkerSize(WDTacticalPingKind kind) => kind switch
    {
        WDTacticalPingKind.Defend => new Vector2(64.75f, 49f),
        WDTacticalPingKind.Focus => new Vector2(64.75f, 45.28125f),
        WDTacticalPingKind.Transfer => new Vector2(77f, 45.5f),
        WDTacticalPingKind.Attention => new Vector2(64.75f, 48.125f),
        _ => new Vector2(64.75f, 47.25f)
    };
    public static float MarkerIconCoreSize(WDTacticalPingKind kind) => kind switch
    {
        WDTacticalPingKind.Attack or WDTacticalPingKind.Attention => 29.75f,
        WDTacticalPingKind.Focus => 27.5625f,
        _ => 28f
    };
    // Extensão completa do SVG exportado; não cortar nem esticar o halo.
    public static float MarkerIconRenderSize(WDTacticalPingKind kind) => kind switch
    {
        WDTacticalPingKind.Attack => 44.75f,
        WDTacticalPingKind.Attention => 46.5f,
        WDTacticalPingKind.Focus => 42.5625f,
        _ => 43f
    };
    public static float MarkerEntryScale(float age)
    {
        const float peakAt = .11f;
        if (age >= MarkerEntryDuration) return 1f;
        if (age <= peakAt) return Mathf.Lerp(.9f, 1.05f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / peakAt)));
        return Mathf.Lerp(1.05f, 1f, Mathf.SmoothStep(0, 1, (age - peakAt) / (MarkerEntryDuration - peakAt)));
    }

    public const float SectorFillOpacity = .98f; // SVG: grupo .9 × corpo .98 = .882 (antes .738).
    public static string Label(WDTacticalPingKind kind) => kind switch
    {
        WDTacticalPingKind.Attack => "ATACAR", WDTacticalPingKind.Defend => "DEFENDER",
        WDTacticalPingKind.Focus => "FOCO", WDTacticalPingKind.Transfer => "TRANSFERIR",
        WDTacticalPingKind.Attention => "ATENÇÃO", _ => string.Empty
    };
    public static string IconName(WDTacticalPingKind kind) => "Tactic" + kind;
    private static readonly Dictionary<WDTacticalPingKind, Color> Colors = new();
    public static Color Color(WDTacticalPingKind kind)
    {
        if (Colors.TryGetValue(kind, out var color)) return color;
        var texture = Resources.Load<Texture2D>(AssetRoot + IconName(kind));
        if (texture == null) return UnityEngine.Color.clear;
        // Arte oficial monocromática: o RGB predominante ignora transparência e antialiasing.
        var weights = new Dictionary<int, int>();
        int best = 0, bestWeight = 0;
        foreach (Color32 pixel in texture.GetPixels32())
        {
            if (pixel.a == 0) continue;
            int rgb = (pixel.r << 16) | (pixel.g << 8) | pixel.b;
            weights.TryGetValue(rgb, out int weight);
            weight += pixel.a; weights[rgb] = weight;
            if (weight > bestWeight) { best = rgb; bestWeight = weight; }
        }
        color = new Color32((byte)(best >> 16), (byte)(best >> 8), (byte)best, 255);
        Colors[kind] = color;
        return color;
    }
}

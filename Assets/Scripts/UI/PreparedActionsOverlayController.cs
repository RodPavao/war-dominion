using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PreparedActionsOverlayController : MonoBehaviour
{
    private sealed class ArrowBinding
    {
        public PreparedActionArrowView View;
        public string OriginId;
        public string DestinationId;
    }

    // ============================================================
    // 01. ESTADO VISUAL DERIVADO DO PRESENTER
    // ============================================================

    private readonly Dictionary<string, ArrowBinding> arrows =
        new Dictionary<string, ArrowBinding>();
    private readonly HashSet<string> activeKeys = new HashSet<string>();
    private MatchUIPresenter presenter;
    private RectTransform overlay;
    private WarDominionUITheme theme;

    // ============================================================
    // 02. INICIALIZAÇÃO E ASSINATURA
    // ============================================================

    public void Configure(
        MatchUIPresenter newPresenter,
        RectTransform newOverlay,
        WarDominionUITheme newTheme)
    {
        if (presenter != null)
            presenter.EstadoAlterado -= OnStateChanged;

        presenter = newPresenter;
        overlay = newOverlay;
        theme = newTheme;

        if (presenter != null)
        {
            presenter.EstadoAlterado += OnStateChanged;
            presenter.SolicitarSnapshotAtual();
        }
    }

    private void OnDestroy()
    {
        if (presenter != null)
            presenter.EstadoAlterado -= OnStateChanged;
    }

    // ============================================================
    // 03. RECONCILIAÇÃO COM A FILA REAL DE ATAQUES
    // ============================================================

    private void OnStateChanged(MatchUIState state)
    {
        if (state == null ||
            state.Fase != GameManager.FaseTurno.Preparacao)
        {
            ClearAll();
            return;
        }

        activeKeys.Clear();
        foreach (MatchUIActionState action in state.Acoes)
        {
            string key = $"action:{action.Posicao}:{action.OrigemId}:{action.DestinoId}";
            EnsureArrow(
                key, action.OrigemId, action.DestinoId,
                state.JogadorLocal, action.Quantidade, false);
            activeKeys.Add(key);
        }

        bool hasChosenDestination =
            !string.IsNullOrEmpty(state.OrigemSelecionadaId) &&
            !string.IsNullOrEmpty(state.DestinoSelecionadoId) &&
            !string.IsNullOrEmpty(state.TipoAcaoEsperado);

        if (hasChosenDestination)
        {
            const string previewKey = "preview";
            EnsureArrow(
                previewKey,
                state.OrigemSelecionadaId,
                state.DestinoSelecionadoId,
                state.JogadorLocal,
                state.QuantidadeAcaoSelecionada,
                false);
            activeKeys.Add(previewKey);
        }
        else if (!string.IsNullOrEmpty(state.OrigemSelecionadaId))
        {
            foreach (MatchUITerritoryState territory in state.Territorios)
            {
                if ((territory.EstadoVisual & MatchUITerritoryVisualState.DestinoValido) == 0)
                    continue;

                string key = "possibility:" + territory.Id;
                EnsureArrow(
                    key,
                    state.OrigemSelecionadaId,
                    territory.Id,
                    state.JogadorLocal,
                    0,
                    true);
                activeKeys.Add(key);
            }
        }

        RemoveInactiveArrows();
    }

    private void EnsureArrow(
        string key,
        string originId,
        string destinationId,
        TerritorioClique.Dono player,
        int amount,
        bool planning)
    {
        if (!arrows.TryGetValue(key, out ArrowBinding binding))
        {
            var arrowObject = new GameObject(
                $"PreparedAttack_{key}", typeof(RectTransform));
            arrowObject.transform.SetParent(overlay, false);
            var view = arrowObject.AddComponent<PreparedActionArrowView>();
            view.Build(theme);
            binding = new ArrowBinding { View = view };
            arrows.Add(key, binding);
        }

        binding.OriginId = originId;
        binding.DestinationId = destinationId;
        binding.View.SetColorAndAmount(
            PaletaJogadores.ObterCorAtiva(player), amount);
        binding.View.SetPlanning(planning);
    }

    private void RemoveInactiveArrows()
    {
        var removedKeys = new List<string>();
        foreach (KeyValuePair<string, ArrowBinding> pair in arrows)
        {
            if (activeKeys.Contains(pair.Key))
                continue;

            Destroy(pair.Value.View.gameObject);
            removedKeys.Add(pair.Key);
        }

        foreach (string key in removedKeys)
            arrows.Remove(key);
    }

    // ============================================================
    // 04. ACOMPANHAMENTO DE CÂMERA, MAPA E CONTADORES
    // ============================================================

    private void LateUpdate()
    {
        if (overlay == null || theme == null)
            return;

        foreach (ArrowBinding binding in arrows.Values)
        {
            if (TryGetOverlayPosition(binding.OriginId, out Vector2 origin) &&
                TryGetOverlayPosition(binding.DestinationId, out Vector2 destination))
            {
                binding.View.SetGeometry(origin, destination);
            }
            else
            {
                binding.View.gameObject.SetActive(false);
            }
        }

    }

    private bool TryGetOverlayPosition(string territoryId, out Vector2 position)
    {
        position = default;
        if (!TryGetTerritory(territoryId, out TerritorioClique territory))
            return false;

        Vector3 worldPosition = MapOverlayProjection.GetTerritoryWorldAnchor(territory);

        Camera worldCamera = Camera.main != null
            ? Camera.main
            : FindAnyObjectByType<Camera>();
        return MapOverlayProjection.TryWorldToOverlay(
            overlay, worldPosition, worldCamera, out position);
    }

    private static bool TryGetTerritory(
        string territoryId,
        out TerritorioClique territory)
    {
        territory = null;
        if (MapaAtivo.Instance != null &&
            MapaAtivo.Instance.TentarObterTerritorio(territoryId, out territory))
        {
            return territory != null;
        }

        foreach (TerritorioClique candidate in MapaAtivo.ObterTerritoriosOuCena())
        {
            if (candidate != null && candidate.idTerritorio == territoryId)
            {
                territory = candidate;
                return true;
            }
        }

        return false;
    }

    private void ClearAll()
    {
        foreach (ArrowBinding binding in arrows.Values)
            if (binding.View != null)
                Destroy(binding.View.gameObject);
        arrows.Clear();
        activeKeys.Clear();
    }
}

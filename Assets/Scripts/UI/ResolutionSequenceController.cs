using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IResolutionVisualPresenter
{
    bool Supports(ResolutionVisualEventType type);
    IEnumerator Present(
        ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context);
}

public interface IResolutionVisualStateCoordinator
{
    void PrepareVisualState(IReadOnlyList<ResolutionVisualEvent> events);
    void CompleteVisualState();
}

public interface IResolutionVisualCleanup
{
    void ResetTransientVisuals();
}

public static class MapOverlayProjection
{
    // ============================================================
    // 01. CONVERSÃO GENÉRICA MUNDO → CAMADA DE UI
    // ============================================================

    public static bool TryWorldToOverlay(
        RectTransform overlay,
        Vector3 worldPosition,
        Camera worldCamera,
        out Vector2 overlayPosition)
    {
        overlayPosition = default;
        if (overlay == null || worldCamera == null)
            return false;

        Vector3 projected = worldCamera.WorldToScreenPoint(worldPosition);
        if (projected.z < 0f)
            return false;
        Vector2 screenPosition = projected;
        Canvas canvas = overlay.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlay, screenPosition, canvasCamera, out overlayPosition);
    }

    // ============================================================
    // 01.1. AUTORIDADE ÚNICA TERRITORY ID → POSIÇÃO LOCAL DE UI
    // ============================================================

    public static bool TryTerritoryToOverlay(string territoryId,
        RectTransform overlay, out Vector2 overlayPosition,
        out TerritorioClique territory)
    {
        overlayPosition = default;
        territory = null;
        Camera mapCamera = LayoutPartida.CameraRuntime;
        return !string.IsNullOrWhiteSpace(territoryId) && overlay != null &&
            mapCamera != null && MapaAtivo.Instance != null &&
            MapaAtivo.Instance.TentarObterTerritorio(territoryId, out territory) &&
            TryWorldToOverlay(overlay, GetTerritoryPresentationWorldAnchor(territory),
                mapCamera, out overlayPosition);
    }

    public static Vector3 GetTerritoryPresentationWorldAnchor(TerritorioClique territory)
    {
        if (territory == null)
            return Vector3.zero;
        Transform counter = territory.transform.Find("ContadorModerno");
        return counter != null ? counter.position : GetTerritoryWorldAnchor(territory);
    }

    public static Vector3 GetTerritoryWorldAnchor(TerritorioClique territory)
    {
        if (territory == null)
            return Vector3.zero;
        Collider2D collider = territory.GetComponent<PolygonCollider2D>() ??
            territory.GetComponent<Collider2D>();
        if (collider == null)
            return territory.transform.position;
        Vector3 center = collider.bounds.center;
        Vector2 insideOrBoundary = collider.ClosestPoint(center);
        return new Vector3(insideOrBoundary.x, insideOrBoundary.y, center.z);
    }
}

public sealed class ResolutionVisualPresentationContext
{
    // ============================================================
    // 01. SERVIÇOS VISUAIS COMPARTILHADOS
    // ============================================================

    public RectTransform Overlay { get; }
    private readonly Func<float> getPlaybackSpeed;

    public ResolutionVisualPresentationContext(
        RectTransform overlay,
        Func<float> playbackSpeedProvider)
    {
        Overlay = overlay;
        getPlaybackSpeed = playbackSpeedProvider;
    }

    public Color GetPlayerColor(TerritorioClique.Dono player) =>
        PaletaJogadores.ObterCorAtiva(player);

    public float PlaybackDeltaTime => ScaleDeltaTime(
        Time.unscaledDeltaTime, getPlaybackSpeed?.Invoke() ?? 1f);

    public static float ScaleDeltaTime(float deltaTime, float playbackSpeed) =>
        deltaTime * Mathf.Max(0.01f, playbackSpeed);

    public bool TryWorldToOverlay(
        Vector3 worldPosition,
        Camera worldCamera,
        out Vector2 overlayPosition)
    {
        return MapOverlayProjection.TryWorldToOverlay(
            Overlay, worldPosition, worldCamera, out overlayPosition);
    }

    public IEnumerator Wait(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            remaining -= ScaleDeltaTime(
                Time.unscaledDeltaTime, getPlaybackSpeed?.Invoke() ?? 1f);
            yield return null;
        }
    }
}

[DisallowMultipleComponent]
public sealed class ResolutionSequenceController : MonoBehaviour
{
    // ============================================================
    // 02. ESTADO, EVENTOS E CONFIGURAÇÃO
    // ============================================================

    private readonly List<IResolutionVisualPresenter> presenters =
        new List<IResolutionVisualPresenter>();
    private readonly List<IResolutionVisualStateCoordinator> stateCoordinators =
        new List<IResolutionVisualStateCoordinator>();
    private ResolutionVisualPresentationContext context;
    private RoundAnnouncementView roundAnnouncement;
    private Coroutine currentSequence;
    private IReadOnlyList<ResolutionVisualEvent> pendingAfterAnnouncement;
    private bool visualStatePrepared;

    public bool IsRunning => currentSequence != null;
    public float PlaybackSpeed { get; set; } = 1f;
    public bool IsAccelerated => PlaybackSpeed > 1f;
    public event Action SequenceStarted;
    public event Action<ResolutionVisualEvent, int> EventStarted;
    public event Action<ResolutionVisualEvent, int> EventCompleted;
    public event Action SequenceCompleted;

    // 02.1. A sequência é a autoridade única da preferência de velocidade. Ela
    // existe durante toda a partida, portanto a escolha feita na Preparation é
    // naturalmente preservada quando a mesma instância entra em Resolution.
    public void TogglePlaybackSpeed()
    {
        PlaybackSpeed = IsAccelerated ? 1f : 2f;
    }

    // ============================================================
    // 03. INICIALIZAÇÃO E REGISTRO DE APRESENTADORES
    // ============================================================

    public void Configure(
        RectTransform overlay,
        RoundAnnouncementView announcement)
    {
        if (roundAnnouncement != null)
            roundAnnouncement.Completed -= OnAnnouncementCompleted;

        context = new ResolutionVisualPresentationContext(
            overlay, () => PlaybackSpeed);
        roundAnnouncement = announcement;

        if (roundAnnouncement != null)
            roundAnnouncement.Completed += OnAnnouncementCompleted;

        RefreshPresenters();
    }

    private void OnDestroy()
    {
        StopCurrentSequence();
        if (roundAnnouncement != null)
            roundAnnouncement.Completed -= OnAnnouncementCompleted;
    }

    private void OnDisable() => StopCurrentSequence();

    public void RefreshPresenters()
    {
        presenters.Clear();
        stateCoordinators.Clear();
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component is IResolutionVisualPresenter presenter)
                presenters.Add(presenter);
            if (component is IResolutionVisualStateCoordinator coordinator)
                stateCoordinators.Add(coordinator);
        }
    }

    // ============================================================
    // 04. EXECUÇÃO ORDENADA E ESPERA DO ANÚNCIO
    // ============================================================

    public void Play(IReadOnlyList<ResolutionVisualEvent> events)
    {
        ResolutionVisualEvent[] snapshot = CopyValidEvents(events);
        StopCurrentSequence();
        PrepareVisualState(snapshot);
        pendingAfterAnnouncement = null;
        currentSequence = StartCoroutine(PlaySequence(snapshot));
    }

    public void PlayAfterAnnouncement(IReadOnlyList<ResolutionVisualEvent> events)
    {
        ResolutionVisualEvent[] snapshot = CopyValidEvents(events);
        StopCurrentSequence();
        PrepareVisualState(snapshot);

        if (roundAnnouncement == null || !roundAnnouncement.IsShowing)
        {
            currentSequence = StartCoroutine(PlaySequence(snapshot));
            return;
        }

        pendingAfterAnnouncement = snapshot;
    }

    private void OnAnnouncementCompleted()
    {
        if (pendingAfterAnnouncement == null)
            return;

        IReadOnlyList<ResolutionVisualEvent> events = pendingAfterAnnouncement;
        pendingAfterAnnouncement = null;
        currentSequence = StartCoroutine(PlaySequence(events));
    }

    private IEnumerator PlaySequence(IReadOnlyList<ResolutionVisualEvent> events)
    {
        SequenceStarted?.Invoke();

        for (int index = 0; index < events.Count; index++)
        {
            ResolutionVisualEvent visualEvent = events[index];
            EventStarted?.Invoke(visualEvent, index);

            IResolutionVisualPresenter presenter = FindPresenter(visualEvent.Type);
            if (presenter != null)
            {
                yield return PresentSafely(presenter, visualEvent);
            }
            else
            {
                Debug.LogWarning(
                    $"[ResolutionVisual] Presenter não registrado para {visualEvent.Type}; " +
                    "o evento será ignorado sem bloquear a partida.");
                yield return null;
            }

            EventCompleted?.Invoke(visualEvent, index);
        }

        currentSequence = null;
        CompleteVisualState();
        SequenceCompleted?.Invoke();
    }

    private IEnumerator PresentSafely(
        IResolutionVisualPresenter presenter,
        ResolutionVisualEvent visualEvent)
    {
        IEnumerator presentation;
        try
        {
            presentation = presenter.Present(visualEvent, context);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[ResolutionVisual] Falha ao iniciar {visualEvent}: {exception}");
            yield break;
        }

        if (presentation == null)
            yield break;

        while (true)
        {
            bool hasNext;
            object current;
            try
            {
                hasNext = presentation.MoveNext();
                current = hasNext ? presentation.Current : null;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ResolutionVisual] Falha durante {visualEvent}: {exception}");
                yield break;
            }

            if (!hasNext)
                yield break;
            yield return current;
        }
    }

    // ============================================================
    // 05. VELOCIDADE FUTURA E UTILITÁRIOS
    // ============================================================

    private IResolutionVisualPresenter FindPresenter(ResolutionVisualEventType type) =>
        presenters.Find(presenter => presenter.Supports(type));

    private void StopCurrentSequence()
    {
        if (currentSequence != null)
            StopCoroutine(currentSequence);
        currentSequence = null;
        pendingAfterAnnouncement = null;
        // 05.1. Fase 7.3A: interrupções removem apenas visuais transitórios;
        // estado autoritativo e sequência funcional permanecem nos controladores.
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
            if (component is IResolutionVisualCleanup cleanup)
                cleanup.ResetTransientVisuals();
        CompleteVisualState();
    }

    private void PrepareVisualState(IReadOnlyList<ResolutionVisualEvent> events)
    {
        foreach (IResolutionVisualStateCoordinator coordinator in stateCoordinators)
            coordinator.PrepareVisualState(events);
        visualStatePrepared = true;
    }

    private void CompleteVisualState()
    {
        if (!visualStatePrepared)
            return;
        foreach (IResolutionVisualStateCoordinator coordinator in stateCoordinators)
            coordinator.CompleteVisualState();
        visualStatePrepared = false;
    }

    private static ResolutionVisualEvent[] CopyValidEvents(
        IReadOnlyList<ResolutionVisualEvent> events)
    {
        if (events == null || events.Count == 0)
            return Array.Empty<ResolutionVisualEvent>();

        var copy = new List<ResolutionVisualEvent>(events.Count);
        for (int index = 0; index < events.Count; index++)
            if (events[index] != null)
                copy.Add(events[index]);
        return copy.ToArray();
    }
}

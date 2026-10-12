using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AttackResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter
{
    private const float AirTransportTravelDuration = .9f;
    private const float TransportedInstallationFadeDuration = .38f;
    // ============================================================
    // 01. CONFIGURAÇÃO E ESTADO VISUAL TEMPORÁRIO
    // ============================================================

    private WarDominionUITheme theme;

    public void Configure(WarDominionUITheme newTheme)
    {
        theme = newTheme;
    }

    public bool Supports(ResolutionVisualEventType type) =>
        type == ResolutionVisualEventType.Attack ||
        type == ResolutionVisualEventType.Movement;

    // ============================================================
    // 02. COREOGRAFIA DO ATAQUE JÁ CALCULADO
    // ============================================================

    public IEnumerator Present(
        ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        Debug.Log(
            $"[AttackResolution][DEBUG] Presenter de ataque acionado para evento: " +
            $"{visualEvent}");
        if (theme == null || visualEvent == null || context == null ||
            !ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.OriginTerritoryId, out TerritorioClique origin) ||
            !ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.DestinationTerritoryId, out TerritorioClique destination) ||
            !ResolutionVisualMapUtility.TryGetPosition(
                origin, context, out Vector2 originPosition) ||
            !ResolutionVisualMapUtility.TryGetPosition(
                destination, context, out Vector2 destinationPosition))
        {
            yield break;
        }

        Color playerColor = context.GetPlayerColor(visualEvent.Player);
        RectTransform root = ResolutionVisualMapUtility.CreateRoot(
            "AttackResolution", context.Overlay);
        PreparedActionArrowView route = root.gameObject.AddComponent<PreparedActionArrowView>();
        route.Build(theme);
        route.SetColorAndAmount(playerColor, 0);
        route.SetPlanning(false);
        GameObject attacker = CreateCounterCopy(
            origin, ObterQuantidadeAnimada(visualEvent), visualEvent.Player);
        RectTransform transportedIcon = null;
        Vector2 installationOrigin = default;
        Vector2 installationDestination = default;
        if (!string.IsNullOrEmpty(visualEvent.InstallationId))
            WDWeaponInstallationMapPresenter.TryBeginTransportTravel(
                visualEvent.InstallationId, context.Overlay, out transportedIcon,
                out installationOrigin, out installationDestination);
        route.SetGeometry(originPosition, destinationPosition);
        Vector3 originWorld = GetCounterWorldPosition(origin);
        Vector3 destinationWorld = GetCounterWorldPosition(destination);
        Vector3 controlWorld = CreateWorldControl(originWorld, destinationWorld);
        attacker.transform.position = originWorld;
        origin.DestacarReforco();
        float elapsed = 0f;
        float travelDuration = visualEvent.ContentId == WDSupportWeaponIds.AirTransport
            ? AirTransportTravelDuration : theme.AttackTravelDuration;
        while (elapsed < travelDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / travelDuration);
            float smooth = progress * progress * (3f - 2f * progress);
            attacker.transform.position = Bezier(
                originWorld, controlWorld, destinationWorld, smooth);
            if (transportedIcon != null)
            {
                transportedIcon.anchoredPosition = Bezier(installationOrigin,
                    CreateOverlayControl(installationOrigin, installationDestination),
                    installationDestination, smooth);
            }
            yield return null;
        }

        Destroy(attacker);
        if (transportedIcon != null) Destroy(transportedIcon.gameObject);
        ApplyFinalVisualState(visualEvent);
        if (!string.IsNullOrEmpty(visualEvent.InstallationId) &&
            WDWeaponInstallationMapPresenter.TryBeginInstallationFade(
                visualEvent.InstallationId, string.Empty,
                visualEvent.DestinationTerritoryId, out string installationId))
        {
            float fadeElapsed = 0f;
            while (fadeElapsed < TransportedInstallationFadeDuration)
            {
                fadeElapsed += context.PlaybackDeltaTime;
                float progress = Mathf.Clamp01(
                    fadeElapsed / TransportedInstallationFadeDuration);
                float eased = progress * progress * (3f - 2f * progress);
                WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(
                    installationId, eased);
                yield return null;
            }
            WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(
                installationId, 1f);
        }
        if (visualEvent.Conquered)
            destination.GetComponent<TerritorioVisual>()?.DestacarConquistaBreve();
        else
            destination.DestacarReforco();
        yield return context.Wait(theme.AttackImpactDuration);
        yield return context.Wait(theme.AttackResultDuration);

        Destroy(root.gameObject);
    }

    // ============================================================
    // 04. APLICAÇÃO DO RESULTADO AUTORITATIVO
    // ============================================================

    private static void ApplyFinalVisualState(ResolutionVisualEvent visualEvent)
    {
        if (ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.OriginTerritoryId, out TerritorioClique origin))
            origin.AplicarEstadoVisualResolucao(
                visualEvent.OriginAfter, visualEvent.Player);
        if (ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.DestinationTerritoryId, out TerritorioClique destination))
            destination.AplicarEstadoVisualResolucao(
                visualEvent.DestinationAfter, visualEvent.NewOwner);
    }

    // ============================================================
    // 05. ELEMENTOS ESPECÍFICOS DO ATAQUE
    // ============================================================

    private static GameObject CreateCounterCopy(
        TerritorioClique origin,
        int amount,
        TerritorioClique.Dono owner)
    {
        ContadorTropas source = origin.GetComponentInChildren<ContadorTropas>(true);
        if (source == null)
            return new GameObject("AnimatedCounterCopy");

        return CriarRepresentacaoAnimada(source.transform, amount, owner);
    }

    // ============================================================
    // 06. REPRESENTAÇÃO DESVINCULADA DO ESTADO DA ORIGEM
    // ============================================================

    public static GameObject CriarRepresentacaoAnimada(
        Transform sourceTransform,
        int amount,
        TerritorioClique.Dono owner)
    {
        var copy = new GameObject("AnimatedCounterCopy");
        if (sourceTransform != null)
        {
            copy.transform.position = sourceTransform.position;
            copy.transform.rotation = sourceTransform.rotation;
            copy.transform.localScale = sourceTransform.lossyScale;
        }

        WDArmyCounterView view = copy.AddComponent<WDArmyCounterView>();
        view.Configurar(null);
        Color stripColor = owner == TerritorioClique.Dono.Neutro
            ? PaletaJogadores.ObterCorNeutraAtiva()
            : PaletaJogadores.ObterCorAtiva(owner);
        view.Exibir(amount, stripColor);
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            renderer.sortingOrder += 100;
        return copy;
    }

    public static int ObterQuantidadeAnimada(ResolutionVisualEvent visualEvent) =>
        visualEvent != null ? visualEvent.Amount : 0;

    private static Vector3 GetCounterWorldPosition(TerritorioClique territory)
    {
        ContadorTropas counter = territory.GetComponentInChildren<ContadorTropas>(true);
        return counter != null ? counter.transform.position : territory.transform.position;
    }

    private Vector3 CreateWorldControl(Vector3 origin, Vector3 destination)
    {
        Vector3 delta = destination - origin;
        Vector3 normal = new Vector3(-delta.y, delta.x, 0f).normalized;
        return (origin + destination) * 0.5f + normal * Mathf.Min(
            theme.AttackMaxCurveHeight,
            delta.magnitude * theme.AttackCurveFactor);
    }

    private static Vector3 Bezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private Vector2 CreateOverlayControl(Vector2 origin, Vector2 destination)
    {
        Vector2 delta = destination - origin;
        Vector2 normal = new(-delta.y, delta.x);
        if (normal.sqrMagnitude > 0f) normal.Normalize();
        return (origin + destination) * .5f + normal * Mathf.Min(
            theme.PreparedArrowMaxCurveHeight,
            delta.magnitude * theme.PreparedArrowCurveFactor);
    }
}

[DisallowMultipleComponent]
public sealed class RegionConquestResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter
{
    public bool Supports(ResolutionVisualEventType type) =>
        type == ResolutionVisualEventType.RegionConquest;

    public IEnumerator Present(ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        if (visualEvent == null || context == null ||
            visualEvent.RegionTerritoryIds == null)
            yield break;

        foreach (string territoryId in visualEvent.RegionTerritoryIds)
            if (ResolutionVisualMapUtility.TryGetTerritory(
                territoryId, out TerritorioClique territory))
                territory.DestacarConquistaRegiao();

        yield return context.Wait(TerritorioVisual.DuracaoDestaqueConquista);
    }
}

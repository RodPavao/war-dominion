using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AttackResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter, IResolutionVisualCleanup
{
    private const float AirTransportTravelDuration = .9f;
    private const float TransportedInstallationFadeDuration = .38f;
    // ============================================================
    // 01. CONFIGURAÇÃO E ESTADO VISUAL TEMPORÁRIO
    // ============================================================

    private WarDominionUITheme theme;
    private GameObject activeRoot;
    private GameObject activeAttacker;

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
        activeRoot = root.gameObject;
        GameObject attacker = CreateCounterCopy(
            origin, ObterQuantidadeAnimada(visualEvent), visualEvent.Player);
        activeAttacker = attacker;
        RectTransform transportedIcon = null;
        Vector2 installationOrigin = default;
        Vector2 installationDestination = default;
        if (!string.IsNullOrEmpty(visualEvent.InstallationId))
            WDWeaponInstallationMapPresenter.TryBeginTransportTravel(
                visualEvent.InstallationId, context.Overlay, out transportedIcon,
                out installationOrigin, out installationDestination);
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
            float smooth = theme.PremiumGameplayFeedbackEnabled
                ? EaseInOutCubic(progress)
                : progress * progress * (3f - 2f * progress);
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
        activeAttacker = null;
        if (transportedIcon != null) Destroy(transportedIcon.gameObject);
        bool isCombat = visualEvent.Type == ResolutionVisualEventType.Attack;
        // 02.1. O contador viajante é a única trajetória da resolução. Ataques
        // recebem impacto/fumaça; movimentações aliadas entram sem combate.
        if (isCombat)
            yield return AnimateImpact(root, destinationPosition, playerColor, context);
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
        else if (theme.PremiumGameplayFeedbackEnabled)
        {
            destination.GetComponent<TerritorioVisual>()?.DestacarDanoBreve();
            destination.GetComponentInChildren<WDArmyCounterView>(true)?.PulsoDano();
        }
        else
            destination.DestacarReforco();
        yield return context.Wait(theme.AttackResultDuration);

        Destroy(root.gameObject);
        activeRoot = null;
    }

    private IEnumerator AnimateImpact(RectTransform root, Vector2 center,
        Color playerColor, ResolutionVisualPresentationContext context)
    {
        const int particleCount = 12;
        var particles = new UnityEngine.UI.Image[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            Color smoke = Color.Lerp(new Color(.08f, .10f, .12f), playerColor, i == 0 ? .45f : .08f);
            particles[i] = ResolutionVisualMapUtility.CreateImage($"Impact_{i}", root, smoke, .76f);
            RectTransform rect = particles[i].rectTransform;
            rect.anchoredPosition = center;
            rect.sizeDelta = Vector2.one * (i < 3 ? 42f : 28f);
            WDHomeUIFactory.ApplyRounded(particles[i], 32);
        }

        float duration = Mathf.Max(.12f, theme.AttackImpactDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += context.PlaybackDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < particles.Length; i++)
            {
                float angle = (i / (float)particles.Length) * Mathf.PI * 2f + .35f;
                float distance = (34f + (i % 4) * 11f) * Mathf.Sin(t * Mathf.PI * .72f);
                particles[i].rectTransform.anchoredPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                particles[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(.65f, 2.15f, t);
                Color color = particles[i].color; color.a = (1f - t) * .76f; particles[i].color = color;
            }
            yield return null;
        }
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

    // ============================================================
    // 07. FASE 7.3A — EASING E LIMPEZA DE EFEITOS INTERROMPIDOS
    // ============================================================

    private static float EaseInOutCubic(float value)
    {
        float t = Mathf.Clamp01(value);
        return .5f - Mathf.Cos(t * Mathf.PI) * .5f;
    }

    private void OnDisable()
    {
        ResetTransientVisuals();
    }

    public void ResetTransientVisuals()
    {
        if (activeAttacker != null)
            Destroy(activeAttacker);
        if (activeRoot != null)
            Destroy(activeRoot);
        activeAttacker = null;
        activeRoot = null;
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

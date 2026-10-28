using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 00. AJUSTE OPCIONAL DO LABORATÓRIO — desativado preserva o tema oficial
// ============================================================
public enum WDAttackTravelEase
{
    Linear,
    SmoothStep,
    EaseOutCubic,
    EaseInOutCubic
}

public readonly struct WDAttackResolutionLabStyle
{
    public readonly bool Enabled;
    public readonly float TravelDuration, CurveFactor, MaxCurveHeight;
    public readonly float CounterScale, ImpactDuration, ImpactSize;
    public readonly float ImpactSpread, ImpactOpacity, ResultDuration;
    public readonly WDAttackTravelEase TravelEase;
    public readonly float ContactHoldDuration, ContactPressureIntensity;
    public readonly float ContactSeparation;

    public WDAttackResolutionLabStyle(bool enabled, float travelDuration,
        float curveFactor, float maxCurveHeight, float counterScale,
        float impactDuration, float impactSize, float impactSpread,
        float impactOpacity, float resultDuration,
        WDAttackTravelEase travelEase = WDAttackTravelEase.EaseInOutCubic,
        float contactHoldDuration = .12f,
        float contactPressureIntensity = .04f,
        float contactSeparation = .62f)
    {
        Enabled = enabled;
        TravelDuration = Mathf.Max(.05f, travelDuration);
        CurveFactor = Mathf.Clamp(curveFactor, -.5f, .5f);
        MaxCurveHeight = Mathf.Max(0f, maxCurveHeight);
        CounterScale = Mathf.Clamp(counterScale, .5f, 2f);
        ImpactDuration = Mathf.Max(.05f, impactDuration);
        ImpactSize = Mathf.Max(4f, impactSize);
        ImpactSpread = Mathf.Max(0f, impactSpread);
        ImpactOpacity = Mathf.Clamp01(impactOpacity);
        ResultDuration = Mathf.Max(0f, resultDuration);
        TravelEase = travelEase;
        ContactHoldDuration = Mathf.Max(0f, contactHoldDuration);
        ContactPressureIntensity = Mathf.Max(0f, contactPressureIntensity);
        ContactSeparation = Mathf.Max(0f, contactSeparation);
    }
}

public readonly struct WDResolutionOverlayLabStyle
{
    public readonly bool Enabled;
    public readonly WDConfirmedAttackStyle ArrowStyle;
    public readonly float ArrowDuration, ArrowSpeed, ArrowSyncOffset;
    public readonly float ConquestRevealDuration, ConquestRevealIntensity;

    public WDResolutionOverlayLabStyle(bool enabled,
        WDConfirmedAttackStyle arrowStyle, float arrowDuration,
        float arrowSpeed, float arrowSyncOffset,
        float conquestRevealDuration, float conquestRevealIntensity)
    {
        Enabled = enabled;
        ArrowStyle = arrowStyle;
        ArrowDuration = Mathf.Max(.05f, arrowDuration);
        ArrowSpeed = Mathf.Max(.05f, arrowSpeed);
        ArrowSyncOffset = Mathf.Clamp(arrowSyncOffset, -.5f, .5f);
        ConquestRevealDuration = Mathf.Max(.05f, conquestRevealDuration);
        ConquestRevealIntensity = Mathf.Clamp01(conquestRevealIntensity);
    }
}

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
    private PreparedActionArrowView activeResolutionArrow;
    private WDAttackResolutionLabStyle conqueredLabStyle;
    private WDAttackResolutionLabStyle defendedLabStyle;
    private WDAttackResolutionLabStyle movementLabStyle;
    private WDResolutionOverlayLabStyle resolutionOverlayLabStyle;
    private WDGameplayGlassStyle casualtyGlassStyle;
    private WDGameplayGlassStyle counterGlassStyle;
    private readonly List<Coroutine> casualtyAnimations = new();

    public void Configure(WarDominionUITheme newTheme)
    {
        theme = newTheme;
    }

    public void ConfigureLabStyles(WDAttackResolutionLabStyle conquered,
        WDAttackResolutionLabStyle defended,
        WDAttackResolutionLabStyle movement)
    {
        conqueredLabStyle = conquered;
        defendedLabStyle = defended;
        movementLabStyle = movement;
    }

    public void ConfigureResolutionOverlayLabStyle(
        WDResolutionOverlayLabStyle style) => resolutionOverlayLabStyle = style;

    // 01.1. GLASS PREMIUM 2.0 — opt-in exclusivo do laboratório.
    public void ConfigureLabGlassStyles(WDGameplayGlassStyle casualties,
        WDGameplayGlassStyle counters)
    {
        casualtyGlassStyle = casualties;
        counterGlassStyle = counters;
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
        WDAttackResolutionLabStyle labStyle = StyleFor(visualEvent);
        RectTransform root = ResolutionVisualMapUtility.CreateRoot(
            "AttackResolution", context.Overlay);
        activeRoot = root.gameObject;
        GameObject attacker = CreateCounterCopy(
            origin, ObterQuantidadeAnimada(visualEvent), visualEvent.Player,
            labStyle.Enabled);
        activeAttacker = attacker;
        attacker.GetComponent<WDArmyCounterView>()?.ConfigurarGlassLaboratorio(
            playerColor, counterGlassStyle);
        Vector3 attackerBaseScale = attacker.transform.localScale;
        RectTransform transportedIcon = null;
        Vector2 installationOrigin = default;
        Vector2 installationDestination = default;
        if (!string.IsNullOrEmpty(visualEvent.InstallationId))
            WDWeaponInstallationMapPresenter.TryBeginTransportTravel(
                visualEvent.InstallationId, context.Overlay, out transportedIcon,
                out installationOrigin, out installationDestination);
        Vector3 originWorld = GetCounterWorldPosition(origin);
        Vector3 destinationWorld = GetCounterWorldPosition(destination);
        Vector2 arrowStart = originPosition;
        Vector2 arrowDirection = destinationPosition - originPosition;
        if (arrowDirection.sqrMagnitude > .0001f)
            arrowStart += arrowDirection.normalized *
                theme.PreparedArrowEndpointInset;
        bool isCombat = visualEvent.Type == ResolutionVisualEventType.Attack;
        ClearResolutionArrow();
        if (labStyle.Enabled && resolutionOverlayLabStyle.Enabled)
        {
            var arrowObject = new GameObject("ResolutionAttackArrow",
                typeof(RectTransform));
            arrowObject.transform.SetParent(root, false);
            activeResolutionArrow =
                arrowObject.AddComponent<PreparedActionArrowView>();
            activeResolutionArrow.Build(theme);
            activeResolutionArrow.ConfigureConfirmedAttackStyle(
                resolutionOverlayLabStyle.ArrowStyle);
            // 02.1. A seta da resolucao nao reutiliza o badge numerico da
            // preparacao. A quantidade deslocada ja e representada pelo
            // contador viajante real, inclusive no movimento aliado.
            activeResolutionArrow.SetColorAndAmount(playerColor, 0);
            activeResolutionArrow.SetMode(WDPreparedActionVisualMode.ConfirmedAttack);
            activeResolutionArrow.SetCounterOcclusion(default, 0f, false);
            activeResolutionArrow.SetResolutionGeometry(
                arrowStart, arrowStart, arrowStart);
        }
        Vector3 travelEnd = isCombat && labStyle.Enabled
            ? ContactPoint(originWorld, destinationWorld,
                labStyle.ContactSeparation) : destinationWorld;
        Vector3 controlWorld = CreateWorldControl(originWorld, travelEnd,
            visualEvent);
        attacker.transform.position = originWorld;
        origin.DestacarReforco();
        float elapsed = 0f;
        float travelDuration = visualEvent.ContentId == WDSupportWeaponIds.AirTransport
            ? AirTransportTravelDuration : labStyle.Enabled
                ? labStyle.TravelDuration : theme.AttackTravelDuration;
        while (elapsed < travelDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / travelDuration);
            float smooth = labStyle.Enabled
                ? EvaluateTravelEase(progress, labStyle.TravelEase)
                : theme.PremiumGameplayFeedbackEnabled
                ? EaseInOutCubic(progress)
                : progress * progress * (3f - 2f * progress);
            labStyle = StyleFor(visualEvent);
            travelEnd = isCombat && labStyle.Enabled
                ? ContactPoint(originWorld, destinationWorld,
                    labStyle.ContactSeparation) : destinationWorld;
            controlWorld = CreateWorldControl(originWorld, travelEnd,
                visualEvent);
            attacker.transform.position = Bezier(
                originWorld, controlWorld, travelEnd, smooth);
            attacker.transform.localScale = attackerBaseScale *
                (labStyle.Enabled ? labStyle.CounterScale : 1f);
            UpdateSynchronizedResolutionArrow(activeResolutionArrow, attacker,
                context, arrowStart, originWorld, controlWorld, travelEnd,
                smooth);
            if (transportedIcon != null)
            {
                transportedIcon.anchoredPosition = Bezier(installationOrigin,
                    CreateOverlayControl(installationOrigin, installationDestination),
                    installationDestination, smooth);
            }
            yield return null;
        }

        // 02.1.1. MARCO FINAL COMPARTILHADO DOS ATAQUES — garante um frame
        // renderizado com contador e seta exatamente no contato. Somente no
        // frame seguinte a seta é retirada e a pausa de choque começa. O
        // movimento aliado conserva integralmente seu fluxo já aprovado.
        if (RequiresFinalSynchronizedAttackFrame(visualEvent, labStyle.Enabled) &&
            activeResolutionArrow != null)
        {
            labStyle = StyleFor(visualEvent);
            travelEnd = ContactPoint(originWorld, destinationWorld,
                labStyle.ContactSeparation);
            controlWorld = CreateWorldControl(originWorld, travelEnd,
                visualEvent);
            attacker.transform.position = travelEnd;
            attacker.transform.localScale = attackerBaseScale *
                labStyle.CounterScale;
            UpdateSynchronizedResolutionArrow(activeResolutionArrow, attacker,
                context, arrowStart, originWorld, controlWorld, travelEnd, 1f);
            yield return null;
        }

        // 02.1.2. A chegada do contador viajante encerra a seta. Contato,
        // fumaça, baixas, revelação e acomodação prosseguem sem esse objeto.
        ClearResolutionArrow();

        // 02.1. CONTATO FIRME — o contador para diante do defensor. A pressão
        // atua somente como compressão estática e nunca desloca o atacante para
        // dentro do contador nem cria ciclos de avanço/recuo.
        if (isCombat && labStyle.Enabled)
        {
            float contactElapsed = 0f;
            while (contactElapsed < labStyle.ContactHoldDuration)
            {
                contactElapsed += context.PlaybackDeltaTime;
                labStyle = StyleFor(visualEvent);
                Vector3 baseContact = ContactPoint(originWorld,
                    destinationWorld, labStyle.ContactSeparation);
                float safePressure = Mathf.Clamp(
                    labStyle.ContactPressureIntensity, 0f, .25f);
                attacker.transform.position = baseContact;
                attacker.transform.localScale = attackerBaseScale *
                    labStyle.CounterScale * (1f - safePressure);
                yield return null;
            }
        }

        Destroy(attacker);
        activeAttacker = null;
        if (transportedIcon != null) Destroy(transportedIcon.gameObject);
        // 02.2. RESULTADO DO LABORATÓRIO — números e proprietário aparecem
        // imediatamente depois do contato. A fumaça passa a ser acabamento e
        // não bloqueia mais a leitura do resultado já calculado.
        if (labStyle.Enabled)
        {
            if (visualEvent.Conquered && resolutionOverlayLabStyle.Enabled)
            {
                ApplyPreRevealVisualState(visualEvent);
                Collider2D destinationCollider =
                    destination.GetComponent<Collider2D>();
                Vector3 entry = destinationCollider != null
                    ? (Vector3)destinationCollider.ClosestPoint(originWorld)
                    : destinationWorld;
                destination.GetComponent<TerritorioVisual>()?
                    .RevealOwnerColorDirectional(visualEvent.NewOwner, entry,
                        destinationWorld - originWorld,
                        resolutionOverlayLabStyle.ConquestRevealDuration,
                        resolutionOverlayLabStyle.ConquestRevealIntensity);
            }
            else
                ApplyFinalVisualState(visualEvent);
        }

        // 02.2.1. BAIXAS REAIS — derivadas exclusivamente do snapshot de
        // combate. Os dois indicadores sobem em paralelo ao impacto.
        if (isCombat && labStyle.Enabled)
            StartCasualtyBadges(root, originPosition, destinationPosition,
                visualEvent, context);

        // 02.3. O contador viajante é a única trajetória da resolução. Ataques
        // recebem impacto/fumaça; movimentações aliadas entram sem combate.
        if (isCombat)
            yield return AnimateImpact(root, destinationPosition, playerColor,
                visualEvent, context);
        if (!labStyle.Enabled)
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
        // 02.4. Feedback territorial de resultado pertence somente ao combate.
        // Movimento aliado conclui com a soma visual do contador, sem fumaça,
        // dano ou qualquer linguagem de conquista.
        if (labStyle.Enabled)
        {
            if (isCombat && !visualEvent.Conquered)
                destination.GetComponentInChildren<WDArmyCounterView>(true)?
                    .PulsoDano();
            else if (!isCombat)
                destination.GetComponentInChildren<WDArmyCounterView>(true)?
                    .PulsoReforco();
            // 02.4.1. Na conquista, a propria revelacao direcional consolida a
            // cor. Nao ha segunda coroutine de glow, que causava flash e escala.
        }
        else if (ShouldPlayPostConquestReaction(labStyle.Enabled,
                     visualEvent.Conquered))
            destination.GetComponent<TerritorioVisual>()?.DestacarConquistaBreve();
        else if (theme.PremiumGameplayFeedbackEnabled)
        {
            destination.GetComponent<TerritorioVisual>()?.DestacarDanoBreve();
            destination.GetComponentInChildren<WDArmyCounterView>(true)?.PulsoDano();
        }
        else
            destination.DestacarReforco();
        labStyle = StyleFor(visualEvent);
        yield return context.Wait(labStyle.Enabled
            ? labStyle.ResultDuration : theme.AttackResultDuration);

        Destroy(root.gameObject);
        activeRoot = null;
    }

    private IEnumerator AnimateImpact(RectTransform root, Vector2 center,
        Color playerColor, ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        const int particleCount = 12;
        var particles = new UnityEngine.UI.Image[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            // 02.3.1. DEFESA NÃO MUDA IDENTIDADE — o impacto defendido usa
            // fumaça neutra. A cor do atacante só participa quando a conquista
            // efetivamente transfere a identidade do território.
            Color impactIdentity = visualEvent.Conquered
                ? playerColor : new Color(.42f, .48f, .54f, 1f);
            Color smoke = Color.Lerp(new Color(.055f, .07f, .085f),
                impactIdentity, i == 0 ? .34f : .07f);
            WDAttackResolutionLabStyle initial = StyleFor(visualEvent);
            float opacity = initial.Enabled ? initial.ImpactOpacity : .76f;
            particles[i] = ResolutionVisualMapUtility.CreateImage($"Impact_{i}", root, smoke, opacity);
            RectTransform rect = particles[i].rectTransform;
            rect.anchoredPosition = center;
            float size = initial.Enabled ? initial.ImpactSize : 42f;
            rect.sizeDelta = Vector2.one * (i < 3 ? size : size * .667f);
            WDHomeUIFactory.ApplyRounded(particles[i], 32);
        }

        WDAttackResolutionLabStyle style = StyleFor(visualEvent);
        float duration = Mathf.Max(.12f, style.Enabled
            ? style.ImpactDuration : theme.AttackImpactDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += context.PlaybackDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < particles.Length; i++)
            {
                float angle = (i / (float)particles.Length) * Mathf.PI * 2f + .35f;
                style = StyleFor(visualEvent);
                float spread = style.Enabled ? style.ImpactSpread : 34f;
                float opacity = style.Enabled ? style.ImpactOpacity : .76f;
                float size = style.Enabled ? style.ImpactSize : 42f;
                particles[i].rectTransform.sizeDelta = Vector2.one *
                    (i < 3 ? size : size * .667f);
                float distance = (spread + (i % 4) * spread * .324f) *
                    Mathf.Sin(t * Mathf.PI * .72f);
                particles[i].rectTransform.anchoredPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                particles[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(.65f, 2.15f, t);
                Color color = particles[i].color;
                color.a = (1f - t) * opacity;
                particles[i].color = color;
            }
            yield return null;
        }
    }

    private void StartCasualtyBadges(RectTransform root, Vector2 origin,
        Vector2 destination, ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        (int attackerLosses, int defenderLosses) =
            CalculateCasualties(visualEvent);
        Color attackerColor = context.GetPlayerColor(
            CasualtyOwnerFor(visualEvent, true));
        Color defenderColor = context.GetPlayerColor(
            CasualtyOwnerFor(visualEvent, false));
        if (attackerLosses > 0)
            casualtyAnimations.Add(StartCoroutine(AnimateCasualtyBadge(root,
                origin, attackerLosses, attackerColor,
                StyleFor(visualEvent).ImpactDuration, context)));
        if (defenderLosses > 0)
            casualtyAnimations.Add(StartCoroutine(AnimateCasualtyBadge(root,
                destination, defenderLosses, defenderColor,
                StyleFor(visualEvent).ImpactDuration, context)));
    }

    public static (int attacker, int defender) CalculateCasualties(
        ResolutionVisualEvent visualEvent)
    {
        if (visualEvent == null) return (0, 0);
        int attacker = Mathf.Max(0, visualEvent.Amount -
            (visualEvent.Conquered ? visualEvent.DestinationAfter : 0));
        int defender = Mathf.Max(0, visualEvent.DestinationBefore -
            (visualEvent.Conquered ? 0 : visualEvent.DestinationAfter));
        return (attacker, defender);
    }

    public static TerritorioClique.Dono CasualtyOwnerFor(
        ResolutionVisualEvent visualEvent, bool attacker) => attacker
            ? visualEvent.Player : visualEvent.PreviousOwner;

    private IEnumerator AnimateCasualtyBadge(RectTransform root, Vector2 start,
        int losses, Color casualtyOwnerColor, float configuredDuration,
        ResolutionVisualPresentationContext context)
    {
        // 03.1. BADGE PREMIUM — superfície compacta neutra e borda semântica
        // distinguem os lados do confronto sem reutilizar cores de propriedade.
        var badgeObject = new GameObject("Casualties",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(CanvasGroup));
        RectTransform badge = badgeObject.GetComponent<RectTransform>();
        badge.SetParent(root, false);
        badge.sizeDelta = new Vector2(58f, 30f);
        Image surface = badgeObject.GetComponent<Image>();
        surface.color = new Color(.025f, .04f, .055f, .88f);
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, 14);
        Outline outline = badgeObject.AddComponent<Outline>();
        outline.effectColor = new Color(.88f, .91f, .94f, .9f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        Outline shadow = badgeObject.AddComponent<Outline>();
        shadow.effectColor = new Color(0f, 0f, 0f, .32f);
        shadow.effectDistance = new Vector2(1.5f, -2f);
        shadow.useGraphicAlpha = true;
        var detailObject = new GameObject("RoleDetail", typeof(RectTransform),
            typeof(Image));
        RectTransform detail = detailObject.GetComponent<RectTransform>();
        detail.SetParent(badge, false);
        detail.anchorMin = new Vector2(.08f, .16f);
        detail.anchorMax = new Vector2(.13f, .84f);
        detail.offsetMin = detail.offsetMax = Vector2.zero;
        Image detailImage = detailObject.GetComponent<Image>();
        casualtyOwnerColor.a = .94f;
        detailImage.color = casualtyOwnerColor;
        detailImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(detailImage, 3);
        if (casualtyGlassStyle.Enabled)
        {
            surface.color = new Color(.025f, .04f, .055f,
                Mathf.Lerp(.84f, .96f, casualtyGlassStyle.ProfiledGlassIntensity));
            outline.effectColor = new Color(.84f, .88f, .92f,
                Mathf.Lerp(.68f, .98f, casualtyGlassStyle.ProfiledBevelStrength));
            shadow.effectColor = new Color(0f, 0f, 0f,
                .22f + casualtyGlassStyle.ProfiledGlowIntensity * .18f);
            var reflectionObject = new GameObject("GlassReflection",
                typeof(RectTransform), typeof(Image));
            RectTransform reflection = reflectionObject.GetComponent<RectTransform>();
            reflection.SetParent(badge, false);
            reflection.anchorMin = new Vector2(.14f, .58f);
            reflection.anchorMax = new Vector2(.88f, .84f);
            reflection.offsetMin = reflection.offsetMax = Vector2.zero;
            Image reflectionImage = reflectionObject.GetComponent<Image>();
            reflectionImage.color = new Color(1f, 1f, 1f,
                .15f * casualtyGlassStyle.ProfiledReflectionIntensity);
            reflectionImage.raycastTarget = false;
            WDHomeUIFactory.ApplyRounded(reflectionImage, 8);
        }
        TextMeshProUGUI label = ResolutionVisualMapUtility.CreateText(
            "Value", badge, theme.FonteDisplay, 18f, Color.white);
        label.text = "-" + losses;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        CanvasGroup group = badgeObject.GetComponent<CanvasGroup>();
        float duration = Mathf.Max(.55f, configuredDuration);
        for (float elapsed = 0f; elapsed < duration;
            elapsed += context.PlaybackDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            if (badge == null)
                yield break;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            badge.anchoredPosition = start +
                Vector2.up * Mathf.Lerp(18f, 48f, eased);
            badge.localScale = Vector3.one * Mathf.Lerp(1.10f, 1f,
                Mathf.Clamp01(progress / .22f));
            group.alpha = progress < .58f
                ? 1f : 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(.58f, 1f, progress));
            yield return null;
        }
        if (badge != null) Destroy(badge.gameObject);
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

    private static void ApplyPreRevealVisualState(
        ResolutionVisualEvent visualEvent)
    {
        if (ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.OriginTerritoryId, out TerritorioClique origin))
            origin.AplicarEstadoVisualResolucao(
                visualEvent.OriginAfter, visualEvent.Player);
        if (ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.DestinationTerritoryId,
                out TerritorioClique destination))
            destination.AplicarEstadoVisualResolucao(
                visualEvent.DestinationAfter, visualEvent.PreviousOwner);
    }

    // ============================================================
    // 05. ELEMENTOS ESPECÍFICOS DO ATAQUE
    // ============================================================

    private static GameObject CreateCounterCopy(
        TerritorioClique origin,
        int amount,
        TerritorioClique.Dono owner,
        bool premiumLabBadge)
    {
        ContadorTropas source = origin.GetComponentInChildren<ContadorTropas>(true);
        if (source == null)
            return new GameObject("AnimatedCounterCopy");

        GameObject copy = CriarRepresentacaoAnimada(
            source.transform, amount, owner);
        try
        {
            if (premiumLabBadge)
                copy.GetComponent<WDArmyCounterView>()?.AplicarEstiloBadgeViajante(
                    PaletaJogadores.ObterCorAtiva(owner));
            return copy;
        }
        catch
        {
            // 05.1. CONSTRUÇÃO ATÔMICA — um acabamento incompleto nunca pode
            // deixar um contador temporário órfão sobre o território original.
            if (copy != null)
            {
                if (Application.isPlaying) Destroy(copy);
                else DestroyImmediate(copy);
            }
            throw;
        }
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

    // A revelacao direcional do laboratório ja e o feedback completo de
    // conquista. O caminho historico oficial conserva sua reacao posterior.
    public static bool ShouldPlayPostConquestReaction(bool labEnabled,
        bool conquered) => conquered && !labEnabled;

    private static Vector3 GetCounterWorldPosition(TerritorioClique territory)
    {
        ContadorTropas counter = territory.GetComponentInChildren<ContadorTropas>(true);
        return counter != null ? counter.transform.position : territory.transform.position;
    }

    // 06.1. SETA E CONTADOR COMPARTILHAM O MESMO PROGRESSO — a ponta ocupa o
    // centro projetado do contador neste frame. A oclusão pela silhueta esconde
    // a ponta e o trecho final do corpo, sem projetar geometria à frente.
    private static void UpdateSynchronizedResolutionArrow(
        PreparedActionArrowView arrow,
        GameObject counter,
        ResolutionVisualPresentationContext context,
        Vector2 arrowStart,
        Vector3 worldOrigin,
        Vector3 worldControl,
        Vector3 worldDestination,
        float progress)
    {
        if (arrow == null || counter == null || context == null)
            return;
        SpriteRenderer[] renderers =
            counter.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
                continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }
        Camera camera = ResolutionVisualMapUtility.GetMapCamera();
        if (!hasBounds || !context.TryWorldToOverlay(bounds.center, camera,
                out Vector2 center))
        {
            arrow.SetResolutionGeometry(arrowStart, arrowStart, arrowStart);
            return;
        }

        Vector3[] cardinalWorld =
        {
            new(bounds.min.x, bounds.center.y, bounds.center.z),
            new(bounds.max.x, bounds.center.y, bounds.center.z),
            new(bounds.center.x, bounds.min.y, bounds.center.z),
            new(bounds.center.x, bounds.max.y, bounds.center.z)
        };
        var cardinalOverlay = new Vector2[4];
        for (int i = 0; i < cardinalWorld.Length; i++)
        {
            if (!context.TryWorldToOverlay(cardinalWorld[i], camera,
                    out cardinalOverlay[i]))
            {
                arrow.SetResolutionGeometry(arrowStart, arrowStart, arrowStart);
                return;
            }
        }
        float radius = CalculateCounterOcclusionRadius(center,
            cardinalOverlay[0], cardinalOverlay[1], cardinalOverlay[2],
            cardinalOverlay[3]);
        Vector3 tangentWorld = QuadraticTangent(worldOrigin, worldControl,
            worldDestination, progress);
        if (tangentWorld.sqrMagnitude < .0001f)
            tangentWorld = worldDestination - worldOrigin;
        Vector3 tangentSample = bounds.center + tangentWorld.normalized;
        if (!context.TryWorldToOverlay(tangentSample, camera,
                out Vector2 tangentOverlay))
        {
            arrow.SetResolutionGeometry(arrowStart, arrowStart, arrowStart);
            return;
        }
        Vector2 direction = tangentOverlay - center;
        if (direction.sqrMagnitude < .0001f)
            direction = center - arrowStart;
        direction.Normalize();
        float headLength = arrow.ResolutionHeadLength;
        Vector2 tip = CalculateOccludedArrowTip(center);
        arrow.SetCounterOcclusion(center, radius, true);

        // Enquanto o contador ainda cobre a origem, a seta permanece oculta.
        if (Vector2.Distance(arrowStart, center) <= radius + headLength + 4f)
        {
            arrow.SetResolutionGeometry(arrowStart, arrowStart, arrowStart);
            return;
        }
        Vector3 partialControlWorld = Vector3.Lerp(worldOrigin, worldControl,
            Mathf.Clamp01(progress));
        if (!context.TryWorldToOverlay(partialControlWorld, camera,
                out Vector2 partialControl))
            partialControl = Vector2.Lerp(arrowStart, tip, .5f);
        arrow.SetResolutionGeometry(arrowStart, partialControl, tip);
    }

    public static Vector3 QuadraticTangent(Vector3 origin, Vector3 control,
        Vector3 destination, float progress)
    {
        float t = Mathf.Clamp01(progress);
        return 2f * (1f - t) * (control - origin) +
            2f * t * (destination - control);
    }

    public static bool RequiresFinalSynchronizedAttackFrame(
        ResolutionVisualEvent visualEvent, bool labEnabled) =>
        labEnabled && visualEvent != null &&
        visualEvent.Type == ResolutionVisualEventType.Attack;

    public static Vector2 CalculateTrailingArrowTip(Vector2 counterCenter,
        Vector2 localDirection, float counterRadius)
    {
        Vector2 direction = localDirection.sqrMagnitude < .0001f
            ? Vector2.right : localDirection.normalized;
        return counterCenter - direction * Mathf.Max(0f, counterRadius);
    }

    // 06.1.1. O endpoint permanece geometricamente dentro do contador. O
    // recorte visual resolve a sobreposição; nenhum offset positivo pode fazer
    // a ponta ultrapassar a borda dianteira durante o deslocamento.
    public static Vector2 CalculateOccludedArrowTip(Vector2 counterCenter) =>
        counterCenter;

    // O badge é circular. O raio até os cantos dos bounds era 41% maior em um
    // quadrado e interrompia a rota antes da silhueta realmente visível.
    public static float CalculateCounterOcclusionRadius(Vector2 center,
        Vector2 left, Vector2 right, Vector2 bottom, Vector2 top)
    {
        float horizontal = Mathf.Max(Vector2.Distance(center, left),
            Vector2.Distance(center, right));
        float vertical = Mathf.Max(Vector2.Distance(center, bottom),
            Vector2.Distance(center, top));
        return Mathf.Max(0f, Mathf.Min(horizontal, vertical));
    }

    // 06.2. Autoridade única do ciclo de vida da seta. A desativação ocorre
    // antes da destruição para que ela suma no próprio marco de chegada.
    public static PreparedActionArrowView RetireResolutionArrow(
        PreparedActionArrowView arrow)
    {
        if (arrow == null)
            return null;
        GameObject arrowObject = arrow.gameObject;
        arrowObject.SetActive(false);
        if (Application.isPlaying)
            Destroy(arrowObject);
        else
            DestroyImmediate(arrowObject);
        return null;
    }

    private void ClearResolutionArrow()
    {
        activeResolutionArrow = RetireResolutionArrow(activeResolutionArrow);
    }

    private Vector3 CreateWorldControl(Vector3 origin, Vector3 destination,
        ResolutionVisualEvent visualEvent)
    {
        Vector3 delta = destination - origin;
        Vector3 normal = new Vector3(-delta.y, delta.x, 0f).normalized;
        WDAttackResolutionLabStyle style = StyleFor(visualEvent);
        float maxHeight = style.Enabled ? style.MaxCurveHeight
            : theme.AttackMaxCurveHeight;
        float factor = style.Enabled ? style.CurveFactor
            : theme.AttackCurveFactor;
        return (origin + destination) * 0.5f + normal * Mathf.Clamp(
            delta.magnitude * factor, -maxHeight, maxHeight);
    }

    private WDAttackResolutionLabStyle StyleFor(ResolutionVisualEvent value)
    {
        if (value.Type == ResolutionVisualEventType.Movement)
            return movementLabStyle;
        return value.Conquered ? conqueredLabStyle : defendedLabStyle;
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

    public static float EvaluateTravelEase(float value,
        WDAttackTravelEase ease)
    {
        float t = Mathf.Clamp01(value);
        return ease switch
        {
            WDAttackTravelEase.Linear => t,
            WDAttackTravelEase.SmoothStep => t * t * (3f - 2f * t),
            WDAttackTravelEase.EaseOutCubic => 1f - Mathf.Pow(1f - t, 3f),
            _ => EaseInOutCubic(t)
        };
    }

    public static Vector3 ContactPoint(Vector3 origin, Vector3 destination,
        float separation)
    {
        Vector3 delta = destination - origin;
        return delta.sqrMagnitude < .000001f ? destination
            : destination - delta.normalized * Mathf.Max(0f, separation);
    }

    private void OnDisable()
    {
        ResetTransientVisuals();
    }

    public void ResetTransientVisuals()
    {
        ClearResolutionArrow();
        foreach (Coroutine animation in casualtyAnimations)
            if (animation != null) StopCoroutine(animation);
        casualtyAnimations.Clear();
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

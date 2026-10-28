using System.Collections;
using UnityEngine;

public class TerritorioVisual : MonoBehaviour
{
    public const float OpacidadeOverlayMapaIntegral = 1f;
    private SpriteRenderer sr;
    private TerritorioClique territorio;
    private WarDominionUITheme temaGameplay;
    private Sprite spriteTerritorialOriginal;

    private static Material materialSemLuz;

    // ==============================================
    // SELEÇÃO VISUAL
    // ==============================================

    private bool selecionado = false;
    public bool TransferConfirmationHighlighted { get; private set; }
    private float SelectionBrightness => TransferConfirmationHighlighted
        ? .55f + .08f * Mathf.Sin(Time.unscaledTime * 4f)
        : labFeedbackEnabled
            ? EvaluateSelectionPulse(Time.unscaledTime - labSelectionPulseStart,
                labSelectionEntryDuration, labSelectionIntensity)
            : intensidadePulso;
    private bool labFeedbackEnabled;
    private float labSelectionIntensity = .42f;
    private float labSelectionEntryDuration = .10f;
    private float labReinforcementIntensity = .38f;
    private float labReinforcementDuration = .45f;
    private float labSelectionPulseStart = float.NegativeInfinity;
    private float labSelectionPulseEnd = float.NegativeInfinity;
    public float SelectionPulseEndTime => labSelectionPulseEnd;

    public void ConfigureLabFeedback(float selectionIntensity,
        float selectionDuration, float reinforcementIntensity,
        float reinforcementDuration)
    {
        labFeedbackEnabled = true;
        labSelectionIntensity = Mathf.Clamp01(selectionIntensity);
        labSelectionEntryDuration = Mathf.Max(.02f, selectionDuration);
        labReinforcementIntensity = Mathf.Clamp01(reinforcementIntensity);
        labReinforcementDuration = Mathf.Max(.05f, reinforcementDuration);
    }

    // ============================================================
    // 00.1. SELEÇÃO PERSISTENTE — um pulso curto converge para o brilho
    // estável enquanto o estado lógico continuar selecionado.
    // ============================================================
    public static float EvaluateSelectionPulse(float elapsed, float duration,
        float intensity)
    {
        float stable = Mathf.Clamp01(intensity);
        if (elapsed < 0f || duration <= 0f || elapsed >= duration)
            return stable;
        float progress = Mathf.Clamp01(elapsed / duration);
        float pulse = Mathf.Sin(progress * Mathf.PI);
        return Mathf.Lerp(stable, Mathf.Min(1f, stable * 1.55f), pulse);
    }

    // Parâmetro da seleção existente; não cria outra representação do território.
    public void SetTransferConfirmationHighlight(bool value)
    {
        TransferConfirmationHighlighted = value;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = SelectionRaised ? Color.Lerp(corBase, Color.white, SelectionBrightness) : corBase;
        if (selectionSprite != null) selectionSprite.color = sr != null ? sr.color : corBase;
    }

    [Header("Seleção")]
    [SerializeField]
    [Range(0.05f, 0.45f)]
    private float intensidadePulso = 0.30f;

    private Color corBase;
    private float fimDestaqueReforco;
    private bool destaqueReforcoAtivo;
    private bool destaqueConquistaAtivo;
    private Coroutine selectionScaleCoroutine;
    private Coroutine conquestGlowCoroutine;
    private Coroutine ownerRevealCoroutine;
    private GameObject ownerRevealRoot;
    private static Sprite revealMaskSprite;
    private Transform selectionVisualRoot;
    private SpriteRenderer selectionSprite;
    private Vector3 selectionPivotLocal;
    private GameObject conquestGlowObject;
    private float inicioDestaqueReforco;
    public bool SelectionRaised => selecionado;
    public float SelectionVisualScale => selectionVisualRoot != null
        ? selectionVisualRoot.localScale.x : 1f;
    public Vector3 SelectionVisualCenterWorld => selectionSprite != null
        ? selectionSprite.transform.TransformPoint(selectionPivotLocal)
        : SelectionCenterWorld();

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        territorio = GetComponent<TerritorioClique>();
        temaGameplay = Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");

        // =====================================================
        // 1. PILOTO REVERSÍVEL DO MÁRMORE B NO CLASSIC
        // =====================================================
        // O catálogo troca somente o sprite visual antes que seleção e
        // conquista criem suas cópias. O sprite serializado permanece guardado
        // para restaurar o território ao desabilitar o piloto ou o componente.
        spriteTerritorialOriginal = sr != null ? sr.sprite : null;
        AplicarSpriteExperimentalSeConfigurado();

        GarantirMaterialSemLuz();
        AtualizarCor();
    }

    private void OnEnable()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (territorio == null)
            territorio = GetComponent<TerritorioClique>();
        if (spriteTerritorialOriginal == null && sr != null)
            spriteTerritorialOriginal = sr.sprite;
        AplicarSpriteExperimentalSeConfigurado();
    }

    private void AplicarSpriteExperimentalSeConfigurado()
    {
        if (sr == null || territorio == null || spriteTerritorialOriginal == null)
            return;

        WDClassicTerritoryArtPassSettings settings =
            WDClassicTerritoryArtPassSettings.Load();
        string activeMapId = MapaAtivo.Instance?.Definicao?.MapaId;
        sr.sprite = settings != null
            ? settings.ResolveSprite(activeMapId, territorio.idTerritorio,
                spriteTerritorialOriginal)
            : spriteTerritorialOriginal;
    }

    private void OnDisable()
    {
        TransferConfirmationHighlighted = false;
        ClearSelectionVisual(true);
        if (conquestGlowObject != null)
            Destroy(conquestGlowObject);
        ClearOwnerReveal();
        conquestGlowCoroutine = null;
        destaqueReforcoAtivo = false;
        destaqueConquistaAtivo = false;
        labSelectionPulseStart = float.NegativeInfinity;
        labSelectionPulseEnd = float.NegativeInfinity;
        if (sr != null)
        {
            sr.enabled = true;
            sr.color = corBase;
            sr.sprite = spriteTerritorialOriginal;
        }
    }

    private void Update()
    {
        if (sr == null)
            return;

        bool premium = PremiumAtivo;
        bool reforcoAtivo = Time.unscaledTime < fimDestaqueReforco;
        if (!SelectionRaised && !reforcoAtivo)
        {
            if (destaqueReforcoAtivo)
            {
                destaqueReforcoAtivo = false;
                sr.color = corBase;
            }
            return;
        }

        Color corAtual = SelectionRaised
            ? Color.Lerp(corBase, Color.white, SelectionBrightness)
            : corBase;

        if (premium && SelectionRaised && selectionVisualRoot != null &&
            selectionScaleCoroutine == null)
        {
            float amplitude = MovimentoReduzido ? 0f : temaGameplay.TerritorySelectionBreath;
            float respiracao = Mathf.Sin(Time.unscaledTime *
                temaGameplay.TerritorySelectionBreathSpeed * Mathf.PI * 2f);
            selectionVisualRoot.localScale = Vector3.one *
                (temaGameplay.TerritorySelectionScale + amplitude * respiracao);
        }

        if (reforcoAtivo)
        {
            float duracao = labFeedbackEnabled ? labReinforcementDuration
                : premium ? temaGameplay.TerritoryReinforcementDuration
                : DuracaoDestaqueConquista;
            float progresso = Mathf.Clamp01((Time.unscaledTime - inicioDestaqueReforco) /
                Mathf.Max(0.01f, duracao));
            float restante = 1f - progresso;
            float brilho = destaqueConquistaAtivo
                ? Mathf.SmoothStep(0f, 1f, restante)
                : Mathf.Sin(progresso * Mathf.PI);
            corAtual = Color.Lerp(corAtual, Color.white,
                (labFeedbackEnabled ? labReinforcementIntensity
                    : premium ? 0.24f : destaqueConquistaAtivo ? 0.36f : 0.42f) * brilho);
        }

        sr.color = corAtual;
        if (selectionSprite != null)
            selectionSprite.color = corAtual;
    }

    private void GarantirMaterialSemLuz()
    {
        if (sr == null)
            return;

        if (materialSemLuz == null)
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                );

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Sprites/Default"
                    );
            }

            if (shader != null)
            {
                materialSemLuz =
                    new Material(shader);
            }
        }

        if (materialSemLuz != null)
        {
            sr.sharedMaterial =
                materialSemLuz;
        }
    }

    public void AtualizarCor()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (territorio == null)
            territorio =
                GetComponent<TerritorioClique>();

        if (sr == null ||
            territorio == null)
            return;

        GarantirMaterialSemLuz();

        corBase = ObterCorBaseTerritorial();

        if (!SelectionRaised)
        {
            sr.color = corBase;
        }
        else if (selectionSprite != null)
        {
            selectionSprite.color = Color.Lerp(
                corBase, Color.white, SelectionBrightness);
        }
    }

    public void AplicarDonoVisual(TerritorioClique.Dono donoVisual)
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        GarantirMaterialSemLuz();
        ClearSelectionVisual(true);
        selecionado = false;
        corBase = donoVisual == TerritorioClique.Dono.Neutro
            ? PaletaJogadores.ObterCorNeutraAtiva()
            : PaletaJogadores.ObterCorAtiva(donoVisual);
        corBase = AplicarOpacidadeDaRepresentacaoAtual(corBase);
        sr.color = corBase;
    }

    public void DestacarContinente(
        Color cor)
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (sr != null)
            sr.color = cor;
    }

    public void RestaurarCor()
    {
        TransferConfirmationHighlighted = false;
        ClearSelectionVisual(true);
        selecionado = false;

        AtualizarCor();
    }

    // 02.2. LIMPEZA DETERMINÍSTICA — o laboratório pode interromper uma
    // demonstração sem deixar corrotinas antigas restaurarem cor ou escala.
    public void ResetTransientFeedback()
    {
        ClearActiveGlow();
        ClearOwnerReveal();
        destaqueReforcoAtivo = false;
        destaqueConquistaAtivo = false;
        fimDestaqueReforco = 0f;
        labSelectionPulseStart = float.NegativeInfinity;
        labSelectionPulseEnd = float.NegativeInfinity;
        TransferConfirmationHighlighted = false;
        ClearSelectionVisual(true);
        selecionado = false;
        AtualizarCor();
    }

    // ============================================================
    // 02.3. REVELAÇÃO DIRECIONAL — overlay temporário recortado pela máscara
    // territorial; não altera sprite, material permanente ou collider.
    // ============================================================
    public void RevealOwnerColorDirectional(TerritorioClique.Dono newOwner,
        Vector3 entryWorld, Vector3 directionWorld, float duration,
        float intensity)
    {
        ClearOwnerReveal();
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null || !isActiveAndEnabled)
        {
            AplicarDonoVisual(newOwner);
            return;
        }
        ownerRevealCoroutine = StartCoroutine(AnimateOwnerReveal(newOwner,
            entryWorld, directionWorld, duration, intensity));
    }

    private IEnumerator AnimateOwnerReveal(TerritorioClique.Dono newOwner,
        Vector3 entryWorld, Vector3 directionWorld, float duration,
        float intensity)
    {
        ownerRevealRoot = new GameObject("DirectionalOwnerReveal");
        ownerRevealRoot.transform.SetParent(transform, false);
        var overlayObject = new GameObject("Color", typeof(SpriteRenderer));
        overlayObject.transform.SetParent(ownerRevealRoot.transform, false);
        SpriteRenderer overlay = overlayObject.GetComponent<SpriteRenderer>();
        overlay.sprite = sr.sprite;
        overlay.sharedMaterial = sr.sharedMaterial;
        overlay.sortingLayerID = sr.sortingLayerID;
        overlay.sortingOrder = sr.sortingOrder + 1;
        overlay.flipX = sr.flipX;
        overlay.flipY = sr.flipY;
        overlay.drawMode = sr.drawMode;
        overlay.size = sr.size;
        overlay.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        Color target = newOwner == TerritorioClique.Dono.Neutro
            ? PaletaJogadores.ObterCorNeutraAtiva()
            : PaletaJogadores.ObterCorAtiva(newOwner);
        target.a *= Mathf.Clamp01(intensity);
        overlay.color = target;

        var maskObject = new GameObject("SweepMask", typeof(SpriteMask));
        maskObject.transform.SetParent(ownerRevealRoot.transform, false);
        SpriteMask mask = maskObject.GetComponent<SpriteMask>();
        mask.sprite = GetRevealMaskSprite();
        mask.alphaCutoff = .01f;
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = sr.sortingLayerID;
        mask.backSortingLayerID = sr.sortingLayerID;
        mask.frontSortingOrder = overlay.sortingOrder;
        mask.backSortingOrder = overlay.sortingOrder - 1;

        Vector2 direction = transform.InverseTransformVector(directionWorld);
        if (direction.sqrMagnitude < .0001f)
            direction = Vector2.right;
        direction.Normalize();
        Vector2 normal = new(-direction.y, direction.x);
        Bounds bounds = sr.localBounds;
        Vector2[] corners = {
            new(bounds.min.x, bounds.min.y), new(bounds.max.x, bounds.min.y),
            new(bounds.max.x, bounds.max.y), new(bounds.min.x, bounds.max.y)
        };
        float minAlong = float.PositiveInfinity, maxAlong = float.NegativeInfinity;
        float minAcross = float.PositiveInfinity, maxAcross = float.NegativeInfinity;
        foreach (Vector2 corner in corners)
        {
            float along = Vector2.Dot(corner, direction);
            float across = Vector2.Dot(corner, normal);
            minAlong = Mathf.Min(minAlong, along);
            maxAlong = Mathf.Max(maxAlong, along);
            minAcross = Mathf.Min(minAcross, across);
            maxAcross = Mathf.Max(maxAcross, across);
        }
        Vector2 entry = transform.InverseTransformPoint(entryWorld);
        float start = Mathf.Clamp(Vector2.Dot(entry, direction),
            minAlong, maxAlong);
        float safeDuration = Mathf.Max(.05f, duration);
        for (float elapsed = 0f; elapsed < safeDuration;
            elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / safeDuration));
            ConfigureRevealMask(maskObject.transform, direction, normal,
                Mathf.Lerp(start, minAlong, progress),
                Mathf.Lerp(start, maxAlong, progress), minAcross, maxAcross);
            yield return null;
        }
        AplicarDonoVisual(newOwner);
        ClearOwnerReveal(false);
    }

    private static void ConfigureRevealMask(Transform mask, Vector2 direction,
        Vector2 normal, float minAlong, float maxAlong, float minAcross,
        float maxAcross)
    {
        float length = Mathf.Max(.001f, maxAlong - minAlong);
        float width = Mathf.Max(.001f, maxAcross - minAcross);
        Vector2 center = direction * ((minAlong + maxAlong) * .5f) +
            normal * ((minAcross + maxAcross) * .5f);
        mask.localPosition = new Vector3(center.x, center.y, -.01f);
        mask.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        mask.localScale = new Vector3(length, width + .05f, 1f);
    }

    private static Sprite GetRevealMaskSprite()
    {
        if (revealMaskSprite != null)
            return revealMaskSprite;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(new[] { Color.white, Color.white,
            Color.white, Color.white });
        texture.Apply(false, true);
        revealMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f),
            new Vector2(.5f, .5f), 2f);
        revealMaskSprite.hideFlags = HideFlags.HideAndDontSave;
        return revealMaskSprite;
    }

    private void ClearOwnerReveal(bool stopCoroutine = true)
    {
        if (stopCoroutine && ownerRevealCoroutine != null)
            StopCoroutine(ownerRevealCoroutine);
        ownerRevealCoroutine = null;
        if (ownerRevealRoot != null)
            Destroy(ownerRevealRoot);
        ownerRevealRoot = null;
    }

    public void DestacarSelecao()
    {
        bool previous = SelectionRaised;
        selecionado = true;
        if (!previous && labFeedbackEnabled)
        {
            labSelectionPulseStart = Time.unscaledTime;
            labSelectionPulseEnd = labSelectionPulseStart +
                labSelectionEntryDuration;
        }
        RefreshSelectionVisual(previous);
    }

    public void RemoverDestaqueSelecao()
    {
        TransferConfirmationHighlighted = false;
        bool previous = SelectionRaised;
        selecionado = false;
        RefreshSelectionVisual(previous);
    }

    private void RefreshSelectionVisual(bool previous)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (territorio == null) territorio = GetComponent<TerritorioClique>();
        if (sr == null || territorio == null) return;
        corBase = ObterCorBaseTerritorial();
        if (SelectionRaised)
        {
            EnsureSelectionVisual();
            if (!previous) AnimateSelectionScaleTo(
                PremiumAtivo ? temaGameplay.TerritorySelectionScale : 1.055f, false);
        }
        else if (previous) AnimateSelectionScaleTo(1f, true);
        AtualizarCor();
    }

    public void DestacarReforcoBreve()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (territorio == null)
            territorio = GetComponent<TerritorioClique>();
        if (sr == null || territorio == null)
            return;

        corBase = ObterCorBaseTerritorial();
        destaqueReforcoAtivo = true;
        destaqueConquistaAtivo = false;
        inicioDestaqueReforco = Time.unscaledTime;
        fimDestaqueReforco = Time.unscaledTime + (labFeedbackEnabled
            ? labReinforcementDuration : PremiumAtivo
            ? temaGameplay.TerritoryReinforcementDuration : DuracaoDestaqueConquista);
    }

    public void DestacarConquistaBreve()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (territorio == null)
            territorio = GetComponent<TerritorioClique>();
        if (sr == null || territorio == null)
            return;
        corBase = ObterCorBaseTerritorial();
        destaqueReforcoAtivo = true;
        destaqueConquistaAtivo = true;
        inicioDestaqueReforco = Time.unscaledTime;
        fimDestaqueReforco = Time.unscaledTime + DuracaoDestaqueConquista;
        ClearActiveGlow();
        conquestGlowCoroutine = StartCoroutine(AnimateConquestGlow(false));
    }

    public void DestacarConquistaRegiaoBreve()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (territorio == null)
            territorio = GetComponent<TerritorioClique>();
        if (sr == null || territorio == null)
            return;
        ClearActiveGlow();
        conquestGlowCoroutine = StartCoroutine(AnimateConquestGlow(true));
    }

    public const float DuracaoDestaqueConquista = 1.5f;

    // ============================================================
    // 03.1. FASE 7.3A — IMPACTO SEM ALTERAR O ESTADO TERRITORIAL
    // ============================================================

    public void DestacarDanoBreve()
    {
        if (!PremiumAtivo)
        {
            DestacarReforcoBreve();
            return;
        }

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        ClearActiveGlow();
        conquestGlowCoroutine = StartCoroutine(AnimateImpactGlow());
    }

    private void EnsureSelectionVisual()
    {
        if (selectionVisualRoot != null || sr == null)
            return;

        // As máscaras territoriais podem ocupar um retângulo do tamanho do mapa.
        // A geometria clicável é a fonte individual estável do centro do território.
        Collider2D territoryCollider = GetComponent<PolygonCollider2D>() ??
            GetComponent<Collider2D>();
        Vector3 localCenter = territoryCollider != null
            ? transform.InverseTransformPoint(territoryCollider.bounds.center)
            : sr.localBounds.center;
        selectionPivotLocal = localCenter;
        var root = new GameObject("SelectionScaleRoot");
        selectionVisualRoot = root.transform;
        selectionVisualRoot.SetParent(transform, false);
        selectionVisualRoot.localPosition = localCenter;

        var spriteObject = new GameObject("SelectionSprite");
        spriteObject.transform.SetParent(selectionVisualRoot, false);
        spriteObject.transform.localPosition = -localCenter;
        selectionSprite = spriteObject.AddComponent<SpriteRenderer>();
        selectionSprite.sprite = sr.sprite;
        selectionSprite.sharedMaterial = sr.sharedMaterial;
        selectionSprite.color = Color.Lerp(corBase, Color.white, SelectionBrightness);
        selectionSprite.sortingLayerID = sr.sortingLayerID;
        selectionSprite.sortingOrder = sr.sortingOrder;
        selectionSprite.flipX = sr.flipX;
        selectionSprite.flipY = sr.flipY;
        selectionSprite.drawMode = sr.drawMode;
        selectionSprite.size = sr.size;
        selectionSprite.tileMode = sr.tileMode;
        selectionSprite.spriteSortPoint = sr.spriteSortPoint;
        selectionSprite.maskInteraction = sr.maskInteraction;
        sr.enabled = false;
    }

    private void AnimateSelectionScaleTo(float targetMultiplier, bool cleanupAfter)
    {
        if (selectionVisualRoot == null)
            return;
        Vector3 target = Vector3.one * targetMultiplier;
        if (!Application.isPlaying)
        {
            selectionVisualRoot.localScale = target;
            if (cleanupAfter)
                ClearSelectionVisual(true);
            return;
        }
        if (selectionScaleCoroutine != null)
            StopCoroutine(selectionScaleCoroutine);
        selectionScaleCoroutine = StartCoroutine(
            AnimateSelectionScale(target, cleanupAfter));
    }

    private IEnumerator AnimateSelectionScale(Vector3 target, bool cleanupAfter)
    {
        float duration = labFeedbackEnabled ? labSelectionEntryDuration
            : PremiumAtivo
            ? (MovimentoReduzido ? 0.08f : temaGameplay.TerritorySelectionEntryDuration)
            : 0.20f;
        Vector3 start = selectionVisualRoot.localScale;
        Color startColor = selectionSprite != null
            ? selectionSprite.color : corBase;
        float elapsed = 0f;
        while (elapsed < duration && selectionVisualRoot != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            selectionVisualRoot.localScale = Vector3.LerpUnclamped(start, target, eased);
            if (cleanupAfter && selectionSprite != null)
                selectionSprite.color = Color.Lerp(startColor, corBase, eased);
            yield return null;
        }
        if (selectionVisualRoot != null)
            selectionVisualRoot.localScale = target;
        selectionScaleCoroutine = null;
        if (cleanupAfter)
            ClearSelectionVisual(false);
    }

    private void ClearSelectionVisual(bool immediate)
    {
        if (selectionScaleCoroutine != null)
            StopCoroutine(selectionScaleCoroutine);
        selectionScaleCoroutine = null;
        if (sr != null)
            sr.enabled = true;
        if (selectionVisualRoot != null)
        {
            GameObject root = selectionVisualRoot.gameObject;
            selectionVisualRoot = null;
            selectionSprite = null;
            selectionPivotLocal = Vector3.zero;
            if (immediate && !Application.isPlaying)
                DestroyImmediate(root);
            else
                Destroy(root);
        }
    }

    private IEnumerator AnimateConquestGlow(bool regional)
    {
        GameObject glowObject = new GameObject("ConquestGlow");
        conquestGlowObject = glowObject;
        glowObject.transform.SetParent(transform, false);
        SpriteRenderer glow = glowObject.AddComponent<SpriteRenderer>();
        glow.sprite = sr.sprite;
        glow.sharedMaterial = sr.sharedMaterial;
        glow.sortingLayerID = sr.sortingLayerID;
        glow.sortingOrder = sr.sortingOrder + 6;
        glow.flipX = sr.flipX;
        glow.flipY = sr.flipY;
        float elapsed = 0f;
        while (elapsed < DuracaoDestaqueConquista)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / DuracaoDestaqueConquista);
            if (PremiumAtivo)
            {
                int ciclos = regional && !MovimentoReduzido ? 2 : 1;
                float onda = Mathf.Sin(progress * Mathf.PI * ciclos);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float alpha = temaGameplay.TerritoryConquestOpacity *
                    Mathf.Max(0f, onda) * envelope;
                glow.color = new Color(1f, 0.82f, 0.34f, alpha);
                float escalaExtra = MovimentoReduzido ? 0.006f :
                    (temaGameplay.TerritoryConquestScale - 1f) *
                    (regional ? 1.35f : 1f);
                glowObject.transform.localScale = Vector3.one *
                    (1f + escalaExtra * EaseOutCubic(progress));
            }
            else
            {
                float pulse = (regional ? 0.94f : 0.32f) +
                    Mathf.Sin(progress * Mathf.PI * (regional ? 4f : 2f)) *
                    (regional ? 0.06f : 0.08f);
                glow.color = new Color(1f, regional ? 0.84f : 0.94f,
                    regional ? 0.20f : 0.48f, pulse * (1f - progress));
                glowObject.transform.localScale = regional
                    ? Vector3.one * (1.025f + Mathf.Sin(progress * Mathf.PI) * 0.035f)
                    : Vector3.one * 1.005f;
            }
            yield return null;
        }
        Destroy(glowObject);
        conquestGlowObject = null;
        conquestGlowCoroutine = null;
    }

    private IEnumerator AnimateImpactGlow()
    {
        GameObject glowObject = new GameObject("ImpactGlow");
        conquestGlowObject = glowObject;
        glowObject.transform.SetParent(transform, false);
        SpriteRenderer glow = glowObject.AddComponent<SpriteRenderer>();
        CopiarRenderizador(sr, glow, sr.sortingOrder + 7);
        float duration = MovimentoReduzido ? 0.08f : temaGameplay.TerritoryImpactDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float split = progress < 0.38f ? progress / 0.38f :
                (progress - 0.38f) / 0.62f;
            float scale = progress < 0.38f
                ? Mathf.Lerp(1f, MovimentoReduzido ? 0.985f : 0.965f, EaseInCubic(split))
                : Mathf.Lerp(MovimentoReduzido ? 0.985f : 0.965f, 1.012f,
                    EaseOutCubic(split));
            glowObject.transform.localScale = Vector3.one * scale;
            glow.color = new Color(1f, 0.86f, 0.78f,
                temaGameplay.TerritoryImpactOpacity * Mathf.Sin(progress * Mathf.PI));
            yield return null;
        }
        Destroy(glowObject);
        conquestGlowObject = null;
        conquestGlowCoroutine = null;
    }

    private static void CopiarRenderizador(SpriteRenderer origem,
        SpriteRenderer destino, int sortingOrder)
    {
        destino.sprite = origem.sprite;
        destino.sharedMaterial = origem.sharedMaterial;
        destino.sortingLayerID = origem.sortingLayerID;
        destino.sortingOrder = sortingOrder;
        destino.flipX = origem.flipX;
        destino.flipY = origem.flipY;
        destino.drawMode = origem.drawMode;
        destino.size = origem.size;
        destino.tileMode = origem.tileMode;
        destino.spriteSortPoint = origem.spriteSortPoint;
        destino.maskInteraction = origem.maskInteraction;
    }

    private bool PremiumAtivo => temaGameplay != null &&
        temaGameplay.PremiumGameplayFeedbackEnabled;
    private bool MovimentoReduzido => temaGameplay != null &&
        temaGameplay.ReducedGameplayMotion;
    private static float EaseOutCubic(float value) =>
        1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);
    private static float EaseInCubic(float value) =>
        Mathf.Pow(Mathf.Clamp01(value), 3f);

    private void ClearActiveGlow()
    {
        if (conquestGlowCoroutine != null)
            StopCoroutine(conquestGlowCoroutine);
        conquestGlowCoroutine = null;
        if (conquestGlowObject != null)
            Destroy(conquestGlowObject);
        conquestGlowObject = null;
    }

    private Color ObterCorBaseTerritorial()
    {
        if (territorio == null)
            return Color.clear;
        if (territorio.UsaOverlaySemDono)
            return AplicarOpacidadeDaRepresentacaoAtual(
                PaletaJogadores.ObterCorNeutraAtiva());

        Color color = PaletaJogadores.ObterCorAtiva(territorio.dono);
        return AplicarOpacidadeDaRepresentacaoAtual(color);
    }

    private static Color AplicarOpacidadeDaRepresentacaoAtual(Color color)
    {
        bool mapaIntegral16x9 = MapaAtivo.Instance != null &&
            LayoutPartida.EhArteIntegral16x9(MapaAtivo.Instance.Definicao);
        return CorTerritorialParaRepresentacao(color, mapaIntegral16x9);
    }

    public static Color CorTerritorialParaRepresentacao(
        Color color,
        bool mapaIntegral16x9)
    {
        color.a = mapaIntegral16x9 ? OpacidadeOverlayMapaIntegral : 1f;
        return color;
    }

    private Vector3 SelectionCenterWorld()
    {
        Collider2D territoryCollider = GetComponent<PolygonCollider2D>() ??
            GetComponent<Collider2D>();
        return territoryCollider != null
            ? territoryCollider.bounds.center
            : (sr != null ? sr.bounds.center : transform.position);
    }
}

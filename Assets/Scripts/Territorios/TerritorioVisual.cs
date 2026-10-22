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
        ? .55f + .08f * Mathf.Sin(Time.unscaledTime * 4f) : intensidadePulso;

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
        conquestGlowCoroutine = null;
        destaqueReforcoAtivo = false;
        destaqueConquistaAtivo = false;
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
            float duracao = premium ? temaGameplay.TerritoryReinforcementDuration
                : DuracaoDestaqueConquista;
            float progresso = Mathf.Clamp01((Time.unscaledTime - inicioDestaqueReforco) /
                Mathf.Max(0.01f, duracao));
            float restante = 1f - progresso;
            float brilho = destaqueConquistaAtivo
                ? Mathf.SmoothStep(0f, 1f, restante)
                : Mathf.Sin(progresso * Mathf.PI);
            corAtual = Color.Lerp(corAtual, Color.white,
                (premium ? 0.24f : destaqueConquistaAtivo ? 0.36f : 0.42f) * brilho);
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

    public void DestacarSelecao()
    {
        bool previous = SelectionRaised;
        selecionado = true;
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
        fimDestaqueReforco = Time.unscaledTime + (PremiumAtivo
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
        float duration = PremiumAtivo
            ? (MovimentoReduzido ? 0.08f : temaGameplay.TerritorySelectionEntryDuration)
            : 0.20f;
        Vector3 start = selectionVisualRoot.localScale;
        float elapsed = 0f;
        while (elapsed < duration && selectionVisualRoot != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            selectionVisualRoot.localScale = Vector3.LerpUnclamped(start, target, eased);
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

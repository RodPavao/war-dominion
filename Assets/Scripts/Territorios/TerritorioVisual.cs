using System.Collections;
using UnityEngine;

public class TerritorioVisual : MonoBehaviour
{
    public const float OpacidadeOverlayMapaIntegral = 1f;
    private SpriteRenderer sr;
    private TerritorioClique territorio;

    private static Material materialSemLuz;

    // ==============================================
    // SELEÇÃO VISUAL
    // ==============================================

    private bool selecionado = false;

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

        GarantirMaterialSemLuz();
        AtualizarCor();
    }

    private void OnDisable()
    {
        ClearSelectionVisual(true);
        if (conquestGlowObject != null)
            Destroy(conquestGlowObject);
        conquestGlowCoroutine = null;
    }

    private void Update()
    {
        if (sr == null)
            return;

        bool reforcoAtivo = Time.unscaledTime < fimDestaqueReforco;
        if (!selecionado && !reforcoAtivo)
        {
            if (destaqueReforcoAtivo)
            {
                destaqueReforcoAtivo = false;
                sr.color = corBase;
            }
            return;
        }

        Color corAtual = selecionado
            ? Color.Lerp(corBase, Color.white, intensidadePulso)
            : corBase;

        if (reforcoAtivo)
        {
            float restante = Mathf.Clamp01((fimDestaqueReforco - Time.unscaledTime) /
                DuracaoDestaqueConquista);
            float brilho = destaqueConquistaAtivo
                ? Mathf.SmoothStep(0f, 1f, restante)
                : Mathf.Sin(restante * Mathf.PI);
            corAtual = Color.Lerp(corAtual, Color.white,
                (destaqueConquistaAtivo ? 0.36f : 0.42f) * brilho);
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

        if (!selecionado)
        {
            sr.color = corBase;
        }
        else if (selectionSprite != null)
        {
            selectionSprite.color = Color.Lerp(
                corBase, Color.white, intensidadePulso);
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
        ClearSelectionVisual(true);
        selecionado = false;

        AtualizarCor();
    }

    public void DestacarSelecao()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (territorio == null)
            territorio =
                GetComponent<TerritorioClique>();

        if (sr == null ||
            territorio == null)
            return;

        bool newlySelected = !selecionado;
        corBase = ObterCorBaseTerritorial();
        selecionado = true;
        EnsureSelectionVisual();
        if (newlySelected)
            AnimateSelectionScaleTo(1.055f, false);
    }

    public void RemoverDestaqueSelecao()
    {
        bool wasSelected = selecionado;
        selecionado = false;
        if (wasSelected)
            AnimateSelectionScaleTo(1f, true);
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
        fimDestaqueReforco = Time.unscaledTime + DuracaoDestaqueConquista;
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
        selectionSprite.color = Color.Lerp(corBase, Color.white, intensidadePulso);
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
        const float duration = 0.20f;
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
            float pulse = (regional ? 0.94f : 0.32f) +
                Mathf.Sin(progress * Mathf.PI * (regional ? 4f : 2f)) *
                (regional ? 0.06f : 0.08f);
            glow.color = new Color(1f, regional ? 0.84f : 0.94f,
                regional ? 0.20f : 0.48f, pulse * (1f - progress));
            glowObject.transform.localScale = regional
                ? Vector3.one * (1.025f + Mathf.Sin(progress * Mathf.PI) * 0.035f)
                : Vector3.one * 1.005f;
            yield return null;
        }
        Destroy(glowObject);
        conquestGlowObject = null;
        conquestGlowCoroutine = null;
    }

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

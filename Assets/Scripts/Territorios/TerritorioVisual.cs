using UnityEngine;

public class TerritorioVisual : MonoBehaviour
{
    private SpriteRenderer sr;
    private TerritorioClique territorio;

    private static Material materialSemLuz;

    // ==============================================
    // SELEÇÃO VISUAL
    // ==============================================

    private bool selecionado = false;

    [Header("Seleção")]
    [SerializeField]
    private float velocidadePulso = 1.5f;

    [SerializeField]
    [Range(0.05f, 0.45f)]
    private float intensidadePulso = 0.30f;

    private Color corBase;
    private float fimDestaqueReforco;
    private bool destaqueReforcoAtivo;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        territorio = GetComponent<TerritorioClique>();

        GarantirMaterialSemLuz();
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

        // Pulso muito lento:
        // 0 -> 1 -> 0.
        float pulso =
            (
                Mathf.Sin(
                    Time.time *
                    velocidadePulso
                ) +
                1f
            ) * 0.5f;

        // Clareia suavemente a própria skin.
        Color corClara =
            Color.Lerp(
                corBase,
                Color.white,
                intensidadePulso
            );

        Color corAtual = selecionado
            ? Color.Lerp(corBase, corClara, pulso)
            : corBase;

        if (reforcoAtivo)
        {
            float brilho = Mathf.Sin(
                Mathf.Clamp01((fimDestaqueReforco - Time.unscaledTime) / 1.5f) * Mathf.PI);
            corAtual = Color.Lerp(corAtual, Color.white, 0.42f * brilho);
        }

        sr.color = corAtual;
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
    }

    public void AplicarDonoVisual(TerritorioClique.Dono donoVisual)
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        GarantirMaterialSemLuz();
        selecionado = false;
        corBase = donoVisual == TerritorioClique.Dono.Neutro
            ? PaletaJogadores.ObterCorNeutraAtiva()
            : PaletaJogadores.ObterCorAtiva(donoVisual);
        if (donoVisual != TerritorioClique.Dono.Neutro)
            corBase.a = 1f;
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

        corBase = Color.Lerp(ObterCorBaseTerritorial(), Color.white, 0.3f);

        selecionado = true;
    }

    public void RemoverDestaqueSelecao()
    {
        selecionado = false;

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
        fimDestaqueReforco = Time.unscaledTime + 1.5f;
    }

    private Color ObterCorBaseTerritorial()
    {
        if (territorio == null)
            return Color.clear;
        if (territorio.UsaOverlaySemDono)
            return PaletaJogadores.ObterCorNeutraAtiva();

        Color color = PaletaJogadores.ObterCorAtiva(territorio.dono);
        color.a = 1f;
        return color;
    }
}

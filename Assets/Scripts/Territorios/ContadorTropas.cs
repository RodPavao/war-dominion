using UnityEngine;
using TMPro;
using System.Collections;

[SelectionBase]
public class ContadorTropas : MonoBehaviour
{
    private TerritorioClique territorio;

    public TerritorioClique Territorio => territorio;

    private SpriteRenderer fundo;
    private SpriteRenderer borda;
    private SpriteRenderer bordaExterna;

    private TextMeshPro numero;
    private WDArmyCounterView novaVisualizacao;

    private Vector3 escalaOriginal;
    private Coroutine animacaoAtual;
    private GameObject feedbackReforcoAtual;

    // =====================================================
    // 1. TAMANHO VISUAL E ÁREA CLICÁVEL
    // =====================================================

    // 0.80 = todos os contadores ficam 20% menores.
    // NÃO altera o tamanho da área clicável.
    private const float escalaVisualGlobal = 0.80f;

    // Área clicável maior que o desenho visual.
    private const float tamanhoCollider = 0.82f;

    // =====================================================
    // 2. ESTADO DO CLIQUE LONGO
    // =====================================================

    private bool pressionando = false;
    private bool cliqueLongoExecutado = false;

    private float inicioPressao;

    private const float tempoCliqueLongo = 1f;

    // =====================================================
    // 3. VALIDAÇÃO NO EDITOR
    // =====================================================

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        territorio =
            GetComponentInParent<TerritorioClique>();

        Transform bordaExistente =
            transform.Find("Borda");

        Transform fundoExistente =
            transform.Find("Fundo");

        Transform numeroExistente =
            transform.Find("Numero");

        if (territorio == null ||
            bordaExistente == null ||
            fundoExistente == null ||
            numeroExistente == null)
            return;

        borda =
            bordaExistente.GetComponent<SpriteRenderer>();

        fundo =
            fundoExistente.GetComponent<SpriteRenderer>();

        numero =
            numeroExistente.GetComponent<TextMeshPro>();

        AplicarEscalaVisual();

        GarantirBordaExterna(
            bordaExistente
        );

        GarantirCollider();

        Atualizar();
    }

    // =====================================================
    // 4. CONFIGURAÇÃO
    // =====================================================

    public void Configurar(
        TerritorioClique territorioAlvo)
    {
        territorio = territorioAlvo;

        CriarVisual();

        AplicarEscalaVisual();

        GarantirNovaVisualizacao();

        escalaOriginal =
            transform.localScale;

        Atualizar();
    }

    // =====================================================
    // 5. DETECÇÃO DO HOLD
    // =====================================================

    private void Update()
    {
        if (!pressionando)
            return;

        if (cliqueLongoExecutado)
            return;

        if (GameManager.instance == null)
            return;

        if (!GameManager.instance.PodeEditarPreparacao)
            return;

        if (Time.unscaledTime - inicioPressao >=
            tempoCliqueLongo)
        {
            cliqueLongoExecutado = true;

            DespejarTodosReforcos();
        }
    }

    // =====================================================
    // 6. CONSTRUÇÃO VISUAL
    // =====================================================

    private void CriarVisual()
    {
        Transform bordaExistente =
            transform.Find("Borda");

        Transform fundoExistente =
            transform.Find("Fundo");

        Transform numeroExistente =
            transform.Find("Numero");

        if (bordaExistente != null &&
            fundoExistente != null &&
            numeroExistente != null)
        {
            borda =
                bordaExistente.GetComponent<SpriteRenderer>();

            fundo =
                fundoExistente.GetComponent<SpriteRenderer>();

            numero =
                numeroExistente.GetComponent<TextMeshPro>();

            GarantirBordaExterna(
                bordaExistente
            );

            GarantirCollider();

            return;
        }

        // =================================================
        // BORDA INTERNA
        // =================================================

        GameObject objBorda =
            new GameObject("Borda");

        objBorda.transform.SetParent(
            transform,
            false
        );

        borda =
            objBorda.AddComponent<SpriteRenderer>();

        borda.sprite =
            CriarSpriteArredondado();

        borda.sortingOrder = 20;

        objBorda.transform.localScale =
            new Vector3(
                0.82f,
                0.82f,
                1f
            );

        GarantirBordaExterna(
            objBorda.transform
        );

        // =================================================
        // FUNDO
        // =================================================

        GameObject objFundo =
            new GameObject("Fundo");

        objFundo.transform.SetParent(
            transform,
            false
        );

        fundo =
            objFundo.AddComponent<SpriteRenderer>();

        fundo.sprite =
            CriarSpriteArredondado();

        fundo.color =
            new Color(
                0.012f,
                0.012f,
                0.015f,
                1f
            );

        fundo.sortingOrder = 21;

        objFundo.transform.localScale =
            new Vector3(
                0.68f,
                0.68f,
                1f
            );

        // =================================================
        // NÚMERO
        // =================================================

        GameObject objNumero =
            new GameObject("Numero");

        objNumero.transform.SetParent(
            transform,
            false
        );

        numero =
            objNumero.AddComponent<TextMeshPro>();

        TMP_FontAsset fonteNumero =
            Resources.Load<TMP_FontAsset>(
                "Fonts & Materials/BebasNeue-Regular SDF"
            );

        if (fonteNumero != null)
        {
            numero.font = fonteNumero;
        }
        else
        {
            Debug.LogError(
                "Fonte Bebas Neue SDF não encontrada."
            );
        }

        numero.fontStyle =
            FontStyles.Normal;

        numero.fontWeight =
            FontWeight.Medium;

        numero.color =
            Color.white;

        numero.alignment =
            TextAlignmentOptions.Center;

        numero.verticalAlignment =
            VerticalAlignmentOptions.Middle;

        numero.enableAutoSizing = false;

        numero.overflowMode =
            TextOverflowModes.Overflow;

        numero.extraPadding = true;
        numero.margin = Vector4.zero;
        numero.sortingOrder = 22;

        RectTransform rectNumero =
            objNumero.GetComponent<RectTransform>();

        rectNumero.sizeDelta =
            new Vector2(
                0.82f,
                0.82f
            );

        rectNumero.localPosition =
            Vector3.zero;

        GarantirCollider();
    }

    // =====================================================
    // 7. ESCALA VISUAL GLOBAL
    // =====================================================

    private void AplicarEscalaVisual()
    {
        Transform bordaTransform =
            transform.Find("Borda");

        Transform fundoTransform =
            transform.Find("Fundo");

        Transform numeroTransform =
            transform.Find("Numero");

        if (bordaTransform != null)
        {
            bordaTransform.localScale =
                new Vector3(
                    0.82f * escalaVisualGlobal,
                    0.82f * escalaVisualGlobal,
                    1f
                );
        }

        if (fundoTransform != null)
        {
            fundoTransform.localScale =
                new Vector3(
                    0.68f * escalaVisualGlobal,
                    0.68f * escalaVisualGlobal,
                    1f
                );
        }

        if (numeroTransform != null)
        {
            numeroTransform.localScale =
                new Vector3(
                    escalaVisualGlobal,
                    escalaVisualGlobal,
                    1f
                );
        }

        if (bordaTransform != null)
        {
            GarantirBordaExterna(
                bordaTransform
            );
        }
    }

    // =====================================================
    // 8. BORDA EXTERNA
    // =====================================================

    private void GarantirBordaExterna(
        Transform bordaTransform)
    {
        if (bordaTransform == null)
            return;

        SpriteRenderer bordaAtual =
            bordaTransform.GetComponent<SpriteRenderer>();

        if (bordaAtual == null)
            return;

        Transform existente =
            transform.Find("BordaExterna");

        GameObject obj;

        if (existente == null)
        {
            obj =
                new GameObject(
                    "BordaExterna"
                );

            obj.transform.SetParent(
                transform,
                false
            );
        }
        else
        {
            obj = existente.gameObject;
        }

        bordaExterna =
            obj.GetComponent<SpriteRenderer>();

        if (bordaExterna == null)
        {
            bordaExterna =
                obj.AddComponent<SpriteRenderer>();
        }

        bordaExterna.sprite =
            bordaAtual.sprite;

        bordaExterna.color =
            Color.black;

        bordaExterna.sortingOrder =
            bordaAtual.sortingOrder - 1;

        obj.transform.localPosition =
            bordaTransform.localPosition;

        obj.transform.localRotation =
            bordaTransform.localRotation;

        Vector3 escalaBorda =
            bordaTransform.localScale;

        obj.transform.localScale =
            new Vector3(
                escalaBorda.x * 1.10f,
                escalaBorda.y * 1.10f,
                escalaBorda.z
            );
    }

    // =====================================================
    // 9. COLLIDER
    // =====================================================

    private void GarantirCollider()
    {
        BoxCollider2D colliderContador =
            GetComponent<BoxCollider2D>();

        if (colliderContador == null)
        {
            colliderContador =
                gameObject.AddComponent<BoxCollider2D>();
        }

        colliderContador.size =
            new Vector2(
                tamanhoCollider,
                tamanhoCollider
            );

        colliderContador.offset =
            Vector2.zero;
    }

    // =====================================================
    // 10. ATUALIZAÇÃO
    // =====================================================

    public void Atualizar()
    {
        if (territorio == null ||
            numero == null ||
            borda == null)
            return;

        AtualizarVisual(territorio.Tropas, territorio.dono);
    }

    public void AtualizarVisual(
        int quantidade,
        TerritorioClique.Dono donoVisual)
    {
        if (novaVisualizacao != null)
        {
            bool estadoSemDono = donoVisual == TerritorioClique.Dono.Neutro ||
                (territorio != null && territorio.UsaOverlaySemDono);
            Color corFaixa = estadoSemDono
                ? PaletaJogadores.ObterCorNeutraAtiva()
                : PaletaJogadores.ObterCorAtiva(donoVisual);
            novaVisualizacao.Exibir(quantidade, corFaixa);
            return;
        }

        if (numero == null || borda == null)
            return;

        numero.text = Mathf.Max(0, quantidade).ToString();

        int quantidadeDigitos =
            numero.text.Length;

        if (quantidadeDigitos == 1)
            numero.fontSize = 4.0f;
        else if (quantidadeDigitos == 2)
            numero.fontSize = 3.7f;
        else
            numero.fontSize = 3.1f;

        numero.ForceMeshUpdate();

        CentralizarMalhaNumero();

        // O arco é vazado: a camada territorial/skin abaixo fornece a cor.
        // O contador preserva somente moldura, fundo central, número e hitbox.
        borda.color = Color.clear;

        if (bordaExterna != null)
        {
            bordaExterna.color =
                Color.black;
        }
    }

    // =====================================================
    // 11. ADAPTADOR PARA A NOVA VISUALIZAÇÃO INDEPENDENTE
    // =====================================================

    private void GarantirNovaVisualizacao()
    {
        novaVisualizacao = GetComponent<WDArmyCounterView>();
        if (novaVisualizacao == null)
            novaVisualizacao = gameObject.AddComponent<WDArmyCounterView>();

        novaVisualizacao.Configurar(territorio);

        if (borda != null)
            borda.enabled = false;
        if (fundo != null)
            fundo.enabled = false;
        if (bordaExterna != null)
            bordaExterna.enabled = false;
        if (numero != null)
            numero.enabled = false;
    }

    // =====================================================
    // 12. INPUT CENTRALIZADO
    // =====================================================
    //
    // IMPORTANTE:
    // OnMouseDown / OnMouseUp / OnMouseExit foram removidos.
    //
    // Agora TODO clique/touch é detectado pelo InputPartida
    // e encaminhado diretamente para estes métodos.
    // =====================================================

    public void IniciarPressaoInput()
    {
        if (territorio == null ||
            GameManager.instance == null)
            return;

        pressionando = true;

        cliqueLongoExecutado = false;

        inicioPressao =
            Time.unscaledTime;
    }

    public void FinalizarPressaoInput()
    {
        if (!pressionando)
            return;

        pressionando = false;

        if (territorio == null ||
            GameManager.instance == null)
            return;

        // Hold de 1 segundo já executou.
        // Soltar não pode executar clique curto também.
        if (cliqueLongoExecutado)
        {
            cliqueLongoExecutado = false;
            return;
        }

        ExecutarCliqueNormal();
    }

    public void CancelarPressaoInput()
    {
        pressionando = false;

        cliqueLongoExecutado = false;
    }

    // =====================================================
    // 12. CLIQUE NORMAL
    // =====================================================

    private void ExecutarCliqueNormal()
    {
        GameManager gm =
            GameManager.instance;

        if (gm == null ||
            territorio == null)
            return;

        if (gm.PodeAdicionarReforcoEm(territorio))
        {
            int tropasAntes =
                territorio.Tropas;

            gm.TentarAdicionarReforco(
                territorio
            );

            if (territorio.Tropas >
                tropasAntes)
            {
                AnimarAdicao(territorio.Tropas - tropasAntes);
            }

            return;
        }

        if (gm.PodeEditarPreparacao)
        {
            gm.ClicarTerritorio(
                territorio
            );
        }
    }

    // Mantido por compatibilidade com possíveis
    // componentes/scripts antigos.
    public void Clicar()
    {
        ExecutarCliqueNormal();
    }

    // =====================================================
    // 13. DISTRIBUIÇÃO POR HOLD
    // =====================================================

    private void DespejarTodosReforcos()
    {
        GameManager gm =
            GameManager.instance;

        if (gm == null ||
            territorio == null)
            return;

        if (!gm.PodeAdicionarReforcoEm(territorio))
            return;

        int quantidade =
            gm.reforcosDisponiveis;

        if (quantidade <= 0)
            return;

        int quantidadeAplicada =
            gm.DistribuirReforcos(
                territorio,
                quantidade
            );

        if (quantidadeAplicada <= 0)
            return;

        Atualizar();

        AnimarAdicao(quantidadeAplicada);
    }

    // =====================================================
    // 14. ANIMAÇÃO
    // =====================================================

    private void AnimarAdicao(int quantidade = 1)
    {
        territorio?.DestacarReforco();

        if (animacaoAtual != null)
        {
            StopCoroutine(
                animacaoAtual
            );
        }

        if (feedbackReforcoAtual != null)
            Destroy(feedbackReforcoAtual);

        animacaoAtual =
            StartCoroutine(
                AnimacaoAdicao(quantidade)
            );
    }

    private IEnumerator AnimacaoAdicao(int quantidade)
    {
        GameObject feedbackObjeto =
            new GameObject("FeedbackReforco");

        feedbackReforcoAtual = feedbackObjeto;

        feedbackObjeto.transform.SetParent(transform, false);
        feedbackObjeto.transform.localPosition =
            new Vector3(0f, 0.35f, -0.1f);

        TextMeshPro feedback =
            feedbackObjeto.AddComponent<TextMeshPro>();

        feedback.text = "+" + Mathf.Max(1, quantidade);
        feedback.fontSize = 2.5f;
        feedback.alignment = TextAlignmentOptions.Center;
        feedback.color = Color.white;
        feedback.sortingOrder = 40;

        float duracao = 1.5f;
        float tempo = 0f;

        Vector3 escalaMaior =
            escalaOriginal * 1.14f;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;

            float progresso =
                tempo / duracao;

            float curva =
                Mathf.Sin(
                    progresso * Mathf.PI
                );

            transform.localScale =
                Vector3.Lerp(
                    escalaOriginal,
                    escalaMaior,
                    curva
                );

            feedbackObjeto.transform.localPosition =
                new Vector3(
                    0f,
                    Mathf.Lerp(0.35f, 0.95f, progresso),
                    -0.1f
                );

            Color corFeedback = feedback.color;
            corFeedback.a = 1f - progresso;
            feedback.color = corFeedback;

            yield return null;
        }

        transform.localScale =
            escalaOriginal;

        Destroy(feedbackObjeto);
        feedbackReforcoAtual = null;

        animacaoAtual = null;
    }

    // =====================================================
    // 15. CENTRALIZAÇÃO DO NÚMERO
    // =====================================================

    private void CentralizarMalhaNumero()
    {
        TMP_TextInfo info =
            numero.textInfo;

        if (info == null ||
            info.characterCount == 0)
            return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        bool encontrou = false;

        for (int i = 0;
             i < info.characterCount;
             i++)
        {
            TMP_CharacterInfo caractere =
                info.characterInfo[i];

            if (!caractere.isVisible)
                continue;

            encontrou = true;

            int indiceVertice =
                caractere.vertexIndex;

            int indiceMaterial =
                caractere.materialReferenceIndex;

            Vector3[] vertices =
                info.meshInfo[
                    indiceMaterial
                ].vertices;

            for (int j = 0; j < 4; j++)
            {
                Vector3 vertice =
                    vertices[
                        indiceVertice + j
                    ];

                minX =
                    Mathf.Min(
                        minX,
                        vertice.x
                    );

                maxX =
                    Mathf.Max(
                        maxX,
                        vertice.x
                    );

                minY =
                    Mathf.Min(
                        minY,
                        vertice.y
                    );

                maxY =
                    Mathf.Max(
                        maxY,
                        vertice.y
                    );
            }
        }

        if (!encontrou)
            return;

        Vector3 centro =
            new Vector3(
                (minX + maxX) / 2f,
                (minY + maxY) / 2f,
                0f
            );

        for (int i = 0;
             i < info.characterCount;
             i++)
        {
            TMP_CharacterInfo caractere =
                info.characterInfo[i];

            if (!caractere.isVisible)
                continue;

            int indiceVertice =
                caractere.vertexIndex;

            int indiceMaterial =
                caractere.materialReferenceIndex;

            Vector3[] vertices =
                info.meshInfo[
                    indiceMaterial
                ].vertices;

            for (int j = 0; j < 4; j++)
            {
                vertices[
                    indiceVertice + j
                ] -= centro;
            }
        }

        numero.UpdateVertexData(
            TMP_VertexDataUpdateFlags.Vertices
        );
    }

    // =====================================================
    // 16. SPRITE DO CONTADOR
    // =====================================================

    private Sprite CriarSpriteArredondado()
    {
        const int tamanho = 64;
        const float raio = 19f;

        Texture2D textura =
            new Texture2D(
                tamanho,
                tamanho
            );

        textura.filterMode =
            FilterMode.Bilinear;

        Color transparente =
            new Color(
                0f,
                0f,
                0f,
                0f
            );

        for (int y = 0;
             y < tamanho;
             y++)
        {
            for (int x = 0;
                 x < tamanho;
                 x++)
            {
                float px =
                    Mathf.Max(
                        Mathf.Abs(
                            x - tamanho / 2f
                        ) -
                        (
                            tamanho / 2f -
                            raio
                        ),
                        0f
                    );

                float py =
                    Mathf.Max(
                        Mathf.Abs(
                            y - tamanho / 2f
                        ) -
                        (
                            tamanho / 2f -
                            raio
                        ),
                        0f
                    );

                bool dentro =
                    px * px +
                    py * py
                    <= raio * raio;

                textura.SetPixel(
                    x,
                    y,
                    dentro
                        ? Color.white
                        : transparente
                );
            }
        }

        textura.Apply();

        return Sprite.Create(
            textura,
            new Rect(
                0,
                0,
                tamanho,
                tamanho
            ),
            new Vector2(
                0.5f,
                0.5f
            ),
            100f
        );
    }
}

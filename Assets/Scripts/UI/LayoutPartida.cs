using UnityEngine;

public class LayoutPartida : MonoBehaviour
{
    public static LayoutPartida instance;

    // =====================================================
    // CONFIGURAÇÃO
    // =====================================================

    [Header("Painéis laterais")]

    [Range(0.05f, 0.20f)]
    public float larguraPainelEsquerdo = 0.10f;

    [Range(0.05f, 0.20f)]
    public float larguraPainelDireito = 0.10f;

    [Tooltip("Reserva mínima de cada painel lateral em pixels de referência.")]
    [Range(240f, 340f)]
    [SerializeField] private float larguraMinimaHudPixels = 280f;


    [Header("Mapa")]

    [Tooltip("1 = tamanho normal. Acima de 1 aproxima.")]
    [Range(0.90f, 1.10f)]
    public float zoomMapa = 1.00f;


    // =====================================================
    // ESTADO
    // =====================================================

    private Camera cameraPrincipal;
    public static Camera CameraRuntime =>
        instance != null && instance.cameraPrincipal != null
            ? instance.cameraPrincipal
            : null;

    private Rect rectOriginal;
    private float sizeOriginal;
    private Vector3 posicaoOriginal;
    private Color corOriginal;
    private float larguraEsquerdaEfetiva;
    private float larguraDireitaEfetiva;

    private bool configurado = false;


    // =====================================================
    // INICIALIZAÇÃO
    // =====================================================

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        EncontrarCamera();
    }


    private void Start()
    {
        AplicarLayout();
    }


    // Recalcula se a resolução/tamanho da janela mudar.
    private void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying && configurado)
        {
            AplicarLayout();
        }
    }


    // =====================================================
    // CÂMERA
    // =====================================================

    private void EncontrarCamera()
    {
        cameraPrincipal = Camera.main;

        if (cameraPrincipal == null)
            cameraPrincipal = FindAnyObjectByType<Camera>();

        if (cameraPrincipal == null)
        {
            Debug.LogError(
                "LAYOUT | Main Camera não encontrada."
            );
        }
    }


    // =====================================================
    // APLICAR LAYOUT
    // =====================================================

    public void AplicarLayout()
    {
        if (cameraPrincipal == null)
        {
            EncontrarCamera();

            if (cameraPrincipal == null)
                return;
        }


        if (!configurado)
        {
            rectOriginal = cameraPrincipal.rect;

            sizeOriginal =
                cameraPrincipal.orthographicSize;

            posicaoOriginal =
                cameraPrincipal.transform.position;

            corOriginal =
                cameraPrincipal.backgroundColor;

            configurado = true;
        }


        float escalaCanvas = Screen.width > 0 && Screen.height > 0
            ? Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f))
            : 1f;

        float larguraMinimaHUD = Screen.width > 0
            ? Mathf.Clamp((larguraMinimaHudPixels * escalaCanvas) / Screen.width, 0.06f, 0.20f)
            : 0.15f;

        larguraEsquerdaEfetiva =
            Mathf.Max(larguraPainelEsquerdo, larguraMinimaHUD);

        larguraDireitaEfetiva =
            Mathf.Max(larguraPainelDireito, larguraMinimaHUD);

        float larguraCentral =
            1f
            - larguraEsquerdaEfetiva
            - larguraDireitaEfetiva;


        larguraCentral =
            Mathf.Clamp(
                larguraCentral,
                0.60f,
                0.90f
            );


        // Mapas WD 16:9 são o fundo integral; o HUD já está em Screen Space.
        // Mapas legados preservam temporariamente o viewport central anterior.
        bool fundoIntegral16x9 = MapaAtivo.Instance != null &&
            EhArteIntegral16x9(MapaAtivo.Instance.Definicao);
        cameraPrincipal.rect = CalcularViewportMapa(fundoIntegral16x9,
            larguraEsquerdaEfetiva, larguraCentral);


        cameraPrincipal.backgroundColor =
            new Color(
                0.005f,
                0.005f,
                0.008f,
                1f
            );


        AjustarMapaPelaAltura();
    }

    public static Rect CalcularViewportMapa(bool fundoIntegral16x9,
        float margemEsquerda, float larguraCentral) => fundoIntegral16x9
        ? new Rect(0f, 0f, 1f, 1f)
        : new Rect(margemEsquerda, 0f, larguraCentral, 1f);

    public static bool EhArteIntegral16x9(DefinicaoMapa mapa) =>
        mapa != null && mapa.ArteBase != null && mapa.ArteBase.rect.height > 0f &&
        Mathf.Abs(mapa.ArteBase.rect.width / mapa.ArteBase.rect.height -
            16f / 9f) < 0.001f;

    public static float TamanhoOrtograficoNormalizado(
        Vector2 tamanhoMapa,
        float aspectoViewport) =>
        Mathf.Max(tamanhoMapa.y * 0.5f,
            tamanhoMapa.x / (2f * Mathf.Max(aspectoViewport, 0.01f)));


    // =====================================================
    // MAPA - ALTURA TEM PRIORIDADE
    // =====================================================

    private void AjustarMapaPelaAltura()
    {
        if (MapaAtivo.Instance == null ||
            !MapaAtivo.Instance.TentarObterLimitesVisuais(out Bounds limitesMapa))
        {
            Debug.LogError(
                "LAYOUT | Visual do mapa ativo não encontrado."
            );

            return;
        }


        // =================================================
        // REGRA PRINCIPAL
        //
        // O mapa ocupa o maior tamanho possível sem recorte, considerando
        // o aspect ratio real do viewport reservado entre os painéis.
        // =================================================

        float alturaMapa =
            limitesMapa.size.y;


        Rect viewportPixels = cameraPrincipal.pixelRect;
        float aspectoViewport = viewportPixels.height > 0f
            ? viewportPixels.width / viewportPixels.height
            : cameraPrincipal.aspect;

        float tamanhoPelaAltura = alturaMapa / 2f;
        float tamanhoPelaLargura = limitesMapa.size.x /
            (2f * Mathf.Max(aspectoViewport, 0.01f));

        float tamanhoCamera = TamanhoOrtograficoNormalizado(
            limitesMapa.size, aspectoViewport);
        string eixoLimitante = tamanhoPelaLargura > tamanhoPelaAltura
            ? "largura"
            : "altura";


        tamanhoCamera /=
            Mathf.Max(
                zoomMapa,
                0.01f
            );

        MapaAtivo.Instance.RegistrarTamanhoOrtograficoEnquadrado(tamanhoCamera);

        cameraPrincipal.orthographicSize =
            tamanhoCamera;


        // =================================================
        // 04. CENTRALIZAÇÃO E OFFSET DATA-DRIVEN
        //
        // O centro da arte permanece como comportamento padrão.
        // Cada definição pode ajustar somente a composição visual,
        // sem mover mapa, territórios, colliders ou contadores.
        // =================================================

        Vector3 centroMapa =
            limitesMapa.center;

        Vector2 deslocamentoEnquadramento =
            MapaAtivo.Instance.Definicao != null
                ? MapaAtivo.Instance.Definicao.DeslocamentoEnquadramento
                : Vector2.zero;


        cameraPrincipal.transform.position =
            new Vector3(
                centroMapa.x + deslocamentoEnquadramento.x,
                centroMapa.y + deslocamentoEnquadramento.y,
                posicaoOriginal.z
            );


        Debug.Log(
            "LAYOUT | Mapa maximizado sem recorte | " +
            "Orthographic Size: " +
            tamanhoCamera.ToString("0.00") +
            " | Offset: " +
            deslocamentoEnquadramento.ToString("F2") +
            " | Painéis: " +
            Mathf.RoundToInt(
                larguraEsquerdaEfetiva * 100f
            ) +
            "% / " +
            Mathf.RoundToInt(
                larguraDireitaEfetiva * 100f
            ) +
            "%"
            + " | Limite: " + eixoLimitante +
            " | Viewport px: " +
            Mathf.RoundToInt(viewportPixels.width) + "x" +
            Mathf.RoundToInt(viewportPixels.height)
        );
    }


    // =====================================================
    // RESTAURAR
    // =====================================================

    public void RestaurarLayout()
    {
        if (!configurado ||
            cameraPrincipal == null)
            return;


        cameraPrincipal.rect =
            rectOriginal;


        cameraPrincipal.orthographicSize =
            sizeOriginal;


        cameraPrincipal.transform.position =
            posicaoOriginal;


        cameraPrincipal.backgroundColor =
            corOriginal;
    }
}

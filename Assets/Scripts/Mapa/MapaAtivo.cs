using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MapaAtivo : MonoBehaviour
{
    // ============================================================
    // 1. CICLO DE VIDA E ESTADO DO MAPA ATIVO
    // ============================================================

    private const string CaminhoCatalogo = "Mapas/CatalogoMapas";
    public const string ChaveMostrarNomesTerritorios =
        "WarGame.Visualizacao.MostrarNomesTerritorios";
    public const float ProporcaoZoomParaExibirNomes = 0.72f;
#if UNITY_EDITOR
    public const string ChaveMapaTesteEditor = "WarGame.MapaSelecionadoEditor";
#endif
    private static MapaAtivo instance;

    [SerializeField] private DefinicaoMapa definicao;

    private readonly List<TerritorioClique> territoriosRuntime = new List<TerritorioClique>();
    private readonly Dictionary<string, TerritorioClique> territorioPorId = new Dictionary<string, TerritorioClique>(StringComparer.Ordinal);
    private readonly Dictionary<string, DefinicaoRegiaoMapa> regiaoPorId = new Dictionary<string, DefinicaoRegiaoMapa>(StringComparer.Ordinal);
    private SpriteRenderer visualMapa;
    private float tamanhoOrtograficoEnquadrado;
    private WDTerritoryNameRuntimeOverlay territoryNameOverlay;

    public static MapaAtivo Instance => instance != null ? instance : null;
    public DefinicaoMapa Definicao => definicao;
    public IReadOnlyList<TerritorioClique> TerritoriosRuntime => territoriosRuntime;
    public static bool NomesTerritoriaisHabilitados =>
        PlayerPrefs.GetInt(ChaveMostrarNomesTerritorios, 0) != 0;

    public static void DefinirNomesTerritoriaisHabilitados(bool habilitados)
    {
        PlayerPrefs.SetInt(ChaveMostrarNomesTerritorios, habilitados ? 1 : 0);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CriarServico()
    {
        if (instance != null)
            return;

        GameObject objeto = new GameObject("MapaAtivo");
        DontDestroyOnLoad(objeto);
        instance = objeto.AddComponent<MapaAtivo>();

        CatalogoMapas catalogo = Resources.Load<CatalogoMapas>(CaminhoCatalogo);
        if (catalogo == null || catalogo.MapaPadrao == null)
        {
            Debug.LogError($"Catálogo de mapas ausente ou sem mapa padrão em Resources/{CaminhoCatalogo}.");
            return;
        }

        instance.definicao = ObterDefinicaoInicial(catalogo);
    }

    private static DefinicaoMapa ObterDefinicaoInicial(CatalogoMapas catalogo)
    {
#if UNITY_EDITOR
        string mapaIdSelecionado = PlayerPrefs.GetString(
            ChaveMapaTesteEditor,
            catalogo.MapaPadrao.MapaId);
        foreach (DefinicaoMapa mapa in catalogo.Mapas)
        {
            if (mapa != null && mapa.MapaId == mapaIdSelecionado)
                return mapa;
        }
#endif
        return catalogo.MapaPadrao;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        GarantirOverlayNomesTerritoriais();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        AtualizarReferenciasDaCena();
    }

    private void Start()
    {
        AtualizarReferenciasDaCena();
    }

    public void AtualizarReferenciasDaCena()
    {
        territoriosRuntime.Clear();
        territorioPorId.Clear();
        regiaoPorId.Clear();
        visualMapa = null;

        if (!SincronizarDefinicaoComCena() || definicao == null)
            return;

        foreach (DefinicaoRegiaoMapa regiao in definicao.Regioes)
        {
            if (regiao != null && !string.IsNullOrWhiteSpace(regiao.id))
                regiaoPorId[regiao.id] = regiao;
        }

        TerritorioClique[] encontrados = FindObjectsByType<TerritorioClique>();
        foreach (TerritorioClique territorio in encontrados)
        {
            if (territorio == null || string.IsNullOrWhiteSpace(territorio.idTerritorio))
                continue;
            territorioPorId[territorio.idTerritorio] = territorio;
        }

        foreach (DefinicaoTerritorioMapa dados in definicao.Territorios)
        {
            if (dados != null && territorioPorId.TryGetValue(dados.id, out TerritorioClique territorio))
                territoriosRuntime.Add(territorio);
        }

        SpriteRenderer[] visuais = FindObjectsByType<SpriteRenderer>();
        foreach (SpriteRenderer candidato in visuais)
        {
            if (candidato.sprite == definicao.ArteBase)
            {
                visualMapa = candidato;
                break;
            }
        }

        AplicarApresentacaoTerritorialDaDefinicao();
        GarantirOverlayNomesTerritoriais().RebuildForMap(this);

        if (territoriosRuntime.Count != definicao.Territorios.Count)
            Debug.LogError($"Mapa {definicao.MapaId}: esperados {definicao.Territorios.Count} territórios, encontrados {territoriosRuntime.Count} na cena.");
    }

    private void AplicarApresentacaoTerritorialDaDefinicao()
    {
        if (visualMapa == null || definicao == null)
            return;

        foreach (DefinicaoTerritorioMapa dados in definicao.Territorios)
        {
            if (dados == null ||
                !territorioPorId.TryGetValue(dados.id, out TerritorioClique territorio))
                continue;

            Transform contador = territorio.transform.Find("ContadorModerno");
            if (contador != null && dados.possuiPosicaoContador)
            {
                Vector2 local = ConverterPosicaoArteParaLocalTerritorio(
                    visualMapa.transform, territorio.transform, dados.posicaoContador);
                contador.localPosition = new Vector3(local.x, local.y,
                    contador.localPosition.z);
            }

        }
    }

    public void AtualizarVisibilidadeNomesTerritoriais()
    {
        GarantirOverlayNomesTerritoriais().RefreshVisibility();
    }

    public static Vector2 ConverterPosicaoArteParaLocalTerritorio(
        Transform arte, Transform territorio, Vector2 posicaoNaArte)
    {
        if (arte == null || territorio == null)
            return posicaoNaArte;
        Vector3 world = arte.TransformPoint(posicaoNaArte);
        return territorio.InverseTransformPoint(world);
    }

    public static Vector3 ConverterPosicaoNomeManualParaMundo(Transform arte,
        Transform territorio, Vector2 posicaoSalva, bool mapaIntegral16x9)
    {
        if (mapaIntegral16x9 && arte != null)
            return arte.TransformPoint(posicaoSalva);
        return territorio != null
            ? territorio.TransformPoint(posicaoSalva)
            : (Vector3)posicaoSalva;
    }

    public static Vector3 ResolverPosicaoNomeManualParaMundo(Transform arte,
        Transform territorio, Vector2 posicaoSalva, bool mapaIntegral16x9)
    {
        if (!mapaIntegral16x9 || arte == null || territorio == null)
            return ConverterPosicaoNomeManualParaMundo(arte, territorio,
                posicaoSalva, mapaIntegral16x9);

        Vector3 pelaArte = arte.TransformPoint(posicaoSalva);
        Vector3 peloTerritorio = territorio.TransformPoint(posicaoSalva);
        Collider2D collider = territorio.GetComponent<Collider2D>();
        if (collider == null)
            return pelaArte;

        float distanciaArte = Vector2.Distance(pelaArte, collider.ClosestPoint(pelaArte));
        float distanciaTerritorio = Vector2.Distance(peloTerritorio,
            collider.ClosestPoint(peloTerritorio));
        return distanciaTerritorio + 0.0001f < distanciaArte
            ? peloTerritorio
            : pelaArte;
    }

    public static bool PosicaoNomeManualUsaEspacoDaArte(Transform arte,
        Transform territorio, Vector2 posicaoSalva, bool mapaIntegral16x9)
    {
        if (!mapaIntegral16x9 || arte == null || territorio == null)
            return false;
        Vector3 resolvida = ResolverPosicaoNomeManualParaMundo(arte, territorio,
            posicaoSalva, true);
        return Vector2.SqrMagnitude(resolvida - arte.TransformPoint(posicaoSalva)) <
            0.000001f;
    }

    public static bool ZoomPermiteExibirNomes(Camera camera, DefinicaoMapa mapa)
    {
        return ZoomPermiteExibirNomes(camera, mapa, 0f);
    }

    public static bool ZoomPermiteExibirNomes(Camera camera, DefinicaoMapa mapa,
        float tamanhoOrtograficoEnquadrado)
    {
        return camera != null && mapa != null &&
            camera.orthographicSize <= ResolverTamanhoBaseDoZoom(
                mapa, tamanhoOrtograficoEnquadrado) *
                ProporcaoZoomParaExibirNomes;
    }

    public static float ResolverTamanhoBaseDoZoom(DefinicaoMapa mapa,
        float tamanhoOrtograficoEnquadrado)
    {
        return tamanhoOrtograficoEnquadrado > 0f
            ? tamanhoOrtograficoEnquadrado
            : (mapa != null ? mapa.TamanhoOrtografico : 0f);
    }

    public void RegistrarTamanhoOrtograficoEnquadrado(float tamanho)
    {
        tamanhoOrtograficoEnquadrado = Mathf.Max(0f, tamanho);
        if (territoryNameOverlay != null)
            territoryNameOverlay.SetInitialFramingSize(
                TamanhoOrtograficoEnquadrado);
    }

    public float TamanhoOrtograficoEnquadrado => ResolverTamanhoBaseDoZoom(
        definicao, tamanhoOrtograficoEnquadrado);

    public Vector3 ConverterDeslocamentoArteParaMundo(Vector2 deslocamento)
    {
        return visualMapa != null
            ? visualMapa.transform.TransformVector(deslocamento)
            : (Vector3)deslocamento;
    }

    public Vector3 ConverterPosicaoArteParaMundo(Vector2 posicao)
    {
        return visualMapa != null
            ? visualMapa.transform.TransformPoint(posicao)
            : (Vector3)posicao;
    }

    public Vector2 ConverterPosicaoMundoParaArte(Vector3 posicao)
    {
        return visualMapa != null
            ? (Vector2)visualMapa.transform.InverseTransformPoint(posicao)
            : (Vector2)posicao;
    }

    public Transform TransformArte => visualMapa != null
        ? visualMapa.transform
        : null;

    public static Vector3 ConverterDeslocamentoArteParaMundo(Transform arte,
        Vector2 deslocamento)
    {
        return arte != null ? arte.TransformVector(deslocamento) :
            (Vector3)deslocamento;
    }

    private WDTerritoryNameRuntimeOverlay GarantirOverlayNomesTerritoriais()
    {
        if (territoryNameOverlay == null)
            territoryNameOverlay = GetComponent<WDTerritoryNameRuntimeOverlay>() ??
                gameObject.AddComponent<WDTerritoryNameRuntimeOverlay>();
        return territoryNameOverlay;
    }

    private bool SincronizarDefinicaoComCena()
    {
        CatalogoMapas catalogo = Resources.Load<CatalogoMapas>(CaminhoCatalogo);
        if (catalogo == null)
            return false;

        SpriteRenderer[] visuais = FindObjectsByType<SpriteRenderer>();
        foreach (DefinicaoMapa mapa in catalogo.Mapas)
        {
            if (mapa == null || mapa.ArteBase == null)
                continue;
            if (!Array.Exists(visuais, renderer => renderer != null && renderer.sprite == mapa.ArteBase))
                continue;

            if (definicao != mapa)
            {
                definicao = mapa;
                Debug.Log($"[MapaAtivo] Definição sincronizada com a cena: {mapa.MapaId} | {mapa.Territorios.Count} territórios | {mapa.Conexoes.Count} conexões.");
            }
            return true;
        }

        return false;
    }

    // ============================================================
    // 2. CONSULTAS UNIVERSAIS DE TERRITÓRIOS E CONEXÕES
    // ============================================================

    public static IReadOnlyList<TerritorioClique> ObterTerritoriosOuCena()
    {
        if (instance != null)
        {
            if (instance.territoriosRuntime.Count == 0)
                instance.AtualizarReferenciasDaCena();
            if (instance.territoriosRuntime.Count > 0)
                return instance.territoriosRuntime;
        }

        return FindObjectsByType<TerritorioClique>();
    }

    public bool TentarObterTerritorio(string id, out TerritorioClique territorio)
    {
        return territorioPorId.TryGetValue(id, out territorio);
    }

    public DefinicaoTerritorioMapa ObterDefinicaoTerritorio(string id)
    {
        if (definicao == null)
            return null;
        foreach (DefinicaoTerritorioMapa territorio in definicao.Territorios)
            if (territorio != null && territorio.id == id)
                return territorio;
        return null;
    }

    public DefinicaoRegiaoMapa ObterRegiao(string regiaoId)
    {
        regiaoPorId.TryGetValue(regiaoId, out DefinicaoRegiaoMapa regiao);
        return regiao;
    }

    public bool SaoConectados(string origemId, string destinoId, TipoConexaoMapa tipo)
    {
        return SaoConectados(definicao, origemId, destinoId, tipo);
    }

    public static bool SaoConectados(DefinicaoMapa mapa, string origemId, string destinoId, TipoConexaoMapa tipo)
    {
        if (mapa == null)
            return false;
        foreach (DefinicaoConexaoMapa conexao in mapa.Conexoes)
        {
            if (conexao == null || conexao.tipo != tipo)
                continue;
            if (conexao.origemId == origemId && conexao.destinoId == destinoId)
                return true;
            if (conexao.bidirecional && conexao.origemId == destinoId && conexao.destinoId == origemId)
                return true;
        }
        return false;
    }

    public bool JogadorControlaRegiao(TerritorioClique.Dono jogador, DefinicaoRegiaoMapa regiao)
    {
        if (regiao == null || regiao.territorioIds == null || regiao.territorioIds.Count == 0)
            return false;
        foreach (string territorioId in regiao.territorioIds)
            if (!territorioPorId.TryGetValue(territorioId, out TerritorioClique territorio) || territorio.dono != jogador)
                return false;
        return true;
    }

    // ============================================================
    // 3. REGIÕES, BÔNUS E ENQUADRAMENTO
    // ============================================================

    public int CalcularBonusRegioes(TerritorioClique.Dono jogador)
    {
        if (definicao == null)
            return 0;
        return CalcularBonusRegioesControladas(
            definicao.Regioes, id => territorioPorId.TryGetValue(
                id, out TerritorioClique territorio) && territorio.dono == jogador);
    }

    public static int CalcularBonusRegioesControladas(
        IEnumerable<DefinicaoRegiaoMapa> regioes,
        Func<string, bool> controlaTerritorio)
    {
        if (regioes == null || controlaTerritorio == null)
            return 0;
        int total = 0;
        foreach (DefinicaoRegiaoMapa regiao in regioes)
        {
            if (regiao?.territorioIds == null || regiao.territorioIds.Count == 0)
                continue;
            bool completa = true;
            foreach (string territorioId in regiao.territorioIds)
                if (!controlaTerritorio(territorioId))
                {
                    completa = false;
                    break;
                }
            if (completa)
                total += Mathf.Max(0, regiao.bonus);
        }
        return total;
    }

    public bool TentarObterLimitesVisuais(out Bounds limites)
    {
        if (visualMapa == null)
            AtualizarReferenciasDaCena();

        if (visualMapa != null)
        {
            limites = visualMapa.bounds;
            return true;
        }
        limites = default;
        return false;
    }

    // ============================================================
    // 8. BOUNDS JOGÁVEIS PARA ENQUADRAMENTO RESPONSIVO
    // ============================================================

    public bool TentarObterLimitesJogaveis(out Bounds limites)
    {
        if (territoriosRuntime.Count == 0)
            AtualizarReferenciasDaCena();

        bool encontrou = false;
        limites = default;
        foreach (TerritorioClique territorio in territoriosRuntime)
        {
            if (territorio == null)
                continue;

            Collider2D collider = territorio.GetComponent<Collider2D>();
            Bounds atual = collider != null
                ? collider.bounds
                : new Bounds(territorio.transform.position, Vector3.zero);
            if (!encontrou)
            {
                limites = atual;
                encontrou = true;
            }
            else
            {
                limites.Encapsulate(atual.min);
                limites.Encapsulate(atual.max);
            }
        }
        return encontrou;
    }
}

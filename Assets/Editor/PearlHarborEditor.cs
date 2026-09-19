#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PearlHarborEditor
{
    // ============================================================
    // 1. CAMINHOS E IDENTIDADE DO MAPA
    // ============================================================

    private const string MapaId = "pearl_harbor";
    private const string CenaBase = "Assets/WarGame.unity";
    private const string PastaMapa = "Assets/Mapas/Pearl Harbor";
    private const string PastaMascaras = PastaMapa + "/Mascaras";
    private const string ArtePath = PastaMapa + "/Arte/PearlHarbor.png";
    private const string GabaritoPath = PastaMapa + "/Arte/PearlHarbor_GabaritoTerritorios.png";
    private const string CenaPath = PastaMapa + "/PearlHarbor.unity";
    private const string DefinicaoPath = "Assets/Resources/Mapas/PearlHarbor.asset";
    private const string CatalogoPath = "Assets/Resources/Mapas/CatalogoMapas.asset";
    private const string TriggerPath = "Temp/GerarPearlHarbor.trigger";
    private const float PixelsPorUnidade = 100f;
    private const float TamanhoCamera = 4.5f;

    static PearlHarborEditor()
    {
        // Processa correções de assets na instância gráfica já aberta do Editor.
        if (File.Exists(Absoluto(TriggerPath)))
            EditorApplication.delayCall += ExecutarGeracaoAgendada;
    }

    // ============================================================
    // 2. TERRITÓRIOS E POSIÇÕES EXTRAÍDAS DA ARTE
    // ============================================================

    private readonly struct TerritorioConfig
    {
        public TerritorioConfig(string id, string nome, string regiaoId, Vector2 posicao, Vector2 contador)
        {
            Id = id;
            Nome = nome;
            RegiaoId = regiaoId;
            Posicao = posicao;
            Contador = contador;
        }

        public string Id { get; }
        public string Nome { get; }
        public string RegiaoId { get; }
        public Vector2 Posicao { get; }
        public Vector2 Contador { get; }
    }

    private static readonly TerritorioConfig[] Territorios =
    {
        T("ReaganIsland", "Reagan Island", "north_sector", -4.595f, 3.135f, -0.400f, 0.145f),
        T("Portland", "Portland", "north_sector", -2.635f, 2.795f, 0.230f, 0.005f),
        T("Tucson", "Tucson", "north_sector", -3.765f, 2.070f, -0.080f, 0.210f),
        T("Albuquerque", "Albuquerque", "north_sector", -2.030f, 1.905f, 0.005f, 0.075f),
        T("Kadena", "Kadena", "north_sector", -0.935f, 2.675f, 0.060f, 0.105f),
        T("AndersonBase", "Anderson Base", "north_sector", -3.445f, 1.330f, 0.080f, 0.060f),
        T("Charlotte", "Charlotte", "left_sector", -2.140f, 0.860f, 0.235f, 0.150f),
        T("Hoover", "Hoover", "left_sector", -3.375f, 0.275f, 0.090f, -0.215f),
        T("Yokota", "Yokota", "left_sector", -1.940f, 0.030f, -0.115f, 0.030f),
        T("Harrison", "Harrison", "center_base", -0.960f, 1.580f, -0.205f, -0.080f),
        T("Incirlik", "Incirlik", "center_base", -0.575f, 1.060f, 0.170f, 0.030f),
        T("Austin", "Austin", "center_base", -0.260f, 0.385f, 0.095f, 0.115f),
        T("Fairchild", "Fairchild", "center_base", 0.515f, 1.200f, 0.010f, 0.040f),
        T("Hayes", "Hayes", "center_base", 0.185f, 1.970f, -0.040f, 0.000f),
        T("Mather", "Mather", "north_sector", 0.260f, 2.940f, 0.035f, 0.090f),
        T("Roosevelt", "Roosevelt", "north_sector", 0.900f, 3.320f, 0.185f, 0.000f),
        T("Coolidge", "Coolidge", "north_sector", 1.955f, 3.065f, -0.100f, -0.005f),
        T("Phoenix", "Phoenix", "right_sector", 2.225f, 2.215f, 0.020f, 0.015f),
        T("PhilipCollins", "Philip Collins", "right_sector", 3.210f, 2.545f, 0.015f, -0.055f),
        T("Maxwell", "Maxwell", "right_sector", 1.260f, 2.125f, -0.015f, 0.045f),
        T("Otix", "Otix", "right_sector", 1.635f, 1.020f, -0.050f, 0.310f),
        T("AbrahamLincolnCoast", "Abraham Lincoln Coast", "right_sector", 3.065f, 1.475f, -0.090f, 0.065f),
        T("ElAguacate", "El Aguacate", "right_sector", 4.520f, 1.740f, -0.305f, -0.030f),
        T("Andrews", "Andrews", "center_base", 0.535f, 0.400f, -0.040f, -0.030f),
        T("Grant", "Grant", "center_base", 0.350f, -0.455f, -0.225f, 0.015f),
        T("Eglin", "Eglin", "right_sector", 1.525f, -0.070f, -0.100f, 0.190f),
        T("Edwards", "Edwards", "right_sector", 1.870f, -0.440f, -0.055f, 0.040f),
        T("DavisMonthan", "Davis-Monthan", "right_sector", 4.675f, 0.995f, 0.040f, -0.075f),
        T("HearborPort", "Hearbor Port", "right_sector", 4.845f, 0.115f, -0.010f, 0.025f),
        T("RheinMain", "Rhein-Main", "right_sector", 3.375f, 0.750f, -0.110f, 0.090f),
        T("Columbus", "Columbus", "right_sector", 2.155f, 0.435f, -0.010f, 0.215f),
        T("Taft", "Taft", "left_sector", -1.230f, -0.605f, 0.165f, 0.065f),
        T("Thule", "Thule", "left_sector", -2.590f, -0.845f, -0.005f, 0.175f),
        T("Atlanta", "Atlanta", "left_sector", -3.700f, -1.070f, -0.125f, 0.190f),
        T("LongBeach", "Long Beach", "left_sector", -2.150f, -1.835f, -0.255f, 0.005f),
        T("Garfield", "Garfield", "center_base", -0.160f, -1.345f, 0.335f, 0.055f),
        T("Malmstrom", "Malmstrom", "center_base", 0.960f, -1.015f, -0.065f, -0.005f),
        T("Robins", "Robins", "left_sector", -1.635f, -1.340f, -0.280f, 0.030f),
        T("Bush", "Bush", "south_sector", 1.835f, -1.635f, -0.160f, 0.005f),
        T("Sacramento", "Sacramento", "south_sector", 0.695f, -2.030f, 0.070f, -0.060f),
        T("Eisenhower", "Eisenhower", "south_sector", -0.520f, -2.180f, 0.335f, 0.070f),
        T("Ramstein", "Ramstein", "south_sector", 0.290f, -2.820f, 0.035f, 0.060f),
        T("Nixon", "Nixon", "south_sector", 2.730f, -2.255f, -0.115f, 0.045f),
        T("VirginiaBeach", "Virginia Beach", "south_sector", 1.615f, -2.445f, -0.130f, 0.005f),
        T("TrumpIsland", "Trump Island", "south_sector", 3.505f, -2.935f, 0.100f, -0.025f)
    };

    // ============================================================
    // 3. REGIÕES E BÔNUS
    // ============================================================

    private static readonly Dictionary<string, (string Nome, int Bonus, int Ordem, Color Cor)> Regioes =
        new Dictionary<string, (string, int, int, Color)>(StringComparer.Ordinal)
        {
            ["north_sector"] = ("North Sector", 6, 0, new Color(0.25f, 0.65f, 1f)),
            ["left_sector"] = ("Left Sector", 5, 1, new Color(0.35f, 0.85f, 0.45f)),
            ["center_base"] = ("Center Base", 7, 2, new Color(1f, 0.72f, 0.25f)),
            ["right_sector"] = ("Right Sector", 6, 3, new Color(0.85f, 0.42f, 0.9f)),
            ["south_sector"] = ("South Sector", 4, 4, new Color(1f, 0.38f, 0.32f))
        };

    // ============================================================
    // 4. FRONTEIRAS TERRESTRES E PONTES
    // ============================================================

    private static readonly (string A, string B)[] Fronteiras =
    {
        P("Roosevelt", "Coolidge"), P("Roosevelt", "Mather"), P("Roosevelt", "Maxwell"),
        P("Coolidge", "Phoenix"), P("Coolidge", "Maxwell"),
        P("Mather", "Kadena"), P("Mather", "Maxwell"), P("Mather", "Hayes"),
        P("Portland", "Kadena"), P("Portland", "Tucson"), P("Portland", "Albuquerque"),
        P("Kadena", "Albuquerque"), P("Kadena", "Hayes"), P("Kadena", "Harrison"),
        P("PhilipCollins", "Phoenix"), P("Phoenix", "Maxwell"), P("Phoenix", "Otix"), P("Maxwell", "Otix"),
        P("Tucson", "Albuquerque"), P("Tucson", "AndersonBase"),
        P("Albuquerque", "Harrison"), P("Albuquerque", "AndersonBase"), P("Albuquerque", "Charlotte"),
        P("Hayes", "Harrison"), P("Hayes", "Fairchild"), P("Hayes", "Incirlik"),
        P("ElAguacate", "DavisMonthan"), P("Harrison", "Incirlik"), P("Harrison", "Charlotte"),
        P("AbrahamLincolnCoast", "Otix"), P("AbrahamLincolnCoast", "RheinMain"), P("AbrahamLincolnCoast", "Columbus"),
        P("AndersonBase", "Charlotte"), P("AndersonBase", "Hoover"), P("Otix", "Columbus"),
        P("Fairchild", "Incirlik"), P("Fairchild", "Andrews"), P("Incirlik", "Austin"),
        P("DavisMonthan", "HearborPort"), P("Charlotte", "Hoover"), P("Charlotte", "Yokota"),
        P("RheinMain", "Columbus"), P("RheinMain", "HearborPort"),
        P("Columbus", "Eglin"), P("Columbus", "Edwards"), P("Andrews", "Austin"), P("Andrews", "Grant"),
        P("Austin", "Grant"), P("Hoover", "Yokota"), P("Hoover", "Thule"), P("Hoover", "Atlanta"),
        P("Yokota", "Taft"), P("Yokota", "Thule"), P("Eglin", "Edwards"),
        P("Grant", "Malmstrom"), P("Grant", "Garfield"), P("Taft", "Thule"),
        P("Thule", "Atlanta"), P("Thule", "Robins"), P("Thule", "LongBeach"),
        P("Malmstrom", "Garfield"), P("Atlanta", "LongBeach"), P("Robins", "LongBeach"),
        P("Garfield", "Eisenhower"), P("Bush", "Sacramento"), P("Bush", "Nixon"), P("Bush", "VirginiaBeach"),
        P("Sacramento", "Eisenhower"), P("Sacramento", "VirginiaBeach"), P("Sacramento", "Ramstein"),
        P("Eisenhower", "Ramstein"), P("Nixon", "VirginiaBeach")
    };

    private static readonly (string A, string B)[] Pontes =
    {
        P("ReaganIsland", "Portland"), P("ReaganIsland", "Tucson"),
        P("Maxwell", "Hayes"), P("Phoenix", "AbrahamLincolnCoast"), P("PhilipCollins", "AbrahamLincolnCoast"),
        P("ElAguacate", "AbrahamLincolnCoast"), P("DavisMonthan", "RheinMain"),
        P("Hayes", "Otix"), P("Charlotte", "Incirlik"), P("Otix", "Andrews"),
        P("Eglin", "Grant"), P("Edwards", "Malmstrom"), P("Grant", "Taft"),
        P("Taft", "Garfield"), P("Robins", "Garfield"), P("Malmstrom", "Bush"),
        P("Garfield", "Sacramento"), P("Nixon", "TrumpIsland")
    };

    // ============================================================
    // 5. MENUS DE SELEÇÃO, GERAÇÃO E VALIDAÇÃO
    // ============================================================

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Gerar ou Atualizar/Pearl Harbor")]
    public static void GerarOuAtualizar()
    {
        ValidarArquivosFonte();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigurarImportadores();
        DefinicaoMapa definicao = GerarDefinicao();
        AtualizarCatalogo(definicao);
        GerarCena(definicao);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidarCompleto();
        Debug.Log("[Mapa] Carregado: Pearl Harbor | Territorios: 45 | Regioes: 5 | Definicao validada com sucesso.");
    }

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Validar/Pearl Harbor")]
    public static void ValidarCompleto()
    {
        DefinicaoMapa mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinicaoPath);
        ValidarDefinicao(mapa);
        ValidarCenaEMascaras(mapa);
        Debug.Log($"[Mapa] Pearl Harbor validado: {mapa.Territorios.Count} territorios, {mapa.Regioes.Count} regioes e {mapa.Conexoes.Count} conexoes.");
    }

    public static void GerarBatch()
    {
        GerarOuAtualizar();
        Debug.Log("PEARL_HARBOR_BATCH_OK");
        EditorApplication.Exit(0);
    }

    public static void ValidarBatch()
    {
        ValidarCompleto();
        Debug.Log("PEARL_HARBOR_VALIDACAO_BATCH_OK");
        EditorApplication.Exit(0);
    }

    private static void ExecutarGeracaoAgendada()
    {
        if (!File.Exists(Absoluto(TriggerPath)))
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ExecutarGeracaoAgendada;
            return;
        }

        try
        {
            GerarOuAtualizar();
            File.Delete(Absoluto(TriggerPath));
            Debug.Log("PEARL_HARBOR_TRIGGER_OK");
        }
        catch (Exception excecao)
        {
            Debug.LogError("PEARL_HARBOR_TRIGGER_ERRO | " + excecao);
        }
    }

    // ============================================================
    // 6. CRIAÇÃO DATA-DRIVEN DA DEFINIÇÃO
    // ============================================================

    private static DefinicaoMapa GerarDefinicao()
    {
        List<DefinicaoTerritorioMapa> territorios = Territorios.Select(config => new DefinicaoTerritorioMapa
        {
            id = config.Id,
            nomeExibido = config.Nome,
            chaveTraducao = "territory_" + Slug(config.Id),
            regiaoId = config.RegiaoId,
            posicaoContador = config.Contador,
            possuiPosicaoContador = true
        }).ToList();

        List<DefinicaoRegiaoMapa> regioes = Regioes.Select(item => new DefinicaoRegiaoMapa
        {
            id = item.Key,
            nomeExibido = item.Value.Nome,
            descricao = item.Value.Nome,
            chaveTraducao = "region_" + item.Key,
            bonus = item.Value.Bonus,
            ordemExibicao = item.Value.Ordem,
            corDestaque = item.Value.Cor,
            territorioIds = Territorios.Where(t => t.RegiaoId == item.Key).Select(t => t.Id).ToList()
        }).OrderBy(regiao => regiao.ordemExibicao).ToList();

        List<DefinicaoConexaoMapa> conexoes = new List<DefinicaoConexaoMapa>();
        HashSet<string> pares = new HashSet<string>(StringComparer.Ordinal);
        AdicionarConexoes(conexoes, pares, Fronteiras);
        AdicionarConexoes(conexoes, pares, Pontes);

        DefinicaoMapa mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinicaoPath);
        if (mapa == null)
        {
            mapa = ScriptableObject.CreateInstance<DefinicaoMapa>();
            AssetDatabase.CreateAsset(mapa, DefinicaoPath);
        }

        mapa.ConfigurarNoEditor(
            MapaId,
            "PearlHarbor",
            "Pearl Harbor",
            "Mapa real Pearl Harbor com 45 territórios e cinco setores.",
            "pearl_harbor",
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtePath),
            Vector2.zero,
            TamanhoCamera,
            territorios,
            regioes,
            conexoes);
        EditorUtility.SetDirty(mapa);
        AssetDatabase.SaveAssetIfDirty(mapa);
        return mapa;
    }

    private static void AdicionarConexoes(
        List<DefinicaoConexaoMapa> destino,
        HashSet<string> pares,
        IEnumerable<(string A, string B)> origem)
    {
        foreach ((string a, string b) in origem)
        {
            string primeiro = string.CompareOrdinal(a, b) <= 0 ? a : b;
            string segundo = string.CompareOrdinal(a, b) <= 0 ? b : a;
            if (!pares.Add(primeiro + "|" + segundo))
                continue;
            destino.Add(new DefinicaoConexaoMapa
            {
                origemId = primeiro,
                destinoId = segundo,
                tipo = TipoConexaoMapa.Terrestre,
                bidirecional = true
            });
        }
    }

    // ============================================================
    // 7. IMPORTAÇÃO TÉCNICA DA ARTE E DAS MÁSCARAS
    // ============================================================

    private static void ConfigurarImportadores()
    {
        ConfigurarSprite(ArtePath, FilterMode.Bilinear);
        foreach (TerritorioConfig territorio in Territorios)
            ConfigurarSprite(CaminhoMascara(territorio.Id), FilterMode.Bilinear);
    }

    private static void ConfigurarSprite(string path, FilterMode filtro)
    {
        TextureImporter importador = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importador == null)
            throw new InvalidOperationException("TextureImporter ausente: " + path);
        importador.textureType = TextureImporterType.Sprite;
        importador.spriteImportMode = SpriteImportMode.Single;
        importador.spritePixelsPerUnit = PixelsPorUnidade;
        importador.alphaSource = TextureImporterAlphaSource.FromInput;
        importador.alphaIsTransparency = true;
        importador.mipmapEnabled = false;
        importador.npotScale = TextureImporterNPOTScale.None;
        importador.textureCompression = TextureImporterCompression.Uncompressed;
        importador.maxTextureSize = 2048;
        importador.filterMode = filtro;
        importador.SaveAndReimport();
    }

    // ============================================================
    // 8. GERAÇÃO DA CENA INDEPENDENTE
    // ============================================================

    private static void GerarCena(DefinicaoMapa definicao)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaPath) != null && !AssetDatabase.DeleteAsset(CenaPath))
            throw new InvalidOperationException("Não foi possível substituir a cena Pearl Harbor.");
        if (!AssetDatabase.CopyAsset(CenaBase, CenaPath))
            throw new InvalidOperationException("Não foi possível copiar a estrutura da cena Classic.");

        Scene cena = EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Single);
        GameObject pai = GameObject.Find("Territorios");
        Transform modelo = pai != null ? pai.transform.Find("Arabia") : null;
        if (pai == null || modelo == null)
            throw new InvalidOperationException("Estrutura Territorios/Arabia ausente na cena base.");

        // As posições dos crops Pearl Harbor já estão expressas no espaço da
        // arte oficial, centrada na origem. A cena Classic usada como molde
        // mantém Territorios deslocado; preservar esse offset aplicaria uma
        // segunda translação às 45 máscaras, colliders e contadores.
        pai.transform.localPosition = Vector3.zero;
        pai.transform.localRotation = Quaternion.identity;
        pai.transform.localScale = Vector3.one;

        List<GameObject> novos = new List<GameObject>();
        for (int indice = 0; indice < Territorios.Length; indice++)
        {
            GameObject copia = UnityEngine.Object.Instantiate(modelo.gameObject, pai.transform, false);
            copia.name = "__PearlHarbor_" + indice;
            novos.Add(copia);
        }

        for (int indice = pai.transform.childCount - 1; indice >= 0; indice--)
        {
            Transform filho = pai.transform.GetChild(indice);
            if (!filho.name.StartsWith("__PearlHarbor_", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(filho.gameObject);
        }

        for (int indice = 0; indice < Territorios.Length; indice++)
            ConfigurarTerritorio(novos[indice], Territorios[indice], indice);

        GameObject visual = GameObject.Find("classic_0");
        if (visual == null)
            throw new InvalidOperationException("Visual base classic_0 ausente.");
        visual.name = "PearlHarbor_Arte";
        visual.transform.position = Vector3.zero;
        visual.transform.localScale = Vector3.one;
        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
        visualRenderer.sprite = definicao.ArteBase;
        visualRenderer.color = Color.white;
        // A arte oficial é a camada visual soberana: costas, rios, pontes e
        // fronteiras permanecem sempre visíveis acima dos preenchimentos técnicos.
        visualRenderer.sortingOrder = 2;

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 0f, camera.transform.position.z);
            camera.orthographicSize = definicao.TamanhoOrtografico;
        }

        EditorSceneManager.MarkSceneDirty(cena);
        if (!EditorSceneManager.SaveScene(cena, CenaPath))
            throw new InvalidOperationException("Falha ao salvar PearlHarbor.unity.");
    }

    private static void ConfigurarTerritorio(GameObject objeto, TerritorioConfig config, int indice)
    {
        objeto.name = config.Id;
        objeto.transform.localPosition = new Vector3(config.Posicao.x, config.Posicao.y, 0f);
        objeto.transform.localRotation = Quaternion.identity;
        objeto.transform.localScale = Vector3.one;

        SpriteRenderer renderer = objeto.GetComponent<SpriteRenderer>();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoMascara(config.Id));
        if (renderer == null || sprite == null)
            throw new InvalidOperationException("Renderer ou máscara ausente: " + config.Id);
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 0;

        TerritorioClique territorio = objeto.GetComponent<TerritorioClique>();
        TerritorioFronteiras fronteiras = objeto.GetComponent<TerritorioFronteiras>();
        TerritorioTropas tropas = objeto.GetComponent<TerritorioTropas>() ?? objeto.AddComponent<TerritorioTropas>();
        if (territorio == null || fronteiras == null)
            throw new InvalidOperationException("Componentes de gameplay ausentes: " + config.Id);
        territorio.idTerritorio = config.Id;
        territorio.chaveTraducao = "territory_" + Slug(config.Id);
        territorio.continente = (TerritorioClique.Continente)Mathf.Clamp(Regioes[config.RegiaoId].Ordem, 0, 5);
        territorio.dono = TerritorioClique.Dono.Neutro;
        tropas.Quantidade = 1;
        fronteiras.Configurar(Array.Empty<TerritorioClique>());

        PolygonCollider2D anterior = objeto.GetComponent<PolygonCollider2D>();
        if (anterior != null)
            UnityEngine.Object.DestroyImmediate(anterior);
        PolygonCollider2D collider = objeto.AddComponent<PolygonCollider2D>();
        AplicarPhysicsShape(sprite, collider);

        Transform contador = objeto.transform.Find("ContadorModerno");
        if (contador == null)
            throw new InvalidOperationException("ContadorModerno ausente: " + config.Id);
        contador.localPosition = new Vector3(config.Contador.x, config.Contador.y, 0f);
    }

    private static void AplicarPhysicsShape(Sprite sprite, PolygonCollider2D collider)
    {
        int quantidade = sprite.GetPhysicsShapeCount();
        if (quantidade <= 0)
            throw new InvalidOperationException("Physics shape ausente: " + sprite.name);
        collider.pathCount = quantidade;
        List<Vector2> pontos = new List<Vector2>();
        for (int indice = 0; indice < quantidade; indice++)
        {
            pontos.Clear();
            sprite.GetPhysicsShape(indice, pontos);
            collider.SetPath(indice, pontos);
        }
    }

    // ============================================================
    // 9. CATÁLOGO E SELEÇÃO PARA TESTE
    // ============================================================

    private static void AtualizarCatalogo(DefinicaoMapa pearlHarbor)
    {
        CatalogoMapas catalogo = AssetDatabase.LoadAssetAtPath<CatalogoMapas>(CatalogoPath);
        if (catalogo == null || catalogo.MapaPadrao == null)
            throw new InvalidOperationException("Catálogo de mapas ausente ou sem mapa padrão.");

        List<DefinicaoMapa> mapas = new List<DefinicaoMapa>();
        foreach (DefinicaoMapa mapa in catalogo.Mapas)
        {
            if (mapa != null && mapa.MapaId != MapaId && !mapas.Contains(mapa))
                mapas.Add(mapa);
        }
        mapas.Add(pearlHarbor);
        catalogo.ConfigurarNoEditor(catalogo.MapaPadrao, mapas);
        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssetIfDirty(catalogo);
    }

    // ============================================================
    // 10. VALIDAÇÕES E DIAGNÓSTICO
    // ============================================================

    private static void ValidarArquivosFonte()
    {
        if (!File.Exists(Absoluto(ArtePath)))
            throw new FileNotFoundException("Arte Pearl Harbor ausente.", ArtePath);
        if (!File.Exists(Absoluto(GabaritoPath)))
            throw new FileNotFoundException("Gabarito territorial Pearl Harbor ausente.", GabaritoPath);
        Vector2Int dimensoesArte = ObterDimensoesImagem(ArtePath);
        Vector2Int dimensoesGabarito = ObterDimensoesImagem(GabaritoPath);
        if (dimensoesArte != dimensoesGabarito)
            throw new InvalidOperationException($"Arte e gabarito Pearl Harbor não são 1:1: arte {dimensoesArte.x}x{dimensoesArte.y}, gabarito {dimensoesGabarito.x}x{dimensoesGabarito.y}.");
        if (dimensoesArte != new Vector2Int(1281, 852))
            throw new InvalidOperationException($"Dimensões Pearl Harbor inesperadas: {dimensoesArte.x}x{dimensoesArte.y}.");
        if (Territorios.Length != 45)
            throw new InvalidOperationException("Pearl Harbor deve possuir exatamente 45 territórios.");
        foreach (TerritorioConfig territorio in Territorios)
            if (!File.Exists(Absoluto(CaminhoMascara(territorio.Id))))
                throw new FileNotFoundException("Máscara ausente.", CaminhoMascara(territorio.Id));
    }

    private static void ValidarDefinicao(DefinicaoMapa mapa)
    {
        if (mapa == null)
            throw new InvalidOperationException("Definição PearlHarbor.asset ausente.");
        List<ProblemaValidacaoMapa> problemas = ValidadorDefinicaoMapa.Validar(mapa);
        if (problemas.Count > 0)
            throw new InvalidOperationException("Pearl Harbor inválido: " + string.Join(" | ", problemas));
        if (mapa.Territorios.Count != 45 || mapa.Regioes.Count != 5)
            throw new InvalidOperationException($"Contagem inválida: {mapa.Territorios.Count} territórios / {mapa.Regioes.Count} regiões.");

        Dictionary<string, int> esperados = new Dictionary<string, int>
        {
            ["north_sector"] = 9,
            ["left_sector"] = 9,
            ["center_base"] = 9,
            ["right_sector"] = 11,
            ["south_sector"] = 7
        };
        foreach (DefinicaoRegiaoMapa regiao in mapa.Regioes)
            if (!esperados.TryGetValue(regiao.id, out int esperado) || regiao.territorioIds.Count != esperado || regiao.bonus <= 0)
                throw new InvalidOperationException($"Região inválida: {regiao.id} ({regiao.territorioIds.Count} territórios, bônus {regiao.bonus}).");

        ValidarConexoesPearlHarbor(mapa);
    }

    private static void ValidarConexoesPearlHarbor(DefinicaoMapa mapa)
    {
        HashSet<string> ids = new HashSet<string>(Territorios.Select(t => t.Id), StringComparer.Ordinal);
        HashSet<string> esperadas = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string a, string b) in Fronteiras.Concat(Pontes))
            esperadas.Add(ChavePar(a, b));

        HashSet<string> encontradas = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, int> graus = ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (DefinicaoConexaoMapa conexao in mapa.Conexoes)
        {
            if (conexao == null || !ids.Contains(conexao.origemId) || !ids.Contains(conexao.destinoId))
                throw new InvalidOperationException("Pearl Harbor possui conexão com território inexistente.");
            if (conexao.origemId == conexao.destinoId)
                throw new InvalidOperationException("Pearl Harbor possui auto-conexão em " + conexao.origemId + ".");
            if (!conexao.bidirecional)
                throw new InvalidOperationException($"Conexão unilateral não permitida: {conexao.origemId} -> {conexao.destinoId}.");
            string chave = ChavePar(conexao.origemId, conexao.destinoId);
            if (!encontradas.Add(chave))
                throw new InvalidOperationException("Conexão duplicada: " + chave + ".");
            graus[conexao.origemId]++;
            graus[conexao.destinoId]++;
        }

        if (!esperadas.SetEquals(encontradas))
            throw new InvalidOperationException(
                "Conexões Pearl Harbor divergentes. Ausentes: " + string.Join(", ", esperadas.Except(encontradas)) +
                " | Extras: " + string.Join(", ", encontradas.Except(esperadas)));
        if (graus.Any(item => item.Value == 0))
            throw new InvalidOperationException("Território isolado: " + string.Join(", ", graus.Where(item => item.Value == 0).Select(item => item.Key)));

        string[] obrigatorias =
        {
            ChavePar("ReaganIsland", "Tucson"),
            ChavePar("ReaganIsland", "Portland"),
            ChavePar("Charlotte", "Incirlik"),
            ChavePar("TrumpIsland", "Nixon")
        };
        if (obrigatorias.Any(chave => !encontradas.Contains(chave)))
            throw new InvalidOperationException("Uma ou mais conexões especiais obrigatórias estão ausentes.");

        (string origem, string destino)[] paresVizinhosRuntime =
        {
            ("Albuquerque", "AndersonBase"),
            ("ReaganIsland", "Portland"),
            ("ReaganIsland", "Tucson"),
            ("TrumpIsland", "Nixon"),
            ("Hayes", "Harrison")
        };
        foreach ((string origem, string destino) in paresVizinhosRuntime)
            if (!MapaAtivo.SaoConectados(mapa, origem, destino, TipoConexaoMapa.Terrestre))
                throw new InvalidOperationException($"Consulta usada pelo gameplay recusou vizinhos válidos: {origem} / {destino}.");

        if (MapaAtivo.SaoConectados(mapa, "ReaganIsland", "Albuquerque", TipoConexaoMapa.Terrestre))
            throw new InvalidOperationException("Consulta usada pelo gameplay aceitou não vizinhos: ReaganIsland / Albuquerque.");
    }

    private static void ValidarCenaEMascaras(DefinicaoMapa mapa)
    {
        Vector2Int dimensoesArte = ObterDimensoesImagem(ArtePath);
        foreach (TerritorioConfig config in Territorios)
        {
            Sprite mascara = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoMascara(config.Id));
            if (mascara == null || mascara.GetPhysicsShapeCount() <= 0)
                throw new InvalidOperationException("Máscara ou physics shape inválido: " + config.Id);
            if (!Mathf.Approximately(mascara.pixelsPerUnit, PixelsPorUnidade))
                throw new InvalidOperationException($"PPU inválido em {config.Id}: {mascara.pixelsPerUnit}.");
            Vector2 centroPixel = new Vector2(
                config.Posicao.x * PixelsPorUnidade + dimensoesArte.x * 0.5f,
                dimensoesArte.y * 0.5f - config.Posicao.y * PixelsPorUnidade);
            Rect boundsAbsolutos = new Rect(
                centroPixel.x - mascara.rect.width * 0.5f,
                centroPixel.y - mascara.rect.height * 0.5f,
                mascara.rect.width,
                mascara.rect.height);
            if (boundsAbsolutos.xMin < -0.51f || boundsAbsolutos.yMin < -0.51f ||
                boundsAbsolutos.xMax > dimensoesArte.x + 0.51f || boundsAbsolutos.yMax > dimensoesArte.y + 0.51f)
                throw new InvalidOperationException("Máscara fora dos bounds da arte: " + config.Id);
        }

        Scene cenaAnterior = SceneManager.GetActiveScene();
        bool cenaJaAberta = cenaAnterior.IsValid() && cenaAnterior.path == CenaPath;
        Scene cena = cenaJaAberta
            ? cenaAnterior
            : EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Additive);
        try
        {
            TerritorioClique[] territoriosCena = cena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<TerritorioClique>(true))
                .ToArray();
            if (territoriosCena.Length != 45)
                throw new InvalidOperationException("Cena Pearl Harbor contém " + territoriosCena.Length + " territórios; esperados 45.");
            HashSet<string> ids = new HashSet<string>(territoriosCena.Select(t => t.idTerritorio), StringComparer.Ordinal);
            if (ids.Count != 45 || mapa.Territorios.Any(t => !ids.Contains(t.id)))
                throw new InvalidOperationException("IDs da cena Pearl Harbor não correspondem à definição.");
            Transform paiTerritorios = territoriosCena[0].transform.parent;
            if (paiTerritorios == null || paiTerritorios.name != "Territorios" ||
                paiTerritorios.localPosition != Vector3.zero || paiTerritorios.localRotation != Quaternion.identity || paiTerritorios.localScale != Vector3.one)
                throw new InvalidOperationException("Transform de Territorios não está neutro na cena Pearl Harbor.");
            foreach (TerritorioClique territorio in territoriosCena)
            {
                PolygonCollider2D collider = territorio.GetComponent<PolygonCollider2D>();
                Transform contador = territorio.transform.Find("ContadorModerno");
                if (collider == null || collider.pathCount <= 0 || contador == null)
                    throw new InvalidOperationException("Collider ou contador ausente: " + territorio.idTerritorio);
                for (int indice = 0; indice < collider.pathCount; indice++)
                    if (collider.GetPath(indice).Length < 3)
                        throw new InvalidOperationException("Collider vazio: " + territorio.idTerritorio);
                if (!collider.OverlapPoint(contador.position))
                    throw new InvalidOperationException("Contador fora da máscara/collider: " + territorio.idTerritorio);
            }
        }
        finally
        {
            if (!cenaJaAberta)
                EditorSceneManager.CloseScene(cena, true);
            if (!cenaJaAberta && cenaAnterior.IsValid())
                SceneManager.SetActiveScene(cenaAnterior);
        }
    }

    // ============================================================
    // 11. UTILITÁRIOS
    // ============================================================

    private static TerritorioConfig T(string id, string nome, string regiao, float x, float y, float contadorX, float contadorY) =>
        new TerritorioConfig(id, nome, regiao, new Vector2(x, y), new Vector2(contadorX, contadorY));

    private static (string A, string B) P(string a, string b) => (a, b);
    internal static bool EhConexaoEspecial(string a, string b) => Pontes.Any(par => ChavePar(par.A, par.B) == ChavePar(a, b));
    private static string ChavePar(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
    private static string CaminhoMascara(string id) => PastaMascaras + "/" + id + ".png";
    private static string Slug(string valor) => valor.ToLowerInvariant();
    private static string Absoluto(string path) => Path.GetFullPath(path);

    private static Vector2Int ObterDimensoesImagem(string path)
    {
        Texture2D textura = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(textura, File.ReadAllBytes(Absoluto(path)), false))
                throw new InvalidOperationException("Não foi possível ler imagem: " + path);
            return new Vector2Int(textura.width, textura.height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(textura);
        }
    }
}

[InitializeOnLoad]
public static class DiagnosticoMascarasPearlHarbor
{
    // ============================================================
    // 1. ESTADO TEMPORÁRIO E MENU DO DIAGNÓSTICO
    // ============================================================

    private const string CenaPearlHarbor = "Assets/Mapas/Pearl Harbor/PearlHarbor.unity";
    private const string DefinicaoPearlHarbor = "Assets/Resources/Mapas/PearlHarbor.asset";
    private const string MenuDiagnostico = "War Dominion/Ferramentas de Desenvolvimento/Mapas/Diagnóstico e Relatórios/Pearl Harbor/Mostrar Diagnóstico de Máscaras";
    private const string MenuRelatorio = "War Dominion/Ferramentas de Desenvolvimento/Mapas/Diagnóstico e Relatórios/Pearl Harbor/Relatório de Vizinhanças";
    private const string ChaveSession = "WarGame.PearlHarbor.MostrarDiagnosticoMascaras";

    static DiagnosticoMascarasPearlHarbor()
    {
        SceneView.duringSceneGui -= Desenhar;
        SceneView.duringSceneGui += Desenhar;
    }

    [MenuItem(MenuDiagnostico)]
    private static void Alternar()
    {
        SessionState.SetBool(ChaveSession, !Ativo);
        Menu.SetChecked(MenuDiagnostico, Ativo);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuDiagnostico, true)]
    private static bool ValidarMenu()
    {
        Menu.SetChecked(MenuDiagnostico, Ativo);
        return SceneManager.GetActiveScene().path == CenaPearlHarbor;
    }

    [MenuItem(MenuRelatorio)]
    private static void GerarRelatorioVizinhanças()
    {
        DefinicaoMapa mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinicaoPearlHarbor);
        if (mapa == null)
        {
            Debug.LogError("Definição PearlHarbor.asset ausente.");
            return;
        }

        Dictionary<string, List<string>> vizinhos = mapa.Territorios.ToDictionary(
            territorio => territorio.id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (DefinicaoConexaoMapa conexao in mapa.Conexoes)
        {
            string sufixo = PearlHarborEditor.EhConexaoEspecial(conexao.origemId, conexao.destinoId) ? " [ponte/conector]" : " [terrestre]";
            vizinhos[conexao.origemId].Add(conexao.destinoId + sufixo);
            vizinhos[conexao.destinoId].Add(conexao.origemId + sufixo);
        }

        string relatorio = string.Join("\n", vizinhos.OrderBy(item => item.Key).Select(item =>
            item.Key + ":\n- " + string.Join("\n- ", item.Value.OrderBy(valor => valor, StringComparer.Ordinal))));
        Debug.Log("PEARL HARBOR — VIZINHANÇAS\n" + relatorio);
    }

    private static bool Ativo => SessionState.GetBool(ChaveSession, false);

    // ============================================================
    // 2. CONTORNOS DOS COLLIDERS E POSIÇÕES DOS CONTADORES
    // ============================================================

    private static void Desenhar(SceneView sceneView)
    {
        if (!Ativo || SceneManager.GetActiveScene().path != CenaPearlHarbor || Event.current.type != EventType.Repaint)
            return;

        Color corAnterior = Handles.color;
        foreach (TerritorioClique territorio in UnityEngine.Object.FindObjectsByType<TerritorioClique>(FindObjectsInactive.Include))
        {
            if (territorio == null || !territorio.gameObject.scene.IsValid())
                continue;
            PolygonCollider2D collider = territorio.GetComponent<PolygonCollider2D>();
            if (collider == null)
                continue;

            // Amarelo: contorno físico derivado diretamente da máscara importada.
            SpriteRenderer renderer = territorio.GetComponent<SpriteRenderer>();
            Sprite sprite = renderer != null ? renderer.sprite : null;
            if (sprite != null)
            {
                Handles.color = new Color(1f, 0.82f, 0f, 0.95f);
                List<Vector2> pontosMascara = new List<Vector2>();
                for (int indiceShape = 0; indiceShape < sprite.GetPhysicsShapeCount(); indiceShape++)
                {
                    pontosMascara.Clear();
                    sprite.GetPhysicsShape(indiceShape, pontosMascara);
                    if (pontosMascara.Count < 2)
                        continue;
                    Vector3[] mundoMascara = new Vector3[pontosMascara.Count + 1];
                    for (int indice = 0; indice < pontosMascara.Count; indice++)
                        mundoMascara[indice] = territorio.transform.TransformPoint(pontosMascara[indice]);
                    mundoMascara[pontosMascara.Count] = mundoMascara[0];
                    Handles.DrawAAPolyLine(1.5f, mundoMascara);
                }
            }

            // Magenta: PolygonCollider2D efetivamente utilizado pelo input.
            Handles.color = new Color(1f, 0f, 0.85f, 0.95f);
            for (int indicePath = 0; indicePath < collider.pathCount; indicePath++)
            {
                Vector2[] path = collider.GetPath(indicePath);
                if (path.Length < 2)
                    continue;
                Vector3[] mundo = new Vector3[path.Length + 1];
                for (int indice = 0; indice < path.Length; indice++)
                    mundo[indice] = territorio.transform.TransformPoint(path[indice] + collider.offset);
                mundo[path.Length] = mundo[0];
                Handles.DrawAAPolyLine(3f, mundo);
            }

            Transform contador = territorio.transform.Find("ContadorModerno");
            if (contador != null)
            {
                Handles.color = new Color(0f, 1f, 1f, 0.95f);
                Handles.DrawSolidDisc(contador.position, Vector3.forward, HandleUtility.GetHandleSize(contador.position) * 0.025f);
            }
        }

        DesenharVizinhosSelecionados();
        Handles.color = corAnterior;
    }

    private static void DesenharVizinhosSelecionados()
    {
        GameObject selecionado = Selection.activeGameObject;
        TerritorioClique origem = selecionado != null ? selecionado.GetComponentInParent<TerritorioClique>() : null;
        if (origem == null)
            return;

        DefinicaoMapa mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinicaoPearlHarbor);
        if (mapa == null)
            return;

        Dictionary<string, TerritorioClique> territorios = UnityEngine.Object
            .FindObjectsByType<TerritorioClique>(FindObjectsInactive.Include)
            .Where(territorio => territorio != null && territorio.gameObject.scene.IsValid())
            .ToDictionary(territorio => territorio.idTerritorio, StringComparer.Ordinal);
        foreach (DefinicaoConexaoMapa conexao in mapa.Conexoes)
        {
            string destinoId = conexao.origemId == origem.idTerritorio
                ? conexao.destinoId
                : conexao.destinoId == origem.idTerritorio ? conexao.origemId : null;
            if (destinoId == null || !territorios.TryGetValue(destinoId, out TerritorioClique destino))
                continue;

            Handles.color = PearlHarborEditor.EhConexaoEspecial(origem.idTerritorio, destinoId)
                ? new Color(1f, 0.55f, 0f, 0.95f)
                : new Color(0.15f, 1f, 0.25f, 0.9f);
            Handles.DrawAAPolyLine(4f, origem.transform.position, destino.transform.position);
            Handles.DrawSolidDisc(destino.transform.position, Vector3.forward, HandleUtility.GetHandleSize(destino.transform.position) * 0.035f);
        }
    }
}
#endif

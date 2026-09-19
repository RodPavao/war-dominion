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
public static class RiachueloEditor
{
    private const string MapaId = "riachuelo";
    private const string CenaBase = "Assets/WarGame.unity";
    private const string PastaMapa = "Assets/Mapas/Batalha do Riachuelo";
    private const string PastaMascaras = PastaMapa + "/Mascaras";
    private const string ArtePath = PastaMapa + "/Arte/Riachuelo.png";
    private const string GabaritoPath = PastaMapa + "/Arte/Riachuelo_GabaritoTerritorios.png";
    private const string CenaPath = PastaMapa + "/Riachuelo.unity";
    private const string DefinicaoPath = "Assets/Resources/Mapas/Riachuelo.asset";
    private const string CatalogoPath = "Assets/Resources/Mapas/CatalogoMapas.asset";
    private const string TriggerPath = "Temp/GerarRiachuelo.trigger";
    private const float PixelsPorUnidade = 100f;
    private const float LarguraImagem = 3015f;
    private const float AlturaImagem = 2311f;
    private const float TamanhoCamera = 11.75f;

    static RiachueloEditor()
    {
        if (File.Exists(Path.GetFullPath(TriggerPath)))
            EditorApplication.delayCall += ExecutarGeracaoAgendada;
    }

    private readonly struct TerritorioConfig
    {
        public TerritorioConfig(string id, string nome, string regiaoId, float centroX, float centroY, float contadorX, float contadorY)
        {
            Id = id;
            Nome = nome;
            RegiaoId = regiaoId;
            Posicao = new Vector2((centroX - LarguraImagem * 0.5f) / PixelsPorUnidade,
                (AlturaImagem * 0.5f - centroY) / PixelsPorUnidade);
            Contador = new Vector2((contadorX - centroX) / PixelsPorUnidade,
                (centroY - contadorY) / PixelsPorUnidade);
        }

        public string Id { get; }
        public string Nome { get; }
        public string RegiaoId { get; }
        public Vector2 Posicao { get; }
        public Vector2 Contador { get; }
    }

    private static readonly TerritorioConfig[] Territorios =
    {
        T("Pilcomayo", "Pilcomayo", "formosa", 2638f, 398.5f, 2646.8f, 396.3f),
        T("Pilagas", "Pilagás", "formosa", 2357f, 489f, 2358.7f, 497f),
        T("Feliciano", "Feliciano", "entre_rios", 371.5f, 539f, 317.2f, 519.5f),
        T("PalomeraIsland", "Palomera Island", "chaco", 1505f, 700.5f, 1523.1f, 717.7f),
        T("Federal", "Federal", "entre_rios", 678.5f, 724f, 691.1f, 725.6f),
        T("GeneralSanMartin", "General San Martín", "chaco", 2074.5f, 728f, 2091f, 732.7f),
        T("LaPaz", "La Paz", "entre_rios", 380f, 831.5f, 394.7f, 832f),
        T("Formosa", "Formosa", "formosa", 2703f, 840f, 2703.1f, 844.4f),
        T("Laishi", "Laishí", "formosa", 2402.5f, 856f, 2400.3f, 858.4f),
        T("Federacion", "Federación", "entre_rios", 981.5f, 853f, 980.3f, 883f),
        T("OHiggins", "O'Higgins", "chaco", 1768f, 887f, 1806.9f, 919.5f),
        T("PrimeroDeMayo", "Primero de Mayo", "chaco", 2105.5f, 992f, 2115.1f, 993.6f),
        T("Parana", "Paraná", "entre_rios", 300.5f, 1011f, 262.4f, 1054.1f),
        T("Villaguay", "Villaguay", "entre_rios", 771.5f, 1056f, 767.8f, 1064.2f),
        T("MayorMartinez", "Mayor Martínez", "neembucu", 2436f, 1109.5f, 2421.5f, 1106.9f),
        T("Concordia", "Concordia", "entre_rios", 1186.5f, 1088.5f, 1192.2f, 1116.8f),
        T("SanLorenzo", "San Lorenzo", "chaco", 1667f, 1225f, 1687.4f, 1222.7f),
        T("SanFernando", "San Fernando", "chaco", 2078f, 1229.5f, 2059.3f, 1227.7f),
        T("Desmochados", "Desmochados", "neembucu", 2702.5f, 1251f, 2717f, 1247.7f),
        T("Mercedes", "Mercedes", "corrientes", 751f, 1294f, 761.6f, 1285.3f),
        T("Goya", "Goya", "corrientes", 320f, 1431f, 299.2f, 1410.2f),
        T("PasoDeLaPatria", "Paso de la Patria", "neembucu", 2404.5f, 1432f, 2419.5f, 1423.4f),
        T("Lavalle", "Lavalle", "corrientes", 606.5f, 1475.5f, 592.1f, 1465.5f),
        T("PasoDeLosLibres", "Paso de los Libres", "corrientes", 1118.5f, 1478.5f, 1113.6f, 1472.9f),
        T("NueveDeJulio", "Nueve de Julio", "santa_fe", 1502.5f, 1519f, 1507.7f, 1500.3f),
        T("Vera", "Vera", "santa_fe", 1848f, 1514.5f, 1848.9f, 1526.7f),
        T("Sauce", "Sauce", "corrientes", 841f, 1564.5f, 838.8f, 1540.5f),
        T("GeneralDiaz", "General Díaz", "neembucu", 2754.5f, 1646.5f, 2759f, 1612.6f),
        T("SanJavier", "San Javier", "santa_fe", 2161.5f, 1657.5f, 2163.2f, 1634.6f),
        T("CuruzuCuatia", "Curuzú Cuatiá", "corrientes", 1115.5f, 1698f, 1091.3f, 1709.8f),
        T("SanCristobal", "San Cristóbal", "santa_fe", 1619.5f, 1747.5f, 1625.3f, 1755.6f),
        T("Humaita", "Humaitá", "neembucu", 2485.5f, 1763.5f, 2467.8f, 1756.2f),
        T("SantaFe", "Santa Fe", "santa_fe", 1942.5f, 1855f, 1935.7f, 1840.9f),
        T("Esquina", "Esquina", "corrientes", 849.5f, 1815.5f, 831.4f, 1841.3f),
        T("MonteCaseros", "Monte Caseros", "corrientes", 1239.5f, 1892f, 1237.8f, 1889.9f),
        T("Villalbin", "Villalbín", "neembucu", 2526f, 1980.5f, 2515.7f, 1978.5f),
        T("Pirane", "Pirané", "formosa", 2150f, 2075.5f, 2164.6f, 2091f)
    };

    private static readonly Dictionary<string, (string Nome, int Bonus, int Ordem, Color Cor)> Regioes =
        new Dictionary<string, (string, int, int, Color)>(StringComparer.Ordinal)
        {
            ["entre_rios"] = ("Entre Ríos", 3, 0, new Color(0.92f, 0.66f, 0.25f)),
            ["corrientes"] = ("Corrientes", 4, 1, new Color(0.28f, 0.72f, 0.92f)),
            ["chaco"] = ("Chaco", 3, 2, new Color(0.52f, 0.78f, 0.34f)),
            ["santa_fe"] = ("Santa Fe", 3, 3, new Color(0.86f, 0.40f, 0.42f)),
            ["neembucu"] = ("Ñeembucú", 3, 4, new Color(0.67f, 0.48f, 0.85f)),
            ["formosa"] = ("Formosa", 3, 5, new Color(0.35f, 0.79f, 0.68f))
        };

    // Pares obtidos pela proximidade das componentes brancas que compartilham linha no gabarito 1:1.
    private static readonly (string A, string B)[] Fronteiras = ParesPorIndice(new[]
    {
        "23-27","7-13","3-7","10-14","10-16","11-12","11-17","11-18","12-18","13-14","14-16","17-18",
        "20-21","20-23","20-24","20-27","21-23","24-27","24-30","27-30","27-34","3-5","30-34","30-35",
        "34-35","5-10","5-14","5-7","6-11","6-12","7-14","29-37","1-2","22-32","1-8","19-22","19-28",
        "2-8","2-9","22-28","25-26","25-31","26-29","26-31","26-33","28-32","28-36","29-33","31-33",
        "32-36","33-37","8-15","8-9","9-15","29-32","15-19","15-22","18-29","22-29","36-37","6-9",
        "8-19","15-18","18-22","2-6","9-12","14-20","17-25","13-20","13-21","16-20","16-24","17-26",
        "18-26","12-15","32-37"
    });

    private static readonly (string A, string B)[] Conectores =
    {
        P("PalomeraIsland", "Federacion"), P("PalomeraIsland", "GeneralSanMartin"),
        P("Concordia", "SanLorenzo"), P("PasoDeLosLibres", "NueveDeJulio"),
        P("CuruzuCuatia", "SanCristobal"), P("MonteCaseros", "SantaFe"),
        P("MonteCaseros", "Pirane")
    };

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Gerar ou Atualizar/Batalha do Riachuelo")]
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
        Debug.Log($"[Mapa] Batalha do Riachuelo gerado: {Territorios.Length} territórios, {Regioes.Count} regiões e {Fronteiras.Concat(Conectores).Select(p => ChavePar(p.A, p.B)).Distinct().Count()} conexões.");
    }

    private static void ExecutarGeracaoAgendada()
    {
        if (!File.Exists(Path.GetFullPath(TriggerPath))) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ExecutarGeracaoAgendada;
            return;
        }
        try
        {
            GerarOuAtualizar();
            File.Delete(Path.GetFullPath(TriggerPath));
            Debug.Log("RIACHUELO_TRIGGER_OK");
        }
        catch (Exception excecao)
        {
            Debug.LogError("RIACHUELO_TRIGGER_ERRO | " + excecao);
        }
    }

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Validar/Batalha do Riachuelo")]
    public static void ValidarCompleto()
    {
        DefinicaoMapa mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinicaoPath);
        if (mapa == null) throw new InvalidOperationException("Riachuelo.asset ausente.");
        List<ProblemaValidacaoMapa> problemas = ValidadorDefinicaoMapa.Validar(mapa);
        if (problemas.Count > 0) throw new InvalidOperationException("Riachuelo inválido: " + string.Join(" | ", problemas));
        if (mapa.Territorios.Count != 37 || mapa.Regioes.Count != 6)
            throw new InvalidOperationException($"Contagem inválida: {mapa.Territorios.Count}/6 regiões {mapa.Regioes.Count}.");
        HashSet<string> esperadas = new HashSet<string>(Fronteiras.Concat(Conectores).Select(p => ChavePar(p.A, p.B)), StringComparer.Ordinal);
        HashSet<string> encontradas = new HashSet<string>(mapa.Conexoes.Select(c => ChavePar(c.origemId, c.destinoId)), StringComparer.Ordinal);
        if (!esperadas.SetEquals(encontradas)) throw new InvalidOperationException("Conexões do Riachuelo divergentes.");
        if (Conectores.Count(p => p.A == "PalomeraIsland" || p.B == "PalomeraIsland") != 2)
            throw new InvalidOperationException("Palomera Island deve possuir duas conexões especiais.");
        ValidarCena(mapa);
        Debug.Log($"[Mapa] Riachuelo validado: {mapa.Territorios.Count} territórios, {mapa.Regioes.Count} regiões e {mapa.Conexoes.Count} conexões.");
    }

    private static DefinicaoMapa GerarDefinicao()
    {
        List<DefinicaoTerritorioMapa> territorios = Territorios.Select(t => new DefinicaoTerritorioMapa
        {
            id = t.Id, nomeExibido = t.Nome, chaveTraducao = "territory_" + t.Id.ToLowerInvariant(),
            regiaoId = t.RegiaoId, posicaoContador = t.Contador, possuiPosicaoContador = true
        }).ToList();
        List<DefinicaoRegiaoMapa> regioes = Regioes.Select(r => new DefinicaoRegiaoMapa
        {
            id = r.Key, nomeExibido = r.Value.Nome, descricao = r.Value.Nome,
            chaveTraducao = "region_" + r.Key, bonus = r.Value.Bonus, ordemExibicao = r.Value.Ordem,
            corDestaque = r.Value.Cor, territorioIds = Territorios.Where(t => t.RegiaoId == r.Key).Select(t => t.Id).ToList()
        }).OrderBy(r => r.ordemExibicao).ToList();
        List<DefinicaoConexaoMapa> conexoes = new List<DefinicaoConexaoMapa>();
        foreach (string chave in Fronteiras.Concat(Conectores).Select(p => ChavePar(p.A, p.B)).Distinct(StringComparer.Ordinal))
        {
            string[] partes = chave.Split('|');
            conexoes.Add(new DefinicaoConexaoMapa { origemId = partes[0], destinoId = partes[1], tipo = TipoConexaoMapa.Terrestre, bidirecional = true });
        }
        DefinicaoMapa mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinicaoPath);
        if (mapa == null) { mapa = ScriptableObject.CreateInstance<DefinicaoMapa>(); AssetDatabase.CreateAsset(mapa, DefinicaoPath); }
        mapa.ConfigurarNoEditor(MapaId, "Riachuelo", "Batalha do Riachuelo",
            "Mapa da Batalha do Riachuelo com 37 territórios e seis regiões.", "riachuelo",
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtePath), Vector2.zero, TamanhoCamera, territorios, regioes, conexoes);
        EditorUtility.SetDirty(mapa);
        AssetDatabase.SaveAssetIfDirty(mapa);
        return mapa;
    }

    private static void AtualizarCatalogo(DefinicaoMapa riachuelo)
    {
        CatalogoMapas catalogo = AssetDatabase.LoadAssetAtPath<CatalogoMapas>(CatalogoPath);
        if (catalogo == null || catalogo.MapaPadrao == null) throw new InvalidOperationException("Catálogo de mapas ausente.");
        List<DefinicaoMapa> mapas = catalogo.Mapas.Where(m => m != null && m.MapaId != MapaId).Distinct().ToList();
        mapas.Add(riachuelo);
        catalogo.ConfigurarNoEditor(catalogo.MapaPadrao, mapas);
        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssetIfDirty(catalogo);
    }

    private static void ConfigurarImportadores()
    {
        ConfigurarSprite(ArtePath, 4096);
        foreach (TerritorioConfig territorio in Territorios) ConfigurarSprite(CaminhoMascara(territorio.Id), 2048);
    }

    private static void ConfigurarSprite(string path, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter ausente: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPorUnidade;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = maxSize;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static void GerarCena(DefinicaoMapa definicao)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaPath) != null && !AssetDatabase.DeleteAsset(CenaPath))
            throw new InvalidOperationException("Não foi possível substituir Riachuelo.unity.");
        if (!AssetDatabase.CopyAsset(CenaBase, CenaPath)) throw new InvalidOperationException("Falha ao copiar cena base.");
        Scene cena = EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Single);
        GameObject pai = GameObject.Find("Territorios");
        Transform modelo = pai != null ? pai.transform.Find("Arabia") : null;
        if (pai == null || modelo == null) throw new InvalidOperationException("Molde Territorios/Arabia ausente.");
        pai.transform.localPosition = Vector3.zero;
        pai.transform.localRotation = Quaternion.identity;
        pai.transform.localScale = Vector3.one;
        List<GameObject> novos = new List<GameObject>();
        for (int i = 0; i < Territorios.Length; i++) { GameObject copia = UnityEngine.Object.Instantiate(modelo.gameObject, pai.transform, false); copia.name = "__Riachuelo_" + i; novos.Add(copia); }
        for (int i = pai.transform.childCount - 1; i >= 0; i--)
            if (!pai.transform.GetChild(i).name.StartsWith("__Riachuelo_", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(pai.transform.GetChild(i).gameObject);
        for (int i = 0; i < Territorios.Length; i++) ConfigurarTerritorio(novos[i], Territorios[i]);
        GameObject visual = GameObject.Find("classic_0");
        if (visual == null) throw new InvalidOperationException("Visual classic_0 ausente.");
        visual.name = "Riachuelo_Arte";
        visual.transform.position = Vector3.zero;
        visual.transform.localScale = Vector3.one;
        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
        visualRenderer.sprite = definicao.ArteBase;
        visualRenderer.color = Color.white;
        visualRenderer.sortingOrder = 2;
        Camera camera = Camera.main;
        if (camera != null) { camera.transform.position = new Vector3(0f, 0f, camera.transform.position.z); camera.orthographicSize = TamanhoCamera; }
        EditorSceneManager.MarkSceneDirty(cena);
        if (!EditorSceneManager.SaveScene(cena, CenaPath)) throw new InvalidOperationException("Falha ao salvar Riachuelo.unity.");
    }

    private static void ConfigurarTerritorio(GameObject objeto, TerritorioConfig config)
    {
        objeto.name = config.Id;
        objeto.transform.localPosition = new Vector3(config.Posicao.x, config.Posicao.y, 0f);
        objeto.transform.localRotation = Quaternion.identity;
        objeto.transform.localScale = Vector3.one;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoMascara(config.Id));
        SpriteRenderer renderer = objeto.GetComponent<SpriteRenderer>();
        if (sprite == null || renderer == null) throw new InvalidOperationException("Máscara/renderer ausente: " + config.Id);
        renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = 0;
        TerritorioClique clique = objeto.GetComponent<TerritorioClique>();
        TerritorioFronteiras fronteiras = objeto.GetComponent<TerritorioFronteiras>();
        TerritorioTropas tropas = objeto.GetComponent<TerritorioTropas>() ?? objeto.AddComponent<TerritorioTropas>();
        clique.idTerritorio = config.Id; clique.chaveTraducao = "territory_" + config.Id.ToLowerInvariant();
        clique.continente = (TerritorioClique.Continente)Mathf.Clamp(Regioes[config.RegiaoId].Ordem, 0, 5);
        clique.dono = TerritorioClique.Dono.Neutro; tropas.Quantidade = 1; fronteiras.Configurar(Array.Empty<TerritorioClique>());
        PolygonCollider2D anterior = objeto.GetComponent<PolygonCollider2D>();
        if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior);
        PolygonCollider2D collider = objeto.AddComponent<PolygonCollider2D>();
        collider.pathCount = sprite.GetPhysicsShapeCount();
        List<Vector2> pontos = new List<Vector2>();
        for (int i = 0; i < collider.pathCount; i++) { pontos.Clear(); sprite.GetPhysicsShape(i, pontos); collider.SetPath(i, pontos); }
        Transform contador = objeto.transform.Find("ContadorModerno");
        if (contador == null) throw new InvalidOperationException("Contador ausente: " + config.Id);
        contador.localPosition = new Vector3(config.Contador.x, config.Contador.y, 0f);
        contador.localScale = new Vector3(2f, 2f, 2f);
    }

    private static void ValidarCena(DefinicaoMapa mapa)
    {
        Scene anterior = SceneManager.GetActiveScene();
        bool aberta = anterior.IsValid() && anterior.path == CenaPath;
        Scene cena = aberta ? anterior : EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Additive);
        try
        {
            TerritorioClique[] objetos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerritorioClique>(true)).ToArray();
            if (objetos.Length != 37 || objetos.Select(t => t.idTerritorio).Distinct(StringComparer.Ordinal).Count() != 37)
                throw new InvalidOperationException("Cena Riachuelo não contém 37 IDs únicos.");
            foreach (TerritorioClique territorio in objetos)
            {
                PolygonCollider2D collider = territorio.GetComponent<PolygonCollider2D>();
                Transform contador = territorio.transform.Find("ContadorModerno");
                if (collider == null || collider.pathCount == 0 || contador == null || !collider.OverlapPoint(contador.position) ||
                    contador.localScale != new Vector3(2f, 2f, 2f))
                    throw new InvalidOperationException("Collider/contador inválido: " + territorio.idTerritorio);
            }
        }
        finally
        {
            if (!aberta) EditorSceneManager.CloseScene(cena, true);
            if (!aberta && anterior.IsValid()) SceneManager.SetActiveScene(anterior);
        }
    }

    private static void ValidarArquivosFonte()
    {
        if (!File.Exists(Path.GetFullPath(ArtePath)) || !File.Exists(Path.GetFullPath(GabaritoPath))) throw new FileNotFoundException("Arte/gabarito Riachuelo ausente.");
        if (ObterDimensoes(ArtePath) != new Vector2Int(3015, 2311) || ObterDimensoes(GabaritoPath) != new Vector2Int(3015, 2311))
            throw new InvalidOperationException("Arte e gabarito devem ser 3015x2311 e estar em relação 1:1.");
        if (Territorios.Length != 37 || Regioes.Count != 6) throw new InvalidOperationException("Riachuelo deve ter 37 territórios e 6 regiões.");
        foreach (TerritorioConfig territorio in Territorios) if (!File.Exists(Path.GetFullPath(CaminhoMascara(territorio.Id)))) throw new FileNotFoundException("Máscara ausente: " + territorio.Id);
    }

    private static Vector2Int ObterDimensoes(string path)
    {
        Texture2D textura = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try { if (!ImageConversion.LoadImage(textura, File.ReadAllBytes(Path.GetFullPath(path)), false)) throw new InvalidOperationException("Imagem inválida: " + path); return new Vector2Int(textura.width, textura.height); }
        finally { UnityEngine.Object.DestroyImmediate(textura); }
    }

    private static TerritorioConfig T(string id, string nome, string regiao, float cx, float cy, float tx, float ty) => new TerritorioConfig(id, nome, regiao, cx, cy, tx, ty);
    private static (string A, string B) P(string a, string b) => (a, b);
    private static (string A, string B)[] ParesPorIndice(IEnumerable<string> pares) => pares.Select(valor => { string[] p = valor.Split('-'); return P(Territorios[int.Parse(p[0]) - 1].Id, Territorios[int.Parse(p[1]) - 1].Id); }).ToArray();
    private static string ChavePar(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
    private static string CaminhoMascara(string id) => PastaMascaras + "/" + id + ".png";
}
#endif

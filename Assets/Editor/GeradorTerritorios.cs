using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class GeradorTerritorios : EditorWindow
{
    private Texture2D fonte;
    private Texture2D texturaLeitura;
    private Texture2D overlay;

    private bool[] selecionado;
    private Color32[] pixels;

    private string nomeTerritorio = "NovoTerritorio";
private int tolerancia = 32;
private int tamanhoBorracha = 10;

private float zoom = 1f;
private Vector2 scrollPos = Vector2.zero;

// Lista de territórios já gerados
private List<string> nomesTerritorios = new List<string>();
private int indiceTerritorio = 0;

// Histórico para DESFAZER
private Stack<bool[]> historico = new Stack<bool[]>();

    private enum Ferramenta
    {
        Selecionar,
        Adicionar,
        Borracha
    }

    private Ferramenta ferramentaAtual = Ferramenta.Selecionar;

    private const string PastaSaida =
        "Assets/TerritoriosDefinitivos";

    // =========================================================
    // ABRIR EXTRATOR
    // =========================================================

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Extrator Visual de Territórios")]
    public static void Abrir()
    {
        GetWindow<GeradorTerritorios>(
            "Extrator de Territorios"
        );
    }

    // =========================================================
    // INTERFACE
    // =========================================================

    private void OnGUI()
    {
        GUILayout.Label(
            "War Dominion - Extrator de Territorios",
            EditorStyles.boldLabel
        );

        fonte = (Texture2D)EditorGUILayout.ObjectField(
            "Mapa fonte",
            fonte,
            typeof(Texture2D),
            false
        );

        if (nomesTerritorios.Count == 0)
{
    AtualizarListaTerritorios();
}

if (nomesTerritorios.Count > 0)
{
    int novoIndice =
        EditorGUILayout.Popup(
            "Territorio",
            indiceTerritorio,
            nomesTerritorios.ToArray()
        );

    if (novoIndice != indiceTerritorio)
    {
        indiceTerritorio = novoIndice;
        nomeTerritorio = nomesTerritorios[indiceTerritorio];

        // Se o mapa já estiver carregado,
        // carrega o território automaticamente.
        if (texturaLeitura != null)
        {
            CarregarTerritorioExistente();
        }
    }
}
else
{
    nomeTerritorio =
        EditorGUILayout.TextField(
            "Nome tecnico",
            nomeTerritorio
        );
}

        tolerancia =
            EditorGUILayout.IntSlider(
                "Tolerancia",
                tolerancia,
                0,
                100
            );

        GUILayout.Space(5);

        if (GUILayout.Button(
            "Carregar / Recarregar mapa"))
        {
            CarregarFonte();
        }

        if (texturaLeitura == null)
            return;

        GUILayout.Space(8);

        GUILayout.Label(
            "Ferramenta",
            EditorStyles.boldLabel
        );

        ferramentaAtual =
            (Ferramenta)GUILayout.Toolbar(
                (int)ferramentaAtual,
                new string[]
                {
                    "Selecionar",
                    "Adicionar",
                    "BORRACHA"
                }
            );

        if (ferramentaAtual ==
            Ferramenta.Borracha)
        {
            tamanhoBorracha =
    EditorGUILayout.IntSlider(
        "Tamanho da borracha",
        tamanhoBorracha,
        1,
        30
    );

zoom =
    EditorGUILayout.Slider(
        "Zoom",
        zoom,
        1f,
        10f
    );
        }

        GUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "SELECIONAR: clique em uma região.\n" +
            "ADICIONAR: soma outra região/ilha.\n" +
            "BORRACHA: clique ou arraste para apagar sujeiras.\n" +
            "SHIFT + clique também adiciona regiões.",
            MessageType.Info
        );

        GUILayout.BeginHorizontal();

if (GUILayout.Button(
    "Carregar territorio existente"))
{
    CarregarTerritorioExistente();
}

GUI.enabled = historico.Count > 0;

if (GUILayout.Button("DESFAZER"))
{
    Desfazer();
}

GUI.enabled = true;

if (GUILayout.Button(
    "Limpar selecao"))
{
    LimparSelecao();
}

GUILayout.EndHorizontal();

        GUILayout.Space(5);

        float larguraDisponivel =
    position.width - 20f;

float alturaDisponivel =
    Mathf.Max(
        250f,
        position.height - 380f
    );

Rect areaVisivel =
    GUILayoutUtility.GetRect(
        larguraDisponivel,
        alturaDisponivel
    );

float larguraImagem =
    larguraDisponivel * zoom;

float alturaImagem =
    larguraImagem *
    texturaLeitura.height /
    texturaLeitura.width;

Rect areaConteudo =
    new Rect(
        0,
        0,
        larguraImagem,
        alturaImagem
    );

scrollPos =
    GUI.BeginScrollView(
        areaVisivel,
        scrollPos,
        areaConteudo
    );

Rect imagemRect =
    new Rect(
        0,
        0,
        larguraImagem,
        alturaImagem
    );

GUI.DrawTexture(
    imagemRect,
    texturaLeitura,
    ScaleMode.StretchToFill,
    false
);

if (overlay != null)
{
    GUI.DrawTexture(
        imagemRect,
        overlay,
        ScaleMode.StretchToFill,
        true
    );
}

ProcessarMouse(imagemRect);

GUI.EndScrollView();

        GUILayout.Space(8);

        GUI.backgroundColor =
            new Color(0.45f, 1f, 0.45f);

        if (GUILayout.Button(
            "GERAR / SOBRESCREVER TERRITORIO",
            GUILayout.Height(35)))
        {
            GerarTerritorio();
        }

        GUI.backgroundColor = Color.white;
    }

    // =========================================================
    // CARREGAR MAPA
    // =========================================================

    private void CarregarFonte()
    {
        if (fonte == null)
        {
            EditorUtility.DisplayDialog(
                "War Dominion",
                "Selecione o ClassicMap no campo Mapa fonte.",
                "OK"
            );

            return;
        }

        string caminhoAsset =
            AssetDatabase.GetAssetPath(fonte);

        byte[] bytes =
            File.ReadAllBytes(caminhoAsset);

        if (texturaLeitura != null)
            DestroyImmediate(texturaLeitura);

        texturaLeitura =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false
            );

        texturaLeitura.LoadImage(bytes);

        pixels =
            texturaLeitura.GetPixels32();

        selecionado =
            new bool[
                texturaLeitura.width *
                texturaLeitura.height
            ];

        CriarOverlay();

        Repaint();
    }

    // =========================================================
    // CARREGAR TERRITÓRIO JÁ GERADO
    // =========================================================

    private void CarregarTerritorioExistente()
    {
        if (texturaLeitura == null)
            return;

            historico.Clear();

        string nomeSeguro =
            nomeTerritorio.Replace(" ", "");

        string caminho =
            PastaSaida +
            "/" +
            nomeSeguro +
            ".png";

        if (!File.Exists(caminho))
        {
            EditorUtility.DisplayDialog(
                "War Dominion",
                "Não encontrei:\n" + caminho,
                "OK"
            );

            return;
        }

        byte[] bytes =
            File.ReadAllBytes(caminho);

        Texture2D mascara =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false
            );

        mascara.LoadImage(bytes);

        if (mascara.width !=
                texturaLeitura.width ||
            mascara.height !=
                texturaLeitura.height)
        {
            DestroyImmediate(mascara);

            EditorUtility.DisplayDialog(
                "War Dominion",
                "O território não tem o mesmo tamanho do mapa.",
                "OK"
            );

            return;
        }

        Color32[] mascaraPixels =
            mascara.GetPixels32();

        selecionado =
            new bool[mascaraPixels.Length];

        for (int i = 0;
             i < mascaraPixels.Length;
             i++)
        {
            selecionado[i] =
                mascaraPixels[i].a > 10;
        }

        DestroyImmediate(mascara);

        AtualizarOverlay();
        Repaint();

        Debug.Log(
            "Território carregado para edição: " +
            nomeSeguro
        );
    }

    // =========================================================
    // DIMENSÃO DA IMAGEM
    // =========================================================

    private Rect CalcularRectImagem(Rect area)
    {
        float proporcaoImagem =
            (float)texturaLeitura.width /
            texturaLeitura.height;

        float proporcaoArea =
            area.width /
            area.height;

        if (proporcaoImagem >
            proporcaoArea)
        {
            float altura =
                area.width /
                proporcaoImagem;

            return new Rect(
                area.x,
                area.y +
                (area.height - altura) / 2f,
                area.width,
                altura
            );
        }

        float largura =
            area.height *
            proporcaoImagem;

        return new Rect(
            area.x +
            (area.width - largura) / 2f,
            area.y,
            largura,
            area.height
        );
    }

    // =========================================================
    // MOUSE
    // =========================================================

    private void ProcessarMouse(Rect imagemRect)
    {
        Event e = Event.current;

        bool clique =
            e.type == EventType.MouseDown &&
            e.button == 0;

        bool arraste =
            e.type == EventType.MouseDrag &&
            e.button == 0;

        if (!clique && !arraste)
            return;

        if (!imagemRect.Contains(
            e.mousePosition))
            return;

        Vector2Int pixel =
            ConverterMouseParaPixel(
                e.mousePosition,
                imagemRect
            );

        // BORRACHA
        if (ferramentaAtual ==
    Ferramenta.Borracha)
{
    // Registra apenas no início do traço.
    if (clique)
    {
        RegistrarEstadoParaDesfazer();
    }

    AplicarBorracha(
        pixel.x,
        pixel.y
    );

            AtualizarOverlay();

            e.Use();
            Repaint();

            return;
        }

        // Seleção só acontece no clique,
        // não no arraste.
        if (!clique)
            return;

        RegistrarEstadoParaDesfazer();    

        bool adicionar =
            ferramentaAtual ==
            Ferramenta.Adicionar ||
            e.shift;

        if (!adicionar)
        {
            selecionado =
                new bool[
                    texturaLeitura.width *
                    texturaLeitura.height
                ];
        }

        FloodFill(
            pixel.x,
            pixel.y
        );

        AtualizarOverlay();

        e.Use();
        Repaint();
    }

    private Vector2Int ConverterMouseParaPixel(
        Vector2 mouse,
        Rect imagemRect)
    {
        float normalX =
            (mouse.x - imagemRect.x) /
            imagemRect.width;

        float normalY =
            1f -
            (
                (mouse.y - imagemRect.y) /
                imagemRect.height
            );

        int x =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    normalX *
                    texturaLeitura.width
                ),
                0,
                texturaLeitura.width - 1
            );

        int y =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    normalY *
                    texturaLeitura.height
                ),
                0,
                texturaLeitura.height - 1
            );

        return new Vector2Int(x, y);
    }

    // =========================================================
    // BORRACHA
    // =========================================================

    private void AplicarBorracha(
    int centroX,
    int centroY)
{
    int largura =
        texturaLeitura.width;

    int altura =
        texturaLeitura.height;

    int raio =
        tamanhoBorracha;

    int raioQuadrado =
        raio * raio;

    for (int y =
            centroY - raio;
         y <= centroY + raio;
         y++)
    {
        if (y < 0 || y >= altura)
            continue;

        for (int x =
                centroX - raio;
             x <= centroX + raio;
             x++)
        {
            if (x < 0 ||
                x >= largura)
                continue;

            int dx =
                x - centroX;

            int dy =
                y - centroY;

            if (
                dx * dx +
                dy * dy >
                raioQuadrado
            )
                continue;

            int indice =
                y * largura + x;

            // Em vez de apagar a máscara,
            // ADICIONA o pixel ao território.
            selecionado[indice] = true;
        }
    }
}

    // =========================================================
    // FLOOD FILL
    // =========================================================

    private void FloodFill(
        int inicioX,
        int inicioY)
    {
        int largura =
            texturaLeitura.width;

        int altura =
            texturaLeitura.height;

        int inicio =
            inicioY *
            largura +
            inicioX;

        Color32 corInicial =
            pixels[inicio];

        bool[] visitado =
            new bool[pixels.Length];

        Queue<int> fila =
            new Queue<int>();

        fila.Enqueue(inicio);

        visitado[inicio] = true;

        while (fila.Count > 0)
        {
            int indice =
                fila.Dequeue();

            if (!CorParecida(
                pixels[indice],
                corInicial))
                continue;

            selecionado[indice] =
                true;

            int x =
                indice % largura;

            int y =
                indice / largura;

            AdicionarFila(
                x + 1, y,
                largura, altura,
                visitado, fila
            );

            AdicionarFila(
                x - 1, y,
                largura, altura,
                visitado, fila
            );

            AdicionarFila(
                x, y + 1,
                largura, altura,
                visitado, fila
            );

            AdicionarFila(
                x, y - 1,
                largura, altura,
                visitado, fila
            );
        }
    }

    private void AdicionarFila(
        int x,
        int y,
        int largura,
        int altura,
        bool[] visitado,
        Queue<int> fila)
    {
        if (x < 0 ||
            x >= largura ||
            y < 0 ||
            y >= altura)
            return;

        int indice =
            y * largura + x;

        if (visitado[indice])
            return;

        visitado[indice] =
            true;

        fila.Enqueue(indice);
    }

    private bool CorParecida(
        Color32 a,
        Color32 b)
    {
        return
            Mathf.Abs(a.r - b.r)
                <= tolerancia &&
            Mathf.Abs(a.g - b.g)
                <= tolerancia &&
            Mathf.Abs(a.b - b.b)
                <= tolerancia &&
            Mathf.Abs(a.a - b.a)
                <= tolerancia;
    }

    // =========================================================
    // OVERLAY VERDE
    // =========================================================

    private void CriarOverlay()
    {
        if (overlay != null)
            DestroyImmediate(overlay);

        overlay =
            new Texture2D(
                texturaLeitura.width,
                texturaLeitura.height,
                TextureFormat.RGBA32,
                false
            );

        overlay.filterMode =
            FilterMode.Point;

        AtualizarOverlay();
    }

    private void AtualizarOverlay()
    {
        if (overlay == null ||
            selecionado == null)
            return;

        Color32[] cores =
            new Color32[
                selecionado.Length
            ];

        Color32 destaque =
            new Color32(
                0,
                255,
                80,
                120
            );

        Color32 transparente =
            new Color32(
                0,
                0,
                0,
                0
            );

        for (int i = 0;
             i < selecionado.Length;
             i++)
        {
            cores[i] =
                selecionado[i]
                ? destaque
                : transparente;
        }

        overlay.SetPixels32(cores);
        overlay.Apply();
    }

    private void LimparSelecao()
    {

        RegistrarEstadoParaDesfazer();

        if (texturaLeitura == null)
            return;

        selecionado =
            new bool[
                texturaLeitura.width *
                texturaLeitura.height
            ];

        AtualizarOverlay();
        Repaint();
    }

    // =========================================================
    // GERAR PNG
    // =========================================================

    private void GerarTerritorio()
    {
        if (texturaLeitura == null ||
            selecionado == null)
            return;

        if (string.IsNullOrWhiteSpace(
            nomeTerritorio))
        {
            EditorUtility.DisplayDialog(
                "War Dominion",
                "Digite o nome técnico do território.",
                "OK"
            );

            return;
        }

        bool existeSelecao =
            false;

        Color32[] resultado =
            new Color32[
                selecionado.Length
            ];

        for (int i = 0;
             i < selecionado.Length;
             i++)
        {
            if (selecionado[i])
            {
                resultado[i] =
                    new Color32(
                        255,
                        255,
                        255,
                        255
                    );

                existeSelecao = true;
            }
            else
            {
                resultado[i] =
                    new Color32(
                        0,
                        0,
                        0,
                        0
                    );
            }
        }

        if (!existeSelecao)
        {
            EditorUtility.DisplayDialog(
                "War Dominion",
                "Nenhuma região selecionada.",
                "OK"
            );

            return;
        }

        if (!Directory.Exists(
            PastaSaida))
        {
            Directory.CreateDirectory(
                PastaSaida
            );
        }

        Texture2D saida =
            new Texture2D(
                texturaLeitura.width,
                texturaLeitura.height,
                TextureFormat.RGBA32,
                false
            );

        saida.SetPixels32(resultado);
        saida.Apply();

        string nomeSeguro =
            nomeTerritorio.Replace(
                " ",
                ""
            );

        string caminho =
            PastaSaida +
            "/" +
            nomeSeguro +
            ".png";

        File.WriteAllBytes(
            caminho,
            saida.EncodeToPNG()
        );

        DestroyImmediate(saida);

        AssetDatabase.Refresh();

        TextureImporter importer =
            AssetImporter.GetAtPath(
                caminho
            ) as TextureImporter;

        if (importer != null)
        {
            importer.textureType =
                TextureImporterType.Sprite;

            importer.spriteImportMode =
                SpriteImportMode.Single;

            importer.spritePixelsPerUnit =
                100;

            importer.alphaIsTransparency =
                true;

            importer.mipmapEnabled =
                false;

            importer.filterMode =
                FilterMode.Bilinear;

            importer.textureCompression =
                TextureImporterCompression
                    .Uncompressed;

            importer.SaveAndReimport();
        }

        Debug.Log(
            "Território salvo/atualizado: " +
            nomeSeguro
        );

        AtualizarListaTerritorios();

    }


// =========================================================
// HISTÓRICO / DESFAZER
// =========================================================

private void RegistrarEstadoParaDesfazer()
{
    if (selecionado == null)
        return;

    bool[] copia =
        new bool[selecionado.Length];

    System.Array.Copy(
        selecionado,
        copia,
        selecionado.Length
    );

    historico.Push(copia);

    // Evita histórico infinito
    while (historico.Count > 50)
    {
        Stack<bool[]> temporario =
            new Stack<bool[]>(historico);

        temporario.Pop();

        historico =
            new Stack<bool[]>(temporario);
    }
}

private void Desfazer()
{
    if (historico.Count == 0)
        return;

    selecionado =
        historico.Pop();

    AtualizarOverlay();

    Repaint();

    Debug.Log("Última alteração desfeita.");
}


// =========================================================
// LISTA AUTOMÁTICA DOS TERRITÓRIOS
// =========================================================

private void AtualizarListaTerritorios()
{
    string nomeAtual = nomeTerritorio;

    nomesTerritorios.Clear();

    string[] guids =
        AssetDatabase.FindAssets(
            "t:Sprite",
            new[]
            {
                PastaSaida
            }
        );

    foreach (string guid in guids)
    {
        string caminho =
            AssetDatabase.GUIDToAssetPath(guid);

        string nome =
            Path.GetFileNameWithoutExtension(
                caminho
            );

        if (!nomesTerritorios.Contains(nome))
        {
            nomesTerritorios.Add(nome);
        }
    }

    nomesTerritorios.Sort();

    if (nomesTerritorios.Count == 0)
        return;

    int encontrado =
        nomesTerritorios.IndexOf(nomeAtual);

    indiceTerritorio =
        encontrado >= 0
        ? encontrado
        : 0;

    nomeTerritorio =
        nomesTerritorios[indiceTerritorio];
}
    private void OnDestroy()
    {
        if (texturaLeitura != null)
            DestroyImmediate(
                texturaLeitura
            );

        if (overlay != null)
            DestroyImmediate(
                overlay
            );
    }
}

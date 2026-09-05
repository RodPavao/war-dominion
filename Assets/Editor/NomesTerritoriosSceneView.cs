using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NomesTerritoriosSceneView
{
    private const string MenuMostrar = "War Dominion/Ferramentas de Desenvolvimento/Visualização/Mostrar Nomes dos Territórios";
    private const string MenuEditar = "War Dominion/Ferramentas de Desenvolvimento/Visualização/Editar Posições dos Nomes";
    private const string ChaveMostrar = "WarGame.Visualizacao.MostrarNomesTerritorios";
    private const string ChaveEditar = "WarGame.Visualizacao.EditarPosicoesNomesTerritorios";
    internal const int TamanhoFonteMinimo = 8;
    internal const int TamanhoFonteMaximo = 32;
    private const int TamanhoFonteAutomatico = 10;
    private const int TamanhoFonteAutomaticoReduzido = 9;
    private const float ProporcaoZoomParaExibir = 0.72f;

    private static GUIStyle estiloNome;
    private static string territorioEmArraste;
    private static DefinicaoMapa mapaPendenteSalvar;

    static NomesTerritoriosSceneView()
    {
        EditorApplication.delayCall -= Inicializar;
        EditorApplication.delayCall += Inicializar;
    }

    private static void Inicializar()
    {
        SceneView.duringSceneGui -= DesenharNomes;
        SceneView.duringSceneGui += DesenharNomes;
        Undo.undoRedoPerformed -= AoDesfazerOuRefazer;
        Undo.undoRedoPerformed += AoDesfazerOuRefazer;
    }

    internal static bool NomesAtivos => SessionState.GetBool(ChaveMostrar, false);
    internal static bool EdicaoAtiva => SessionState.GetBool(ChaveEditar, false);

    [MenuItem(MenuMostrar)]
    private static void AlternarNomes()
    {
        bool estado = !NomesAtivos;
        SessionState.SetBool(ChaveMostrar, estado);
        if (!estado)
            SessionState.SetBool(ChaveEditar, false);
        AtualizarMenusEView();
    }

    [MenuItem(MenuMostrar, true)]
    private static bool ValidarMenuMostrar()
    {
        Menu.SetChecked(MenuMostrar, NomesAtivos);
        return true;
    }

    [MenuItem(MenuEditar)]
    private static void AlternarEdicao()
    {
        bool estado = !EdicaoAtiva;
        SessionState.SetBool(ChaveEditar, estado);
        if (estado)
            SessionState.SetBool(ChaveMostrar, true);
        AtualizarMenusEView();
    }

    [MenuItem(MenuEditar, true)]
    private static bool ValidarMenuEditar()
    {
        Menu.SetChecked(MenuEditar, EdicaoAtiva);
        return true;
    }

    private static void AtualizarMenusEView()
    {
        Menu.SetChecked(MenuMostrar, NomesAtivos);
        Menu.SetChecked(MenuEditar, EdicaoAtiva);
        SceneView.RepaintAll();
    }

    private static void AoDesfazerOuRefazer()
    {
        SceneView.RepaintAll();
        ConfiguradorNomesTerritoriosWindow.RepaintAberta();
    }

    private static void DesenharNomes(SceneView sceneView)
    {
        if (!NomesAtivos)
            return;

        GUIStyle estilo = ObterEstiloNome();
        DefinicaoMapa mapa = ObterMapaAtivo();
        if (estilo == null || mapa == null)
            return;
        if (!EdicaoAtiva && !ZoomPermiteExibirNomes(sceneView, mapa))
            return;

        TerritorioClique[] territorios = UnityEngine.Object.FindObjectsByType<TerritorioClique>(FindObjectsInactive.Include);
        foreach (TerritorioClique territorio in territorios)
        {
            if (territorio == null || string.IsNullOrWhiteSpace(territorio.idTerritorio) || !territorio.gameObject.scene.IsValid())
                continue;

            DefinicaoTerritorioMapa dadosMapa = EncontrarDadosTerritorio(mapa, territorio.idTerritorio);
            if (dadosMapa == null)
                continue;

            string nomeExibido = ConverterNomeTecnico(territorio.idTerritorio);
            DadosLabel dadosLabel = ObterDadosLabel(territorio, dadosMapa, nomeExibido);
            estilo.fontSize = dadosLabel.FontSize;
            Vector3 posicao = EdicaoAtiva
                ? DesenharHandle(mapa, territorio, dadosMapa, dadosLabel.Posicao)
                : dadosLabel.Posicao;

            if (Event.current.type == EventType.Repaint)
                Handles.Label(posicao, nomeExibido, estilo);
        }

        if (Event.current.rawType == EventType.MouseUp)
            FinalizarArraste();
    }

    private static Vector3 DesenharHandle(DefinicaoMapa mapa, TerritorioClique territorio, DefinicaoTerritorioMapa dadosMapa, Vector3 posicao)
    {
        float tamanho = HandleUtility.GetHandleSize(posicao) * 0.11f;
        Color corAnterior = Handles.color;
        Handles.color = dadosMapa.possuiPosicaoNomeManual
            ? new Color(0.1f, 0.45f, 1f, 0.9f)
            : new Color(1f, 0.55f, 0f, 0.9f);
        EditorGUI.BeginChangeCheck();
        Vector3 novaPosicao = Handles.FreeMoveHandle(posicao, tamanho, Vector3.zero, Handles.RectangleHandleCap);
        Handles.color = corAnterior;
        if (!EditorGUI.EndChangeCheck())
            return posicao;

        if (territorioEmArraste != dadosMapa.id)
        {
            Undo.RecordObject(mapa, "Mover nome de " + dadosMapa.id);
            territorioEmArraste = dadosMapa.id;
        }
        dadosMapa.posicaoNomeManual = territorio.transform.InverseTransformPoint(novaPosicao);
        dadosMapa.possuiPosicaoNomeManual = true;
        EditorUtility.SetDirty(mapa);
        mapaPendenteSalvar = mapa;
        ConfiguradorNomesTerritoriosWindow.RepaintAberta();
        return novaPosicao;
    }

    private static void FinalizarArraste()
    {
        territorioEmArraste = null;
        if (mapaPendenteSalvar == null)
            return;
        AssetDatabase.SaveAssetIfDirty(mapaPendenteSalvar);
        mapaPendenteSalvar = null;
    }

    private static GUIStyle ObterEstiloNome()
    {
        if (estiloNome != null)
            return estiloNome;
        if (GUI.skin == null)
            return null;

        estiloNome = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        AplicarCorPreta(estiloNome.normal);
        AplicarCorPreta(estiloNome.hover);
        AplicarCorPreta(estiloNome.active);
        AplicarCorPreta(estiloNome.focused);
        AplicarCorPreta(estiloNome.onNormal);
        AplicarCorPreta(estiloNome.onHover);
        AplicarCorPreta(estiloNome.onActive);
        AplicarCorPreta(estiloNome.onFocused);
        return estiloNome;
    }

    private static bool ZoomPermiteExibirNomes(SceneView sceneView, DefinicaoMapa mapa)
    {
        if (sceneView == null)
            return false;
        float tamanhoMapa = Mathf.Max(0.01f, mapa.TamanhoOrtografico);
        return sceneView.size <= tamanhoMapa * ProporcaoZoomParaExibir;
    }

    private static void AplicarCorPreta(GUIStyleState estado) => estado.textColor = Color.black;

    internal readonly struct DadosLabel
    {
        public readonly Vector3 Posicao;
        public readonly int FontSize;
        public DadosLabel(Vector3 posicao, int fontSize)
        {
            Posicao = posicao;
            FontSize = fontSize;
        }
    }

    internal static DadosLabel ObterDadosLabel(TerritorioClique territorio, DefinicaoTerritorioMapa dadosMapa, string nomeExibido)
    {
        PolygonCollider2D collider = territorio.GetComponent<PolygonCollider2D>();
        Vector2[] maiorPath = ObterMaiorPath(collider);
        int automatico = maiorPath == null || maiorPath.Length < 3
            ? TamanhoFonteAutomatico
            : CalcularTamanhoFonte(territorio, maiorPath, nomeExibido);
        int tamanho = dadosMapa.tamanhoFonteNome > 0
            ? Mathf.Clamp(dadosMapa.tamanhoFonteNome, TamanhoFonteMinimo, TamanhoFonteMaximo)
            : automatico;

        if (dadosMapa.possuiPosicaoNomeManual)
            return new DadosLabel(territorio.transform.TransformPoint(dadosMapa.posicaoNomeManual), tamanho);
        if (maiorPath == null || maiorPath.Length < 3)
            return new DadosLabel(territorio.transform.position, tamanho);

        Transform contador = territorio.transform.Find("ContadorModerno");
        Vector2 contadorLocal = contador != null
            ? territorio.transform.InverseTransformPoint(contador.position)
            : new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 pontoLocal = EncontrarPontoInterno(maiorPath, contadorLocal, ObterRaioContadorLocal(territorio, contador));
        return new DadosLabel(territorio.transform.TransformPoint(pontoLocal), tamanho);
    }

    internal static DefinicaoMapa ObterMapaAtivo()
    {
        if (MapaAtivo.Instance != null && MapaAtivo.Instance.Definicao != null)
            return MapaAtivo.Instance.Definicao;

        SpriteRenderer[] visuais = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);
        string[] guidsMapas = AssetDatabase.FindAssets("t:DefinicaoMapa");
        foreach (string guid in guidsMapas)
        {
            DefinicaoMapa candidato = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidato == null || candidato.ArteBase == null)
                continue;
            foreach (SpriteRenderer visual in visuais)
                if (visual != null && visual.gameObject.scene.IsValid() && visual.sprite == candidato.ArteBase)
                    return candidato;
        }

        CatalogoMapas catalogo = AssetDatabase.LoadAssetAtPath<CatalogoMapas>("Assets/Resources/Mapas/CatalogoMapas.asset");
        return catalogo != null ? catalogo.MapaPadrao : null;
    }

    internal static DefinicaoTerritorioMapa EncontrarDadosTerritorio(DefinicaoMapa mapa, string id)
    {
        if (mapa == null)
            return null;
        foreach (DefinicaoTerritorioMapa dados in mapa.Territorios)
            if (dados != null && string.Equals(dados.id, id, StringComparison.Ordinal))
                return dados;
        return null;
    }

    private static Vector2[] ObterMaiorPath(PolygonCollider2D collider)
    {
        if (collider == null || collider.pathCount == 0)
            return null;
        Vector2[] maiorPath = null;
        float maiorArea = 0f;
        for (int indice = 0; indice < collider.pathCount; indice++)
        {
            Vector2[] path = collider.GetPath(indice);
            float area = Mathf.Abs(CalcularArea(path));
            if (area > maiorArea)
            {
                maiorArea = area;
                maiorPath = path;
            }
        }
        return maiorPath;
    }

    private static Vector2 EncontrarPontoInterno(Vector2[] path, Vector2 contador, float raioContador)
    {
        Vector2 minimo = path[0];
        Vector2 maximo = path[0];
        foreach (Vector2 ponto in path)
        {
            minimo = Vector2.Min(minimo, ponto);
            maximo = Vector2.Max(maximo, ponto);
        }
        const int Divisoes = 18;
        Vector2 melhorPonto = CalcularCentroide(path);
        float melhorPontuacao = AvaliarPonto(melhorPonto, path, contador, raioContador);
        for (int y = 1; y < Divisoes; y++)
        {
            for (int x = 1; x < Divisoes; x++)
            {
                Vector2 candidato = new Vector2(
                    Mathf.Lerp(minimo.x, maximo.x, x / (float)Divisoes),
                    Mathf.Lerp(minimo.y, maximo.y, y / (float)Divisoes));
                if (!PontoDentroDoPoligono(candidato, path))
                    continue;
                float pontuacao = AvaliarPonto(candidato, path, contador, raioContador);
                if (pontuacao > melhorPontuacao)
                {
                    melhorPontuacao = pontuacao;
                    melhorPonto = candidato;
                }
            }
        }
        return melhorPonto;
    }

    private static float AvaliarPonto(Vector2 ponto, Vector2[] path, Vector2 contador, float raioContador)
    {
        if (!PontoDentroDoPoligono(ponto, path))
            return float.NegativeInfinity;
        float distanciaBorda = float.PositiveInfinity;
        for (int indice = 0; indice < path.Length; indice++)
            distanciaBorda = Mathf.Min(distanciaBorda, DistanciaAoSegmento(ponto, path[indice], path[(indice + 1) % path.Length]));
        float distanciaContador = float.IsInfinity(contador.x)
            ? float.PositiveInfinity
            : Vector2.Distance(ponto, contador) - raioContador - 0.08f;
        return Mathf.Min(distanciaBorda, distanciaContador * 0.8f);
    }

    private static float DistanciaAoSegmento(Vector2 ponto, Vector2 inicio, Vector2 fim)
    {
        Vector2 segmento = fim - inicio;
        float comprimentoQuadrado = segmento.sqrMagnitude;
        if (comprimentoQuadrado <= Mathf.Epsilon)
            return Vector2.Distance(ponto, inicio);
        float t = Mathf.Clamp01(Vector2.Dot(ponto - inicio, segmento) / comprimentoQuadrado);
        return Vector2.Distance(ponto, inicio + segmento * t);
    }

    private static bool PontoDentroDoPoligono(Vector2 ponto, Vector2[] path)
    {
        bool dentro = false;
        for (int atual = 0, anterior = path.Length - 1; atual < path.Length; anterior = atual++)
        {
            Vector2 a = path[atual];
            Vector2 b = path[anterior];
            bool cruza = (a.y > ponto.y) != (b.y > ponto.y) && ponto.x < (b.x - a.x) * (ponto.y - a.y) / (b.y - a.y) + a.x;
            if (cruza)
                dentro = !dentro;
        }
        return dentro;
    }

    private static Vector2 CalcularCentroide(Vector2[] path)
    {
        float areaAcumulada = 0f;
        Vector2 centro = Vector2.zero;
        for (int indice = 0; indice < path.Length; indice++)
        {
            Vector2 atual = path[indice];
            Vector2 proximo = path[(indice + 1) % path.Length];
            float cruzado = atual.x * proximo.y - proximo.x * atual.y;
            areaAcumulada += cruzado;
            centro += (atual + proximo) * cruzado;
        }
        if (Mathf.Abs(areaAcumulada) <= Mathf.Epsilon)
        {
            foreach (Vector2 ponto in path)
                centro += ponto;
            return centro / path.Length;
        }
        return centro / (3f * areaAcumulada);
    }

    private static float ObterRaioContadorLocal(TerritorioClique territorio, Transform contador)
    {
        if (contador == null)
            return 0f;
        Collider2D colliderContador = contador.GetComponent<Collider2D>();
        if (colliderContador == null)
            colliderContador = contador.GetComponentInChildren<Collider2D>();
        float raioMundo;
        if (colliderContador != null)
        {
            Vector3 extensao = colliderContador.bounds.extents;
            raioMundo = Mathf.Max(extensao.x, extensao.y);
        }
        else
        {
            Vector3 escalaContador = contador.lossyScale;
            raioMundo = Mathf.Max(Mathf.Abs(escalaContador.x), Mathf.Abs(escalaContador.y)) * 0.5f;
        }
        Vector3 escalaTerritorio = territorio.transform.lossyScale;
        float escala = Mathf.Max(Mathf.Abs(escalaTerritorio.x), Mathf.Abs(escalaTerritorio.y));
        return escala > Mathf.Epsilon ? raioMundo / escala : raioMundo;
    }

    private static int CalcularTamanhoFonte(TerritorioClique territorio, Vector2[] path, string nomeExibido)
    {
        Vector2 minimo = HandleUtility.WorldToGUIPoint(territorio.transform.TransformPoint(path[0]));
        Vector2 maximo = minimo;
        foreach (Vector2 ponto in path)
        {
            Vector2 gui = HandleUtility.WorldToGUIPoint(territorio.transform.TransformPoint(ponto));
            minimo = Vector2.Min(minimo, gui);
            maximo = Vector2.Max(maximo, gui);
        }
        float largura = Mathf.Abs(maximo.x - minimo.x);
        float altura = Mathf.Abs(maximo.y - minimo.y);
        float larguraNecessaria = nomeExibido.Length * TamanhoFonteAutomatico * 0.56f;
        float alturaNecessaria = TamanhoFonteAutomatico * 1.2f;
        return largura >= larguraNecessaria && altura >= alturaNecessaria
            ? TamanhoFonteAutomatico
            : TamanhoFonteAutomaticoReduzido;
    }

    private static float CalcularArea(Vector2[] pontos)
    {
        if (pontos == null || pontos.Length < 3)
            return 0f;
        float area = 0f;
        for (int indice = 0; indice < pontos.Length; indice++)
        {
            Vector2 atual = pontos[indice];
            Vector2 proximo = pontos[(indice + 1) % pontos.Length];
            area += atual.x * proximo.y - proximo.x * atual.y;
        }
        return area * 0.5f;
    }

    public static string ConverterNomeTecnico(string idTerritorio)
    {
        if (string.IsNullOrWhiteSpace(idTerritorio))
            return string.Empty;
        string siglas = Regex.Replace(idTerritorio, "(?<=[A-Z])(?=[A-Z][a-z])", " ");
        return Regex.Replace(siglas, "(?<=[a-z0-9])(?=[A-Z])", " ");
    }
}

public sealed class ConfiguradorNomesTerritoriosWindow : EditorWindow
{
    private static ConfiguradorNomesTerritoriosWindow instancia;
    private int indiceSelecionado;

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Visualização/Configurar Nomes dos Territórios")]
    private static void Abrir()
    {
        instancia = GetWindow<ConfiguradorNomesTerritoriosWindow>("Nomes dos Territórios");
        instancia.minSize = new Vector2(360f, 280f);
        instancia.Show();
    }

    internal static void RepaintAberta()
    {
        if (instancia != null)
            instancia.Repaint();
    }

    private void OnEnable() => instancia = this;
    private void OnDisable()
    {
        if (instancia == this)
            instancia = null;
    }

    private void OnGUI()
    {
        DefinicaoMapa mapa = NomesTerritoriosSceneView.ObterMapaAtivo();
        if (mapa == null || mapa.Territorios.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma definição de mapa ativa foi encontrada.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Mapa ativo", mapa.NomeExibido);
        string[] opcoes = new string[mapa.Territorios.Count];
        for (int indice = 0; indice < mapa.Territorios.Count; indice++)
        {
            DefinicaoTerritorioMapa item = mapa.Territorios[indice];
            opcoes[indice] = item == null ? "(inválido)" : NomesTerritoriosSceneView.ConverterNomeTecnico(item.id);
        }

        indiceSelecionado = Mathf.Clamp(indiceSelecionado, 0, opcoes.Length - 1);
        indiceSelecionado = EditorGUILayout.Popup("Território", indiceSelecionado, opcoes);
        DefinicaoTerritorioMapa dados = mapa.Territorios[indiceSelecionado];
        if (dados == null)
            return;

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("ID técnico", dados.id);
            EditorGUILayout.TextField("Nome exibido", NomesTerritoriosSceneView.ConverterNomeTecnico(dados.id));
            EditorGUILayout.TextField("Posicionamento", dados.possuiPosicaoNomeManual ? "Manual" : "Automático");
            EditorGUILayout.Vector2Field("Posição manual", dados.posicaoNomeManual);
        }

        EditorGUI.BeginChangeCheck();
        int novoTamanho = EditorGUILayout.IntSlider(
            new GUIContent("Tamanho da fonte", "Zero mantém o tamanho automático."),
            dados.tamanhoFonteNome,
            0,
            NomesTerritoriosSceneView.TamanhoFonteMaximo);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mapa, "Alterar tamanho do nome de " + dados.id);
            dados.tamanhoFonteNome = novoTamanho == 0 ? 0 : Mathf.Max(NomesTerritoriosSceneView.TamanhoFonteMinimo, novoTamanho);
            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssetIfDirty(mapa);
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox(
            dados.tamanhoFonteNome == 0
                ? "Tamanho automático ativo. Mova o controle para salvar um tamanho individual."
                : "Tamanho individual salvo neste mapa.",
            MessageType.None);

        using (new EditorGUI.DisabledScope(!dados.possuiPosicaoNomeManual))
        {
            if (GUILayout.Button("Restaurar posição automática"))
            {
                Undo.RecordObject(mapa, "Restaurar posição automática de " + dados.id);
                dados.possuiPosicaoNomeManual = false;
                EditorUtility.SetDirty(mapa);
                AssetDatabase.SaveAssetIfDirty(mapa);
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("Restaurar todos para automático") && EditorUtility.DisplayDialog(
            "Restaurar todas as posições?",
            "Todos os overrides manuais de posição deste mapa serão removidos. Os tamanhos não serão alterados.",
            "Restaurar",
            "Cancelar"))
        {
            Undo.RecordObject(mapa, "Restaurar todas as posições automáticas");
            foreach (DefinicaoTerritorioMapa item in mapa.Territorios)
                if (item != null)
                    item.possuiPosicaoNomeManual = false;
            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssetIfDirty(mapa);
            SceneView.RepaintAll();
        }
    }
}

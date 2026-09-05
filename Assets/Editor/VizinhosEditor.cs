using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class VizinhosEditor : EditorWindow
{
    private enum ModoEdicao { EscolherBase, EditarVizinhos }

    private const string CaminhoCatalogo = "Mapas/CatalogoMapas";
    private static readonly Color CorBase = new Color(1f, 0.75f, 0.05f, 1f);
    private static readonly Color CorVizinho = new Color(0.05f, 0.9f, 1f, 1f);
    private static readonly Color CorHover = new Color(1f, 0.2f, 0.75f, 1f);

    private DefinicaoMapa mapa;
    private TerritorioClique territorioBase;
    private TerritorioClique territorioHover;
    private ModoEdicao modo = ModoEdicao.EscolherBase;
    private TipoConexaoMapa tipoNovaConexao = TipoConexaoMapa.Terrestre;
    private Vector2 scroll;

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Editor de Vizinhanças")]
    private static void Abrir()
    {
        VizinhosEditor janela = GetWindow<VizinhosEditor>("Vizinhanças");
        janela.minSize = new Vector2(360f, 430f);
        janela.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DesenharSceneView;
        SceneView.duringSceneGui += DesenharSceneView;
        Undo.undoRedoPerformed -= AoDesfazerRefazer;
        Undo.undoRedoPerformed += AoDesfazerRefazer;
        EditorApplication.hierarchyChanged -= AtualizarMapaAtivo;
        EditorApplication.hierarchyChanged += AtualizarMapaAtivo;
        AtualizarMapaAtivo();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DesenharSceneView;
        Undo.undoRedoPerformed -= AoDesfazerRefazer;
        EditorApplication.hierarchyChanged -= AtualizarMapaAtivo;
        SceneView.RepaintAll();
    }

    private void OnFocus() => AtualizarMapaAtivo();

    private void AoDesfazerRefazer()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        AtualizarMapaAtivoSeNecessario();
        EditorGUILayout.LabelField("Editor Visual de Vizinhanças", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Mapa atual", mapa, typeof(DefinicaoMapa), false);
            EditorGUILayout.IntField("Territórios", mapa != null ? mapa.Territorios.Count : 0);
        }

        if (mapa == null)
        {
            EditorGUILayout.HelpBox("Nenhuma DefinicaoMapa do catálogo corresponde à arte ou aos IDs da cena ativa.", MessageType.Warning);
            if (GUILayout.Button("PROCURAR MAPA NOVAMENTE"))
                AtualizarMapaAtivo();
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Modo", modo == ModoEdicao.EscolherBase ? "Escolher Base" : "Editar Vizinhos");
        EditorGUILayout.LabelField("Base", territorioBase != null ? NomeExibido(territorioBase.idTerritorio) : "Nenhuma");

        if (modo == ModoEdicao.EscolherBase)
            EditorGUILayout.HelpBox("Clique em um território na Scene View para defini-lo como base.", MessageType.Info);
        else
        {
            tipoNovaConexao = (TipoConexaoMapa)EditorGUILayout.EnumPopup("Tipo ao adicionar", tipoNovaConexao);
            EditorGUILayout.HelpBox("Clique em outro território para adicionar ou remover a conexão. Conexões novas são sempre bidirecionais.", MessageType.Info);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("ESCOLHER BASE"))
                EntrarModoEscolherBase();
            using (new EditorGUI.DisabledScope(territorioBase == null))
                if (GUILayout.Button("EDITAR VIZINHOS"))
                    modo = ModoEdicao.EditarVizinhos;
        }

        List<DefinicaoConexaoMapa> conexoesBase = ObterConexoesDaBase();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Vizinhos atuais ({conexoesBase.Count})", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(125f));
        if (conexoesBase.Count == 0)
            EditorGUILayout.LabelField("Nenhum vizinho cadastrado.", EditorStyles.miniLabel);
        foreach (DefinicaoConexaoMapa conexao in conexoesBase.OrderBy(NomeOutroExtremo, StringComparer.Ordinal))
            EditorGUILayout.LabelField($"✓ {NomeExibido(NomeOutroExtremo(conexao))}  [{conexao.tipo}]");
        EditorGUILayout.EndScrollView();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("SALVAR VIZINHANÇAS"))
                Salvar();
            if (GUILayout.Button("DESFAZER"))
                Undo.PerformUndo();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(territorioBase == null || conexoesBase.Count == 0))
                if (GUILayout.Button("LIMPAR VIZINHOS"))
                    ConfirmarELimparVizinhos();
            if (GUILayout.Button("RECARREGAR DADOS SALVOS"))
                RecarregarDadosSalvos();
        }
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("VALIDAR VIZINHANÇAS"))
                ValidarVizinhanças();
            if (GUILayout.Button("IMPRIMIR RELATÓRIO"))
                ImprimirRelatorio();
        }
    }

    private void DesenharSceneView(SceneView sceneView)
    {
        if (mapa == null)
            return;
        Event evento = Event.current;
        TerritorioClique sobMouse = EncontrarTerritorio(evento.mousePosition);
        if (territorioHover != sobMouse)
        {
            territorioHover = sobMouse;
            Repaint();
        }
        DesenharConexoesEDestaques();
        if (evento.type == EventType.MouseDown && evento.button == 0 && !evento.alt && sobMouse != null)
        {
            if (modo == ModoEdicao.EscolherBase)
                SelecionarBase(sobMouse);
            else if (territorioBase != null && sobMouse != territorioBase)
                AlternarVizinhanca(sobMouse);
            else if (sobMouse == territorioBase)
                EntrarModoEscolherBase();
            evento.Use();
            Repaint();
            SceneView.RepaintAll();
        }
        if (evento.type == EventType.MouseMove)
            sceneView.Repaint();
    }

    private void DesenharConexoesEDestaques()
    {
        if (territorioBase != null)
        {
            Vector3 centroBase = CentroTerritorio(territorioBase);
            foreach (DefinicaoConexaoMapa conexao in ObterConexoesDaBase())
            {
                TerritorioClique vizinho = EncontrarTerritorioPorId(NomeOutroExtremo(conexao));
                if (vizinho == null)
                    continue;
                Handles.color = CorVizinho;
                Handles.DrawAAPolyLine(4f, centroBase, CentroTerritorio(vizinho));
                DesenharContorno(vizinho, CorVizinho, 3f);
            }
            DesenharContorno(territorioBase, CorBase, 5f);
        }
        if (territorioHover != null && territorioHover != territorioBase)
            DesenharContorno(territorioHover, CorHover, 4f);
    }

    private static void DesenharContorno(TerritorioClique territorio, Color cor, float espessura)
    {
        PolygonCollider2D collider = territorio != null ? territorio.GetComponent<PolygonCollider2D>() : null;
        if (collider == null)
            return;
        Handles.color = cor;
        for (int caminho = 0; caminho < collider.pathCount; caminho++)
        {
            Vector2[] pontos = collider.GetPath(caminho);
            if (pontos.Length < 2)
                continue;
            Vector3[] mundo = new Vector3[pontos.Length + 1];
            for (int i = 0; i < pontos.Length; i++)
                mundo[i] = collider.transform.TransformPoint(pontos[i]);
            mundo[pontos.Length] = mundo[0];
            Handles.DrawAAPolyLine(espessura, mundo);
        }
    }

    private TerritorioClique EncontrarTerritorio(Vector2 posicaoGui)
    {
        Ray raio = HandleUtility.GUIPointToWorldRay(posicaoGui);
        Plane plano = new Plane(Vector3.forward, Vector3.zero);
        if (!plano.Raycast(raio, out float distancia))
            return null;
        Vector2 ponto = raio.GetPoint(distancia);
        return TerritoriosDaCena()
            .Where(t => t.GetComponent<PolygonCollider2D>()?.OverlapPoint(ponto) == true)
            .OrderBy(t => t.GetComponent<PolygonCollider2D>().bounds.size.sqrMagnitude)
            .FirstOrDefault();
    }

    private void SelecionarBase(TerritorioClique territorio)
    {
        territorioBase = territorio;
        modo = ModoEdicao.EditarVizinhos;
    }

    private void EntrarModoEscolherBase()
    {
        territorioBase = null;
        modo = ModoEdicao.EscolherBase;
        SceneView.RepaintAll();
    }

    private void AlternarVizinhanca(TerritorioClique destino)
    {
        if (mapa == null || territorioBase == null || destino == null)
            return;
        string origemId = territorioBase.idTerritorio;
        string destinoId = destino.idTerritorio;
        SerializedObject serializado = new SerializedObject(mapa);
        SerializedProperty conexoes = serializado.FindProperty("conexoes");
        int indiceExistente = EncontrarIndiceConexao(conexoes, origemId, destinoId);
        Undo.RecordObject(mapa, indiceExistente >= 0 ? "Remover vizinhança" : "Adicionar vizinhança");
        serializado.Update();
        if (indiceExistente >= 0)
            conexoes.DeleteArrayElementAtIndex(indiceExistente);
        else
        {
            int indice = conexoes.arraySize;
            conexoes.InsertArrayElementAtIndex(indice);
            SerializedProperty nova = conexoes.GetArrayElementAtIndex(indice);
            nova.FindPropertyRelative("origemId").stringValue = origemId;
            nova.FindPropertyRelative("destinoId").stringValue = destinoId;
            nova.FindPropertyRelative("tipo").enumValueIndex = (int)tipoNovaConexao;
            nova.FindPropertyRelative("bidirecional").boolValue = true;
        }
        serializado.ApplyModifiedProperties();
        EditorUtility.SetDirty(mapa);
    }

    private static int EncontrarIndiceConexao(SerializedProperty conexoes, string a, string b)
    {
        for (int i = 0; i < conexoes.arraySize; i++)
        {
            SerializedProperty conexao = conexoes.GetArrayElementAtIndex(i);
            string origem = conexao.FindPropertyRelative("origemId").stringValue;
            string destino = conexao.FindPropertyRelative("destinoId").stringValue;
            if ((origem == a && destino == b) || (origem == b && destino == a))
                return i;
        }
        return -1;
    }

    private void ConfirmarELimparVizinhos()
    {
        if (territorioBase == null || !EditorUtility.DisplayDialog("Limpar vizinhos", $"Remover todas as conexões de {NomeExibido(territorioBase.idTerritorio)}?", "Remover", "Cancelar"))
            return;
        string baseId = territorioBase.idTerritorio;
        SerializedObject serializado = new SerializedObject(mapa);
        SerializedProperty conexoes = serializado.FindProperty("conexoes");
        Undo.RecordObject(mapa, "Limpar vizinhos do território");
        serializado.Update();
        for (int i = conexoes.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty conexao = conexoes.GetArrayElementAtIndex(i);
            if (conexao.FindPropertyRelative("origemId").stringValue == baseId || conexao.FindPropertyRelative("destinoId").stringValue == baseId)
                conexoes.DeleteArrayElementAtIndex(i);
        }
        serializado.ApplyModifiedProperties();
        EditorUtility.SetDirty(mapa);
        Repaint();
        SceneView.RepaintAll();
    }

    private void Salvar()
    {
        EditorUtility.SetDirty(mapa);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Editor de Vizinhanças] {mapa.NomeExibido}: {mapa.Conexoes.Count} conexões salvas.", mapa);
    }

    private void RecarregarDadosSalvos()
    {
        string caminho = AssetDatabase.GetAssetPath(mapa);
        if (EditorUtility.IsDirty(mapa) && !EditorUtility.DisplayDialog("Recarregar dados salvos", "Alterações ainda não salvas serão descartadas. Continuar?", "Recarregar", "Cancelar"))
            return;
        AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        mapa = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(caminho);
        territorioBase = null;
        modo = ModoEdicao.EscolherBase;
        Repaint();
        SceneView.RepaintAll();
    }

    private void ValidarVizinhanças()
    {
        List<ProblemaValidacaoMapa> problemas = ValidadorDefinicaoMapa.Validar(mapa);
        int erros = 0;
        int avisos = 0;
        foreach (ProblemaValidacaoMapa problema in problemas)
        {
            if (problema.Codigo == "TERRITORIO_ISOLADO")
            {
                Debug.LogWarning("[Editor de Vizinhanças] " + problema, mapa);
                avisos++;
            }
            else
            {
                Debug.LogError("[Editor de Vizinhanças] " + problema, mapa);
                erros++;
            }
        }
        foreach (DefinicaoConexaoMapa conexao in mapa.Conexoes)
            if (conexao != null && !conexao.bidirecional)
            {
                Debug.LogError($"[Editor de Vizinhanças] Conexão unilateral: {conexao.origemId} -> {conexao.destinoId}.", mapa);
                erros++;
            }
        if (erros == 0 && avisos == 0)
            Debug.Log($"[Editor de Vizinhanças] {mapa.NomeExibido} válido: {mapa.Territorios.Count} territórios, {mapa.Conexoes.Count} conexões bidirecionais.", mapa);
        else
            Debug.Log($"[Editor de Vizinhanças] Validação concluída: {erros} erro(s), {avisos} aviso(s).", mapa);
    }

    private void ImprimirRelatorio()
    {
        List<string> linhas = new List<string> { $"[Editor de Vizinhanças] Relatório — {mapa.NomeExibido} ({mapa.Territorios.Count} territórios / {mapa.Conexoes.Count} conexões)" };
        foreach (DefinicaoTerritorioMapa territorio in mapa.Territorios.OrderBy(t => t.nomeExibido, StringComparer.Ordinal))
        {
            linhas.Add(territorio.nomeExibido + ":");
            List<DefinicaoConexaoMapa> conexoes = mapa.Conexoes
                .Where(c => c != null && (c.origemId == territorio.id || c.destinoId == territorio.id))
                .OrderBy(c => NomeOutroExtremo(c, territorio.id), StringComparer.Ordinal).ToList();
            if (conexoes.Count == 0)
                linhas.Add("- (sem vizinhos)");
            foreach (DefinicaoConexaoMapa conexao in conexoes)
                linhas.Add($"- {NomeExibido(NomeOutroExtremo(conexao, territorio.id))} [{conexao.tipo}]");
        }
        Debug.Log(string.Join("\n", linhas), mapa);
    }

    private void AtualizarMapaAtivoSeNecessario()
    {
        if (mapa == null || !TerritoriosDaCena().Any(t => mapa.Territorios.Any(d => d.id == t.idTerritorio)))
            AtualizarMapaAtivo();
    }

    private void AtualizarMapaAtivo()
    {
        DefinicaoMapa anterior = mapa;
        mapa = EncontrarMapaDaCena();
        if (mapa != anterior)
        {
            territorioBase = null;
            modo = ModoEdicao.EscolherBase;
        }
        Repaint();
        SceneView.RepaintAll();
    }

    private static DefinicaoMapa EncontrarMapaDaCena()
    {
        CatalogoMapas catalogo = Resources.Load<CatalogoMapas>(CaminhoCatalogo);
        if (catalogo == null)
            return null;
        SpriteRenderer[] visuais = ObjetosDaCena<SpriteRenderer>().ToArray();
        foreach (DefinicaoMapa candidato in catalogo.Mapas)
            if (candidato != null && candidato.ArteBase != null && visuais.Any(v => v.sprite == candidato.ArteBase))
                return candidato;
        HashSet<string> idsCena = new HashSet<string>(TerritoriosDaCena().Select(t => t.idTerritorio), StringComparer.Ordinal);
        return catalogo.Mapas.FirstOrDefault(candidato => candidato != null && candidato.Territorios.Count == idsCena.Count && candidato.Territorios.All(t => idsCena.Contains(t.id)));
    }

    private static IEnumerable<T> ObjetosDaCena<T>() where T : Component
    {
        Scene ativa = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<T>().Where(objeto => objeto != null && objeto.gameObject.scene.IsValid() && objeto.gameObject.scene == ativa && !EditorUtility.IsPersistent(objeto));
    }

    private static IEnumerable<TerritorioClique> TerritoriosDaCena() => ObjetosDaCena<TerritorioClique>().Where(t => !string.IsNullOrWhiteSpace(t.idTerritorio));
    private static TerritorioClique EncontrarTerritorioPorId(string id) => TerritoriosDaCena().FirstOrDefault(t => t.idTerritorio == id);

    private List<DefinicaoConexaoMapa> ObterConexoesDaBase()
    {
        if (mapa == null || territorioBase == null)
            return new List<DefinicaoConexaoMapa>();
        string id = territorioBase.idTerritorio;
        return mapa.Conexoes.Where(c => c != null && (c.origemId == id || c.destinoId == id)).ToList();
    }

    private string NomeOutroExtremo(DefinicaoConexaoMapa conexao) => NomeOutroExtremo(conexao, territorioBase != null ? territorioBase.idTerritorio : string.Empty);
    private static string NomeOutroExtremo(DefinicaoConexaoMapa conexao, string baseId) => conexao.origemId == baseId ? conexao.destinoId : conexao.origemId;

    private string NomeExibido(string id)
    {
        DefinicaoTerritorioMapa territorio = mapa?.Territorios.FirstOrDefault(t => t.id == id);
        return territorio != null && !string.IsNullOrWhiteSpace(territorio.nomeExibido) ? territorio.nomeExibido : id;
    }

    private static Vector3 CentroTerritorio(TerritorioClique territorio)
    {
        PolygonCollider2D collider = territorio.GetComponent<PolygonCollider2D>();
        return collider != null ? collider.bounds.center : territorio.transform.position;
    }
}

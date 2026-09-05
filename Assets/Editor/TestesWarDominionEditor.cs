#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TestesWarDominionEditor
{
    private const string Raiz = "War Dominion/Testes/";

    // ============================================================
    // 00. DIAGNÓSTICO SINTÉTICO DA FILA VISUAL
    // ============================================================

    [MenuItem(Raiz + "Resolução Visual/Testar Sequência")]
    private static void TestarSequenciaVisual()
    {
        ResolutionSequenceController controller =
            UnityEngine.Object.FindAnyObjectByType<ResolutionSequenceController>();
        List<TerritorioClique> territories = ObterTerritoriosReaisOrdenados();
        if (controller == null || territories.Count < 2) return;

        TerritorioClique origin = territories.Find(
            territory => territory.dono != TerritorioClique.Dono.Neutro) ?? territories[0];
        TerritorioClique destination = territories.Find(territory => territory != origin);
        if (destination == null) return;

        var events = new List<ResolutionVisualEvent>
        {
            ResolutionVisualEvent.Reinforcement(
                origin.dono, origin.idTerritorio, 3, origin.Tropas, origin.Tropas),
            ResolutionVisualEvent.Attack(
                origin.dono, origin.idTerritorio, destination.idTerritorio, 3,
                origin.Tropas, origin.Tropas,
                destination.Tropas, destination.Tropas, false,
                destination.dono, destination.dono),
            CriarPassagemControleSintetica(destination)
        };

        for (int index = 0; index < events.Count; index++)
            Debug.Log($"[ResolutionVisual][DEBUG][Teste Sequência] " +
                $"Evento {index + 1}: {events[index]}");

        controller.Play(events);
    }

    [MenuItem(Raiz + "Resolução Visual/Testar Sequência", true)]
    private static bool ValidarTestarSequenciaVisual() =>
        EmPlayCom<ResolutionSequenceController>();

    [MenuItem(Raiz + "Resolução Visual/Testar Reforço Visual")]
    private static void TestarReforcoVisual()
    {
        ResolutionSequenceController controller =
            UnityEngine.Object.FindAnyObjectByType<ResolutionSequenceController>();
        List<TerritorioClique> territories = ObterTerritoriosReaisOrdenados();
        if (controller == null || territories.Count == 0) return;

        TerritorioClique territory = territories.Find(
            item => item.dono != TerritorioClique.Dono.Neutro) ?? territories[0];
        controller.Play(new[]
        {
            ResolutionVisualEvent.Reinforcement(
                territory.dono, territory.idTerritorio, 3,
                territory.Tropas, territory.Tropas)
        });
    }

    [MenuItem(Raiz + "Resolução Visual/Testar Reforço Visual", true)]
    private static bool ValidarTestarReforcoVisual() =>
        EmPlayCom<ResolutionSequenceController>();

    [MenuItem(Raiz + "Resolução Visual/Testar Transferência Visual")]
    private static void TestarTransferenciaVisual()
    {
        ResolutionSequenceController controller =
            UnityEngine.Object.FindAnyObjectByType<ResolutionSequenceController>();
        List<TerritorioClique> territories = ObterTerritoriosReaisOrdenados();
        if (controller == null || territories.Count == 0) return;

        TerritorioClique territory = territories.Find(
            item => item.dono != TerritorioClique.Dono.Neutro) ?? territories[0];
        controller.Play(new[]
        {
            CriarPassagemControleSintetica(territory)
        });
    }

    [MenuItem(Raiz + "Resolução Visual/Testar Transferência Visual", true)]
    private static bool ValidarTestarTransferenciaVisual() =>
        EmPlayCom<ResolutionSequenceController>();

    [MenuItem(Raiz + "Resolução Visual/Diagnóstico: Apenas Handoff via Sequência")]
    private static void DiagnosticarApenasHandoffViaSequencia()
    {
        ResolutionSequenceController controller =
            UnityEngine.Object.FindAnyObjectByType<ResolutionSequenceController>();
        List<TerritorioClique> territories = ObterTerritoriosReaisOrdenados();
        if (controller == null || territories.Count == 0) return;

        TerritorioClique territory = territories.Find(
            item => item.dono != TerritorioClique.Dono.Neutro) ?? territories[0];
        ResolutionVisualEvent visualEvent =
            CriarPassagemControleSintetica(territory);
        Debug.Log($"[ResolutionVisual][DEBUG][Apenas Handoff] {visualEvent}");
        controller.Play(new[] { visualEvent });
    }

    [MenuItem(
        Raiz + "Resolução Visual/Diagnóstico: Apenas Handoff via Sequência", true)]
    private static bool ValidarDiagnosticarApenasHandoffViaSequencia() =>
        EmPlayCom<ResolutionSequenceController>();

    [MenuItem(Raiz + "Resolução Visual/Testar Ataque Visual")]
    private static void TestarAtaqueVisual()
    {
        ResolutionSequenceController controller =
            UnityEngine.Object.FindAnyObjectByType<ResolutionSequenceController>();
        if (controller == null) return;

        List<TerritorioClique> territories = ObterTerritoriosReaisOrdenados();
        if (territories.Count < 2) return;

        TerritorioClique origin = territories.Find(
            territory => territory.dono != TerritorioClique.Dono.Neutro) ?? territories[0];
        TerritorioClique destination = territories.Find(territory => territory != origin);
        if (destination == null) return;

        int amount = Mathf.Max(1, Mathf.Min(origin.Tropas - 1, 3));
        var visualEvent = ResolutionVisualEvent.Attack(
            origin.dono,
            origin.idTerritorio,
            destination.idTerritorio,
            amount,
            origin.Tropas,
            origin.Tropas,
            destination.Tropas,
            destination.Tropas,
            false,
            destination.dono,
            destination.dono);

        controller.Play(new[] { visualEvent });
    }

    private static List<TerritorioClique> ObterTerritoriosReaisOrdenados()
    {
        var territories = new List<TerritorioClique>(MapaAtivo.ObterTerritoriosOuCena());
        territories.RemoveAll(territory => territory == null);
        territories.Sort((left, right) =>
            string.CompareOrdinal(left.idTerritorio, right.idTerritorio));
        return territories;
    }

    private static TerritorioClique.Dono ObterAliadoVisual(
        TerritorioClique.Dono owner)
    {
        switch (owner)
        {
            case TerritorioClique.Dono.Jogador1:
                return TerritorioClique.Dono.Jogador2;
            case TerritorioClique.Dono.Jogador2:
                return TerritorioClique.Dono.Jogador1;
            case TerritorioClique.Dono.Jogador3:
                return TerritorioClique.Dono.Jogador4;
            case TerritorioClique.Dono.Jogador4:
                return TerritorioClique.Dono.Jogador3;
            default:
                return TerritorioClique.Dono.Jogador1;
        }
    }

    private static ResolutionVisualEvent CriarPassagemControleSintetica(
        TerritorioClique territory)
    {
        return ResolutionVisualEvent.TerritoryHandoff(
            territory.dono,
            ObterAliadoVisual(territory.dono),
            territory.idTerritorio,
            territory.Tropas,
            territory.Tropas);
    }

    [MenuItem(Raiz + "Resolução Visual/Testar Ataque Visual", true)]
    private static bool ValidarTestarAtaqueVisual() =>
        EmPlayCom<ResolutionSequenceController>();

    // ============================================================
    // 01. CONTROLE MANUAL DO CENÁRIO VISUAL DE RESOLUÇÃO
    // ============================================================

    [MenuItem(Raiz + "Rodada/Entrar em Resolução")]
    private static void EntrarEmResolucao() =>
        ComGameManager(g => g.TesteEditorEntrarEmResolucao());

    [MenuItem(Raiz + "Rodada/Entrar em Resolução", true)]
    private static bool ValidarEntrarEmResolucao()
    {
        if (!EditorApplication.isPlaying) return false;
        GameManager manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        return manager != null && manager.PodeEntrarResolucaoTesteEditor;
    }

    [MenuItem(Raiz + "Rodada/Avançar para Próximo Round")]
    private static void AvancarParaProximoRound() =>
        ComGameManager(g => g.TesteEditorAvancarParaProximoRound());

    [MenuItem(Raiz + "Rodada/Avançar para Próximo Round", true)]
    private static bool ValidarAvancarParaProximoRound()
    {
        if (!EditorApplication.isPlaying) return false;
        GameManager manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        return manager != null && manager.PodeAvancarResolucaoTesteEditor;
    }

    [MenuItem(Raiz + "Partida/Iniciar Partida Teste")]
    private static void IniciarPartida() => ComRodada(r => r.TesteEditorIniciarPartida());
    [MenuItem(Raiz + "Partida/Iniciar Partida Teste", true)]
    private static bool ValidarIniciarPartida() => EmPlayCom<GerenciadorRodada>();

    [MenuItem(Raiz + "Partida/Redistribuir Territórios 2x2")]
    private static void RedistribuirTerritorios()
    {
        if (!EditorApplication.isPlaying) return;
        DistribuidorTerritorios distribuidor = UnityEngine.Object.FindAnyObjectByType<DistribuidorTerritorios>();
        if (distribuidor != null) distribuidor.Distribuir();
    }
    [MenuItem(Raiz + "Partida/Redistribuir Territórios 2x2", true)]
    private static bool ValidarRedistribuirTerritorios() => EmPlayCom<DistribuidorTerritorios>();

    [MenuItem(Raiz + "Rodada/Iniciar Próxima Rodada")]
    private static void ProximaRodada() => ComRodada(r => r.TesteEditorIniciarProximaRodada());
    [MenuItem(Raiz + "Rodada/Iniciar Próxima Rodada", true)]
    private static bool ValidarProximaRodada() => EmPlayCom<GerenciadorRodada>();

    [MenuItem(Raiz + "Rodada/Resolver Round Atual")]
    private static void Resolver() => ComRodada(r => r.TesteEditorResolverRoundAtual());
    [MenuItem(Raiz + "Rodada/Resolver Round Atual", true)]
    private static bool ValidarResolver() => EmPlayCom<GerenciadorRodada>();

    [MenuItem(Raiz + "Rodada/Preparar Round 10")]
    private static void Round10() => ComRodada(r => r.TesteEditorPrepararRound10());
    [MenuItem(Raiz + "Rodada/Preparar Round 10", true)]
    private static bool ValidarRound10() => EmPlayCom<GerenciadorRodada>();

    [MenuItem(Raiz + "Rodada/Preparar Round 10 Empatado")]
    private static void Round10Empatado() => ComRodada(r => r.TesteEditorPrepararRound10Empatado());
    [MenuItem(Raiz + "Rodada/Preparar Round 10 Empatado", true)]
    private static bool ValidarRound10Empatado() => EmPlayCom<GerenciadorRodada>();

    [MenuItem(Raiz + "Rodada/Forçar Desempate")]
    private static void Desempate() => ComRodada(r => r.TesteEditorForcarDesempate());
    [MenuItem(Raiz + "Rodada/Forçar Desempate", true)]
    private static bool ValidarDesempate() => EmPlayCom<GerenciadorRodada>();

    [MenuItem(Raiz + "Ações/Enviar Ações")]
    private static void EnviarAcoes() => ComGameManager(g => g.EnviarAcoes());
    [MenuItem(Raiz + "Ações/Enviar Ações", true)]
    private static bool ValidarEnviarAcoes() => EmPlayCom<GameManager>();

    [MenuItem(Raiz + "Jogadores Simulados/Ativar Autoenvio")]
    private static void AtivarAutoenvio() => ComGameManager(g => g.TesteEditorAtivarAutoenvio());
    [MenuItem(Raiz + "Jogadores Simulados/Ativar Autoenvio", true)]
    private static bool ValidarAtivarAutoenvio() => EmPlayCom<GameManager>();

    [MenuItem(Raiz + "Jogadores Simulados/Desativar Autoenvio")]
    private static void DesativarAutoenvio() => ComGameManager(g => g.TesteEditorDesativarAutoenvio());
    [MenuItem(Raiz + "Jogadores Simulados/Desativar Autoenvio", true)]
    private static bool ValidarDesativarAutoenvio() => EmPlayCom<GameManager>();

    // ============================================================
    // 02. ACESSO À PALETA GLOBAL DO PROJETO
    // ============================================================

    [MenuItem(Raiz + "Jogadores Simulados/Editar Cores dos Jogadores")]
    private static void EditarCoresDosJogadores()
    {
        WDPlayerPaletteConfig paleta = WDPlayerPaletteConfig.LoadGlobal();
        if (paleta == null) return;

        Selection.activeObject = paleta;
        EditorGUIUtility.PingObject(paleta);
        EditorApplication.ExecuteMenuItem("Window/General/Inspector");
    }

    [MenuItem(Raiz + "Jogadores Simulados/Editar Cores dos Jogadores", true)]
    private static bool ValidarEditarCoresDosJogadores() =>
        WDPlayerPaletteConfig.LoadGlobal() != null;

    private static bool EmPlayCom<T>() where T : UnityEngine.Object =>
        EditorApplication.isPlaying && UnityEngine.Object.FindAnyObjectByType<T>() != null;

    private static void ComRodada(Action<GerenciadorRodada> acao)
    {
        if (!EditorApplication.isPlaying) return;
        GerenciadorRodada rodada = UnityEngine.Object.FindAnyObjectByType<GerenciadorRodada>();
        if (rodada != null) acao(rodada);
    }

    private static void ComGameManager(Action<GameManager> acao)
    {
        if (!EditorApplication.isPlaying) return;
        GameManager manager = GameManager.instance ?? UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (manager != null) acao(manager);
    }
}
#endif

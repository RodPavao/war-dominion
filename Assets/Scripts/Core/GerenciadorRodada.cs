using System.Collections.Generic;
using UnityEngine;

public class GerenciadorRodada : MonoBehaviour
{
    // =====================================================
    // 1. REFERÊNCIAS E ESTADO
    // =====================================================

    private GameManager gameManager;
    private AvaliadorVitoria avaliadorVitoria;

    public enum EstadoPartida
    {
        EmPreparacao,
        EmResolucao,
        MorteSubita,
        Encerrada
    }

    [SerializeField] private EstadoPartida estadoAtual =
        EstadoPartida.EmPreparacao;

    public EstadoPartida EstadoAtual => estadoAtual;
    public bool PartidaEncerrada => estadoAtual == EstadoPartida.Encerrada;
    public bool EmMorteSubita => estadoAtual == EstadoPartida.MorteSubita;
    public int RoundMorteSubita => EmMorteSubita
        ? Mathf.Max(1, rodadaAtual - LimiteRoundsNormais)
        : 0;
    public ResultadoPartida ResultadoAtual { get; private set; }

    public const int TotalRoundsNormais = 10;
    public const int ReforcosIniciaisPorJogador = 8;
    public const int ReforcosIniciaisUmContraUm =
        ReforcosIniciaisPorJogador;
    public const int ReforcosMinimosUmContraUm = 6;
    private int LimiteRoundsNormais => gameManager != null
        ? gameManager.LimiteRoundsDaPartida
        : TotalRoundsNormais;

    [Header("Rodada")]
    [SerializeField]
    private int rodadaAtual = 1;

    public int RodadaAtual =>
        rodadaAtual;

    private readonly List<TerritorioClique.Dono> prioridadeJogadores =
        new List<TerritorioClique.Dono>();

    private readonly HashSet<TerritorioClique.Dono> jogadoresParticipantes =
        new HashSet<TerritorioClique.Dono>();

    private void Awake()
    {
        gameManager =
            GetComponent<GameManager>();

        avaliadorVitoria = GetComponent<AvaliadorVitoria>();

        if (avaliadorVitoria == null)
            avaliadorVitoria = gameObject.AddComponent<AvaliadorVitoria>();
    }

    // =====================================================
    // 2. CICLO DE RODADAS
    // =====================================================

    public void IniciarPartida()
    {
        rodadaAtual = 1;
        estadoAtual = EstadoPartida.EmPreparacao;
        ResultadoAtual = null;
        jogadoresParticipantes.Clear();

        IniciarRodada();
    }

    private void IniciarProximaRodada()
    {
        if (PartidaEncerrada)
            return;

        rodadaAtual++;

        IniciarRodada();
    }

    private void IniciarRodada()
    {
        if (gameManager == null)
            return;

        if (PartidaEncerrada)
            return;

        if (!EmMorteSubita)
            estadoAtual = EstadoPartida.EmPreparacao;

        // A preparação é uma única janela para reforços e ações.
        gameManager
            .IniciarPreparacaoRound();

        // Depois prepara os reforços.
        PrepararReforcos();

        AtualizarPrioridadeJogadores();

        // Publica a Preparação somente após as autoridades da rodada estarem prontas.
        gameManager.GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
    }

    public void NotificarInicioResolucao()
    {
        AtualizarJogadoresParticipantes();

        if (!EmMorteSubita)
            estadoAtual = EstadoPartida.EmResolucao;
    }

    public void ConcluirResolucao()
    {
        bool deveAvaliar =
            EmMorteSubita ||
            rodadaAtual >= LimiteRoundsNormais;

        if (!deveAvaliar)
        {
            IniciarProximaRodada();
            return;
        }

        bool rodadaFoiMorteSubita = EmMorteSubita;

        ResultadoAtual = avaliadorVitoria.Avaliar(
            jogadoresParticipantes,
            rodadaAtual,
            rodadaFoiMorteSubita);

        if (ResultadoAtual.Encerrada)
        {
            estadoAtual = EstadoPartida.Encerrada;
            Debug.Log("PARTIDA ENCERRADA: " + DescreverVencedor(ResultadoAtual));
            return;
        }

        if (!gameManager.MorteSubitaHabilitada)
        {
            ResultadoAtual = ResultadoPartida.CriarEmpateFinal(
                rodadaAtual,
                ResultadoAtual.QuantidadeTerritorios);
            estadoAtual = EstadoPartida.Encerrada;
            Debug.Log("PARTIDA ENCERRADA EM EMPATE | Round: " + rodadaAtual);
            return;
        }

        estadoAtual = EstadoPartida.MorteSubita;
        Debug.Log("EMPATE APÓS O ROUND " + rodadaAtual + " | MORTE SÚBITA");
        IniciarProximaRodada();
    }

    public void PrepararReforcos()
    {
        if (gameManager == null)
            return;

        int reforcos =
            CalcularReforcos(
                gameManager.jogadorLocal
            );

        gameManager.DefinirReforcos(
            reforcos
        );
    }

    // =====================================================
    // 3. CÁLCULO DE REFORÇOS
    // =====================================================

    public int CalcularReforcos(
        TerritorioClique.Dono jogador)
    {
        int calculados = CalcularReforcosRegulares(
            jogador, out int baseTerritorial, out int bonusRegioes);
        return CalcularReforcosPorModo(
            baseTerritorial * 2,
            bonusRegioes,
            gameManager != null && gameManager.EhPartidaUmContraUm,
            rodadaAtual);
    }

    // =====================================================
    // 4. POLÍTICA CENTRAL DE REFORÇOS POR CATEGORIA DE MODO
    // =====================================================

    public static int CalcularReforcosPorModo(
        int quantidadeTerritorios,
        int bonusAplicaveis,
        bool partidaUmContraUm,
        int round)
    {
        if (Mathf.Max(1, round) == 1)
            return ReforcosIniciaisPorJogador;

        int calculados = Mathf.Max(0, quantidadeTerritorios) / 2 +
            Mathf.Max(0, bonusAplicaveis);
        return AplicarPisoReforcosUmContraUm(calculados, partidaUmContraUm);
    }

    public static int AplicarPisoReforcosUmContraUm(
        int reforcosCalculados,
        bool partidaUmContraUm)
    {
        int normalizados = Mathf.Max(0, reforcosCalculados);
        return partidaUmContraUm
            ? Mathf.Max(ReforcosMinimosUmContraUm, normalizados)
            : normalizados;
    }


    public int CalcularReforcosRegulares(
        TerritorioClique.Dono jogador,
        out int baseTerritorial,
        out int bonusRegioes)
    {

        int quantidadeTerritorios = 0;

        foreach (
            TerritorioClique territorio
            in MapaAtivo.ObterTerritoriosOuCena())
        {
            if (territorio.dono ==
                jogador)
            {
                quantidadeTerritorios++;
            }
        }

        baseTerritorial = quantidadeTerritorios / 2;
        bonusRegioes = MapaAtivo.Instance != null
            ? MapaAtivo.Instance.CalcularBonusRegioes(jogador)
            : 0;

        return baseTerritorial + bonusRegioes;
    }

    // =====================================================
    // 5. PRIORIDADE DETERMINÍSTICA ENTRE JOGADORES
    // =====================================================

    public IReadOnlyList<TerritorioClique.Dono> ObterPrioridadeJogadores()
    {
        if (prioridadeJogadores.Count == 0)
            AtualizarPrioridadeJogadores();

        return prioridadeJogadores;
    }

    public IReadOnlyList<TerritorioClique.Dono> ObterJogadoresNoTabuleiro()
    {
        SortedSet<TerritorioClique.Dono> jogadores =
            new SortedSet<TerritorioClique.Dono>(jogadoresParticipantes);

        foreach (TerritorioClique territorio in
                 MapaAtivo.ObterTerritoriosOuCena())
        {
            if (territorio.dono != TerritorioClique.Dono.Neutro)
                jogadores.Add(territorio.dono);
        }

        return new List<TerritorioClique.Dono>(jogadores);
    }

    private void AtualizarPrioridadeJogadores()
    {
        SortedSet<TerritorioClique.Dono> ativos =
            new SortedSet<TerritorioClique.Dono>();

        foreach (TerritorioClique territorio in
                 MapaAtivo.ObterTerritoriosOuCena())
        {
            if (territorio.dono != TerritorioClique.Dono.Neutro)
                ativos.Add(territorio.dono);
        }

        // O setup completa a lista mesmo quando a cena ainda contém donos
        // serializados de uma configuração com menos participantes.
        if (gameManager != null && gameManager.MatchSetupAtual != null)
        {
            foreach (WDMatchParticipant participant in gameManager.MatchSetupAtual.Participants)
            {
                int value = participant.SlotIndex + 1;
                if (System.Enum.IsDefined(typeof(TerritorioClique.Dono), value))
                    ativos.Add((TerritorioClique.Dono)value);
            }
        }

        prioridadeJogadores.Clear();

        if (ativos.Count == 0)
            return;

        prioridadeJogadores.AddRange(ConstruirPrioridadeJogadores(
            new List<TerritorioClique.Dono>(ativos),
            gameManager != null ? gameManager.MatchSetupAtual : null,
            rodadaAtual));

        Debug.Log(
            "PRIORIDADE ROUND " + rodadaAtual + ": " +
            string.Join(" -> ", prioridadeJogadores));
    }

    // =====================================================
    // 6. ORDEM INTERCALADA PARA MODOS POR EQUIPE
    // =====================================================

    public static IReadOnlyList<TerritorioClique.Dono> ConstruirPrioridadeJogadores(
        IReadOnlyList<TerritorioClique.Dono> jogadores,
        WDMatchSetup setup,
        int round)
    {
        var baseOrdenada = new List<TerritorioClique.Dono>();
        if (jogadores == null || jogadores.Count == 0)
            return baseOrdenada;

        bool usaEquipes = setup != null && setup.IsTeamMatch;
        var jogadoresAtivos = new HashSet<TerritorioClique.Dono>(jogadores);
        if (!usaEquipes)
        {
            baseOrdenada.AddRange(jogadores);
            int deslocamentoNormal = (Mathf.Max(1, round) - 1) % baseOrdenada.Count;
            return Rotacionar(baseOrdenada, deslocamentoNormal, false);
        }

        var porEquipe = new SortedDictionary<int, List<WDMatchParticipant>>();
        foreach (WDMatchParticipant participant in setup.Participants)
        {
            if (!porEquipe.TryGetValue(participant.TeamIndex, out List<WDMatchParticipant> team))
            {
                team = new List<WDMatchParticipant>();
                porEquipe.Add(participant.TeamIndex, team);
            }
            team.Add(participant);
        }
        foreach (List<WDMatchParticipant> team in porEquipe.Values)
            team.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

        int maiorEquipe = 0;
        foreach (List<WDMatchParticipant> team in porEquipe.Values)
            maiorEquipe = Mathf.Max(maiorEquipe, team.Count);
        for (int member = 0; member < maiorEquipe; member++)
            foreach (List<WDMatchParticipant> team in porEquipe.Values)
                if (member < team.Count)
                {
                    TerritorioClique.Dono owner =
                        (TerritorioClique.Dono)(team[member].SlotIndex + 1);
                    if (jogadoresAtivos.Contains(owner))
                        baseOrdenada.Add(owner);
                }

        int deslocamentoEquipe = (Mathf.Max(1, round) - 1) % baseOrdenada.Count;
        return Rotacionar(baseOrdenada, deslocamentoEquipe, true);
    }

    private static IReadOnlyList<TerritorioClique.Dono> Rotacionar(
        List<TerritorioClique.Dono> baseOrdenada,
        int deslocamento,
        bool paraDireita)
    {
        var resultado = new List<TerritorioClique.Dono>(baseOrdenada.Count);
        for (int i = 0; i < baseOrdenada.Count; i++)
        {
            int indice = paraDireita
                ? (i - deslocamento + baseOrdenada.Count) % baseOrdenada.Count
                : (i + deslocamento) % baseOrdenada.Count;
            resultado.Add(baseOrdenada[indice]);
        }
        return resultado;
    }

    // =====================================================
    // 7. PARTICIPANTES E DESCRIÇÃO DO RESULTADO
    // =====================================================

    private void AtualizarJogadoresParticipantes()
    {
        foreach (TerritorioClique.Dono jogador in ObterJogadoresNoTabuleiro())
            jogadoresParticipantes.Add(jogador);
    }

    private static string DescreverVencedor(ResultadoPartida resultado)
    {
        string vencedor = resultado.Tipo == ResultadoPartida.TipoVencedor.Equipe
            ? resultado.EquipeVencedora.ToString()
            : resultado.JogadorVencedor.ToString();

        return vencedor + " | Territórios: " +
            resultado.QuantidadeTerritorios + " | Round: " +
            resultado.RoundFinal;
    }

    // =====================================================
    // 8. CONTROLES DE TESTE NO INSPECTOR
    // =====================================================

    [ContextMenu("Iniciar Partida Teste")]
    private void IniciarPartidaTeste()
    {
        IniciarPartida();
    }

    [ContextMenu("Iniciar Proxima Rodada")]
    private void ProximaRodadaTeste()
    {
        if (!PartidaEncerrada)
            IniciarProximaRodada();
    }

    // 8.1. A seleção territorial inicial roda também no Player Android.
    // Ela usa a autoridade normal de prioridade antes de começar o round 1.
    public void PrepararPrioridadeRoundUm()
    {
        rodadaAtual = 1;
        AtualizarPrioridadeJogadores();
    }

#if UNITY_EDITOR
    private class LadoTesteEditor
    {
        public int Tipo;
        public int Id;
        public int Territorios;
        public readonly List<TerritorioClique.Dono> Jogadores =
            new List<TerritorioClique.Dono>();
    }

    [ContextMenu("Teste Editor/Preparar Round 10")]
    private void PrepararRound10Teste()
    {
        if (PartidaEncerrada)
            return;

        rodadaAtual = LimiteRoundsNormais;
        estadoAtual = EstadoPartida.EmPreparacao;
        ResultadoAtual = null;
        gameManager?.LimparPreparacaoParaTesteEditor();
        IniciarRodada();
    }

    [ContextMenu("Teste Editor/Preparar Round 10 Empatado")]
    private void PrepararRound10EmpatadoTeste()
    {
        List<TerritorioClique> territorios = ObterTerritoriosOrdenadosTeste();
        List<LadoTesteEditor> lados = CriarLadosTeste(territorios);

        if (lados.Count < 2)
        {
            Debug.LogError("TESTE MORTE SÚBITA | São necessários ao menos 2 lados.");
            return;
        }

        if (territorios.Count % lados.Count != 0)
        {
            Debug.LogError(
                "TESTE MORTE SÚBITA | Não é possível dividir " +
                territorios.Count + " territórios igualmente entre " +
                lados.Count + " lados.");
            return;
        }

        rodadaAtual = LimiteRoundsNormais;
        estadoAtual = EstadoPartida.EmPreparacao;
        ResultadoAtual = null;
        gameManager?.LimparPreparacaoParaTesteEditor();
        IniciarRodada();

        for (int i = 0; i < territorios.Count; i++)
        {
            int indiceLado = i % lados.Count;
            int volta = i / lados.Count;
            LadoTesteEditor lado = lados[indiceLado];
            TerritorioClique.Dono novoDono =
                lado.Jogadores[volta % lado.Jogadores.Count];

            territorios[i].DefinirDono(novoDono);
        }

        AtualizarJogadoresParticipantes();
        PrepararReforcos();
        AtualizarPrioridadeJogadores();

        Debug.Log(
            "TESTE MORTE SÚBITA | Round 10 preparado com " +
            (territorios.Count / lados.Count) +
            " territórios para cada um dos " + lados.Count + " lados.");
    }

    [ContextMenu("Teste Editor/Forçar Desempate")]
    private void ForcarDesempateTeste()
    {
        if (!EmMorteSubita || PartidaEncerrada)
        {
            Debug.LogError(
                "TESTE MORTE SÚBITA | O desempate exige uma partida em morte súbita.");
            return;
        }

        List<TerritorioClique> territorios = ObterTerritoriosOrdenadosTeste();
        List<LadoTesteEditor> lados = CriarLadosTeste(territorios);
        int maiorQuantidade = -1;
        List<LadoTesteEditor> lideres = new List<LadoTesteEditor>();

        foreach (LadoTesteEditor lado in lados)
        {
            if (lado.Territorios > maiorQuantidade)
            {
                maiorQuantidade = lado.Territorios;
                lideres.Clear();
                lideres.Add(lado);
            }
            else if (lado.Territorios == maiorQuantidade)
            {
                lideres.Add(lado);
            }
        }

        if (lideres.Count == 1)
        {
            Debug.Log("TESTE MORTE SÚBITA | Já existe um líder único.");
            return;
        }

        LadoTesteEditor beneficiado = lideres[0];
        LadoTesteEditor cedente = lideres[1];
        TerritorioClique territorioCedente = territorios.Find(
            territorio => PertenceAoLadoTeste(territorio.dono, cedente));

        if (territorioCedente == null || beneficiado.Jogadores.Count == 0)
        {
            Debug.LogError("TESTE MORTE SÚBITA | Não foi possível ajustar o tabuleiro.");
            return;
        }

        int tropasAntes = territorioCedente.Tropas;
        territorioCedente.DefinirDono(beneficiado.Jogadores[0]);

        Debug.Log(
            "TESTE MORTE SÚBITA | Desempate preparado alterando somente " +
            territorioCedente.name + " | Tropas preservadas: " + tropasAntes + ".");
    }

    [ContextMenu("Teste Editor/Resolver Round Atual")]
    private void ResolverRoundAtualTeste()
    {
        if (!PartidaEncerrada && gameManager != null)
            gameManager.FecharPreparacaoEIniciarResolucao();
    }

    public void TesteEditorIniciarPartida() => IniciarPartidaTeste();
    public void TesteEditorIniciarProximaRodada() => ProximaRodadaTeste();
    public void TesteEditorPrepararRound10() => PrepararRound10Teste();
    public void TesteEditorPrepararRound10Empatado() => PrepararRound10EmpatadoTeste();
    public void TesteEditorForcarDesempate() => ForcarDesempateTeste();
    public void TesteEditorResolverRoundAtual() => ResolverRoundAtualTeste();

    private static List<TerritorioClique> ObterTerritoriosOrdenadosTeste()
    {
        List<TerritorioClique> territorios =
            new List<TerritorioClique>(MapaAtivo.ObterTerritoriosOuCena());

        territorios.Sort((a, b) => string.CompareOrdinal(
            ObterIdTerritorioTeste(a),
            ObterIdTerritorioTeste(b)));

        return territorios;
    }

    private static List<LadoTesteEditor> CriarLadosTeste(
        List<TerritorioClique> territorios)
    {
        List<LadoTesteEditor> lados = new List<LadoTesteEditor>();

        foreach (TerritorioClique territorio in territorios)
        {
            if (territorio.dono == TerritorioClique.Dono.Neutro)
                continue;

            LadoTesteEditor lado = EncontrarLadoTeste(lados, territorio.dono);

            if (lado == null)
            {
                lado = CriarLadoTeste(territorio.dono);
                lados.Add(lado);
            }

            if (!lado.Jogadores.Contains(territorio.dono))
                lado.Jogadores.Add(territorio.dono);

            lado.Territorios++;
        }

        lados.Sort((a, b) =>
        {
            int tipo = a.Tipo.CompareTo(b.Tipo);
            return tipo != 0 ? tipo : a.Id.CompareTo(b.Id);
        });

        return lados;
    }

    private static LadoTesteEditor EncontrarLadoTeste(
        List<LadoTesteEditor> lados,
        TerritorioClique.Dono jogador)
    {
        LadoTesteEditor chave = CriarLadoTeste(jogador);
        return lados.Find(lado => lado.Tipo == chave.Tipo && lado.Id == chave.Id);
    }

    private static LadoTesteEditor CriarLadoTeste(TerritorioClique.Dono jogador)
    {
        EquipesJogadores.Equipe equipe = EquipesJogadores.ObterEquipe(jogador);

        return equipe != EquipesJogadores.Equipe.Nenhuma
            ? new LadoTesteEditor { Tipo = 1, Id = (int)equipe }
            : new LadoTesteEditor { Tipo = 2, Id = (int)jogador };
    }

    private static bool PertenceAoLadoTeste(
        TerritorioClique.Dono jogador,
        LadoTesteEditor lado)
    {
        LadoTesteEditor chave = CriarLadoTeste(jogador);
        return chave.Tipo == lado.Tipo && chave.Id == lado.Id;
    }

    private static string ObterIdTerritorioTeste(TerritorioClique territorio)
    {
        return !string.IsNullOrWhiteSpace(territorio.idTerritorio)
            ? territorio.idTerritorio
            : territorio.name;
    }
#endif
}

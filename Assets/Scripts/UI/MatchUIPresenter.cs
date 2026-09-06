using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MatchUIPresenter : MonoBehaviour
{
    // ============================================================
    // 01. ESTADO PUBLICADO E COMANDOS
    // ============================================================

    private GameManager gameManager;
    private MatchUIState estadoAtual;
    private float proximaAtualizacao;

    private const float IntervaloAtualizacao = 0.1f;

    public MatchUIState EstadoAtual => estadoAtual;
    public IMatchUICommands Comandos { get; private set; }
    public event Action<MatchUIState> EstadoAlterado;

    // ============================================================
    // 02. INICIALIZAÇÃO E ATUALIZAÇÃO CENTRALIZADA
    // ============================================================

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        Comandos = new MatchUICommands(gameManager);
    }

    private void Start()
    {
        PublicarSeAlterado(true);
    }

    private void Update()
    {
        if (Time.unscaledTime < proximaAtualizacao)
            return;

        proximaAtualizacao = Time.unscaledTime + IntervaloAtualizacao;
        PublicarSeAlterado(false);
    }

    public void SolicitarSnapshotAtual()
    {
        PublicarSeAlterado(true);
    }

    private void PublicarSeAlterado(bool forcar)
    {
        if (gameManager == null)
            return;

        MatchUIState novoEstado = CriarSnapshot();

        if (!forcar && novoEstado.Equals(estadoAtual))
            return;

        estadoAtual = novoEstado;
        EstadoAlterado?.Invoke(estadoAtual);
    }

    // ============================================================
    // 03. CONSTRUÇÃO DO SNAPSHOT SOMENTE DE LEITURA
    // ============================================================

    private MatchUIState CriarSnapshot()
    {
        IReadOnlyList<TerritorioClique> territorios = MapaAtivo.ObterTerritoriosOuCena();
        MatchUIPlayerState[] jogadores = CriarJogadores(territorios);
        MatchUIActionState[] acoes = CriarAcoes();
        MatchUITerritoryState[] estadosTerritorios =
            CriarTerritorios(territorios, acoes);
        MatchUIReinforcementState[] distribuicoes = CriarDistribuicoes();
        MatchUITransferState transferencia = CriarTransferencia();
        MatchUIResultState resultado = gameManager.ResultadoPartidaAtual != null
            ? new MatchUIResultState(gameManager.ResultadoPartidaAtual)
            : null;

        float tempo = Mathf.Max(0f, gameManager.TempoPreparacaoRestante);
        tempo = Mathf.Round(tempo * 10f) / 10f;

        string origemId = ObterId(gameManager.territorioSelecionado);
        string destinoId = ObterId(gameManager.territorioDestinoSelecionado);
        string transferenciaId = ObterId(gameManager.territorioTransferenciaSelecionado);
        MatchUIFeedbackState feedbackAtual = CriarFeedbackAtual();
        IReadOnlyList<string> hand = gameManager.CartasRandomJogadorLocal;
        IReadOnlyList<TerritorioClique.Dono> transferRecipients =
            gameManager.ObterDestinatariosTransferencia();
        var transferRecipientArray = new TerritorioClique.Dono[transferRecipients.Count];
        for (int i = 0; i < transferRecipients.Count; i++)
            transferRecipientArray[i] = transferRecipients[i];
        string[] cards = new string[hand.Count];
        for (int i = 0; i < hand.Count; i++)
            cards[i] = hand[i];
        string chave = CriarChaveConteudo(
            tempo, jogadores, estadosTerritorios, acoes, distribuicoes,
            transferencia, resultado, feedbackAtual,
            origemId, destinoId, transferenciaId);
        chave += $"|random:{gameManager.UsaRandomCards}:{string.Join(",", cards)}";
        chave += $"|initial:{gameManager.EmSelecaoInicial}:" +
            $"{gameManager.EtapaSelecaoInicial}:{gameManager.FeedbackSelecaoInicial}";

        return new MatchUIState(
            gameManager.RodadaAtual,
            gameManager.faseAtual,
            gameManager.estadoPreparacao,
            gameManager.modoAcao,
            gameManager.EstadoAtualPartida,
            gameManager.EstadoAtualPartida == GerenciadorRodada.EstadoPartida.MorteSubita,
            gameManager.RoundMorteSubita,
            tempo,
            gameManager.reforcosDisponiveis,
            gameManager.QuantidadeOrdensPreparadas,
            gameManager.LimiteAcoesPorRodada,
            gameManager.TransferenciaPreparada != null,
            gameManager.TransferenciaDisponivelParaJogadorLocal,
            gameManager.MatchSetupUsaEquipes,
            gameManager.AcoesEnviadas,
            gameManager.PodeCancelarEnvio,
            gameManager.PodeEditarPreparacao,
            gameManager.PossuiPreparacaoParaEnviar && gameManager.PodeEditarPreparacao &&
                gameManager.reforcosDisponiveis == 0 &&
                gameManager.TodosTerritoriosControladosPossuemTropa,
            gameManager.PodeEditarPreparacao &&
                gameManager.territorioSelecionado != null &&
                gameManager.territorioDestinoSelecionado != null,
            gameManager.PodeEditarPreparacao && gameManager.QuantidadeOrdensPreparadas > 0,
            gameManager.PodeEditarPreparacao && gameManager.reforcosDisponiveis > 0,
            gameManager.jogadorLocal,
            origemId,
            destinoId,
            transferenciaId,
            gameManager.quantidadeAcaoSelecionada,
            gameManager.TipoAcaoSelecionadaEsperado,
            gameManager.feedbackTransferencia,
            transferRecipientArray,
            gameManager.DestinatarioTransferenciaSelecionado,
            feedbackAtual,
            jogadores,
            estadosTerritorios,
            acoes,
            distribuicoes,
            transferencia,
            resultado,
            gameManager.UsaRandomCards,
            cards,
            gameManager.EmSelecaoInicial,
            gameManager.EtapaSelecaoInicial,
            gameManager.FeedbackSelecaoInicial,
            chave);
    }

    // ============================================================
    // 04. PROJEÇÕES SEM REFERÊNCIAS A GAMEOBJECTS
    // ============================================================

    private MatchUIPlayerState[] CriarJogadores(IReadOnlyList<TerritorioClique> territorios)
    {
        WDMatchSetup setup = gameManager.MatchSetupAtual;
        if (setup != null)
            return CriarJogadoresDoSetup(setup, territorios);

        IReadOnlyList<TerritorioClique.Dono> jogadoresAtivos =
            gameManager.ObterJogadoresTransferencia();
        var resultado = new MatchUIPlayerState[jogadoresAtivos.Count];

        for (int i = 0; i < jogadoresAtivos.Count; i++)
        {
            TerritorioClique.Dono jogador = jogadoresAtivos[i];
            int controlados = 0;

            foreach (TerritorioClique territorio in territorios)
                if (territorio != null && territorio.dono == jogador)
                    controlados++;

            resultado[i] = new MatchUIPlayerState(
                jogador,
                EquipesJogadores.ObterEquipe(jogador),
                controlados,
                jogador == gameManager.jogadorLocal,
                jogador.ToString(),
                PaletaJogadores.ObterCorAtiva(jogador),
                ObterEstadoRodadaJogador(jogador));
        }

        return resultado;
    }

    private MatchUIPlayerState[] CriarJogadoresDoSetup(
        WDMatchSetup setup,
        IReadOnlyList<TerritorioClique> territorios)
    {
        var resultado = new List<MatchUIPlayerState>(setup.Participants.Count);
        var participants = new List<WDMatchParticipant>(setup.Participants);
        IReadOnlyList<TerritorioClique.Dono> priority =
            gameManager.ObterPrioridadeJogadoresParaUI();
        participants.Sort((a, b) =>
        {
            int ownerA = ObterIndicePrioridade(
                priority, (TerritorioClique.Dono)(a.SlotIndex + 1));
            int ownerB = ObterIndicePrioridade(
                priority, (TerritorioClique.Dono)(b.SlotIndex + 1));
            return ownerA.CompareTo(ownerB);
        });
        foreach (WDMatchParticipant participant in participants)
        {
            int ownerValue = participant.SlotIndex + 1;
            if (!Enum.IsDefined(typeof(TerritorioClique.Dono), ownerValue))
                continue;

            TerritorioClique.Dono owner = (TerritorioClique.Dono)ownerValue;
            int controlled = 0;
            foreach (TerritorioClique territory in territorios)
                if (territory != null && territory.dono == owner)
                    controlled++;

            string displayName = participant.Kind == WDMatchParticipantKind.Local &&
                !string.IsNullOrWhiteSpace(participant.Nickname)
                ? participant.Nickname
                : participant.Kind == WDMatchParticipantKind.Local
                    ? "Jogador"
                    : $"Jogador {participant.SlotIndex + 1}";
            resultado.Add(new MatchUIPlayerState(
                owner,
                EquipesJogadores.ObterEquipe(owner),
                controlled,
                participant.Kind == WDMatchParticipantKind.Local,
                displayName,
                participant.MatchColor,
                ObterEstadoRodadaJogador(owner)));
        }
        return resultado.ToArray();
    }

    private static int ObterIndicePrioridade(
        IReadOnlyList<TerritorioClique.Dono> priority,
        TerritorioClique.Dono owner)
    {
        for (int i = 0; i < priority.Count; i++)
            if (priority[i] == owner)
                return i;
        return int.MaxValue;
    }

    private MatchUIPlayerRoundStatus ObterEstadoRodadaJogador(
        TerritorioClique.Dono jogador)
    {
        if (gameManager.faseAtual == GameManager.FaseTurno.Resolucao)
            return MatchUIPlayerRoundStatus.EmResolucao;
        if (jogador != gameManager.jogadorLocal)
            return MatchUIPlayerRoundStatus.Participante;
        return gameManager.AcoesEnviadas
            ? MatchUIPlayerRoundStatus.Enviado
            : MatchUIPlayerRoundStatus.Preparando;
    }

    private MatchUITerritoryState[] CriarTerritorios(
        IReadOnlyList<TerritorioClique> territorios,
        IReadOnlyList<MatchUIActionState> acoes)
    {
        var preparados = new HashSet<string>();
        foreach (MatchUIActionState acao in acoes)
        {
            preparados.Add(acao.OrigemId);
            preparados.Add(acao.DestinoId);
        }
        foreach (DistribuicaoReforco distribuicao in gameManager.DistribuicoesReforcos)
            preparados.Add(ObterId(distribuicao.Territorio));
        if (gameManager.TransferenciaPreparada != null)
            preparados.Add(gameManager.TransferenciaPreparada.IdTerritorio);

        var resultado = new List<MatchUITerritoryState>(territorios.Count);
        foreach (TerritorioClique territorio in territorios)
        {
            if (territorio == null)
                continue;

            string id = ObterId(territorio);
            MatchUITerritoryVisualState estado =
                MatchUITerritoryVisualState.Normal;
            if (territorio == gameManager.territorioSelecionado)
                estado |= MatchUITerritoryVisualState.Selecionado |
                    MatchUITerritoryVisualState.Origem;
            if (territorio == gameManager.territorioDestinoSelecionado)
                estado |= MatchUITerritoryVisualState.Selecionado |
                    MatchUITerritoryVisualState.Destino;
            if (territorio == gameManager.territorioTransferenciaSelecionado)
                estado |= MatchUITerritoryVisualState.Selecionado |
                    MatchUITerritoryVisualState.Origem;

            if (gameManager.territorioSelecionado != null &&
                territorio != gameManager.territorioSelecionado)
            {
                estado |= gameManager.PodeSelecionarDestinoParaUI(territorio)
                    ? MatchUITerritoryVisualState.DestinoValido
                    : MatchUITerritoryVisualState.DestinoInvalido;
            }

            if (territorio.dono == gameManager.jogadorLocal ||
                EquipesJogadores.SaoAliados(
                    territorio.dono, gameManager.jogadorLocal))
            {
                estado |= MatchUITerritoryVisualState.Aliado;
            }
            else if (territorio.dono != TerritorioClique.Dono.Neutro)
            {
                estado |= MatchUITerritoryVisualState.Inimigo;
            }

            if (preparados.Contains(id))
                estado |= MatchUITerritoryVisualState.AcaoPreparada;
            if (gameManager.faseAtual == GameManager.FaseTurno.Resolucao)
                estado |= MatchUITerritoryVisualState.EmResolucao;

            resultado.Add(new MatchUITerritoryState(
                id, territorio.dono, territorio.Tropas, estado));
        }

        return resultado.ToArray();
    }

    private MatchUIFeedbackState CriarFeedbackAtual()
    {
        if (!string.IsNullOrWhiteSpace(gameManager.feedbackTransferencia))
        {
            return new MatchUIFeedbackState(
                MatchUIFeedbackKind.Informacao,
                gameManager.feedbackTransferencia);
        }

        if (gameManager.AcoesEnviadas)
            return new MatchUIFeedbackState(
                MatchUIFeedbackKind.Sucesso, "Preparação enviada.");
        if (gameManager.territorioDestinoSelecionado != null)
            return new MatchUIFeedbackState(
                MatchUIFeedbackKind.Informacao,
                gameManager.TipoAcaoSelecionadaEsperado + " preparado para confirmação.");
        if (gameManager.territorioSelecionado != null)
            return new MatchUIFeedbackState(
                MatchUIFeedbackKind.Informacao, "Selecione o território de destino.");
        return new MatchUIFeedbackState(MatchUIFeedbackKind.Nenhum, string.Empty);
    }

    private MatchUIActionState[] CriarAcoes()
    {
        IReadOnlyList<OrdemTerrestre> ordens = gameManager.OrdensPreparadas;
        if (ordens == null)
            return Array.Empty<MatchUIActionState>();

        var resultado = new MatchUIActionState[ordens.Count];
        for (int i = 0; i < ordens.Count; i++)
        {
            OrdemTerrestre ordem = ordens[i];
            resultado[i] = new MatchUIActionState(
                ordem.PosicaoNaFila,
                ordem.IdOrigem,
                ordem.IdDestino,
                ordem.QuantidadePretendida,
                SistemaAcoesTerrestres.ObterTipoEsperado(ordem.Jogador, ordem.Destino));
        }

        return resultado;
    }

    private MatchUIReinforcementState[] CriarDistribuicoes()
    {
        IReadOnlyList<DistribuicaoReforco> distribuicoes = gameManager.DistribuicoesReforcos;
        var resultado = new MatchUIReinforcementState[distribuicoes.Count];

        for (int i = 0; i < distribuicoes.Count; i++)
        {
            DistribuicaoReforco distribuicao = distribuicoes[i];
            resultado[i] = new MatchUIReinforcementState(
                distribuicao.Id,
                ObterId(distribuicao.Territorio),
                distribuicao.Quantidade);
        }

        return resultado;
    }

    private MatchUITransferState CriarTransferencia()
    {
        OrdemTransferencia transferencia = gameManager.TransferenciaPreparada;
        return transferencia == null
            ? null
            : new MatchUITransferState(
                transferencia.IdTerritorio,
                transferencia.Autor,
                transferencia.Destinatario);
    }

    // ============================================================
    // 05. DETECÇÃO DETERMINÍSTICA DE MUDANÇAS
    // ============================================================

    private string CriarChaveConteudo(
        float tempo,
        MatchUIPlayerState[] jogadores,
        MatchUITerritoryState[] territorios,
        MatchUIActionState[] acoes,
        MatchUIReinforcementState[] distribuicoes,
        MatchUITransferState transferencia,
        MatchUIResultState resultado,
        MatchUIFeedbackState feedbackAtual,
        string origemId,
        string destinoId,
        string transferenciaId)
    {
        var chave = new StringBuilder(512);
        chave.Append(gameManager.RodadaAtual).Append('|')
            .Append((int)gameManager.faseAtual).Append('|')
            .Append((int)gameManager.estadoPreparacao).Append('|')
            .Append((int)gameManager.modoAcao).Append('|')
            .Append((int)gameManager.EstadoAtualPartida).Append('|')
            .Append(gameManager.RoundMorteSubita).Append('|')
            .Append(tempo).Append('|')
            .Append(gameManager.reforcosDisponiveis).Append('|')
            .Append(gameManager.quantidadeAcaoSelecionada).Append('|')
            .Append(gameManager.TransferenciaDisponivelParaJogadorLocal).Append('|')
            .Append(gameManager.MatchSetupUsaEquipes).Append('|')
            .Append(origemId).Append('|').Append(destinoId).Append('|')
            .Append(transferenciaId).Append('|').Append(gameManager.feedbackTransferencia);

        chave.Append('|').Append((int)feedbackAtual.Tipo).Append(':')
            .Append(feedbackAtual.Mensagem);

        foreach (MatchUIPlayerState jogador in jogadores)
            chave.Append("|P:").Append((int)jogador.Jogador).Append(':')
                .Append(jogador.TerritoriosControlados).Append(':')
                .Append((int)jogador.EstadoRodada).Append(':')
                .Append(jogador.Cor);

        foreach (MatchUITerritoryState territorio in territorios)
            chave.Append("|D:").Append(territorio.Id).Append(':')
                .Append((int)territorio.Proprietario).Append(':')
                .Append(territorio.Tropas).Append(':')
                .Append((int)territorio.EstadoVisual);

        foreach (MatchUIActionState acao in acoes)
            chave.Append("|A:").Append(acao.Posicao).Append(':').Append(acao.OrigemId)
                .Append(':').Append(acao.DestinoId).Append(':').Append(acao.Quantidade);

        foreach (MatchUIReinforcementState distribuicao in distribuicoes)
            chave.Append("|R:").Append(distribuicao.Id).Append(':')
                .Append(distribuicao.TerritorioId).Append(':').Append(distribuicao.Quantidade);

        if (transferencia != null)
            chave.Append("|T:").Append(transferencia.TerritorioId).Append(':')
                .Append((int)transferencia.Destinatario);

        if (resultado != null)
            chave.Append("|F:").Append(resultado.Encerrada).Append(':').Append(resultado.Empate)
                .Append(':').Append((int)resultado.Tipo).Append(':').Append(resultado.Territorios)
                .Append(':').Append(resultado.RoundFinal).Append(':').Append(resultado.HouveMorteSubita);

        return chave.ToString();
    }

    private static string ObterId(TerritorioClique territorio)
    {
        if (territorio == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(territorio.idTerritorio)
            ? territorio.name
            : territorio.idTerritorio;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 01. SNAPSHOT IMUTÁVEL DA PARTIDA
// ============================================================

public sealed class MatchUIState : IEquatable<MatchUIState>
{
    private readonly string chaveConteudo;

    public int Round { get; }
    public GameManager.FaseTurno Fase { get; }
    public GameManager.EstadoPreparacao EstadoPreparacao { get; }
    public GameManager.ModoAcao ModoAcao { get; }
    public bool EmModoAcaoTerrestre => ModoAcao == GameManager.ModoAcao.AcaoTerrestre;
    public bool EmModoTransferencia => ModoAcao == GameManager.ModoAcao.Transferir;
    public GerenciadorRodada.EstadoPartida EstadoPartida { get; }
    public bool EmMorteSubita { get; }
    public int RoundMorteSubita { get; }
    public float TempoRestante { get; }
    public int ReforcosDisponiveis { get; }
    public int AcoesPreparadas { get; }
    public int LimiteAcoes { get; }
    public bool TransferenciaUsada { get; }
    public bool TransferenciaDisponivel { get; }
    public bool UsaEquipes { get; }
    public bool AcoesEnviadas { get; }
    public bool PodeCancelarEnvio { get; }
    public bool PodeEditarPreparacao { get; }
    public bool PodeEnviarPreparacao { get; }
    public bool PodeConfirmarAcao { get; }
    public bool PodeDesfazerAcao { get; }
    public bool PodeAdicionarReforco { get; }
    public TerritorioClique.Dono JogadorLocal { get; }
    public string OrigemSelecionadaId { get; }
    public string DestinoSelecionadoId { get; }
    public string TerritorioTransferenciaId { get; }
    public int QuantidadeAcaoSelecionada { get; }
    public int QuantidadeMaximaAcao { get; }
    public string TipoAcaoEsperado { get; }
    public string FeedbackTransferencia { get; }
    public IReadOnlyList<TerritorioClique.Dono> DestinatariosTransferencia { get; }
    public TerritorioClique.Dono DestinatarioTransferenciaSelecionado { get; }
    public MatchUIFeedbackState FeedbackAtual { get; }
    public IReadOnlyList<MatchUIPlayerState> Jogadores { get; }
    public IReadOnlyList<MatchUITerritoryState> Territorios { get; }
    public IReadOnlyList<MatchUIActionState> Acoes { get; }
    public IReadOnlyList<MatchUIReinforcementState> Distribuicoes { get; }
    public MatchUITransferState Transferencia { get; }
    public MatchUIResultState Resultado { get; }
    public bool UsaRandomCards { get; }
    public IReadOnlyList<string> CartasJogadorLocal { get; }
    public bool EmSelecaoInicial { get; }
    public int EtapaSelecaoInicial { get; }
    public string FeedbackSelecaoInicial { get; }

    internal MatchUIState(
        int round,
        GameManager.FaseTurno fase,
        GameManager.EstadoPreparacao estadoPreparacao,
        GameManager.ModoAcao modoAcao,
        GerenciadorRodada.EstadoPartida estadoPartida,
        bool emMorteSubita,
        int roundMorteSubita,
        float tempoRestante,
        int reforcosDisponiveis,
        int acoesPreparadas,
        int limiteAcoes,
        bool transferenciaUsada,
        bool transferenciaDisponivel,
        bool usaEquipes,
        bool acoesEnviadas,
        bool podeCancelarEnvio,
        bool podeEditarPreparacao,
        bool podeEnviarPreparacao,
        bool podeConfirmarAcao,
        bool podeDesfazerAcao,
        bool podeAdicionarReforco,
        TerritorioClique.Dono jogadorLocal,
        string origemSelecionadaId,
        string destinoSelecionadoId,
        string territorioTransferenciaId,
        int quantidadeAcaoSelecionada,
        int quantidadeMaximaAcao,
        string tipoAcaoEsperado,
        string feedbackTransferencia,
        TerritorioClique.Dono[] destinatariosTransferencia,
        TerritorioClique.Dono destinatarioTransferenciaSelecionado,
        MatchUIFeedbackState feedbackAtual,
        MatchUIPlayerState[] jogadores,
        MatchUITerritoryState[] territorios,
        MatchUIActionState[] acoes,
        MatchUIReinforcementState[] distribuicoes,
        MatchUITransferState transferencia,
        MatchUIResultState resultado,
        bool usaRandomCards,
        string[] cartasJogadorLocal,
        bool emSelecaoInicial,
        int etapaSelecaoInicial,
        string feedbackSelecaoInicial,
        string chaveConteudo)
    {
        Round = round;
        Fase = fase;
        EstadoPreparacao = estadoPreparacao;
        ModoAcao = modoAcao;
        EstadoPartida = estadoPartida;
        EmMorteSubita = emMorteSubita;
        RoundMorteSubita = roundMorteSubita;
        TempoRestante = tempoRestante;
        ReforcosDisponiveis = reforcosDisponiveis;
        AcoesPreparadas = acoesPreparadas;
        LimiteAcoes = limiteAcoes;
        TransferenciaUsada = transferenciaUsada;
        TransferenciaDisponivel = transferenciaDisponivel;
        UsaEquipes = usaEquipes;
        AcoesEnviadas = acoesEnviadas;
        PodeCancelarEnvio = podeCancelarEnvio;
        PodeEditarPreparacao = podeEditarPreparacao;
        PodeEnviarPreparacao = podeEnviarPreparacao;
        PodeConfirmarAcao = podeConfirmarAcao;
        PodeDesfazerAcao = podeDesfazerAcao;
        PodeAdicionarReforco = podeAdicionarReforco;
        JogadorLocal = jogadorLocal;
        OrigemSelecionadaId = origemSelecionadaId ?? string.Empty;
        DestinoSelecionadoId = destinoSelecionadoId ?? string.Empty;
        TerritorioTransferenciaId = territorioTransferenciaId ?? string.Empty;
        QuantidadeAcaoSelecionada = quantidadeAcaoSelecionada;
        QuantidadeMaximaAcao = quantidadeMaximaAcao;
        TipoAcaoEsperado = tipoAcaoEsperado ?? string.Empty;
        FeedbackTransferencia = feedbackTransferencia ?? string.Empty;
        DestinatariosTransferencia = Array.AsReadOnly(
            destinatariosTransferencia ?? Array.Empty<TerritorioClique.Dono>());
        DestinatarioTransferenciaSelecionado = destinatarioTransferenciaSelecionado;
        FeedbackAtual = feedbackAtual;
        Jogadores = Array.AsReadOnly(jogadores ?? Array.Empty<MatchUIPlayerState>());
        Territorios = Array.AsReadOnly(territorios ?? Array.Empty<MatchUITerritoryState>());
        Acoes = Array.AsReadOnly(acoes ?? Array.Empty<MatchUIActionState>());
        Distribuicoes = Array.AsReadOnly(distribuicoes ?? Array.Empty<MatchUIReinforcementState>());
        Transferencia = transferencia;
        Resultado = resultado;
        UsaRandomCards = usaRandomCards;
        CartasJogadorLocal = Array.AsReadOnly(
            cartasJogadorLocal ?? Array.Empty<string>());
        EmSelecaoInicial = emSelecaoInicial;
        EtapaSelecaoInicial = etapaSelecaoInicial;
        FeedbackSelecaoInicial = feedbackSelecaoInicial ?? string.Empty;
        this.chaveConteudo = chaveConteudo ?? string.Empty;
    }

    // ============================================================
    // 02. COMPARAÇÃO PARA PUBLICAÇÃO DE MUDANÇAS
    // ============================================================

    public bool Equals(MatchUIState other) =>
        other != null && chaveConteudo == other.chaveConteudo;

    public override bool Equals(object obj) => Equals(obj as MatchUIState);

    public override int GetHashCode() => chaveConteudo.GetHashCode();
}

// ============================================================
// 03. DADOS SIMPLES EXPOSTOS À APRESENTAÇÃO
// ============================================================

public readonly struct MatchUIPlayerState
{
    public TerritorioClique.Dono Jogador { get; }
    public EquipesJogadores.Equipe Equipe { get; }
    public int TerritoriosControlados { get; }
    public bool EhJogadorLocal { get; }
    public string Nome { get; }
    public Color Cor { get; }
    public MatchUIPlayerRoundStatus EstadoRodada { get; }

    public MatchUIPlayerState(
        TerritorioClique.Dono jogador,
        EquipesJogadores.Equipe equipe,
        int territoriosControlados,
        bool ehJogadorLocal,
        string nome,
        Color cor,
        MatchUIPlayerRoundStatus estadoRodada)
    {
        Jogador = jogador;
        Equipe = equipe;
        TerritoriosControlados = territoriosControlados;
        EhJogadorLocal = ehJogadorLocal;
        Nome = nome ?? string.Empty;
        Cor = cor;
        EstadoRodada = estadoRodada;
    }
}

public enum MatchUIPlayerRoundStatus
{
    Participante,
    Preparando,
    Enviado,
    EmResolucao
}

[Flags]
public enum MatchUITerritoryVisualState
{
    Normal = 0,
    Hover = 1 << 0,
    Selecionado = 1 << 1,
    Origem = 1 << 2,
    Destino = 1 << 3,
    DestinoValido = 1 << 4,
    DestinoInvalido = 1 << 5,
    Aliado = 1 << 6,
    Inimigo = 1 << 7,
    AcaoPreparada = 1 << 8,
    EmResolucao = 1 << 9,
    ConquistaEmTransicao = 1 << 10
}

public readonly struct MatchUITerritoryState
{
    public string Id { get; }
    public TerritorioClique.Dono Proprietario { get; }
    public int Tropas { get; }
    public MatchUITerritoryVisualState EstadoVisual { get; }

    public MatchUITerritoryState(
        string id,
        TerritorioClique.Dono proprietario,
        int tropas,
        MatchUITerritoryVisualState estadoVisual)
    {
        Id = id ?? string.Empty;
        Proprietario = proprietario;
        Tropas = tropas;
        EstadoVisual = estadoVisual;
    }
}

public enum MatchUIFeedbackKind
{
    Nenhum,
    Informacao,
    Sucesso,
    Aviso,
    Erro
}

public readonly struct MatchUIFeedbackState
{
    public MatchUIFeedbackKind Tipo { get; }
    public string Mensagem { get; }

    public MatchUIFeedbackState(MatchUIFeedbackKind tipo, string mensagem)
    {
        Tipo = tipo;
        Mensagem = mensagem ?? string.Empty;
    }
}

public readonly struct MatchUIActionState
{
    public int Posicao { get; }
    public string OrigemId { get; }
    public string DestinoId { get; }
    public int Quantidade { get; }
    public string TipoEsperado { get; }
    public MatchUIActionKind Kind { get; }

    public MatchUIActionState(int posicao, string origemId, string destinoId, int quantidade, string tipoEsperado)
        : this(posicao, origemId, destinoId, quantidade, tipoEsperado,
            MatchUIActionKindUtility.FromLegacyLabel(tipoEsperado))
    {
    }

    public MatchUIActionState(int posicao, string origemId, string destinoId,
        int quantidade, string tipoEsperado, MatchUIActionKind kind)
    {
        Posicao = posicao;
        OrigemId = origemId ?? string.Empty;
        DestinoId = destinoId ?? string.Empty;
        Quantidade = quantidade;
        TipoEsperado = tipoEsperado ?? string.Empty;
        Kind = kind;
    }
}

// ============================================================
// 03.1. CLASSIFICAÇÃO SEMÂNTICA SEM DEPENDER DO TEXTO DA UI
// ============================================================
public enum MatchUIActionKind
{
    Attack,
    Occupation,
    OwnMovement,
    AlliedMovement
}

public static class MatchUIActionKindUtility
{
    public static MatchUIActionKind FromLegacyLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return MatchUIActionKind.Attack;
        if (label.IndexOf("aliado", StringComparison.OrdinalIgnoreCase) >= 0) return MatchUIActionKind.AlliedMovement;
        if (label.IndexOf("movimento", StringComparison.OrdinalIgnoreCase) >= 0) return MatchUIActionKind.OwnMovement;
        if (label.IndexOf("ocup", StringComparison.OrdinalIgnoreCase) >= 0) return MatchUIActionKind.Occupation;
        return MatchUIActionKind.Attack;
    }
}

public readonly struct MatchUIReinforcementState
{
    public int Id { get; }
    public string TerritorioId { get; }
    public int Quantidade { get; }

    public MatchUIReinforcementState(int id, string territorioId, int quantidade)
    {
        Id = id;
        TerritorioId = territorioId ?? string.Empty;
        Quantidade = quantidade;
    }
}

public sealed class MatchUITransferState
{
    public string TerritorioId { get; }
    public TerritorioClique.Dono Autor { get; }
    public TerritorioClique.Dono Destinatario { get; }

    public MatchUITransferState(string territorioId, TerritorioClique.Dono autor, TerritorioClique.Dono destinatario)
    {
        TerritorioId = territorioId ?? string.Empty;
        Autor = autor;
        Destinatario = destinatario;
    }
}

public sealed class MatchUIResultState
{
    public bool Encerrada { get; }
    public bool Empate { get; }
    public ResultadoPartida.TipoVencedor Tipo { get; }
    public TerritorioClique.Dono JogadorVencedor { get; }
    public EquipesJogadores.Equipe EquipeVencedora { get; }
    public int Territorios { get; }
    public int RoundFinal { get; }
    public bool HouveMorteSubita { get; }

    public MatchUIResultState(ResultadoPartida resultado)
    {
        Encerrada = resultado.Encerrada;
        Empate = resultado.Empate;
        Tipo = resultado.Tipo;
        JogadorVencedor = resultado.JogadorVencedor;
        EquipeVencedora = resultado.EquipeVencedora;
        Territorios = resultado.QuantidadeTerritorios;
        RoundFinal = resultado.RoundFinal;
        HouveMorteSubita = resultado.HouveMorteSubita;
    }
}

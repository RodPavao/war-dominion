using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WDGameWeaponPlayerView
{
    public string PlayerId { get; }
    public int SlotIndex { get; }
    public int TeamIndex { get; }

    public WDGameWeaponPlayerView(string playerId, int slotIndex, int teamIndex)
    {
        PlayerId = playerId ?? string.Empty;
        SlotIndex = slotIndex;
        TeamIndex = teamIndex;
    }
}

public sealed class WDGameWeaponTerritoryView
{
    public string TerritoryId { get; }
    public string OwnerPlayerId { get; }
    public int Troops { get; }
    public WDWeaponTerritoryStateKind State { get; }

    public WDGameWeaponTerritoryView(
        string territoryId, string ownerPlayerId, int troops,
        WDWeaponTerritoryStateKind state)
    {
        TerritoryId = territoryId ?? string.Empty;
        OwnerPlayerId = ownerPlayerId ?? string.Empty;
        Troops = Math.Max(0, troops);
        State = state;
    }
}

public interface IWDGameWeaponStateSource
{
    int CurrentRound { get; }
    string ActiveMapId { get; }
    IReadOnlyList<WDGameWeaponPlayerView> GetPlayers();
    IReadOnlyList<string> GetOfficialPlayerOrder();
    IReadOnlyList<WDGameWeaponTerritoryView> GetTerritories();
    bool AreConnected(string originId, string targetId, TipoConexaoMapa type);
    IReadOnlyList<string> GetNeighbours(string territoryId, TipoConexaoMapa? type);
}

public static class WDGameWeaponStateMapper
{
    // ============================================================
    // 01. CONVERSÃO SEMÂNTICA EXPLÍCITA, SEM INFERÊNCIA POR TROPAS
    // ============================================================

    public static WDWeaponTerritoryStateKind ToWeaponState(
        TerritorioClique.EstadoTerritorial state)
    {
        return state switch
        {
            TerritorioClique.EstadoTerritorial.Ocupado =>
                WDWeaponTerritoryStateKind.Occupied,
            TerritorioClique.EstadoTerritorial.Neutro =>
                WDWeaponTerritoryStateKind.Neutral,
            TerritorioClique.EstadoTerritorial.Vazio =>
                WDWeaponTerritoryStateKind.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}

public sealed class WDGameWeaponContext
{
    private readonly IWDGameWeaponStateSource source;

    // ============================================================
    // 01. VISÃO READ-ONLY DA PARTIDA AUTORITATIVA
    // ============================================================

    public int CurrentRound => Math.Max(1, source.CurrentRound);
    public string ActiveMapId => source.ActiveMapId ?? string.Empty;
    public IReadOnlyList<WDGameWeaponPlayerView> Players => source.GetPlayers();
    public IReadOnlyList<string> OfficialPlayerOrder => source.GetOfficialPlayerOrder();
    public IReadOnlyList<WDGameWeaponTerritoryView> Territories => source.GetTerritories();

    public WDGameWeaponContext(GameManager gameManager)
        : this(new WDUnityGameWeaponStateSource(gameManager))
    {
    }

    public WDGameWeaponContext(IWDGameWeaponStateSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    // ============================================================
    // 02. IDENTIDADE, EQUIPES E TERRITÓRIOS
    // ============================================================

    public WDGameWeaponPlayerView FindPlayer(string playerId) =>
        Players.FirstOrDefault(player => player.PlayerId == playerId);

    public WDGameWeaponTerritoryView FindTerritory(string territoryId) =>
        Territories.FirstOrDefault(territory => territory.TerritoryId == territoryId);

    public bool AreAllied(string playerAId, string playerBId)
    {
        if (string.IsNullOrEmpty(playerAId) || string.IsNullOrEmpty(playerBId) ||
            playerAId == playerBId)
            return false;

        WDGameWeaponPlayerView playerA = FindPlayer(playerAId);
        WDGameWeaponPlayerView playerB = FindPlayer(playerBId);
        if (playerA == null || playerB == null)
            return false;

        return Players.Count(player => player.TeamIndex == playerA.TeamIndex) > 1 &&
            playerA.TeamIndex == playerB.TeamIndex;
    }

    public bool AreOpponents(string playerAId, string playerBId) =>
        FindPlayer(playerAId) != null && FindPlayer(playerBId) != null &&
        playerAId != playerBId && !AreAllied(playerAId, playerBId);

    // ============================================================
    // 03. GRAFO CANÔNICO DO MAPA
    // ============================================================

    public bool AreConnected(
        string originId, string targetId, TipoConexaoMapa type) =>
        source.AreConnected(originId, targetId, type);

    public IReadOnlyList<string> GetNeighbours(string territoryId) =>
        source.GetNeighbours(territoryId, null);

    public IReadOnlyList<string> GetNeighbours(
        string territoryId, TipoConexaoMapa type) =>
        source.GetNeighbours(territoryId, type);
}

internal sealed class WDUnityGameWeaponStateSource : IWDGameWeaponStateSource
{
    private readonly GameManager gameManager;

    public int CurrentRound => gameManager != null ? gameManager.RodadaAtual : 1;
    public string ActiveMapId => MapaAtivo.Instance?.Definicao?.MapaId ?? string.Empty;

    public WDUnityGameWeaponStateSource(GameManager gameManager)
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
    }

    // ============================================================
    // 04. PROJEÇÃO DE PARTICIPANTES E ORDEM SEM CÓPIA CONCORRENTE
    // ============================================================

    public IReadOnlyList<WDGameWeaponPlayerView> GetPlayers()
    {
        WDMatchSetup setup = gameManager.MatchSetupAtual;
        if (setup == null)
            return Array.Empty<WDGameWeaponPlayerView>();

        return setup.Participants
            .OrderBy(participant => participant.SlotIndex)
            .Select(participant => new WDGameWeaponPlayerView(
                participant.PlayerId, participant.SlotIndex, participant.TeamIndex))
            .ToArray();
    }

    public IReadOnlyList<string> GetOfficialPlayerOrder()
    {
        var playerIdByOwner = GetPlayers().ToDictionary(
            player => OwnerFromSlot(player.SlotIndex), player => player.PlayerId);
        return gameManager.ObterPrioridadeJogadoresParaUI()
            .Where(playerIdByOwner.ContainsKey)
            .Select(owner => playerIdByOwner[owner])
            .ToArray();
    }

    // ============================================================
    // 05. CONVERSÃO EXPLÍCITA DO ESTADO TERRITORIAL REAL
    // ============================================================

    public IReadOnlyList<WDGameWeaponTerritoryView> GetTerritories()
    {
        var playerIdByOwner = GetPlayers().ToDictionary(
            player => OwnerFromSlot(player.SlotIndex), player => player.PlayerId);
        return MapaAtivo.ObterTerritoriosOuCena()
            .Where(territory => territory != null &&
                !string.IsNullOrWhiteSpace(territory.idTerritorio))
            .Select(territory => new WDGameWeaponTerritoryView(
                territory.idTerritorio,
                playerIdByOwner.TryGetValue(territory.dono, out string playerId)
                    ? playerId : string.Empty,
                territory.Tropas,
                WDGameWeaponStateMapper.ToWeaponState(territory.Estado)))
            .ToArray();
    }

    private static TerritorioClique.Dono OwnerFromSlot(int slotIndex) =>
        (TerritorioClique.Dono)(slotIndex + 1);

    // ============================================================
    // 06. CONEXÕES DA DEFINIÇÃO CANÔNICA DO MAPA ATIVO
    // ============================================================

    public bool AreConnected(
        string originId, string targetId, TipoConexaoMapa type) =>
        MapaAtivo.SaoConectados(
            MapaAtivo.Instance?.Definicao, originId, targetId, type);

    public IReadOnlyList<string> GetNeighbours(
        string territoryId, TipoConexaoMapa? type)
    {
        DefinicaoMapa map = MapaAtivo.Instance?.Definicao;
        if (map == null || string.IsNullOrWhiteSpace(territoryId) ||
            !map.Territorios.Any(territory => territory != null && territory.id == territoryId))
            return Array.Empty<string>();

        var neighbours = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefinicaoConexaoMapa connection in map.Conexoes)
        {
            if (connection == null || type.HasValue && connection.tipo != type.Value)
                continue;
            if (connection.origemId == territoryId)
                neighbours.Add(connection.destinoId);
            if (connection.bidirecional && connection.destinoId == territoryId)
                neighbours.Add(connection.origemId);
        }
        return neighbours.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }
}

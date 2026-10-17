using System;
using System.Collections.Generic;
using System.Linq;

public enum WDFriendlyMode { OneVsOne = 1, TwoVsTwo = 2, ThreeVsThree = 3 }
public enum WDFriendlyCardMode { Normal, RandomCards }
public enum WDFriendlyMapMode { Classic, Random }
public enum WDFriendlyRoomState { WaitingForPlayers, Ready, Starting, Closed }
public enum WDFriendlyRoomOperationResult
{
    Success, InvalidConfiguration, InvalidPlayer, DuplicatePlayer, RoomFull,
    TeamFull, HostProtected, InvalidDeck, InvalidState
}

public sealed class WDFriendlyRoomConfiguration
{
    // ============================================================
    // 01. CONFIGURAÇÃO IMUTÁVEL DA SALA AMISTOSA
    // ============================================================
    public WDFriendlyMode Mode { get; }
    public WDFriendlyCardMode CardMode { get; }
    public WDFriendlyMapMode MapMode { get; }
    public bool PrivateRoom { get; }
    public bool FriendsOnly { get; }
    public int TeamSize => (int)Mode;
    public int Capacity => TeamSize * 2;
    public bool RequiresDeck => CardMode == WDFriendlyCardMode.Normal;

    public WDFriendlyRoomConfiguration(WDFriendlyMode mode,
        WDFriendlyCardMode cardMode, WDFriendlyMapMode mapMode,
        bool privateRoom, bool friendsOnly)
    {
        Mode = mode;
        CardMode = cardMode;
        MapMode = mapMode;
        PrivateRoom = privateRoom;
        FriendsOnly = friendsOnly;
    }
}

public sealed class WDFriendlyRoomParticipant
{
    public string PlayerId { get; }
    public string Nickname { get; }
    public int TeamIndex { get; internal set; }
    public int PositionIndex { get; internal set; }
    public bool IsHost { get; }
    public bool IsReady { get; internal set; }
    public int? SelectedDeckIndex { get; internal set; }
    public bool HasValidDeck { get; internal set; }

    internal WDFriendlyRoomParticipant(string playerId, string nickname,
        int teamIndex, int positionIndex, bool isHost)
    {
        PlayerId = playerId;
        Nickname = nickname;
        TeamIndex = teamIndex;
        PositionIndex = positionIndex;
        IsHost = isHost;
    }
}

public static class WDFriendlyRules
{
    // ============================================================
    // 02. REGRAS COMPATÍVEIS COM O CATÁLOGO REAL DE PARTIDAS
    // ============================================================
    public static bool IsSupported(WDFriendlyRoomConfiguration configuration) =>
        configuration != null &&
        !(configuration.Mode == WDFriendlyMode.ThreeVsThree &&
          configuration.CardMode == WDFriendlyCardMode.RandomCards);

    public static string ModeId(WDFriendlyMode mode) => mode switch
    {
        WDFriendlyMode.OneVsOne => "1x1",
        WDFriendlyMode.TwoVsTwo => "2x2",
        _ => "3x3"
    };

    public static string SubmodeId(WDFriendlyCardMode mode) =>
        mode == WDFriendlyCardMode.RandomCards ? "random_cards" : "normal";

    public static bool TryCreateRequest(WDFriendlyRoomConfiguration configuration,
        WarDominionMatchFlowConfig catalog, out WDMatchmakingRequest request,
        out string error)
    {
        request = null;
        error = string.Empty;
        if (!IsSupported(configuration))
        {
            error = "A combinação 3V3 com Random Cards não está disponível.";
            return false;
        }
        if (catalog == null)
        {
            error = "Catálogo de partidas indisponível.";
            return false;
        }

        string modeId = ModeId(configuration.Mode);
        string submodeId = SubmodeId(configuration.CardMode);
        WDMatchModeDefinition mode = catalog.Modes.FirstOrDefault(item =>
            item != null && item.Id == modeId &&
            WDContentAvailabilityRules.IsAvailable(item.Availability));
        WDMatchSubmodeDefinition submode = mode?.Submodes.FirstOrDefault(item =>
            item != null && item.Id == submodeId &&
            WDContentAvailabilityRules.IsAvailable(item.Availability));
        if (mode == null || submode == null)
        {
            error = "Modo ou regra de baralho indisponível no catálogo.";
            return false;
        }
        if (configuration.MapMode == WDFriendlyMapMode.Classic &&
            (!catalog.IsMapAllowedForMode(modeId, "classic") ||
             !submode.EligibleMaps.Any(item => item.MapId == "classic")))
        {
            error = "Classic não está disponível para esta modalidade.";
            return false;
        }
        if (configuration.MapMode == WDFriendlyMapMode.Random &&
            catalog.GetEligibleMaps(modeId, submode.EligibleMaps).Count == 0)
        {
            error = "Nenhum mapa elegível está disponível.";
            return false;
        }

        request = new WDMatchmakingRequest(mode, submode, "friendly_room");
        return true;
    }
}

public sealed class WDFriendlyRoom
{
    // ============================================================
    // 03. DOMÍNIO DA SALA: PARTICIPANTES, EQUIPES, DECKS E PRONTIDÃO
    // ============================================================
    private readonly List<WDFriendlyRoomParticipant> participants = new();

    public string RoomId { get; }
    public string HostPlayerId { get; }
    public WDFriendlyRoomConfiguration Configuration { get; private set; }
    public WDFriendlyRoomState State { get; private set; }
    public IReadOnlyList<WDFriendlyRoomParticipant> Participants => participants;
    public int AvailableSlots => Configuration.Capacity - participants.Count;
    public bool IsCompositionComplete => participants.Count == Configuration.Capacity &&
        Enumerable.Range(0, 2).All(team =>
            participants.Count(item => item.TeamIndex == team) == Configuration.TeamSize);

    public WDFriendlyRoom(string roomId, string hostPlayerId, string hostNickname,
        WDFriendlyRoomConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Sala sem ID.", nameof(roomId));
        if (string.IsNullOrWhiteSpace(hostPlayerId)) throw new ArgumentException("Host sem ID.", nameof(hostPlayerId));
        if (!WDFriendlyRules.IsSupported(configuration)) throw new ArgumentException("Configuração incompatível.", nameof(configuration));
        RoomId = roomId;
        HostPlayerId = hostPlayerId;
        Configuration = configuration;
        State = WDFriendlyRoomState.WaitingForPlayers;
        participants.Add(new WDFriendlyRoomParticipant(hostPlayerId,
            hostNickname ?? string.Empty, 0, 0, true));
    }

    public WDFriendlyRoomParticipant Find(string playerId) => participants.FirstOrDefault(item =>
        string.Equals(item.PlayerId, playerId, StringComparison.Ordinal));

    public WDFriendlyRoomOperationResult AddParticipant(string playerId,
        string nickname, int? preferredTeam = null)
    {
        if (State != WDFriendlyRoomState.WaitingForPlayers && State != WDFriendlyRoomState.Ready)
            return WDFriendlyRoomOperationResult.InvalidState;
        if (string.IsNullOrWhiteSpace(playerId)) return WDFriendlyRoomOperationResult.InvalidPlayer;
        if (Find(playerId) != null) return WDFriendlyRoomOperationResult.DuplicatePlayer;
        if (participants.Count >= Configuration.Capacity) return WDFriendlyRoomOperationResult.RoomFull;
        int team = preferredTeam ?? Enumerable.Range(0, 2)
            .OrderBy(index => participants.Count(item => item.TeamIndex == index)).First();
        if (team < 0 || team > 1) return WDFriendlyRoomOperationResult.InvalidPlayer;
        if (participants.Count(item => item.TeamIndex == team) >= Configuration.TeamSize)
            return WDFriendlyRoomOperationResult.TeamFull;
        int position = Enumerable.Range(0, Configuration.TeamSize).First(index =>
            participants.All(item => item.TeamIndex != team || item.PositionIndex != index));
        participants.Add(new WDFriendlyRoomParticipant(playerId, nickname ?? string.Empty,
            team, position, false));
        InvalidateReadyState();
        return WDFriendlyRoomOperationResult.Success;
    }

    public WDFriendlyRoomOperationResult RemoveParticipant(string playerId)
    {
        WDFriendlyRoomParticipant participant = Find(playerId);
        if (participant == null) return WDFriendlyRoomOperationResult.InvalidPlayer;
        if (participant.IsHost) return WDFriendlyRoomOperationResult.HostProtected;
        participants.Remove(participant);
        InvalidateReadyState();
        return WDFriendlyRoomOperationResult.Success;
    }

    public WDFriendlyRoomOperationResult MoveParticipant(string playerId, int targetTeam)
    {
        WDFriendlyRoomParticipant participant = Find(playerId);
        if (participant == null || targetTeam < 0 || targetTeam > 1)
            return WDFriendlyRoomOperationResult.InvalidPlayer;
        if (participant.IsHost) return WDFriendlyRoomOperationResult.HostProtected;
        if (participant.TeamIndex == targetTeam) return WDFriendlyRoomOperationResult.Success;
        if (participants.Count(item => item.TeamIndex == targetTeam) >= Configuration.TeamSize)
            return WDFriendlyRoomOperationResult.TeamFull;
        participant.TeamIndex = targetTeam;
        participant.PositionIndex = Enumerable.Range(0, Configuration.TeamSize).First(index =>
            participants.All(item => item == participant || item.TeamIndex != targetTeam ||
                item.PositionIndex != index));
        InvalidateReadyState();
        return WDFriendlyRoomOperationResult.Success;
    }

    public WDFriendlyRoomOperationResult SetDeck(string playerId, int deckIndex, bool isValid)
    {
        WDFriendlyRoomParticipant participant = Find(playerId);
        if (participant == null) return WDFriendlyRoomOperationResult.InvalidPlayer;
        participant.SelectedDeckIndex = deckIndex;
        participant.HasValidDeck = isValid;
        participant.IsReady = false;
        RefreshState();
        return isValid ? WDFriendlyRoomOperationResult.Success : WDFriendlyRoomOperationResult.InvalidDeck;
    }

    public WDFriendlyRoomOperationResult ChangeConfiguration(
        WDFriendlyRoomConfiguration configuration)
    {
        if (State != WDFriendlyRoomState.WaitingForPlayers && State != WDFriendlyRoomState.Ready)
            return WDFriendlyRoomOperationResult.InvalidState;
        if (!WDFriendlyRules.IsSupported(configuration))
            return WDFriendlyRoomOperationResult.InvalidConfiguration;
        if (participants.Count > configuration.Capacity)
            return WDFriendlyRoomOperationResult.RoomFull;
        Configuration = configuration;
        // O host permanece em Vanguard/posição 0. Os demais participantes reais
        // são redistribuídos deterministicamente sem ultrapassar as equipes.
        WDFriendlyRoomParticipant host = Find(HostPlayerId);
        host.TeamIndex = 0;
        host.PositionIndex = 0;
        int nextTeam = 1;
        foreach (WDFriendlyRoomParticipant participant in participants.Where(item => !item.IsHost))
        {
            int team = Enumerable.Range(0, 2).Where(index =>
                    participants.Count(item => item != participant && item.TeamIndex == index) <
                    configuration.TeamSize)
                .OrderBy(index => participants.Count(item => item != participant &&
                    item.TeamIndex == index))
                .ThenBy(index => index == nextTeam ? 0 : 1).First();
            participant.TeamIndex = team;
            participant.PositionIndex = Enumerable.Range(0, configuration.TeamSize)
                .First(index => participants.All(item => item == participant ||
                    item.TeamIndex != team || item.PositionIndex != index));
            nextTeam = 1 - team;
        }
        InvalidateReadyState();
        return WDFriendlyRoomOperationResult.Success;
    }

    public WDFriendlyRoomOperationResult SetReady(string playerId, bool ready)
    {
        WDFriendlyRoomParticipant participant = Find(playerId);
        if (participant == null) return WDFriendlyRoomOperationResult.InvalidPlayer;
        if (ready && Configuration.RequiresDeck &&
            (!participant.SelectedDeckIndex.HasValue || !participant.HasValidDeck))
            return WDFriendlyRoomOperationResult.InvalidDeck;
        participant.IsReady = ready;
        RefreshState();
        return WDFriendlyRoomOperationResult.Success;
    }

    public bool CanStart(out string reason)
    {
        if (!WDFriendlyRules.IsSupported(Configuration))
            reason = "Configuração incompatível.";
        else if (!IsCompositionComplete)
            reason = $"Aguardando jogadores: {participants.Count}/{Configuration.Capacity}.";
        else if (participants.Any(item => !item.IsReady))
            reason = "Todos os jogadores precisam estar prontos.";
        else if (Configuration.RequiresDeck && participants.Any(item => !item.HasValidDeck))
            reason = "Todos os jogadores precisam de um deck válido.";
        else
        {
            reason = string.Empty;
            return true;
        }
        return false;
    }

    public bool TryBeginStart(out string reason)
    {
        if (!CanStart(out reason)) return false;
        State = WDFriendlyRoomState.Starting;
        return true;
    }

    public WDFriendlyRoomOperationResult Close(string requesterPlayerId)
    {
        if (!string.Equals(requesterPlayerId, HostPlayerId, StringComparison.Ordinal))
            return WDFriendlyRoomOperationResult.HostProtected;
        State = WDFriendlyRoomState.Closed;
        foreach (WDFriendlyRoomParticipant participant in participants)
            participant.IsReady = false;
        return WDFriendlyRoomOperationResult.Success;
    }

    private void InvalidateReadyState()
    {
        foreach (WDFriendlyRoomParticipant participant in participants)
            participant.IsReady = false;
        State = WDFriendlyRoomState.WaitingForPlayers;
    }

    private void RefreshState()
    {
        State = CanStart(out _) ? WDFriendlyRoomState.Ready :
            WDFriendlyRoomState.WaitingForPlayers;
    }
}

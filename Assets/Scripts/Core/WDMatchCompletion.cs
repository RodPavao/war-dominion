using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WDMatchCompletion
{
    // 01. Resultado único derivado do setup e do encerramento canônico.
    public string ResultId { get; }
    public string ModeId { get; }
    public string SubmodeId { get; }
    public string MapId { get; }
    public int LocalSlot { get; }
    public int LocalTeam { get; }
    public int WinnerSlot { get; }
    public int WinnerTeam { get; }
    public int Placement { get; }
    public bool IsDraw { get; }
    public WDMatchOrigin Origin { get; }
    public bool AwardsCompetitiveProgress => Origin != WDMatchOrigin.Friendly;
    public long PlayedAtUtcTicks { get; }
    public bool IsLocalWinner => !IsDraw && (WinnerTeam >= 0
        ? LocalTeam == WinnerTeam : LocalSlot == WinnerSlot);
    public int LocalTrophies => AwardsCompetitiveProgress && IsLocalWinner ? 40 : 0;

    private WDMatchCompletion(string resultId, WDMatchSetup setup, int localSlot,
        int localTeam, int winnerSlot, int winnerTeam, int placement, bool draw,
        long playedAtUtcTicks)
    {
        ResultId = resultId;
        ModeId = setup.ModeId;
        SubmodeId = setup.SubmodeId;
        MapId = setup.MapId;
        LocalSlot = localSlot;
        LocalTeam = localTeam;
        WinnerSlot = winnerSlot;
        WinnerTeam = winnerTeam;
        Placement = placement;
        IsDraw = draw;
        Origin = setup.Origin;
        PlayedAtUtcTicks = playedAtUtcTicks;
    }

    public int TrophiesFor(WDMatchParticipant participant) => AwardsCompetitiveProgress && participant != null &&
        !IsDraw && (WinnerTeam >= 0 ? participant.TeamIndex == WinnerTeam :
            participant.SlotIndex == WinnerSlot) ? 40 : 0;

    public static WDMatchCompletion Create(WDMatchSetup setup, ResultadoPartida result,
        IReadOnlyDictionary<int, int> territoriesBySlot, long playedAtUtcTicks)
    {
        if (setup == null || result == null || !result.Encerrada ||
            string.IsNullOrWhiteSpace(setup.MatchId) ||
            (setup.ModeId != "1x1" && setup.ModeId != "2x2" && setup.ModeId != "ffa"))
            return null;
        if (setup.Participants.Count(item => item.Kind == WDMatchParticipantKind.Local) != 1)
            return null;
        WDMatchParticipant local = setup.Participants.First(item =>
            item.Kind == WDMatchParticipantKind.Local);
        if (!result.Empate && (setup.ModeId == "2x2"
                ? result.Tipo != ResultadoPartida.TipoVencedor.Equipe
                : result.Tipo != ResultadoPartida.TipoVencedor.Jogador))
            return null;
        int winnerSlot = result.Tipo == ResultadoPartida.TipoVencedor.Jogador
            ? (int)result.JogadorVencedor - 1 : -1;
        int winnerTeam = result.Tipo == ResultadoPartida.TipoVencedor.Equipe
            ? result.EquipeVencedora == EquipesJogadores.Equipe.Vanguard ? 0
              : result.EquipeVencedora == EquipesJogadores.Equipe.Sentinel ? 1 : -1
            : -1;
        if (!result.Empate && (winnerTeam < 0 && winnerSlot < 0 ||
            winnerTeam >= 0 && !setup.Participants.Any(item => item.TeamIndex == winnerTeam) ||
            winnerSlot >= 0 && !setup.Participants.Any(item => item.SlotIndex == winnerSlot)))
            return null;
        int placement = 0;
        if (setup.ModeId == "ffa")
        {
            if (territoriesBySlot == null ||
                setup.Participants.Any(item => !territoriesBySlot.ContainsKey(item.SlotIndex)))
                return null;
            int localCount = territoriesBySlot[local.SlotIndex];
            // Empates de territórios compartilham colocação; não inventamos desempate.
            placement = 1 + setup.Participants.Count(item =>
                territoriesBySlot[item.SlotIndex] > localCount);
        }
        return new WDMatchCompletion(setup.MatchId, setup, local.SlotIndex,
            local.TeamIndex, winnerSlot, winnerTeam, placement, result.Empate,
            playedAtUtcTicks);
    }
}

[Serializable]
public sealed class WDLocalMatchRecord
{
    public string resultId;
    public string modeId;
    public string submodeId;
    public string mapId;
    public bool victory;
    public bool draw;
    public int placement;
    public int localTeam;
    public int trophiesEarned;
    public long playedAtUtcTicks;

    public WDLocalMatchRecord() { }

    public WDLocalMatchRecord(WDMatchCompletion result)
    {
        resultId = result.ResultId;
        modeId = result.ModeId;
        submodeId = result.SubmodeId;
        mapId = result.MapId;
        victory = result.IsLocalWinner;
        draw = result.IsDraw;
        placement = result.Placement;
        localTeam = result.LocalTeam;
        trophiesEarned = result.LocalTrophies;
        playedAtUtcTicks = result.PlayedAtUtcTicks;
    }
}

[Serializable]
public sealed class WDLocalMissionProgressRecord
{
    public string missionId;
    public int current;
    public bool discovered;
    public int completionCount;
    public int processedRewardCount;

    public WDLocalMissionProgressRecord() { }
    public WDLocalMissionProgressRecord(WDMissionProgress value)
    {
        missionId = value.MissionId;
        current = value.Current;
        discovered = value.IsDiscovered;
        completionCount = value.CompletionCount;
        processedRewardCount = value.ProcessedRewardCount;
    }

    public WDMissionProgress ToDomain() => new(missionId, current, discovered,
        completionCount, processedRewardCount);
}

[Serializable]
public sealed class WDLocalModeCounter
{
    public string modeId;
    public int matches;
    public int wins;
    public int draws;
}

[Serializable]
public sealed class WDLocalMatchProgress
{
    // 02. Um único documento persistido contém projeções e IDs já aplicados.
    public int trophies;
    public int matches;
    public int wins;
    public int draws;
    public List<WDLocalMatchRecord> history = new();
    public List<string> processedResultIds = new();
    public List<WDLocalMissionProgressRecord> missions = new();
    public List<WDLocalModeCounter> modes = new();

    public void Normalize()
    {
        trophies = Math.Max(0, trophies);
        matches = Math.Max(0, matches);
        wins = Math.Max(0, Math.Min(matches, wins));
        draws = Math.Max(0, Math.Min(matches - wins, draws));
        history ??= new List<WDLocalMatchRecord>();
        processedResultIds ??= new List<string>();
        missions ??= new List<WDLocalMissionProgressRecord>();
        modes ??= new List<WDLocalModeCounter>();
        if (history.Count > 10) history.RemoveRange(10, history.Count - 10);
    }
}

public interface IWDLocalMatchProgressStore
{
    WDLocalMatchProgress Load();
    void Save(WDLocalMatchProgress progress);
}

public sealed class WDPlayerPrefsLocalMatchProgressStore : IWDLocalMatchProgressStore
{
    private const string Key = "war_dominion.match_progress.v1";
    public WDLocalMatchProgress Load()
    {
        WDLocalMatchProgress value = PlayerPrefs.HasKey(Key)
            ? JsonUtility.FromJson<WDLocalMatchProgress>(PlayerPrefs.GetString(Key)) : null;
        value ??= new WDLocalMatchProgress();
        value.Normalize();
        return value;
    }

    public void Save(WDLocalMatchProgress progress)
    {
        progress.Normalize();
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(progress));
        PlayerPrefs.Save();
    }
}

public sealed class WDLocalMatchResultService
{
    // 03. Aplicação idempotente; Perfil e Ranking só projetam este documento.
    private readonly IWDLocalMatchProgressStore store;
    private readonly WDMissionRepository missions;
    private WDLocalMatchProgress progress;

    public WDLocalMatchProgress Progress => progress;
    public WDMissionRepository Missions => missions;

    public WDLocalMatchResultService(IWDLocalMatchProgressStore store,
        IWDMissionRewardProcessor rewardProcessor = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        progress = store.Load() ?? new WDLocalMatchProgress();
        progress.Normalize();
        missions = new WDMissionRepository(initialProgress: progress.missions
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.missionId))
            .Select(item => item.ToDomain()).ToArray(), rewardProcessor: rewardProcessor);
    }

    public bool TryApply(WDMatchCompletion result)
    {
        // 04. Amistosos são concluídos normalmente pelo motor, mas nunca alimentam
        // ranking, histórico competitivo, missões ou processadores de recompensa.
        if (result != null && !result.AwardsCompetitiveProgress) return false;
        if (result == null || string.IsNullOrWhiteSpace(result.ResultId) ||
            progress.processedResultIds.Contains(result.ResultId)) return false;
        progress.processedResultIds.Add(result.ResultId);
        progress.matches++;
        if (result.IsLocalWinner) progress.wins++;
        if (result.IsDraw) progress.draws++;
        progress.trophies += result.LocalTrophies;
        WDLocalModeCounter mode = progress.modes.FirstOrDefault(item =>
            item.modeId == result.ModeId);
        if (mode == null)
        {
            mode = new WDLocalModeCounter { modeId = result.ModeId };
            progress.modes.Add(mode);
        }
        mode.matches++;
        if (result.IsLocalWinner) mode.wins++;
        if (result.IsDraw) mode.draws++;
        progress.history.Insert(0, new WDLocalMatchRecord(result));
        if (progress.history.Count > 10) progress.history.RemoveRange(10,
            progress.history.Count - 10);
        // Só "first-win" tem condição verificável; os demais mocks não a possuem.
        if (result.IsLocalWinner) missions.AddProgress("first-win");
        progress.missions = missions.Catalog.Select(item => missions.Progress(item.MissionId))
            .Select(item => new WDLocalMissionProgressRecord(item)).ToList();
        store.Save(progress);
        return true;
    }
}

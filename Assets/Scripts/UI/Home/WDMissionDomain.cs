using System;
using System.Collections.Generic;
using System.Linq;

public enum WDMissionCategory { Match, Mode, Territory, Action, Prototype }
public enum WDMissionRewardType { Coins, Emeralds, WeaponTokens, Other }
public enum WDMissionCollectionState { Ready, Loading, Empty, Error, Unavailable }
public enum WDMissionStatus { SecretUndiscovered, InProgress, Completed }

public sealed class WDMissionRewardDefinition
{
    public WDMissionRewardType Type { get; }
    public int? Quantity { get; }

    public WDMissionRewardDefinition(WDMissionRewardType type, int? quantity = null)
    { Type = type; Quantity = quantity; }
}

public sealed class WDMissionDefinition
{
    // ============================================================
    // 01. DEFINIÇÃO PERMANENTE E DATA-DRIVEN
    // ============================================================

    public string MissionId { get; }
    public string Title { get; }
    public string Description { get; }
    public string Condition { get; }
    public WDMissionCategory Category { get; }
    public bool IsSecret { get; }
    public bool IsRepeatable { get; }
    public int Target { get; }
    public WDMissionRewardDefinition Reward { get; }
    public string FutureArtKey { get; }
    public bool IsPermanent => true;

    public WDMissionDefinition(string missionId, string title, string description,
        string condition, WDMissionCategory category, bool isSecret, bool isRepeatable,
        int target, WDMissionRewardDefinition reward = null, string futureArtKey = null)
    {
        if (string.IsNullOrWhiteSpace(missionId)) throw new ArgumentException(nameof(missionId));
        if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
        MissionId = missionId; Title = title ?? string.Empty; Description = description ?? string.Empty;
        Condition = condition ?? string.Empty; Category = category; IsSecret = isSecret;
        IsRepeatable = isRepeatable; Target = target; Reward = reward; FutureArtKey = futureArtKey;
    }
}

public sealed class WDMissionProgress
{
    public string MissionId { get; }
    public int Current { get; internal set; }
    public bool IsDiscovered { get; internal set; }
    public int CompletionCount { get; internal set; }
    public int ProcessedRewardCount { get; internal set; }

    public WDMissionProgress(string missionId, int current = 0, bool discovered = false,
        int completionCount = 0, int processedRewardCount = 0)
    {
        MissionId = missionId; Current = Math.Max(0, current); IsDiscovered = discovered;
        CompletionCount = Math.Max(0, completionCount);
        ProcessedRewardCount = Math.Max(0, processedRewardCount);
    }
}

public sealed class WDMissionPresentation
{
    public string MissionId { get; }
    public string Title { get; }
    public string Description { get; }
    public string Condition { get; }
    public string Category { get; }
    public string Reward { get; }
    public int? Current { get; }
    public int? Target { get; }
    public int CompletionCount { get; }
    public bool IsRepeatable { get; }
    public bool IsHidden { get; }
    public WDMissionStatus Status { get; }

    internal WDMissionPresentation(WDMissionDefinition definition, WDMissionProgress progress)
    {
        MissionId = definition.MissionId;
        IsHidden = definition.IsSecret && !progress.IsDiscovered;
        CompletionCount = progress.CompletionCount;
        IsRepeatable = definition.IsRepeatable;
        if (IsHidden)
        {
            Title = "???"; Description = "MISSÃO SECRETA"; Condition = "NÃO DESCOBERTA";
            Category = string.Empty; Reward = string.Empty; Current = null; Target = null;
            Status = WDMissionStatus.SecretUndiscovered;
            return;
        }
        Title = definition.Title; Description = definition.Description;
        Condition = definition.Condition; Category = definition.Category.ToString().ToUpperInvariant();
        Reward = definition.Reward == null ? string.Empty : RewardLabel(definition.Reward.Type);
        Current = progress.Current; Target = definition.Target;
        Status = progress.CompletionCount > 0 ? WDMissionStatus.Completed : WDMissionStatus.InProgress;
    }

    private static string RewardLabel(WDMissionRewardType type) => type switch
    {
        WDMissionRewardType.Coins => "MOEDAS · VALOR A DEFINIR",
        WDMissionRewardType.Emeralds => "ESMERALDAS · VALOR A DEFINIR",
        WDMissionRewardType.WeaponTokens => "TOKENS DE ARMA · VALOR A DEFINIR",
        _ => "RECOMPENSA FUTURA · A DEFINIR"
    };
}

public interface IWDMissionRewardProcessor
{
    bool Process(string grantId, WDMissionRewardDefinition reward);
}

public sealed class WDMemoryMissionRewardProcessor : IWDMissionRewardProcessor
{
    private readonly HashSet<string> processed = new(StringComparer.Ordinal);
    public int ProcessedCount => processed.Count;
    public bool Process(string grantId, WDMissionRewardDefinition reward) =>
        reward != null && processed.Add(grantId);
}

public sealed class WDMissionRepository
{
    // ============================================================
    // 02. AUTORIDADE ÚNICA DE CATÁLOGO, PROGRESSO E RECOMPENSAS
    // ============================================================

    private readonly List<WDMissionDefinition> catalog;
    private readonly Dictionary<string, WDMissionProgress> progress;
    private readonly IWDMissionRewardProcessor rewards;

    public WDMissionCollectionState State { get; set; } = WDMissionCollectionState.Ready;
    public IReadOnlyList<WDMissionDefinition> Catalog => catalog;
    public int TotalCount => catalog.Count;
    public int DiscoveredCount => catalog.Count(item => Progress(item.MissionId).IsDiscovered);

    public WDMissionRepository(IEnumerable<WDMissionDefinition> definitions = null,
        IEnumerable<WDMissionProgress> initialProgress = null,
        IWDMissionRewardProcessor rewardProcessor = null)
    {
        catalog = (definitions ?? CreatePrototypeCatalog()).ToList();
        if (catalog.Select(item => item.MissionId).Distinct(StringComparer.Ordinal).Count() != catalog.Count)
            throw new ArgumentException("MissionIds devem ser únicos.");
        progress = catalog.ToDictionary(item => item.MissionId,
            item => new WDMissionProgress(item.MissionId, discovered: !item.IsSecret),
            StringComparer.Ordinal);
        foreach (WDMissionProgress item in initialProgress ?? CreatePrototypeProgress())
            if (progress.ContainsKey(item.MissionId)) progress[item.MissionId] = item;
        rewards = rewardProcessor ?? new WDMemoryMissionRewardProcessor();
    }

    public WDMissionProgress Progress(string missionId) =>
        progress.TryGetValue(missionId, out WDMissionProgress value) ? value : null;

    public WDMissionPresentation Present(string missionId)
    {
        WDMissionDefinition definition = catalog.FirstOrDefault(item => item.MissionId == missionId);
        return definition == null ? null : new WDMissionPresentation(definition, Progress(missionId));
    }

    public IReadOnlyList<WDMissionPresentation> PresentAll() =>
        catalog.Select(item => Present(item.MissionId)).ToArray();

    public int AddProgress(string missionId, int amount = 1)
    {
        WDMissionDefinition definition = catalog.FirstOrDefault(item => item.MissionId == missionId);
        WDMissionProgress state = Progress(missionId);
        if (definition == null || state == null || amount <= 0) return 0;
        if (!definition.IsRepeatable && state.CompletionCount > 0) return 0;

        state.Current += amount;
        int completedNow = 0;
        while (state.Current >= definition.Target && (definition.IsRepeatable || state.CompletionCount == 0))
        {
            state.Current -= definition.Target;
            state.CompletionCount++;
            state.IsDiscovered = true;
            ProcessReward(definition, state);
            completedNow++;
            if (!definition.IsRepeatable) { state.Current = definition.Target; break; }
        }
        return completedNow;
    }

    private void ProcessReward(WDMissionDefinition definition, WDMissionProgress state)
    {
        int cycle = state.ProcessedRewardCount + 1;
        if (cycle > state.CompletionCount || definition.Reward == null) return;
        if (rewards.Process($"{definition.MissionId}:{cycle}", definition.Reward))
            state.ProcessedRewardCount++;
    }

    // ============================================================
    // 03. MOCK REPRESENTATIVO; TEXTOS E BALANCEAMENTO NÃO DEFINITIVOS
    // ============================================================

    public static IReadOnlyList<WDMissionDefinition> CreatePrototypeCatalog()
    {
        var items = new List<WDMissionDefinition>
        {
            Mission("first-win", "PRIMEIRO TRIUNFO", "Mock de vitória.", "Vença uma partida válida.", WDMissionCategory.Match, false, false, 1, WDMissionRewardType.Coins),
            Mission("mode-wins", "DOMÍNIO DE MODO", "Mock repetível de modo.", "Complete o objetivo do modo configurado.", WDMissionCategory.Mode, false, true, 2, WDMissionRewardType.WeaponTokens),
            Mission("territory-control", "CONTROLE TERRITORIAL", "Mock de progresso territorial.", "Registre territórios pelo resultado consolidado.", WDMissionCategory.Territory, false, false, 10),
            Mission("valid-action", "AÇÃO CONFIRMADA", "Mock de ação válida.", "Registre uma ação consolidada.", WDMissionCategory.Action, false, false, 3, WDMissionRewardType.Emeralds),
            Mission("secret-complete", "PROTOCOLO REVELADO", "Mock secreto já revelado.", "Condição mock revelada após conclusão.", WDMissionCategory.Prototype, true, false, 1, WDMissionRewardType.Other),
            Mission("catalog-sample-a", "AMOSTRA DE CATÁLOGO A", "Placeholder conhecido.", "Condição a definir.", WDMissionCategory.Prototype, false, false, 1),
            Mission("catalog-sample-b", "AMOSTRA DE CATÁLOGO B", "Placeholder conhecido.", "Condição a definir.", WDMissionCategory.Prototype, false, false, 1)
        };
        for (int i = 8; i <= 39; i++)
            items.Add(Mission($"secret-placeholder-{i:00}", $"SEGREDO MOCK {i:00}",
                "Placeholder secreto interno.", "Condição interna a definir.",
                WDMissionCategory.Prototype, true, false, 1));
        return items;
    }

    public static IReadOnlyList<WDMissionProgress> CreatePrototypeProgress() => new[]
    {
        new WDMissionProgress("first-win", 1, true, 1, 1),
        new WDMissionProgress("mode-wins", 1, true, 7, 7),
        new WDMissionProgress("territory-control", 4, true),
        new WDMissionProgress("valid-action", 1, true),
        new WDMissionProgress("secret-complete", 1, true, 1, 1)
    };

    private static WDMissionDefinition Mission(string id, string title, string description,
        string condition, WDMissionCategory category, bool secret, bool repeatable, int target,
        WDMissionRewardType? reward = null) => new(id, title, description, condition, category,
            secret, repeatable, target, reward.HasValue ? new WDMissionRewardDefinition(reward.Value) : null);
}

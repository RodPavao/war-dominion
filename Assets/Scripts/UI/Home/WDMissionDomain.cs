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
    public string ArtKey { get; }

    internal WDMissionPresentation(WDMissionDefinition definition, WDMissionProgress progress)
    {
        MissionId = definition.MissionId;
        ArtKey = definition.FutureArtKey;
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
    // 03. CATÁLOGO VISUAL CANÔNICO; REGRAS INDIVIDUAIS SERÃO DEFINIDAS DEPOIS
    // ============================================================

    public static IReadOnlyList<WDMissionDefinition> CreatePrototypeCatalog()
    {
        string[] ids =
        {
            "first-win", "mode-wins", "territory-control", "valid-action",
            "secret-complete", "catalog-sample-a", "catalog-sample-b",
            "secret-placeholder-08", "secret-placeholder-09", "secret-placeholder-10",
            "secret-placeholder-11", "secret-placeholder-12", "secret-placeholder-13",
            "secret-placeholder-14", "secret-placeholder-15", "secret-placeholder-16",
            "secret-placeholder-17", "secret-placeholder-18", "secret-placeholder-19",
            "secret-placeholder-20", "secret-placeholder-21", "secret-placeholder-22",
            "secret-placeholder-23", "secret-placeholder-24", "secret-placeholder-25",
            "secret-placeholder-26", "secret-placeholder-27", "secret-placeholder-28",
            "secret-placeholder-29", "secret-placeholder-30", "secret-placeholder-31",
            "secret-placeholder-32", "secret-placeholder-33", "secret-placeholder-34",
            "secret-placeholder-35", "secret-placeholder-36", "secret-placeholder-37",
            "secret-placeholder-38", "secret-placeholder-39"
        };
        string[] titles =
        {
            "HEARTLAND", "RIMLAND", "MÃE RÚSSIA", "AMÉRICAS", "CORTINA DE FERRO",
            "EURÁSIA", "PAN-AMERICANA", "MEDITERRÂNEO", "CERCO CONTINENTAL",
            "POTÊNCIA INSULAR", "DOMÍNIO DO PACÍFICO", "ROTA DO ATLÂNTICO",
            "CABEÇA DE PONTE", "PEARL HARBOR ATTACK", "CERCO NAVAL",
            "TRÍPLICE FRONTEIRA", "BLOQUEIO DO PARANÁ", "TRÍPLICE ALIANÇA",
            "IMPÉRIO ATENIENSE", "LIGA DO PELOPONESO", "HEGEMONIA GREGA",
            "SENHOR DAS TREVAS", "ALÉM DAS FRONTEIRAS", "DOMÍNIO DO DARK WORLD",
            "FORTALEZA", "ÚLTIMO BASTIÃO", "EFEITO DOMINÓ", "LINHA DE SUPRIMENTO",
            "TENAZA", "CONTRA-ATAQUE", "TERRA ARRASADA", "FRONTEIRA IMPENETRÁVEL",
            "DAVI VS GOLIAS", "SEM RECUAR", "GUERRA EM DUAS FRENTES",
            "PONTE ENTRE MUNDOS", "VIRA-VIRA", "GENERAL INVICTO", "CHECKMATE"
        };
        string[] artKeys =
        {
            "01-mission-heartland", "02-mission-rimland", "03-mission-mae-russia",
            "04-mission-americas", "05-mission-cortina-de-ferro", "06-mission-eurasia",
            "07-mission-pan-americana", "08-mission-mediterraneo",
            "09-mission-cerco-continental", "10-mission-potencia-insular",
            "11-mission-dominio-do-pacifico", "12-mission-rota-do-atlantico",
            "13-mission-cabeca-de-ponte", "14-mission-pearl-harbor-attack",
            "15-mission-cerco-naval", "16-mission-triplice-fronteira",
            "17-mission-bloqueio-do-parana", "18-mission-triplice-alianca",
            "19-mission-imperio-ateniense", "20-mission-liga-do-peloponeso",
            "21-mission-hegemonia-grega", "22-mission-senhor-das-trevas",
            "23-mission-alem-das-fronteiras", "24-mission-dominio-do-dark-world",
            "25-mission-fortaleza", "26-mission-ultimo-bastao", "27-mission-efeito-domino",
            "28-mission-linha-de-suprimento", "29-mission-tenaza",
            "30-mission-contra-ataque", "31-mission-terra-arrasada",
            "32-mission-fronteira-impenetravel", "33-mission-davi-vs-golias",
            "34-mission-sem-recuar", "35-mission-guerra-em-duas-frentes",
            "36-mission-ponte-entre-mundos", "37-mission-vira-vira",
            "38-mission-general-invicto", "39-mission-checkmate"
        };
        return ids.Select((id, index) => new WDMissionDefinition(id, titles[index],
            string.Empty, string.Empty, WDMissionCategory.Prototype,
            true, true, 1, futureArtKey: artKeys[index])).ToArray();
    }

    public static IReadOnlyList<WDMissionProgress> CreatePrototypeProgress() =>
        Array.Empty<WDMissionProgress>();

    private static WDMissionDefinition Mission(string id, string title, string description,
        string condition, WDMissionCategory category, bool secret, bool repeatable, int target,
        WDMissionRewardType? reward = null) => new(id, title, description, condition, category,
            secret, repeatable, target, reward.HasValue ? new WDMissionRewardDefinition(reward.Value) : null);
}

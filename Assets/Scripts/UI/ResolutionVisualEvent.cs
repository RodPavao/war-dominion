using System;
using System.Collections.Generic;
using System.Linq;

public enum ResolutionVisualEventType
{
    Attack,
    Movement,
    Reinforcement,
    Transfer,
    RegionConquest,
    Installation,
    Card,
    Other
}

public enum ResolutionVisualResult
{
    None,
    Completed,
    Cancelled,
    AttackDefended,
    TerritoryConquered,
    RegionConquered
}

public sealed class ResolutionVisualEvent
{
    // ============================================================
    // 01. CONTRATO IMUTÁVEL COMUM
    // ============================================================

    public ResolutionVisualEventType Type { get; }
    public ResolutionVisualResult Result { get; }
    public TerritorioClique.Dono Player { get; }
    public string OriginTerritoryId { get; }
    public string DestinationTerritoryId { get; }
    public int Amount { get; }
    public int OriginBefore { get; }
    public int OriginAfter { get; }
    public int DestinationBefore { get; }
    public int DestinationAfter { get; }
    public bool Conquered { get; }
    public TerritorioClique.Dono PreviousOwner { get; }
    public TerritorioClique.Dono NewOwner { get; }
    public string ContentId { get; }
    public string InstallationId { get; }
    public string RegionId { get; }
    public IReadOnlyList<string> RegionTerritoryIds { get; }

    private ResolutionVisualEvent(
        ResolutionVisualEventType type,
        ResolutionVisualResult result,
        TerritorioClique.Dono player,
        string originTerritoryId,
        string destinationTerritoryId,
        int amount,
        int originBefore,
        int originAfter,
        int destinationBefore,
        int destinationAfter,
        bool conquered,
        TerritorioClique.Dono previousOwner,
        TerritorioClique.Dono newOwner,
        string contentId,
        string regionId = null,
        IEnumerable<string> regionTerritoryIds = null,
        string installationId = null)
    {
        Type = type;
        Result = result;
        Player = player;
        OriginTerritoryId = originTerritoryId ?? string.Empty;
        DestinationTerritoryId = destinationTerritoryId ?? string.Empty;
        Amount = Math.Max(0, amount);
        OriginBefore = Math.Max(0, originBefore);
        OriginAfter = Math.Max(0, originAfter);
        DestinationBefore = Math.Max(0, destinationBefore);
        DestinationAfter = Math.Max(0, destinationAfter);
        Conquered = conquered;
        PreviousOwner = previousOwner;
        NewOwner = newOwner;
        ContentId = contentId ?? string.Empty;
        InstallationId = installationId ?? string.Empty;
        RegionId = regionId ?? string.Empty;
        RegionTerritoryIds = (regionTerritoryIds ?? Array.Empty<string>()).ToArray();
    }

    // ============================================================
    // 02. FÁBRICAS FORTEMENTE TIPADAS
    // ============================================================

    public static ResolutionVisualEvent Attack(
        TerritorioClique.Dono player,
        string originId,
        string destinationId,
        int amount,
        int originBefore,
        int originAfter,
        int destinationBefore,
        int destinationAfter,
        bool conquered,
        TerritorioClique.Dono previousOwner,
        TerritorioClique.Dono newOwner)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Attack,
            conquered
                ? ResolutionVisualResult.TerritoryConquered
                : ResolutionVisualResult.AttackDefended,
            player, originId, destinationId, amount,
            originBefore, originAfter, destinationBefore, destinationAfter,
            conquered, previousOwner, newOwner, string.Empty);
    }

    public static ResolutionVisualEvent Reinforcement(
        TerritorioClique.Dono player,
        string territoryId,
        int amount,
        int before,
        int after)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Reinforcement,
            ResolutionVisualResult.Completed,
            player, string.Empty, territoryId, amount,
            0, 0, before, after, false,
            player, player, string.Empty);
    }

    public static ResolutionVisualEvent Movement(
        TerritorioClique.Dono player,
        string originId,
        string destinationId,
        int amount,
        int originBefore,
        int originAfter,
        int destinationBefore,
        int destinationAfter,
        TerritorioClique.Dono previousOwner,
        TerritorioClique.Dono newOwner)
    {
        bool conquered = previousOwner != newOwner &&
            newOwner != TerritorioClique.Dono.Neutro;
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Movement,
            conquered ? ResolutionVisualResult.TerritoryConquered
                : ResolutionVisualResult.Completed,
            player, originId, destinationId, amount,
            originBefore, originAfter, destinationBefore, destinationAfter,
            conquered, previousOwner, newOwner, string.Empty);
    }

    public static ResolutionVisualEvent AirTransportMovement(
        TerritorioClique.Dono player, string originId, string destinationId,
        int amount, int originBefore, int originAfter, int destinationBefore,
        int destinationAfter, TerritorioClique.Dono destinationOwner,
        string installationId)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Movement, ResolutionVisualResult.Completed,
            player, originId, destinationId, amount,
            originBefore, originAfter, destinationBefore, destinationAfter,
            false, destinationOwner, destinationOwner, WDSupportWeaponIds.AirTransport,
            installationId: installationId);
    }

    public static ResolutionVisualEvent TerritoryHandoff(
        TerritorioClique.Dono player,
        TerritorioClique.Dono recipient,
        string territoryId,
        int troopsBefore,
        int troopsAfter)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Transfer,
            ResolutionVisualResult.Completed,
            player, string.Empty, territoryId, 0,
            0, 0, troopsBefore, troopsAfter,
            false, player, recipient, string.Empty);
    }

    public static ResolutionVisualEvent RegionControlGained(
        TerritorioClique.Dono player,
        string regionId,
        IEnumerable<string> territoryIds)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.RegionConquest,
            ResolutionVisualResult.RegionConquered,
            player, string.Empty, string.Empty, 0,
            0, 0, 0, 0, false,
            TerritorioClique.Dono.Neutro, player, string.Empty,
            regionId, territoryIds);
    }

    public static ResolutionVisualEvent FutureContent(
        ResolutionVisualEventType type,
        TerritorioClique.Dono player,
        string contentId)
    {
        if (type != ResolutionVisualEventType.Card &&
            type != ResolutionVisualEventType.Other)
        {
            throw new ArgumentException(
                "Conteúdo futuro deve usar Card ou Other.", nameof(type));
        }

        return new ResolutionVisualEvent(
            type, ResolutionVisualResult.None, player,
            string.Empty, string.Empty, 0, 0, 0, 0, 0,
            false, TerritorioClique.Dono.Neutro,
            TerritorioClique.Dono.Neutro, contentId);
    }

    public static ResolutionVisualEvent WeaponCard(
        TerritorioClique.Dono player, string weaponId, string status)
    {
        string content = (weaponId ?? string.Empty) + "|" + (status ?? string.Empty);
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Card, ResolutionVisualResult.Completed, player,
            string.Empty, string.Empty, 0, 0, 0, 0, 0, false,
            TerritorioClique.Dono.Neutro, TerritorioClique.Dono.Neutro, content);
    }

    public static ResolutionVisualEvent InstallationImplemented(
        TerritorioClique.Dono player, string weaponId, string territoryId,
        string installationId = null)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Installation, ResolutionVisualResult.Completed,
            player, string.Empty, territoryId, 0, 0, 0, 0, 0, false,
            player, player, weaponId, installationId: installationId);
    }

    public static ResolutionVisualEvent InstallationTransported(
        TerritorioClique.Dono player, string weaponId, string originTerritoryId,
        string destinationTerritoryId, string installationId)
    {
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Installation, ResolutionVisualResult.Completed,
            player, originTerritoryId, destinationTerritoryId, 0, 0, 0, 0, 0, false,
            player, player, weaponId, installationId: installationId);
    }

    // ============================================================
    // 03. DESCRIÇÃO PARA DIAGNÓSTICO
    // ============================================================

    public override string ToString()
    {
        string route = string.IsNullOrEmpty(OriginTerritoryId)
            ? DestinationTerritoryId
            : $"{OriginTerritoryId} -> {DestinationTerritoryId}";
        return $"{Type} | {route} | quantidade {Amount} | resultado {Result}";
    }
}

public readonly struct WDTerritoryOwnershipChange
{
    public string TerritoryId { get; }
    public TerritorioClique.Dono PreviousOwner { get; }
    public TerritorioClique.Dono NewOwner { get; }

    public WDTerritoryOwnershipChange(string territoryId,
        TerritorioClique.Dono previousOwner, TerritorioClique.Dono newOwner)
    {
        TerritoryId = territoryId ?? string.Empty;
        PreviousOwner = previousOwner;
        NewOwner = newOwner;
    }
}

public static class WDRegionConquestVisualDetector
{
    // ============================================================
    // 01. TRANSIÇÃO CANÔNICA DE CONTROLE → EVENTO VISUAL REGIONAL
    // ============================================================

    public static IReadOnlyList<ResolutionVisualEvent> Detect(
        IEnumerable<DefinicaoRegiaoMapa> regions,
        IReadOnlyDictionary<string, TerritorioClique.Dono> ownersBefore,
        IEnumerable<WDTerritoryOwnershipChange> changes)
    {
        var events = new List<ResolutionVisualEvent>();
        var owners = ownersBefore != null
            ? new Dictionary<string, TerritorioClique.Dono>(ownersBefore,
                StringComparer.Ordinal)
            : new Dictionary<string, TerritorioClique.Dono>(StringComparer.Ordinal);
        DefinicaoRegiaoMapa[] definitions = (regions ??
            Array.Empty<DefinicaoRegiaoMapa>()).Where(region =>
                region?.territorioIds != null && region.territorioIds.Count > 0).ToArray();

        foreach (WDTerritoryOwnershipChange change in changes ??
            Array.Empty<WDTerritoryOwnershipChange>())
        {
            if (string.IsNullOrEmpty(change.TerritoryId) ||
                change.PreviousOwner == change.NewOwner)
                continue;

            DefinicaoRegiaoMapa[] relevant = definitions.Where(region =>
                region.territorioIds.Contains(change.TerritoryId)).ToArray();
            bool[] controlledBefore = relevant.Select(region =>
                Controls(region, change.NewOwner, owners)).ToArray();
            owners[change.TerritoryId] = change.NewOwner;
            for (int index = 0; index < relevant.Length; index++)
            {
                DefinicaoRegiaoMapa region = relevant[index];
                bool controlledAfter = change.NewOwner != TerritorioClique.Dono.Neutro &&
                    Controls(region, change.NewOwner, owners);
                if (!controlledBefore[index] && controlledAfter)
                    events.Add(ResolutionVisualEvent.RegionControlGained(
                        change.NewOwner, region.id, region.territorioIds));
            }
        }
        return events;
    }

    public static IReadOnlyList<ResolutionVisualEvent> InterleaveWithTerritoryResults(
        IEnumerable<DefinicaoRegiaoMapa> regions,
        IReadOnlyDictionary<string, TerritorioClique.Dono> ownersBefore,
        IEnumerable<ResolutionVisualEvent> territoryEvents)
    {
        var ordered = new List<ResolutionVisualEvent>();
        var owners = ownersBefore != null
            ? new Dictionary<string, TerritorioClique.Dono>(ownersBefore,
                StringComparer.Ordinal)
            : new Dictionary<string, TerritorioClique.Dono>(StringComparer.Ordinal);

        foreach (ResolutionVisualEvent territoryEvent in territoryEvents ??
            Array.Empty<ResolutionVisualEvent>())
        {
            if (territoryEvent == null)
                continue;
            ordered.Add(territoryEvent);
            if (string.IsNullOrEmpty(territoryEvent.DestinationTerritoryId) ||
                territoryEvent.PreviousOwner == territoryEvent.NewOwner)
                continue;

            var change = new WDTerritoryOwnershipChange(
                territoryEvent.DestinationTerritoryId,
                territoryEvent.PreviousOwner,
                territoryEvent.NewOwner);
            ordered.AddRange(Detect(regions, owners, new[] { change }));
            owners[change.TerritoryId] = change.NewOwner;
        }

        return ordered;
    }

    private static bool Controls(DefinicaoRegiaoMapa region,
        TerritorioClique.Dono player,
        IReadOnlyDictionary<string, TerritorioClique.Dono> owners)
    {
        if (player == TerritorioClique.Dono.Neutro)
            return false;
        foreach (string territoryId in region.territorioIds)
            if (!owners.TryGetValue(territoryId, out TerritorioClique.Dono owner) ||
                owner != player)
                return false;
        return true;
    }
}

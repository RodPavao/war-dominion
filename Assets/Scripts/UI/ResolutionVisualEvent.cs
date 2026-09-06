using System;

public enum ResolutionVisualEventType
{
    Attack,
    Movement,
    Reinforcement,
    Transfer,
    Card,
    Other
}

public enum ResolutionVisualResult
{
    None,
    Completed,
    Cancelled,
    AttackDefended,
    TerritoryConquered
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
        string contentId)
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
        return new ResolutionVisualEvent(
            ResolutionVisualEventType.Movement,
            ResolutionVisualResult.Completed,
            player, originId, destinationId, amount,
            originBefore, originAfter, destinationBefore, destinationAfter,
            false, previousOwner, newOwner, string.Empty);
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

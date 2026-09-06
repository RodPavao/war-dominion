using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResolutionVisualStateCoordinator : MonoBehaviour,
    IResolutionVisualStateCoordinator
{
    // ============================================================
    // 01. PRIMEIRO SNAPSHOT DE CADA TERRITÓRIO
    // ============================================================

    private readonly HashSet<TerritorioClique> overriddenTerritories =
        new HashSet<TerritorioClique>();

    public void PrepareVisualState(IReadOnlyList<ResolutionVisualEvent> events)
    {
        CompleteVisualState();
        if (events == null)
            return;

        foreach (ResolutionVisualEvent visualEvent in events)
        {
            if (visualEvent == null)
                continue;

            if (visualEvent.Type == ResolutionVisualEventType.Reinforcement ||
                visualEvent.Type == ResolutionVisualEventType.Transfer)
            {
                ApplyInitialIfFirst(
                    visualEvent.DestinationTerritoryId,
                    visualEvent.DestinationBefore,
                    visualEvent.PreviousOwner);
                continue;
            }

            if (visualEvent.Type == ResolutionVisualEventType.Attack ||
                visualEvent.Type == ResolutionVisualEventType.Movement)
            {
                ApplyInitialIfFirst(
                    visualEvent.OriginTerritoryId,
                    visualEvent.OriginBefore,
                    visualEvent.Player);
                ApplyInitialIfFirst(
                    visualEvent.DestinationTerritoryId,
                    visualEvent.DestinationBefore,
                    visualEvent.PreviousOwner);
            }
        }
    }

    // ============================================================
    // 02. RESTAURAÇÃO DO ESTADO AUTORITATIVO
    // ============================================================

    public void CompleteVisualState()
    {
        foreach (TerritorioClique territory in overriddenTerritories)
            if (territory != null)
                territory.RestaurarEstadoVisualLogico();
        overriddenTerritories.Clear();
    }

    private void ApplyInitialIfFirst(
        string territoryId,
        int troops,
        TerritorioClique.Dono owner)
    {
        if (!ResolutionVisualMapUtility.TryGetTerritory(
                territoryId, out TerritorioClique territory))
        {
            Debug.LogWarning(
                $"[ResolutionVisual] Snapshot ignorado; território não encontrado: " +
                $"'{territoryId}'.");
            return;
        }

        if (overriddenTerritories.Contains(territory))
        {
            return;
        }

        overriddenTerritories.Add(territory);
        territory.AplicarEstadoVisualResolucao(troops, owner);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WDWeaponHudSelectionStage
{
    None,
    Origin,
    TargetTerritory,
    TargetPlayer,
    TargetWeapon,
    Parameters,
    RadarConfirmation
}

public sealed class WDWeaponHudCardState
{
    public string WeaponId { get; }
    public string DisplayName { get; }
    public int Level { get; }
    public int EnergyCost { get; }
    public int CooldownRemaining { get; }
    public WDWeaponAvailabilityReason AvailabilityReason { get; }
    public bool Selected { get; }
    public bool Programmed { get; }

    public WDWeaponHudCardState(string weaponId, string displayName, int level,
        int energyCost, int cooldownRemaining,
        WDWeaponAvailabilityReason availabilityReason, bool selected, bool programmed)
    {
        WeaponId = weaponId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Level = level;
        EnergyCost = energyCost;
        CooldownRemaining = cooldownRemaining;
        AvailabilityReason = availabilityReason;
        Selected = selected;
        Programmed = programmed;
    }
}

public sealed class WDWeaponHudInstallationState
{
    public string InstallationId { get; }
    public string WeaponId { get; }
    public string DisplayName { get; }
    public string TerritoryId { get; }
    public string OwnerPlayerId { get; }
    public int Level { get; }
    public int Charges { get; }
    public int Duration { get; }
    public int Barrier { get; }
    public bool IsAbandoned { get; }
    public bool IsDisabled { get; }
    public bool CanOperate { get; }

    public WDWeaponHudInstallationState(WDWeaponInstallationState value,
        string displayName, bool canOperate)
    {
        InstallationId = value.InstallationId;
        WeaponId = value.WeaponId;
        DisplayName = displayName ?? value.WeaponId;
        TerritoryId = value.TerritoryId;
        OwnerPlayerId = value.OwnerPlayerId;
        Level = value.Level;
        Charges = value.ChargesRemaining;
        Duration = value.DurationRemaining;
        Barrier = value.BarrierRemaining;
        IsAbandoned = value.IsAbandoned;
        IsDisabled = value.IsTemporarilyDisabled;
        CanOperate = canOperate;
    }
}

[DisallowMultipleComponent]
public sealed class WDWeaponMatchUIController : MonoBehaviour
{
    private GameManager gameManager;
    private WDArmoryCatalogConfig catalog;
    private WDWeaponProgrammingOption selectedOption;
    private WDWeaponIntentKind intentKind;
    private string originId = string.Empty;
    private string targetId = string.Empty;
    private string targetPlayerId = string.Empty;
    private string installationId = string.Empty;
    private int transportTroops = 1;
    private readonly List<WDWeaponActionParameter> parameters = new();

    public WDWeaponHudSelectionStage Stage { get; private set; }
    public string Feedback { get; private set; } = string.Empty;
    public string SelectedWeaponId => selectedOption?.WeaponId ?? string.Empty;
    public string CurrentOriginId => originId;
    public int TransportTroops => transportTroops;
    public int MaximumTransportTroops
    {
        get
        {
            WDGameWeaponTerritoryView origin = gameManager == null
                ? null : new WDGameWeaponContext(gameManager).FindTerritory(originId);
            int available = Math.Max(0, (origin?.Troops ?? 0) - 1);
            int capacity = WDSupportWeaponTables.AirTransportCapacity(
                selectedOption?.Availability.Level ?? 1);
            return Math.Min(available, capacity);
        }
    }
    public string LocalPlayerId => gameManager?.ObterIdJogadorLocalParaArmas() ?? string.Empty;
    public bool IsSelecting => Stage != WDWeaponHudSelectionStage.None;
    public bool IsSelectingTerritory =>
        Stage == WDWeaponHudSelectionStage.Origin ||
        Stage == WDWeaponHudSelectionStage.TargetTerritory;

    public void Configure(GameManager manager)
    {
        gameManager = manager;
        catalog = Resources.Load<WDArmoryCatalogConfig>("UI/Home/WDArmoryCatalogConfig");
    }

    // ============================================================
    // 01. SNAPSHOT VISUAL, SEM CÓPIA AUTORITATIVA DE GAMEPLAY
    // ============================================================

    public int CurrentEnergy => gameManager?.WeaponRuntime?.FindPlayer(LocalPlayerId)?.Energy ?? 0;

    public IReadOnlyList<WDWeaponHudCardState> GetCards()
    {
        if (gameManager == null || catalog == null)
            return Array.Empty<WDWeaponHudCardState>();
        IReadOnlyList<string> ids = GetLocalWeaponIds();
        IReadOnlyList<WDWeaponAction> programmed =
            gameManager.ObterAcoesArmasProgramadas(LocalPlayerId);
        return ids.Take(8).Select(id =>
        {
            WDWeaponDefinition definition = catalog.FindWeapon(id);
            WDWeaponProgrammingOption option = gameManager.WeaponPreparation?.GetOption(
                LocalPlayerId, id);
            WDWeaponAvailability availability = option?.Availability ?? default;
            bool isProgrammed = programmed.Any(action => action.WeaponId == id);
            return new WDWeaponHudCardState(id, definition?.DisplayName ?? id,
                availability.Level, definition?.EnergyCost ?? 0,
                isProgrammed ? definition?.CooldownRounds ?? 0 : availability.CooldownRemaining,
                availability.Reason, SelectedWeaponId == id, isProgrammed);
        }).ToArray();
    }

    public IReadOnlyList<WDWeaponHudCardState> GetSupportCards()
    {
        if (gameManager == null || catalog == null)
            return Array.Empty<WDWeaponHudCardState>();
        WDMatchParticipant local = gameManager.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == LocalPlayerId);
        IEnumerable<string> ids = local?.WeaponLoadout?.SupportSlots
            .Select(item => item.WeaponId) ?? Enumerable.Empty<string>();
        IReadOnlyList<WDWeaponAction> programmed =
            gameManager.ObterAcoesArmasProgramadas(LocalPlayerId);
        return ids.Take(2).Select(id =>
        {
            WDWeaponDefinition definition = catalog.FindWeapon(id);
            WDWeaponProgrammingOption option = gameManager.WeaponPreparation?.GetOption(
                LocalPlayerId, id);
            WDWeaponAvailability availability = option?.Availability ?? default;
            bool isProgrammed = programmed.Any(action => action.WeaponId == id);
            return new WDWeaponHudCardState(id, definition?.DisplayName ?? id,
                availability.Level, definition?.EnergyCost ?? 0,
                isProgrammed ? definition?.CooldownRounds ?? 0 : availability.CooldownRemaining,
                availability.Reason, SelectedWeaponId == id, isProgrammed);
        }).ToArray();
    }

    public IReadOnlyList<WDWeaponHudInstallationState> GetVisibleInstallations()
    {
        if (gameManager?.WeaponInstallationRegistry == null)
            return Array.Empty<WDWeaponHudInstallationState>();
        string local = LocalPlayerId;
        return gameManager.WeaponInstallationRegistry.GetAll()
            .Where(item => IsVisibleTo(item, local))
            .Select(item => new WDWeaponHudInstallationState(item,
                catalog?.FindWeapon(item.WeaponId)?.DisplayName,
                item.OwnerPlayerId == local && !item.IsTemporarilyDisabled &&
                !item.IsDestroyed && item.ChargesRemaining > 0 &&
                IsOperableInstallation(item.WeaponId)))
            .OrderBy(item => item.TerritoryId, StringComparer.Ordinal)
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<WDMatchParticipant> GetOpponents() =>
        gameManager?.MatchSetupAtual?.Participants
            .Where(item => item.PlayerId != LocalPlayerId &&
                AreOpponents(LocalPlayerId, item.PlayerId)).ToArray()
        ?? Array.Empty<WDMatchParticipant>();

    public IReadOnlyList<WDInfiltratorWeaponTarget> GetInfiltratorWeapons(
        string opponentPlayerId) => gameManager?.ObterArmasDoInfiltrado(
            LocalPlayerId, opponentPlayerId) ?? Array.Empty<WDInfiltratorWeaponTarget>();

    public IReadOnlyList<WDWeaponAction> GetProgrammedActions() =>
        gameManager?.ObterAcoesArmasProgramadas(LocalPlayerId) ??
        Array.Empty<WDWeaponAction>();

    public bool IsIrreversible(string actionId) =>
        gameManager?.WeaponPreparation?.IsIrreversible(actionId) == true;

    public string GetResolutionStatus(string actionId)
    {
        WDGlobalResolutionEntry entry = gameManager?.UltimaResolucaoGlobal?.Entries
            .FirstOrDefault(item => item.Kind == WDGlobalResolutionEntryKind.WeaponAction &&
                item.StableId == actionId);
        return entry?.WeaponStatus?.ToString().ToUpperInvariant() ?? "PENDENTE";
    }

    public string DescribeDeck(TerritorioClique.Dono owner)
    {
        WDMatchParticipant participant = gameManager?.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.SlotIndex == Math.Max(0, (int)owner - 1));
        return DescribeDeck(participant?.PlayerId);
    }

    public string DescribeDeck(string playerId)
    {
        WDMatchParticipant participant = gameManager?.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == playerId);
        IEnumerable<string> ids = participant?.WeaponLoadout?.MainSlots
            .Select(item => item.WeaponId) ?? Array.Empty<string>();
        string[] labels = ids.Select(id => catalog?.FindWeapon(id)?.DisplayName ?? id)
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return labels.Length == 0 ? "DECK NÃO DISPONÍVEL" : string.Join("  |  ", labels);
    }

    public IReadOnlyList<string> GetDeckCardLabels(string playerId)
    {
        WDMatchParticipant participant = gameManager?.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == playerId);
        return participant?.WeaponLoadout?.MainSlots
            .Select(item => catalog?.FindWeapon(item.WeaponId)?.DisplayName ?? item.WeaponId)
            .Take(8).ToArray() ?? Array.Empty<string>();
    }

    public int GetPlayerEnergy(string playerId) =>
        gameManager?.WeaponRuntime?.FindPlayer(playerId)?.Energy ?? 0;

    public string PlayerIdForOwner(TerritorioClique.Dono owner) =>
        gameManager?.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.SlotIndex == Math.Max(0, (int)owner - 1))
            ?.PlayerId ?? string.Empty;

    // ============================================================
    // 02. FLUXO DE SELEÇÃO TOUCH SOBRE O MAPA REAL
    // ============================================================

    public bool SelectWeapon(string weaponId)
    {
        CancelSelection();
        selectedOption = gameManager?.SelecionarArma(LocalPlayerId, weaponId);
        if (selectedOption?.Requirement == null || !selectedOption.Availability.IsAvailable)
        {
            Feedback = AvailabilityText(selectedOption?.Availability ?? default);
            selectedOption = null;
            return false;
        }
        intentKind = WDWeaponIntentKind.Standard;
        installationId = Requires(selectedOption.Requirement,
            WDWeaponSelectionFields.Installation)
            ? $"installation:{gameManager.RodadaAtual}:{LocalPlayerId}:{weaponId}:" +
              Guid.NewGuid().ToString("N")
            : string.Empty;
        return AdvanceStageOrProgram();
    }

    public bool SelectInstallation(string id)
    {
        WDWeaponInstallationState installation = gameManager?.WeaponInstallationRegistry?.Find(id);
        if (installation == null || installation.OwnerPlayerId != LocalPlayerId ||
            installation.IsDestroyed || installation.IsTemporarilyDisabled)
            return false;
        CancelSelection();
        selectedOption = gameManager.WeaponPreparation?.GetOption(LocalPlayerId,
            installation.WeaponId);
        installationId = installation.InstallationId;
        originId = installation.TerritoryId;
        intentKind = installation.WeaponId == WDSpecialWeaponIds.Drone
            ? WDWeaponIntentKind.DroneShot
            : WDWeaponIntentKind.RocketLaunch;
        if (installation.WeaponId == WDTacticalWeaponIds.Radar)
            intentKind = WDWeaponIntentKind.Standard;
        selectedOption = new WDWeaponProgrammingOption(installation.WeaponId,
            selectedOption?.Availability ?? default,
            gameManager.WeaponPreparation?.GetRequirement(installation.WeaponId,
                intentKind));
        Stage = WDWeaponHudSelectionStage.TargetTerritory;
        Feedback = $"Selecione o alvo de {catalog?.FindWeapon(installation.WeaponId)?.DisplayName}.";
        return true;
    }

    public bool SelectProgrammedRadar(string actionId)
    {
        WDWeaponAction action = gameManager?.ObterAcoesArmasProgramadas(LocalPlayerId)
            .FirstOrDefault(item => item.ActionId == actionId &&
                item.WeaponId == WDTacticalWeaponIds.Radar);
        if (action == null)
            return false;
        CancelSelection();
        selectedOption = gameManager.WeaponPreparation.GetOption(
            LocalPlayerId, WDTacticalWeaponIds.Radar);
        installationId = action.InstallationId;
        originId = action.TargetTerritoryId;
        Stage = WDWeaponHudSelectionStage.TargetTerritory;
        Feedback = "Selecione o território a investigar.";
        return true;
    }

    public bool HandleTerritory(string territoryId)
    {
        if (!IsSelecting || string.IsNullOrWhiteSpace(territoryId))
            return false;
        if (Stage == WDWeaponHudSelectionStage.Origin)
        {
            originId = territoryId;
            return AdvanceStageOrProgram();
        }
        if (Stage != WDWeaponHudSelectionStage.TargetTerritory)
            return false;
        targetId = territoryId;
        if (SelectedWeaponId == WDTacticalWeaponIds.Radar &&
            !string.IsNullOrEmpty(installationId) &&
            gameManager.WeaponInstallationRegistry.Find(installationId) != null)
        {
            Stage = WDWeaponHudSelectionStage.RadarConfirmation;
            Feedback = "Confirmar varredura irreversível do Radar?";
            return true;
        }
        return AdvanceStageOrProgram();
    }

    public bool SelectTargetPlayer(string playerId)
    {
        if (Stage != WDWeaponHudSelectionStage.TargetPlayer)
            return false;
        targetPlayerId = playerId ?? string.Empty;
        if (SelectedWeaponId == WDSpecialWeaponIds.Infiltrator)
        {
            Stage = WDWeaponHudSelectionStage.TargetWeapon;
            Feedback = "Selecione uma arma do deck adversário.";
            return true;
        }
        return AdvanceStageOrProgram();
    }

    public bool SelectInfiltratorWeapon(string weaponId)
    {
        if (Stage != WDWeaponHudSelectionStage.TargetWeapon ||
            GetInfiltratorWeapons(targetPlayerId).All(item => item.WeaponId != weaponId))
            return false;
        parameters.Clear();
        parameters.Add(new WDWeaponActionParameter("targetWeaponId", weaponId));
        return ProgramCurrent();
    }

    public void SetTransportParameters(int troops, string transportedInstallationId)
    {
        parameters.Clear();
        parameters.Add(new WDWeaponActionParameter("troops", Math.Max(1, troops).ToString()));
        installationId = transportedInstallationId ?? string.Empty;
    }

    public bool ConfirmTransport(int troops, string transportedInstallationId)
    {
        if (Stage != WDWeaponHudSelectionStage.Parameters ||
            troops < 1 || troops > MaximumTransportTroops)
            return false;
        SetTransportParameters(troops, transportedInstallationId);
        return AdvanceStageOrProgram();
    }

    public void ChangeTransportTroops(int delta)
    {
        transportTroops = Math.Clamp(transportTroops + delta, 1,
            Math.Max(1, MaximumTransportTroops));
    }

    public WDRadarUseResult ConfirmRadarScan()
    {
        if (Stage != WDWeaponHudSelectionStage.RadarConfirmation)
            return new WDRadarUseResult(WDRadarUseStatus.Invalid);
        string commandId = $"radar:{gameManager.RodadaAtual}:{LocalPlayerId}:" +
            Guid.NewGuid().ToString("N");
        WDRadarUseResult result = gameManager.UsarRadarDurantePreparacao(
            commandId, LocalPlayerId, installationId, targetId);
        Feedback = result.Status switch
        {
            WDRadarUseStatus.Used => result.Reveal.HasFlakCoverage
                ? "TEM COBERTURA DE FLAK" : "NÃO TEM COBERTURA DE FLAK",
            WDRadarUseStatus.BlockedBySniper => "RADAR BLOQUEADO",
            _ => "VARREDURA INVÁLIDA"
        };
        CancelSelection(false);
        return result;
    }

    public void CancelSelection(bool clearFeedback = true)
    {
        gameManager?.WeaponPreparation?.ClearSelection(LocalPlayerId);
        selectedOption = null;
        intentKind = WDWeaponIntentKind.Standard;
        originId = targetId = targetPlayerId = installationId = string.Empty;
        transportTroops = 1;
        parameters.Clear();
        Stage = WDWeaponHudSelectionStage.None;
        if (clearFeedback)
            Feedback = string.Empty;
    }

    public WDWeaponProgrammingResult CancelAction(string actionId)
    {
        WDWeaponProgrammingResult result = gameManager.CancelarAcaoArma(LocalPlayerId, actionId);
        Feedback = result.Succeeded ? "Ação e dependências canceladas." :
            result.Reason == WDWeaponProgrammingReason.IrreversibleAction
                ? "Esta ação já produziu informação irreversível."
                : "Não foi possível cancelar a ação.";
        return result;
    }

    // ============================================================
    // 03. PROGRAMAÇÃO POR CONTRATO; EFEITOS CONTINUAM NO COORDENADOR
    // ============================================================

    private bool AdvanceStageOrProgram()
    {
        WDWeaponSelectionRequirement requirement = selectedOption?.Requirement;
        if (requirement == null)
            return false;
        if (Requires(requirement, WDWeaponSelectionFields.OriginTerritory) &&
            string.IsNullOrEmpty(originId))
            return SetStage(WDWeaponHudSelectionStage.Origin, "Selecione a origem.");
        if (Requires(requirement, WDWeaponSelectionFields.TargetTerritory) &&
            string.IsNullOrEmpty(targetId))
            return SetStage(WDWeaponHudSelectionStage.TargetTerritory,
                "Selecione o território alvo.");
        if (Requires(requirement, WDWeaponSelectionFields.TargetPlayer) &&
            string.IsNullOrEmpty(targetPlayerId))
            return SetStage(WDWeaponHudSelectionStage.TargetPlayer,
                "Selecione um adversário.");
        if (Requires(requirement, WDWeaponSelectionFields.Parameters) &&
            parameters.Count == 0 && SelectedWeaponId == WDSupportWeaponIds.AirTransport)
            return SetStage(WDWeaponHudSelectionStage.Parameters,
                "Defina tropas e, opcionalmente, uma instalação.");
        return ProgramCurrent();
    }

    private bool ProgramCurrent()
    {
        WDWeaponProgrammingResult result = gameManager.ProgramarArma(
            new WDWeaponProgrammingRequest(LocalPlayerId, SelectedWeaponId, intentKind,
                originId, targetId, targetPlayerId, installationId, parameters));
        Feedback = result.Succeeded ? "Ação programada." :
            $"Ação inválida: {result.Reason}.";
        if (result.Succeeded)
            CancelSelection(false);
        return result.Succeeded;
    }

    private bool SetStage(WDWeaponHudSelectionStage value, string feedback)
    {
        Stage = value;
        Feedback = feedback;
        return true;
    }

    private IReadOnlyList<string> GetLocalWeaponIds()
    {
        if (gameManager.UsaRandomCards)
            return gameManager.CartasRandomJogadorLocal;
        WDMatchParticipant local = gameManager.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == LocalPlayerId);
        return local?.WeaponLoadout?.MainSlots.Select(item => item.WeaponId).ToArray()
            ?? Array.Empty<string>();
    }

    private bool IsVisibleTo(WDWeaponInstallationState item, string viewer)
    {
        if (item == null || item.IsDestroyed)
            return false;
        return IsInstallationVisibleTo(item.WeaponId, item.OwnerPlayerId, viewer,
            AreAllied(item.OwnerPlayerId, viewer));
    }

    public static bool IsInstallationVisibleTo(
        string weaponId, string ownerPlayerId, string viewerPlayerId, bool areAllied)
    {
        if (weaponId != WDSupportWeaponIds.Minefield &&
            weaponId != WDSupportWeaponIds.FlakTower)
            return true;
        return !string.IsNullOrWhiteSpace(viewerPlayerId) &&
            (ownerPlayerId == viewerPlayerId || areAllied);
    }

    private bool AreOpponents(string first, string second)
    {
        WDMatchParticipant a = gameManager.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == first);
        WDMatchParticipant b = gameManager.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == second);
        return a != null && b != null && a.TeamIndex != b.TeamIndex;
    }

    private bool AreAllied(string first, string second)
    {
        WDMatchParticipant a = gameManager.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == first);
        WDMatchParticipant b = gameManager.MatchSetupAtual?.Participants
            .FirstOrDefault(item => item.PlayerId == second);
        return a != null && b != null && first != second && a.TeamIndex == b.TeamIndex;
    }

    private static bool IsOperableInstallation(string weaponId) =>
        weaponId == WDSpecialWeaponIds.Drone ||
        weaponId == WDStrategicWeaponIds.Rockets ||
        weaponId == WDTacticalWeaponIds.Radar;

    private static bool Requires(WDWeaponSelectionRequirement requirement,
        WDWeaponSelectionFields field) => (requirement.Fields & field) != 0;

    private static string AvailabilityText(WDWeaponAvailability availability) =>
        availability.Reason switch
        {
            WDWeaponAvailabilityReason.InsufficientEnergy => "Energy insuficiente.",
            WDWeaponAvailabilityReason.Cooldown =>
                $"Cooldown: {availability.CooldownRemaining}.",
            WDWeaponAvailabilityReason.InitialRoundLock =>
                $"Disponível em {availability.CooldownRemaining} round(s).",
            WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation =>
                "Carta já programada nesta Preparation.",
            _ => $"Arma indisponível: {availability.Reason}."
        };
}

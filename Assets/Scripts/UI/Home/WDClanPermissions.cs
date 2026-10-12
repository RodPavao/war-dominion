public enum WDClanPermission
{
    EditClan,
    EditRequirements,
    EditMessageOfDay,
    ReviewJoinRequests,
    PromoteMember,
    PromoteToCommander,
    DemoteOfficer,
    ManageCommander,
    RemoveMember,
    RequestRemoval,
    ApproveRemoval,
    TransferLeadership,
    DeleteClan,
    ExchangeWeaponTokens
}

// ============================================================
// 01. MATRIZ CANÔNICA DE CAPACIDADES POR CARGO
// ============================================================
public static class WDClanPermissions
{
    public static bool Can(WDClanRole role, WDClanPermission permission) => permission switch
    {
        WDClanPermission.EditClan => IsGeneralOrCommander(role),
        WDClanPermission.EditRequirements => IsGeneralOrCommander(role),
        WDClanPermission.EditMessageOfDay => role != WDClanRole.Member,
        WDClanPermission.ReviewJoinRequests => IsGeneralOrCommander(role),
        WDClanPermission.PromoteMember => IsGeneralOrCommander(role),
        WDClanPermission.PromoteToCommander => role == WDClanRole.General,
        WDClanPermission.DemoteOfficer => IsGeneralOrCommander(role),
        WDClanPermission.ManageCommander => role == WDClanRole.General,
        WDClanPermission.RemoveMember => IsGeneralOrCommander(role),
        WDClanPermission.RequestRemoval => role != WDClanRole.Member,
        WDClanPermission.ApproveRemoval => IsGeneralOrCommander(role),
        WDClanPermission.TransferLeadership => role == WDClanRole.General,
        WDClanPermission.DeleteClan => role == WDClanRole.General,
        WDClanPermission.ExchangeWeaponTokens => true,
        _ => false
    };

    // ============================================================
    // 02. RESTRIÇÕES ATOR × ALVO
    // ============================================================
    public static bool CanManageRole(WDClanRole actor, WDClanRole target)
    {
        if (target == WDClanRole.General)
            return false;
        if (target == WDClanRole.Commander)
            return actor == WDClanRole.General;
        return IsGeneralOrCommander(actor);
    }

    public static bool CanRequestRemovalOf(WDClanRole actor, WDClanRole target) =>
        Can(actor, WDClanPermission.RequestRemoval) &&
        target != WDClanRole.General &&
        target != WDClanRole.Commander;

    public static bool CanApproveRemovalOf(WDClanRole actor, WDClanRole target) =>
        Can(actor, WDClanPermission.ApproveRemoval) && CanManageRole(actor, target);

    private static bool IsGeneralOrCommander(WDClanRole role) =>
        role == WDClanRole.General || role == WDClanRole.Commander;
}

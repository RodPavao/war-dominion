using System;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// 01. TIPOS, RESULTADOS E CONTRATOS
// ============================================================
public enum WDClanRole { Member, Officer, Commander, General }
public enum WDClanAdmission { Open, Approval }
public enum WDClanResult
{
    Success, NotFound, Invalid, Forbidden, AlreadyMember, AlreadyPending,
    Locked, InsufficientTokens, InsufficientEmeralds, CommanderLimit,
    RequiresSuccessor, Unavailable
}
public enum WDClanAvailability { Available, Loading, Empty, Error, Offline }

public sealed class WDClanCreationPolicy
{
    public const int CanonicalEmeraldCost = 10;
    public int EmeraldCost { get; }
    public WDClanCreationPolicy(int emeraldCost = CanonicalEmeraldCost) =>
        EmeraldCost = Math.Max(0, emeraldCost);
}

public interface IWDClanEconomy
{
    bool TrySpendEmeralds(string playerId, int amount);
    void RefundEmeralds(string playerId, int amount);
}

public interface IWDClanArmoryProfiles
{
    WDArmoryProfile Load(string playerId);
    void Save(string playerId, WDArmoryProfile profile);
}

public interface IWDClanRepository
{
    event Action Changed;
    WDClanCreationPolicy CreationPolicy { get; }
    WDClanAvailability Availability { get; }
    IReadOnlyList<WDClanData> Clans { get; }
    IReadOnlyList<WDWeaponDefinition> Weapons { get; }
    string LocalPlayerId { get; }
    WDClanData LocalClan { get; }
    WDClanData Find(string clanId);
    WDClanData ClanOf(string playerId);
    IReadOnlyList<WDClanData> Search(string query);
}

// ============================================================
// 02. ENTIDADES DO DOMÍNIO
// ============================================================
public sealed class WDClanMember
{
    public string PlayerId { get; }
    public WDClanRole Role { get; internal set; }
    public DateTimeOffset? CommanderSinceUtc { get; internal set; }

    public WDClanMember(string playerId, WDClanRole role,
        DateTimeOffset? commanderSinceUtc = null)
    {
        PlayerId = playerId;
        SetRole(role, commanderSinceUtc);
    }

    internal void SetRole(WDClanRole role, DateTimeOffset? commanderSinceUtc = null)
    {
        Role = role;
        CommanderSinceUtc = role == WDClanRole.Commander ? commanderSinceUtc : null;
    }
}

public sealed class WDClanJoinRequest
{
    public string Id { get; }
    public string ClanId { get; }
    public string PlayerId { get; }
    public DateTimeOffset CreatedAt { get; }
    public WDClanJoinRequest(string clanId, string playerId, DateTimeOffset at)
    { Id = Guid.NewGuid().ToString("N"); ClanId = clanId; PlayerId = playerId; CreatedAt = at; }
}

public sealed class WDClanKickRequest
{
    public string Id { get; }
    public string ClanId { get; }
    public string OfficerId { get; }
    public string TargetPlayerId { get; }
    public DateTimeOffset CreatedAt { get; }
    public WDClanKickRequest(string clanId, string officerId, string targetId, DateTimeOffset at)
    { Id = Guid.NewGuid().ToString("N"); ClanId = clanId; OfficerId = officerId;
      TargetPlayerId = targetId; CreatedAt = at; }
}

public sealed class WDClanTokenRequest
{
    public string Id { get; }
    public string ClanId { get; }
    public string PlayerId { get; }
    public string WeaponId { get; }
    public int Quantity { get; }
    public bool Fulfilled { get; internal set; }
    public WDClanTokenRequest(string clanId, string playerId, string weaponId, int quantity)
    { Id = Guid.NewGuid().ToString("N"); ClanId = clanId; PlayerId = playerId;
      WeaponId = weaponId; Quantity = quantity; }
}

public sealed class WDClanLogEntry
{
    public DateTimeOffset At { get; }
    public string Text { get; }
    public WDClanLogEntry(DateTimeOffset at, string text) { At = at; Text = text; }
}

public sealed class WDClanData
{
    private readonly List<WDClanMember> members = new();
    private readonly List<WDClanJoinRequest> joinRequests = new();
    private readonly List<WDClanKickRequest> kickRequests = new();
    private readonly List<WDClanTokenRequest> tokenRequests = new();
    private readonly List<WDClanLogEntry> log = new();

    public string Id { get; }
    public string Name { get; internal set; }
    public string Tag { get; internal set; }
    public string Description { get; internal set; }
    public string MessageOfDay { get; internal set; }
    public string EmblemId { get; internal set; }
    public WDClanAdmission Admission { get; internal set; }
    public IReadOnlyList<WDClanMember> Members => members;
    public IReadOnlyList<WDClanJoinRequest> JoinRequests => joinRequests;
    public IReadOnlyList<WDClanKickRequest> KickRequests => kickRequests;
    public IReadOnlyList<WDClanTokenRequest> TokenRequests => tokenRequests;
    public IReadOnlyList<WDClanLogEntry> Log => log;
    public string GeneralId => members.FirstOrDefault(item =>
        item.Role == WDClanRole.General)?.PlayerId;
    public IReadOnlyList<WDClanMember> Commanders => members.Where(item =>
        item.Role == WDClanRole.Commander).ToArray();

    public WDClanData(string id, string name, string tag, string description,
        WDClanAdmission admission, string emblemId = "clan_default")
    { Id = id; Name = name; Tag = tag; Description = description;
      Admission = admission; EmblemId = emblemId; }

    public WDClanMember Member(string playerId) => members.FirstOrDefault(item =>
        item.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase));
    internal void AddMember(string playerId, WDClanRole role,
        DateTimeOffset? commanderSinceUtc = null) =>
        members.Add(new WDClanMember(playerId, role, commanderSinceUtc));
    internal void RemoveMember(string playerId) => members.RemoveAll(item =>
        item.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase));
    internal void ClearMembers() => members.Clear();
    internal void AddJoin(WDClanJoinRequest value) => joinRequests.Add(value);
    internal void RemoveJoin(string id) => joinRequests.RemoveAll(item => item.Id == id);
    internal void AddKick(WDClanKickRequest value) => kickRequests.Add(value);
    internal void RemoveKick(string id) => kickRequests.RemoveAll(item => item.Id == id);
    internal void AddToken(WDClanTokenRequest value) => tokenRequests.Add(value);
    internal void ClearPrivateState()
    { joinRequests.Clear(); kickRequests.Clear(); tokenRequests.Clear(); }
    internal void AddLog(DateTimeOffset at, string text) =>
        log.Add(new WDClanLogEntry(at, text));
}

// ============================================================
// 03. AUTORIDADE LOCAL SUBSTITUÍVEL POR BACKEND
// ============================================================
public sealed class WDClanRepository : IWDClanRepository
{
    public const int MaxCommanders = 2;

    private readonly WDProfileRepository profiles;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly IWDClanArmoryProfiles armory;
    private readonly IWDClanEconomy economy;
    private readonly Func<DateTimeOffset> clock;
    private readonly List<WDClanData> clans = new();
    private readonly Dictionary<string, string> membership =
        new(StringComparer.OrdinalIgnoreCase);

    public event Action Changed;
    public WDClanCreationPolicy CreationPolicy { get; }
    public WDClanAvailability Availability { get; private set; } =
        WDClanAvailability.Available;
    public IReadOnlyList<WDClanData> Clans => clans;
    public IReadOnlyList<WDWeaponDefinition> Weapons =>
        catalog?.Weapons ?? Array.Empty<WDWeaponDefinition>();
    public string LocalPlayerId => profiles.LocalProfile.PlayerId;
    public WDClanData LocalClan => ClanOf(LocalPlayerId);

    public WDClanRepository(WDProfileRepository profiles, WDArmoryCatalogConfig catalog,
        IWDClanArmoryProfiles armory, Func<DateTimeOffset> clock = null,
        WDClanCreationPolicy creationPolicy = null, bool seedMocks = true,
        IWDClanEconomy economy = null)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.catalog = catalog;
        this.armory = armory;
        this.economy = economy ?? armory as IWDClanEconomy;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        CreationPolicy = creationPolicy ?? new WDClanCreationPolicy();
        if (seedMocks) SeedMocks();
        ProjectMembership();
    }

    public WDClanData Find(string clanId) => clans.FirstOrDefault(item => item.Id == clanId);
    public WDClanData ClanOf(string playerId) =>
        membership.TryGetValue(playerId ?? string.Empty, out string id) ? Find(id) : null;
    public IReadOnlyList<WDClanData> Search(string query) =>
        Availability != WDClanAvailability.Available ? Array.Empty<WDClanData>() :
        clans.Where(item => string.IsNullOrWhiteSpace(query) ||
            item.Name.IndexOf(WDUIContentContracts.NormalizeSearch(query),
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            item.Tag.IndexOf(WDUIContentContracts.NormalizeSearch(query),
                StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
    public void SetAvailability(WDClanAvailability value)
    { Availability = value; Changed?.Invoke(); }

    // ============================================================
    // 04. CRIAÇÃO, ADMISSÃO E DADOS DO CLÃ
    // ============================================================
    public WDClanResult Create(string playerId, string name, string tag, string description,
        WDClanAdmission admission, out WDClanData created)
    {
        created = null;
        if (profiles.Find(playerId) == null || !WDUIContentContracts.IsValidClanName(name) ||
            !WDUIContentContracts.IsValidClanTag(tag) ||
            !WDUIContentContracts.IsValidClanDescription(description))
            return WDClanResult.Invalid;
        if (ClanOf(playerId) != null) return WDClanResult.AlreadyMember;
        if (clans.Any(item => item.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            item.Tag.Equals(tag.Trim(), StringComparison.OrdinalIgnoreCase)))
            return WDClanResult.Invalid;
        if (CreationPolicy.EmeraldCost > 0 && economy == null)
            return WDClanResult.Unavailable;
        if (CreationPolicy.EmeraldCost > 0 &&
            !economy.TrySpendEmeralds(playerId, CreationPolicy.EmeraldCost))
            return WDClanResult.InsufficientEmeralds;

        try
        {
            created = new WDClanData($"clan-{Guid.NewGuid():N}", name.Trim(), tag.Trim(),
                description?.Trim() ?? string.Empty, admission);
            clans.Add(created);
            AddMember(created, playerId, WDClanRole.General);
            Log(created, $"Clã criado por {playerId}.");
            Notify();
            return WDClanResult.Success;
        }
        catch
        {
            if (CreationPolicy.EmeraldCost > 0)
                economy.RefundEmeralds(playerId, CreationPolicy.EmeraldCost);
            if (created != null) clans.Remove(created);
            membership.Remove(playerId);
            ProjectMembership();
            created = null;
            throw;
        }
    }

    public WDClanResult Join(string clanId, string playerId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null || profiles.Find(playerId) == null) return WDClanResult.NotFound;
        if (ClanOf(playerId) != null) return WDClanResult.AlreadyMember;
        if (clan.Admission == WDClanAdmission.Approval)
        {
            if (clan.JoinRequests.Any(item => item.PlayerId == playerId))
                return WDClanResult.AlreadyPending;
            clan.AddJoin(new WDClanJoinRequest(clanId, playerId, clock()));
            Log(clan, $"Solicitação de entrada: {playerId}."); Notify();
            return WDClanResult.Success;
        }
        AddMember(clan, playerId, WDClanRole.Member);
        Log(clan, $"Membro entrou: {playerId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult DecideJoin(string clanId, string actorId, string requestId, bool accept)
    {
        WDClanData clan = Find(clanId);
        WDClanRole? role = clan?.Member(actorId)?.Role;
        if (clan == null) return WDClanResult.NotFound;
        if (role == null || !WDClanPermissions.Can(role.Value,
            WDClanPermission.ReviewJoinRequests)) return WDClanResult.Forbidden;
        WDClanJoinRequest request = clan.JoinRequests.FirstOrDefault(item =>
            item.Id == requestId);
        if (request == null) return WDClanResult.NotFound;
        if (accept && ClanOf(request.PlayerId) != null) return WDClanResult.AlreadyMember;
        clan.RemoveJoin(requestId);
        if (accept) AddMember(clan, request.PlayerId, WDClanRole.Member);
        Log(clan, $"Entrada {(accept ? "aceita" : "rejeitada")}: {request.PlayerId}.");
        Notify(); return WDClanResult.Success;
    }

    public WDClanResult SetAdmission(string clanId, string actorId, WDClanAdmission mode)
    {
        WDClanData clan = Find(clanId);
        WDClanRole? role = clan?.Member(actorId)?.Role;
        if (clan == null) return WDClanResult.NotFound;
        if (role == null || !WDClanPermissions.Can(role.Value,
            WDClanPermission.EditRequirements)) return WDClanResult.Forbidden;
        clan.Admission = mode; Log(clan, $"Admissão alterada: {mode}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult Edit(string clanId, string actorId, string name, string tag,
        string description, string emblemId)
    {
        WDClanData clan = Find(clanId);
        WDClanRole? role = clan?.Member(actorId)?.Role;
        if (clan == null) return WDClanResult.NotFound;
        if (role == null || !WDClanPermissions.Can(role.Value,
            WDClanPermission.EditClan)) return WDClanResult.Forbidden;
        if (!WDUIContentContracts.IsValidClanName(name) ||
            !WDUIContentContracts.IsValidClanTag(tag) ||
            !WDUIContentContracts.IsValidClanDescription(description))
            return WDClanResult.Invalid;
        if (clans.Any(item => item != clan &&
            (item.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) ||
             item.Tag.Equals(tag.Trim(), StringComparison.OrdinalIgnoreCase))))
            return WDClanResult.Invalid;
        clan.Name = name.Trim(); clan.Tag = tag.Trim();
        clan.Description = description?.Trim() ?? string.Empty;
        clan.EmblemId = string.IsNullOrWhiteSpace(emblemId)
            ? clan.EmblemId : emblemId.Trim();
        Log(clan, "Informações do clã editadas."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult SetMessageOfDay(string clanId, string actorId, string message)
    {
        WDClanData clan = Find(clanId);
        WDClanRole? role = clan?.Member(actorId)?.Role;
        if (clan == null) return WDClanResult.NotFound;
        if (role == null || !WDClanPermissions.Can(role.Value,
            WDClanPermission.EditMessageOfDay)) return WDClanResult.Forbidden;
        clan.MessageOfDay = message?.Trim() ?? string.Empty;
        Log(clan, "Mensagem do Dia atualizada."); Notify();
        return WDClanResult.Success;
    }

    // ============================================================
    // 05. HIERARQUIA E ANTIGUIDADE DE COMANDANTES
    // ============================================================
    public WDClanResult Promote(string clanId, string actorId, string targetId) =>
        ChangeOfficerRole(clanId, actorId, targetId, WDClanRole.Member,
            WDClanRole.Officer, WDClanPermission.PromoteMember, "promovido");

    public WDClanResult Demote(string clanId, string actorId, string targetId) =>
        ChangeOfficerRole(clanId, actorId, targetId, WDClanRole.Officer,
            WDClanRole.Member, WDClanPermission.DemoteOfficer, "rebaixado");

    private WDClanResult ChangeOfficerRole(string clanId, string actorId, string targetId,
        WDClanRole required, WDClanRole next, WDClanPermission permission, string verb)
    {
        WDClanData clan = Find(clanId);
        WDClanMember actor = clan?.Member(actorId);
        WDClanMember target = clan?.Member(targetId);
        if (clan == null) return WDClanResult.NotFound;
        if (actor == null || !WDClanPermissions.Can(actor.Role, permission))
            return WDClanResult.Forbidden;
        if (target == null || target.PlayerId == actorId || target.Role != required ||
            !WDClanPermissions.CanManageRole(actor.Role, target.Role))
            return WDClanResult.Invalid;
        target.SetRole(next); Log(clan, $"{targetId} {verb} para {next}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult PromoteToCommander(string clanId, string actorId, string targetId)
    {
        WDClanData clan = Find(clanId);
        WDClanMember actor = clan?.Member(actorId);
        WDClanMember target = clan?.Member(targetId);
        if (clan == null) return WDClanResult.NotFound;
        if (actor == null || !WDClanPermissions.Can(actor.Role,
            WDClanPermission.PromoteToCommander)) return WDClanResult.Forbidden;
        if (target == null || target.Role != WDClanRole.Officer ||
            target.PlayerId == actorId) return WDClanResult.Invalid;
        if (clan.Commanders.Count >= MaxCommanders) return WDClanResult.CommanderLimit;
        target.SetRole(WDClanRole.Commander, clock().ToUniversalTime());
        Log(clan, $"{targetId} promovido para Commander."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult DemoteCommander(string clanId, string actorId, string targetId)
    {
        WDClanData clan = Find(clanId);
        WDClanMember actor = clan?.Member(actorId);
        WDClanMember target = clan?.Member(targetId);
        if (clan == null) return WDClanResult.NotFound;
        if (actor == null || !WDClanPermissions.Can(actor.Role,
            WDClanPermission.ManageCommander)) return WDClanResult.Forbidden;
        if (target == null || target.Role != WDClanRole.Commander)
            return WDClanResult.Invalid;
        target.SetRole(WDClanRole.Officer);
        Log(clan, $"{targetId} rebaixado para Officer."); Notify();
        return WDClanResult.Success;
    }

    // ============================================================
    // 06. TRANSFERÊNCIA MANUAL E SUCESSÃO AUTOMÁTICA
    // ============================================================
    public WDClanMember OldestCommander(WDClanData clan)
    {
        if (clan == null) return null;
        // Timestamp ausente e empate usam PlayerId ordinal somente como fallback
        // técnico determinístico, não como regra comercial.
        return clan.Commanders
            .OrderBy(member => member.CommanderSinceUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(member => member.PlayerId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public WDClanResult TransferLeadership(string clanId, string generalId,
        string commanderId)
    {
        WDClanData clan = Find(clanId);
        WDClanMember general = clan?.Member(generalId);
        WDClanMember successor = clan?.Member(commanderId);
        if (clan == null) return WDClanResult.NotFound;
        if (general == null || !WDClanPermissions.Can(general.Role,
            WDClanPermission.TransferLeadership)) return WDClanResult.Forbidden;
        if (successor == null || successor.Role != WDClanRole.Commander)
            return WDClanResult.Invalid;
        successor.SetRole(WDClanRole.General);
        RemoveMember(clan, generalId);
        Log(clan, $"Liderança transferida de {generalId} para {commanderId}.");
        Notify(); return WDClanResult.Success;
    }

    public WDClanResult LeaveAsGeneral(string clanId, string generalId,
        out string successorId)
    {
        successorId = null;
        WDClanData clan = Find(clanId);
        WDClanMember general = clan?.Member(generalId);
        if (clan == null) return WDClanResult.NotFound;
        if (general?.Role != WDClanRole.General) return WDClanResult.Forbidden;
        WDClanMember successor = OldestCommander(clan);
        if (successor == null) return WDClanResult.RequiresSuccessor;
        successorId = successor.PlayerId;
        successor.SetRole(WDClanRole.General);
        RemoveMember(clan, generalId);
        Log(clan, $"{generalId} saiu; {successorId} assumiu a liderança.");
        Notify(); return WDClanResult.Success;
    }

    public WDClanResult Leave(string playerId)
    {
        WDClanData clan = ClanOf(playerId);
        if (clan == null) return WDClanResult.NotFound;
        if (clan.Member(playerId)?.Role == WDClanRole.General)
            return WDClanResult.RequiresSuccessor;
        RemoveMember(clan, playerId);
        Log(clan, $"Membro saiu: {playerId}."); Notify();
        return WDClanResult.Success;
    }

    // ============================================================
    // 07. REMOÇÃO, EXCLUSÃO E SOLICITAÇÕES ADMINISTRATIVAS
    // ============================================================
    public WDClanResult Kick(string clanId, string actorId, string targetId)
    {
        WDClanData clan = Find(clanId);
        WDClanMember actor = clan?.Member(actorId);
        WDClanMember target = clan?.Member(targetId);
        if (clan == null) return WDClanResult.NotFound;
        if (actor == null || !WDClanPermissions.Can(actor.Role,
            WDClanPermission.RemoveMember)) return WDClanResult.Forbidden;
        if (target == null || targetId == actorId ||
            !WDClanPermissions.CanManageRole(actor.Role, target.Role))
            return WDClanResult.Invalid;
        RemoveMember(clan, targetId);
        Log(clan, $"Membro expulso: {targetId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult Delete(string clanId, string actorId)
    {
        WDClanData clan = Find(clanId);
        WDClanRole? role = clan?.Member(actorId)?.Role;
        if (clan == null) return WDClanResult.NotFound;
        if (role == null || !WDClanPermissions.Can(role.Value,
            WDClanPermission.DeleteClan)) return WDClanResult.Forbidden;
        foreach (WDClanMember member in clan.Members.ToArray())
            membership.Remove(member.PlayerId);
        clan.ClearMembers(); clan.ClearPrivateState(); clans.Remove(clan); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult RequestKick(string clanId, string actorId, string targetId)
    {
        WDClanData clan = Find(clanId);
        WDClanMember actor = clan?.Member(actorId);
        WDClanMember target = clan?.Member(targetId);
        if (clan == null) return WDClanResult.NotFound;
        if (actor == null || target == null || targetId == actorId ||
            !WDClanPermissions.CanRequestRemovalOf(actor.Role, target.Role))
            return WDClanResult.Forbidden;
        if (clan.KickRequests.Any(item => item.TargetPlayerId == targetId))
            return WDClanResult.AlreadyPending;
        clan.AddKick(new WDClanKickRequest(clanId, actorId, targetId, clock()));
        Log(clan, $"Expulsão solicitada: {targetId} por {actorId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult DecideKick(string clanId, string actorId, string requestId,
        bool approve)
    {
        WDClanData clan = Find(clanId);
        WDClanMember actor = clan?.Member(actorId);
        if (clan == null) return WDClanResult.NotFound;
        WDClanKickRequest request = clan.KickRequests.FirstOrDefault(item =>
            item.Id == requestId);
        if (request == null) return WDClanResult.NotFound;
        WDClanMember target = clan.Member(request.TargetPlayerId);
        if (actor == null || target == null ||
            !WDClanPermissions.CanApproveRemovalOf(actor.Role, target.Role))
            return WDClanResult.Forbidden;
        clan.RemoveKick(requestId);
        if (approve) RemoveMember(clan, request.TargetPlayerId);
        Log(clan, $"Expulsão {(approve ? "aprovada" : "rejeitada")}: {request.TargetPlayerId}.");
        Notify(); return WDClanResult.Success;
    }

    // ============================================================
    // 08. TOKENS CANÔNICOS DE ARMAS, SEM LIMITES TEMPORAIS INVENTADOS
    // ============================================================
    public WDClanResult RequestTokens(string playerId, string weaponId, int quantity,
        out WDClanTokenRequest request)
    {
        request = null;
        WDClanData clan = ClanOf(playerId);
        if (clan == null || !WDClanPermissions.Can(clan.Member(playerId).Role,
            WDClanPermission.ExchangeWeaponTokens)) return WDClanResult.Forbidden;
        if (catalog?.FindWeapon(weaponId) == null || quantity <= 0)
            return WDClanResult.Invalid;
        request = new WDClanTokenRequest(clan.Id, playerId, weaponId, quantity);
        clan.AddToken(request);
        Log(clan, $"Pedido de {quantity} tokens de {weaponId} por {playerId}.");
        Notify(); return WDClanResult.Success;
    }

    public WDClanResult Donate(string donorId, string requestId)
    {
        WDClanData clan = ClanOf(donorId);
        if (clan == null || !WDClanPermissions.Can(clan.Member(donorId).Role,
            WDClanPermission.ExchangeWeaponTokens)) return WDClanResult.Forbidden;
        WDClanTokenRequest request = clan.TokenRequests.FirstOrDefault(item =>
            item.Id == requestId);
        if (request == null || request.Fulfilled) return WDClanResult.NotFound;
        if (request.PlayerId == donorId || ClanOf(request.PlayerId)?.Id != clan.Id)
            return WDClanResult.Forbidden;
        if (catalog?.FindWeapon(request.WeaponId) == null || armory == null)
            return WDClanResult.Unavailable;
        WDArmoryProfile donor = armory.Load(donorId);
        WDArmoryProfile receiver = armory.Load(request.PlayerId);
        WDWeaponProgress outgoing = donor?.GetProgress(request.WeaponId);
        if (outgoing == null || outgoing.Tokens < request.Quantity)
            return WDClanResult.InsufficientTokens;
        if (receiver == null) return WDClanResult.Unavailable;
        WDWeaponProgress incoming = receiver.GetProgress(request.WeaponId);
        if (incoming != null && incoming.Tokens > int.MaxValue - request.Quantity)
            return WDClanResult.Invalid;
        outgoing.SetProgress(outgoing.Obtained, outgoing.Level,
            outgoing.Tokens - request.Quantity);
        if (incoming == null)
            receiver.AddProgress(new WDWeaponProgress(request.WeaponId, false, 1,
                request.Quantity));
        else
            incoming.SetProgress(incoming.Obtained, incoming.Level,
                incoming.Tokens + request.Quantity);
        armory.Save(donorId, donor); armory.Save(request.PlayerId, receiver);
        request.Fulfilled = true;
        Log(clan, $"Doação concluída: {donorId} → {request.PlayerId}, " +
            $"{request.Quantity} tokens de {request.WeaponId}.");
        Notify(); return WDClanResult.Success;
    }

    // ============================================================
    // 09. MEMBERSHIP, PROJEÇÕES E MOCKS DETERMINÍSTICOS
    // ============================================================
    private void AddMember(WDClanData clan, string playerId, WDClanRole role,
        DateTimeOffset? commanderSinceUtc = null)
    {
        clan.AddMember(playerId, role, commanderSinceUtc);
        membership[playerId] = clan.Id;
        foreach (WDClanData other in clans)
            foreach (WDClanJoinRequest request in other.JoinRequests.Where(item =>
                item.PlayerId == playerId).ToArray())
                other.RemoveJoin(request.Id);
    }

    private void RemoveMember(WDClanData clan, string playerId)
    {
        clan.RemoveMember(playerId); membership.Remove(playerId);
        foreach (WDClanKickRequest request in clan.KickRequests.Where(item =>
            item.TargetPlayerId == playerId || item.OfficerId == playerId).ToArray())
            clan.RemoveKick(request.Id);
        foreach (WDClanTokenRequest request in clan.TokenRequests.Where(item =>
            item.PlayerId == playerId && !item.Fulfilled))
            request.Fulfilled = true;
    }

    private void Log(WDClanData clan, string text) => clan.AddLog(clock(), text);
    private void Notify() { ProjectMembership(); Changed?.Invoke(); }
    private void ProjectMembership()
    {
        foreach (WDProfileData profile in profiles.AllProfiles)
            profile.ApplyClan(ClanOf(profile.PlayerId)?.Name ?? string.Empty);
    }

    private void SeedMocks()
    {
        DateTimeOffset now = clock().ToUniversalTime();
        WDClanData home = new("clan-vanguard", "Vanguard Elite", "VGD",
            "Comunidade competitiva de War Dominion.", WDClanAdmission.Approval);
        clans.Add(home);
        AddMember(home, LocalPlayerId, WDClanRole.General);
        if (profiles.Find("WD-2847") != null)
            AddMember(home, "WD-2847", WDClanRole.Commander, now.AddDays(-20));
        if (profiles.Find("WD-7105") != null)
            AddMember(home, "WD-7105", WDClanRole.Commander, now.AddDays(-10));
        Log(home, "Clã criado.");
        if (profiles.Find("WD-3906") != null)
        {
            home.AddJoin(new WDClanJoinRequest(home.Id, "WD-3906", now));
            Log(home, "Solicitação de entrada: WD-3906.");
        }
        WDWeaponDefinition tradeWeapon = catalog?.Weapons.FirstOrDefault(item =>
            (armory?.Load(LocalPlayerId)?.GetProgress(item.Id)?.Tokens ?? 0) > 0)
            ?? catalog?.Weapons.FirstOrDefault();
        if (tradeWeapon != null && home.Member("WD-7105") != null)
        {
            home.AddToken(new WDClanTokenRequest(home.Id, "WD-7105", tradeWeapon.Id, 1));
            Log(home, $"Pedido de 1 token de {tradeWeapon.Id} por WD-7105.");
        }

        WDClanData open = new("clan-iron", "Iron Dominion", "IRON",
            "Clã aberto para novos membros.", WDClanAdmission.Open);
        clans.Add(open);
        if (profiles.Find("WD-5582") != null)
            AddMember(open, "WD-5582", WDClanRole.General);
        WDClanData approval = new("clan-sentinel", "Sentinel Order", "SNT",
            "Entrada por aprovação.", WDClanAdmission.Approval);
        clans.Add(approval);
        if (profiles.Find("WD-7331") != null)
            AddMember(approval, "WD-7331", WDClanRole.General);
    }
}

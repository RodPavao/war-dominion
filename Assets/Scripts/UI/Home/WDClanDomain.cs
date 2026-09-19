using System;
using System.Collections.Generic;
using System.Linq;

public enum WDClanRole { Member, Officer, Commander }
public enum WDClanAdmission { Open, Approval }
public enum WDClanResult
{
    Success, NotFound, Invalid, Forbidden, AlreadyMember, AlreadyPending,
    Locked, DailyLimit, InsufficientTokens, Unavailable
}
public enum WDClanAvailability { Available, Loading, Empty, Error, Offline }

public sealed class WDClanCreationPolicy
{
    public int CoinCost { get; }
    public WDClanCreationPolicy(int coinCost = 0) => CoinCost = Math.Max(0, coinCost);
}

public sealed class WDClanMember
{
    public string PlayerId { get; }
    public WDClanRole Role { get; internal set; }
    public WDClanMember(string playerId, WDClanRole role) { PlayerId = playerId; Role = role; }
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
    public string EmblemId { get; internal set; }
    public WDClanAdmission Admission { get; internal set; }
    public IReadOnlyList<WDClanMember> Members => members;
    public IReadOnlyList<WDClanJoinRequest> JoinRequests => joinRequests;
    public IReadOnlyList<WDClanKickRequest> KickRequests => kickRequests;
    public IReadOnlyList<WDClanTokenRequest> TokenRequests => tokenRequests;
    public IReadOnlyList<WDClanLogEntry> Log => log;
    public string CommanderId => members.FirstOrDefault(item => item.Role == WDClanRole.Commander)?.PlayerId;

    public WDClanData(string id, string name, string tag, string description,
        WDClanAdmission admission, string emblemId = "clan_default")
    { Id = id; Name = name; Tag = tag; Description = description;
      Admission = admission; EmblemId = emblemId; }

    public WDClanMember Member(string playerId) => members.FirstOrDefault(item =>
        item.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase));
    internal void AddMember(string playerId, WDClanRole role) => members.Add(new WDClanMember(playerId, role));
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
    internal void AddLog(DateTimeOffset at, string text) => log.Add(new WDClanLogEntry(at, text));
}

public interface IWDClanArmoryProfiles
{
    WDArmoryProfile Load(string playerId);
    void Save(string playerId, WDArmoryProfile profile);
}

// 01. Autoridade de Clã local de sessão; perfis públicos são projeções, não membership paralelo.
public sealed class WDClanRepository
{
    private readonly WDProfileRepository profiles;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly IWDClanArmoryProfiles armory;
    private readonly Func<DateTimeOffset> clock;
    private readonly List<WDClanData> clans = new();
    private readonly Dictionary<string, string> membership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> requestDays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> donationDays = new(StringComparer.OrdinalIgnoreCase);

    public event Action Changed;
    public WDClanCreationPolicy CreationPolicy { get; }
    public WDClanAvailability Availability { get; private set; } = WDClanAvailability.Available;
    public IReadOnlyList<WDClanData> Clans => clans;
    public IReadOnlyList<WDWeaponDefinition> Weapons => catalog?.Weapons ?? Array.Empty<WDWeaponDefinition>();
    public string LocalPlayerId => profiles.LocalProfile.PlayerId;
    public WDClanData LocalClan => ClanOf(LocalPlayerId);

    public WDClanRepository(WDProfileRepository profiles, WDArmoryCatalogConfig catalog,
        IWDClanArmoryProfiles armory, Func<DateTimeOffset> clock = null,
        WDClanCreationPolicy creationPolicy = null, bool seedMocks = true)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.catalog = catalog;
        this.armory = armory;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        CreationPolicy = creationPolicy ?? new WDClanCreationPolicy();
        if (seedMocks) SeedMocks();
        ProjectMembership();
    }

    public WDClanData Find(string clanId) => clans.FirstOrDefault(item => item.Id == clanId);
    public WDClanData ClanOf(string playerId) => membership.TryGetValue(playerId ?? string.Empty,
        out string id) ? Find(id) : null;
    public IReadOnlyList<WDClanData> Search(string query) =>
        Availability != WDClanAvailability.Available ? Array.Empty<WDClanData>() :
        clans.Where(item => string.IsNullOrWhiteSpace(query) ||
            item.Name.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
            item.Tag.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
    public void SetAvailability(WDClanAvailability value)
    { Availability = value; Changed?.Invoke(); }

    // 02. Criação, admissão e associação única.
    public WDClanResult Create(string playerId, string name, string tag, string description,
        WDClanAdmission admission, out WDClanData created)
    {
        created = null;
        if (profiles.Find(playerId) == null || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(tag)) return WDClanResult.Invalid;
        if (ClanOf(playerId) != null) return WDClanResult.AlreadyMember;
        if (CreationPolicy.CoinCost != 0) return WDClanResult.Unavailable;
        if (clans.Any(item => item.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            item.Tag.Equals(tag.Trim(), StringComparison.OrdinalIgnoreCase))) return WDClanResult.Invalid;
        created = new WDClanData($"clan-{Guid.NewGuid():N}", name.Trim(), tag.Trim(),
            description?.Trim() ?? string.Empty, admission);
        clans.Add(created);
        AddMember(created, playerId, WDClanRole.Commander);
        Log(created, $"Clã criado por {playerId}.");
        Notify();
        return WDClanResult.Success;
    }

    public WDClanResult Join(string clanId, string playerId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null || profiles.Find(playerId) == null) return WDClanResult.NotFound;
        if (ClanOf(playerId) != null) return WDClanResult.AlreadyMember;
        if (clan.Admission == WDClanAdmission.Approval)
        {
            if (clan.JoinRequests.Any(item => item.PlayerId == playerId)) return WDClanResult.AlreadyPending;
            clan.AddJoin(new WDClanJoinRequest(clanId, playerId, clock()));
            Log(clan, $"Solicitação de entrada: {playerId}.");
            Notify();
            return WDClanResult.Success;
        }
        AddMember(clan, playerId, WDClanRole.Member);
        Log(clan, $"Membro entrou: {playerId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult DecideJoin(string clanId, string actorId, string requestId, bool accept)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        WDClanJoinRequest request = clan.JoinRequests.FirstOrDefault(item => item.Id == requestId);
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
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        clan.Admission = mode; Log(clan, $"Admissão alterada: {mode}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult Edit(string clanId, string actorId, string name, string tag,
        string description, string emblemId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tag)) return WDClanResult.Invalid;
        if (clans.Any(item => item != clan &&
            (item.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) ||
             item.Tag.Equals(tag.Trim(), StringComparison.OrdinalIgnoreCase))))
            return WDClanResult.Invalid;
        clan.Name = name.Trim(); clan.Tag = tag.Trim();
        clan.Description = description?.Trim() ?? string.Empty;
        clan.EmblemId = string.IsNullOrWhiteSpace(emblemId) ? clan.EmblemId : emblemId.Trim();
        Log(clan, "Informações do clã editadas."); Notify(); return WDClanResult.Success;
    }

    // 03. Cargos, sucessão e remoção de membros.
    public WDClanResult Promote(string clanId, string actorId, string targetId) =>
        ChangeRole(clanId, actorId, targetId, WDClanRole.Member, WDClanRole.Officer, "promovido");
    public WDClanResult Demote(string clanId, string actorId, string targetId) =>
        ChangeRole(clanId, actorId, targetId, WDClanRole.Officer, WDClanRole.Member, "rebaixado");

    private WDClanResult ChangeRole(string clanId, string actorId, string targetId,
        WDClanRole required, WDClanRole next, string verb)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        WDClanMember target = clan.Member(targetId);
        if (target == null || target.PlayerId == actorId || target.Role != required)
            return WDClanResult.Invalid;
        target.Role = next; Log(clan, $"{targetId} {verb} para {next}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult TransferCommand(string clanId, string actorId, string targetId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        WDClanMember target = clan.Member(targetId);
        if (target == null || target.PlayerId == actorId) return WDClanResult.Invalid;
        WDClanMember old = clan.Member(actorId);
        old.Role = WDClanRole.Officer; target.Role = WDClanRole.Commander;
        Log(clan, $"Comando transferido de {actorId} para {targetId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult Leave(string playerId)
    {
        WDClanData clan = ClanOf(playerId);
        if (clan == null) return WDClanResult.NotFound;
        if (IsCommander(clan, playerId)) return WDClanResult.Forbidden;
        RemoveMember(clan, playerId);
        Log(clan, $"Membro saiu: {playerId}."); Notify(); return WDClanResult.Success;
    }

    public WDClanResult Kick(string clanId, string actorId, string targetId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        if (targetId == actorId || clan.Member(targetId) == null) return WDClanResult.Invalid;
        RemoveMember(clan, targetId);
        Log(clan, $"Membro expulso: {targetId}."); Notify(); return WDClanResult.Success;
    }

    public WDClanResult Delete(string clanId, string actorId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        foreach (WDClanMember member in clan.Members.ToArray()) membership.Remove(member.PlayerId);
        clan.ClearMembers(); clan.ClearPrivateState(); clans.Remove(clan); Notify();
        return WDClanResult.Success;
    }

    // 04. Oficial só solicita expulsão de Membro; decisão cabe ao Comandante.
    public WDClanResult RequestKick(string clanId, string officerId, string targetId)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (clan.Member(officerId)?.Role != WDClanRole.Officer) return WDClanResult.Forbidden;
        if (clan.Member(targetId)?.Role != WDClanRole.Member) return WDClanResult.Invalid;
        if (clan.KickRequests.Any(item => item.TargetPlayerId == targetId))
            return WDClanResult.AlreadyPending;
        clan.AddKick(new WDClanKickRequest(clanId, officerId, targetId, clock()));
        Log(clan, $"Expulsão solicitada: {targetId} por {officerId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult DecideKick(string clanId, string actorId, string requestId, bool approve)
    {
        WDClanData clan = Find(clanId);
        if (clan == null) return WDClanResult.NotFound;
        if (!IsCommander(clan, actorId)) return WDClanResult.Forbidden;
        WDClanKickRequest request = clan.KickRequests.FirstOrDefault(item => item.Id == requestId);
        if (request == null) return WDClanResult.NotFound;
        if (approve && clan.Member(request.TargetPlayerId)?.Role != WDClanRole.Member)
            return WDClanResult.Invalid;
        clan.RemoveKick(requestId);
        if (approve) RemoveMember(clan, request.TargetPlayerId);
        Log(clan, $"Expulsão {(approve ? "aprovada" : "rejeitada")}: {request.TargetPlayerId}.");
        Notify(); return WDClanResult.Success;
    }

    // 05. Pedido/doação integral de tokens canônicos de arma; limites diários independentes em UTC.
    public WDClanResult RequestTokens(string playerId, string weaponId, int quantity,
        out WDClanTokenRequest request)
    {
        request = null;
        WDClanData clan = ClanOf(playerId);
        if (clan == null) return WDClanResult.Forbidden;
        if (catalog?.FindWeapon(weaponId) == null || quantity <= 0) return WDClanResult.Invalid;
        DateTime day = clock().UtcDateTime.Date;
        if (requestDays.TryGetValue(playerId, out DateTime previous) && previous == day)
            return WDClanResult.DailyLimit;
        request = new WDClanTokenRequest(clan.Id, playerId, weaponId, quantity);
        clan.AddToken(request); requestDays[playerId] = day;
        Log(clan, $"Pedido de {quantity} tokens de {weaponId} por {playerId}."); Notify();
        return WDClanResult.Success;
    }

    public WDClanResult Donate(string donorId, string requestId)
    {
        WDClanData clan = ClanOf(donorId);
        if (clan == null) return WDClanResult.Forbidden;
        WDClanTokenRequest request = clan.TokenRequests.FirstOrDefault(item => item.Id == requestId);
        if (request == null || request.Fulfilled) return WDClanResult.NotFound;
        if (request.PlayerId == donorId || ClanOf(request.PlayerId)?.Id != clan.Id)
            return WDClanResult.Forbidden;
        if (catalog?.FindWeapon(request.WeaponId) == null || armory == null)
            return WDClanResult.Unavailable;
        DateTime day = clock().UtcDateTime.Date;
        if (donationDays.TryGetValue(donorId, out DateTime previous) && previous == day)
            return WDClanResult.DailyLimit;
        WDArmoryProfile donor = armory.Load(donorId);
        WDArmoryProfile receiver = armory.Load(request.PlayerId);
        WDWeaponProgress outgoing = donor?.GetProgress(request.WeaponId);
        if (outgoing == null || outgoing.Tokens < request.Quantity)
            return WDClanResult.InsufficientTokens;
        if (receiver == null) return WDClanResult.Unavailable;
        WDWeaponProgress incoming = receiver.GetProgress(request.WeaponId);
        if (incoming != null && incoming.Tokens > int.MaxValue - request.Quantity)
            return WDClanResult.Invalid;
        outgoing.SetProgress(outgoing.Obtained, outgoing.Level, outgoing.Tokens - request.Quantity);
        if (incoming == null)
            receiver.AddProgress(new WDWeaponProgress(request.WeaponId, false, 1, request.Quantity));
        else incoming.SetProgress(incoming.Obtained, incoming.Level, incoming.Tokens + request.Quantity);
        armory.Save(donorId, donor); armory.Save(request.PlayerId, receiver);
        request.Fulfilled = true; donationDays[donorId] = day;
        Log(clan, $"Doação concluída: {donorId} → {request.PlayerId}, {request.Quantity} tokens de {request.WeaponId}.");
        Notify(); return WDClanResult.Success;
    }

    // 06. Normalização de solicitações, projeções e eventos.
    private void AddMember(WDClanData clan, string playerId, WDClanRole role)
    {
        clan.AddMember(playerId, role); membership[playerId] = clan.Id;
        foreach (WDClanData other in clans)
            foreach (WDClanJoinRequest request in other.JoinRequests.Where(item => item.PlayerId == playerId).ToArray())
                other.RemoveJoin(request.Id);
    }

    private void RemoveMember(WDClanData clan, string playerId)
    {
        clan.RemoveMember(playerId); membership.Remove(playerId);
        foreach (WDClanKickRequest request in clan.KickRequests.Where(item =>
            item.TargetPlayerId == playerId || item.OfficerId == playerId).ToArray())
            clan.RemoveKick(request.Id);
        foreach (WDClanTokenRequest request in clan.TokenRequests.Where(item =>
            item.PlayerId == playerId && !item.Fulfilled)) request.Fulfilled = true;
    }

    private bool IsCommander(WDClanData clan, string actorId) =>
        clan.Member(actorId)?.Role == WDClanRole.Commander;
    private void Log(WDClanData clan, string text) => clan.AddLog(clock(), text);
    private void Notify() { ProjectMembership(); Changed?.Invoke(); }
    private void ProjectMembership()
    {
        foreach (WDProfileData profile in profiles.AllProfiles)
            profile.ApplyClan(ClanOf(profile.PlayerId)?.Name ?? string.Empty);
    }

    private void SeedMocks()
    {
        WDClanData home = new("clan-vanguard", "Vanguard Elite", "VGD",
            "Comunidade competitiva de War Dominion.", WDClanAdmission.Approval);
        clans.Add(home);
        AddMember(home, LocalPlayerId, WDClanRole.Commander);
        foreach (string id in new[] { "WD-2847", "WD-7105" })
            if (profiles.Find(id) != null) AddMember(home, id,
                id == "WD-2847" ? WDClanRole.Officer : WDClanRole.Member);
        Log(home, "Clã criado.");
        if (profiles.Find("WD-3906") != null)
        {
            home.AddJoin(new WDClanJoinRequest(home.Id, "WD-3906", clock()));
            Log(home, "Solicitação de entrada: WD-3906.");
        }
        if (home.Member("WD-2847") != null && home.Member("WD-7105") != null)
        {
            home.AddKick(new WDClanKickRequest(home.Id, "WD-2847", "WD-7105", clock()));
            Log(home, "Expulsão solicitada: WD-7105 por WD-2847.");
        }
        WDWeaponDefinition tradeWeapon = catalog?.Weapons.FirstOrDefault(item =>
            (armory?.Load(LocalPlayerId)?.GetProgress(item.Id)?.Tokens ?? 0) > 0)
            ?? catalog?.Weapons.FirstOrDefault();
        if (tradeWeapon != null && home.Member("WD-7105") != null)
        {
            home.AddToken(new WDClanTokenRequest(home.Id, "WD-7105", tradeWeapon.Id, 1));
            requestDays["WD-7105"] = clock().UtcDateTime.Date;
            Log(home, $"Pedido de 1 token de {tradeWeapon.Id} por WD-7105.");
        }
        WDClanData open = new("clan-iron", "Iron Dominion", "IRON",
            "Clã aberto para novos membros.", WDClanAdmission.Open);
        clans.Add(open);
        if (profiles.Find("WD-5582") != null) AddMember(open, "WD-5582", WDClanRole.Commander);
        WDClanData approval = new("clan-sentinel", "Sentinel Order", "SNT",
            "Entrada por aprovação.", WDClanAdmission.Approval);
        clans.Add(approval);
        if (profiles.Find("WD-7331") != null) AddMember(approval, "WD-7331", WDClanRole.Commander);
    }
}

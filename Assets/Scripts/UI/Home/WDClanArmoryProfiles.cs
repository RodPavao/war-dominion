using System;
using System.Collections.Generic;
using UnityEngine;

// 01. Perfis de Arsenal canônicos: local persistido, jogadores mock somente na sessão.
public sealed class WDClanArmoryProfiles : IWDClanArmoryProfiles
{
    private readonly string localPlayerId;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly IWDArmoryRepository localRepository;
    private readonly Dictionary<string, WDArmoryProfile> remote =
        new(StringComparer.OrdinalIgnoreCase);

    public WDClanArmoryProfiles(string localPlayerId, WDArmoryCatalogConfig catalog,
        IWDArmoryRepository localRepository)
    {
        this.localPlayerId = localPlayerId;
        this.catalog = catalog;
        this.localRepository = localRepository;
    }

    public WDArmoryProfile Load(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return null;
        if (playerId.Equals(localPlayerId, StringComparison.OrdinalIgnoreCase))
            return localRepository.Load(catalog);
        if (!remote.TryGetValue(playerId, out WDArmoryProfile profile))
        {
            profile = new WDArmoryProfile();
            if (catalog != null && catalog.Weapons.Count > 0)
                profile.AddProgress(new WDWeaponProgress(catalog.Weapons[0].Id, true, 1, 30));
            remote[playerId] = profile;
        }
        return profile;
    }

    public void Save(string playerId, WDArmoryProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(playerId)) return;
        if (playerId.Equals(localPlayerId, StringComparison.OrdinalIgnoreCase))
            localRepository.Save(profile);
        else remote[playerId] = profile;
    }
}

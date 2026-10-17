using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum WDMatchParticipantKind
{
    Local,
    Remote,
    Bot
}

public enum WDMatchOrigin
{
    Standard,
    Friendly,
    Tournament
}

[Serializable]
public sealed class WDMatchParticipant
{
    public string PlayerId { get; }
    public string Nickname { get; }
    public int TeamIndex { get; }
    public int SlotIndex { get; }
    public Color ProfileColor { get; }
    public Color MatchColor { get; internal set; }
    public string SkinId { get; }
    public string DeckId { get; }
    public WDMatchWeaponLoadoutSnapshot WeaponLoadout { get; }
    public WDMatchParticipantKind Kind { get; }

    public WDMatchParticipant(
        string playerId, string nickname, int teamIndex, int slotIndex,
        Color profileColor, string skinId, string deckId,
        WDMatchParticipantKind kind,
        WDMatchWeaponLoadoutSnapshot weaponLoadout = null)
    {
        PlayerId = playerId ?? string.Empty;
        Nickname = nickname ?? string.Empty;
        TeamIndex = teamIndex;
        SlotIndex = slotIndex;
        ProfileColor = profileColor;
        MatchColor = profileColor;
        SkinId = skinId ?? string.Empty;
        DeckId = deckId ?? string.Empty;
        Kind = kind;
        WeaponLoadout = weaponLoadout;
    }
}

[Serializable]
public sealed class WDMatchSetup
{
    // ============================================================
    // 01. CONTRATO IMUTÁVEL DA PARTIDA PRONTA
    // ============================================================

    public string ModeId { get; }
    public string SubmodeId { get; }
    public string MapId { get; }
    public string ScenePath { get; }
    public string CardRuleId { get; }
    public int RoundLimit { get; }
    public bool SuddenDeathEnabled { get; }
    public int DeterministicSeed { get; }
    public string MatchId { get; }
    public WDMatchOrigin Origin { get; }
    public bool AwardsCompetitiveProgress => Origin != WDMatchOrigin.Friendly;
    public IReadOnlyList<WDMatchParticipant> Participants { get; }
    public bool IsOneVsOne => ModeId == "1x1" && Participants.Count == 2 &&
        Participants[0].TeamIndex != Participants[1].TeamIndex;
    public bool RequiresInitialTerritorySelection => IsOneVsOne;
    public bool IsTeamMatch => Participants.Count > 1 &&
        Participants.GroupBy(participant => participant.TeamIndex)
            .Any(team => team.Count() > 1);
    public bool UsesSymmetricRandomCards =>
        IsRandomCardsRule(CardRuleId);

    public static bool IsRandomCardsRule(string cardRuleId) =>
        cardRuleId == "symmetric_random_cards" ||
        cardRuleId == "symmetric_team_random_cards";

    public WDMatchSetup(
        string modeId, string submodeId, string mapId, string scenePath,
        string cardRuleId, int roundLimit, bool suddenDeathEnabled,
        int deterministicSeed, IReadOnlyList<WDMatchParticipant> participants,
        string matchId = null, WDMatchOrigin origin = WDMatchOrigin.Standard)
    {
        ModeId = modeId ?? string.Empty;
        SubmodeId = submodeId ?? string.Empty;
        MapId = mapId ?? string.Empty;
        ScenePath = scenePath ?? string.Empty;
        CardRuleId = cardRuleId ?? string.Empty;
        RoundLimit = roundLimit;
        SuddenDeathEnabled = suddenDeathEnabled;
        DeterministicSeed = deterministicSeed;
        MatchId = string.IsNullOrWhiteSpace(matchId) ? "WD-" + Guid.NewGuid().ToString("N") : matchId;
        Origin = origin;
        Participants = participants ?? Array.Empty<WDMatchParticipant>();
    }
}

public static class WDMatchColorResolver
{
    // ============================================================
    // 02. CORES EFETIVAS SEM ALTERAR O PERFIL
    // ============================================================

    private const float MinimumColorDistance = 0.18f;

    public static void Resolve(
        IList<WDMatchParticipant> participants,
        IReadOnlyList<Color> preferredAlternatives = null)
    {
        preferredAlternatives ??= WDPlayerPaletteConfig.GetGlobalColors();
        var used = new List<Color>();
        for (int i = 0; i < participants.Count; i++)
        {
            WDMatchParticipant participant = participants[i];
            Color selected = participant.ProfileColor;
            if (IsDistinct(selected, used))
            {
                participant.MatchColor = Opaque(selected);
                used.Add(participant.MatchColor);
                continue;
            }

            selected = FindAlternative(
                participant.ProfileColor, used, preferredAlternatives);
            participant.MatchColor = Opaque(selected);
            used.Add(participant.MatchColor);
        }
    }

    private static Color FindAlternative(
        Color preferred, IReadOnlyList<Color> used,
        IReadOnlyList<Color> alternatives)
    {
        if (alternatives != null)
            foreach (Color candidate in alternatives)
                if (IsDistinct(candidate, used))
                    return candidate;

        // A configuração global é a única fonte de alternativas. Se a paleta
        // estiver esgotada, não inventa uma cor paralela fora do catálogo.
        return preferred;
    }

    private static bool IsDistinct(Color candidate, IReadOnlyList<Color> used)
    {
        for (int i = 0; i < used.Count; i++)
        {
            Vector3 difference = new Vector3(
                candidate.r - used[i].r,
                candidate.g - used[i].g,
                candidate.b - used[i].b);
            if (difference.sqrMagnitude < MinimumColorDistance * MinimumColorDistance)
                return false;
        }
        return true;
    }

    private static Color Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }
}

public static class WDMatchSetupContext
{
    // ============================================================
    // 03. PONTE TRANSITÓRIA ENTRE HOME E CENA DA PARTIDA
    // ============================================================

    public static WDMatchSetup Current { get; private set; }
    public static bool HasSetup => Current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Current = null;

    public static void Store(WDMatchSetup setup)
    {
        Current = setup ?? throw new ArgumentNullException(nameof(setup));
    }

    public static void Clear() => Current = null;

    public static bool TryGetParticipant(
        TerritorioClique.Dono owner,
        out WDMatchParticipant participant)
    {
        participant = null;
        if (Current == null || owner == TerritorioClique.Dono.Neutro)
            return false;

        int slotIndex = (int)owner - 1;
        foreach (WDMatchParticipant candidate in Current.Participants)
        {
            if (candidate.SlotIndex != slotIndex)
                continue;

            participant = candidate;
            return true;
        }

        return false;
    }

    public static void ExitToHome()
    {
        Clear();
        const string homePath = "Assets/Scenes/Home.unity";
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            homePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene("Assets/Scenes/Home");
#endif
    }

    public static bool TryLoadCurrentScene()
    {
        if (Current == null || string.IsNullOrWhiteSpace(Current.ScenePath))
            return false;

#if UNITY_EDITOR
        PlayerPrefs.SetString(MapaAtivo.ChaveMapaTesteEditor, Current.MapId);
#endif
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            Current.ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        string sceneReference = Current.ScenePath.EndsWith(
            ".unity", StringComparison.OrdinalIgnoreCase)
            ? Current.ScenePath.Substring(0, Current.ScenePath.Length - ".unity".Length)
            : Current.ScenePath;
        SceneManager.LoadScene(sceneReference);
#endif
        return true;
    }
}

public static class WDMatchSetupFactory
{
    // ============================================================
    // 04. COMPOSIÇÃO DATA-DRIVEN DO SETUP E PARTICIPANTES MOCK
    // ============================================================

    public static WDMatchSetup Create(
        WDMatchmakingRequest request, DefinicaoMapa map,
        WarDominionHomeData profile, int selectedDeckIndex,
        int deterministicSeed = 0,
        WDArmoryCatalogConfig armoryCatalog = null,
        WDArmoryProfile armoryProfile = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        bool usesRandomCards = WDMatchSetup.IsRandomCardsRule(
            request.CardRuleId);
        bool usesProfileDeck = !usesRandomCards;
        int deckIndex = request.MapSelectionPolicy == WDMapSelectionPolicy.Fixed ||
            !request.DeckSelectionEnabled
            ? armoryProfile?.ActiveDeckIndex ?? profile.DefaultDeckIndex
            : selectedDeckIndex;
        string deckId = !usesProfileDeck ? string.Empty : armoryProfile != null
            ? $"deck_{deckIndex + 1}"
            : profile.GetDeck(deckIndex).Id;
        int participantCount = Mathf.Max(1, request.MatchSize);
        var participants = new List<WDMatchParticipant>(participantCount);
        WDMatchWeaponLoadoutSnapshot localLoadout = CreateWeaponSnapshot(
            usesRandomCards, deckId, deckIndex, armoryCatalog, armoryProfile);
        participants.Add(new WDMatchParticipant(
            "local_player", profile.Nickname, 0, 0, profile.PlayerColor,
            profile.SkinId, deckId, WDMatchParticipantKind.Local, localLoadout));

        for (int i = 1; i < participantCount; i++)
        {
            int teamCount = request.GroupSize > 1
                ? Mathf.Max(1, participantCount / request.GroupSize)
                : participantCount;
            int team = request.GroupSize > 1 ? i % teamCount : i;
            Color defaultColor =
                WDPlayerPaletteConfig.GetGlobalDefaultColorForSlot(i);
            WDMatchWeaponLoadoutSnapshot remoteLoadout = CreateWeaponSnapshot(
                usesRandomCards, deckId, deckIndex, armoryCatalog, armoryProfile);
            participants.Add(new WDMatchParticipant(
                $"remote_{i}", string.Empty, team, i, defaultColor,
                string.Empty, deckId, WDMatchParticipantKind.Remote, remoteLoadout));
        }
        WDMatchColorResolver.Resolve(
            participants, WDPlayerPaletteConfig.GetGlobalColors());
        int setupSeed = deterministicSeed != 0
            ? deterministicSeed
            : usesRandomCards
                ? UnityEngine.Random.Range(1, int.MaxValue)
                : 0;

        return new WDMatchSetup(
            request.ModeId, request.SubmodeId, map.MapaId,
            GetMetadata(map, "scenePath"), request.CardRuleId,
            request.RoundLimit, request.SuddenDeathEnabled,
            setupSeed, participants);
    }

    private static WDMatchWeaponLoadoutSnapshot CreateWeaponSnapshot(
        bool usesRandomCards, string deckId, int deckIndex,
        WDArmoryCatalogConfig catalog, WDArmoryProfile profile) =>
        usesRandomCards
            ? WDMatchWeaponSnapshotFactory.CreateRandomCards(catalog, profile)
            : WDMatchWeaponSnapshotFactory.CreateNormal(
                deckId, deckIndex, catalog, profile);

    // 05. A entrada valida o contexto pronto, sem alterar a criação de snapshots.
    public static bool IsReadyForEntry(WDMatchSetup setup, WDMatchmakingRequest request,
        string expectedMapId, int expectedDeckIndex, WDArmoryCatalogConfig catalog = null)
    {
        if (setup == null || request == null || string.IsNullOrWhiteSpace(expectedMapId) ||
            setup.ModeId != request.ModeId || setup.SubmodeId != request.SubmodeId ||
            setup.MapId != expectedMapId || string.IsNullOrWhiteSpace(setup.ScenePath) ||
            (request.MapSelectionPolicy == WDMapSelectionPolicy.Fixed &&
             setup.MapId != request.FixedMapId) ||
            setup.CardRuleId != request.CardRuleId ||
            setup.Participants.Count != request.MatchSize || request.MatchSize < 2 ||
            request.GroupSize < 1 || request.MatchSize % request.GroupSize != 0 ||
            setup.Participants.Count(item => item.Kind == WDMatchParticipantKind.Local) != 1 ||
            setup.Participants.Select(item => item.PlayerId).Distinct(StringComparer.Ordinal).Count() !=
                setup.Participants.Count ||
            setup.Participants.Select(item => item.SlotIndex).Distinct().Count() !=
                setup.Participants.Count)
            return false;

        int teamCount = request.MatchSize / request.GroupSize;
        if (setup.Participants.Any(item => item.TeamIndex < 0 || item.TeamIndex >= teamCount) ||
            setup.Participants.GroupBy(item => item.TeamIndex).Any(team =>
                team.Count() != request.GroupSize))
            return false;

        bool randomCards = WDMatchSetup.IsRandomCardsRule(request.CardRuleId);
        string expectedDeckId = $"deck_{expectedDeckIndex + 1}";
        return setup.Participants.All(item =>
            IsPlayableSnapshot(item.WeaponLoadout, randomCards, catalog) &&
            (randomCards
                ? string.IsNullOrEmpty(item.DeckId) &&
                  string.IsNullOrEmpty(item.WeaponLoadout.DeckId)
                : item.DeckId == expectedDeckId &&
                  item.WeaponLoadout.DeckId == expectedDeckId));
    }

    private static bool IsPlayableSnapshot(WDMatchWeaponLoadoutSnapshot snapshot,
        bool randomCards, WDArmoryCatalogConfig catalog)
    {
        if (snapshot == null) return false;
        if (snapshot.IsValid) return true;
        // O Arsenal permite slots vazios: eles não tornam inválidas as armas equipadas.
        if (randomCards || snapshot.Status != WDMatchWeaponSnapshotStatus.IncompleteDeck ||
            catalog == null || snapshot.MainSlots.Count != WDSavedWeaponDeck.MainSlotCount ||
            snapshot.SupportSlots.Count != WDSavedWeaponDeck.SupportSlotCount ||
            snapshot.UniversalWeapons.Count != 1 || snapshot.UniversalWeapons[0].Level < 1)
            return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (WDMatchWeaponSlotSnapshot slot in snapshot.MainSlots.Concat(snapshot.SupportSlots))
        {
            if (string.IsNullOrWhiteSpace(slot.WeaponId)) continue;
            WDWeaponDefinition weapon = catalog.FindWeapon(slot.WeaponId);
            if (weapon == null || (slot.WeaponId == WDMatchWeaponSnapshotFactory.UniversalEnergyPlantId &&
                slot.Kind != WDMatchWeaponSlotKind.Main) ||
                !seen.Add(slot.WeaponId) || slot.Level < 1 || slot.Level > weapon.MaximumLevel ||
                slot.Kind == WDMatchWeaponSlotKind.Support && !catalog.IsSupportEligible(weapon))
                return false;
        }
        return true;
    }

    private static string GetMetadata(DefinicaoMapa map, string key)
    {
        MetadadoMapa metadata = map.Metadados.FirstOrDefault(item =>
            item != null && string.Equals(item.chave, key, StringComparison.Ordinal));
        return metadata?.valor ?? string.Empty;
    }
}

using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public sealed class WDDeckProfile
{
    [SerializeField] private string id = "deck_1";
    [SerializeField] private string displayName = "DECK 1";
    [SerializeField] private List<string> cardIds = new(8);

    public string Id => id;
    public string DisplayName => displayName;
    public IReadOnlyList<string> CardIds => cardIds;
}

[CreateAssetMenu(fileName = "WarDominionHomeMockData", menuName = "War Dominion/UI/Home Mock Data")]
public sealed class WarDominionHomeData : ScriptableObject
{
    // ============================================================
    // 01. IDENTIDADE MOCK CONFIGURÁVEL
    // ============================================================

    [Header("Identidade")]
    [SerializeField] private string nickname = "PLAYER";
    [SerializeField] private string status = "Online";
    [SerializeField, Range(0, 9)] private int preferredColorIndex = 4;
    [SerializeField, Min(0)] private int notifications = 3;
    [SerializeField] private bool hasClan;
    [SerializeField] private string skinId = "default";

    [Header("Decks")]
    [SerializeField] private List<WDDeckProfile> decks = new(3);
    [SerializeField, Range(0, 2)] private int defaultDeckIndex;

    [Header("Competitivo")]
    [SerializeField] private string league = "Liga 1";
    [SerializeField, Min(0)] private int trophies;

    [Header("Eventos provisórios")]
    [SerializeField] private string nextEvent = "Operação Atlântico";
    [SerializeField] private string registrationState = "Inscrições abertas";
    [SerializeField] private string liveEvent = "Confronto Relâmpago";
    [SerializeField] private string countdown = "02D 14H";

    public string Nickname => nickname;
    public string Status => status;
    public int PreferredColorIndex => preferredColorIndex;
    public Color PlayerColor => WDPlayerPaletteConfig.GetGlobalColor(preferredColorIndex);
    public int Notifications => notifications;
    public bool HasClan => hasClan;
    public string SkinId => skinId;
    public IReadOnlyList<WDDeckProfile> Decks => decks;
    public int DefaultDeckIndex => Mathf.Clamp(defaultDeckIndex, 0, Mathf.Max(0, decks.Count - 1));
    public string League => league;
    public int Trophies => trophies;
    public string NextEvent => nextEvent;
    public string RegistrationState => registrationState;
    public string LiveEvent => liveEvent;
    public string Countdown => countdown;

    public WDDeckProfile GetDeck(int index)
    {
        if (decks == null || decks.Count == 0)
            return new WDDeckProfile();
        return decks[Mathf.Clamp(index, 0, decks.Count - 1)];
    }
}

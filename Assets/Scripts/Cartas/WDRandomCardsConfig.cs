using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WDCardPoolEntry
{
    [SerializeField] private string cardId;
    [SerializeField] private bool basic;
    [SerializeField] private bool randomCardsEligible = true;

    public string CardId => cardId;
    public bool Basic => basic;
    public bool RandomCardsEligible => randomCardsEligible;

    public WDCardPoolEntry(string cardId, bool basic, bool randomCardsEligible = true)
    {
        this.cardId = cardId;
        this.basic = basic;
        this.randomCardsEligible = randomCardsEligible;
    }
}

[CreateAssetMenu(
    fileName = "WDRandomCardsConfig",
    menuName = "War Dominion/Cartas/Random Cards Config")]
public sealed class WDRandomCardsConfig : ScriptableObject
{
    // ============================================================
    // 01. POOL CANÔNICO E DATA-DRIVEN DO RANDOM CARDS
    // ============================================================

    [SerializeField] private List<WDCardPoolEntry> cards = new();

    public IReadOnlyList<WDCardPoolEntry> Cards => cards;
}

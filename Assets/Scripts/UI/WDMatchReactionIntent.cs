using System;
using System.Collections.Generic;

// 01. Contrato local de reação. Rede futura transporta o payload, não a View.
public readonly struct WDMatchReactionIntent
{
    public TerritorioClique.Dono Author { get; }
    public string Emoji { get; }
    public int Round { get; }
    public WDMatchReactionIntent(TerritorioClique.Dono author, string emoji, int round)
    { Author = author; Emoji = emoji; Round = round; }
}

public sealed class WDMatchReactions
{
    public static readonly IReadOnlyList<string> Options = Array.AsReadOnly(
        new[] { "😎", "😢", "😘", "✌️", "😁", "😂" });
    public event Action<WDMatchReactionIntent> Requested;
    public bool Hidden { get; private set; }
    public void ToggleHidden() => Hidden = !Hidden;

    public static bool CanReact(MatchUIState state, TerritorioClique.Dono player) =>
        state != null && player == state.JogadorLocal &&
        !state.EmSelecaoInicial &&
        (state.Fase == GameManager.FaseTurno.Preparacao ||
         state.Fase == GameManager.FaseTurno.Resolucao);

    public bool Request(MatchUIState state, TerritorioClique.Dono player, string emoji)
    {
        if (!CanReact(state, player)) return false;
        bool found = false;
        foreach (string option in Options) if (option == emoji) found = true;
        if (!found) return false;
        Requested?.Invoke(new WDMatchReactionIntent(player, emoji, state.Round));
        return true;
    }
}

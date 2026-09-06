using System.Collections.Generic;

public static class EquipesJogadores
{
    public enum Equipe
    {
        Nenhuma,
        Vanguard,
        Sentinel
    }

    public static Equipe ObterEquipe(
        TerritorioClique.Dono jogador)
    {
        WDMatchSetup setup = WDMatchSetupContext.Current;
        if (setup != null)
        {
            int slot = (int)jogador - 1;
            WDMatchParticipant participante = null;
            int membrosNaEquipe = 0;

            foreach (WDMatchParticipant candidato in setup.Participants)
                if (candidato.SlotIndex == slot)
                    participante = candidato;

            if (participante == null)
                return Equipe.Nenhuma;

            foreach (WDMatchParticipant candidato in setup.Participants)
                if (candidato.TeamIndex == participante.TeamIndex)
                    membrosNaEquipe++;

            if (membrosNaEquipe < 2)
                return Equipe.Nenhuma;

            return participante.TeamIndex == 0
                ? Equipe.Vanguard
                : participante.TeamIndex == 1
                    ? Equipe.Sentinel
                    : Equipe.Nenhuma;
        }

        switch (jogador)
        {
            case TerritorioClique.Dono.Jogador1:
            case TerritorioClique.Dono.Jogador3:
            case TerritorioClique.Dono.Jogador5:
                return Equipe.Vanguard;

            case TerritorioClique.Dono.Jogador2:
            case TerritorioClique.Dono.Jogador4:
            case TerritorioClique.Dono.Jogador6:
                return Equipe.Sentinel;

            default:
                return Equipe.Nenhuma;
        }
    }

    public static bool SaoAliados(
        TerritorioClique.Dono jogadorA,
        TerritorioClique.Dono jogadorB)
    {
        Equipe a = ObterEquipe(jogadorA);
        Equipe b = ObterEquipe(jogadorB);

        return
            a != Equipe.Nenhuma &&
            a == b;
    }

    // =====================================================
    // 2. CONSULTAS SEMÂNTICAS REUTILIZÁVEIS
    // =====================================================

    public static bool SaoInimigos(
        TerritorioClique.Dono jogadorA,
        TerritorioClique.Dono jogadorB)
    {
        if (jogadorA == TerritorioClique.Dono.Neutro ||
            jogadorB == TerritorioClique.Dono.Neutro ||
            jogadorA == jogadorB)
            return false;

        Equipe a = ObterEquipe(jogadorA);
        Equipe b = ObterEquipe(jogadorB);
        return a == Equipe.Nenhuma || b == Equipe.Nenhuma || a != b;
    }

    public static bool TerritorioPertenceAEquipe(
        TerritorioClique territorio,
        TerritorioClique.Dono jogador)
    {
        return territorio != null &&
            (territorio.dono == jogador || SaoAliados(territorio.dono, jogador));
    }

    public static bool TerritorioPertenceAEquipeAdversaria(
        TerritorioClique territorio,
        TerritorioClique.Dono jogador)
    {
        return territorio != null && SaoInimigos(territorio.dono, jogador);
    }

    // =====================================================
    // 3. ALIADOS DERIVADOS DA IDENTIDADE AUTORITATIVA DO SETUP
    // =====================================================

    public static IReadOnlyList<TerritorioClique.Dono> ObterAliados(
        WDMatchSetup setup,
        TerritorioClique.Dono jogador)
    {
        var aliados = new List<TerritorioClique.Dono>();
        if (setup == null || jogador == TerritorioClique.Dono.Neutro)
            return aliados;

        int slot = (int)jogador - 1;
        WDMatchParticipant participante = null;
        foreach (WDMatchParticipant candidato in setup.Participants)
            if (candidato.SlotIndex == slot)
            {
                participante = candidato;
                break;
            }

        if (participante == null)
            return aliados;

        foreach (WDMatchParticipant candidato in setup.Participants)
        {
            if (candidato.SlotIndex == participante.SlotIndex ||
                candidato.TeamIndex != participante.TeamIndex)
                continue;

            int valorDono = candidato.SlotIndex + 1;
            if (System.Enum.IsDefined(typeof(TerritorioClique.Dono), valorDono))
                aliados.Add((TerritorioClique.Dono)valorDono);
        }

        return aliados;
    }
}

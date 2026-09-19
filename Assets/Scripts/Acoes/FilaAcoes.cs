using System.Collections.Generic;
using UnityEngine;

public class FilaAcoes : MonoBehaviour
{
    // =====================================================
    // 1. ESTADO E CONSULTAS
    // =====================================================

    public const int MaximoAcoesPorRodada = 3;

    private readonly List<OrdemTerrestre> ordens =
        new List<OrdemTerrestre>();

    public IReadOnlyList<OrdemTerrestre> Ordens => ordens;

    public int QuantidadePara(TerritorioClique.Dono jogador)
    {
        int total = 0;

        foreach (OrdemTerrestre ordem in ordens)
        {
            if (ordem.Jogador == jogador)
                total++;
        }

        return total;
    }

    public bool EstaCheiaPara(TerritorioClique.Dono jogador) =>
        QuantidadePara(jogador) >= MaximoAcoesPorRodada;

    public bool EstaCheiaPara(TerritorioClique.Dono jogador, int limite) =>
        QuantidadePara(jogador) >= Mathf.Max(0, limite);

    public int ProximaPosicaoPara(TerritorioClique.Dono jogador)
    {
        int maiorPosicao = 0;

        foreach (OrdemTerrestre ordem in ordens)
        {
            if (ordem.Jogador == jogador)
                maiorPosicao = Mathf.Max(maiorPosicao, ordem.PosicaoNaFila);
        }

        return maiorPosicao + 1;
    }

    // =====================================================
    // 2. REGISTRO E DUPLICIDADE
    // =====================================================

    public bool Adicionar(OrdemTerrestre ordem)
        => Adicionar(ordem, MaximoAcoesPorRodada);

    public bool Adicionar(OrdemTerrestre ordem, int limite)
    {
        if (ordem == null)
            return false;

        if (EstaCheiaPara(ordem.Jogador, limite) ||
            ContemPar(ordem.Jogador, ordem.Origem, ordem.Destino))
            return false;

        ordens.Add(ordem);

        Debug.Log(
            "AÇÃO TERRESTRE #" + ordem.PosicaoNaFila +
            " REGISTRADA: " +
            ordem.Origem.name +
            " -> " +
            ordem.Destino.name +
            " | Tropas: " +
            ordem.QuantidadePretendida
        );

        return true;
    }

    public bool ContemPar(
        TerritorioClique.Dono jogador,
        TerritorioClique origem,
        TerritorioClique destino)
    {
        foreach (OrdemTerrestre ordem in ordens)
        {
            if (ordem.Jogador == jogador &&
                ordem.Origem == origem &&
                ordem.Destino == destino)
                return true;
        }

        return false;
    }

    // =====================================================
    // 3. RESERVA DE TROPAS
    // =====================================================

    public int TropasReservadas(
        TerritorioClique origem)
    {
        int total = 0;

        foreach (OrdemTerrestre ordem in ordens)
        {
            if (ordem.Origem == origem)
                total += ordem.QuantidadePretendida;
        }

        return total;
    }

    public int TropasDisponiveis(
        TerritorioClique origem,
        TerritorioClique.Dono jogador)
    {
        if (origem == null || origem.dono != jogador)
            return 0;

        // Sempre deixa 1 na origem.
        int disponiveis =
            origem.Tropas -
            1 -
            TropasReservadas(origem);

        return Mathf.Max(
            0,
            disponiveis
        );
    }

    // =====================================================
    // 4. REMOÇÃO E CÓPIA PARA RESOLUÇÃO
    // =====================================================

    public bool RemoverUltima(TerritorioClique.Dono jogador)
    {
        for (int i = ordens.Count - 1; i >= 0; i--)
        {
            if (ordens[i].Jogador != jogador)
                continue;

            return RemoverNoIndice(i);
        }

        return false;
    }

    public bool Remover(OrdemTerrestre ordem)
    {
        int indice = ordens.IndexOf(ordem);
        return RemoverNoIndice(indice);
    }

    public int RemoverExcedentes(TerritorioClique.Dono jogador, int limite)
    {
        int removidas = 0;
        while (QuantidadePara(jogador) > Mathf.Max(0, limite) &&
               RemoverUltima(jogador))
            removidas++;
        return removidas;
    }

    private bool RemoverNoIndice(int indice)
    {
        if (indice < 0 || indice >= ordens.Count)
            return false;

        OrdemTerrestre removida = ordens[indice];
        ordens.RemoveAt(indice);

        Debug.Log(
            "ORDEM CANCELADA: " +
            removida.Origem.name +
            " -> " +
            removida.Destino.name +
            " | Tropas liberadas: " +
            removida.QuantidadePretendida
        );

        return true;
    }

    public List<OrdemTerrestre> CriarCopia()
    {
        return new List<OrdemTerrestre>(ordens);
    }

    public void Limpar()
    {
        ordens.Clear();
    }
}

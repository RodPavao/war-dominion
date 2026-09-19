using UnityEngine;

public class SistemaAcoesTerrestres : MonoBehaviour
{
    // =====================================================
    // 1. ESTADO E INICIALIZAÇÃO
    // =====================================================

    private FilaAcoes filaAcoes;

    public void Inicializar(FilaAcoes fila)
    {
        filaAcoes = fila;
    }

    // =====================================================
    // 2. VALIDAÇÃO DA INTERAÇÃO UNIVERSAL
    // =====================================================

    public bool PodePreparar(
        TerritorioClique origem,
        TerritorioClique destino,
        TerritorioClique.Dono jogador)
    {
        if (origem == null || destino == null || origem == destino)
            return false;

        if (origem.dono != jogador)
        {
            Debug.Log("Ação impossível: origem não pertence ao jogador autor.");
            return false;
        }

        if (!origem.EhVizinho(destino))
        {
            Debug.Log(
                "Ação impossível: " + destino.name +
                " não é vizinho de " + origem.name + ".");
            return false;
        }

        if (origem.Tropas <= 1)
        {
            Debug.Log("Ação impossível: origem possui apenas 1 tropa.");
            return false;
        }

        return true;
    }

    // =====================================================
    // 3. REGISTRO E RESERVA
    // =====================================================

    public bool Registrar(
        TerritorioClique origem,
        TerritorioClique destino,
        int quantidade,
        TerritorioClique.Dono jogador,
        int limite = FilaAcoes.MaximoAcoesPorRodada)
    {
        if (filaAcoes == null)
        {
            Debug.LogError("FilaAcoes não inicializada.");
            return false;
        }

        if (!PodePreparar(origem, destino, jogador))
            return false;

        if (filaAcoes.EstaCheiaPara(jogador, limite))
        {
            Debug.Log("Limite atual de " + limite +
                " ações terrestres por jogador e rodada.");
            return false;
        }

        if (filaAcoes.ContemPar(jogador, origem, destino))
        {
            Debug.Log("Ação duplicada: este par origem -> destino já foi preparado.");
            return false;
        }

        int disponiveis = filaAcoes.TropasDisponiveis(origem, jogador);

        if (quantidade < 1 || quantidade > disponiveis)
        {
            Debug.Log(
                origem.name + " possui " + disponiveis +
                " tropa(s) disponíveis para novas ordens.");
            return false;
        }

        OrdemTerrestre ordem = new OrdemTerrestre(
            origem,
            destino,
            quantidade,
            jogador,
            filaAcoes.ProximaPosicaoPara(jogador));

        return filaAcoes.Adicionar(ordem, limite);
    }

    // =====================================================
    // 4. CLASSIFICAÇÃO PROVISÓRIA PARA O HUD
    // =====================================================

    public static string ObterTipoEsperado(
        TerritorioClique.Dono jogador,
        TerritorioClique destino)
    {
        if (destino == null)
            return "—";

        if (destino.dono == jogador)
            return "MOVIMENTO";

        if (destino.IsEmpty)
            return "OCUPAÇÃO";

        if (EquipesJogadores.SaoAliados(jogador, destino.dono))
            return "MOVIMENTO ALIADO";

        return "ATAQUE";
    }

    public static bool PodeOcuparVazio(
        TerritorioClique origem,
        TerritorioClique destino,
        TerritorioClique.Dono jogador)
    {
        return origem != null && destino != null &&
            origem.PossuiDono && origem.dono == jogador &&
            origem.Tropas >= 2 && destino.IsEmpty;
    }
}

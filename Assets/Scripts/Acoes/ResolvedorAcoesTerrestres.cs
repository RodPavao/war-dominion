using System;
using System.Collections.Generic;
using UnityEngine;

public class ResolvedorAcoesTerrestres : MonoBehaviour
{
    // =====================================================
    // 1. REFERÊNCIAS
    // =====================================================

    private ResolvedorCombate resolvedorCombate;
    private Func<OrdemTerrestre, int, int> prepararEntradaHostil;

    public void Inicializar(
        ResolvedorCombate combate,
        Func<OrdemTerrestre, int, int> prepararEntradaHostil = null)
    {
        resolvedorCombate = combate;
        this.prepararEntradaHostil = prepararEntradaHostil;
    }

    // =====================================================
    // 2. MODEL A E ORDEM SEQUENCIAL
    // =====================================================

    public List<ResultadoAcaoTerrestre> Resolver(
        FilaAcoes fila,
        IReadOnlyList<TerritorioClique.Dono> prioridadeJogadores)
    {
        List<ResultadoAcaoTerrestre> resultados =
            new List<ResultadoAcaoTerrestre>();

        if (fila == null)
            return resultados;

        List<OrdemTerrestre> pendentes = fila.CriarCopia();

        Debug.Log("=== INÍCIO DA RESOLUÇÃO TERRESTRE ===");

        if (prioridadeJogadores != null)
        {
            foreach (TerritorioClique.Dono jogador in prioridadeJogadores)
                ResolverOrdensDoJogador(jogador, pendentes, resultados);
        }

        // Autores ainda não conhecidos pela política de rodada também são
        // resolvidos deterministicamente pelo valor do enum, sem perder ordens.
        pendentes.Sort((a, b) =>
        {
            int autor = a.Jogador.CompareTo(b.Jogador);
            return autor != 0
                ? autor
                : a.PosicaoNaFila.CompareTo(b.PosicaoNaFila);
        });

        while (pendentes.Count > 0)
        {
            TerritorioClique.Dono jogador = pendentes[0].Jogador;
            ResolverOrdensDoJogador(jogador, pendentes, resultados);
        }

        fila.Limpar();
        Debug.Log("=== FIM DA RESOLUÇÃO TERRESTRE ===");
        return resultados;
    }

    private void ResolverOrdensDoJogador(
        TerritorioClique.Dono jogador,
        List<OrdemTerrestre> pendentes,
        List<ResultadoAcaoTerrestre> resultados)
    {
        List<OrdemTerrestre> ordensDoJogador = pendentes.FindAll(
            ordem => ordem.Jogador == jogador);

        ordensDoJogador.Sort((a, b) =>
            a.PosicaoNaFila.CompareTo(b.PosicaoNaFila));

        foreach (OrdemTerrestre ordem in ordensDoJogador)
        {
            resultados.Add(ResolverOrdem(ordem));
            pendentes.Remove(ordem);
        }
    }

    // =====================================================
    // 3. REVALIDAÇÃO AUTORITATIVA E ADAPTAÇÃO
    // =====================================================

    public ResultadoAcaoTerrestre ResolverOrdem(OrdemTerrestre ordem)
    {
        if (ordem == null || ordem.Origem == null || ordem.Destino == null)
            return Cancelar("Referências da ordem não estão disponíveis.");

        if (ordem.Origem.dono != ordem.Jogador)
            return Cancelar("A origem não pertence mais ao jogador autor.");

        if (!ordem.Origem.EhVizinho(ordem.Destino))
            return Cancelar("Origem e destino não são mais vizinhos válidos.");

        int quantidadeEfetiva = Mathf.Min(
            ordem.QuantidadePretendida,
            ordem.Origem.Tropas - 1);

        if (quantidadeEfetiva < 1)
            return Cancelar("A origem possui apenas 1 tropa.");

        if (ordem.Destino.dono == ordem.Jogador)
        {
            return ResolverMovimento(
                ordem,
                quantidadeEfetiva,
                ResultadoAcaoTerrestre.TipoResultado.MovimentoProprio);
        }

        if (EquipesJogadores.SaoAliados(
                ordem.Jogador,
                ordem.Destino.dono))
        {
            return ResolverMovimento(
                ordem,
                quantidadeEfetiva,
                ResultadoAcaoTerrestre.TipoResultado.MovimentoAliado);
        }

        // A porta opcional permite que efeitos autoritativos de entrada, como
        // Campo Minado, sejam resolvidos antes do combate sem acoplar armas aqui.
        if (prepararEntradaHostil != null)
        {
            quantidadeEfetiva = prepararEntradaHostil(ordem, quantidadeEfetiva);
            if (quantidadeEfetiva < 1)
                return Cancelar("A força de entrada foi eliminada antes do combate.");
        }

        if (ordem.Destino.IsEmpty)
            return ResolverOcupacaoVazio(ordem, quantidadeEfetiva);

        if (resolvedorCombate == null)
            return Cancelar("Resolvedor de combate não está disponível.");

        int tropasOrigemAntes = ordem.Origem.Tropas;
        int tropasDestinoAntes = ordem.Destino.Tropas;
        TerritorioClique.Dono donoDestinoAntes = ordem.Destino.dono;

        ResultadoCombate combate = resolvedorCombate.ResolverAtaque(
            ordem,
            quantidadeEfetiva);

        return new ResultadoAcaoTerrestre(
            combate.Valido,
            ResultadoAcaoTerrestre.TipoResultado.Ataque,
            quantidadeEfetiva,
            combate.Motivo,
            combate,
            ordem,
            tropasOrigemAntes,
            ordem.Origem.Tropas,
            tropasDestinoAntes,
            ordem.Destino.Tropas,
            donoDestinoAntes,
            ordem.Destino.dono);
    }

    // =====================================================
    // 4. MOVIMENTO AMIGÁVEL E CANCELAMENTOS
    // =====================================================

    private ResultadoAcaoTerrestre ResolverOcupacaoVazio(
        OrdemTerrestre ordem,
        int quantidade)
    {
        int tropasOrigemAntes = ordem.Origem.Tropas;
        int tropasDestinoAntes = ordem.Destino.Tropas;

        if (!ordem.Origem.RemoverTropas(quantidade))
            return Cancelar("A origem não conseguiu manter a tropa mínima.");

        ordem.Destino.DefinirDono(ordem.Jogador);
        ordem.Destino.DefinirTropas(quantidade);

        return new ResultadoAcaoTerrestre(
            true,
            ResultadoAcaoTerrestre.TipoResultado.OcupacaoVazio,
            quantidade,
            "Território vazio ocupado sem combate.",
            null,
            ordem,
            tropasOrigemAntes,
            ordem.Origem.Tropas,
            tropasDestinoAntes,
            ordem.Destino.Tropas,
            TerritorioClique.Dono.Neutro,
            ordem.Jogador);
    }

    private ResultadoAcaoTerrestre ResolverMovimento(
        OrdemTerrestre ordem,
        int quantidade,
        ResultadoAcaoTerrestre.TipoResultado tipo)
    {
        int tropasOrigemAntes = ordem.Origem.Tropas;
        int tropasDestinoAntes = ordem.Destino.Tropas;
        TerritorioClique.Dono donoDestino = ordem.Destino.dono;

        if (!ordem.Origem.RemoverTropas(quantidade))
            return Cancelar("A origem não conseguiu manter a tropa mínima.");

        ordem.Destino.DefinirTropas(ordem.Destino.Tropas + quantidade);

        string descricao = tipo ==
            ResultadoAcaoTerrestre.TipoResultado.MovimentoAliado
                ? "MOVIMENTO PARA ALIADO"
                : "MOVIMENTO PRÓPRIO";

        Debug.Log(
            descricao + ": " + ordem.Origem.name + " -> " +
            ordem.Destino.name + " | Tropas: " + quantidade);

        return new ResultadoAcaoTerrestre(
            true,
            tipo,
            quantidade,
            descricao + " concluído.",
            null,
            ordem,
            tropasOrigemAntes,
            ordem.Origem.Tropas,
            tropasDestinoAntes,
            ordem.Destino.Tropas,
            donoDestino,
            ordem.Destino.dono);
    }

    private ResultadoAcaoTerrestre Cancelar(string motivo)
    {
        Debug.Log("AÇÃO TERRESTRE CANCELADA: " + motivo);

        return new ResultadoAcaoTerrestre(
            false,
            ResultadoAcaoTerrestre.TipoResultado.Cancelada,
            0,
            motivo);
    }
}

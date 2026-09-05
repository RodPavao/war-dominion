public class ResultadoAcaoTerrestre
{
    // =====================================================
    // 1. CLASSIFICAÇÃO
    // =====================================================

    public enum TipoResultado
    {
        Cancelada,
        Ataque,
        OcupacaoVazio,
        MovimentoProprio,
        MovimentoAliado
    }

    public bool Executada { get; private set; }
    public TipoResultado Tipo { get; private set; }
    public int QuantidadeEfetiva { get; private set; }
    public string Motivo { get; private set; }
    public ResultadoCombate Combate { get; private set; }
    public OrdemTerrestre Ordem { get; private set; }
    public int TropasOrigemAntes { get; private set; }
    public int TropasOrigemDepois { get; private set; }
    public int TropasDestinoAntes { get; private set; }
    public int TropasDestinoDepois { get; private set; }
    public TerritorioClique.Dono DonoDestinoAntes { get; private set; }
    public TerritorioClique.Dono DonoDestinoDepois { get; private set; }

    // =====================================================
    // 2. CONSTRUÇÃO DO RESULTADO
    // =====================================================

    public ResultadoAcaoTerrestre(
        bool executada,
        TipoResultado tipo,
        int quantidadeEfetiva,
        string motivo,
        ResultadoCombate combate = null,
        OrdemTerrestre ordem = null,
        int tropasOrigemAntes = 0,
        int tropasOrigemDepois = 0,
        int tropasDestinoAntes = 0,
        int tropasDestinoDepois = 0,
        TerritorioClique.Dono donoDestinoAntes = TerritorioClique.Dono.Neutro,
        TerritorioClique.Dono donoDestinoDepois = TerritorioClique.Dono.Neutro)
    {
        Executada = executada;
        Tipo = tipo;
        QuantidadeEfetiva = quantidadeEfetiva;
        Motivo = motivo;
        Combate = combate;
        Ordem = ordem;
        TropasOrigemAntes = tropasOrigemAntes;
        TropasOrigemDepois = tropasOrigemDepois;
        TropasDestinoAntes = tropasDestinoAntes;
        TropasDestinoDepois = tropasDestinoDepois;
        DonoDestinoAntes = donoDestinoAntes;
        DonoDestinoDepois = donoDestinoDepois;
    }
}

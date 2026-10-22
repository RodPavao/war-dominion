using UnityEngine;

public class ResolvedorCombate : MonoBehaviour
{
    // =====================================================
    // 1. RESOLUÇÃO DO COMBATE
    // =====================================================

    public ResultadoCombate ResolverAtaque(
        OrdemTerrestre ordem,
        int quantidadeEfetiva)
    {
        if (ordem == null ||
            ordem.Origem == null ||
            ordem.Destino == null ||
            quantidadeEfetiva < 1)
        {
            return new ResultadoCombate(
                false,
                false,
                0,
                0,
                0,
                0,
                "Dados de combate inválidos."
            );
        }

        TerritorioClique origem =
            ordem.Origem;

        TerritorioClique destino =
            ordem.Destino;

        int ataque = quantidadeEfetiva;

        int defesa =
            destino.Tropas;

        int minimoParaConquistar =
            CalcularAtaqueMinimo(defesa);

        // ================================================
        // CONQUISTA
        // ================================================

        if (ataque >= minimoParaConquistar)
        {
            int sobreviventes =
                ataque - defesa;

            origem.RemoverTropas(
                ataque
            );

            destino.DefinirDono(
                ordem.Jogador
            );

            destino.DefinirTropas(
                sobreviventes
            );

            Debug.Log(
                "CONQUISTA: " +
                origem.name +
                " -> " +
                destino.name +
                " | Ataque: " +
                ataque +
                " | Defesa: " +
                defesa +
                " | Sobreviventes: " +
                sobreviventes
            );

            return new ResultadoCombate(
                true,
                true,
                ataque,
                defesa,
                sobreviventes,
                0,
                "Território conquistado."
            );
        }

        // ================================================
        // ATAQUE REPELIDO
        // ================================================

        int sobreviventesAtaque =
            Mathf.Max(
                1,
                ataque - defesa
            );

        int sobreviventesDefesa =
            Mathf.Max(
                1,
                defesa - ataque
            );

        int perdasAtacante =
            ataque -
            sobreviventesAtaque;

        int tropasOrigemDepois =
            origem.Tropas -
            perdasAtacante;

        tropasOrigemDepois =
            Mathf.Max(
                1,
                tropasOrigemDepois
            );

        origem.DefinirTropas(
            tropasOrigemDepois
        );

        destino.DefinirTropas(
            sobreviventesDefesa
        );

        Debug.Log(
            "ATAQUE REPELIDO: " +
            origem.name +
            " -> " +
            destino.name +
            " | Ataque enviado: " +
            ataque +
            " | Defesa: " +
            defesa +
            " | Origem agora: " +
            origem.Tropas +
            " | Defesa agora: " +
            destino.Tropas
        );

        return new ResultadoCombate(
            true,
            false,
            ataque,
            defesa,
            sobreviventesAtaque,
            sobreviventesDefesa,
            "Ataque insuficiente para conquista."
        );
    }

    // =====================================================
    // 2. REGRA DE FORÇA
    // =====================================================

    public int CalcularAtaqueMinimo(
        int defesa)
        => CalcularAtaqueMinimoBase(defesa);

    // =====================================================
    // 2.1. REGRA PURA COMPARTILHADA COM A APRESENTAÇÃO
    // =====================================================
    public static int CalcularAtaqueMinimoBase(int defesa)
    {
        defesa =
            Mathf.Max(
                1,
                defesa
            );

        // Até defesa 10:
        // basta vantagem de +1.
        //
        // 1 defesa -> 2 ataque
        // 2 defesa -> 3 ataque
        // ...
        // 10 defesa -> 11 ataque
        if (defesa <= 10)
        {
            return defesa + 1;
        }

        // Defesa 11+:
        // vantagem de 15%,
        // com mínimo de +2.
        int vantagemPercentual =
            Mathf.CeilToInt(
                defesa * 0.15f
            );

        int vantagemNecessaria =
            Mathf.Max(
                2,
                vantagemPercentual
            );

        return
            defesa +
            vantagemNecessaria;
    }
}

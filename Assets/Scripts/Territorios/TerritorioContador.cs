using System.Collections.Generic;
using UnityEngine;

public class TerritorioContador : MonoBehaviour
{
    // =====================================================
    // 1. ESCALA VISUAL GLOBAL E OVERRIDES FUTUROS
    // =====================================================

    private static readonly Vector3 escalaPadraoGlobal =
        new Vector3(0.6719f, 0.7201f, 1.2150f);

    private static readonly Dictionary<string, Vector3> overridesEscalaPorMapa =
        new Dictionary<string, Vector3>
        {
            {
                "pearl_harbor",
                new Vector3(0.6078756f, 0.6184228f, 1.215004f)
            },
            {
                "riachuelo",
                new Vector3(1.609024f, 1.644019f, 2f)
            }
        };

    private ContadorTropas contadorTropas;
    private TerritorioClique territorio;

    private void Awake()
    {
        territorio =
            GetComponent<TerritorioClique>();
    }

    public void Inicializar()
    {
        if (territorio == null)
            territorio =
                GetComponent<TerritorioClique>();

        Transform existente =
            transform.Find("ContadorModerno");

        if (existente != null)
        {
            AplicarEscalaDoContador(existente);

            contadorTropas =
                existente.GetComponent<ContadorTropas>();

            if (contadorTropas != null)
                contadorTropas.Configurar(territorio);

            return;
        }

        CriarContador();
    }

    private static void AplicarEscalaDoContador(
        Transform contador)
    {
        if (contador == null)
            return;

        DefinicaoMapa mapa = MapaAtivo.Instance != null
            ? MapaAtivo.Instance.Definicao
            : null;

        Vector3 escala = escalaPadraoGlobal;
        if (mapa != null &&
            overridesEscalaPorMapa.TryGetValue(mapa.MapaId, out Vector3 overrideMapa))
            escala = overrideMapa;

        contador.localScale = escala;
    }

    private void CriarContador()
    {
        PolygonCollider2D collider =
            GetComponent<PolygonCollider2D>();

        if (collider == null)
            return;

        GameObject obj =
            new GameObject("ContadorModerno");

        obj.transform.SetParent(
            transform,
            false
        );

        Vector2 centroEscolhido =
            Vector2.zero;

        float maiorArea =
            0f;

        for (int i = 0;
             i < collider.pathCount;
             i++)
        {
            Vector2[] pontos =
                collider.GetPath(i);

            float area =
                Mathf.Abs(
                    CalcularAreaPoligono(pontos)
                );

            if (area > maiorArea)
            {
                maiorArea = area;

                centroEscolhido =
                    CalcularCentroPoligono(pontos);
            }
        }

        obj.transform.localPosition =
            new Vector3(
                centroEscolhido.x,
                centroEscolhido.y,
                0f
            );

        const float escalaPadraoContador =
            0.9004f;

        obj.transform.localScale =
            new Vector3(
                escalaPadraoContador,
                escalaPadraoContador,
                1f
            );

        AplicarEscalaDoContador(obj.transform);

        contadorTropas =
            obj.AddComponent<ContadorTropas>();

        contadorTropas.Configurar(
            territorio
        );
    }

    public void Atualizar()
    {
        if (contadorTropas != null)
            contadorTropas.Atualizar();
    }

    private float CalcularAreaPoligono(
        Vector2[] pontos)
    {
        if (pontos == null ||
            pontos.Length < 3)
            return 0f;

        float area = 0f;

        for (int i = 0;
             i < pontos.Length;
             i++)
        {
            Vector2 atual =
                pontos[i];

            Vector2 proximo =
                pontos[
                    (i + 1) %
                    pontos.Length
                ];

            area +=
                atual.x * proximo.y -
                proximo.x * atual.y;
        }

        return area * 0.5f;
    }

    private Vector2 CalcularCentroPoligono(
        Vector2[] pontos)
    {
        if (pontos == null ||
            pontos.Length == 0)
            return Vector2.zero;

        Vector2 soma =
            Vector2.zero;

        foreach (Vector2 ponto in pontos)
        {
            soma += ponto;
        }

        return soma /
            pontos.Length;
    }
}

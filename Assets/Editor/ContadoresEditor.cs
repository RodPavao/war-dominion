using UnityEngine;
using UnityEditor;

public static class ContadoresEditor
{
    // =========================================================
    // GERAR CONTADORES PERMANENTES
    // =========================================================

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Contadores/Gerar Contadores Permanentes")]
    public static void GerarContadoresPermanentes()
    {
        GameObject pai =
            GameObject.Find("Territorios");

        if (pai == null)
        {
            Debug.LogError(
                "Objeto Territorios não encontrado."
            );

            return;
        }

        int criados = 0;
        int preservados = 0;
        int erros = 0;

        foreach (Transform filho in pai.transform)
        {
            TerritorioClique territorio =
                filho.GetComponent<TerritorioClique>();

            if (territorio == null)
                continue;

            Transform existente =
                filho.Find("ContadorModerno");

            if (existente != null)
            {
                preservados++;
                continue;
            }

            PolygonCollider2D collider =
                filho.GetComponent<PolygonCollider2D>();

            if (collider == null)
            {
                Debug.LogWarning(
                    "Sem PolygonCollider2D: " +
                    filho.name
                );

                erros++;
                continue;
            }

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
                        CalcularAreaContador(pontos)
                    );

                if (area > maiorArea)
                {
                    maiorArea = area;

                    centroEscolhido =
                        CalcularCentroContador(
                            pontos
                        );
                }
            }

            GameObject contadorObj =
                new GameObject(
                    "ContadorModerno"
                );

            Undo.RegisterCreatedObjectUndo(
                contadorObj,
                "Criar Contador Moderno"
            );

            contadorObj.transform.SetParent(
                filho,
                false
            );

            contadorObj.transform.localPosition =
                new Vector3(
                    centroEscolhido.x,
                    centroEscolhido.y,
                    0f
                );

            float escala =
                Mathf.Sqrt(
                    maiorArea / 0.65f
                );

            escala =
                Mathf.Clamp(
                    escala,
                    0.68f,
                    1f
                );

            contadorObj.transform.localScale =
                new Vector3(
                    escala,
                    escala,
                    1f
                );

            ContadorTropas contador =
                contadorObj.AddComponent<
                    ContadorTropas>();

            contador.Configurar(
                territorio
            );

            EditorUtility.SetDirty(
                contadorObj
            );

            criados++;
        }

        UnityEditor.SceneManagement
            .EditorSceneManager
            .MarkSceneDirty(
                UnityEngine.SceneManagement
                    .SceneManager
                    .GetActiveScene()
            );

        Debug.Log(
            "CONTADORES PERMANENTES | Criados: " +
            criados +
            " | Preservados: " +
            preservados +
            " | Erros: " +
            erros
        );
    }

    // =========================================================
    // PADRONIZAR TAMANHO PELO PERU
    // =========================================================

    [MenuItem(
    "War Dominion/Ferramentas de Desenvolvimento/Contadores/Aplicar Tamanho Padrão"
)]
public static void AplicarTamanhoPadrao()
    {
        GameObject pai =
            GameObject.Find("Territorios");

        if (pai == null)
        {
            Debug.LogError(
                "Objeto Territorios não encontrado."
            );

            return;
        }

        Transform peru =
            pai.transform.Find("Peru");

        if (peru == null)
        {
            Debug.LogError(
                "Território Peru não encontrado."
            );

            return;
        }

        Transform contadorPeru =
            peru.Find("ContadorModerno");

        if (contadorPeru == null)
        {
            Debug.LogError(
                "ContadorModerno do Peru não encontrado."
            );

            return;
        }

        Vector3 escalaPadrao =
            contadorPeru.localScale;

        int alterados = 0;

        foreach (Transform territorio in pai.transform)
        {
            Transform contador =
                territorio.Find("ContadorModerno");

            if (contador == null)
                continue;

            Undo.RecordObject(
                contador,
                "Padronizar tamanho dos contadores"
            );

            contador.localScale =
                escalaPadrao;

            EditorUtility.SetDirty(
                contador
            );

            alterados++;
        }

        UnityEditor.SceneManagement
            .EditorSceneManager
            .MarkSceneDirty(
                UnityEngine.SceneManagement
                    .SceneManager
                    .GetActiveScene()
            );

        Debug.Log(
            "CONTADORES PADRONIZADOS PELO PERU | Total: " +
            alterados +
            " | Escala: " +
            escalaPadrao
        );
    }

    // =========================================================
    // CÁLCULOS
    // =========================================================

    private static float CalcularAreaContador(
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

    private static Vector2 CalcularCentroContador(
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

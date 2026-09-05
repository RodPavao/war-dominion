using UnityEngine;
using UnityEditor;

public static class MontadorTerritoriosEditor
{
    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Montar Territórios Definitivos")]
    public static void MontarTerritoriosDefinitivos()
    {
        const string pastaSprites =
            "Assets/TerritoriosDefinitivos";

        GameObject pai =
            GameObject.Find("Territorios");

        if (pai == null)
        {
            Debug.LogError(
                "Objeto Territorios não encontrado."
            );

            return;
        }

        Transform modelo =
            pai.transform.Find("Arabia");

        if (modelo == null)
        {
            Debug.LogError(
                "Arabia não encontrada para servir de referência."
            );

            return;
        }

        Vector3 posicaoPadrao =
            modelo.localPosition;

        Vector3 escalaPadrao =
            modelo.localScale;

        Quaternion rotacaoPadrao =
            modelo.localRotation;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Sprite",
                new[]
                {
                    pastaSprites
                }
            );

        int criados = 0;
        int atualizados = 0;

        foreach (string guid in guids)
        {
            string caminho =
                AssetDatabase.GUIDToAssetPath(guid);

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    caminho
                );

            if (sprite == null)
                continue;

            string nome = sprite.name;

            Transform existente =
                pai.transform.Find(nome);

            GameObject objeto;

            if (existente != null)
            {
                objeto =
                    existente.gameObject;

                atualizados++;
            }
            else
            {
                objeto =
                    new GameObject(nome);

                objeto.transform.SetParent(
                    pai.transform,
                    false
                );

                criados++;
            }

            objeto.transform.localPosition =
                posicaoPadrao;

            objeto.transform.localRotation =
                rotacaoPadrao;

            objeto.transform.localScale =
                escalaPadrao;

            SpriteRenderer sr =
                objeto.GetComponent<SpriteRenderer>();

            if (sr == null)
            {
                sr =
                    objeto.AddComponent<SpriteRenderer>();
            }

            sr.sprite = sprite;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 1;
            sr.color = Color.white;

            TerritorioClique territorio =
                objeto.GetComponent<TerritorioClique>();

            if (territorio == null)
            {
                territorio =
                    objeto.AddComponent<TerritorioClique>();
            }

            territorio.dono =
                TerritorioClique.Dono.Neutro;

            TerritorioTropas tropasTerritorio =
    objeto.GetComponent<TerritorioTropas>();

if (tropasTerritorio == null)
{
    tropasTerritorio =
        objeto.AddComponent<TerritorioTropas>();
}

tropasTerritorio.Quantidade = 1;

            territorio.idTerritorio = nome;

            territorio.chaveTraducao =
                "territory_" +
                ConverterParaChave(nome);

            territorio.continente =
                ObterContinente(nome);

            TerritorioFronteiras fronteiras =
                objeto.GetComponent<TerritorioFronteiras>();

            if (fronteiras == null)
            {
                fronteiras =
                    objeto.AddComponent<TerritorioFronteiras>();
            }

            PolygonCollider2D antigo =
                objeto.GetComponent<PolygonCollider2D>();

            if (antigo != null)
            {
                Object.DestroyImmediate(antigo);
            }

            PolygonCollider2D collider =
                objeto.AddComponent<PolygonCollider2D>();

            collider.isTrigger = false;

            EditorUtility.SetDirty(objeto);
        }

        UnityEditor.SceneManagement
            .EditorSceneManager
            .MarkSceneDirty(
                UnityEngine.SceneManagement
                    .SceneManager
                    .GetActiveScene()
            );

        Debug.Log(
            "MONTAGEM CONCLUÍDA | Criados: " +
            criados +
            " | Atualizados: " +
            atualizados
        );
    }

    private static string ConverterParaChave(
        string nome)
    {
        System.Text.StringBuilder resultado =
            new System.Text.StringBuilder();

        for (int i = 0; i < nome.Length; i++)
        {
            char atual = nome[i];

            if (i > 0 &&
                char.IsUpper(atual) &&
                char.IsLower(nome[i - 1]))
            {
                resultado.Append("_");
            }

            resultado.Append(
                char.ToLowerInvariant(atual)
            );
        }

        return resultado.ToString();
    }

    private static TerritorioClique.Continente
        ObterContinente(string nome)
    {
        switch (nome)
        {
            // AMÉRICA DO NORTE
            case "Alaska":
            case "Yukon":
            case "Alberta":
            case "Ontario":
            case "Quebec":
            case "Greenland":
            case "WestUSA":
            case "EastUSA":
            case "Mexico":
                return TerritorioClique.Continente
                    .AmericaDoNorte;

            // AMÉRICA DO SUL
            case "Venezuela":
            case "Peru":
            case "Brazil":
            case "Argentina":
                return TerritorioClique.Continente
                    .AmericaDoSul;

            // ÁFRICA
            case "Algeria":
            case "Egypt":
            case "Ethiopia":
            case "Congo":
            case "SouthAfrica":
            case "Madagascar":
                return TerritorioClique.Continente
                    .Africa;

            // EUROPA
            case "Iceland":
            case "Scandinavia":
            case "UnitedKingdom":
            case "Germany":
            case "Spain":
            case "Italy":
            case "Russia":
                return TerritorioClique.Continente
                    .Europa;

            // ÁSIA
            case "Ural":
            case "Siberia":
            case "Yakutsk":
            case "Kamchatka":
            case "Irkutsk":
            case "Mongolia":
            case "Japan":
            case "Afghanistan":
            case "China":
            case "Arabia":
            case "India":
            case "Indochina":
                return TerritorioClique.Continente
                    .Asia;

            // OCEANIA
            case "Indonesia":
            case "NewGuinea":
            case "WestAustralia":
            case "EastAustralia":
                return TerritorioClique.Continente
                    .Oceania;

            default:
                Debug.LogWarning(
                    "Continente não definido para: " +
                    nome
                );

                return TerritorioClique.Continente
                    .AmericaDoNorte;
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class LimparScriptsAusentes
{
    [MenuItem("War Dominion/Manutenção/Limpar Scripts Ausentes")]
    public static void Executar()
    {
        int removidos = 0;

        Scene cena = SceneManager.GetActiveScene();

        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            Transform[] objetos =
                raiz.GetComponentsInChildren<Transform>(true);

            foreach (Transform objeto in objetos)
            {
                int quantidade =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        objeto.gameObject
                    );

                if (quantidade <= 0)
                    continue;

                Undo.RegisterCompleteObjectUndo(
                    objeto.gameObject,
                    "Remover scripts ausentes"
                );

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                    objeto.gameObject
                );

                removidos += quantidade;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager
            .MarkSceneDirty(cena);

        Debug.Log(
            "LIMPEZA CONCLUÍDA | Scripts ausentes removidos: " +
            removidos
        );
    }
}

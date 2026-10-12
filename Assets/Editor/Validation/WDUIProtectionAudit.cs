#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WDUIProtectionAudit
{
    private const string ScreenAssetPath = "Assets/Resources/UI/Armory/Prefabs/WDArmoryScreen.prefab";
    private const string CardAssetPath = "Assets/Resources/UI/Armory/Prefabs/WDArmoryWeaponCard.prefab";
    private const string RuntimeResourcesPath = "UI/Armory/Prefabs/WDArmoryScreen";
    private const string HomeScenePath = "Assets/Scenes/Home.unity";
    private const string ThemeAssetPath = "Assets/Resources/UI/WarDominionUITheme.asset";
    private const string VisualCatalogPath = "Assets/Resources/UI/Armory/WDArmoryVisualCatalog.asset";
    private const string ArmoryV2ScreenPath = "Assets/VisualKit/ArsenalV2/Prefabs/WDArmoryScreenV2.prefab";
    private const string ArmoryV2SettingsPath = "Assets/Resources/UI/ArmoryV2/WDArmoryV2VisualSettings.asset";
    private const string CanonicalArtworkRoot = "Assets/Resources/UI/Arsenal/Canonical/";
    private const string LegacyArtworkRoot = "Assets/VisualKit/Arsenal/Weapons/";
    private const string VisualKitRoot = "Assets/VisualKit/";
    private static readonly string[] PrefabWriteOperations =
    {
        "PrefabUtility.SaveAsPrefabAsset(",
        "PrefabUtility.SaveAsPrefabAssetAndConnect(",
        "PrefabUtility.ReplacePrefab(",
        "AssetDatabase.CopyAsset(",
        "AssetDatabase.MoveAsset("
    };

    // ============================================================
    // 01. RELATÓRIO SOMENTE LEITURA
    // ============================================================
    [MenuItem("War Dominion/Validate/UI Protection Audit")]
    public static void Run()
    {
        var report = new StringBuilder(4096);
        var errors = new List<string>();
        report.AppendLine("[WD-UI-PROTECTION]");
        report.AppendLine("Auditoria somente leitura. Nenhum asset é salvo ou regenerado.");

        ValidateRuntimePath(report, errors);
        ValidateActivePrefabs(report, errors);
        ValidateRelevantScenes(report, errors);
        ValidateBuilderProtection(report, errors);
        ValidateVisualKitReferences(report, errors);
        ValidateCanonicalArmoryArtworks(report, errors);

        report.AppendLine(errors.Count == 0
            ? "RESULTADO: OK — proteções e referências essenciais confirmadas."
            : $"RESULTADO: FALHA — {errors.Count} problema(s) encontrado(s).\n- {string.Join("\n- ", errors)}");

        if (errors.Count == 0)
            Debug.Log(report.ToString());
        else
            Debug.LogError(report.ToString());
    }

    // ============================================================
    // 02. CAMINHO RUNTIME E PREFABS ATIVOS
    // ============================================================
    private static void ValidateRuntimePath(StringBuilder report, ICollection<string> errors)
    {
        string presenterPath = "Assets/Scripts/UI/Home/WDHomeArmoryPanel.cs";
        string source = File.Exists(presenterPath) ? File.ReadAllText(presenterPath) : string.Empty;
        bool pathConfirmed = source.Contains($"ScreenPrefabPath = \"{RuntimeResourcesPath}\"");
        report.AppendLine($"Runtime Resources path: {RuntimeResourcesPath} ({State(pathConfirmed)})");
        if (!pathConfirmed)
            errors.Add("WDHomeArmoryPanel não confirma o caminho Resources esperado do Arsenal.");
    }

    private static void ValidateActivePrefabs(StringBuilder report, ICollection<string> errors)
    {
        ValidatePrefab(ScreenAssetPath, report, errors, true);
        ValidatePrefab(CardAssetPath, report, errors, false);
    }

    private static void ValidatePrefab(string path, StringBuilder report, ICollection<string> errors, bool requireScreenView)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        bool exists = prefab != null;
        report.AppendLine($"Prefab ativo: {path} ({State(exists)})");
        if (!exists)
        {
            errors.Add($"Prefab ativo ausente: {path}");
            return;
        }

        int missingScripts = CountMissingScripts(prefab);
        ReferenceInspection references = InspectPrefabReferences(path);
        report.AppendLine($"  Missing Scripts={missingScripts}; referências quebradas={references.Broken.Count}; indeterminadas={references.Indeterminate.Count}");
        if (missingScripts > 0)
            errors.Add($"{path} contém {missingScripts} Missing Script(s).");
        AppendReferenceDetails(report, "QUEBRADA", references.Broken);
        AppendReferenceDetails(report, "INDETERMINADA", references.Indeterminate);
        if (references.Broken.Count > 0)
            errors.Add($"{path} contém {references.Broken.Count} referência(s) serializada(s) realmente quebrada(s).");
        if (requireScreenView && prefab.GetComponent<WDArmoryScreenView>() == null)
            errors.Add("WDArmoryScreen.prefab não possui WDArmoryScreenView na raiz.");
    }

    // ============================================================
    // 03. CENAS RELEVANTES SEM ALTERAÇÃO PERSISTENTE
    // ============================================================
    private static void ValidateRelevantScenes(StringBuilder report, ICollection<string> errors)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HomeScenePath) == null)
        {
            errors.Add($"Cena relevante ausente: {HomeScenePath}");
            return;
        }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
            int missing = scene.GetRootGameObjects().Sum(CountMissingScripts);
            report.AppendLine($"Cena relevante: {HomeScenePath}; Missing Scripts={missing}");
            if (missing > 0)
                errors.Add($"{HomeScenePath} contém {missing} Missing Script(s).");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    // ============================================================
    // 04. BUILDERS E AUTORIDADES OFICIAIS
    // ============================================================
    private static void ValidateBuilderProtection(StringBuilder report, ICollection<string> errors)
    {
        bool armoryRetired = WDArmorySectorPrefabBuilder.IsRetired;
        report.AppendLine($"Builder antigo do Arsenal aposentado: {State(armoryRetired)}");
        if (!armoryRetired)
            errors.Add("WDArmorySectorPrefabBuilder ainda não está aposentado.");

        string[] editorScripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Editor" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (string path in editorScripts)
        {
            if (path.EndsWith("WDArmorySectorPrefabBuilder.cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("WDUIProtectionAudit.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            string source = File.ReadAllText(path);
            bool targetsActivePrefab = source.Contains(ScreenAssetPath) || source.Contains(CardAssetPath);
            bool writesPrefabOrAsset = PrefabWriteOperations.Any(source.Contains);
            if (targetsActivePrefab && writesPrefabOrAsset)
                errors.Add($"Script Editor pode gravar em Prefab ativo do Arsenal: {path}");
        }

        bool visualKitBuilderExists = File.Exists("Assets/Editor/UI/WDVisualKitAssetBuilder.cs");
        report.AppendLine($"Builder oficial do VisualKit preservado: {State(visualKitBuilderExists)}");
        if (!visualKitBuilderExists)
            errors.Add("WDVisualKitAssetBuilder.cs não foi encontrado.");
    }

    private static void ValidateVisualKitReferences(StringBuilder report, ICollection<string> errors)
    {
        bool themeExists = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(ThemeAssetPath) != null;
        bool catalogExists = AssetDatabase.LoadAssetAtPath<WDArmoryVisualCatalog>(VisualCatalogPath) != null;
        report.AppendLine($"Tema oficial: {ThemeAssetPath} ({State(themeExists)})");
        report.AppendLine($"Catálogo visual Arsenal: {VisualCatalogPath} ({State(catalogExists)})");
        if (!themeExists) errors.Add("Tema oficial ausente.");
        if (!catalogExists) errors.Add("Catálogo visual do Arsenal ausente.");

        string[] dependencies = AssetDatabase.GetDependencies(new[] { ScreenAssetPath, CardAssetPath }, true);
        int visualKitDependencies = dependencies.Count(path => path.StartsWith(VisualKitRoot, StringComparison.Ordinal));
        report.AppendLine($"Dependências oficiais do VisualKit nos Prefabs ativos: {visualKitDependencies}");
        if (visualKitDependencies == 0)
            errors.Add("Os Prefabs ativos do Arsenal não possuem dependências detectáveis em Assets/VisualKit.");
    }

    // ============================================================
    // 05. ARTWORKS CANÔNICOS DO ARSENAL V2
    // ============================================================
    private static void ValidateCanonicalArmoryArtworks(StringBuilder report, ICollection<string> errors)
    {
        WDArmoryVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<WDArmoryVisualCatalog>(VisualCatalogPath);
        if (catalog == null)
            return;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        int canonicalCount = 0;
        foreach (WDArmoryVisualCatalog.Entry entry in catalog.Weapons)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.WeaponId))
            {
                errors.Add("Catálogo visual contém entrada sem ID canônico.");
                continue;
            }
            if (!ids.Add(entry.WeaponId))
                errors.Add($"Catálogo visual contém ID duplicado: {entry.WeaponId}");
            if (entry.Artwork == null)
            {
                errors.Add($"Catálogo visual sem artwork: {entry.WeaponId}");
                continue;
            }

            string path = AssetDatabase.GetAssetPath(entry.Artwork).Replace('\\', '/');
            if (!path.StartsWith(CanonicalArtworkRoot, StringComparison.Ordinal))
                errors.Add($"Artwork não canônico no catálogo: {entry.WeaponId} -> {path}");
            else
                canonicalCount++;
        }

        report.AppendLine($"Artworks canônicos no catálogo visual: {canonicalCount}/{catalog.Weapons.Count}");
        if (catalog.Weapons.Count != 26)
            errors.Add($"Catálogo visual deveria conter 26 armas, mas contém {catalog.Weapons.Count}.");

        string[] v2Dependencies = AssetDatabase.GetDependencies(ArmoryV2ScreenPath, true);
        string[] legacyDependencies = v2Dependencies
            .Where(path => path.Replace('\\', '/').StartsWith(LegacyArtworkRoot, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        report.AppendLine($"Dependências antigas no Prefab V2: {legacyDependencies.Length}");
        foreach (string dependency in legacyDependencies)
            errors.Add($"Prefab V2 ainda referencia artwork antigo: {dependency}");

        WDArmoryV2VisualSettings settings = AssetDatabase.LoadAssetAtPath<WDArmoryV2VisualSettings>(ArmoryV2SettingsPath);
        if (settings == null)
        {
            errors.Add($"Configuração visual do Arsenal V2 ausente: {ArmoryV2SettingsPath}");
            return;
        }

        int configuredOverrides = 0;
        foreach (WDArmoryV2VisualSettings.ArtworkOverride artworkOverride in settings.ArtworkOverrides)
        {
            if (artworkOverride?.Sprite == null)
                continue;
            configuredOverrides++;
            string path = AssetDatabase.GetAssetPath(artworkOverride.Sprite).Replace('\\', '/');
            if (!path.StartsWith(CanonicalArtworkRoot, StringComparison.Ordinal))
                errors.Add($"Override não canônico: {artworkOverride.WeaponId} -> {path}");
        }
        report.AppendLine($"Overrides editoriais preenchidos: {configuredOverrides} (nulos são permitidos)");
    }

    // ============================================================
    // 06. INSPEÇÃO SERIALIZADA
    // ============================================================
    private static int CountMissingScripts(GameObject root)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
    }

    private static ReferenceInspection InspectPrefabReferences(string prefabPath)
    {
        var result = new ReferenceInspection();
        GameObject contents = null;
        try
        {
            // 05.1 — LoadPrefabContents é a API oficial para resolver referências internas
            // do Prefab em um escopo isolado, sem instanciar na cena ativa ou salvar mudanças.
            contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var uniqueBroken = new HashSet<string>(StringComparer.Ordinal);
            var uniqueIndeterminate = new HashSet<string>(StringComparer.Ordinal);
            foreach (Component component in contents.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;

                try
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        try
                        {
                            UnityEngine.Object resolved = property.objectReferenceValue;
                            bool hasSerializedTarget = !property.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId));
                            if (resolved == null && hasSerializedTarget)
                                uniqueBroken.Add(DescribeReference(contents.transform, component, property.propertyPath));
                        }
                        catch (Exception exception)
                        {
                            uniqueIndeterminate.Add(DescribeReference(contents.transform, component, property.propertyPath) +
                                $" — {exception.GetType().Name}: {exception.Message}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    uniqueIndeterminate.Add($"{GetHierarchyPath(contents.transform, component.transform)} | " +
                        $"{component.GetType().FullName} | <componente> — {exception.GetType().Name}: {exception.Message}");
                }
            }

            result.Broken.AddRange(uniqueBroken.OrderBy(value => value, StringComparer.Ordinal));
            result.Indeterminate.AddRange(uniqueIndeterminate.OrderBy(value => value, StringComparer.Ordinal));
        }
        catch (Exception exception)
        {
            result.Indeterminate.Add($"{prefabPath} | <prefab> | <carregamento> — {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (contents != null)
                PrefabUtility.UnloadPrefabContents(contents);
        }

        return result;
    }

    private static string DescribeReference(Transform prefabRoot, Component component, string propertyPath)
    {
        return $"{GetHierarchyPath(prefabRoot, component.transform)} | {component.GetType().FullName} | {propertyPath}";
    }

    private static string GetHierarchyPath(Transform root, Transform current)
    {
        var names = new Stack<string>();
        Transform cursor = current;
        while (cursor != null)
        {
            names.Push(cursor.name);
            if (cursor == root)
                break;
            cursor = cursor.parent;
        }
        return string.Join("/", names);
    }

    private static void AppendReferenceDetails(StringBuilder report, string classification, IReadOnlyCollection<string> references)
    {
        foreach (string reference in references)
            report.AppendLine($"    {classification}: {reference}");
    }

    private sealed class ReferenceInspection
    {
        public readonly List<string> Broken = new List<string>();
        public readonly List<string> Indeterminate = new List<string>();
    }

    private static string State(bool value) => value ? "OK" : "FALHA";
}
#endif

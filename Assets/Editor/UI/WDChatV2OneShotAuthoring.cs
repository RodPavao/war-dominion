#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class WDChatV2OneShotAuthoring
{
    private const string PrefabPath = "Assets/Resources/UI/Chat/WDChatHubV2.prefab";
    private const string BackgroundPath = "Assets/Art/Backgrounds/Chat/Chat.png";
    private const string TypographyRoot = "Assets/VisualKit/Typography/";
    private const string ChatSpriteRoot = "Assets/VisualKit/Sprites/ChatV2";
    private const string SharedFill12 = "Assets/VisualKit/Sprites/RoundedFill_R12.png";
    private const string SharedOutline12 = "Assets/VisualKit/Sprites/RoundedOutline_R12.png";
    private const string ThemePath = "Assets/Resources/UI/WarDominionUITheme.asset";

    // ============================================================
    // 01. AUTORIA EXPLÍCITA E EXECUTADA UMA ÚNICA VEZ
    // ============================================================
    [MenuItem("War Dominion/UI/Chat V2/Criar Prefab Serializado")]
    public static void CreateSerializedPrefab()
    {
        EnsureFolder("Assets/Resources/UI/Chat");
        GameObject root = new("WDChatHubV2", typeof(RectTransform));
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(1920f, 1080f);

            // 02. Dados editoriais existem apenas para materializar a View; não são salvos como autoridade.
            WDProfileRepository profiles = new(null, new WDMemoryProfilePrivacyStore());
            string friendId = profiles.AllProfiles.First(profile => !profile.IsLocalPlayer).PlayerId;
            WDSocialRepository social = new(profiles, new[]
            {
                new WDSocialMockSeed(friendId, WDProfileSocialState.Friend, WDSocialPresence.Online)
            });
            WDSettingsData settings = WDSettingsData.Defaults();
            WDChatRepository chat = new(profiles, social, () => settings);
            WDNavigationStack navigation = new();
            navigation.SetRoot(new WDNavigationContext("home", _ => { }));

            WDChatHubPanel panel = root.AddComponent<WDChatHubPanel>();
            WarDominionUITheme theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(ThemePath) ??
                throw new InvalidOperationException("Tema Premium Tactical ausente: " + ThemePath);
            panel.Build(theme, chat, social, profiles, navigation,
                _ => { }, (_, __, accept) => accept?.Invoke(), () => { },
                AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath));
            PersistTypography(root);
            PersistSprites(root);
            // 02.1. Salva inativo para impedir Refresh com dados editoriais dentro do asset.
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WD-CHAT-V2] Prefab serializado criado em " + PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // ============================================================
    // 03. FONTES FÍSICAS DO VISUALKIT; SEM TMP TEMPORÁRIO
    // ============================================================
    private static void PersistTypography(GameObject root)
    {
        TMP_FontAsset interRegular = Font("Inter-Regular SDF.asset");
        TMP_FontAsset interSemiBold = Font("Inter-SemiBold SDF.asset");
        TMP_FontAsset gabaritoBold = Font("Gabarito-Bold SDF.asset");
        TMP_FontAsset gabaritoBlack = Font("Gabarito-Black SDF.asset");
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            bool title = text.name == "Title" || text.transform.parent?.name == "HeaderTitleGroup";
            bool control = text.transform.parent?.GetComponent<Button>() != null;
            bool emphasized = text.fontStyle.HasFlag(FontStyles.Bold) ||
                (text.font != null && text.font.name.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0);
            text.font = title ? gabaritoBlack : control ? gabaritoBold :
                emphasized ? interSemiBold : interRegular;
            text.fontSharedMaterial = text.font.material;
            EditorUtility.SetDirty(text);
        }
    }

    private static TMP_FontAsset Font(string name) =>
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TypographyRoot + name) ??
        throw new InvalidOperationException("Fonte VisualKit ausente: " + name);

    // ============================================================
    // 04. PERSISTÊNCIA DOS 9-SLICES GERADOS PELA COMPOSIÇÃO APROVADA
    // ============================================================
    private static void PersistSprites(GameObject root)
    {
        EnsureFolder(ChatSpriteRoot);
        List<string> createdAssets = new();
        try
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                Sprite transient = image.sprite;
                if (transient == null || EditorUtility.IsPersistent(transient)) continue;
                Texture2D texture = transient.texture;
                if (texture == null || !texture.name.StartsWith("WDHome", StringComparison.Ordinal))
                    throw new InvalidOperationException("Sprite transitório não classificado em " + FullPath(image.transform));

                int radius = ParseTrailingNumber(texture.name);
                bool outline = texture.name.Contains("Border", StringComparison.Ordinal);
                Sprite persistent = radius == 12
                    ? AssetDatabase.LoadAssetAtPath<Sprite>(outline ? SharedOutline12 : SharedFill12)
                    : null;
                if (persistent == null)
                {
                    string assetName = texture.name.Replace("WDHome", "ChatV2_") + ".png";
                    string assetPath = ChatSpriteRoot + "/" + assetName;
                    persistent = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (persistent == null)
                    {
                        byte[] png = EncodeNonReadableTexture(texture);
                        File.WriteAllBytes(assetPath, png);
                        createdAssets.Add(assetPath);
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter ??
                            throw new InvalidOperationException("Importador de textura indisponível: " + assetPath);
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false;
                        importer.spritePixelsPerUnit = 100f;
                        importer.spriteBorder = Vector4.one * (radius + 1f);
                        importer.SaveAndReimport();
                        persistent = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    }
                }
                image.sprite = persistent ?? throw new InvalidOperationException(
                    "Sprite persistente não pôde ser resolvido para " + FullPath(image.transform));
                image.type = Image.Type.Sliced;
                EditorUtility.SetDirty(image);
            }
        }
        catch (Exception exception)
        {
            foreach (string path in createdAssets) AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            throw new InvalidOperationException(
                "Falha ao persistir sprites do Chat V2. Assets criados nesta tentativa foram revertidos.", exception);
        }
    }

    // 04.1. Readback isolado: não altera isReadable nem o importador da textura de origem.
    private static byte[] EncodeNonReadableTexture(Texture2D source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D readable = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            readable.Apply(false, false);
            return readable.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    private static int ParseTrailingNumber(string value)
    {
        string digits = new(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out int result) ? result : 12;
    }

    private static string FullPath(Transform value) => value.parent == null
        ? value.name : FullPath(value.parent) + "/" + value.name;

    // ============================================================
    // 05. PASTAS FÍSICAS SEM REGERAR OUTROS RECURSOS
    // ============================================================
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif

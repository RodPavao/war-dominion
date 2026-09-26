using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

// 01. Unicode permanece conteúdo literal; fonte incorporada e fallback restritos ao Chat.
public static class WDChatEmojiText
{
    private static TMP_FontAsset emoji;
    private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> fonts = new();
    private static readonly HashSet<TMP_FontAsset> ownedFonts = new();

    private static bool lifetimeRegistered;

    private static void RegisterLifetime()
    {
        if (lifetimeRegistered) return;
        lifetimeRegistered = true;
        // 01.1. O cache possui o grafo inteiro, inclusive atlas criados depois pelo TMP.
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ProtectOwnedResources);
        Application.quitting += ReleaseOwnedResources;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseOwnedResources;
#endif
    }

    public static void Apply(TMP_Text text)
    {
        RegisterLifetime();
        text.richText = false;
        if (text.font == null) return;
        if (emoji == null)
        {
            var source = Resources.Load<Font>("UI/ChatEmoji/NotoEmoji-Regular");
            if (source == null) return;
            emoji = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048);
            emoji.name = "Chat Noto Emoji Monochrome";
            emoji.hideFlags = HideFlags.DontSave;
            ownedFonts.Add(emoji);
            emoji.isMultiAtlasTexturesEnabled = true;
            // Seletores de apresentação/junção não viram tofu; não alteram o texto armazenado.
            var invisible = new Glyph(0, new GlyphMetrics(0, 0, 0, 0, 0), new GlyphRect(0, 0, 0, 0), 1, 0);
            emoji.glyphTable.Add(invisible);
            foreach (uint code in new uint[] { 0xFE0E, 0xFE0F, 0x200D })
                emoji.characterTable.Add(new TMP_Character(code, emoji, invisible));
            emoji.ReadFontAssetDefinition();
        }
        if (!fonts.TryGetValue(text.font, out var local))
        {
            local = CreateOwnedFont(text.font);
            ownedFonts.Add(local);
            local.fallbackFontAssetTable = new List<TMP_FontAsset>(text.font.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
            local.fallbackFontAssetTable.Add(emoji);
            fonts[text.font] = local;
            fonts[local] = local;
        }
        ProtectOwnedResources(null);
        text.font = local;
        WDChatEmojiColor.Apply(text);
    }

    // 01.2. O clone possui material/atlas próprios. Fonte TTF e fallbacks são emprestados.
    private static TMP_FontAsset CreateOwnedFont(TMP_FontAsset source)
    {
        var local = UnityEngine.Object.Instantiate(source);
        local.name = source.name + " Chat Emoji";
        local.hideFlags = HideFlags.DontSave;
        // Desvincula imediatamente os recursos alheios, inclusive em caso de falha.
        var atlases = new Texture2D[source.atlasTextureCount];
        local.atlasTextures = atlases;
        local.material = null;
        bool completed = false;
        try
        {
            for (int i = 0; i < atlases.Length; i++) atlases[i] = CopyAtlas(source.atlasTextures[i]);
            local.material = new Material(source.material) { hideFlags = HideFlags.DontSave };
            local.material.mainTexture = atlases[0];
            completed = true;
            return local;
        }
        finally
        {
            if (!completed) UnityEngine.Object.DestroyImmediate(local);
        }
    }

    private static Texture2D CopyAtlas(Texture2D source)
    {
        if (source.isReadable)
        {
            var copy = UnityEngine.Object.Instantiate(source);
            copy.hideFlags = HideFlags.DontSave;
            return copy;
        }
        // Atlas importados (ex.: LiberationSans) podem existir apenas na GPU.
        // ReadPixels lê o render target próprio, nunca os pixels CPU da origem.
        var previous = RenderTexture.active;
        var staging = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D result = null;
        bool completed = false;
        try
        {
            Graphics.Blit(source, staging);
            RenderTexture.active = staging;
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
            {
                name = source.name + " Chat Copy",
                hideFlags = HideFlags.DontSave,
                filterMode = source.filterMode,
                wrapMode = source.wrapMode,
                anisoLevel = source.anisoLevel
            };
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            result.Apply(false, false);
            completed = true;
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(staging);
            if (!completed && result != null) UnityEngine.Object.DestroyImmediate(result);
        }
    }

    private static void ProtectOwnedResources(UnityEngine.Object changed)
    {
        foreach (var font in ownedFonts)
        {
            font.hideFlags = HideFlags.DontSave;
            font.material.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < font.atlasTextureCount; i++)
                font.atlasTextures[i].hideFlags = HideFlags.DontSave;
        }
    }

    private static void ReleaseOwnedResources()
    {
        // A liberação acontece no fim da sessão/reload, nunca no teardown de um texto.
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ProtectOwnedResources);
        Application.quitting -= ReleaseOwnedResources;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseOwnedResources;
#endif
        fonts.Clear();
        emoji = null;
        foreach (var font in ownedFonts) UnityEngine.Object.DestroyImmediate(font);
        ownedFonts.Clear();
        lifetimeRegistered = false;
    }

    // 02. Edição atômica em limites de elementos Unicode, sem cortar pares surrogate.
    public static bool TryInsert(TMP_InputField field, string value, int anchor, int focus, out int caret)
    {
        caret = 0;
        if (field == null || !field.interactable || field.readOnly || string.IsNullOrEmpty(value)) return false;
        string original = field.text ?? string.Empty;
        int start = Mathf.Clamp(Math.Min(anchor, focus), 0, original.Length);
        int end = Mathf.Clamp(Math.Max(anchor, focus), 0, original.Length);
        var boundaries = new List<int>(StringInfo.ParseCombiningCharacters(original)) { original.Length };
        int snappedStart = 0;
        foreach (int boundary in boundaries) if (boundary <= start) snappedStart = boundary;
        if (start == end) end = snappedStart;
        else foreach (int boundary in boundaries) if (boundary >= end) { end = boundary; break; }
        start = snappedStart;
        if (field.characterLimit > 0 && original.Length - (end - start) + value.Length > field.characterLimit) return false;
        // Validadores especializados precisam aceitar o emoji inteiro. Nunca burlamos filtros.
        if (field.characterValidation != TMP_InputField.CharacterValidation.None || field.inputValidator != null) return false;
        string prefix = original.Substring(0, start);
        if (field.onValidateInput != null)
            foreach (char c in value)
            {
                if (field.onValidateInput(prefix, prefix.Length, c) != c) return false;
                prefix += c;
            }
        field.text = original.Substring(0, start) + value + original.Substring(end);
        caret = start + value.Length;
        field.stringPosition = caret;
        return true;
    }
}

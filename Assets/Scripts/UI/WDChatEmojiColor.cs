using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 01. Atlas compartilhado de apresentação; mensagens e TMP_InputField.text continuam Unicode.
public static class WDChatEmojiColor
{
    [Serializable] private sealed class Manifest { public Entry[] entries = Array.Empty<Entry>(); }
    [Serializable] private sealed class Entry
    {
        public string unicode = string.Empty;
        public int index = 0, x = 0, y = 0, width = 0, height = 0;
    }
    private static TMP_SpriteAsset asset;
    private static Entry[] entries;
    private static Dictionary<char, List<Entry>> starts = new();
    private static bool registered;
    public static TMP_SpriteAsset SpriteAsset { get { Ensure(); return asset; } }

    public static void Apply(TMP_Text text)
    {
        Ensure();
        text.richText = false;
        text.tintAllSprites = false;
        text.spriteAsset = asset;
        if (!(text.textPreprocessor is Presentation)) text.textPreprocessor = new Presentation();
    }

    // 02. Aliases só no buffer interno TMP, com o mesmo comprimento UTF-16 do emoji.
    // Isso mantém characterInfo.index/stringLength compatíveis com o input lógico,
    // inclusive ❤️ e ✌️ (dois chars BMP -> um scalar suplementar de dois chars).
    // Faixas auditadas contra Inter, LiberationSans e Noto; E000 pertence ao Inter.
    private static uint Alias(Entry entry) => (uint)(entry.unicode.Length == 1 ? 0xF000 : 0xF0000) + (uint)entry.index;

    private sealed class Presentation : ITextPreprocessor
    {
        private string previous, rendered;
        public string PreprocessText(string text)
        {
            if (text == previous) return rendered;
            Ensure();
            StringBuilder output = null;
            for (int i = 0; i < text.Length;)
            {
                Entry match = null;
                if (starts.TryGetValue(text[i], out var candidates))
                    foreach (var entry in candidates)
                        if (i + entry.unicode.Length <= text.Length &&
                            string.CompareOrdinal(text, i, entry.unicode, 0, entry.unicode.Length) == 0)
                        { match = entry; break; }
                if (match != null)
                {
                    int end = i + match.unicode.Length;
                    int next = end < text.Length ? (char.IsHighSurrogate(text[end]) && end + 1 < text.Length && char.IsLowSurrogate(text[end + 1])
                        ? char.ConvertToUtf32(text, end) : text[end]) : 0;
                    // Sequências fora do catálogo usam o fallback, sem decompor uma
                    // família/modificador desconhecido em um emoji colorido diferente.
                    if ((i > 0 && text[i - 1] == '\u200D') || next == 0x200D || next == 0xFE0E ||
                        next == 0xFE0F || (next >= 0x1F3FB && next <= 0x1F3FF)) match = null;
                }
                int length = match?.unicode.Length ?? (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1);
                uint scalar = (uint)(length == 2 && char.IsHighSurrogate(text[i]) ? char.ConvertToUtf32(text, i) : text[i]);
                bool reserved = (scalar >= 0xF000 && scalar < 0xF000 + entries.Length) ||
                    (scalar >= 0xF0000 && scalar < 0xF0000 + entries.Length);
                if (match != null || reserved)
                {
                    if (output == null) output = new StringBuilder(text.Length).Append(text, 0, i);
                    // PUA digitado não pode fingir ser alias produzido pela UI.
                    output.Append(char.ConvertFromUtf32((int)(match != null ? Alias(match) : length == 1 ? 0xF8FFu : 0x10FFFDu)));
                }
                else if (output != null) output.Append(text, i, length);
                i += length;
            }
            previous = text;
            rendered = output?.ToString() ?? text;
            return rendered;
        }
    }

    // 03. Asset nativo TMP 1.1.0 importado. Publicar o cache só após validar tudo:
    // uma falha de inicialização nunca pode deixar um parser parcialmente pronto.
    private static void Ensure()
    {
        if (asset != null) return;
        const string root = "UI/Chat/Emoji/NotoColor/";
        var candidate = Resources.Load<TMP_SpriteAsset>(root + "ChatNotoColor");
        var catalog = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>(root + "manifest").text).entries;
        if (candidate == null || candidate.version != "1.1.0" || candidate.material == null ||
            candidate.spriteSheet == null || candidate.material.mainTexture != candidate.spriteSheet)
            throw new InvalidOperationException("Chat Noto Color requires a native TMP 1.1.0 asset with its atlas/material.");
        candidate.UpdateLookupTables();
        if (candidate.spriteCharacterTable.Count != catalog.Length || candidate.spriteGlyphTable.Count != catalog.Length)
            throw new InvalidOperationException("Chat Noto Color tables do not match the catalog.");
        var lookup = new Dictionary<char, List<Entry>>();
        foreach (var entry in catalog)
        {
            if (entry.unicode.Length < 1 || entry.unicode.Length > 2)
                throw new InvalidOperationException("Emoji sequence needs an explicit UTF-16 editing strategy: " + entry.unicode);
            if (!candidate.spriteCharacterLookupTable.TryGetValue(Alias(entry), out var character) ||
                character.glyphIndex != (uint)entry.index || character.glyph == null ||
                character.glyph.glyphRect.x != entry.x || character.glyph.glyphRect.y != entry.y ||
                character.glyph.glyphRect.width != entry.width || character.glyph.glyphRect.height != entry.height)
                throw new InvalidOperationException("Chat Noto Color glyph mismatch: " + entry.unicode);
            if (!lookup.TryGetValue(entry.unicode[0], out var list)) lookup[entry.unicode[0]] = list = new List<Entry>();
            list.Add(entry);
        }
        entries = catalog;
        starts = lookup;
        asset = candidate;
        if (!registered)
        {
            registered = true;
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }
    }

    private static void Release()
    {
        // Resources possui o asset, atlas e material. Liberar só referências locais.
        asset = null;
        entries = null;
        starts.Clear();
        Application.quitting -= Release;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Release;
#endif
        registered = false;
    }
}

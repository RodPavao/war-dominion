using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class WDChatEmojiPickerTests
{
    // 01. Host independente de Match/Clan/repository, reproduzindo o campo TMP e eventos UI.
    private sealed class Fixture : IDisposable
    {
        public GameObject Root = new("ChatHost", typeof(RectTransform));
        public GameObject Events = new("Events", typeof(EventSystem));
        public TMP_InputField Input;
        public WDChatEmojiPicker Picker;
        public List<string> Recent = new();
        public int Submitted;
        public Fixture()
        {
            ((RectTransform)Root.transform).sizeDelta = new Vector2(1400, 760);
            var rect = WDUIFactory.Rect("Input", Root.transform);
            Input = rect.gameObject.AddComponent<TMP_InputField>();
            var label = WDUIFactory.Rect("Text", rect).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = WDHomeTypography.Load("Inter-Regular"); label.richText = false;
            Input.textComponent = label; Input.textViewport = (RectTransform)label.transform;
            Input.richText = false;
            Input.onSubmit.AddListener(_ => Submitted++);
            var button = WDUIFactory.Rect("EmojiButton", Root.transform).gameObject.AddComponent<Button>();
            Picker = button.gameObject.AddComponent<WDChatEmojiPicker>();
            Picker.Build(Input, button, (RectTransform)Root.transform, Recent);
        }
        public void Set(string text, int a, int b)
        {
            Input.text = text; Input.selectionStringAnchorPosition = a; Input.selectionStringFocusPosition = b;
            Picker.CaptureSelection();
        }
        public void Click(Transform target) => ExecuteEvents.ExecuteHierarchy(target.gameObject,
            new PointerEventData(Events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        public void Dispose() { Object.DestroyImmediate(Root); Object.DestroyImmediate(Events); }
    }

    // 02. Toggle, categorias, histórico local real e cliques limitados ao picker.
    [Test]
    public void ToggleCategoriesEmptyRecentsAndOutsideClickStayLocal()
    {
        using var f = new Fixture();
        f.Picker.Toggle(); Assert.That(f.Picker.IsOpen, Is.True);
        f.Picker.SelectCategory(0);
        Assert.That(f.Picker.Recents, Is.Empty);
        Assert.That(f.Picker.Category, Is.Zero);
        var layer = f.Root.transform.Find("EmojiPickerLayer");
        f.Click(layer.Find("Popup")); Assert.That(f.Picker.IsOpen, Is.True);
        f.Click(layer); Assert.That(f.Picker.IsOpen, Is.False);
        Assert.That(f.Root.activeSelf, Is.True);
        f.Picker.Toggle(); f.Picker.Toggle(); Assert.That(f.Picker.IsOpen, Is.False);
    }

    [TestCase("Boa partida!", 3, 3, "🔥", "Boa🔥 partida!", 5)]
    [TestCase("Boa partida!", 4, 11, "👍", "Boa 👍!", 6)]
    [TestCase("Boa partida!", 11, 4, "❤️", "Boa ❤️!", 6)]
    [TestCase("A😀B", 2, 2, "🔥", "A🔥😀B", 3)]
    public void InsertionPreservesUnicodeAndSelectionWithoutSending(string before, int a, int b, string emoji, string after, int caret)
    {
        using var f = new Fixture(); f.Set(before, a, b); f.Picker.Toggle();
        Assert.That(f.Picker.Choose(emoji), Is.True);
        Assert.That(f.Input.text, Is.EqualTo(after));
        Assert.That(f.Input.stringPosition, Is.EqualTo(caret));
        Assert.That(f.Submitted, Is.Zero);
        Assert.That(f.Input.richText, Is.False);
        Assert.That(f.Input.textComponent.richText, Is.False);
    }

    [Test]
    public void LimitRejectsWholeEmojiAndValidatorsCannotBeBypassed()
    {
        using var f = new Fixture(); f.Set("abc", 3, 3); f.Input.characterLimit = 4; f.Picker.Toggle();
        Assert.That(f.Picker.Choose("🔥"), Is.False);
        Assert.That(f.Input.text, Is.EqualTo("abc")); Assert.That(f.Recent, Is.Empty);
        f.Input.characterLimit = 0; f.Input.onValidateInput = (_, _, _) => '\0';
        Assert.That(f.Picker.Choose("🔥"), Is.False);
        f.Input.onValidateInput = null; f.Input.characterValidation = TMP_InputField.CharacterValidation.Integer;
        Assert.That(f.Picker.Choose("🔥"), Is.False);
        f.Input.characterValidation = TMP_InputField.CharacterValidation.None; f.Input.interactable = false;
        Assert.That(f.Picker.Choose("🔥"), Is.False);
        Assert.That(f.Input.text, Is.EqualTo("abc"));
    }

    [Test]
    public void RecentsAreUniqueBoundedAndRecordOnlyActualSelections()
    {
        using var f = new Fixture();
        foreach (var category in WDChatEmojiPicker.Catalog)
            foreach (var emoji in category) { f.Picker.Toggle(); Assert.That(f.Picker.Choose(emoji), Is.True); }
        Assert.That(f.Recent.Count, Is.EqualTo(16));
        f.Picker.Toggle(); f.Picker.Choose("🔥"); f.Picker.Toggle(); f.Picker.Choose("🔥");
        Assert.That(f.Recent[0], Is.EqualTo("🔥"));
        Assert.That(f.Recent.FindAll(x => x == "🔥").Count, Is.EqualTo(1));
        Assert.That(f.Recent.Count, Is.EqualTo(16));
    }

    [Test]
    public void PopupStaysInsideHostAndProvidesTouchTargets()
    {
        using var f = new Fixture(); f.Picker.Toggle();
        var host = (RectTransform)f.Root.transform;
        var popup = (RectTransform)host.Find("EmojiPickerLayer/Popup");
        var corners = new Vector3[4]; popup.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var point = host.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(host.rect.xMin, host.rect.xMax));
            Assert.That(point.y, Is.InRange(host.rect.yMin, host.rect.yMax));
        }
        foreach (var button in popup.GetComponentsInChildren<Button>())
        {
            var rect = ((RectTransform)button.transform).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(44));
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(44));
        }
        Assert.That(popup.GetComponent<Image>().color.a, Is.EqualTo(.95f).Within(.001f));
        Assert.That(popup.GetComponentInChildren<ScrollRect>().vertical, Is.True);
    }

    [Test]
    public void CachedFontGraphSurvivesFixtureAndSceneTeardown()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewPreviewScene();
        TMP_FontAsset cached;
        TMP_SpriteAsset sprites;
        try
        {
            using var first = new Fixture();
            SceneManager.MoveGameObjectToScene(first.Root, scene);
            SceneManager.MoveGameObjectToScene(first.Events, scene);
            cached = first.Input.textComponent.font;
            sprites = first.Input.textComponent.spriteAsset;
            first.Input.textComponent.text = "Primeira 😀❤️";
            first.Input.textComponent.GetPreferredValues(600, 100);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previous));
        using var second = new Fixture();
        Assert.That(second.Input.textComponent.font, Is.SameAs(cached));
        Assert.That(second.Input.textComponent.spriteAsset, Is.SameAs(sprites));
        Assert.That(sprites.material != null && sprites.spriteSheet != null, Is.True);
        foreach (var font in new[] { cached, cached.fallbackFontAssetTable[cached.fallbackFontAssetTable.Count - 1] })
        {
            Assert.That(font.material != null, Is.True);
            Assert.That(font.material.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            for (int i = 0; i < font.atlasTextureCount; i++)
            {
                var atlas = font.atlasTextures[i];
                Assert.That(atlas != null, Is.True);
                Assert.That(atlas.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            }
        }
        Assert.That(cached.HasCharacters("Reaberto 🤯🔥", out uint[] missing, true, true), Is.True);
        second.Input.textComponent.text = "Reaberto 🤯🔥";
        Assert.That(second.Input.textComponent.GetPreferredValues(600, 100).x, Is.GreaterThan(0));
    }

    [Test]
    public void ChatFontOwnsAtlasAndMaterialIndependentlyOfSource()
    {
        var source = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("UI/Fonts/Inter-Regular"));
        var root = new GameObject("IndependentFont", typeof(RectTransform));
        try
        {
            var label = root.AddComponent<TextMeshProUGUI>();
            label.font = source;
            WDChatEmojiText.Apply(label);
            Assert.That(label.font.material, Is.Not.SameAs(source.material));
            Assert.That(label.font.atlasTexture, Is.Not.SameAs(source.atlasTexture));
            Object.DestroyImmediate(source);
            Assert.That(label.font.HasCharacters("Fonte própria 😀", out uint[] missing, true, true), Is.True);
            Assert.That(label.GetPreferredValues("Fonte própria 😀").x, Is.GreaterThan(0));
        }
        finally
        {
            Object.DestroyImmediate(root);
            if (source != null) Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void NonReadableProjectAtlasHasIndependentCopyWithoutChangingOriginal()
    {
        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var atlas = source.atlasTexture;
        var material = source.material;
        Assert.That(atlas.isReadable, Is.False);
        var factory = typeof(WDChatEmojiText).GetMethod("CreateOwnedFont",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        TMP_FontAsset copy = null;
        try
        {
            copy = (TMP_FontAsset)factory.Invoke(null, new object[] { source });
            Assert.That(copy.atlasTexture, Is.Not.SameAs(atlas));
            Assert.That(copy.material, Is.Not.SameAs(material));
            Assert.That(copy.material.mainTexture, Is.SameAs(copy.atlasTexture));
            Assert.That(copy.atlasTexture.width, Is.EqualTo(atlas.width));
            Assert.That(copy.atlasTexture.height, Is.EqualTo(atlas.height));
            Assert.That(copy.HasCharacters("ABC 123", out uint[] missing, true, true), Is.True);
            Assert.That(Array.Exists(copy.atlasTexture.GetPixels32(), pixel => pixel.a > 0), Is.True);
        }
        finally { if (copy != null) Object.DestroyImmediate(copy); }
        Assert.That(atlas != null, Is.True);
        Assert.That(material != null, Is.True);
        Assert.That(source.atlasTexture, Is.SameAs(atlas));
        Assert.That(source.material, Is.SameAs(material));
        Assert.That(material.mainTexture, Is.SameAs(atlas));
        Assert.That(atlas.isReadable, Is.False);
        Assert.That(source.HasCharacters("ABC 123", out uint[] originalMissing, true, true), Is.True);
    }

    [Test]
    public void ColorAssetIsNativeAndCacheReleaseDoesNotDestroyImportedResources()
    {
        const string path = "Assets/Resources/UI/Chat/Emoji/NotoColor/ChatNotoColor.asset";
        var imported = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);
        Assert.That(imported, Is.Not.Null);
        Assert.That(imported.version, Is.EqualTo("1.1.0"));
        Assert.That(UnityEditor.EditorUtility.IsPersistent(imported), Is.True);
        var material = imported.material;
        var atlas = imported.spriteSheet;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            typeof(WDChatEmojiColor).GetMethod("Release", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, null);
            var shared = WDChatEmojiColor.SpriteAsset;
            Assert.That(shared, Is.Not.SameAs(imported));
            Assert.That(imported.spriteGlyphTable.Count, Is.EqualTo(34));
            Assert.That(imported.spriteGlyphTable[0].metrics.height, Is.EqualTo(120));
            shared.UpdateLookupTables();
            Assert.That(shared.spriteCharacterTable.Count, Is.EqualTo(34));
            Assert.That(shared.spriteGlyphTable.Count, Is.EqualTo(34));
            Assert.That(shared.spriteCharacterLookupTable.Count, Is.EqualTo(34));
            foreach (var character in shared.spriteCharacterTable)
            {
                Assert.That(character.elementType, Is.EqualTo(TextElementType.Sprite));
                Assert.That(character.textAsset, Is.SameAs(shared));
                Assert.That(character.glyph, Is.SameAs(shared.spriteGlyphTable[(int)character.glyphIndex]));
            }
            Assert.That(material != null && atlas != null, Is.True);
            Assert.That(shared.material, Is.SameAs(material));
            Assert.That(material.mainTexture, Is.SameAs(atlas));
        }
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void AllCatalogEntriesResolveToSharedColorSpritesWithStableUnicodeIndices()
    {
        using var f = new Fixture();
        var text = f.Input.textComponent;
        var shared = WDChatEmojiColor.SpriteAsset;
        Assert.That(shared.spriteCharacterTable.Count, Is.EqualTo(34));
        foreach (var category in WDChatEmojiPicker.Catalog)
            foreach (var value in category)
            {
                text.text = "A" + value + "B";
                string logical = text.text;
                string rendered = text.textPreprocessor.PreprocessText(logical);
                Assert.That(rendered.Length, Is.EqualTo(logical.Length));
                text.GetPreferredValues(600, 100);
                var info = text.textInfo.characterInfo;
                Assert.That(info[1].elementType, Is.EqualTo(TMP_TextElementType.Sprite), value);
                Assert.That(info[1].index, Is.EqualTo(1));
                Assert.That(info[1].stringLength, Is.EqualTo(value.Length));
                Assert.That(info[1].textElement.textAsset, Is.SameAs(shared));
                Assert.That(info[2].index, Is.EqualTo(1 + value.Length));
                Assert.That(text.text, Is.EqualTo(logical));
                Assert.That(text.richText, Is.False);
            }
        f.Picker.Toggle();
        var cell = f.Root.transform.Find("EmojiPickerLayer/Popup/Viewport/Grid/Emoji0/Label").GetComponent<TMP_Text>();
        Assert.That(cell.spriteAsset.material, Is.SameAs(shared.material));
        Assert.That(cell.spriteAsset.spriteSheet, Is.SameAs(shared.spriteSheet));
        Assert.That(cell.spriteAsset.spriteGlyphTable[0].metrics.height, Is.EqualTo(120));
        Assert.That(cell.fontSize, Is.EqualTo(28));
        cell.GetPreferredValues(48, 48);
        Assert.That(cell.textInfo.characterInfo[0].elementType, Is.EqualTo(TMP_TextElementType.Sprite));
    }

    [TestCase(14f)]
    [TestCase(20f)]
    [TestCase(28f)]
    public void ColorSpritesUseProportionalFaceMetricsAndStableUnicodeElements(float size)
    {
        using var f = new Fixture();
        f.Root.AddComponent<Canvas>();
        var text = f.Input.textComponent;
        text.rectTransform.sizeDelta = new Vector2(600, 100);
        text.fontSize = size;
        var asset = WDChatEmojiColor.SpriteAsset;
        Assert.That(asset.faceInfo.pointSize, Is.EqualTo(100));
        Assert.That(asset.faceInfo.scale, Is.EqualTo(1));
        Assert.That(asset.faceInfo.baseline, Is.Zero);
        Assert.That(asset.faceInfo.ascentLine, Is.EqualTo(108));
        Assert.That(asset.faceInfo.descentLine, Is.EqualTo(-12));
        Assert.That(asset.spriteGlyphTable.Count, Is.EqualTo(34));
        Assert.That(asset.spriteCharacterTable.Count, Is.EqualTo(34));
        foreach (var glyph in asset.spriteGlyphTable)
        {
            Assert.That(glyph.scale, Is.EqualTo(1));
            Assert.That(glyph.metrics.width, Is.EqualTo(150));
            Assert.That(glyph.metrics.height, Is.EqualTo(150));
            Assert.That(glyph.metrics.horizontalBearingY, Is.EqualTo(123));
            Assert.That(glyph.metrics.horizontalAdvance, Is.EqualTo(150));
        }
        foreach (var category in WDChatEmojiPicker.Catalog)
        foreach (var value in category)
        {
            text.text = "A" + value + "B";
            text.ForceMeshUpdate();
            var info = text.textInfo.characterInfo;
            Assert.That(text.textInfo.characterCount, Is.EqualTo(3), value);
            Assert.That(info[1].elementType, Is.EqualTo(TMP_TextElementType.Sprite), value);
            Assert.That(info[1].index, Is.EqualTo(1));
            Assert.That(info[1].stringLength, Is.EqualTo(value.Length));
            Assert.That(info[2].index, Is.EqualTo(1 + value.Length));
            Assert.That(info[1].topRight.y - info[1].bottomLeft.y, Is.EqualTo(size * 1.5f).Within(.01f), value);
            Assert.That(info[1].textElement.scale, Is.EqualTo(1));
            Assert.That(text.text, Is.EqualTo("A" + value + "B"));
        }
    }

    [TestCase(14f)]
    [TestCase(20f)]
    public void InlineEmojiEnlargementPreservesLineMetricsAndImportedAtlas(float size)
    {
        using var f = new Fixture();
        var text = f.Input.textComponent;
        text.fontSize = size;
        text.text = "A😎B";
        WDChatEmojiColor.Apply(text, picker: true);
        var original = text.spriteAsset;
        float originalHeight = text.GetPreferredValues(600, 100).y;
        WDChatEmojiColor.Apply(text);
        Assert.That(text.GetPreferredValues(600, 100).y, Is.EqualTo(originalHeight).Within(.01f));
        Assert.That(text.spriteAsset.spriteSheet, Is.SameAs(original.spriteSheet));
        Assert.That(text.spriteAsset.material, Is.SameAs(original.material));
        Assert.That(original.spriteGlyphTable[0].metrics.height, Is.EqualTo(120));
        Assert.That(text.spriteAsset.spriteGlyphTable[0].metrics.height, Is.EqualTo(150));
        Assert.That(text.text, Is.EqualTo("A😎B"));
        Assert.That(text.fontSize, Is.EqualTo(size));
    }

    [Test]
    public void UserMarkupUnknownSequencesAndPrivateAliasesCannotForgePresentation()
    {
        using var f = new Fixture();
        var text = f.Input.textComponent;
        const string literal = "<sprite=0><b>x</b> 🦊 👍🏽 👨‍👩‍👦";
        Assert.That(text.textPreprocessor.PreprocessText(literal), Is.EqualTo(literal));
        text.text = literal;
        Assert.That(text.richText, Is.False);
        string alias = text.textPreprocessor.PreprocessText("😁");
        Assert.That(alias, Is.Not.EqualTo("😁"));
        string escaped = text.textPreprocessor.PreprocessText(alias);
        Assert.That(WDChatEmojiColor.SpriteAsset.spriteCharacterLookupTable.ContainsKey((uint)char.ConvertToUtf32(escaped, 0)), Is.False);
        Assert.That(text.text, Is.EqualTo(literal));
    }

    [TestCase("😁")]
    [TestCase("❤️")]
    [TestCase("✌️")]
    public void ComposerNativeBackspaceRemovesOneRenderedEmojiAndKeepsUnicode(string value)
    {
        using var f = new Fixture();
        f.Root.AddComponent<Canvas>();
        f.Input.textComponent.rectTransform.sizeDelta = new Vector2(600, 50);
        f.Input.text = "A" + value;
        f.Input.ForceLabelUpdate();
        f.Input.textComponent.ForceMeshUpdate();
        Assert.That(f.Input.textComponent.textInfo.characterInfo[1].elementType, Is.EqualTo(TMP_TextElementType.Sprite));
        Assert.That(f.Input.textComponent.textInfo.characterInfo[1].stringLength, Is.EqualTo(value.Length));
        f.Input.MoveTextEnd(false);
        Assert.That(f.Input.text, Is.EqualTo("A" + value));
        Assert.That(f.Input.isRichTextEditingAllowed, Is.False);
        typeof(TMP_InputField).GetMethod("Backspace", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(f.Input, null);
        Assert.That(f.Input.text, Is.EqualTo("A"));
        Assert.That(f.Submitted, Is.Zero);
    }

    [Test]
    public void BundledFallbackCoversCatalogWithoutRichTextOrSystemFonts()
    {
        using var f = new Fixture();
        foreach (var category in WDChatEmojiPicker.Catalog)
            foreach (var emoji in category)
                Assert.That(f.Input.textComponent.font.HasCharacters(emoji, out uint[] missing, true, true), Is.True,
                    emoji + " missing: " + string.Join(",", missing ?? Array.Empty<uint>()));
        Assert.That(f.Input.textComponent.richText, Is.False);
    }
}

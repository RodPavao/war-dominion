#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WDArmoryV2FunctionalBinder
{
    private const string ScreenPath = "Assets/VisualKit/ArsenalV2/Prefabs/WDArmoryScreenV2.prefab";
    private const string CardPath = "Assets/VisualKit/ArsenalV2/Prefabs/WDArmoryWeaponCardV2.prefab";
    private const string ProviderFolder = "Assets/Resources/UI/ArmoryV2";
    private const string ProviderPath = ProviderFolder + "/WDArmoryV2PrefabProvider.asset";

    // ============================================================
    // 01. VINCULAÇÃO EXPLÍCITA, IDEMPOTENTE E SEM RECONSTRUÇÃO VISUAL
    // ============================================================
    [MenuItem("War Dominion/VisualKit/Armory V2/Bind Functional References")]
    public static void Bind()
    {
        BindCard();
        BindScreen();
        CreateProvider();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WD-ARMORY-V2] Referências funcionais serializadas e provider oficial atualizados.");
    }

    private static void BindCard()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
        try
        {
            WDArmoryWeaponCardV2View view = root.GetComponent<WDArmoryWeaponCardV2View>();
            SerializedObject so = new SerializedObject(view);
            Set(so, "theme", AssetDatabase.LoadAssetAtPath<WarDominionUITheme>("Assets/Resources/UI/WarDominionUITheme.asset"));
            Set(so, "selectionButton", root.GetComponent<Button>());
            Set(so, "equipButton", Find(root.transform, "EquipButton").GetComponent<Button>());
            Set(so, "surface", root.GetComponent<Image>());
            Image border = Find(root.transform, "Border").GetComponent<Image>();
            Outline outline = border.GetComponent<Outline>() ?? border.gameObject.AddComponent<Outline>();
            Set(so, "selectionBorder", border);
            Set(so, "selectionOutline", outline);
            Set(so, "artwork", Find(root.transform, "Artwork").GetComponent<RawImage>());
            Set(so, "weaponName", Find(root.transform, "WeaponName").GetComponent<TMP_Text>());
            Set(so, "categoryAndLevel", Find(root.transform, "Level").GetComponent<TMP_Text>());
            Set(so, "energyLabel", Find(root.transform, "Energy").GetComponent<TMP_Text>());
            Set(so, "tokenLabel", Find(root.transform, "Tokens").GetComponent<TMP_Text>());
            Set(so, "tokenFill", Find(root.transform, "TokenFill").GetComponent<Image>());
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BindScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        try
        {
            Transform reference = Find(root.transform, "ReferenceFrame_1920x1080");
            Transform header = Find(reference, "Header");
            Transform deck = Find(reference, "DeckPanel");
            Transform catalog = Find(reference, "CatalogPanel");
            Transform details = Find(reference, "DetailsPanel");
            WDArmoryScreenV2View view = root.GetComponent<WDArmoryScreenV2View>();
            SerializedObject so = new SerializedObject(view);
            Set(so, "referenceFrame", reference as RectTransform);
            Set(so, "header", header as RectTransform);
            Set(so, "deckPanel", deck as RectTransform);
            Set(so, "catalogPanel", catalog as RectTransform);
            Set(so, "detailsPanel", details as RectTransform);
            Set(so, "backButton", Find(header, "Back").GetComponent<Button>());
            Set(so, "coins", Find(Find(header, "Coins"), "Value").GetComponent<TMP_Text>());
            Set(so, "gems", Find(Find(header, "Gems"), "Value").GetComponent<TMP_Text>());
            Set(so, "deckNameInput", EnsureInput(Find(deck, "DeckName"), false));
            Set(so, "deckNameEditButton", EnsureEditButton(Find(deck, "Edit")));
            Set(so, "deckMeta", Find(deck, "DeckMeta").GetComponent<TMP_Text>());
            Set(so, "mainMeta", Find(deck, "MainLabel").GetComponent<TMP_Text>());
            Set(so, "supportMeta", Find(deck, "SupportLabel").GetComponent<TMP_Text>());
            Set(so, "feedback", Find(details, "Hint").GetComponent<TMP_Text>());
            Set(so, "saveButton", Find(deck, "SaveDeck").GetComponent<Button>());

            Transform tabs = Find(deck, "DeckTabs");
            SetArray(so, "deckButtons", Enumerable.Range(1, 5).Select(i => Find(tabs, "Deck" + i).GetComponent<Button>()).ToArray());
            SetArray(so, "deckLabels", Enumerable.Range(1, 5).Select(i => Find(Find(tabs, "Deck" + i), "Label").GetComponent<TMP_Text>()).ToArray());
            SetArray(so, "deckLockIcons", Enumerable.Range(1, 5).Select(i => i < 3 ? null : Find(Find(tabs, "Deck" + i), "Lock").GetComponent<Image>()).ToArray());

            SetArray(so, "mainSlots", BindSlots(Find(deck, "MainSlots")));
            SetArray(so, "supportSlots", BindSlots(Find(deck, "SupportSlots")));
            Set(so, "searchField", EnsureInput(Find(catalog, "Search"), true));
            Transform filters = Find(catalog, "Filters");
            string[] filterNames = { "TODAS", "APOIO", "TÁTICA", "ESTRATÉGICA", "ESPECIAL" };
            SetArray(so, "filterButtons", filterNames.Select(name => Find(filters, "Filter" + name).GetComponent<Button>()).ToArray());
            SetArray(so, "filterLabels", filterNames.Select(name => Find(Find(filters, "Filter" + name), "Label").GetComponent<TMP_Text>()).ToArray());
            SetArray(so, "catalogCards", root.GetComponentsInChildren<WDArmoryWeaponCardV2View>(true).Cast<WDArmoryWeaponCardView>().ToArray());

            Set(so, "detailArtwork", Find(Find(details, "ArtworkFrame"), "Artwork").GetComponent<RawImage>());
            Set(so, "detailCategory", Find(details, "Category").GetComponent<TMP_Text>());
            Set(so, "detailName", Find(details, "Name").GetComponent<TMP_Text>());
            Set(so, "detailFacts", Find(details, "Facts").GetComponent<TMP_Text>());
            Set(so, "detailDescription", Find(details, "Description").GetComponent<TMP_Text>());
            Set(so, "detailEvolution", Find(details, "Levels").GetComponent<TMP_Text>());
            Set(so, "detailTokens", Find(details, "Tokens").GetComponent<TMP_Text>());
            Set(so, "detailMissing", Find(details, "Missing").GetComponent<TMP_Text>());
            Set(so, "detailTokenFill", Find(details, "ProgressFill").GetComponent<Image>());
            Set(so, "detailBenefit", Find(Find(details, "NextLevel"), "Label").GetComponent<TMP_Text>());
            Set(so, "detailUpgradeCost", Find(details, "UpgradeCost").GetComponent<TMP_Text>());
            Set(so, "upgradeButton", Find(details, "Upgrade").GetComponent<Button>());
            Set(so, "addButton", Find(details, "AddToDeck").GetComponent<Button>());
            Set(so, "detailHint", Find(details, "Hint").GetComponent<TMP_Text>());
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ============================================================
    // 02. ADAPTERS DOS CONTROLES EXISTENTES NO PREFAB APROVADO
    // ============================================================
    private static WDArmorySlotView[] BindSlots(Transform group)
    {
        return group.Cast<Transform>().Select(slot =>
        {
            WDArmorySlotView view = slot.GetComponent<WDArmorySlotView>() ?? slot.gameObject.AddComponent<WDArmorySlotView>();
            Button selection = slot.GetComponent<Button>();
            Transform name = Find(slot, "Name");
            Transform energy = Find(slot, "Energy");
            Transform emptyState = Find(slot, "EmptyState");
            Transform emptyLabel = Find(emptyState, "Label");
            Transform emptyHint = Find(emptyState, "Hint");
            Transform emptyPlus = Find(emptyState, "Plus");
            ConfigureEmptyState(emptyLabel, emptyHint, emptyPlus);
            Transform remove = FindOptional(slot, "Remove") ?? CreateRemove(slot);
            Button removeButton = remove.GetComponent<Button>() ?? remove.gameObject.AddComponent<Button>();
            removeButton.targetGraphic = remove.GetComponent<Image>();
            removeButton.targetGraphic.raycastTarget = true;
            Transform frame = FindOptional(slot, "ArtworkFrame") ?? CreateArtworkFrame(slot);
            RawImage artwork = Find(frame, "Artwork").GetComponent<RawImage>();
            Image frameImage = frame.GetComponent<Image>() ?? frame.gameObject.AddComponent<Image>();
            frameImage.color = Color.clear;
            Outline outline = slot.GetComponent<Outline>() ?? slot.gameObject.AddComponent<Outline>();
            SerializedObject so = new SerializedObject(view);
            Set(so, "selectionButton", selection); Set(so, "removeButton", removeButton);
            Set(so, "artwork", artwork); Set(so, "artworkFrame", frameImage);
            Set(so, "artworkAspect", artwork.GetComponent<AspectRatioFitter>());
            Set(so, "weaponName", name.GetComponent<TMP_Text>()); Set(so, "energy", energy.GetComponent<TMP_Text>());
            // 02.2 — Estado vazio possui sua própria composição; o binder não pode
            // voltar a compartilhar os TMP usados pelos dados da arma equipada.
            Set(so, "emptyLabel", emptyLabel.GetComponent<TMP_Text>());
            Set(so, "emptyHint", emptyHint.GetComponent<TMP_Text>());
            Set(so, "emptyPlus", emptyPlus.GetComponent<TMP_Text>());
            Set(so, "emptyState", emptyState.gameObject);
            Set(so, "occupiedBorder", Find(slot, "Border").GetComponent<Graphic>());
            Set(so, "surface", slot.GetComponent<Image>()); Set(so, "selectionOutline", outline);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }).ToArray();
    }

    private static Transform CreateArtworkFrame(Transform slot)
    {
        GameObject frame = new GameObject("ArtworkFrame", typeof(RectTransform), typeof(RectMask2D));
        frame.transform.SetParent(slot, false);
        RectTransform rect = (RectTransform)frame.transform;
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
        // 02.1 — Fallback editorial alinhado aos slots 124×184 aprovados.
        rect.anchoredPosition = new Vector2(6f, -6f); rect.sizeDelta = new Vector2(112f, 104f);
        GameObject art = new GameObject("Artwork", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        art.transform.SetParent(frame.transform, false);
        RectTransform artRect = (RectTransform)art.transform;
        artRect.anchorMin = Vector2.zero; artRect.anchorMax = Vector2.one; artRect.offsetMin = artRect.offsetMax = Vector2.zero;
        art.GetComponent<RawImage>().raycastTarget = false;
        art.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        frame.SetActive(false);
        return frame.transform;
    }

    // ============================================================
    // 02.3 ESTADO VAZIO CANÔNICO: + E LIVRE CENTRALIZADOS
    // ============================================================
    private static void ConfigureEmptyState(Transform label, Transform hint, Transform plus)
    {
        // 02.3.1 — Todo Graphic uGUI precisa de CanvasRenderer serializado.
        // A ausência desse componente tornava a malha tracejada invisível.
        GameObject emptyState = label.parent.gameObject;
        if (emptyState.GetComponent<CanvasRenderer>() == null)
            emptyState.AddComponent<CanvasRenderer>();

        TMP_Text labelText = label.GetComponent<TMP_Text>();
        labelText.text = "LIVRE";
        labelText.enableAutoSizing = false;
        labelText.fontSize = labelText.fontSizeMin = labelText.fontSizeMax = 12f;
        labelText.color = new Color32(214, 220, 225, 255);
        labelText.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = (RectTransform)label;
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
        labelRect.localScale = Vector3.one;
        labelRect.anchoredPosition = new Vector2(0f, -10f);
        labelRect.sizeDelta = new Vector2(108f, 20f);

        TMP_Text plusText = plus.GetComponent<TMP_Text>();
        plusText.text = "+";
        plusText.enableAutoSizing = false;
        plusText.fontSize = plusText.fontSizeMin = plusText.fontSizeMax = 16f;
        plusText.color = new Color32(197, 155, 99, 255);
        plusText.alignment = TextAlignmentOptions.Center;
        RectTransform plusRect = (RectTransform)plus;
        plusRect.anchorMin = plusRect.anchorMax = plusRect.pivot = new Vector2(.5f, .5f);
        plusRect.localScale = Vector3.one;
        plusRect.anchoredPosition = new Vector2(0f, 11f);
        plusRect.sizeDelta = new Vector2(30f, 20f);

        WDDashedRoundedBorderGraphic dashed = label.parent.GetComponent<WDDashedRoundedBorderGraphic>();
        if (dashed != null)
            dashed.Configure(1.5f, 5f, 4f, 10f, new Color32(197, 155, 99, 166));

        hint.gameObject.SetActive(false);
    }

    private static Transform CreateRemove(Transform slot)
    {
        GameObject remove = new GameObject("Remove", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        remove.transform.SetParent(slot, false);
        RectTransform rect = (RectTransform)remove.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(8f, -8f); rect.sizeDelta = new Vector2(26f, 26f);
        Image image = remove.GetComponent<Image>();
        WarDominionUITheme theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>("Assets/Resources/UI/WarDominionUITheme.asset");
        image.sprite = theme.MasterRoundedFill;
        image.color = new Color(172f / 255f, 45f / 255f, 45f / 255f, .94f);
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(remove.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = "×"; label.font = theme.MasterGabaritoBold; label.fontSize = 13f;
        label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        remove.SetActive(false);
        return remove.transform;
    }

    private static TMP_InputField EnsureInput(Transform root, bool hasPlaceholder)
    {
        TMP_InputField input = root.GetComponent<TMP_InputField>() ?? root.gameObject.AddComponent<TMP_InputField>();
        TMP_Text existing = hasPlaceholder ? Find(root, "Placeholder").GetComponent<TMP_Text>() : root.GetComponent<TMP_Text>();
        TMP_Text text = existing;
        if (hasPlaceholder)
        {
            // 03.1 — Reutiliza um único InputText e remove duplicatas editoriais.
            Transform[] candidates = root.Cast<Transform>().Where(child => child.name == "InputText").ToArray();
            GameObject go = candidates.Length > 0
                ? candidates[0].gameObject
                : new GameObject("InputText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (go.transform.parent != root) go.transform.SetParent(root, false);
            for (int i = 1; i < candidates.Length; i++) UnityEngine.Object.DestroyImmediate(candidates[i].gameObject);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(34f, 8f); rect.offsetMax = new Vector2(-8f, -8f);
            text = go.GetComponent<TMP_Text>() ?? go.AddComponent<TextMeshProUGUI>();
            text.font = existing.font; text.fontSize = existing.fontSize; text.color = existing.color;
            text.horizontalAlignment = HorizontalAlignmentOptions.Left;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            existing.horizontalAlignment = HorizontalAlignmentOptions.Left;
            existing.verticalAlignment = VerticalAlignmentOptions.Middle;
            input.placeholder = existing;
        }
        input.textViewport = root as RectTransform;
        input.textComponent = text;
        input.targetGraphic = root.GetComponent<Graphic>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        if (!hasPlaceholder) input.characterLimit = WDUIContentContracts.DeckNameMaxLength;
        return input;
    }

    // ============================================================
    // 03.2 LÁPIS SERIALIZADO COMO AÇÃO DE EDIÇÃO DO DECK
    // ============================================================
    private static Button EnsureEditButton(Transform root)
    {
        Image image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        image.raycastPadding = new Vector4(-8f, -8f, -8f, -8f);
        Button button = root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    // ============================================================
    // 03. PROVIDER EM RESOURCES SEM DUPLICAR O PREFAB VISUAL
    // ============================================================
    private static void CreateProvider()
    {
        EnsureFolder(ProviderFolder);
        WDArmoryV2PrefabProvider provider = AssetDatabase.LoadAssetAtPath<WDArmoryV2PrefabProvider>(ProviderPath);
        if (provider == null) { provider = ScriptableObject.CreateInstance<WDArmoryV2PrefabProvider>(); AssetDatabase.CreateAsset(provider, ProviderPath); }
        SerializedObject so = new SerializedObject(provider);
        Set(so, "screenPrefab", AssetDatabase.LoadAssetAtPath<WDArmoryScreenV2View>(ScreenPath));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(provider);
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/').Skip(1)) { string next = current + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part); current = next; }
    }
    private static Transform Find(Transform root, string name) => FindOptional(root, name) ?? throw new MissingReferenceException($"Arsenal V2: '{name}' ausente em '{root.name}'.");
    private static Transform FindOptional(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    private static void Set(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
    private static void SetArray<T>(SerializedObject so, string name, T[] values) where T : UnityEngine.Object
    {
        SerializedProperty property = so.FindProperty(name); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif

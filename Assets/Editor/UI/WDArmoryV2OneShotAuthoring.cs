#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class WDArmoryV2OneShotAuthoring
{
    private const string Root = "Assets/VisualKit/ArsenalV2";
    private const string PrefabRoot = Root + "/Prefabs";
    private const string ValidationRoot = Root + "/Validation";
    private const string CardPath = PrefabRoot + "/WDArmoryWeaponCardV2.prefab";
    private const string ScreenPath = PrefabRoot + "/WDArmoryScreenV2.prefab";
    private const string ScenePath = ValidationRoot + "/WDArmoryV2Validation.unity";
    private const string ThemePath = "Assets/Resources/UI/WarDominionUITheme.asset";
    private const string CatalogPath = "Assets/Resources/UI/Armory/WDArmoryVisualCatalog.asset";

    private static WarDominionUITheme theme;
    private static WDArmoryVisualCatalog catalog;

    static WDArmoryV2OneShotAuthoring()
    {
        // 01.1 — A autoria automática só ocorre para a primeira criação e nunca sobrescreve a V2.
        EditorApplication.delayCall += CreateOnceWhenReady;
    }

    // ============================================================
    // 01. AUTORIA ÚNICA, ISOLADA E NÃO DESTRUTIVA
    // ============================================================
    private static void CreateOnceWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateOnceWhenReady;
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) != null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(CardPath) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return;

        CreateAssets();
    }

    [MenuItem("War Dominion/VisualKit/Armory V2/Create Isolated Preview", priority = 220)]
    public static void CreateAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) != null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(CardPath) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.Log("[WD-ARMORY-V2] A V2 já existe. A autoria protegida não sobrescreve Prefabs ou cenas.");
            return;
        }

        theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(ThemePath);
        catalog = AssetDatabase.LoadAssetAtPath<WDArmoryVisualCatalog>(CatalogPath);
        if (theme == null || catalog == null || catalog.Weapons.Count != 26)
            throw new MissingReferenceException("Tema MASTER ou catálogo visual canônico de 26 armas ausente.");

        Directory.CreateDirectory(PrefabRoot);
        Directory.CreateDirectory(ValidationRoot);
        GameObject card = BuildCard();
        GameObject cardPrefab = PrefabUtility.SaveAsPrefabAsset(card, CardPath);
        UnityEngine.Object.DestroyImmediate(card);

        GameObject screen = BuildScreen(cardPrefab);
        GameObject screenPrefab = PrefabUtility.SaveAsPrefabAsset(screen, ScreenPath);
        UnityEngine.Object.DestroyImmediate(screen);
        BuildValidationScene(screenPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateAssets();
        Debug.Log("[WD-ARMORY-V2] Prefabs e cena isolada criados sem alterar o Arsenal ativo.", screenPrefab);
    }

    // ============================================================
    // 01.2 VALIDAÇÃO SOMENTE LEITURA DA V2
    // ============================================================
    [MenuItem("War Dominion/VisualKit/Armory V2/Validate Isolated Preview", priority = 221)]
    public static void ValidateAssets()
    {
        GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        GameObject screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (card == null || screen == null || scene == null)
            throw new MissingReferenceException("Prefab de card, tela ou cena V2 ausente.");

        ValidatePrefabContents(CardPath, root =>
        {
            if (root.GetComponent<WDArmoryWeaponCardV2View>() == null)
                throw new MissingComponentException("WDArmoryWeaponCardV2View ausente no card V2.");
        });
        ValidatePrefabContents(ScreenPath, root =>
        {
            WDArmoryScreenV2View view = root.GetComponent<WDArmoryScreenV2View>();
            if (view == null || view.CatalogCards.Length != 26)
                throw new InvalidOperationException("Tela V2 deve possuir View raiz e 26 cards serializados.");
            ValidateRect(view.Header, new Vector2(1856f, 72f), "Header");
            ValidateRect(view.DeckPanel, new Vector2(550f, 944f), "Deck");
            ValidateRect(view.CatalogPanel, new Vector2(714f, 944f), "Catálogo");
            ValidateRect(view.DetailsPanel, new Vector2(560f, 944f), "Detalhes");
            if (view.MainSlots.Length != 8 || view.SupportSlots.Length != 2)
                throw new InvalidOperationException("Tela V2 deve possuir 8 slots principais e 2 slots APOIO vinculados.");
            foreach (WDArmorySlotView slot in view.MainSlots.Concat(view.SupportSlots))
            {
                Button[] buttons = slot.GetComponentsInChildren<Button>(true);
                Button remove = buttons.FirstOrDefault(button => button.name == "Remove");
                if (remove == null || remove.targetGraphic == null || !remove.targetGraphic.raycastTarget)
                    throw new InvalidOperationException($"Botão de remoção sem raycast funcional em {slot.name}.");
            }
        });

        WDArmoryV2VisualSettings settings = AssetDatabase.LoadAssetAtPath<WDArmoryV2VisualSettings>(
            "Assets/Resources/UI/ArmoryV2/WDArmoryV2VisualSettings.asset");
        if (settings == null || settings.ArtworkOverrides.Count != 26)
            throw new MissingReferenceException("Configuração visual V2 ausente ou sem as 26 artes canônicas.");

        string[] dependencies = AssetDatabase.GetDependencies(new[] { ScreenPath, CardPath, ScenePath }, true);
        string oldScreen = "Assets/Resources/UI/Armory/Prefabs/WDArmoryScreen.prefab";
        string oldCard = "Assets/Resources/UI/Armory/Prefabs/WDArmoryWeaponCard.prefab";
        if (dependencies.Contains(oldScreen) || dependencies.Contains(oldCard))
            throw new InvalidOperationException("A V2 possui dependência indevida de Prefab visual antigo.");
        if (!dependencies.Any(path => path.StartsWith("Assets/VisualKit/", StringComparison.Ordinal)))
            throw new InvalidOperationException("A V2 não possui dependências oficiais do VisualKit.");

        Debug.Log("[WD-ARMORY-V2-VALIDATION] OK — hierarquia independente, 26 cards, geometria MASTER, scripts e referências válidos.", screen);
    }

    // ============================================================
    // 01.3 MIGRAÇÃO PONTUAL — ACABAMENTO FINAL DO MASTER 3095:432
    // ============================================================
    [MenuItem("War Dominion/VisualKit/Armory V2/Apply Final Visual Polish", priority = 222)]
    public static void ApplyFinalVisualPolish()
    {
        theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(ThemePath);
        if (theme == null || theme.MasterArmoryEnergyIcon == null || theme.MasterArmoryArrowRightIcon == null)
            throw new MissingReferenceException("Tema MASTER ou ícones oficiais Energy/Arrow Right ausentes.");

        EditPrefab(CardPath, ApplyCardPolish);
        EditPrefab(ScreenPath, ApplyScreenPolish);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateAssets();
        Debug.Log("[WD-ARMORY-V2] Acabamento final do MASTER 3095:432 aplicado aos Prefabs serializados.");
    }

    // ============================================================
    // 01.4 MIGRAÇÃO PONTUAL — CONTORNOS UNIVERSAIS DO HEADER
    // ============================================================
    [MenuItem("War Dominion/VisualKit/Armory V2/Apply Universal Header Outlines", priority = 223)]
    public static void ApplyUniversalHeaderOutlines()
    {
        theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(ThemePath);
        if (theme == null || theme.MasterRoundedOutline == null)
            throw new MissingReferenceException("Tema MASTER ou outline oficial ausente.");
        EditPrefab(ScreenPath, root =>
        {
            Transform header = FindDeep(root.transform, "Header") ??
                throw new MissingReferenceException("Header do Arsenal V2 ausente.");
            string[] paths = { "Back", "Economy/Coins", "Economy/Gems", "Economy/Notifications" };
            foreach (string path in paths)
            {
                RectTransform control = header.Find(path) as RectTransform;
                if (control == null)
                    throw new MissingReferenceException("Controle universal ausente: Header/" + path);
                WDUniversalControlFactory.AddGoldOutline(control, theme);
            }
        });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WD-ARMORY-V2] Contornos universais do Header aplicados sem reconstruir o Arsenal.");
    }

    private static void ApplyCardPolish(GameObject root)
    {
        TMP_Text energy = FindDeep(root.transform, "Energy")?.GetComponent<TMP_Text>();
        if (energy == null) throw new MissingReferenceException("Energy ausente no card V2.");
        energy.text = "0";
        PlaceTopLeft(energy.rectTransform, 120f, 146f, 33f, 16f);

        Image energyIcon = EnsureIcon(root.transform, "EnergyIcon", theme.MasterArmoryEnergyIcon, theme.MasterCyan);
        PlaceTopLeft(energyIcon.rectTransform, 104f, 147f, 12f, 12f);

        RectTransform track = FindDeep(root.transform, "TokenTrack") as RectTransform;
        if (track != null) PlaceBottomLeft(track, 8f, 8f, 116f, 3f);

        TMP_Text tokens = FindDeep(root.transform, "Tokens")?.GetComponent<TMP_Text>();
        if (tokens != null)
        {
            tokens.enableAutoSizing = false;
            tokens.fontSize = 7f;
            tokens.fontSizeMin = 7f;
            tokens.fontSizeMax = 10f;
        }
    }

    private static void ApplyScreenPolish(GameObject root)
    {
        Transform deck = FindDeep(root.transform, "DeckPanel");
        Transform catalogPanel = FindDeep(root.transform, "CatalogPanel");
        Transform details = FindDeep(root.transform, "DetailsPanel");
        if (deck == null || catalogPanel == null || details == null)
            throw new MissingReferenceException("Painéis estruturais do Arsenal V2 ausentes.");

        Transform tabs = FindDeep(deck, "DeckTabs");
        for (int index = 1; index <= 5; index++)
        {
            Transform tab = FindDeep(tabs, "Deck" + index);
            if (tab == null) continue;
            if (tab.TryGetComponent(out Button button)) button.transition = Selectable.Transition.None;
            if (tab.TryGetComponent(out Image surface)) surface.color = theme.MasterSurfaceElevated;
            Image border = EnsureIcon(tab, "Border", theme.MasterRoundedOutline,
                index == 1 ? theme.MasterCyan : new Color(1f, 1f, 1f, .07f));
            Stretch(border.rectTransform);
            border.type = UnityEngine.UI.Image.Type.Sliced;
        }

        RectTransform mainSlots = FindDeep(deck, "MainSlots") as RectTransform;
        RectTransform supportSlots = FindDeep(deck, "SupportSlots") as RectTransform;
        if (mainSlots != null) mainSlots.anchoredPosition = new Vector2(mainSlots.anchoredPosition.x, -240f);
        if (supportSlots != null) supportSlots.anchoredPosition = new Vector2(supportSlots.anchoredPosition.x, -664f);

        Transform filters = FindDeep(catalogPanel, "Filters");
        if (filters != null)
        {
            HorizontalLayoutGroup layout = filters.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.spacing = 6f;
            foreach (Transform filter in filters)
            {
                Transform borderTransform = filter.Find("Border");
                if (borderTransform == null || !borderTransform.TryGetComponent(out Image border)) continue;
                border.type = UnityEngine.UI.Image.Type.Sliced;
                border.color = filter.name == "FilterTODAS" ? theme.MasterGold : new Color(1f, 1f, 1f, .09f);
            }
        }

        RectTransform viewport = FindDeep(catalogPanel, "CatalogViewport") as RectTransform;
        if (viewport != null) PlaceTopLeft(viewport, 20f, 118f, 674f, 800f);
        RectTransform scrollbarTrack = FindDeep(viewport, "ScrollbarTrack") as RectTransform;
        if (scrollbarTrack != null) PlaceTopRight(scrollbarTrack, 0f, 0f, 4f, 800f);

        TMP_Text category = FindDeep(details, "Category")?.GetComponent<TMP_Text>();
        TMP_Text facts = FindDeep(details, "Facts")?.GetComponent<TMP_Text>();
        TMP_Text levels = FindDeep(details, "Levels")?.GetComponent<TMP_Text>();
        TMP_Text cost = FindDeep(details, "UpgradeCost")?.GetComponent<TMP_Text>();
        if (category != null) category.richText = true;
        if (facts != null) facts.richText = true;
        if (levels != null) levels.richText = true;
        if (cost != null) cost.color = theme.MasterTextPrimary;

        // O custo no preview é textual no MASTER atual. Remove somente o
        // ícone local criado pela migração anterior, sem afetar cards/slots.
        Transform detailEnergy = details.Find("EnergyIcon");
        if (detailEnergy != null) UnityEngine.Object.DestroyImmediate(detailEnergy.gameObject);
        Image arrow = EnsureIcon(details, "ProgressionArrow", theme.MasterArmoryArrowRightIcon, theme.MasterTextSecondary);
        PlaceTopLeft(arrow.rectTransform, 424f, 399f, 14f, 14f);
    }

    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Image EnsureIcon(Transform parent, string name, Sprite sprite, Color color)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject target = existing != null ? existing.gameObject : UI(name, parent);
            image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        }
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f
            ? UnityEngine.UI.Image.Type.Sliced
            : UnityEngine.UI.Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void ValidatePrefabContents(string path, Action<GameObject> validate)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int missingScripts = contents.GetComponentsInChildren<Transform>(true)
                .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
            if (missingScripts != 0)
                throw new MissingComponentException($"{path} contém {missingScripts} Missing Script(s).");
            validate(contents);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ValidateRect(RectTransform rect, Vector2 expected, string label)
    {
        if (rect == null || Vector2.Distance(rect.sizeDelta, expected) > .1f)
            throw new InvalidOperationException($"Geometria V2 divergente em {label}. Esperado {expected}.");
    }

    // ============================================================
    // 02. CARD V2 SERIALIZADO
    // ============================================================
    private static GameObject BuildCard()
    {
        GameObject root = UI("WDArmoryWeaponCardV2", null);
        Rect(root).sizeDelta = new Vector2(161f, 184f);
        Image surface = Image(root, theme.MasterSurfaceElevated, theme.MasterRoundedFill);
        surface.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.None;

        Image border = ChildImage("Border", root.transform, new Color(theme.MasterGold.r, theme.MasterGold.g, theme.MasterGold.b, .35f), theme.MasterRoundedOutline);
        Stretch(border.rectTransform);
        border.raycastTarget = false;

        RectTransform artFrame = Rect(UI("ArtworkFrame", root.transform));
        PlaceTopLeft(artFrame, 8f, 8f, 145f, 104f);
        artFrame.gameObject.AddComponent<RectMask2D>();
        RawImage artwork = UI("Artwork", artFrame).AddComponent<RawImage>();
        artwork.raycastTarget = false;
        Stretch(artwork.rectTransform);
        AspectRatioFitter artworkFit = artwork.gameObject.AddComponent<AspectRatioFitter>();
        artworkFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        artworkFit.aspectRatio = 1f;

        TMP_Text name = Text("WeaponName", root.transform, "CENTRAL DE ENERGIA", theme.MasterGabaritoExtraBold, 12.85f, theme.MasterTextPrimary, TextAlignmentOptions.Left);
        PlaceTopLeft(name.rectTransform, 8f, 114f, 145f, 32f);
        name.textWrappingMode = TextWrappingModes.Normal;
        name.enableAutoSizing = true;
        name.fontSizeMin = 9f;
        name.fontSizeMax = 12.85f;
        name.maxVisibleLines = 2;
        name.overflowMode = TextOverflowModes.Truncate;
        TMP_Text category = Text("Level", root.transform, "ESTRATÉGICA", theme.MasterInterMedium, 10f, theme.MasterTextSecondary, TextAlignmentOptions.Left);
        PlaceTopLeft(category.rectTransform, 8f, 146f, 92f, 16f);
        category.overflowMode = TextOverflowModes.Overflow;
        Image energyIcon = ChildImage("EnergyIcon", root.transform, theme.MasterCyan, theme.MasterArmoryEnergyIcon);
        PlaceTopLeft(energyIcon.rectTransform, 104f, 147f, 12f, 12f);
        TMP_Text energy = Text("Energy", root.transform, "0", theme.MasterGabaritoBold, 12f, theme.MasterCyan, TextAlignmentOptions.Right);
        PlaceTopLeft(energy.rectTransform, 120f, 146f, 33f, 16f);

        Image track = ChildImage("TokenTrack", root.transform, new Color(1f, 1f, 1f, .07f), theme.MasterRoundedFill);
        PlaceBottomLeft(track.rectTransform, 8f, 8f, 116f, 3f);
        Image fill = ChildImage("TokenFill", track.transform, theme.MasterCyan, theme.MasterRoundedFill);
        Stretch(fill.rectTransform);
        fill.rectTransform.anchorMax = new Vector2(.64f, 1f);
        TMP_Text tokens = Text("Tokens", root.transform, "N1", theme.MasterInterRegular, 7f, theme.MasterCyan, TextAlignmentOptions.Right);
        tokens.enableAutoSizing = false;
        tokens.fontSizeMin = 7f;
        tokens.fontSizeMax = 10f;
        PlaceBottomRight(tokens.rectTransform, 8f, 3f, 42f, 16f);

        Image selected = ChildImage("SelectionBorder", root.transform, theme.MasterCyan, theme.MasterRoundedOutline);
        Stretch(selected.rectTransform);
        selected.raycastTarget = false;
        selected.gameObject.SetActive(false);

        Image lockOverlay = ChildImage("LockOverlay", artFrame, new Color(5f / 255f, 10f / 255f, 16f / 255f, .58f), theme.MasterRoundedFill);
        Stretch(lockOverlay.rectTransform);
        lockOverlay.raycastTarget = false;
        Image lockIcon = ChildImage("LockIcon", lockOverlay.transform, theme.MasterTextPrimary, theme.MasterArmoryLockIcon);
        Center(lockIcon.rectTransform, 30f, 35f);
        lockOverlay.gameObject.SetActive(false);

        Button equip = Button("EquipButton", root.transform, "+", theme.MasterGabaritoBold, 14f,
            Color.white, new Color(34f / 255f, 184f / 255f, 125f / 255f, .94f), 26f, 26f);
        RectTransform equipRect = equip.GetComponent<RectTransform>();
        equipRect.anchorMin = equipRect.anchorMax = equipRect.pivot = new Vector2(0f, 1f);
        equipRect.anchoredPosition = new Vector2(8f, -8f);
        equip.transition = Selectable.Transition.None;

        WDArmoryWeaponCardV2View view = root.AddComponent<WDArmoryWeaponCardV2View>();
        SerializedObject serialized = new SerializedObject(view);
        Assign(serialized, "selectionButton", button);
        Assign(serialized, "equipButton", equip);
        Assign(serialized, "surface", surface);
        Assign(serialized, "artwork", artwork);
        Assign(serialized, "weaponName", name);
        Assign(serialized, "categoryAndLevel", category);
        Assign(serialized, "energyLabel", energy);
        Assign(serialized, "tokenLabel", tokens);
        Assign(serialized, "tokenFill", fill);
        Assign(serialized, "selectionBorder", border);
        Assign(serialized, "lockOverlay", lockOverlay);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    // ============================================================
    // 03. TELA V2 — MASTER 1920×1080
    // ============================================================
    private static GameObject BuildScreen(GameObject cardPrefab)
    {
        GameObject root = UI("WDArmoryScreenV2", null);
        Stretch(Rect(root));
        WDArmoryScreenV2View view = root.AddComponent<WDArmoryScreenV2View>();

        Image background = ChildImage("FullscreenBackground", root.transform, Color.white, theme.MasterArmoryBackground);
        Stretch(background.rectTransform);
        background.preserveAspect = true;
        AspectRatioFitter backgroundFit = background.gameObject.AddComponent<AspectRatioFitter>();
        backgroundFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundFit.aspectRatio = 16f / 9f;
        Image veil = ChildImage("EnvironmentalVeil", root.transform, new Color(3f / 255f, 8f / 255f, 13f / 255f, .70f), null);
        Stretch(veil.rectTransform);

        RectTransform reference = Rect(UI("ReferenceFrame_1920x1080", root.transform));
        Center(reference, 1920f, 1080f);
        Image header = Panel("Header", reference, 32f, 24f, 1856f, 72f);
        BuildHeader(header.transform);
        // 03.1 — Geometria aprovada para o reequilíbrio dos três painéis.
        Image deck = Panel("DeckPanel", reference, 32f, 112f, 550f, 944f);
        BuildDeck(deck.transform);
        Image catalogPanel = Panel("CatalogPanel", reference, 598f, 112f, 714f, 944f);
        List<WDArmoryWeaponCardV2View> cards = BuildCatalog(catalogPanel.transform, cardPrefab);
        Image details = Panel("DetailsPanel", reference, 1328f, 112f, 560f, 944f);
        BuildDetails(details.transform);

        SerializedObject serialized = new SerializedObject(view);
        Assign(serialized, "referenceFrame", reference);
        Assign(serialized, "header", header.rectTransform);
        Assign(serialized, "deckPanel", deck.rectTransform);
        Assign(serialized, "catalogPanel", catalogPanel.rectTransform);
        Assign(serialized, "detailsPanel", details.rectTransform);
        TMP_InputField deckName = deck.transform.Find("DeckName").GetComponent<TMP_InputField>();
        deckName.characterLimit = WDUIContentContracts.DeckNameMaxLength;
        Assign(serialized, "deckNameInput", deckName);
        Assign(serialized, "deckNameEditButton", deck.transform.Find("Edit").GetComponent<Button>());
        AssignArray(serialized, "catalogCards", cards.Cast<UnityEngine.Object>().ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void BuildHeader(Transform parent)
    {
        Button back = Button("Back", parent, "VOLTAR", theme.MasterGabaritoBold, 14f, theme.MasterGold, theme.MasterSurfaceElevated, 140f, 46f);
        PlaceCenteredLeft(Rect(back.gameObject), 24f, 0f, 140f, 46f);
        Image chevron = ChildImage("Chevron", back.transform, theme.MasterGold, theme.MasterChevronLeftIcon);
        PlaceCenteredLeft(chevron.rectTransform, 16f, 0f, 16f, 16f);
        back.GetComponentInChildren<TMP_Text>().margin = new Vector4(22f, 0f, 0f, 0f);
        WDUniversalControlFactory.AddGoldOutline(Rect(back.gameObject), theme);
        TMP_Text title = Text("Title", parent, "ARSENAL", theme.MasterGabaritoBlack, 34f, theme.MasterTextPrimary, TextAlignmentOptions.Center);
        Center(title.rectTransform, 360f, 50f);

        RectTransform economy = Rect(UI("Economy", parent));
        PlaceCenteredRight(economy, 24f, 0f, 248f, 32f);
        HorizontalLayoutGroup layout = economy.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = false;
        BuildEconomy(economy, "Coins", theme.MasterCoinIcon, "15.420", 96f);
        BuildEconomy(economy, "Gems", theme.MasterGemIcon, "340", 96f);
        BuildIconButton(economy, "Notifications", theme.MasterBellIcon, 36f, 32f);
    }

    private static void BuildDeck(Transform parent)
    {
        TMP_Text name = Text("DeckName", parent, "ATAQUE TOTAL", theme.MasterGabaritoExtraBold, 21f, theme.MasterGold, TextAlignmentOptions.Left);
        PlaceTopLeft(name.rectTransform, 16f, 16f, 260f, 28f);
        TMP_InputField nameInput = name.gameObject.AddComponent<TMP_InputField>();
        nameInput.textViewport = name.rectTransform;
        nameInput.textComponent = name;
        nameInput.targetGraphic = name;
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.characterLimit = WDUIContentContracts.DeckNameMaxLength;
        Image edit = ChildImage("Edit", parent, theme.MasterTextSecondary, theme.MasterArmoryEditIcon);
        PlaceTopLeft(edit.rectTransform, 152f, 22f, 13f, 13f);
        edit.raycastTarget = true;
        edit.raycastPadding = new Vector4(-8f, -8f, -8f, -8f);
        Button editButton = edit.gameObject.AddComponent<Button>();
        editButton.targetGraphic = edit;
        editButton.transition = Selectable.Transition.None;
        TMP_Text meta = Text("DeckMeta", parent, "DECK 1 · 7/8 PRINCIPAIS · 0/2 APOIO", theme.MasterInterRegular, 12f, theme.MasterTextSecondary, TextAlignmentOptions.Left);
        PlaceTopLeft(meta.rectTransform, 16f, 47f, 420f, 18f);

        RectTransform tabs = Rect(UI("DeckTabs", parent));
        PlaceTopLeft(tabs, 16f, 79f, 438f, 38f);
        HorizontalLayoutGroup tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 6.5f;
        tabsLayout.childControlWidth = tabsLayout.childControlHeight = false;
        for (int i = 0; i < 5; i++)
        {
            bool active = i == 0;
            Button tab = Button("Deck" + (i + 1), tabs, "DECK " + (i + 1), theme.MasterGabaritoBold, 13f,
                active ? theme.MasterCyan : theme.MasterTextSecondary,
                theme.MasterSurfaceElevated,
                82.4f, 38f);
            tab.transition = Selectable.Transition.None;
            Image tabBorder = ChildImage("Border", tab.transform,
                active ? theme.MasterCyan : new Color(1f, 1f, 1f, .07f), theme.MasterRoundedOutline);
            Stretch(tabBorder.rectTransform);
            if (i >= 2)
            {
                Image icon = ChildImage("Lock", tab.transform, theme.MasterTextSecondary, theme.MasterArmoryLockIcon);
                PlaceCenteredRight(icon.rectTransform, 8f, 0f, 13f, 13f);
                tab.GetComponentInChildren<TMP_Text>().margin = new Vector4(0f, 0f, 16f, 0f);
            }
        }

        Button save = Button("SaveDeck", parent, "SALVAR DECK", theme.MasterGabaritoBold, 14f, theme.MasterGold, theme.MasterSurfaceElevated, 156f, 40f);
        PlaceTopRight(Rect(save.gameObject), 16f, 137f, 156f, 40f);
        Image saveIcon = ChildImage("Save", save.transform, theme.MasterGold, theme.MasterArmorySaveIcon);
        PlaceCenteredLeft(saveIcon.rectTransform, 17f, 0f, 16f, 16f);
        save.GetComponentInChildren<TMP_Text>().margin = new Vector4(21f, 0f, 0f, 0f);

        TMP_Text mainLabel = Text("MainLabel", parent, "PRINCIPAIS · 7/8", theme.MasterInterSemiBold, 12f, theme.MasterTextSecondary, TextAlignmentOptions.Left);
        PlaceTopLeft(mainLabel.rectTransform, 16f, 212f, 210f, 18f);
        RectTransform mainGrid = Rect(UI("MainSlots", parent));
        PlaceTopLeft(mainGrid, 16f, 240f, 518f, 376f);
        GridLayoutGroup mainLayout = mainGrid.gameObject.AddComponent<GridLayoutGroup>();
        mainLayout.cellSize = new Vector2(124f, 184f);
        mainLayout.spacing = new Vector2(7.33f, 8f);
        mainLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        mainLayout.constraintCount = 4;
        string[] equipped = { "ATAQUE AÉREO", "SABOTAGEM", "BUNKER", "RADAR", "ARTILHARIA", "CAMPO MINADO", "DRONE" };
        Texture2D[] textures = { Art("air_strike"), Art("sabotage"), Art("bunker"), Art("radar"), Art("artillery"), Art("minefield"), Art("drone") };
        for (int i = 0; i < 8; i++)
            BuildSlot(mainGrid, i < 7 ? equipped[i] : "LIVRE", i < 7 ? textures[i] : null, i < 7 ? "ϟ " + (15 + i * 5) : "+", false);

        TMP_Text supportLabel = Text("SupportLabel", parent, "APOIO · 0/2", theme.MasterInterSemiBold, 12f, theme.MasterTextSecondary, TextAlignmentOptions.Left);
        PlaceTopLeft(supportLabel.rectTransform, 16f, 636f, 210f, 18f);
        RectTransform supportGrid = Rect(UI("SupportSlots", parent));
        PlaceTopLeft(supportGrid, 16f, 664f, 255.33f, 184f);
        GridLayoutGroup supportLayout = supportGrid.gameObject.AddComponent<GridLayoutGroup>();
        supportLayout.cellSize = new Vector2(124f, 184f);
        supportLayout.spacing = new Vector2(7.33f, 0f);
        supportLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        supportLayout.constraintCount = 2;
        BuildSlot(supportGrid, "LIVRE", null, "+", true);
        BuildSlot(supportGrid, "LIVRE", null, "+", true);
    }

    private static List<WDArmoryWeaponCardV2View> BuildCatalog(Transform parent, GameObject cardPrefab)
    {
        TMP_Text title = Text("Title", parent, "CATÁLOGO", theme.MasterGabaritoExtraBold, 18f, theme.MasterTextPrimary, TextAlignmentOptions.Left);
        PlaceTopLeft(title.rectTransform, 20f, 16f, 120f, 28f);
        TMP_Text count = Text("Count", parent, "26 ARMAS", theme.MasterInterRegular, 10f, theme.MasterGold, TextAlignmentOptions.Left);
        PlaceTopLeft(count.rectTransform, 136f, 20f, 90f, 20f);
        Image search = ChildImage("Search", parent, theme.MasterSurfaceElevated, theme.MasterRoundedFill);
        PlaceTopRight(search.rectTransform, 20f, 16f, 260f, 44f);
        Image searchIcon = ChildImage("Icon", search.transform, theme.MasterTextSecondary, theme.MasterArmorySearchIcon);
        PlaceCenteredLeft(searchIcon.rectTransform, 12f, 0f, 14f, 14f);
        TMP_Text searchText = Text("Placeholder", search.transform, "Buscar arma", theme.MasterInterRegular, 11f, theme.MasterTextSecondary, TextAlignmentOptions.Left);
        Stretch(searchText.rectTransform, 34f, 8f, 8f, 8f);

        RectTransform filters = Rect(UI("Filters", parent));
        PlaceTopLeft(filters, 20f, 68f, 466f, 40f);
        HorizontalLayoutGroup filterLayout = filters.gameObject.AddComponent<HorizontalLayoutGroup>();
        filterLayout.spacing = 6f;
        filterLayout.childControlWidth = filterLayout.childControlHeight = false;
        filterLayout.childForceExpandWidth = filterLayout.childForceExpandHeight = false;
        string[] labels = { "TODAS", "APOIO", "TÁTICA", "ESTRATÉGICA", "ESPECIAL" };
        float[] widths = { 76f, 76f, 78f, 120f, 92f };
        for (int i = 0; i < labels.Length; i++)
        {
            string label = labels[i];
            Button filter = Button("Filter" + label, filters, label, theme.MasterGabaritoBold, 12f,
                label == "TODAS" ? theme.MasterGold : theme.MasterTextSecondary,
                theme.MasterSurfaceElevated, widths[i], 40f);
            filter.transition = Selectable.Transition.None;
            Image border = ChildImage("Border", filter.transform,
                label == "TODAS"
                    ? theme.MasterGold
                    : new Color(theme.MasterTextSecondary.r, theme.MasterTextSecondary.g, theme.MasterTextSecondary.b, .09f),
                theme.MasterRoundedOutline);
            Stretch(border.rectTransform);
            border.raycastTarget = false;
        }

        RectTransform viewport = Rect(UI("CatalogViewport", parent));
        PlaceTopLeft(viewport, 20f, 118f, 674f, 800f);
        Image viewportGraphic = Image(viewport.gameObject, Color.clear, null);
        viewportGraphic.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewport;

        RectTransform grid = Rect(UI("WeaponGrid", viewport));
        grid.anchorMin = new Vector2(0f, 1f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.pivot = new Vector2(.5f, 1f);
        grid.anchoredPosition = Vector2.zero;
        grid.sizeDelta = Vector2.zero;
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(161f, 184f);
        layout.spacing = new Vector2(10f, 12f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        ContentSizeFitter fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = grid;

        var result = new List<WDArmoryWeaponCardV2View>(26);
        for (int i = 0; i < catalog.Weapons.Count; i++)
        {
            WDArmoryVisualCatalog.Entry entry = catalog.Weapons[i];
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, grid);
            instance.name = "Weapon_" + entry.WeaponId;
            WDArmoryWeaponCardV2View card = instance.GetComponent<WDArmoryWeaponCardV2View>();
            card.Present(entry.Artwork, DisplayName(entry.WeaponId), i % 5 == 0 ? "NÍVEL 2" : "NÍVEL 1",
                "ϟ " + (5 + i % 7 * 5), i % 4 == 0 ? "320/500" : "—", i % 4 == 0 ? .64f : 0f,
                entry.WeaponId == "energy_plant", entry.WeaponId == "reinforcements");
            result.Add(card);
        }

        Image track = ChildImage("ScrollbarTrack", viewport, new Color(1f, 1f, 1f, .07f), theme.MasterRoundedFill);
        PlaceTopRight(track.rectTransform, 0f, 0f, 4f, 800f);
        Image thumb = ChildImage("ScrollbarThumb", track.transform, new Color(theme.MasterGold.r, theme.MasterGold.g, theme.MasterGold.b, .55f), theme.MasterRoundedFill);
        PlaceTopLeft(thumb.rectTransform, 0f, 0f, 4f, 300f);
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = thumb.rectTransform;
        scrollbar.targetGraphic = thumb;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        return result;
    }

    private static void BuildDetails(Transform parent)
    {
        Image previewFrame = ChildImage("ArtworkFrame", parent, theme.MasterBackground, theme.MasterRoundedFill);
        PlaceTopLeft(previewFrame.rectTransform, 24f, 24f, 264f, 264f);
        previewFrame.gameObject.AddComponent<RectMask2D>();
        RawImage preview = UI("Artwork", previewFrame.transform).AddComponent<RawImage>();
        preview.texture = Art("energy_plant");
        preview.raycastTarget = false;
        Stretch(preview.rectTransform);
        AspectRatioFitter previewFit = preview.gameObject.AddComponent<AspectRatioFitter>();
        previewFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        previewFit.aspectRatio = 1f;

        TMP_Text category = Text("Category", parent, "ESTRATÉGICA   •  SELECIONADA", theme.MasterInterSemiBold, 12f, theme.MasterGold, TextAlignmentOptions.Left);
        PlaceTopLeft(category.rectTransform, 304f, 24f, 232f, 22f);
        TMP_Text name = Text("Name", parent, "CENTRAL DE\nENERGIA", theme.MasterGabaritoExtraBold, 30f, theme.MasterTextPrimary, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(name.rectTransform, 304f, 58f, 232f, 84f);
        TMP_Text facts = Text("Facts", parent, "<color=#C59B63>NÍVEL 1</color>          <color=#0DC2DB>CUSTO 0</color>", theme.MasterInterSemiBold, 12f, theme.MasterTextSecondary, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(facts.rectTransform, 304f, 154f, 232f, 52f);
        TMP_Text description = Text("Description", parent, "Configuração canônica da arma selecionada.", theme.MasterInterRegular, 12f, theme.MasterTextSecondary, TextAlignmentOptions.TopLeft);
        description.textWrappingMode = TextWrappingModes.Normal;
        PlaceTopLeft(description.rectTransform, 304f, 224f, 232f, 76f);

        Image divider = ChildImage("Divider", parent, new Color(theme.MasterGold.r, theme.MasterGold.g, theme.MasterGold.b, .24f), null);
        PlaceTopLeft(divider.rectTransform, 24f, 375f, 512f, 1f);
        TMP_Text evolution = Text("Evolution", parent, "EVOLUÇÃO", theme.MasterGabaritoBold, 20f, theme.MasterTextPrimary, TextAlignmentOptions.Left);
        PlaceTopLeft(evolution.rectTransform, 23f, 392f, 160f, 28f);
        TMP_Text levels = Text("Levels", parent, "<color=#C59B63>ATUAL · 1</color>                 <color=#F5F7FA>PRÓXIMO · 2</color>", theme.MasterInterRegular, 12f, theme.MasterGold, TextAlignmentOptions.Right);
        PlaceTopRight(levels.rectTransform, 23f, 396f, 300f, 22f);
        Image progressionArrow = ChildImage("ProgressionArrow", parent, theme.MasterTextSecondary, theme.MasterArmoryArrowRightIcon);
        PlaceTopLeft(progressionArrow.rectTransform, 424f, 399f, 14f, 14f);
        TMP_Text tokens = Text("Tokens", parent, "320 / 500 TOKENS", theme.MasterGabaritoBold, 22f, theme.MasterCyan, TextAlignmentOptions.Left);
        PlaceTopLeft(tokens.rectTransform, 23f, 436f, 260f, 30f);
        TMP_Text missing = Text("Missing", parent, "FALTAM 180 TOKENS", theme.MasterInterRegular, 12f, theme.MasterTextSecondary, TextAlignmentOptions.Right);
        PlaceTopRight(missing.rectTransform, 23f, 442f, 200f, 20f);
        Image track = ChildImage("ProgressTrack", parent, new Color(1f, 1f, 1f, .07f), theme.MasterRoundedFill);
        PlaceTopLeft(track.rectTransform, 24f, 474f, 512f, 6f);
        Image fill = ChildImage("ProgressFill", track.transform, theme.MasterCyan, theme.MasterRoundedFill);
        Stretch(fill.rectTransform);
        fill.rectTransform.anchorMax = new Vector2(.64f, 1f);
        Image benefit = ChildImage("NextLevel", parent, new Color(18f / 255f, 26f / 255f, 36f / 255f, .85f), theme.MasterRoundedFill);
        PlaceTopLeft(benefit.rectTransform, 24f, 496f, 512f, 58f);
        TMP_Text benefitLabel = Text("Label", benefit.transform, "PRÓXIMO NÍVEL · 2\nBenefício ampliado conforme a progressão da arma", theme.MasterInterSemiBold, 12f, theme.MasterGold, TextAlignmentOptions.Left);
        Stretch(benefitLabel.rectTransform, 12f, 8f, 12f, 8f);
        TMP_Text cost = Text("UpgradeCost", parent, "CUSTO DA EVOLUÇÃO\n500 TOKENS + 2.500 MOEDAS", theme.MasterInterRegular, 12f, theme.MasterTextSecondary, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(cost.rectTransform, 24f, 570f, 324f, 50f);
        Button upgrade = Button("Upgrade", parent, "EVOLUIR", theme.MasterGabaritoBold, 14f, theme.MasterTextMuted, theme.MasterDisabled, 148f, 48f);
        PlaceTopRight(Rect(upgrade.gameObject), 24f, 566f, 148f, 48f);
        upgrade.interactable = false;
        Button add = Button("AddToDeck", parent, "+   ADICIONAR AO DECK", theme.MasterGabaritoBold, 14f, theme.MasterBackground, theme.MasterGold, 512f, 48f);
        PlaceTopLeft(Rect(add.gameObject), 24f, 638f, 512f, 48f);
        TMP_Text hint = Text("Hint", parent, "DECK 1 · ATAQUE TOTAL · 1 vaga principal disponível", theme.MasterInterRegular, 12f, theme.MasterTextSecondary, TextAlignmentOptions.Center);
        PlaceTopLeft(hint.rectTransform, 24f, 694f, 512f, 22f);
    }

    // ============================================================
    // 04. CENA ISOLADA DE VALIDAÇÃO
    // ============================================================
    private static void BuildValidationScene(GameObject screenPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("ValidationCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = theme.MasterBackground;

        GameObject canvasObject = new GameObject("ArmoryV2ValidationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        GameObject screen = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, canvasObject.transform);
        Stretch(Rect(screen));
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ============================================================
    // 05. HELPERS EDITORIAIS LOCAIS DA V2
    // ============================================================
    private static Image Panel(string name, Transform parent, float x, float y, float width, float height)
    {
        Image panel = ChildImage(name, parent, new Color(9f / 255f, 14f / 255f, 19f / 255f, .91f), theme.MasterRoundedFill);
        PlaceTopLeft(panel.rectTransform, x, y, width, height);
        Image border = ChildImage("Border", panel.transform, new Color(theme.MasterGold.r, theme.MasterGold.g, theme.MasterGold.b, .24f), theme.MasterRoundedOutline);
        Stretch(border.rectTransform);
        border.raycastTarget = false;
        return panel;
    }

    private static void BuildSlot(Transform parent, string label, Texture texture, string energy, bool support)
    {
        GameObject root = UI(label.Replace(" ", "") + "Slot", parent);
        Image surface = Image(root, new Color(18f / 255f, 26f / 255f, 36f / 255f, .85f), theme.MasterRoundedFill);
        surface.raycastTarget = true;
        root.AddComponent<Button>().targetGraphic = surface;
        Image border = ChildImage("Border", root.transform, new Color(1f, 1f, 1f, .07f), theme.MasterRoundedOutline);
        Stretch(border.rectTransform);
        border.gameObject.SetActive(texture != null);
        if (texture != null)
        {
            RectTransform frame = Rect(UI("ArtworkFrame", root.transform));
            PlaceTopLeft(frame, 6f, 6f, 112f, 104f);
            frame.gameObject.AddComponent<RectMask2D>();
            RawImage artwork = UI("Artwork", frame).AddComponent<RawImage>();
            artwork.texture = texture;
            artwork.raycastTarget = false;
            Stretch(artwork.rectTransform);
            AspectRatioFitter fit = artwork.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;
        }
        TMP_Text name = Text("Name", root.transform, label, theme.MasterGabaritoBold, texture == null ? 11f : 13f,
            texture == null ? (support ? theme.MasterCyan : theme.MasterGold) : theme.MasterTextPrimary,
            texture == null ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft);
        PlaceTopLeft(name.rectTransform, 6f, 119f, 112f, 38f);
        name.textWrappingMode = TextWrappingModes.Normal;
        name.enableAutoSizing = true;
        name.fontSizeMin = 9f;
        name.fontSizeMax = 13f;
        name.maxVisibleLines = 2;
        name.overflowMode = TextOverflowModes.Truncate;
        TMP_Text energyLabel = Text("Energy", root.transform, energy, theme.MasterGabaritoBold, 12f,
            support ? theme.MasterCyan : theme.MasterGold, TextAlignmentOptions.Left);
        PlaceBottomLeft(energyLabel.rectTransform, 6f, 8f, 70f, 22f);
        name.gameObject.SetActive(texture != null);
        energyLabel.gameObject.SetActive(texture != null);

        // 05.1 — O estado vazio é uma árvore independente e declarativa. Isso
        // preserva nome e energia do estado ocupado e reproduz o card pontilhado.
        GameObject emptyState = UI("EmptyState", root.transform);
        Stretch(Rect(emptyState), 2f, 2f, 2f, 2f);
        // 05.1.1 — O CanvasRenderer é a ponte obrigatória entre a malha do
        // Graphic customizado e o Canvas; deve permanecer serializado.
        if (emptyState.GetComponent<CanvasRenderer>() == null)
            emptyState.AddComponent<CanvasRenderer>();
        WDDashedRoundedBorderGraphic dashed = emptyState.AddComponent<WDDashedRoundedBorderGraphic>();
        dashed.Configure(1.5f, 5f, 4f, 10f,
            new Color(theme.MasterGold.r, theme.MasterGold.g, theme.MasterGold.b, .65f));
        dashed.raycastTarget = false;
        TMP_Text plus = Text("Plus", emptyState.transform, "+", theme.MasterGabaritoBold, 16f,
            theme.MasterGold, TextAlignmentOptions.Center);
        plus.enableAutoSizing = false;
        plus.fontSizeMin = plus.fontSizeMax = 16f;
        Center(plus.rectTransform, 30f, 20f);
        plus.rectTransform.anchoredPosition = new Vector2(0f, 11f);
        TMP_Text emptyLabel = Text("Label", emptyState.transform, "LIVRE",
            theme.MasterGabaritoBold, 12f, new Color32(214, 220, 225, 255), TextAlignmentOptions.Center);
        emptyLabel.enableAutoSizing = false;
        emptyLabel.fontSizeMin = emptyLabel.fontSizeMax = 12f;
        Center(emptyLabel.rectTransform, 108f, 20f);
        emptyLabel.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        TMP_Text hint = Text("Hint", emptyState.transform, "Adicionar do\ncatálogo", theme.MasterInterRegular, 11f,
            theme.MasterTextSecondary, TextAlignmentOptions.Center);
        Center(hint.rectTransform, 110f, 36f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -41f);
        hint.gameObject.SetActive(false);
        emptyState.SetActive(texture == null);
        Image remove = ChildImage("Remove", root.transform,
            new Color(172f / 255f, 45f / 255f, 45f / 255f, .94f), theme.MasterRoundedFill);
        PlaceTopLeft(remove.rectTransform, 8f, 8f, 26f, 26f);
        Button removeButton = remove.gameObject.AddComponent<Button>();
        removeButton.targetGraphic = remove;
        removeButton.transition = Selectable.Transition.None;
        TMP_Text removeLabel = Text("Label", remove.transform, "×", theme.MasterGabaritoBold, 13f,
            Color.white, TextAlignmentOptions.Center);
        Stretch(removeLabel.rectTransform);
        removeLabel.raycastTarget = false;
        remove.gameObject.SetActive(texture != null);
    }

    private static void BuildEconomy(Transform parent, string name, Sprite iconSprite, string value, float width)
    {
        Image root = ChildImage(name, parent, theme.MasterSurfaceElevated, theme.MasterRoundedFill);
        root.rectTransform.sizeDelta = new Vector2(width, 32f);
        LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = 32f;
        Image icon = ChildImage("Icon", root.transform, Color.white, iconSprite);
        PlaceCenteredLeft(icon.rectTransform, 12f, 0f, 16f, 16f);
        TMP_Text label = Text("Value", root.transform, value, theme.MasterGabaritoBold, 15f, theme.MasterTextPrimary, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 32f, 4f, 8f, 4f);
        WDUniversalControlFactory.AddGoldOutline(root.rectTransform, theme);
    }

    private static void BuildIconButton(Transform parent, string name, Sprite iconSprite, float width, float height)
    {
        Image root = ChildImage(name, parent, theme.MasterSurfaceElevated, theme.MasterRoundedFill);
        root.rectTransform.sizeDelta = new Vector2(width, height);
        LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        root.gameObject.AddComponent<Button>().targetGraphic = root;
        Image icon = ChildImage("Icon", root.transform, theme.MasterTextSecondary, iconSprite);
        Center(icon.rectTransform, 18f, 18f);
        WDUniversalControlFactory.AddGoldOutline(root.rectTransform, theme);
    }

    private static Button Button(string name, Transform parent, string label, TMP_FontAsset font, float size,
        Color textColor, Color surfaceColor, float width, float height)
    {
        GameObject root = UI(name, parent);
        Rect(root).sizeDelta = new Vector2(width, height);
        Image surface = Image(root, surfaceColor, theme.MasterRoundedFill);
        surface.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = surface;
        TMP_Text text = Text("Label", root.transform, label, font, size, textColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
        return button;
    }

    private static GameObject UI(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null) gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image ChildImage(string name, Transform parent, Color color, Sprite sprite)
    {
        return Image(UI(name, parent), color, sprite);
    }

    private static Image Image(GameObject gameObject, Color color, Sprite sprite)
    {
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f
            ? UnityEngine.UI.Image.Type.Sliced
            : UnityEngine.UI.Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size,
        Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = UI(name, parent).AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Texture2D Art(string id) => catalog.Resolve(id);

    private static string DisplayName(string id)
    {
        return id switch
        {
            "drone" => "DRONE", "atomic_bomb" => "ATAQUE NUCLEAR", "war_propaganda" => "PROPAGANDA",
            "chemical_bomb" => "BOMBA QUÍMICA", "infiltrator" => "INFILTRADO", "sabotage" => "SABOTAGEM",
            "energy_plant" => "CENTRAL DE ENERGIA", "long_range_bomb" => "ATAQUE DE PRECISÃO", "peace" => "PAZ",
            "artillery" => "ARTILHARIA", "rockets" => "FOGUETES", "minefield" => "CAMPO MINADO",
            "blitzkrieg" => "BLITZKRIEG", "transfer_nullifier" => "BLOQUEIO DE TRANSFERÊNCIA", "sniper" => "SNIPER",
            "kamikaze" => "KAMIKAZE", "paratroopers" => "PARAQUEDISTAS", "radar" => "RADAR",
            "air_strike" => "ATAQUE AÉREO", "reinforcements" => "REFORÇOS", "air_transport" => "TRANSPORTE AÉREO",
            "flak_tower" => "DEFESA ANTIAÉREA", "bunker" => "BUNKER", "reserve_troops" => "TROPAS RESERVAS",
            "troop_recovery" => "RECUPERAÇÃO DE TROPAS", "mercenaries" => "MERCENÁRIOS", _ => id.ToUpperInvariant()
        };
    }

    private static RectTransform Rect(GameObject gameObject) => gameObject.GetComponent<RectTransform>();
    private static void Assign(SerializedObject target, string property, UnityEngine.Object value) => target.FindProperty(property).objectReferenceValue = value;
    private static void AssignArray(SerializedObject target, string property, UnityEngine.Object[] values)
    {
        SerializedProperty array = target.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
    private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static void Center(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(width, height);
    }
    private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void PlaceTopRight(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f); rect.anchoredPosition = new Vector2(-x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void PlaceBottomLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void PlaceBottomRight(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f); rect.anchoredPosition = new Vector2(-x, y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void PlaceCenteredLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void PlaceCenteredRight(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, .5f); rect.anchoredPosition = new Vector2(-x, y); rect.sizeDelta = new Vector2(width, height);
    }
}
#endif


#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WDArmorySectorPrefabBuilder
{
    public const bool IsRetired = true;
    public const string RetirementReason = "Builder visual aposentado pela Fase 0 do saneamento global. Os Prefabs ativos do Arsenal são preservados e não podem ser regenerados por esta ferramenta.";
    const string CardPath="Assets/Resources/UI/Armory/Prefabs/WDArmoryWeaponCard.prefab";
    const string ScreenPath="Assets/Resources/UI/Armory/Prefabs/WDArmoryScreen.prefab";
    const string VisualCatalogPath="Assets/Resources/UI/Armory/WDArmoryVisualCatalog.asset";
    const string VisualKitRoot="Assets/VisualKit/";
    const string CanonicalArtworkRoot="Assets/Resources/UI/Arsenal/Canonical/";
    static TMP_FontAsset font = null;
    static TMP_FontAsset displayFont = null;
    static readonly Color Navy=new Color32(5,13,22,248), Panel=new Color32(8,22,35,245), Alt=new Color32(12,31,48,245), Border=new Color32(68,83,94,180), Gold=new Color32(197,155,99,255), Cyan=new Color32(13,194,219,255), White=new Color32(245,246,248,255), Muted=new Color32(158,176,188,255);

    // ============================================================
    // 01. AUTORIA EDITORIAL SERIALIZADA
    // ============================================================
    [MenuItem("War Dominion/Create/Armory Sector Prefabs")]
    [Obsolete(RetirementReason)]
    public static void CreateSectorPrefabs()
    {
        // 01.1 — Neutralização reversível do gerador visual legado.
        // Este ponto de entrada permanece apenas para explicar por que a geração não é mais permitida.
        Debug.LogWarning("[WD-UI-PROTECTION] " + RetirementReason);
    }

    // ============================================================
    // 01.1 CATÁLOGO VISUAL SERIALIZADO — ARTWORKS MASTER LIMPOS
    // ============================================================
    static void BuildVisualCatalog()
    {
        WDArmoryVisualCatalog catalog=AssetDatabase.LoadAssetAtPath<WDArmoryVisualCatalog>(VisualCatalogPath);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<WDArmoryVisualCatalog>();AssetDatabase.CreateAsset(catalog,VisualCatalogPath);}
        // 01.2 — Mesmo aposentado, o mapeamento retido não pode reintroduzir a biblioteca visual antiga.
        (string id,string file)[] map={
            ("drone","weapon-artwork-card-drone.png"),("atomic_bomb","weapon-artwork-card-bomba-atomica.png"),
            ("war_propaganda","weapon-artwork-card-propaganda-de-guerra.png"),("chemical_bomb","weapon-artwork-card-bomba-quimica.png"),
            ("infiltrator","weapon-artwork-card-infiltrado.png"),("sabotage","weapon-artwork-card-sabotagem.png"),
            ("energy_plant","weapon-artwork-card-central-de-energia.png"),("long_range_bomb","weapon-artwork-card-bomba-de-longo-alcance.png"),
            ("peace","weapon-artwork-card-paz.png"),("artillery","weapon-artwork-card-artilharia.png"),
            ("rockets","weapon-artwork-card-foguetes.png"),("minefield","weapon-artwork-card-campo-minado.png"),
            ("blitzkrieg","weapon-artwork-card-blitzkrieg.png"),("transfer_nullifier","weapon-artwork-card-anulador-de-transferencia.png"),
            ("sniper","weapon-artwork-card-sniper.png"),("kamikaze","weapon-artwork-card-kamikaze.png"),
            ("paratroopers","weapon-artwork-card-paraquedistas.png"),("radar","weapon-artwork-card-radar.png"),
            ("air_strike","weapon-artwork-ataque-aereo.png"),("reinforcements","weapon-artwork-card-reforcos.png"),
            ("air_transport","weapon-artwork-card-transporte-aereo.png"),("flak_tower","weapon-artwork-card-torre-antiaerea.png"),
            ("bunker","weapon-artwork-card-bunker.png"),("reserve_troops","weapon-artwork-card-tropas-reservas.png"),
            ("troop_recovery","weapon-artwork-card-recuperacao-de-tropas.png"),("mercenaries","weapon-artwork-card-mercenarios.png")};
        var entries=map.Select(item=>new WDArmoryVisualCatalog.Entry(item.id,
            AssetDatabase.LoadAssetAtPath<Texture2D>(CanonicalArtworkRoot+item.file))).ToArray();
        if(entries.Any(entry=>entry.Artwork==null))throw new MissingReferenceException("Artwork canônico ausente no Arsenal.");
        catalog.Configure(entries);EditorUtility.SetDirty(catalog);
    }

    static GameObject BuildCard()
    {
        GameObject root=UI("WDArmoryWeaponCard",null); R(root).sizeDelta=new Vector2(178,202); Image surface=G<Image>(root,Panel); surface.raycastTarget=true; Button button=root.AddComponent<Button>(); button.targetGraphic=surface;
        Image border=Img("SelectionBorder",root.transform,Color.clear); Stretch(border.rectTransform,0); Outline cardOutline=border.gameObject.AddComponent<Outline>(); cardOutline.effectColor=Border; cardOutline.effectDistance=new Vector2(1,-1);
        Image frame=Img("ArtworkFrame",root.transform,Navy); Place(frame.rectTransform,.5f,1,0,-8,162,112,.5f,1); frame.gameObject.AddComponent<RectMask2D>(); RawImage art=UI("Artwork",frame.transform).AddComponent<RawImage>(); art.raycastTarget=false; Stretch(art.rectTransform,4); AspectRatioFitter artFit=art.gameObject.AddComponent<AspectRatioFitter>(); artFit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
        TMP_Text name=Txt("WeaponName",root.transform,"ARTILHARIA",12,White,TextAlignmentOptions.Left); Place(name.rectTransform,0,1,9,-126,126,20,0,1);
        TMP_Text energy=Txt("Energy",root.transform,"ϟ 4",10,Gold,TextAlignmentOptions.Right); Place(energy.rectTransform,1,1,-9,-126,36,20,1,1);
        TMP_Text meta=Txt("CategoryAndLevel",root.transform,"TÁTICA · NÍVEL 1",9,Muted,TextAlignmentOptions.Left); Place(meta.rectTransform,0,1,9,-150,160,16,0,1);
        Image track=Img("TokenTrack",root.transform,new Color32(35,48,59,255)); Place(track.rectTransform,.5f,0,0,22,160,4,.5f,0); Image fill=Img("TokenFill",track.transform,Cyan); fill.rectTransform.anchorMin=Vector2.zero; fill.rectTransform.anchorMax=new Vector2(.35f,1); fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
        TMP_Text tokens=Txt("TokenLabel",root.transform,"12/40",8,Muted,TextAlignmentOptions.Left); Place(tokens.rectTransform,0,0,9,6,70,13,0,0);
        Image pill=Img("StatePill",root.transform,new Color(Cyan.r,Cyan.g,Cyan.b,.18f)); Place(pill.rectTransform,1,0,-7,7,55,15,1,0); TMP_Text state=Txt("StateLabel",pill.transform,"EQUIPADA",7,Cyan,TextAlignmentOptions.Center); Stretch(state.rectTransform,2,0);
        WDArmoryWeaponCardView view=root.AddComponent<WDArmoryWeaponCardView>(); SerializedObject so=new(view); A(so,"theme",AssetDatabase.LoadAssetAtPath<WarDominionUITheme>("Assets/Resources/UI/WarDominionUITheme.asset")); A(so,"selectionButton",button); A(so,"surface",surface); A(so,"selectionBorder",border); A(so,"selectionOutline",cardOutline); A(so,"artwork",art); A(so,"statePill",pill); A(so,"weaponName",name); A(so,"categoryAndLevel",meta); A(so,"energyLabel",energy); A(so,"tokenLabel",tokens); A(so,"stateLabel",state); A(so,"tokenFill",fill); so.ApplyModifiedPropertiesWithoutUndo(); return root;
    }

    static GameObject BuildScreen(GameObject cardPrefab)
    {
        GameObject root=UI("WDArmoryScreen",null); Stretch(R(root),0); WDArmoryScreenView view=root.AddComponent<WDArmoryScreenView>();
        RawImage bg=UI("Background",root.transform).AddComponent<RawImage>(); bg.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(VisualKitRoot+"Arsenal/Backgrounds/Arsenal_Ambiente_1920x1080.png"); bg.raycastTarget=false; Stretch(bg.rectTransform,0); AspectRatioFitter fit=bg.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio=16f/9f;
        Image veil=Img("ReadabilityVeil",root.transform,new Color(3f/255f,8f/255f,13f/255f,.70f)); Stretch(veil.rectTransform,0);
        Image header=Pane("Header",root.transform,new Color32(9,14,19,232)); TopStretch(header.rectTransform,32,32,24,72); Button back=Btn("BackButton",header.transform,"VOLTAR",14,Gold,Alt); Place(R(back.gameObject),0,.5f,18,0,140,46,0,.5f); RawImage backIcon=Icon("Chevron",back.transform,VisualKitRoot+"Icons/Shared/Runtime/Chevron_Left.png"); Place(backIcon.rectTransform,0,.5f,16,0,16,16,0,.5f); back.GetComponentInChildren<TMP_Text>().margin=new Vector4(20,0,0,0); TMP_Text title=Txt("Title",header.transform,"ARSENAL",34,White,TextAlignmentOptions.Center,displayFont); Stretch(title.rectTransform,250,0);
        TMP_Text coins=Economy("Coins",header.transform,"12.450",-176,VisualKitRoot+"Icons/Shared/Runtime/Coin.png"); TMP_Text gems=Economy("Gems",header.transform,"320",-70,VisualKitRoot+"Icons/Shared/Runtime/Gem.png"); Button bell=Btn("Notifications",header.transform,"",16,Muted,Alt); Place(R(bell.gameObject),1,.5f,-24,0,36,32,1,.5f); RawImage bellIcon=Icon("Bell",bell.transform,VisualKitRoot+"Icons/Shared/Runtime/Bell.png"); Place(bellIcon.rectTransform,.5f,.5f,0,0,18,18,.5f,.5f);
        Image deck=Pane("DeckPanel",root.transform,Panel); Place(deck.rectTransform,0,1,32,-112,470,944,0,1); TMP_InputField deckName=Input("DeckNameInput",deck.transform,"NOME DO DECK"); Place(R(deckName.gameObject),0,1,16,-16,300,32,0,1); deckName.textComponent.fontSize=20; TMP_Text deckMeta=Txt("DeckMeta",deck.transform,"DECK 1 · 0/8 PRINCIPAIS · 0/2 APOIO",10,Muted,TextAlignmentOptions.Left); Place(deckMeta.rectTransform,0,1,16,-52,420,18,0,1);
        Button[] deckButtons=new Button[5]; TMP_Text[] deckLabels=new TMP_Text[5]; Image[] deckLocks=new Image[5]; float deckX=16f; for(int i=0;i<5;i++){float width=82.4f;deckButtons[i]=Btn("Deck"+(i+1),deck.transform,"DECK "+(i+1),10,i==0?Cyan:Muted,i==0?new Color(Cyan.r,Cyan.g,Cyan.b,.09f):Alt); Place(R(deckButtons[i].gameObject),0,1,deckX,-82,width,38,0,1); deckLabels[i]=deckButtons[i].GetComponentInChildren<TMP_Text>(); deckLocks[i]=Img("Lock",deckButtons[i].transform,Color.white); deckLocks[i].sprite=AssetDatabase.LoadAssetAtPath<Sprite>(VisualKitRoot+"Icons/Arsenal/Runtime/Lock.png"); Place(deckLocks[i].rectTransform,1,.5f,-7,0,11,11,1,.5f); deckLocks[i].gameObject.SetActive(i>=2); deckX+=width+6.5f;}
        Button save=Btn("SaveDeck",deck.transform,"SALVAR DECK",12,Gold,Alt); Place(R(save.gameObject),0,0,16,48,438,48,0,0); RawImage saveIcon=Icon("SaveIcon",save.transform,VisualKitRoot+"Icons/Arsenal/Runtime/Save.png"); Place(saveIcon.rectTransform,0,.5f,128,0,16,16,0,.5f); save.GetComponentInChildren<TMP_Text>().margin=new Vector4(18,0,0,0); TMP_Text feedback=Txt("DeckFeedback",deck.transform,"",9,Cyan,TextAlignmentOptions.Center); Place(feedback.rectTransform,.5f,0,0,18,438,18,.5f,0);
        TMP_Text mainMeta=Txt("MainLabel",deck.transform,"PRINCIPAIS · 0/8",10,Gold,TextAlignmentOptions.Left); Place(mainMeta.rectTransform,0,1,16,-134,200,18,0,1); TMP_Text supportMeta=Txt("SupportLabel",deck.transform,"APOIO · 0/2",10,Cyan,TextAlignmentOptions.Left); Place(supportMeta.rectTransform,0,1,16,-522,200,18,0,1);
        WDArmorySlotView[] mainSlots=Slots("MainSlots",deck.transform,8,false,16,438); Place(mainSlots[0].transform.parent.GetComponent<RectTransform>(),0,1,16,-158,438,342,0,1); WDArmorySlotView[] supportSlots=Slots("SupportSlots",deck.transform,2,true,16,216); Place(supportSlots[0].transform.parent.GetComponent<RectTransform>(),0,1,16,-546,216,166,0,1);
        Image catalog=Pane("CatalogPanel",root.transform,Panel); Place(catalog.rectTransform,0,1,518,-112,810,944,0,1);
        TMP_Text collection=Txt("CollectionTitle",catalog.transform,"CATÁLOGO",18,White,TextAlignmentOptions.Left,displayFont); Place(collection.rectTransform,0,1,20,-16,150,28,0,1); TMP_Text countLabel=Txt("CollectionCount",catalog.transform,"26 ARMAS",10,Gold,TextAlignmentOptions.Left); Place(countLabel.rectTransform,0,1,136,-19,100,22,0,1); TMP_InputField search=Input("Search",catalog.transform,"BUSCAR ARMA..."); Place(R(search.gameObject),1,1,-20,-16,154,36,1,1); RawImage searchIcon=Icon("SearchIcon",search.transform,VisualKitRoot+"Icons/Arsenal/Runtime/Search.png"); Place(searchIcon.rectTransform,0,.5f,12,0,14,14,0,.5f); search.textComponent.margin=new Vector4(22,0,0,0); ((TMP_Text)search.placeholder).margin=new Vector4(22,0,0,0);
        string[] names={"TODAS","APOIO","TÁTICA","ESTRATÉGICA","ESPECIAL"}; Button[] filters=new Button[names.Length]; TMP_Text[] filterLabels=new TMP_Text[names.Length]; for(int i=0;i<names.Length;i++){filters[i]=Btn("Filter"+i,catalog.transform,names[i],10,i==0?Navy:Muted,i==0?Gold:Alt); Place(R(filters[i].gameObject),0,1,20+i*122,-58,112,30,0,1); filterLabels[i]=filters[i].GetComponentInChildren<TMP_Text>();}
        RectTransform catalogViewport=R(UI("CatalogViewport",catalog.transform)); catalogViewport.anchorMin=Vector2.zero; catalogViewport.anchorMax=Vector2.one; catalogViewport.offsetMin=new Vector2(20,28); catalogViewport.offsetMax=new Vector2(-20,-100); Image catalogViewportGraphic=G<Image>(catalogViewport.gameObject,Color.clear); catalogViewportGraphic.raycastTarget=true; catalogViewport.gameObject.AddComponent<RectMask2D>(); ScrollRect catalogScroll=catalogViewport.gameObject.AddComponent<ScrollRect>(); catalogScroll.horizontal=false; catalogScroll.vertical=true; catalogScroll.movementType=ScrollRect.MovementType.Clamped; catalogScroll.viewport=catalogViewport;
        RectTransform grid=R(UI("WeaponGrid",catalogViewport)); grid.anchorMin=new Vector2(0,1); grid.anchorMax=new Vector2(1,1); grid.pivot=new Vector2(.5f,1); grid.anchoredPosition=Vector2.zero; grid.sizeDelta=Vector2.zero; GridLayoutGroup gl=grid.gameObject.AddComponent<GridLayoutGroup>(); gl.cellSize=new Vector2(178,203); gl.spacing=new Vector2(10,12); gl.constraint=GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount=4; ContentSizeFitter catalogFitter=grid.gameObject.AddComponent<ContentSizeFitter>(); catalogFitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize; WDArmoryCatalogGridSizer gridSizer=grid.gameObject.AddComponent<WDArmoryCatalogGridSizer>(); SerializedObject gridSizerSo=new(gridSizer); A(gridSizerSo,"viewport",catalogViewport); gridSizerSo.ApplyModifiedPropertiesWithoutUndo(); catalogScroll.content=grid; WDArmoryWeaponCardView[] cards=new WDArmoryWeaponCardView[26]; for(int i=0;i<26;i++) cards[i]=UnityEngine.Object.Instantiate(cardPrefab,grid).GetComponent<WDArmoryWeaponCardView>();
        Image details=Pane("DetailsPanel",root.transform,Panel); Place(details.rectTransform,0,1,1344,-112,544,944,0,1); Image detailFrame=Pane("DetailArtworkFrame",details.transform,Navy); Place(detailFrame.rectTransform,0,1,24,-24,280,280,0,1); detailFrame.gameObject.AddComponent<RectMask2D>(); RawImage detailArt=UI("Artwork",detailFrame.transform).AddComponent<RawImage>(); detailArt.raycastTarget=false; Stretch(detailArt.rectTransform,0); AspectRatioFitter detailFit=detailArt.gameObject.AddComponent<AspectRatioFitter>(); detailFit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
        TMP_Text category=DL("Category",details.transform,"TÁTICA",11,Gold,322,-24,198,20), detailName=DL("WeaponName",details.transform,"ARTILHARIA",25,White,322,-54,198,78), facts=DL("Facts",details.transform,"NÍVEL 1 · ϟ 4 · NÃO EQUIPADA",11,Muted,322,-142,198,42), description=DL("Description",details.transform,"Aumenta a força de ataque na próxima ofensiva.",12,White,322,-196,198,108), evolution=DL("EvolutionTitle",details.transform,"EVOLUÇÃO",20,White,24,-392,496,30), tokenText=DL("Tokens",details.transform,"12/40 TOKENS",22,Cyan,24,-436,280,30), missing=DL("Missing",details.transform,"FALTAM 28",11,Muted,304,-440,216,22), benefit=DL("Benefit",details.transform,"PRÓXIMO NÍVEL\nBenefício ampliado conforme a progressão da arma.",13,White,24,-512,496,78), cost=DL("UpgradeCost",details.transform,"CUSTO 500 COINS",12,Gold,24,-610,280,34); description.textWrappingMode=benefit.textWrappingMode=TextWrappingModes.Normal; missing.alignment=TextAlignmentOptions.Right;
        Image tokenTrack=Img("TokenTrack",details.transform,new Color32(35,48,59,255)); Place(tokenTrack.rectTransform,0,1,24,-478,496,6,0,1); Image tokenFill=Img("TokenFill",tokenTrack.transform,Cyan); tokenFill.rectTransform.anchorMin=Vector2.zero; tokenFill.rectTransform.anchorMax=new Vector2(.3f,1); tokenFill.rectTransform.offsetMin=tokenFill.rectTransform.offsetMax=Vector2.zero;
        Button upgrade=Btn("Upgrade",details.transform,"EVOLUIR",12,Muted,Alt); ColorBlock upgradeColors=upgrade.colors; upgradeColors.normalColor=Color.white; upgradeColors.disabledColor=new Color(.55f,.62f,.68f,.45f); upgrade.colors=upgradeColors; Place(R(upgrade.gameObject),1,1,-24,-606,148,48,1,1); Button add=Btn("AddToDeck",details.transform,"+  ADICIONAR AO DECK",14,Navy,Gold); Place(R(add.gameObject),.5f,0,0,78,496,48,.5f,0); TMP_Text hint=Txt("DetailHint",details.transform,"",10,Muted,TextAlignmentOptions.Center); Place(hint.rectTransform,.5f,0,0,42,496,22,.5f,0);
        SerializedObject so=new(view); A(so,"backButton",back);A(so,"coins",coins);A(so,"gems",gems);A(so,"deckNameInput",deckName);A(so,"deckMeta",deckMeta);A(so,"mainMeta",mainMeta);A(so,"supportMeta",supportMeta);A(so,"feedback",feedback);A(so,"saveButton",save); AA(so,"deckButtons",deckButtons);AA(so,"deckLabels",deckLabels);AA(so,"deckLockIcons",deckLocks);AA(so,"mainSlots",mainSlots);AA(so,"supportSlots",supportSlots);A(so,"searchField",search);AA(so,"filterButtons",filters);AA(so,"filterLabels",filterLabels);AA(so,"catalogCards",cards);A(so,"detailArtwork",detailArt);A(so,"detailCategory",category);A(so,"detailName",detailName);A(so,"detailFacts",facts);A(so,"detailDescription",description);A(so,"detailEvolution",evolution);A(so,"detailTokens",tokenText);A(so,"detailMissing",missing);A(so,"detailTokenFill",tokenFill);A(so,"detailBenefit",benefit);A(so,"detailUpgradeCost",cost);A(so,"upgradeButton",upgrade);A(so,"addButton",add);A(so,"detailHint",hint);so.ApplyModifiedPropertiesWithoutUndo(); return root;
    }

    // ============================================================
    // 02. SUBÁRVORES DECLARATIVAS
    // ============================================================
    static WDArmorySlotView[] Slots(string name,Transform parent,int count,bool support,float margin,float width)
    {
        RectTransform group=R(UI(name,parent)); Place(group,0,1,margin,-105,width,count>2?342:166,0,1);
        GridLayoutGroup layout=group.gameObject.AddComponent<GridLayoutGroup>(); int columns=count>2?4:2; float gap=6f; float slotWidth=(width-(columns-1)*gap)/columns; float slotHeight=count>columns?168:166; layout.cellSize=new Vector2(slotWidth,slotHeight); layout.spacing=new Vector2(gap,gap); layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount=columns;
        WDArmorySlotView[] result=new WDArmorySlotView[count];
        for(int i=0;i<count;i++)
        {
            GameObject slot=UI("Slot"+(i+1),group); R(slot).sizeDelta=new Vector2(slotWidth,slotHeight);
            Image surface=G<Image>(slot,new Color32(18,26,36,217)); surface.raycastTarget=true; Button select=slot.AddComponent<Button>(); select.targetGraphic=surface;
            Outline outline=slot.AddComponent<Outline>(); outline.effectColor=new Color32(197,155,99,90); outline.effectDistance=new Vector2(1,-1);
            Image artworkFrame=Img("ArtworkFrame",slot.transform,Navy); Place(artworkFrame.rectTransform,.5f,1,0,-6,slotWidth-12,88,.5f,1); artworkFrame.gameObject.AddComponent<RectMask2D>(); Outline artworkOutline=artworkFrame.gameObject.AddComponent<Outline>(); artworkOutline.effectColor=new Color32(197,155,99,70); artworkOutline.effectDistance=new Vector2(1,-1); RawImage artwork=UI("Artwork",artworkFrame.transform).AddComponent<RawImage>(); artwork.raycastTarget=false; Stretch(artwork.rectTransform,3); AspectRatioFitter artworkAspect=artwork.gameObject.AddComponent<AspectRatioFitter>(); artworkAspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent; artworkAspect.aspectRatio=1f;
            TMP_Text plus=Txt("EmptyPlus",slot.transform,"+",17,support?Cyan:Gold,TextAlignmentOptions.Center); Place(plus.rectTransform,.5f,.5f,0,18,24,20,.5f,.5f);
            TMP_Text empty=Txt("Empty",slot.transform,support?"APOIO LIVRE":"PRINCIPAL LIVRE",9,support?Cyan:Gold,TextAlignmentOptions.Center); Place(empty.rectTransform,.5f,.5f,0,-8,slotWidth-10,18,.5f,.5f);
            TMP_Text emptyHint=Txt("EmptyHint",slot.transform,"Adicionar",8,Muted,TextAlignmentOptions.Center); Place(emptyHint.rectTransform,.5f,0,0,8,slotWidth-10,14,.5f,0);
            TMP_Text weapon=Txt("WeaponName",slot.transform,"ARMA",9,White,TextAlignmentOptions.Left); Place(weapon.rectTransform,0,0,6,28,slotWidth-30,30,0,0);
            TMP_Text energy=Txt("Energy",slot.transform,"ϟ 4",9,Gold,TextAlignmentOptions.Left); Place(energy.rectTransform,0,0,6,8,48,16,0,0);
            Button remove=Btn("Remove",slot.transform,"×",12,Muted,Color.clear); Place(R(remove.gameObject),1,0,-5,8,22,22,1,0);
            result[i]=slot.AddComponent<WDArmorySlotView>(); SerializedObject so=new(result[i]);
            A(so,"selectionButton",select); A(so,"removeButton",remove); A(so,"artwork",artwork); A(so,"artworkFrame",artworkFrame); A(so,"artworkAspect",artworkAspect); A(so,"weaponName",weapon); A(so,"energy",energy); A(so,"emptyLabel",empty); A(so,"emptyHint",emptyHint); A(so,"emptyPlus",plus); A(so,"surface",surface); A(so,"selectionOutline",outline); so.ApplyModifiedPropertiesWithoutUndo();
        }
        return result;
    }
    static RectTransform Picker(Transform parent,GameObject prefab,out Button blocker,out Button close,out Button cancel,out TMP_Text title,out TMP_Text subtitle,out TMP_Text count,out TMP_Text feedback,out WDArmoryWeaponCardView[] cards)
    {
        RectTransform root=R(UI("WeaponPicker",parent)); Stretch(root,0);
        Image dim=G<Image>(root.gameObject,new Color(0,0,0,.72f)); dim.raycastTarget=true;
        blocker=root.gameObject.AddComponent<Button>(); blocker.targetGraphic=dim;

        Image p=Pane("ModalPanel",root,Navy); p.raycastTarget=true;
        Place(p.rectTransform,.5f,.5f,0,0,872,752,.5f,.5f);
        title=Txt("Title",p.transform,"ADICIONAR ARMA · PRINCIPAL",24,White,TextAlignmentOptions.Left);
        Place(title.rectTransform,0,1,24,-22,650,34,0,1);
        subtitle=Txt("Subtitle",p.transform,"SELECIONE UMA ARMA ELEGÍVEL PARA ESTE SLOT",10,Muted,TextAlignmentOptions.Left);
        Place(subtitle.rectTransform,0,1,24,-61,650,18,0,1);
        close=Btn("Close",p.transform,"×",22,White,Color.clear);
        Place(R(close.gameObject),1,1,-20,-20,42,42,1,1);
        count=Txt("Count",p.transform,"0 OPÇÕES",10,Gold,TextAlignmentOptions.Right);
        Place(count.rectTransform,1,1,-72,-70,160,18,1,1);

        // 4.1 A viewport serializada mantém todas as 26 opções sem recriar a árvore visual.
        RectTransform viewport=R(UI("PickerViewport",p.transform));
        Place(viewport,.5f,1,0,-112,752,500,.5f,1);
        Image viewportGraphic=G<Image>(viewport.gameObject,Color.clear); viewportGraphic.raycastTarget=true;
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll=viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.viewport=viewport;

        RectTransform grid=R(UI("EligibleGrid",viewport));
        grid.anchorMin=new Vector2(0,1); grid.anchorMax=new Vector2(1,1); grid.pivot=new Vector2(.5f,1);
        grid.anchoredPosition=Vector2.zero; grid.sizeDelta=Vector2.zero;
        GridLayoutGroup gl=grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize=new Vector2(178,174); gl.spacing=new Vector2(13,14);
        gl.constraint=GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount=4;
        ContentSizeFitter fitter=grid.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        cards=new WDArmoryWeaponCardView[26];
        for(int i=0;i<cards.Length;i++) cards[i]=UnityEngine.Object.Instantiate(prefab,grid).GetComponent<WDArmoryWeaponCardView>();
        scroll.content=grid;

        feedback=Txt("Feedback",p.transform,"",11,Cyan,TextAlignmentOptions.Center);
        Place(feedback.rectTransform,.5f,0,0,92,760,28,.5f,0);
        cancel=Btn("Cancel",p.transform,"CANCELAR",13,White,Alt);
        Place(R(cancel.gameObject),.5f,0,0,28,220,46,.5f,0);
        return root;
    }

    // ============================================================
    // 03. VALIDAÇÃO AUTOMÁTICA
    // ============================================================
    [MenuItem("War Dominion/Validate/Armory Sector Prefabs")]
    public static void ValidateSectorPrefabs()
    {
        GameObject card=AssetDatabase.LoadAssetAtPath<GameObject>(CardPath),screen=AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        if(card==null||screen==null)throw new MissingReferenceException("Prefabs Arsenal ausentes");
        foreach(GameObject root in new[]{card,screen})foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.GetComponents<Graphic>().Length>1)throw new InvalidOperationException("Graphics concorrentes em "+t.name);
        WDArmoryScreenView view=screen.GetComponent<WDArmoryScreenView>(); if(view==null)throw new MissingComponentException("WDArmoryScreenView ausente");
        if(view.DeckButtons.Length!=5||view.MainSlots.Length!=8||view.SupportSlots.Length!=2||view.FilterButtons.Length!=5||view.CatalogCards.Length!=26)throw new InvalidOperationException("Quantidade serializada divergente do setor Arsenal.");
        if(view.BackButton==null||view.SearchField==null||view.DeckNameInput==null||view.AddButton==null||view.UpgradeButton==null)throw new MissingReferenceException("Interação serializada ausente.");
        foreach(WDArmorySlotView slot in view.MainSlots.Concat(view.SupportSlots)){SerializedObject slotSo=new(slot);Button select=slotSo.FindProperty("selectionButton").objectReferenceValue as Button,remove=slotSo.FindProperty("removeButton").objectReferenceValue as Button;if(select==null||remove==null||select.targetGraphic==null||!select.targetGraphic.raycastTarget)throw new MissingReferenceException("Slot sem superfície selecionável: "+slot.name);}
        SerializedObject cardSo=new(card.GetComponent<WDArmoryWeaponCardView>()); if(cardSo.FindProperty("selectionOutline").objectReferenceValue==null)throw new MissingReferenceException("Outline de estado do card ausente.");

        // 03.1 — As imagens usam EnvelopeParent para preencher sem distorção.
        // Todo frame que pode receber crop precisa ser a autoridade explícita do clipping.
        Transform detailFrame=screen.transform.Find("DetailsPanel/DetailArtworkFrame");
        if(detailFrame==null||detailFrame.GetComponent<RectMask2D>()==null)throw new MissingComponentException("Preview direito sem RectMask2D.");
        RectTransform detailRect=detailFrame as RectTransform;
        if(detailRect==null||Vector2.Distance(detailRect.sizeDelta,new Vector2(280,280))>.1f)throw new InvalidOperationException("Preview direito deve permanecer 280x280.");
        foreach(WDArmorySlotView slot in view.MainSlots.Concat(view.SupportSlots))if(slot.transform.Find("ArtworkFrame")?.GetComponent<RectMask2D>()==null)throw new MissingComponentException("Slot sem clipping de artwork: "+slot.name);
        Transform cardArtwork=card.transform.Find("ArtworkFrame");
        if(cardArtwork==null||cardArtwork.GetComponent<RectMask2D>()==null)throw new MissingComponentException("Card do catálogo sem clipping de artwork.");
        Debug.Log("[WD-ARMORY-SECTOR] Prefabs, GUIDs, referências, raycasts e Graphics validados.",screen);
    }

    // ============================================================
    // 04. HELPERS EXCLUSIVOS DO UNITY EDITOR
    // ============================================================
    static GameObject UI(string n,Transform p){GameObject g=new(n,typeof(RectTransform));g.layer=LayerMask.NameToLayer("UI");if(p!=null)g.transform.SetParent(p,false);return g;} static RectTransform R(GameObject g)=>g.GetComponent<RectTransform>(); static T G<T>(GameObject o,Color c)where T:Graphic{T g=o.AddComponent<T>();g.color=c;g.raycastTarget=false;if(g is Image image){image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");image.type=Image.Type.Sliced;}return g;}static Image Img(string n,Transform p,Color c)=>G<Image>(UI(n,p),c);static Image Pane(string n,Transform p,Color c){Image i=Img(n,p,c);Outline o=i.gameObject.AddComponent<Outline>();o.effectColor=Border;o.effectDistance=new Vector2(1,-1);return i;}
    static TMP_Text Txt(string n,Transform p,string v,float z,Color c,TextAlignmentOptions a,TMP_FontAsset overrideFont=null){TextMeshProUGUI t=UI(n,p).AddComponent<TextMeshProUGUI>();t.text=v;t.font=overrideFont!=null?overrideFont:font;t.fontSize=z;t.fontStyle=FontStyles.Normal;t.color=c;t.alignment=a;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.raycastTarget=false;return t;}static Button Btn(string n,Transform p,string v,float z,Color tc,Color bc){GameObject g=UI(n,p);Image i=G<Image>(g,bc);i.raycastTarget=true;Button b=g.AddComponent<Button>();b.targetGraphic=i;TMP_Text t=Txt("Label",g.transform,v,z,tc,TextAlignmentOptions.Center,displayFont);Stretch(t.rectTransform,4,0);return b;}static TMP_InputField Input(string n,Transform p,string hint){GameObject g=UI(n,p);Image bg=G<Image>(g,Navy);bg.raycastTarget=true;TMP_InputField i=g.AddComponent<TMP_InputField>();TMP_Text text=Txt("Text",g.transform,"",11,White,TextAlignmentOptions.Left);Stretch(text.rectTransform,12,4);TMP_Text ph=Txt("Placeholder",g.transform,hint,11,Muted,TextAlignmentOptions.Left);Stretch(ph.rectTransform,12,4);i.textComponent=text;i.placeholder=ph;i.targetGraphic=bg;return i;}static TMP_Text DL(string n,Transform p,string s,float z,Color c,float x,float y,float w,float h){TMP_Text t=Txt(n,p,s,z,c,TextAlignmentOptions.Left);Place(t.rectTransform,0,1,x,y,w,h,0,1);return t;}
    static RawImage Icon(string name,Transform parent,string path){RawImage icon=UI(name,parent).AddComponent<RawImage>();icon.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);icon.color=Color.white;icon.raycastTarget=false;return icon;}
    static TMP_Text Economy(string name,Transform parent,string value,float x,string iconPath){Image surface=Pane(name+"Surface",parent,Alt);Place(surface.rectTransform,1,.5f,x,0,96,32,1,.5f);RawImage icon=Icon(name+"Icon",surface.transform,iconPath);Place(icon.rectTransform,0,.5f,12,0,14,14,0,.5f);TMP_Text label=Txt(name,surface.transform,value,15,White,TextAlignmentOptions.Center);Stretch(label.rectTransform,23,0);return label;}
    static void A(SerializedObject s,string n,UnityEngine.Object v){SerializedProperty p=s.FindProperty(n);if(p==null)throw new MissingFieldException(n);p.objectReferenceValue=v;}static void AA<T>(SerializedObject s,string n,T[] v)where T:UnityEngine.Object{SerializedProperty p=s.FindProperty(n);p.arraySize=v.Length;for(int i=0;i<v.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=v[i];}static void Stretch(RectTransform r,float x,float y=-1){if(y<0)y=x;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(x,y);r.offsetMax=new Vector2(-x,-y);}static void Place(RectTransform r,float ax,float ay,float x,float y,float w,float h,float px,float py){r.anchorMin=r.anchorMax=new Vector2(ax,ay);r.pivot=new Vector2(px,py);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}static void TopStretch(RectTransform r,float l,float rr,float top,float h){r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(l,-top-h);r.offsetMax=new Vector2(-rr,-top);}
}
#endif

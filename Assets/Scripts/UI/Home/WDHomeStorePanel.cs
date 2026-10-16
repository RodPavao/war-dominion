using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeStorePanel : MonoBehaviour
{
    private sealed class FactionVisual
    {
        public string Id, Name, Folder;
        public FactionVisual(string id, string name, string folder) { Id=id; Name=name; Folder=folder; }
        public string Root => "UI/Factions/Canonical/"+Folder+"/";
    }

    private static readonly FactionVisual[] Factions =
    {
        new("iron_legion","IRON LEGION","01-iron-legion"), new("spartan_dominion","SPARTAN DOMINION","02-spartan-dominion"),
        new("ghost_battalion","GHOST BATTALION","03-ghost-battalion"), new("black_ops","BLACK OPS","04-black-ops"),
        new("sons_of_odin","SONS OF ODIN","05-sons-of-odin"), new("vulcan_legion","VULCAN LEGION","06-vulcan-legion"),
        new("neon_vanguard","NEON VANGUARD","07-neon-vanguard"), new("chrome_rebellion","CHROME REBELLION","08-chrome-rebellion"),
        new("obsidian_guard","OBSIDIAN GUARD","09-obsidian-guard"), new("veteran_corps","VETERAN CORPS","10-veteran-corps"),
        new("imperial_guard","IMPERIAL GUARD","11-imperial-guard"), new("legio_invicta","LEGIO INVICTA","12-legio-invicta"),
        new("command_1944","1944 COMMAND","13-1944-command"), new("seasonal_forces","SEASONAL FORCES","14-seasonal-forces")
    };

    private static readonly Color Navy=Hex("08111D"),NavyCard=Hex("0A1727"),NavyDeep=Hex("050B13"),Gold=Hex("D4A95F"),
        White=Hex("F7F8FA"),Muted=Hex("93A0B2"),Cyan=Hex("00D9F5"),Green=Hex("25D695");
    private WarDominionUITheme theme; private WDStoreService service; private WDNavigationStack navigation;
    private Action<bool> homeVisibility; private Action notifications; private Texture2D background;
    private RectTransform frame,body; private int tab,page,detailReturnTab=1; private string detailFactionId,feedback;
    public bool IsOpen=>gameObject.activeSelf; public bool CatalogReady=>service!=null; public int ActiveTab=>tab; public string OpenCollectionId=>detailFactionId;

    // 01. AUTORIDADES, FUNDO FULLSCREEN E FRAME 1920x1080
    public void Build(WarDominionUITheme newTheme,WDStoreService newService,WDNavigationStack newNavigation,
        Texture2D backgroundTexture=null,Action<bool> setHomeVisibility=null,Action openNotifications=null)
    {
        theme=newTheme??throw new ArgumentNullException(nameof(newTheme)); service=newService??throw new ArgumentNullException(nameof(newService));
        navigation=newNavigation??throw new ArgumentNullException(nameof(newNavigation)); background=backgroundTexture;
        homeVisibility=setHomeVisibility; notifications=openNotifications;
        Image blocker=gameObject.AddComponent<Image>();blocker.color=Color.clear;blocker.raycastTarget=true;
        RectTransform scenery=R("StoreFullscreenBackground",transform);Stretch(scenery);RawImage art=scenery.gameObject.AddComponent<RawImage>();art.texture=background;art.raycastTarget=false;
        AspectRatioFitter crop=scenery.gameObject.AddComponent<AspectRatioFitter>();crop.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;crop.aspectRatio=background!=null?(float)background.width/background.height:16f/9f;
        RectTransform dim=R("ReadabilityScrim",transform);Stretch(dim);Image scrim=dim.gameObject.AddComponent<Image>();scrim.color=new Color(.016f,.035f,.059f,.80f);scrim.raycastTarget=false;
        RectTransform contentSurface=R("StoreFullscreenContentSurface",transform);Stretch(contentSurface);contentSurface.offsetMax=new Vector2(0f,-136f);
        Image contentSurfaceImage=contentSurface.gameObject.AddComponent<Image>();contentSurfaceImage.color=Color.clear;contentSurfaceImage.raycastTarget=false;
        // 01.1. A superfície mantém a hierarquia funcional sem o contorno azul
        // legado que atravessava abas, laterais e rodapé.
        frame=R("StoreResponsiveShell",transform);WDHomeSecondaryScreenFrame.Configure(frame);
        gameObject.SetActive(false);
    }
    public void Open(int selectedTab=0){tab=Mathf.Clamp(selectedTab,0,3);page=0;detailFactionId=null;feedback=null;navigation.Push(new WDNavigationContext("store",_=>Restore(),exit:HideFromNavigation));}
    private void Restore(){homeVisibility?.Invoke(false);transform.SetAsLastSibling();gameObject.SetActive(true);Render();}
    public void HideFromNavigation(){gameObject.SetActive(false);homeVisibility?.Invoke(true);}
    public bool TryHandleLocalBack(){if(detailFactionId==null)return false;detailFactionId=null;tab=detailReturnTab;Render();return true;}
    public void SelectTab(int value){tab=Mathf.Clamp(value,0,3);page=0;detailFactionId=null;feedback=null;Render();}
    public void ShowCollection(string id){if(Factions.All(f=>f.Id!=id))return;detailReturnTab=1;detailFactionId=id;tab=1;feedback=null;Render();}
    private void ShowFeaturedCollection(string id){if(Factions.All(f=>f.Id!=id))return;detailReturnTab=0;detailFactionId=id;tab=0;feedback=null;Render();}

    // 02. SHELL COMPARTILHADO — HEADER EM DUAS LINHAS E ABAS
    private void Render()
    {
        Clear(frame); Header(); Tabs();RectTransform bodyShell=R("StoreBody",frame);StretchBody(bodyShell,0,0,168,0);
        // 02.1. Páginas com duas linhas preservam 32 px abaixo dos cards e
        // passam a rolar dentro do corpo, sem recortar a última linha.
        float bodyHeight=tab==0&&detailFactionId==null?937f:(detailFactionId!=null||tab==1||tab==2?944f:912f);
        Image viewportGraphic=bodyShell.gameObject.AddComponent<Image>();viewportGraphic.color=Color.clear;viewportGraphic.raycastTarget=true;
        bodyShell.gameObject.AddComponent<RectMask2D>();body=R("StoreCanonicalContent",bodyShell);CenterTop(body,0,0,1920,bodyHeight);
        ScrollRect bodyScroll=bodyShell.gameObject.AddComponent<ScrollRect>();bodyScroll.viewport=bodyShell;bodyScroll.content=body;bodyScroll.horizontal=false;bodyScroll.vertical=bodyHeight>912f;bodyScroll.movementType=ScrollRect.MovementType.Clamped;
        if(detailFactionId!=null)RenderDetail(Factions.First(f=>f.Id==detailFactionId));
        else if(tab==0)RenderFeatured();else if(tab==1)RenderCollections();else if(tab==2)RenderAvatars();else RenderVip();
        if(!string.IsNullOrWhiteSpace(feedback))Text("Feedback",frame,feedback,15,Gold,520,1024,880,32,TextAlignmentOptions.Center,"Inter-Bold");
    }
    private void Header()
    {
        // 02.1. A Loja usa o mesmo Header completo do Arsenal.
        WDUniversalHeaderContent header=WDUniversalHeaderFactory.BuildInset(frame,theme,"LOJA",()=>{if(!TryHandleLocalBack())navigation.TryBack();},notifications==null?null:()=>notifications.Invoke());
        WDUniversalHeaderFactory.SetEconomy(header,service.Wallet);
    }
    private void Tabs(){RectTransform row=Box("Tabs",frame,32,112,1856,40,new Color(Navy.r,Navy.g,Navy.b,.82f),0,Color.clear);string[] labels={"DESTAQUES","COLEÇÕES","AVATARES","VIP"};float[] widths={120,116,112,78};float x=0;for(int i=0;i<4;i++){int selected=i;RectTransform root=R("Tab_"+labels[i],row);P(root,x,0,widths[i],40);if(i==tab){Image active=root.gameObject.AddComponent<Image>();active.color=new Color(Gold.r,Gold.g,Gold.b,.18f);WDHomeUIFactory.ApplyRounded(active,8);WDHomeUIFactory.AddThinRoundedBorder(root,Gold,8);}Text("Label",root,labels[i],13,i==tab?Gold:Muted,0,2,widths[i],34,TextAlignmentOptions.Center,"Inter-Bold",1.04f);Image hit=root.GetComponent<Image>()??root.gameObject.AddComponent<Image>();if(i!=tab)hit.color=Color.clear;Button(root,()=>SelectTab(selected));x+=widths[i]+4;}}

    // 03. DESTAQUES — NODE 285:2
    private void RenderFeatured()
    {
        FactionVisual faction=Factions[4];RectTransform hero=Box("FeaturedCollection",body,32,20,880,546,new Color(Navy.r,Navy.g,Navy.b,.90f),20,new Color(Gold.r,Gold.g,Gold.b,.38f));
        // 03.2. A superfície arredondada principal também funciona como recorte.
        // Os painéis funcionais internos permanecem intactos, mas não cobrem os
        // quatro cantos nem escapam da silhueta aprovada do card.
        Mask heroClip=hero.gameObject.AddComponent<Mask>();heroClip.showMaskGraphic=true;
        // 03.2. O cabeçalho interno é apenas uma faixa de preenchimento. Sua
        // antiga borda quadrada concorria com a moldura arredondada do card.
        RectTransform heroHeader=Box("CollectionHeader",hero,0,0,880,56,new Color(Gold.r,Gold.g,Gold.b,.12f),0,Color.clear);Text("Label",heroHeader,"★  COLEÇÃO DO MÊS",11,Gold,28,12,320,30,TextAlignmentOptions.Left,"Inter-Bold",1.54f);
        Art("HeroSkin",hero,faction.Root+"skin-territory-profile_1024x1024",0,56,440,466,true);Art("HeroAvatar",hero,faction.Root+faction.Folder,440,56,440,286,false);
        RectTransform identity=Box("CollectionIdentity",hero,440,342,440,204,new Color(.043f,.071f,.125f,.95f),0,Color.clear);
        Text("Kicker",identity,"SONS OF ODIN",11,Gold,24,12,380,20,TextAlignmentOptions.Left,"Inter-SemiBold",1.54f);Text("Name",identity,"COLEÇÃO DO MÊS",22,White,24,36,380,34,TextAlignmentOptions.Left,"Inter-Bold");
        Chip(identity,"SkinChip","SKIN TERRITORIAL",24,80,112,26,Gold);Chip(identity,"AvatarChip","AVATAR",142,80,74,26,Cyan);Chip(identity,"MoreChip","+4 ITENS",222,80,78,26,Muted);
        Action("Explore",identity,"VER COLEÇÃO",24,132,392,48,Gold,Gold,()=>ShowFeaturedCollection(faction.Id));
        Transform heroBorder=hero.Find("Border");if(heroBorder!=null)heroBorder.SetAsLastSibling();
        RectTransform vip=Box("VipFeature",body,932,20,356,546,new Color(Navy.r,Navy.g,Navy.b,.92f),20,new Color(Gold.r,Gold.g,Gold.b,.31f));
        Icon("Crown",vip,"UI/Store/Canonical/icon_crown",20,17,18,18,Gold);Text("Title",vip,"WAR VIP",16,Gold,46,10,160,34,TextAlignmentOptions.Left,"Inter-Bold",1.28f);
        Art("VipSkin",vip,faction.Root+"skin-territory-profile_1024x1024",0,52,178,160,true);Art("VipAvatar",vip,faction.Root+faction.Folder,178,52,178,160,false);
        Text("Benefits",vip,"BENEFÍCIOS MENSAIS\n✓ Cosméticos exclusivos\n✓ Avatares exclusivos\n✓ Molduras de perfil\n✓ Emblema permanente",13,White,20,230,316,166,TextAlignmentOptions.Left,"Inter-Regular");
        Action("VipCta",vip,"CONHECER O VIP",20,466,316,44,Gold,new Color(Gold.r,Gold.g,Gold.b,.10f),()=>SelectTab(3));
        // 03.1. A seção inferior sobe 32 px; os cards conservam dimensões e
        // ganham margem rolável abaixo dos botões.
        Text("Highlights",body,"DESTAQUES",16,White,32,572,300,28,TextAlignmentOptions.Left,"Inter-Bold",1.6f);
        ProductPreview(body,"Avatar",faction,"AVATAR",32,614,448,291,faction.Root+faction.Folder);ProductPreview(body,"Frame",faction,"MOLDURA",496,614,448,291,faction.Root+"profile-frame_1024x1024");
        ProductPreview(body,"Territory",faction,"SKIN TERRITORIAL",960,614,448,291,faction.Root+"skin-territory-profile_1024x1024");ProductPreview(body,"Counter",faction,"CONTADOR",1424,614,448,291,faction.Root+"territory-counter_512x512");
    }
    private void ProductPreview(Transform parent,string name,FactionVisual faction,string label,float x,float y,float w,float h,string path)
    {RectTransform card=Box(name,parent,x,y,w,h,new Color(Navy.r,Navy.g,Navy.b,.94f),16,new Color(Gold.r,Gold.g,Gold.b,.28f));ProductArtwork(card,label,path,w,180);RectTransform info=Box("Info",card,0,180,w,111,new Color(.043f,.071f,.125f,.97f),0,Color.clear);Text("Name",info,label+" "+faction.Name,14,White,16,12,w-32,22,TextAlignmentOptions.Left,"Inter-Bold");Text("Category",info,label,11,Muted,16,36,w-32,18,TextAlignmentOptions.Left,"Inter-Regular");Action("View",info,"VER NA COLEÇÃO",16,61,w-32,40,Cyan,new Color(Cyan.r,Cyan.g,Cyan.b,.14f),()=>ShowCollection(faction.Id));}

    // 04. COLEÇÕES E PAGINAÇÃO — NODE 285:4
    private void RenderCollections()
    {
        SectionHeading("COLEÇÕES","");int pages=Mathf.CeilToInt(Factions.Length/6f);Pager(pages,18);int start=page*6;for(int i=0;i<6&&start+i<Factions.Length;i++){FactionVisual f=Factions[start+i];int col=i%3,row=i/3;CollectionCard(f,32+col*608,76+row*426);}
    }
    private void CollectionCard(FactionVisual faction,float x,float y)
    {RectTransform card=Box("Collection_"+faction.Id,body,x,y,592,410,new Color(Navy.r,Navy.g,Navy.b,.94f),18,new Color(Gold.r,Gold.g,Gold.b,.28f));RectTransform art=Box("ArtArea",card,0,0,592,300,new Color(.024f,.055f,.09f,.97f),0,Color.clear);Art("SkinPreview",art,faction.Root+"skin-territory-profile_1024x1024",8,12,380,196,true);Art("AvatarPreview",art,faction.Root+faction.Folder,396,12,188,196,false);RectTransform info=Box("Info",card,0,300,592,110,new Color(.043f,.071f,.125f,.97f),0,Color.clear);Text("Name",info,faction.Name,16,White,16,10,320,24,TextAlignmentOptions.Left,"Inter-Bold");Chip(info,"SkinChip","SKIN TERRITORIAL",16,40,116,24,Gold);Chip(info,"AvatarChip","AVATAR",138,40,70,24,Cyan);Action("View",info,"VER COLEÇÃO",424,55,152,40,Gold,Gold,()=>ShowCollection(faction.Id));}

    // 05. DETALHE — NODE 285:6, CARDS 296x243
    private void RenderDetail(FactionVisual faction)
    {
        RectTransform skin=Box("CollectionHero",body,0,0,780,620,new Color(Navy.r,Navy.g,Navy.b,.92f),0,Color.clear);Art("Art",skin,faction.Root+"skin-territory-profile_1024x1024",0,0,780,620,true);
        RectTransform portrait=Box("AvatarPreview",skin,560,400,200,200,NavyDeep,14,Cyan);Art("Art",portrait,faction.Root+faction.Folder,0,0,200,200,false);
        RectTransform avatar=Box("CollectionInfo",body,780,0,1140,620,new Color(Navy.r,Navy.g,Navy.b,.94f),0,new Color(Gold.r,Gold.g,Gold.b,.19f));
        Text("BackToCollections",avatar,"DESTAQUES  ›  COLEÇÕES",11,Muted,28,24,360,22,TextAlignmentOptions.Left,"Inter-Regular");Text("Name",avatar,faction.Name,34,White,28,76,720,48,TextAlignmentOptions.Left,"Inter-Bold",1.2f);
        Text("Description",avatar,"IDENTIDADE COMPLETA DA FACÇÃO\nSKIN TERRITORIAL · AVATAR · MOLDURA · CONTADOR · EMBLEMA · EFEITO",14,Muted,28,138,940,64,TextAlignmentOptions.Left,"Inter-Regular");
        string[] summary={"SKIN TERRITORIAL","AVATAR","MOLDURA","CONTADOR","EMBLEMA","EFEITO"};for(int i=0;i<summary.Length;i++){int col=i%3,row=i/3;Chip(avatar,"Summary_"+i,summary[i],28+col*248,230+row*58,240,48,i==0?Gold:i==1?Cyan:Muted);}
        WDStoreCollection collection=service.Catalog.FindCollection(faction.Id);string bundleLabel=collection==null?"COLEÇÃO · EM BREVE":$"OBTER COLEÇÃO · {collection.price} {Currency(collection.currency,collection.price)}";
        Action("CollectionAcquire",avatar,bundleLabel,28,526,1084,56,Gold,Gold,()=>AcquireCollection(faction,collection));
        string[] labels={"AVATAR","MOLDURA","SKIN TERRITORIAL","CONTADOR","EMBLEMA","EFEITO"};string[] paths={faction.Folder,"profile-frame_1024x1024","skin-territory-profile_1024x1024","territory-counter_512x512","epic-emblem_512x512","cosmetic-effect-core_1024x1024"};
        Text("ItemsTitle",body,"ITENS DA COLEÇÃO",13,White,32,632,400,22,TextAlignmentOptions.Left,"Inter-Bold",1.56f);for(int i=0;i<6;i++)DetailProduct(faction,labels[i],faction.Root+paths[i],32+i*312,665,i);
    }
    private void DetailProduct(FactionVisual faction,string label,string path,float x,float y,int index)
    {RectTransform card=Box("Detail_"+label,body,x,y,296,243,new Color(Navy.r,Navy.g,Navy.b,.95f),14,new Color(Gold.r,Gold.g,Gold.b,.28f));ProductArtwork(card,label,path,296,150);RectTransform info=Box("Info",card,0,150,296,93,new Color(.043f,.071f,.125f,.98f),0,Color.clear);Text("Label",info,label,12,White,12,7,272,22,TextAlignmentOptions.Center,"Inter-Bold",.6f);Action("Acquire",info,AcquisitionLabel(faction,index),12,49,272,34,Gold,new Color(NavyDeep.r,NavyDeep.g,NavyDeep.b,.96f),()=>Acquire(faction,index));}
    private string AcquisitionLabel(FactionVisual f,int index){WDStoreProduct p=MappedProduct(f,index);if(p==null)return "EM BREVE";if(service.Owns(p))return "POSSUÍDO";return service.EffectivePrice(p)+" "+Currency(p.currency,service.EffectivePrice(p));}
    private WDStoreProduct MappedProduct(FactionVisual f,int index){if(f.Id!="sons_of_odin")return null;string id=index switch{0=>"avatar_viking",1=>"odin_frame",2=>"odin_skin",5=>"odin_effect",_=>null};return id==null?null:service.Catalog.FindProduct(id);}
    private void Acquire(FactionVisual f,int index){WDStoreProduct p=MappedProduct(f,index);feedback=p==null?"INTEGRAÇÃO COMERCIAL FUTURA":Message(service.BuyProduct(p.id));Render();}
    private void AcquireCollection(FactionVisual faction,WDStoreCollection collection){feedback=collection==null?"INTEGRAÇÃO COMERCIAL FUTURA":Message(service.BuyCollection(collection.id));Render();}

    // 06. AVATARES — NODE 311:2; ARTE SEM DEFORMAÇÃO
    private void RenderAvatars()
    {
        SectionHeading("AVATARES","");int pages=Mathf.CeilToInt(Factions.Length/6f);Pager(pages,18);int start=page*6;for(int i=0;i<6&&start+i<Factions.Length;i++){FactionVisual f=Factions[start+i];int col=i%3,row=i/3;AvatarCard(f,32+col*608,76+row*426);}
    }
    private void AvatarCard(FactionVisual faction,float x,float y)
    {RectTransform card=Box("Avatar_"+faction.Id,body,x,y,592,410,new Color(Navy.r,Navy.g,Navy.b,.94f),18,new Color(Gold.r,Gold.g,Gold.b,.28f));RectTransform art=Box("ArtArea",card,0,0,592,300,new Color(.024f,.055f,.09f,.97f),0,Color.clear);RectTransform portraitFrame=Box("PortraitFrame",art,166,20,260,260,NavyDeep,18,new Color(Cyan.r,Cyan.g,Cyan.b,.55f));Art("Portrait",portraitFrame,faction.Root+faction.Folder,12,12,236,236,false);RectTransform info=Box("Info",card,0,300,592,110,new Color(.043f,.071f,.125f,.97f),0,Color.clear);Text("Name",info,"AVATAR "+faction.Name,16,White,16,10,390,24,TextAlignmentOptions.Left,"Inter-Bold");Chip(info,"Kind","AVATAR",16,40,70,24,Cyan);Action("View",info,"VER AVATAR",424,55,152,40,Gold,Gold,()=>ShowCollection(faction.Id));}

    // 07. VIP — NODE 285:8; CTA VISUAL SEM ASSINATURA/PAGAMENTO FICTÍCIO
    private void RenderVip()
    {
        FactionVisual monthly=Factions[1];Text("Kicker",body,"WAR VIP",16,Cyan,32,24,360,24,TextAlignmentOptions.Left,"Inter-Bold",1.8f);Text("Title",body,"DOMINE COM BENEFÍCIOS EXCLUSIVOS",28,White,32,54,850,42,TextAlignmentOptions.Left,"Gabarito-ExtraBold",1f);
        RectTransform cosmetic=Box("MonthlyCosmetic",body,32,112,520,600,new Color(Navy.r,Navy.g,Navy.b,.93f),18,new Color(Gold.r,Gold.g,Gold.b,.38f));Art("Art",cosmetic,monthly.Root+monthly.Folder,0,0,520,474,false);Text("Label",cosmetic,"COSMÉTICO MENSAL",12,Gold,24,492,300,22,TextAlignmentOptions.Left,"Inter-Bold",1.2f);Text("Name",cosmetic,monthly.Name,22,White,24,522,470,38,TextAlignmentOptions.Left,"Gabarito-Bold");
        RectTransform offer=Box("VipOffer",body,564,112,628,600,new Color(Navy.r,Navy.g,Navy.b,.94f),20,new Color(Gold.r,Gold.g,Gold.b,.54f));Icon("Crown",offer,"UI/Store/Canonical/icon_crown",48,72,104,104,Gold);Text("Name",offer,"WAR VIP",38,White,184,66,380,54,TextAlignmentOptions.Left,"Gabarito-ExtraBold",1.5f);Text("State",offer,service.Vip.IsVipActive?"ATIVO · "+service.Vip.Remaining.Days+" DIAS RESTANTES":"ASSINATURA NÃO ATIVA",15,service.Vip.IsVipActive?Green:Muted,184,126,380,28,TextAlignmentOptions.Left,"Inter-Bold");Text("Copy",offer,"Mais liberdade para personalizar, progredir e comandar.",16,Muted,48,210,532,50,TextAlignmentOptions.Left,"Inter-Regular");Action("Subscribe",offer,"ASSINAR WAR VIP",48,510,532,56,Gold,Gold,()=>{feedback="ASSINATURA DEPENDE DA INTEGRAÇÃO COMERCIAL";Render();});
        string[] benefits={"3 DECKS ADICIONAIS","VOTO DE COMANDANTE 2×","RECOMPENSA DIÁRIA 2×","+1 ESMERALDA POR DIA","DESCONTO EM COSMÉTICOS","AVATARES VIP","MOLDURAS VIP","BADGE VIP","PERSONALIZAÇÕES","EMOJIS EXCLUSIVOS","COSMÉTICO MENSAL"};
        for(int i=0;i<benefits.Length;i++){int col=i%2,row=i/2;float x=1212+col*338,y=112+row*96;RectTransform item=Box("Benefit_"+i,body,x,y,324,82,new Color(Navy.r,Navy.g,Navy.b,.91f),8,new Color(Gold.r,Gold.g,Gold.b,.25f));Icon("Check",item,"UI/Store/Canonical/icon_check",18,25,30,30,Cyan);Text("Label",item,benefits[i],13,White,62,17,244,46,TextAlignmentOptions.Left,"Inter-Bold");}
    }

    // 08. PRIMITIVAS COMPARTILHADAS
    private void SectionHeading(string title,string subtitle){Text("Heading",body,title,30,White,32,24,620,44,TextAlignmentOptions.Left,"Gabarito-ExtraBold",1f);Text("Subtitle",body,subtitle,13,Muted,32,64,900,22,TextAlignmentOptions.Left,"Inter-Bold",1.2f);}
    private void Pager(int pages,float y){if(pages<=1)return;Action("Prev",body,"‹",810,y,54,42,Gold,new Color(Navy.r,Navy.g,Navy.b,.94f),()=>{page=(page-1+pages)%pages;Render();});Text("Page",body,$"{page+1} / {pages}",14,White,872,y,176,42,TextAlignmentOptions.Center,"Inter-Bold");Action("Next",body,"›",1056,y,54,42,Gold,new Color(Navy.r,Navy.g,Navy.b,.94f),()=>{page=(page+1)%pages;Render();});}
    private void ProductArtwork(Transform parent,string category,string path,float width,float height)
    {
        RectTransform area=Box("ArtworkArea",parent,0,0,width,height,new Color(.024f,.055f,.09f,.98f),0,Color.clear);
        bool skin=category.Contains("SKIN");float size=category.Contains("AVATAR")?Mathf.Min(height-16,160):category.Contains("MOLDURA")?Mathf.Min(height-8,172):category.Contains("CONTADOR")?Mathf.Min(height-20,160):category.Contains("EMBLEMA")?Mathf.Min(height-22,154):category.Contains("EFEITO")?Mathf.Min(height-16,160):Mathf.Min(height-16,160);
        if(skin)Art("Artwork",area,path,8,8,width-16,height-16,true);else Art("Artwork",area,path,(width-size)*.5f,(height-size)*.5f,size,size,false);
    }
    private void Chip(Transform parent,string name,string label,float x,float y,float w,float h,Color accent){RectTransform chip=Box(name,parent,x,y,w,h,new Color(accent.r,accent.g,accent.b,.08f),5,new Color(accent.r,accent.g,accent.b,.28f));Text("Label",chip,label,10,accent,5,1,w-10,h-2,TextAlignmentOptions.Center,"Inter-SemiBold",.35f);}
    private void Art(string name,Transform parent,string path,float x,float y,float w,float h,bool cover)
    {
        RectTransform viewport=R(name,parent);P(viewport,x,y,w,h);viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform visual=R("Visual",viewport);visual.anchorMin=visual.anchorMax=visual.pivot=new Vector2(.5f,.5f);visual.anchoredPosition=Vector2.zero;visual.sizeDelta=new Vector2(w,h);
        RawImage image=visual.gameObject.AddComponent<RawImage>();Texture2D texture=Resources.Load<Texture2D>(path);image.texture=texture;image.color=texture==null?new Color(.08f,.11f,.15f,1):Color.white;image.raycastTarget=false;
        AspectRatioFitter fit=visual.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=cover?AspectRatioFitter.AspectMode.EnvelopeParent:AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=texture!=null?(float)texture.width/texture.height:1f;
    }
    private Image Icon(string name,Transform parent,string path,float x,float y,float w,float h,Color tint){RectTransform root=R(name,parent);P(root,x,y,w,h);Image image=root.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>(path);image.color=tint;image.preserveAspect=true;image.raycastTarget=false;return image;}
    private RawImage TextureIcon(string name,Transform parent,string path,float x,float y,float w,float h,Color tint){RectTransform root=R(name,parent);P(root,x,y,w,h);RawImage image=root.gameObject.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>(path);image.color=tint;image.raycastTarget=false;return image;}
    private void Action(string name,Transform parent,string label,float x,float y,float w,float h,Color accent,Color fill,UnityAction action){RectTransform root=Box(name,parent,x,y,w,h,fill,5,accent);Color labelColor=fill==Gold?Navy:White;Text("Label",root,label,13,labelColor,8,4,w-16,h-8,TextAlignmentOptions.Center,"Gabarito-Bold",.5f);Button(root,action);}
    private TextMeshProUGUI Text(string name,Transform parent,string value,float size,Color color,float x,float y,float w,float h,TextAlignmentOptions alignment,string font,float spacing=0){RectTransform root=R(name,parent);P(root,x,y,w,h);TextMeshProUGUI label=root.gameObject.AddComponent<TextMeshProUGUI>();label.text=value;label.fontSize=size;label.color=color;label.alignment=alignment;label.font=WDHomeTypography.Load(font);label.characterSpacing=spacing;label.raycastTarget=false;label.textWrappingMode=value.Contains("\n")?TextWrappingModes.Normal:TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;return label;}
    private static RectTransform Box(string name,Transform parent,float x,float y,float w,float h,Color color,int radius,Color border){RectTransform root=R(name,parent);P(root,x,y,w,h);Image image=root.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=true;if(radius>0)WDHomeUIFactory.ApplyRounded(image,radius);if(border.a>0)WDHomeUIFactory.AddThinRoundedBorder(root,border,Mathf.Max(4,radius));return root;}
    private static Image Surface(string name,Transform parent,float x,float y,float w,float h,Color color){RectTransform root=R(name,parent);P(root,x,y,w,h);Image image=root.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
    private static void Button(RectTransform root,UnityAction action){Button button=root.gameObject.GetComponent<Button>()??root.gameObject.AddComponent<Button>();button.targetGraphic=root.GetComponent<Image>();button.onClick.AddListener(action);}
    private static string Currency(WDStoreCurrency currency,int amount)=>currency==WDStoreCurrency.Emeralds?(amount==1?"ESMERALDA":"ESMERALDAS"):"COINS";
    private static string Message(WDStorePurchaseStatus status)=>status switch{WDStorePurchaseStatus.Success=>"AQUISIÇÃO CONCLUÍDA",WDStorePurchaseStatus.AlreadyOwned=>"ITEM JÁ POSSUÍDO",WDStorePurchaseStatus.InsufficientFunds=>"SALDO INSUFICIENTE",WDStorePurchaseStatus.VipRequired=>"VIP ATIVO NECESSÁRIO",WDStorePurchaseStatus.Unavailable=>"ITEM INDISPONÍVEL",_=>"AQUISIÇÃO INDISPONÍVEL"};
    private static RectTransform R(string name,Transform parent){GameObject go=new(name,typeof(RectTransform));RectTransform root=(RectTransform)go.transform;root.SetParent(parent,false);return root;}
    private static void StretchHorizontal(RectTransform r,float left,float right){r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(left,r.offsetMin.y);r.offsetMax=new Vector2(-right,r.offsetMax.y);}private static void StretchBody(RectTransform r,float left,float right,float top,float bottom){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}private static void CenterTop(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}private static void RightTop(RectTransform r,float right,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(1,1);r.pivot=new Vector2(1,1);r.anchoredPosition=new Vector2(-right,-y);r.sizeDelta=new Vector2(w,h);}private static void P(RectTransform root,float x,float y,float w,float h){root.anchorMin=root.anchorMax=root.pivot=new Vector2(0,1);root.anchoredPosition=new Vector2(x,-y);root.sizeDelta=new Vector2(w,h);}private static void Stretch(RectTransform root){root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;}
    private static void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--){GameObject child=parent.GetChild(i).gameObject;if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}}
    private static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out Color color);return color;}

}

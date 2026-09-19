using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeStorePanel : MonoBehaviour
{
    private WarDominionUITheme theme;
    private WDStoreService service;
    private WDNavigationStack navigation;
    private RectTransform content;
    private int tab;
    private int returnTab;
    private string collectionId;
    private string feedback;
    public bool IsOpen => gameObject.activeSelf;
    public bool CatalogReady => service != null;
    public int ActiveTab => tab;
    public string OpenCollectionId => collectionId;

    // 01. Uma entrada global: tabs e detalhe são estado local.
    public void Build(WarDominionUITheme newTheme, WDStoreService newService, WDNavigationStack newNavigation)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        service = newService ?? throw new ArgumentNullException(nameof(newService));
        navigation = newNavigation ?? throw new ArgumentNullException(nameof(newNavigation));
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.96f); blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("StoreContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 40f, 54f, 40f);
        gameObject.SetActive(false);
    }
    public void Open(int selectedTab = 0)
    {
        tab = Mathf.Clamp(selectedTab, 0, 3); returnTab = tab; collectionId = null; feedback = null;
        navigation.Push(new WDNavigationContext("store", _ => { transform.SetAsLastSibling(); Render(); }));
    }
    public void HideFromNavigation() => gameObject.SetActive(false);
    public bool TryHandleLocalBack()
    {
        if (collectionId == null) return false;
        collectionId = null; tab = returnTab; Render(); return true;
    }
    public void SelectTab(int value) { tab = Mathf.Clamp(value, 0, 3); collectionId = null; feedback = null; Render(); }
    public void ShowCollection(string id)
    {
        if (service.Catalog.FindCollection(id) == null) return;
        returnTab = tab; tab = 1; collectionId = id; feedback = null; Render();
    }

    // 02. UI provisória: cards derivam exclusivamente do catálogo.
    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "< VOLTAR", () => { if (!TryHandleLocalBack()) navigation.TryBack(); },
            new Vector2(.01f, .92f), new Vector2(.14f, .99f));
        AddText(content, "Title", "LOJA", 29, theme.TextoPrincipal, new Vector2(.38f,.92f), new Vector2(.62f,.99f), TextAlignmentOptions.Center);
        WDArmoryProfile wallet = service.Wallet;
        AddText(content, "Wallet", $"COINS {wallet.Coins:N0}    ESMERALDAS {wallet.Emeralds:N0}", 15,
            theme.Acento, new Vector2(.68f,.92f), new Vector2(.99f,.99f), TextAlignmentOptions.Right);
        string[] tabs = { "DESTAQUES", "COLEÇÕES", "AVATARES", "VIP" };
        for (int i=0; i<tabs.Length; i++)
        {
            int selected = i;
            AddButton(content, "Tab_" + tabs[i], tabs[i], () => SelectTab(selected),
                new Vector2(.16f + i*.17f,.83f), new Vector2(.32f+i*.17f,.90f));
        }
        if (!service.Catalog.IsValid(GetProfiles()))
        {
            AddText(content,"Invalid","CATÁLOGO INDISPONÍVEL · TENTAR NOVAMENTE",18,theme.TextoSecundario,
                new Vector2(.15f,.4f),new Vector2(.85f,.6f),TextAlignmentOptions.Center);
            return;
        }
        if (collectionId != null) RenderCollectionDetail();
        else if (tab == 0) RenderFeatured();
        else if (tab == 1) RenderCollections();
        else if (tab == 2) RenderProducts(service.Catalog.Products.Where(p => p.kind == WDStoreKind.Avatar).OrderBy(p => p.order).ToArray(), .74f);
        else RenderVip();
        if (!string.IsNullOrWhiteSpace(feedback))
            AddText(content,"Feedback",feedback,15,theme.Acento,new Vector2(.04f,.03f),new Vector2(.96f,.09f),TextAlignmentOptions.Center);
    }
    private WDProfileRepository GetProfiles() => service.Profiles;
    private void RenderFeatured()
    {
        WDStoreCollection featured = service.Catalog.Collections.Where(c => c.featured).OrderBy(c => c.order).FirstOrDefault();
        if (featured != null)
            AddButton(content,"FeaturedCollection",$"COLEÇÃO EM DESTAQUE: {featured.title}  >",() => ShowCollection(featured.id),
                new Vector2(.03f,.68f),new Vector2(.60f,.78f));
        AddButton(content,"FeaturedVip","WAR VIP  ·  VER BENEFÍCIOS",() => SelectTab(3),
            new Vector2(.62f,.68f),new Vector2(.97f,.78f));
        RenderProducts(service.Catalog.Products.Where(p => p.featured).OrderBy(p => p.order).ToArray(),.63f);
    }
    private void RenderCollections()
    {
        WDStoreCollection[] items = service.Catalog.Collections.OrderBy(c => c.order).ToArray();
        for (int i=0; i<items.Length && i<9; i++)
        {
            int column=i%3, row=i/3; WDStoreCollection item=items[i];
            float x=.03f+column*.32f, y=.74f-row*.22f;
            AddButton(content,"Collection_"+item.id,$"{item.title}\n{item.productIds.Count} ITENS\nVER COLEÇÃO >",
                () => ShowCollection(item.id),new Vector2(x,y-.19f),new Vector2(x+.30f,y));
        }
        if (items.Length==0) AddText(content,"Empty","NENHUMA COLEÇÃO DISPONÍVEL",18,theme.TextoSecundario,
            new Vector2(.2f,.35f),new Vector2(.8f,.6f),TextAlignmentOptions.Center);
    }
    private void RenderCollectionDetail()
    {
        WDStoreCollection collection=service.Catalog.FindCollection(collectionId);
        if (collection==null) { collectionId=null; RenderCollections(); return; }
        AddText(content,"CollectionTitle",collection.title+"\n"+collection.description,20,theme.TextoPrincipal,
            new Vector2(.04f,.68f),new Vector2(.95f,.79f));
        RenderProducts(collection.productIds.Select(service.Catalog.FindProduct).ToArray(),.64f);
        AddButton(content,"BuyCollection",$"OBTER CONJUNTO COMPLETO · {collection.price} {collection.currency}",
            () => { feedback=Message(service.BuyCollection(collection.id)); Render(); },
            new Vector2(.28f,.10f),new Vector2(.72f,.17f));
    }
    private void RenderProducts(WDStoreProduct[] products,float top)
    {
        for (int i=0; i<products.Length && i<9; i++)
        {
            WDStoreProduct p=products[i]; int column=i%3,row=i/3;
            float x=.03f+column*.32f,y=top-row*.19f;
            string state=service.Owns(p) ? "POSSUÍDO" : !p.available ? "INDISPONÍVEL" :
                p.vipPolicy==WDStoreVipPolicy.RequiredToAcquire && !service.Vip.IsVipActive ? "VIP NECESSÁRIO" :
                $"{service.EffectivePrice(p)} {p.currency}";
            AddButton(content,"Product_"+p.id,$"{p.title}\n{p.description}\n{state}",
                () => { feedback=Message(service.BuyProduct(p.id)); Render(); },
                new Vector2(x,y-.17f),new Vector2(x+.30f,y));
        }
        if (products.Length==0) AddText(content,"Empty","NENHUM ITEM DISPONÍVEL",18,theme.TextoSecundario,
            new Vector2(.2f,.35f),new Vector2(.8f,.6f),TextAlignmentOptions.Center);
    }
    private void RenderVip()
    {
        string state=service.Vip.IsVipActive ? $"ATIVO · {service.Vip.Remaining.Days} DIAS RESTANTES" : "INATIVO";
        AddText(content,"VipState","WAR VIP · "+state,23,theme.Acento,new Vector2(.05f,.69f),new Vector2(.95f,.78f),TextAlignmentOptions.Center);
        string[] benefits={"DECKS 3, 4 E 5","VOTO DE COMANDANTE 2×","RECOMPENSA DIÁRIA 2×","+1 ESMERALDA POR DIA",
            "DESCONTO EM COSMÉTICOS","AVATARES VIP EXCLUSIVOS","MOLDURAS DE PERFIL VIP","BADGE VIP NO PERFIL",
            "PERSONALIZAÇÕES DE PERFIL","EMOJIS VIP EXCLUSIVOS","COSMÉTICO MENSAL SELECIONADO"};
        for(int i=0;i<benefits.Length;i++)
        {
            int col=i%3,row=i/3; float x=.04f+col*.32f,y=.65f-row*.105f;
            AddText(content,"Benefit_"+i,benefits[i],14,theme.TextoPrincipal,
                new Vector2(x,y-.09f),new Vector2(x+.30f,y),TextAlignmentOptions.Center);
        }
        AddButton(content,"SubscribeMock",$"ATIVAR VIP MOCK · {service.Catalog.MockVipDays} DIAS (SEM PAGAMENTO)",
            () => { service.Vip.ActivateMock(TimeSpan.FromDays(service.Catalog.MockVipDays));
                service.RefreshVipProjection(); feedback="VIP MOCK ATIVADO"; Render(); },
            new Vector2(.29f,.10f),new Vector2(.71f,.17f));
    }
    private static string Message(WDStorePurchaseStatus status) => status switch
    {
        WDStorePurchaseStatus.Success=>"AQUISIÇÃO CONCLUÍDA · ITEM NÃO EQUIPADO",
        WDStorePurchaseStatus.AlreadyOwned=>"ITEM JÁ POSSUÍDO · NENHUMA COBRANÇA",
        WDStorePurchaseStatus.InsufficientFunds=>"SALDO INSUFICIENTE",
        WDStorePurchaseStatus.VipRequired=>"VIP ATIVO NECESSÁRIO",
        WDStorePurchaseStatus.Unavailable=>"ITEM INDISPONÍVEL",
        WDStorePurchaseStatus.Unknown=>"ITEM NÃO ENCONTRADO",
        _=>"FALHA NA TRANSAÇÃO · VERIFIQUE O CATÁLOGO"
    };
    private TextMeshProUGUI AddText(Transform parent,string name,string value,float size,Color color,Vector2 min,Vector2 max,
        TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        TextMeshProUGUI text=WDHomeUIFactory.Text(name,parent,theme,value,size,color,alignment);
        text.rectTransform.anchorMin=min;text.rectTransform.anchorMax=max;
        text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero; return text;
    }
    private WDUIPremiumButton AddButton(Transform parent,string name,string label,UnityAction action,Vector2 min,Vector2 max)
    {
        RectTransform rect=WDHomeUIFactory.Rect(name,parent);rect.anchorMin=min;rect.anchorMax=max;
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        WDUIPremiumButton button=rect.gameObject.AddComponent<WDUIPremiumButton>();button.Build(theme,label,action);return button;
    }
    private static void Clear(Transform parent)
    {
        for(int i=parent.childCount-1;i>=0;i--)
        {
            GameObject child=parent.GetChild(i).gameObject;
            if(Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// AÇÕES / WDMatchActionsView = APRESENTAÇÃO OFICIAL APROVADA.
// Referências Figma: drawer 143:149, Sabotagem 155:262 e Infiltrado 155:327.
// Apenas apresentação e encaminhamento de intenção. Substituível na composição da
// WDMatchUIV2; regras e programação permanecem nos controllers/serviços canônicos.
public sealed class WDMatchActionsView : MonoBehaviour
{
    private WarDominionUITheme theme;
    private IMatchUICommands commands;
    private WDWeaponMatchUIController weapons;
    private MatchUIState state;
    private RectTransform drawer, list, popup, opponents, outside;
    private readonly Vector3[] canvasCorners = new Vector3[4];
    private static WDMatchActionsView lastDismissed;
    private static int dismissedFrame = -1;
    private TextMeshProUGUI count, heading, subtitle;
    private string listKey, popupKey;
    private bool suppressed;
    private readonly Dictionary<string, Sprite> sprites = new();
    private readonly List<Texture2D> ownedTextures = new();
    private static readonly Color Gold = WDMatchVisualStyle.BorderMatchGold;
    private static readonly Color Muted = new Color32(142, 146, 154, 255);
    public bool IsOpen => drawer != null && drawer.gameObject.activeSelf;
    public event Action IntentChanged;

    public void Build(WarDominionUITheme value)
    {
        theme = value;
        outside = Rect("ActionsOutside", transform, 0, 0, 0, 0);
        outside.anchorMin = Vector2.zero; outside.anchorMax = Vector2.one;
        outside.offsetMin = outside.offsetMax = Vector2.zero;
        outside.gameObject.AddComponent<Image>().color = Color.clear;
        Button dismissOutside = outside.gameObject.AddComponent<Button>();
        dismissOutside.transition = Selectable.Transition.None;
        // Button dispara no release/click: a camada permanece capturando todo o press.
        dismissOutside.onClick.AddListener(() =>
        {
            lastDismissed = this;
            dismissedFrame = Time.frameCount;
            SetDrawerOpen(false);
        });
        drawer = Surface("ActionsDrawer", transform, 295, 205, 396, 854,
            new Color(11f / 255, 14f / 255, 18f / 255, .95f), 12);
        // O frame possui cantos arredondados somente à direita. A textura compartilhada
        // permanece intacta; esta View possui apenas o Sprite recortado.
        Image drawerImage = drawer.GetComponent<Image>();
        Sprite rounded = drawerImage.sprite;
        Sprite rightRounded = Sprite.Create(rounded.texture,
            new Rect(rounded.rect.x + 13, rounded.rect.y, rounded.rect.width - 13, rounded.rect.height),
            new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(0, 13, 13, 13));
        rightRounded.hideFlags = HideFlags.DontSave;
        sprites.Add("DrawerSurface", rightRounded);
        drawerImage.sprite = rightRounded;
        Label(drawer, "Title", "AÇÕES PROGRAMADAS", 24, 24, 300, 34, 28, Gold);
        count = Label(drawer, "Count", "", 24, 62, 300, 17, 14, Muted);
        // Consome cliques internos; os X continuam sendo botões independentes.
        drawer.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        list = Scroll(drawer, "List", 24, 99, 348, 731);
        SetDrawerOpen(false);

        popup = Rect("WeaponTargetPopup", transform, 0, 0, 0, 0);
        popup.anchorMin = Vector2.zero;
        popup.anchorMax = Vector2.one;
        popup.offsetMin = popup.offsetMax = Vector2.zero;
        Image backdrop = popup.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0, 0, 0, .55f);
        Button dismiss = popup.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(() => { weapons?.CancelSelection(); IntentChanged?.Invoke(); Refresh(); });
        RectTransform panel = Surface("Panel", popup, 0, 0, 1400, 620,
            new Color(7f / 255, 17f / 255, 31f / 255, .95f), 20, Gold);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        // Um Selectable próprio consome o clique interno sem acionar o backdrop pai.
        Button blocker = panel.gameObject.AddComponent<Button>();
        blocker.transition = Selectable.Transition.None;
        heading = Label(panel, "Title", "", 40, 40, 1320, 39, 32, new Color32(245,247,250,255));
        subtitle = Label(panel, "Subtitle", "", 40, 87, 1320, 24, 16, Gold);
        opponents = Scroll(panel, "Opponents", 40, 135, 1320, 445);
        popup.gameObject.SetActive(false);
    }

    public void Bind(IMatchUICommands nextCommands, WDWeaponMatchUIController controller)
    {
        // Snapshots frequentes reaplicam os mesmos bindings. Preservar o objeto
        // pressionado entre pointer down/up; invalidar somente ao trocar autoridade.
        if (ReferenceEquals(commands, nextCommands) && ReferenceEquals(weapons, controller)) return;
        commands = nextCommands;
        weapons = controller;
        listKey = popupKey = null;
        Refresh();
    }

    public void Present(MatchUIState value) { state = value; Refresh(); }
    public void SetSuppressed(bool value)
    {
        suppressed = value;
        if (value) SetDrawerOpen(false);
        Refresh();
    }
    public void Toggle()
    {
        if (state == null || suppressed) return;
        SetDrawerOpen(!IsOpen);
        Refresh();
    }

    private void SetDrawerOpen(bool open)
    {
        drawer.gameObject.SetActive(open);
        outside.gameObject.SetActive(open);
        if (open) FitOutsideToCanvas();
    }

    private void FitOutsideToCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(canvasCorners);
        RectTransform parent = (RectTransform)transform;
        Vector3 min = parent.InverseTransformPoint(canvasCorners[0]);
        Vector3 max = parent.InverseTransformPoint(canvasCorners[2]);
        outside.offsetMin = (Vector2)min - parent.rect.min;
        outside.offsetMax = (Vector2)max - parent.rect.max;
    }

    // InputPartida usa raycast atual, sem depender do cache do InputModule, que pode
    // ainda refletir o frame anterior no primeiro toque ou na ordem dos Updates.
    public static bool ConsumesMapPointer(Vector2 screenPosition)
    {
        if (lastDismissed != null && dismissedFrame == Time.frameCount) return true;
        if (EventSystem.current == null) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = screenPosition }, hits);
        foreach (RaycastResult hit in hits)
        {
            WDMatchActionsView view = hit.gameObject.GetComponentInParent<WDMatchActionsView>();
            if (view != null && view.IsOpen) return true;
        }
        return false;
    }

    // Armas não fazem parte da chave de MatchUIState; consultar também quando apenas
    // o intent muda. Reconstruir objetos somente se o conteúdo consultado mudou.
    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (drawer == null || state == null) return;
        if (IsOpen) { FitOutsideToCanvas(); RefreshList(); }
        bool target = !suppressed && state.PodeEditarPreparacao &&
            weapons?.CanEditPreparation == true &&
            (weapons.Stage == WDWeaponHudSelectionStage.TargetPlayer ||
             weapons.Stage == WDWeaponHudSelectionStage.TargetWeapon) &&
            (weapons.SelectedWeaponId == WDStrategicWeaponIds.Sabotage ||
             weapons.SelectedWeaponId == WDSpecialWeaponIds.Infiltrator);
        popup.gameObject.SetActive(target);
        if (target) RefreshOpponents();
    }

    private void RefreshList()
    {
        var rows = new List<(string Id, string Title, string Detail, string Icon, bool Enabled, Action Cancel)>();
        bool editable = state.PodeEditarPreparacao && commands != null;
        foreach (MatchUIActionState action in state.Acoes)
        {
            MatchUIActionState captured = action;
            rows.Add(($"Action-{action.Posicao}", action.TipoEsperado.ToUpperInvariant(),
                $"{action.OrigemId} → {action.DestinoId} · {action.Quantidade} TROPAS",
                "Target", editable, () => commands?.RemoverAcao(captured.Posicao)));
        }
        foreach (MatchUIReinforcementState reinforcement in state.Distribuicoes)
        {
            int id = reinforcement.Id;
            rows.Add(($"Reinforcement-{id}", "REFORÇO",
                $"{reinforcement.TerritorioId} · +{reinforcement.Quantidade} TROPAS",
                "Shield", editable, () => commands?.RemoverDistribuicaoReforco(id)));
        }
        if (state.Transferencia != null)
            rows.Add(("Transfer", "TRANSFERÊNCIA", state.Transferencia.TerritorioId,
                "ArrowRight", editable, () => commands?.RemoverTransferencia()));
        foreach (WDWeaponAction action in weapons?.GetProgrammedActions() ?? Array.Empty<WDWeaponAction>())
        {
            string id = action.ActionId;
            string detail = string.IsNullOrEmpty(action.TargetPlayerId)
                ? action.TargetTerritoryId
                : weapons.GetOpponents().FirstOrDefault(p => p.PlayerId == action.TargetPlayerId)?.Nickname
                    ?? action.TargetPlayerId;
            if (!string.IsNullOrEmpty(action.OriginTerritoryId))
                detail = action.OriginTerritoryId + " → " + detail;
            string targetWeapon = action.Parameters.FirstOrDefault(p => p.Key == "targetWeaponId")?.Value;
            if (!string.IsNullOrEmpty(targetWeapon)) detail += " · " + weapons.WeaponDisplayName(targetWeapon);
            rows.Add(("Weapon-" + id, weapons.WeaponDisplayName(action.WeaponId).ToUpperInvariant(), detail,
                ActionIcon(action.WeaponId), state.PodeEditarPreparacao && weapons.CanCancelAction(id),
                () => weapons.CancelAction(id)));
        }


        count.text = $"ROUND {state.Round} · {rows.Count} AÇÕES";
        string key = string.Join("|", rows.Select(r => $"{r.Id}:{r.Title}:{r.Detail}:{r.Enabled}"));
        if (key == listKey) return;
        listKey = key;
        Clear(list);
        list.sizeDelta = new Vector2(348, Math.Max(731, rows.Count * 65));
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            RectTransform item = Rect(row.Id, list, 0, i * 65, 348, 64);
            Color semantic = row.Icon == "Target" ? new Color32(217,90,90,255) :
                row.Icon == "Shield" ? new Color32(101,169,107,255) :
                row.Icon == "Sparkles" ? new Color32(155,114,207,255) :
                row.Icon == "ArrowRight" ? Gold : new Color32(85,184,209,255);
            if (!string.IsNullOrEmpty(row.Icon))
            {
                RectTransform badge = Surface("IconBadge", item, 0, 16, 32, 32,
                    new Color(semantic.r, semantic.g, semantic.b, .08f), 8,
                    new Color(semantic.r, semantic.g, semantic.b, .4f));
                badge.GetComponent<Image>().raycastTarget = false;
                Icon(badge, row.Icon, 8, 8, 16);
            }
            Label(item, "Title", row.Title, 44, 12, 268, 22, 18, Color.white);
            Label(item, "Detail", row.Detail, 44, 36, 268, 16, 13, Muted).font =
                WDHomeTypography.Load("Gabarito-Medium");

            // ============================================================
            // 05. CANCELAMENTO DAS AÇÕES PROGRAMADAS
            // ============================================================
            // O X existe somente quando a ação pode realmente ser cancelada.
            // Ações irreversíveis, como o Radar após programação, não exibem X.
            if (row.Enabled)
            {
                CancelButton(item, () =>
                {
                    // Revalidar no comando: o estado pode ter congelado desde o último frame.
                    if (state.PodeEditarPreparacao)
                        row.Cancel();

                    IntentChanged?.Invoke();
                    listKey = null;
                    Refresh();
                });
            }


            if (i < rows.Count - 1)
                Surface("Divider", item, 0, 64, 348, 1,
                    new Color(Gold.r, Gold.g, Gold.b, .2f), 0).GetComponent<Image>().raycastTarget = false;
        }
    }

    private Button CancelButton(RectTransform row, Action cancel)
    {
        // Hit target 44x44 contido na linha/lista. O visual continua em (324,20),24x24.
        // Expansão para a esquerda evita clipping na borda direita do viewport.
        RectTransform hit = Rect("Cancel", row, 304, 10, 44, 44);
        hit.gameObject.AddComponent<Image>().color = Color.clear;
        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => cancel());
        Color red = new Color32(217, 90, 90, 255);
        RectTransform visual = Surface("Visual", hit, 20, 10, 24, 24,
            new Color32(11, 14, 18, 255), 6, red);
        visual.GetComponent<Image>().raycastTarget = false;
        Label(visual, "Label", "×", 0, 0, 24, 24, 16, red).alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void RefreshOpponents()
    {
        bool infiltrator = weapons.SelectedWeaponId == WDSpecialWeaponIds.Infiltrator;
        var players = weapons.GetOpponents();
        string key = weapons.SelectedWeaponId + weapons.Stage + weapons.Feedback +
            string.Join("|", players.Select(p => p.PlayerId + p.Nickname + p.MatchColor +
                string.Join(",", weapons.GetDeckCardLabels(p.PlayerId)) +
                string.Join(",", weapons.GetInfiltratorWeapons(p.PlayerId).Select(w => w.WeaponId))));
        if (key == popupKey) return;
        popupKey = key;
        heading.text = infiltrator ? "SELECIONE UMA ARMA PARA BLOQUEAR" : "SELECIONE O ADVERSÁRIO";
        subtitle.text = weapons.Feedback.StartsWith("Ação inválida:", StringComparison.Ordinal)
            ? weapons.Feedback : infiltrator ? "Escolha uma arma de um adversário" : "Escolha quem receberá a sabotagem";
        Clear(opponents);
        opponents.sizeDelta = new Vector2(1320, Math.Max(445, players.Count * 144));
        for (int i = 0; i < players.Count; i++)
        {
            WDMatchParticipant player = players[i];
            RectTransform row = Surface("Opponent-" + player.PlayerId, opponents, 0, i * 144,
                1320, 132, new Color(10f/255,19f/255,32f/255,.8f),16,
                new Color(Gold.r,Gold.g,Gold.b,.2f));
            if (!infiltrator)
            {
                Button choose = row.gameObject.AddComponent<Button>();
                choose.transition = Selectable.Transition.None;
                choose.onClick.AddListener(() => { weapons.SelectTargetPlayer(player.PlayerId); IntentChanged?.Invoke(); Refresh(); });
            }
            RectTransform avatar = Surface("Avatar", row,20,30,72,72,
                new Color(player.MatchColor.r,player.MatchColor.g,player.MatchColor.b,.18f),12,player.MatchColor);
            Label(avatar,"Slot",(player.SlotIndex+1).ToString(),0,0,72,72,24,Color.white).alignment = TextAlignmentOptions.Center;
            Label(row,"Name",player.Nickname.ToUpperInvariant(),112,39,280,18,12,Gold);
            string role = state.Jogadores.FirstOrDefault(p => (int)p.Jogador == player.SlotIndex + 1).Equipe.ToString().ToUpperInvariant();
            Label(row,"Role",role,112,63,280,30,24,Color.white);
            var targets = weapons.GetInfiltratorWeapons(player.PlayerId);
            IReadOnlyList<string> labels = infiltrator ? targets.Select(t => t.DisplayName).ToArray() : weapons.GetDeckCardLabels(player.PlayerId);
            RectTransform arsenal = Scroll(row, "Arsenal", 412, 20, 888, 92);
            float x = 0, y = 10;
            for (int j = 0; j < labels.Count; j++)
            {
                string label = labels[j].ToUpperInvariant();
                // Mesma tipografia usada no chip: wrap por largura real do texto.
                float width = Mathf.Min(868, Mathf.Ceil(heading.GetPreferredValues(label, 10000, 100).x * 12f / 32f) + 36);
                if (x + width > 888) { x = 0; y += 40; }
                if (infiltrator)
                {
                    string weaponId = targets[j].WeaponId;
                    Button chip = Click(arsenal,"Weapon-"+weaponId,"• " + label,x,y,width,32,12,() =>
                    {
                        // O frame canônico escolhe adversário+arma no mesmo clique.
                        // O controller continua percorrendo seus dois estágios existentes.
                        if (!weapons.CanEditPreparation) return;
                        if (weapons.Stage == WDWeaponHudSelectionStage.TargetWeapon)
                            weapons.SelectWeapon(WDSpecialWeaponIds.Infiltrator);
                        if (weapons.SelectTargetPlayer(player.PlayerId)) weapons.SelectInfiltratorWeapon(weaponId);
                        IntentChanged?.Invoke();
                        Refresh();
                    },16,new Color(Gold.r,Gold.g,Gold.b,.4f),Color.white);
                    chip.GetComponent<Image>().color = new Color(10f/255,19f/255,32f/255,.5f);
                }
                else
                {
                    RectTransform chip = Surface("Deck-"+j,arsenal,x,y,width,32,new Color(10f/255,19f/255,32f/255,.5f),16,
                        new Color(Gold.r,Gold.g,Gold.b,.12f));
                    Label(chip,"Label","• "+label,10,0,width-20,32,12,Color.white);
                }
                x += width + 8;
            }
            arsenal.sizeDelta = new Vector2(888, Math.Max(92, y + 42));
        }
    }

        // ============================================================
    // 06. ÍCONES DAS ARMAS NA LISTA DE AÇÕES
    // ============================================================
    // As sete instalações preservam seus ícones próprios. Ações sem pictograma
    // territorial usam o ícone tático genérico no mesmo slot visual da lista.
    private static string ActionIcon(string id) => id switch
    {
        WDSupportWeaponIds.Bunker => "Hub:Bunker",
        WDSupportWeaponIds.Minefield => "Hub:Minefield",
        WDSupportWeaponIds.FlakTower => "Hub:FlakTower",
        WDStrategicWeaponIds.EnergyPlant => "Hub:PowerPlant",
        WDStrategicWeaponIds.Artillery => "Hub:Artillery",
        WDStrategicWeaponIds.Rockets => "Hub:Rockets",
        WDSpecialWeaponIds.Drone => "Hub:Drone",
        _ => "Hub:TacticalAction"
    };

    private RectTransform Scroll(Transform parent,string name,float x,float y,float w,float h)
    {
        RectTransform viewport = Rect(name+"Viewport",parent,x,y,w,h);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        RectTransform content = Rect(name,viewport,0,0,w,h);
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        RectTransform rect = WDUIFactory.Rect(name,parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h);
        return rect;
    }
    private RectTransform Surface(string name,Transform parent,float x,float y,float w,float h,Color color,int radius,Color? border=null)
    {
        RectTransform rect=Rect(name,parent,x,y,w,h);
        Image image=rect.gameObject.AddComponent<Image>(); image.color=color;
        if(radius>0) WDHomeUIFactory.ApplyRounded(image,radius);
        if(border.HasValue)
        {
            Image outline = WDHomeUIFactory.AddRoundedBorder(rect,border.Value,radius);
            if (drawer != null && parent.IsChildOf(drawer))
                outline.sprite = DrawerBorder(radius);
        }
        return rect;
    }
    // Contorno de 1 px exclusivo do drawer; não altera o padrão dos outros módulos.
    private Sprite DrawerBorder(int radius)
    {
        string key = "DrawerBorder-" + radius;
        if (sprites.TryGetValue(key, out Sprite existing)) return existing;
        int size = radius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = key, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = size * .5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(0, Mathf.Abs(x + .5f - half) - (half - radius));
            float dy = Mathf.Max(0, Mathf.Abs(y + .5f - half) - (half - radius));
            float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            float outer = Mathf.Clamp01(.5f - distance);
            float inner = Mathf.Clamp01(-.5f - distance);
            pixels[y * size + x] = new Color32(255, 255, 255,
                (byte)Mathf.RoundToInt((outer - inner) * 255));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        ownedTextures.Add(texture);
        float edge = radius + 1;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
        sprite.hideFlags = HideFlags.DontSave;
        sprites.Add(key, sprite);
        return sprite;
    }
    private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color)
    {
        TextMeshProUGUI label=WDUIFactory.Text(name,parent,theme,size,color,TextAlignmentOptions.MidlineLeft);
        if (popup != null && parent.IsChildOf(popup) && theme.FonteInterface != null)
            label.font = theme.FonteInterface;
        label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(0,1);
        label.rectTransform.anchoredPosition=new Vector2(x,-y); label.rectTransform.sizeDelta=new Vector2(w,h);
        label.richText=false; label.enableAutoSizing=false; label.textWrappingMode=TextWrappingModes.NoWrap;
        label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false; label.text=text;
        return label;
    }
    private Button Click(Transform parent,string name,string text,float x,float y,float w,float h,float size,Action action,
        int radius = 6, Color? border = null, Color? labelColor = null)
    {
        RectTransform rect=Surface(name,parent,x,y,w,h,new Color32(11,14,18,255),radius,border ?? Gold);
        Button button=rect.gameObject.AddComponent<Button>(); button.transition=Selectable.Transition.None;
        button.onClick.AddListener(()=>action());
        Label(rect,"Label",text,0,0,w,h,size,labelColor ?? Muted).alignment=TextAlignmentOptions.Center;
        return button;
    }
        // ============================================================
    // 07. CARREGAMENTO DE ÍCONES DE AÇÕES E INSTALAÇÕES
    // ============================================================
    // "Hub:" identifica os ícones coloridos oficiais das instalações.
    // Os demais continuam usando a pasta tradicional de AÇÕES.
    private void Icon(Transform parent, string name, float x, float y, float size)
    {
        bool installationHubIcon = name.StartsWith("Hub:", StringComparison.Ordinal);
        string assetName = installationHubIcon ? name.Substring(4) : name;


// 08. ORIGEM DOS ÍCONES DA LISTA DE AÇÕES
// ============================================================
// Instalações usam exclusivamente seus ícones isolados.
// Não reutilizar as composições visuais dos Hubs.
string resourcePath = installationHubIcon
    ? "UI/Match/FigmaCurrent/InstallationActionIcons/" + assetName
    : "UI/Match/Actions/" + assetName;

        string cacheKey = resourcePath;

        if (!sprites.TryGetValue(cacheKey, out Sprite sprite))
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return;

            Rect sourceRect = assetName == "TacticalAction"
                ? CenterSquare(texture.width, texture.height)
                : new Rect(0, 0, texture.width, texture.height);
            sprite = Sprite.Create(
                texture,
                sourceRect,
                new Vector2(.5f, .5f),
                100);

            sprite.name = "ActionIcon-" + assetName;
            sprite.hideFlags = HideFlags.DontSave;
            sprites.Add(cacheKey, sprite);
        }

        Image image = Rect("Icon", parent, x, y, size, size)
            .gameObject.AddComponent<Image>();

        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    // O asset genérico possui canvas fonte horizontal. O recorte central remove
    // somente essa folga e mantém o mesmo container 16x16 dos demais pictogramas.
    private static Rect CenterSquare(float width, float height)
    {
        float side = Mathf.Min(width, height);
        return new Rect((width - side) * .5f, (height - side) * .5f, side, side);
    }
    private static void Clear(Transform parent)
    {
        for(int i=parent.childCount-1;i>=0;i--)
        {
            GameObject child=parent.GetChild(i).gameObject;
            child.SetActive(false);
            if(Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
    private void OnDestroy()
    {
        if (lastDismissed == this) { lastDismissed = null; dismissedFrame = -1; }
        foreach(Sprite sprite in sprites.Values)
            if(Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
        foreach(Texture2D texture in ownedTextures)
            if(Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
    }
}

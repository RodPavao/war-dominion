using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 01. Apresentação substituível. Transferências continuam em GameManager/FilaTransferencias;
// sinais táticos continuam no controller informativo; reações são intenções locais.
public sealed class WDMatchInteractionView : MonoBehaviour
{
    private const string AssetRoot = "UI/Match/FigmaCurrent/Hubs/";
    private static readonly Color Gold = WDMatchVisualStyle.BorderMatchGold;
    private static readonly Color Cyan = new Color32(30, 191, 219, 255);
    private static readonly List<WDMatchInteractionView> Live = new();
    private static WDMatchInteractionView consumedBy;
    private static int consumedFrame = -1;
    private readonly List<RaycastResult> raycasts = new();
    private readonly List<Sprite> ownedSprites = new();
    private readonly Dictionary<string, TMP_FontAsset> fonts = new();
    private readonly List<Transform> entries = new();
    private readonly Dictionary<TerritorioClique.Dono, RectTransform> playerEntries = new();
    private TerritorioClique.Dono reactionAuthor;
    private TerritorioVisual confirmationVisual;
    private Image hideSlash;
    private TextMeshProUGUI hideLabel;
    private WarDominionUITheme theme;
    private IMatchUICommands commands;
    private WDMatchInformationalOverlayController informational;
    private MatchUIState state;
    private RectTransform outside, emoji, tactical, modal, anchor;
    private TextMeshProUGUI emojiName, tacticalName, title, marker, reaction;
    private Image kissReaction;
    private readonly WDUIPremiumButton[] allies = new WDUIPremiumButton[2];
    private WDUIPremiumButton confirm, cancel;
    private TerritorioClique markerTerritory;
    private string markerId;
    private TerritorioClique.Dono player;
    private float reactionUntil;
    private bool suppressed;
    public WDMatchReactions Reactions { get; } = new();
    public event Action Opening;
    public event Action TacticalOpening;
    public bool IsOpen => (emoji != null && emoji.gameObject.activeSelf) ||
        (tactical != null && tactical.gameObject.activeSelf) ||
        (modal != null && modal.gameObject.activeSelf);
    public bool IsAllySelection => modal != null && modal.gameObject.activeSelf && allies[0].gameObject.activeSelf;
    public string ProgrammedTerritoryId => state?.Transferencia?.TerritorioId ?? string.Empty;

    public void Bind(IMatchUICommands value, WDMatchInformationalOverlayController controller)
    {
        commands = value; informational = controller;
    }
    public void RegisterEntry(Transform entry) { if (entry != null) entries.Add(entry); }
    public void RegisterPlayerEntry(TerritorioClique.Dono owner, RectTransform entry)
    { RegisterEntry(entry); playerEntries[owner] = entry; }
    public void ClearEntries() { entries.Clear(); playerEntries.Clear(); }
    private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
    private void OnDisable()
    {
        Live.Remove(this);
        Close();
        informational?.CancelTacticalSelection();
        if (state?.EmModoTransferencia == true)
        { commands?.SelecionarModoAcao(); commands?.CancelarSelecaoTransferencia(); }
        if (marker != null) marker.gameObject.SetActive(false);
        if (reaction != null) reaction.gameObject.SetActive(false);
        if (kissReaction != null) kissReaction.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        SetConfirmationVisual(null);
        if (consumedBy == this) consumedBy = null;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ProtectFonts);
        foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
            if (fonts.ContainsValue(label.font)) label.font = null;
        foreach (TMP_FontAsset font in fonts.Values)
            if (font != null) { if (Application.isPlaying) Destroy(font); else DestroyImmediate(font); }
        foreach (Sprite sprite in ownedSprites)
            if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
    }

    // 02. Captura até pointer-up: o backdrop fecha somente no click, nunca no down.
    // O latch cobre também a ordem inversa InputModule -> InputPartida no mesmo frame.
    public void Consume() { consumedBy = this; consumedFrame = Time.frameCount; }
    public static bool ConsumesMapPointer(Vector2 position) => ConsumesMapPointerAtFrame(position, Time.frameCount);
    private static bool ConsumesMapPointerAtFrame(Vector2 position, int frame)
    {
        if (consumedBy != null && consumedFrame == frame) return true;
        foreach (WDMatchInteractionView view in Live)
        {
            if (view == null) continue;
            if (view.IsOpen) return true;
            if (EventSystem.current == null) continue;
            view.raycasts.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, view.raycasts);
            foreach (RaycastResult hit in view.raycasts)
                foreach (Transform entry in view.entries)
                    if (entry != null && hit.gameObject.transform.IsChildOf(entry)) return true;
        }
        return false;
    }
    public void Close()
    {
        SetConfirmationVisual(null);
        if (IsOpen) Consume();
        if (emoji != null) emoji.gameObject.SetActive(false);
        if (tactical != null) tactical.gameObject.SetActive(false);
        if (modal != null) modal.gameObject.SetActive(false);
        if (outside != null) outside.gameObject.SetActive(false);
        if (outside != null) outside.GetComponent<Image>().color = Color.clear;
        anchor = null;
    }
    public void Dismiss()
    {
        bool transfer = modal != null && modal.gameObject.activeSelf;
        Consume(); Close();
        informational?.CancelTacticalSelection();
        if (transfer) commands?.SelecionarModoAcao();
    }
    public void SetSuppressed(bool value)
    { suppressed = value; if (value) { Dismiss(); informational?.CancelTacticalSelection(); } }

    public void Build(WarDominionUITheme value)
    {
        theme = value;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ProtectFonts);
        outside = Rect("InteractionOutside", transform, 0, 0, 1, 1);
        Stretch(outside);
        outside.gameObject.AddComponent<Image>().color = Color.clear;
        Click(outside, Dismiss);
        emoji = BuildRadial(false);
        tactical = BuildRadial(true);
        BuildTransferModal();
        // 02.1. O marcador textual legado foi removido. A transferência
        // confirmada agora pertence ao overlay territorial compartilhado.
        marker = null;
        reaction = Label(transform, "LocalReaction", "", 0, 0, 80, 80, 42, Color.white);
        WDChatEmojiColor.Apply(reaction);
        reaction.gameObject.SetActive(false);
        kissReaction = Art(transform, "NotoKiss", 0, 0, 63, 63);
        kissReaction.gameObject.name = "LocalKissReaction";
        kissReaction.gameObject.SetActive(false);
        Close();
    }

    public bool OpenPlayer(TerritorioClique.Dono target, RectTransform entry)
    {
        bool own = WDMatchReactions.CanReact(state, target);
        bool allied = WDMatchInformationalOverlayController.CanBeginTacticalPing(state, target);
        if ((!own && !allied) || suppressed || entry == null) return false;
        if (allied) TacticalOpening?.Invoke(); else Opening?.Invoke();
        informational?.CancelTacticalSelection();
        Close(); Consume();
        player = target; anchor = entry;
        playerEntries[target] = entry;
        RefreshEmojiVisibility();
        (own ? emoji : tactical).gameObject.SetActive(true);
        outside.gameObject.SetActive(true);
        string name = state.Jogadores.FirstOrDefault(p => p.Jogador == target).Nome ?? target.ToString();
        (own ? emojiName : tacticalName).text = name.ToUpperInvariant();
        FitRadial();
        return true;
    }
    public bool SelectEmoji(string value)
    {
        if (emoji == null || !emoji.gameObject.activeSelf || !Reactions.Request(state, player, value)) return false;
        reactionAuthor = player;
        Close(); Consume();
        bool kiss = value == "😘";
        reaction.text = kiss ? string.Empty : value;
        RectTransform reactionRect = kiss ? kissReaction.rectTransform : reaction.rectTransform;
        PositionBesidePlayer(reactionRect, reactionAuthor);
        reactionUntil = Time.unscaledTime + 3f;
        reaction.gameObject.SetActive(!Reactions.Hidden && !kiss);
        kissReaction.gameObject.SetActive(!Reactions.Hidden && kiss);
        return true;
    }
    public bool SelectTactic(WDTacticalPingKind kind)
    {
        if (tactical == null || !tactical.gameObject.activeSelf ||
            informational?.BeginTacticalPing(player, kind) != true) return false;
        Close(); Consume(); return true;
    }
    public void Present(MatchUIState value)
    {
        bool phaseChanged = state != null && (value == null || state.Fase != value.Fase || state.JogadorLocal != value.JogadorLocal);
        bool abandonTransfer = phaseChanged && state.EmModoTransferencia;
        state = value;
        if (phaseChanged) { Close(); informational?.CancelTacticalSelection(); }
        if (abandonTransfer) commands?.CancelarSelecaoTransferencia();
        if (state == null) { Close(); return; }
        if ((emoji.gameObject.activeSelf && !WDMatchReactions.CanReact(state, player)) ||
            (tactical.gameObject.activeSelf && !WDMatchInformationalOverlayController.CanBeginTacticalPing(state, player))) Close();
        PresentTransfer();
        if (markerId != ProgrammedTerritoryId)
        {
            markerId = ProgrammedTerritoryId;
            markerTerritory = string.IsNullOrEmpty(markerId) ? null :
                MapaAtivo.ObterTerritoriosOuCena().FirstOrDefault(t => t != null && t.idTerritorio == markerId);
        }
        UpdateMarker();
    }

    // 03. O modal observa a seleção canônica; escolher aliado não registra uma ordem.
    private void PresentTransfer()
    {
        bool show = !suppressed && state.PodeEditarPreparacao && state.EmModoTransferencia &&
            !string.IsNullOrEmpty(state.TerritorioTransferenciaId) && state.Transferencia == null;
        if (!show) { if (modal.gameObject.activeSelf) Close(); return; }
        emoji.gameObject.SetActive(false); tactical.gameObject.SetActive(false);
        modal.gameObject.SetActive(true); outside.gameObject.SetActive(true);
        outside.GetComponent<Image>().color = new Color(0,0,0,.48f);
        bool choose = state.DestinatariosTransferencia.Count > 1 &&
            !state.DestinatariosTransferencia.Contains(state.DestinatarioTransferenciaSelecionado);
        title.text = choose ? "SELECIONE UM ALIADO" : "CONFIRMAR TRANSFERÊNCIA";
        modal.sizeDelta = new Vector2(560, choose ? 262 : 230);
        ((RectTransform)confirm.transform).anchoredPosition = new Vector2(292, -126);
        ((RectTransform)cancel.transform).anchoredPosition = new Vector2(44, -126);
        SetConfirmationVisual(choose ? null : MapaAtivo.ObterTerritoriosOuCena()
            .FirstOrDefault(t => t != null && t.idTerritorio == state.TerritorioTransferenciaId)?.GetComponent<TerritorioVisual>());
        for (int i = 0; i < allies.Length; i++)
        {
            allies[i].gameObject.SetActive(choose && i < state.DestinatariosTransferencia.Count);
            if (choose && i < state.DestinatariosTransferencia.Count)
            {
                var recipient = state.DestinatariosTransferencia[i];
                MatchUIPlayerState identity = state.Jogadores.FirstOrDefault(p => p.Jogador == recipient);
                allies[i].SetLabel(PlayerName(recipient).ToUpperInvariant()+"\n"+identity.Equipe.ToString().ToUpperInvariant());
                allies[i].transform.Find("AllyMarker").GetComponent<Image>().color = identity.Cor;
            }
        }
        confirm.gameObject.SetActive(!choose); cancel.gameObject.SetActive(!choose);
        confirm.SetInteractable(state.DestinatariosTransferencia.Contains(state.DestinatarioTransferenciaSelecionado));
        FitModal();
    }
    private string PlayerName(TerritorioClique.Dono owner) =>
        state.Jogadores.FirstOrDefault(p => p.Jogador == owner).Nome ?? owner.ToString();
    public void ChooseAlly(int index)
    {
        if (!IsAllySelection || index < 0 || index >= state.DestinatariosTransferencia.Count) return;
        Consume(); commands?.SelecionarDestinatarioTransferencia(state.DestinatariosTransferencia[index]);
    }
    public void ConfirmTransfer()
    {
        if (modal == null || !modal.gameObject.activeSelf || IsAllySelection ||
            !state.PodeEditarPreparacao || !state.EmModoTransferencia) return;
        Consume(); commands?.ConfirmarTransferencia();
    }
    private void BuildTransferModal()
    {
        modal = Rect("TransferConfirmation", transform, 0, 0, 560, 330);
        Image image = modal.gameObject.AddComponent<Image>(); image.color = new Color(9/255f, 12/255f, 17/255f, .96f);
        WDHomeUIFactory.ApplyRounded(image, 12); WDHomeUIFactory.AddRoundedBorder(modal, Gold, 12);
        Click(modal, Consume);
        title = Label(modal, "Title", "", 36, 30, 488, 42, 27, Gold);
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            allies[i] = Premium(modal, "Ally" + i, "", 44 + 248*i, 102, 224, 120, () => ChooseAlly(index));
            allies[i].SetPersistentColors(new Color32(14,21,27,255), new Color32(23,35,43,255));
            allies[i].SetLabelColor(Color.white);
            Image border = allies[i].transform.Find("Border")?.GetComponent<Image>();
            if (border != null) border.color = i == 0 ? Cyan : Gold;
            TextMeshProUGUI label = allies[i].GetComponentInChildren<TextMeshProUGUI>();
            label.font = Font("Inter-Bold"); label.fontSize = 16;
            label.rectTransform.offsetMin = new Vector2(80, 16);
            label.rectTransform.offsetMax = new Vector2(-12, -16);
            RectTransform badge = Rect("AllyMarker", allies[i].transform, 18, 30, 46, 52);
            Image icon = badge.gameObject.AddComponent<Image>(); icon.raycastTarget = false;
            Texture2D texture = Resources.Load<Texture2D>("UI/Match/Official/PlayerMarkerMask");
            if (texture != null) { icon.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);ownedSprites.Add(icon.sprite); }
            icon.color = i == 0 ? new Color32(38,93,151,255) : new Color32(56,133,64,255);
        }
        confirm = Premium(modal, "Confirm", "CONFIRMAR", 292, 172, 224, 64, ConfirmTransfer);
        cancel = Premium(modal, "Cancel", "CANCELAR", 44, 172, 224, 64, Dismiss);
    }

    // 04. Setores e efeitos exportados dos nodes 122:467 e 131:843, sem redesenho.
    private RectTransform BuildRadial(bool isTactic)
    {
        float oy = isTactic ? 192 : 7;
        float ox = isTactic ? 1259 : 1258;
        RectTransform root = Rect(isTactic ? "TacticalRadial" : "EmojiRadial", transform, 0, 0, 461, isTactic ? 615 : 691);
        Vector2 center = new Vector2(345.5f, isTactic ? 317.5f : 357.5f);
        float[,] boxes = isTactic ? new float[,] {
            {1352.983f,581.116f,194.583f,214.629f,-115.714f}, {1278.925f,494.083f,218.805f,212.01f,-141.429f},
            {1271,409.916f,199.691f,167.399f,-167.143f}, {1301.445f,306.403f,199.691f,167.399f,167.143f},
            {1364.231f,204.046f,218.805f,212.01f,141.429f} } : new float[,] {
            {1477.506f,510.776f,131.821f,174.739f,-90}, {1351.982f,438.116f,194.583f,214.629f,-115.714f},
            {1277.924f,351.083f,218.805f,212.01f,-141.429f}, {1270,266.916f,199.691f,167.399f,-167.143f},
            {1300.444f,163.403f,199.691f,167.399f,167.143f}, {1363.230f,61.046f,218.805f,212.01f,141.429f},
            {1475.253f,19,194.583f,214.629f,115.714f} };
        for (int i = 0; i < boxes.GetLength(0); i++)
        {
            Image sector = Art(root, (isTactic ? "TacticalSegment" : "EmojiSegment") + (i+1), 0, 0, 198.739f, 155.821f);
            sector.rectTransform.pivot = new Vector2(.5f, .5f);
            sector.rectTransform.anchoredPosition = new Vector2(boxes[i,0]+boxes[i,2]/2-ox, -(boxes[i,1]+boxes[i,3]/2-oy));
            sector.rectTransform.localEulerAngles = new Vector3(0, 0, boxes[i,4]);
        }
        Art(root, "HubArc", 1458-ox, (isTactic ? 359 : 215)-oy, 145, 300);
        if (!isTactic) Art(root, "EmojiRing", 1490-ox, 251-oy, 227, 227);
        Art(root, "HubAvatar", (isTactic ? 1504 : 1503)-ox, (isTactic ? 409 : 264)-oy, 201, 201);
        RectTransform plate = Rect("Nameplate", root, (isTactic ? 1544 : 1540)-ox, (isTactic ? 581 : 435)-oy, 131, 26);
        plate.gameObject.AddComponent<Image>().color = new Color32(7,17,31,235);
        WDHomeUIFactory.AddRoundedBorder(plate, Cyan, 4);
        TextMeshProUGUI name = Label(plate, "PlayerName", "", 4, 1, 123, 24, 14, Color.white);
        name.font = Font("Inter-Bold");
        if (isTactic) tacticalName = name; else emojiName = name;
        // Centro recebe o toque sem fechar; setores têm hit test polar, sem retângulos sobrepostos.
        RectTransform centerHit = Rect("AvatarHit", root, center.x-106, center.y-106, 212, 212);
        centerHit.gameObject.AddComponent<Image>().color = Color.clear; Click(centerHit, Consume);
        if (!isTactic)
        {
            float[,] positions = { {1540,112},{1452,159},{1387,246},{1363,344},{1382,450},{1445,537} };
            for (int i = 0; i < 6; i++)
            {
                string option = WDMatchReactions.Options[i];
                // Cinco símbolos já estão no atlas compartilhado; o beijo usa o SVG
                // Noto da mesma revisão, sem alterar os 34 índices/aliases do Chat.
                if (option == "😘")
                    Art(root, "NotoKiss", positions[i,0]-ox, positions[i,1]-oy, 42, 42);
                else
                {
                    TextMeshProUGUI label = Label(root, "Emoji"+i, option, positions[i,0]-ox, positions[i,1]-oy, 50, 50, 34, Color.white);
                    WDChatEmojiColor.Apply(label, true);
                }
                SectorHit(root, center, i, 7, () => SelectEmoji(option));
            }
            Art(root, "HideEye", 1543-ox, 588-oy, 35, 25);
            hideSlash = Art(root, "HideSlash", 1543-ox, 588-oy, 35, 25);
            hideLabel = Label(root, "HideLabel", "OCULTAR\nEMOJIS", 1535-ox, 617-oy, 62, 32, 12, Color.gray);
            SectorHit(root, center, 6, 7, () => { Reactions.ToggleHidden(); RefreshEmojiVisibility(); reaction.gameObject.SetActive(false); kissReaction.gameObject.SetActive(false); Close(); Consume(); });
        }
        else
        {
            WDTacticalPingKind[] kinds = { WDTacticalPingKind.Attack,WDTacticalPingKind.Defend,WDTacticalPingKind.Focus,WDTacticalPingKind.Transfer,WDTacticalPingKind.Attention };
            float[,] pos = { {1423,273,74,1425,329},{1363,360,72,1364,418},{1338.25f,468.25f,71.5f,1337,522},{1359,570,72,1350,624},{1418,658,76,1419,716} };
            for (int i = 0; i < 5; i++)
            {
                WDTacticalPingKind kind = kinds[i];
                Art(root, WDTacticalVisualStyle.IconName(kind), pos[i,0]-ox,pos[i,1]-oy,pos[i,2],pos[i,2]);
                Label(root, WDTacticalVisualStyle.Label(kind), WDTacticalVisualStyle.Label(kind),pos[i,3]-ox,pos[i,4]-oy,88,22,15,Color.white);
                SectorHit(root, center, i+1, 7, () => SelectTactic(kind));
            }
            Art(root,"TacticAttentionDot",1434-ox,681-oy,44,44);
        }
        return root;
    }
    private void SectorHit(RectTransform root, Vector2 center, int index, int count, Action action)
    {
        RectTransform hit = Rect("SectorHit"+index, root, 0, 0, root.sizeDelta.x, root.sizeDelta.y);
        hit.gameObject.AddComponent<Image>().color = Color.clear;
        var filter = hit.gameObject.AddComponent<WDMatchRadialHit>();
        filter.Center = center; filter.StartAngle = 90 + index*180f/count; filter.EndAngle = 90 + (index+1)*180f/count;
        Click(hit, () => { Consume(); action(); });
    }

    // 05. Apenas overlays se adaptam à Safe Area. O mapa/câmera não são modificados.
    private void LateUpdate()
    {
        RefreshEmojiVisibility();
        if (IsOpen) { FitOutside(); FitRadial(); if (modal.gameObject.activeSelf) FitModal(); }
        UpdateMarker();
        if (reaction != null && reaction.gameObject.activeSelf) PositionBesidePlayer(reaction.rectTransform, reactionAuthor);
        if (kissReaction != null && kissReaction.gameObject.activeSelf) PositionBesidePlayer(kissReaction.rectTransform, reactionAuthor);
        if (reaction != null && reaction.gameObject.activeSelf && Time.unscaledTime >= reactionUntil) reaction.gameObject.SetActive(false);
        if (kissReaction != null && kissReaction.gameObject.activeSelf && Time.unscaledTime >= reactionUntil) kissReaction.gameObject.SetActive(false);
    }
    public static Vector2 ClampTopLeft(Rect safe, Vector2 topLeft, Vector2 size, float margin = 8) => new Vector2(
        Mathf.Clamp(topLeft.x, safe.xMin+margin, Mathf.Max(safe.xMin+margin, safe.xMax-size.x-margin)),
        Mathf.Clamp(topLeft.y, Mathf.Min(safe.yMax-margin, safe.yMin+size.y+margin), safe.yMax-margin));
    private void FitRadial()
    {
        if (playerEntries.TryGetValue(player, out var current)) anchor = current;
        if (anchor == null) return;
        RectTransform root = emoji.gameObject.activeSelf ? emoji : tactical;
        RectTransform parent = transform as RectTransform;
        float scale = Mathf.Min(1, (parent.rect.width-16)/root.sizeDelta.x, (parent.rect.height-16)/root.sizeDelta.y);
        root.localScale = Vector3.one*Mathf.Max(.1f,scale);
        Vector2 point = parent.InverseTransformPoint(anchor.TransformPoint(anchor.rect.center));
        Vector2 center = new Vector2(345.5f, root == emoji ? 357.5f : 317.5f);
        Vector2 topLeft = new Vector2(PlayerColumnLeft(anchor) - 16 - root.sizeDelta.x*scale, point.y + center.y*scale);
        PlaceInParent(root, ClampTopLeft(parent.rect, topLeft, root.sizeDelta*scale));
    }
    private void FitModal()
    {
        RectTransform parent = transform as RectTransform;
        float scale = Mathf.Min(1, (parent.rect.width-16)/modal.sizeDelta.x, (parent.rect.height-16)/modal.sizeDelta.y);
        modal.localScale = Vector3.one*scale;
        PlaceInParent(modal, parent.rect.center + new Vector2(-modal.sizeDelta.x/2,modal.sizeDelta.y/2)*scale);
    }
    private void RefreshEmojiVisibility()
    {
        if (hideSlash != null) hideSlash.gameObject.SetActive(!Reactions.Hidden);
        if (hideLabel != null) hideLabel.text = Reactions.Hidden ? "MOSTRAR\nEMOJIS" : "OCULTAR\nEMOJIS";
        if (!Reactions.Hidden) return;
        if (reaction != null) reaction.gameObject.SetActive(false);
        if (kissReaction != null) kissReaction.gameObject.SetActive(false);
    }
    private float PlayerColumnLeft(RectTransform entry)
    {
        // O painel é irmão de Players na raiz V2, não filho da lista.
        // Medir sua borda externa inclui o padding aprovado de 8 px.
        var panel = entry.GetComponentInParent<WDMatchUIV2>()?.transform.Find("PlayersContrast") as RectTransform;
        var corners = new Vector3[4]; (panel != null ? panel : entry).GetWorldCorners(corners);
        return transform.InverseTransformPoint(corners[0]).x;
    }
    private void PositionBesidePlayer(RectTransform rect, TerritorioClique.Dono owner)
    {
        if (!playerEntries.TryGetValue(owner, out var entry) || entry == null) return;
        var parent = (RectTransform)transform;
        Vector2 point = parent.InverseTransformPoint(entry.TransformPoint(entry.rect.center));
        rect.pivot = new Vector2(.5f, .5f);
        point.x = PlayerColumnLeft(entry) - 12 - rect.sizeDelta.x/2;
        point = WDMatchInformationalOverlayController.ClampToViewport(point, parent.rect, rect.sizeDelta);
        PlaceInParent(rect, point);
    }
    private void SetConfirmationVisual(TerritorioVisual value)
    {
        if (confirmationVisual != value && confirmationVisual != null) confirmationVisual.SetTransferConfirmationHighlight(false);
        confirmationVisual = value;
        if (confirmationVisual != null) confirmationVisual.SetTransferConfirmationHighlight(true);
    }
    private void PlaceInParent(RectTransform rect, Vector2 point)
    { RectTransform parent = rect.parent as RectTransform; rect.anchoredPosition = point - new Vector2(parent.rect.xMin,parent.rect.yMax); }
    private void FitOutside()
    {
        Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null) return;
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform parent = transform as RectTransform;
        Vector3 min = parent.InverseTransformPoint(canvasRect.TransformPoint(canvasRect.rect.min));
        Vector3 max = parent.InverseTransformPoint(canvasRect.TransformPoint(canvasRect.rect.max));
        outside.offsetMin = (Vector2)min-parent.rect.min; outside.offsetMax = (Vector2)max-parent.rect.max;
    }
    private void UpdateMarker()
    {
        if (marker == null) return;
        bool visible = state?.Transferencia != null && markerTerritory != null && Camera.main != null;
        marker.gameObject.SetActive(visible);
        if (!visible) return;
        if (MapOverlayProjection.TryWorldToOverlay(transform as RectTransform,
            MapOverlayProjection.GetTerritoryWorldAnchor(markerTerritory), Camera.main, out Vector2 point))
        { marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = marker.rectTransform.pivot = new Vector2(.5f,.5f); marker.rectTransform.anchoredPosition = point; }
    }

    // 06. Recursos pertencem à View; TMP/Noto compartilhados nunca são destruídos.
    private Image Art(Transform parent, string name, float x,float y,float w,float h)
    {
        RectTransform rect = Rect(name,parent,x,y,w,h);
        Image image = rect.gameObject.AddComponent<Image>(); image.raycastTarget=false;
        Texture2D texture = Resources.Load<Texture2D>(AssetRoot+name);
        if (texture != null) { image.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100); ownedSprites.Add(image.sprite); }
        return image;
    }
    private TextMeshProUGUI Label(Transform parent,string name,string value,float x,float y,float w,float h,float size,Color color)
    {
        TextMeshProUGUI label=WDUIFactory.Text(name,parent,theme,size,color,TextAlignmentOptions.Center);
        label.font = Font("Gabarito-Medium");
        RectTransform rect=label.rectTransform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h);
        label.richText=false; label.raycastTarget=false; label.enableAutoSizing=false; label.text=value;
        if (name == "PlayerName")
            WDUITextPolicies.Apply(label, WDUITextPolicy.SingleLineEllipsis);
        return label;
    }
    private TMP_FontAsset Font(string name)
    {
        if (fonts.TryGetValue(name, out TMP_FontAsset font)) return font;
        UnityEngine.Font source = Resources.Load<UnityEngine.Font>("UI/Fonts/"+name);
        if (source == null) return theme.FonteInterface;
        font = TMP_FontAsset.CreateFontAsset(source); font.hideFlags = HideFlags.DontSave;
        fonts.Add(name,font); ProtectFonts(null); return font;
    }
    private void ProtectFonts(UnityEngine.Object changed)
    {
        foreach (TMP_FontAsset font in fonts.Values)
        {
            if (font == null) continue;
            font.material.hideFlags = HideFlags.DontSave;
            for (int i=0;i<font.atlasTextureCount;i++) font.atlasTextures[i].hideFlags=HideFlags.DontSave;
        }
    }
    private WDUIPremiumButton Premium(Transform parent,string name,string label,float x,float y,float w,float h,Action action)
    {
        var button=Rect(name,parent,x,y,w,h).gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme,label,()=>{Consume();action();}); return button;
    }
    private static void Click(RectTransform rect,Action action)
    { Button button=rect.gameObject.AddComponent<Button>(); button.transition=Selectable.Transition.None; button.onClick.AddListener(()=>action()); }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    { RectTransform rect=WDUIFactory.Rect(name,parent); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect; }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
}

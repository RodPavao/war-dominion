using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeRankingPanel : MonoBehaviour
{
    private const float RowsViewportY = 119f;
    private const float RowsViewportHeight = 618f;
    private const float PinnedRowY = 749f;
    private const string EmblemRoot = "UI/Home/FigmaPhase1A/Canonical/";
    private static readonly Color Gold = C("C59B63"), Cyan = C("00E5FF"),
        Text = C("F8FAFC"), Muted = C("94A3B8"), Border = C("1E3048"),
        PanelColor = C("08111D"), RowA = C("0C1929"), RowB = C("0A1424"), Ink = C("05080D");

    private WarDominionUITheme theme;
    private WDRankingRepository repository;
    private WDNavigationStack navigation;
    private Action<string> openPublicProfile;
    private Action<bool> setHomeCompositionVisible;
    private WDRankingQuery query;
    private RectTransform content;
    private GameObject divisionsModal;
    private TMP_InputField searchInput;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsDivisionsModalOpen => divisionsModal != null;
    public WDRankingQuery CurrentQuery => query.Clone();

    public void Build(WarDominionUITheme newTheme, WDRankingRepository rankingRepository,
        WDNavigationStack navigationStack, Action<string> publicProfileAction)
        => Build(newTheme, rankingRepository, navigationStack, publicProfileAction, null, null);

    public void Build(WarDominionUITheme newTheme, WDRankingRepository rankingRepository,
        WDNavigationStack navigationStack, Action<string> publicProfileAction,
        Texture2D backgroundTexture, Action<bool> homeCompositionVisibility)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        repository = rankingRepository ?? throw new ArgumentNullException(nameof(rankingRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        openPublicProfile = publicProfileAction;
        setHomeCompositionVisible = homeCompositionVisibility;
        query = new WDRankingQuery { Season = repository.CurrentSeason };
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = C("060A12");
        blocker.raycastTarget = true;

        RectTransform background = WDHomeUIFactory.Rect("RankingBackground", transform);
        background.anchorMin = background.anchorMax = background.pivot = new Vector2(.5f, .5f);
        RawImage art = background.gameObject.AddComponent<RawImage>();
        art.texture = backgroundTexture;
        art.color = backgroundTexture != null ? Color.white : C("060A12");
        art.raycastTarget = false;
        AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = backgroundTexture != null
            ? (float)backgroundTexture.width / backgroundTexture.height : 16f / 9f;
        Image atmosphere = WDHomeUIFactory.Image("RankingAtmosphere", transform,
            new Color(.024f, .039f, .071f, .85f));
        WDHomeUIFactory.Stretch(atmosphere.rectTransform);
        atmosphere.raycastTarget = false;

        content = WDHomeUIFactory.Rect("RankingContent", transform);
        WDHomeUIFactory.Stretch(content);
        gameObject.SetActive(false);
    }

    public void Open() => navigation.Push(new WDNavigationContext("ranking", Restore, CaptureState));
    public void HideFromNavigation()
    {
        CloseDivisions();
        gameObject.SetActive(false);
        setHomeCompositionVisible?.Invoke(true);
    }
    public bool TryHandleLocalBack()
    {
        if (!IsDivisionsModalOpen) return false;
        CloseDivisions();
        return true;
    }
    private object CaptureState()
    {
        if (searchInput != null) query.Search = searchInput.text;
        return query.Clone();
    }
    private void Restore(object state)
    {
        if (state is WDRankingQuery saved) query = saved.Clone();
        setHomeCompositionVisible?.Invoke(false);
        transform.SetAsLastSibling();
        Render();
    }

    // 02. A referência central preserva os pixels do Figma em 16:9 e ganha
    // espaço lateral, sem distorção, nas proporções ultrawide.
    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        RectTransform frame = Rect("RankingResponsiveShell", content, 0, 0, 0, 0);
        WDHomeUIFactory.Stretch(frame);
        WDHomeBackButtonFactory.BuildCanonical(frame, () => navigation.TryBack());
        TextMeshProUGUI title = Label(frame, "Title", "RANKING", 28, Text, 0, 40, 300, 46,
            TextAlignmentOptions.Center, "Gabarito-ExtraBold");
        CenterTop(title.rectTransform, 0, 40, 300, 46);

        RectTransform summary = Surface(frame, "DivisionSummary", 64, 132, 480, 525, PanelColor, 16, Border);
        DivisionSummary(summary);
        RectTransform section = Rect("RankingListSection", frame, 576, 132, 1280, 868);
        StretchBody(section, 576, 64, 132, 80);
        Selectors(section);
        RectTransform table = Surface(section, "RankingTable", 0, 51, 1280, 817, PanelColor, 16, Border);
        StretchBody(table, 0, 0, 51, 0);
        Table(table);
    }

    private void Selectors(RectTransform parent)
    {
        RectTransform entity = Surface(parent, "PrimarySelector", 0, 0, 318, 40, RowA, 8, Border);
        Segment(entity, "Players", "JOGADORES", () => SetEntity(WDRankingEntity.Players),
            0, 0, 159, 40, query.Entity == WDRankingEntity.Players, true);
        Segment(entity, "Clans", "CLÃS", () => SetEntity(WDRankingEntity.Clans),
            159, 0, 159, 40, query.Entity == WDRankingEntity.Clans, true);
        RectTransform scope = Surface(parent, "SecondarySelector", 918, 4, 362, 32,
            Color.clear, 7, Border);
        Segment(scope, "Global", "GLOBAL", () => SetScope(WDRankingScope.General),
            0, 0, 181, 32, query.Scope == WDRankingScope.General, false);
        Segment(scope, "Season", "TEMPORADA", () => SetScope(WDRankingScope.Season),
            181, 0, 181, 32, query.Scope == WDRankingScope.Season, false);
    }

    private void DivisionSummary(RectTransform parent)
    {
        WDProfileData profile = repository.Profiles.LocalProfile;
        WDDivisionDefinition current = profile.DivisionDefinition;
        WDDivisionDefinition next = repository.Divisions.Ordered.FirstOrDefault(
            d => d.DisplayOrder > current.DisplayOrder);
        Label(parent, "Heading", "SUA DIVISÃO ATUAL", 14, Cyan, 32, 28, 416, 22,
            TextAlignmentOptions.Left, "Gabarito-Bold", .84f);
        Emblem(parent, "CurrentEmblem", current, 180, 76, 120);
        Label(parent, "CurrentDivision", current.DisplayName, 32, Text, 32, 212, 416, 40,
            TextAlignmentOptions.Center, "Gabarito-ExtraBold");
        Label(parent, "Points", $"{profile.DivisionPoints:N0} PONTOS", 16, Cyan, 32, 254, 416, 24,
            TextAlignmentOptions.Center, "Inter-Bold");
        Rule(parent, 32, 302, 416);
        int upper = next == null ? Math.Max(1, profile.DivisionPoints) :
            Mathf.Max(500, Mathf.CeilToInt((profile.DivisionPoints + 1) / 500f) * 500);
        int lower = next == null ? 0 : Mathf.Max(0, upper - 500);
        float progress = next == null ? 1 : Mathf.InverseLerp(lower, upper, profile.DivisionPoints);
        Label(parent, "ProgressCaption", "PROGRESSO DA DIVISÃO", 12, Muted, 32, 324, 210, 18,
            TextAlignmentOptions.Left, "Gabarito-Medium");
        Label(parent, "ProgressValue", next == null ? "SEM LIMITE" :
            $"{profile.DivisionPoints:N0} / {upper:N0}", 12, Text, 242, 324, 206, 18,
            TextAlignmentOptions.Right, "Inter-Bold");
        RectTransform track = Surface(parent, "ProgressTrack", 32, 352, 416, 8, Border, 4);
        Surface(track, "ProgressFill", 0, 0, 416 * progress, 8, Gold, 4);
        Label(parent, "NextCaption", "PRÓXIMA DIVISÃO:", 14, Muted, 32, 386, 215, 24,
            TextAlignmentOptions.Left, "Gabarito-Medium");
        if (next != null)
        {
            Emblem(parent, "NextEmblem", next, 252, 374, 48);
            Label(parent, "NextDivision", next.DisplayName, 14, Text, 308, 378, 140, 40,
                TextAlignmentOptions.Right, "Gabarito-Bold");
        }
        else Label(parent, "NextDivision", "SEM LIMITE SUPERIOR", 12, Gold, 220, 386, 228, 24,
            TextAlignmentOptions.Right, "Gabarito-Bold");
        Button(parent, "AllDivisions", "VER TODAS AS DIVISÕES", OpenDivisions,
            32, 445, 416, 48, false);
    }

    private void Table(RectTransform parent)
    {
        string hint = query.Entity == WDRankingEntity.Players
            ? "BUSCAR JOGADOR OU ID" : "BUSCAR CLÃ OU ID";
        searchInput = Input(parent, "Search", hint, query.Search, 730, 24, 390, 40);
        Button(parent, "SearchButton", "BUSCAR", ApplySearch, 1130, 24, 126, 40, true);
        Rule(parent, 24, 108, 1232);
        Headers(parent);
        WDRankingResult result = repository.Query(query);
        if (result.State != WDRankingResultState.Ready)
        {
            Label(parent, "Empty", result.State == WDRankingResultState.Empty ?
                "NENHUM RESULTADO PARA O RECORTE ATUAL." : "RANKING INDISPONÍVEL.",
                17, Muted, 80, 310, 1120, 90, TextAlignmentOptions.Center, "Gabarito-Medium");
            return;
        }
        RenderScrollableRows(parent, result);
        if (query.Entity == WDRankingEntity.Players && result.LocalPlayer != null)
            PinnedPlayer(parent, result.LocalPlayer);
        else if (query.Entity == WDRankingEntity.Clans)
            PinnedClan(parent);
    }

    private void RenderScrollableRows(RectTransform parent, WDRankingResult result)
    {
        const float rowHeight = 72f;
        const float rowStep = 78f;
        RectTransform viewport = Rect("RowsViewport", parent, 24, RowsViewportY,
            1232, RowsViewportHeight);
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = Color.clear;
        viewportHit.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform rows = WDHomeUIFactory.Rect("RowsContent", viewport);
        rows.anchorMin = new Vector2(0f, 1f);
        rows.anchorMax = new Vector2(1f, 1f);
        rows.pivot = new Vector2(.5f, 1f);
        rows.anchoredPosition = Vector2.zero;
        rows.sizeDelta = new Vector2(0f,
            Mathf.Max(RowsViewportHeight,
                result.Entries.Count * rowStep - (rowStep - rowHeight)));

        for (int i = 0; i < result.Entries.Count; i++)
            RankingRow(rows, result.Entries[i], 0, i * rowStep, 1218, rowHeight,
                result.Entries[i].IsLocalPlayer, false);

        Scrollbar scrollbar = CreateScrollbar(parent, 1262, RowsViewportY, 6,
            RowsViewportHeight);
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = rows;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = .135f;
        scroll.scrollSensitivity = 36f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalNormalizedPosition = 1f;
    }

    private void Headers(RectTransform p)
    {
        Label(p, "PositionHeader", "POS.", 12, Muted, 31, 91, 72, 18, TextAlignmentOptions.Left, "Gabarito-Black");
        Label(p, "NameHeader", query.Entity == WDRankingEntity.Players ? "JOGADOR" : "CLÃ",
            12, Muted, 160, 91, 300, 18, TextAlignmentOptions.Left, "Gabarito-Black");
        Label(p, "ContextHeader", query.Entity == WDRankingEntity.Players ? "CLÃ" : "MEMBROS",
            12, Muted, 524, 91, 220, 18, TextAlignmentOptions.Left, "Gabarito-Black");
        Label(p, "DivisionHeader", query.Entity == WDRankingEntity.Players ? "DIVISÃO" : "DIVISÃO / LIGA",
            12, Muted, 904, 91, 190, 18, TextAlignmentOptions.Left, "Gabarito-Black");
        Label(p, "PointsHeader", "PONTOS", 12, Muted, 1132, 91, 100, 18,
            TextAlignmentOptions.Right, "Gabarito-Black");
    }

    private void RankingRow(RectTransform parent, WDRankingEntry e, float x, float y,
        float width, float height, bool selected, bool pinned)
    {
        RectTransform row = Surface(parent, "RankingRow_" + e.EntityId, x, y, width, height,
            selected ? new Color(.055f, .145f, .20f, 1) : (e.Position % 2 == 1 ? RowA : RowB),
            8, selected ? Cyan : Border);
        if (query.Entity == WDRankingEntity.Players)
        {
            Button hit = row.gameObject.AddComponent<Button>();
            row.GetComponent<Image>().raycastTarget = true;
            hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => openPublicProfile?.Invoke(e.PublicPlayerId));
        }
        Color rank = e.Position == 1 ? Gold :
            e.Position == 2 ? C("E0E6EE") :
            e.Position == 3 ? C("CD7F32") : Muted;
        Label(row, "Position", $"#{e.Position}", 20, rank, 0, 10, 72, 44,
            TextAlignmentOptions.Center, "Gabarito-ExtraBold");
        Avatar(row, 76, 10, 52, rank, e);
        Label(row, "Name", pinned ? "SUA POSIÇÃO · " + e.DisplayName : e.DisplayName,
            16, Text, 144, 14, 332, 44, TextAlignmentOptions.Left, "Gabarito-Medium");
        if (query.Entity == WDRankingEntity.Players)
        {
            Label(row, "Clan", string.IsNullOrWhiteSpace(e.ClanName) ? "—" : e.ClanName,
                14, Muted, 500, 10, 275, 44, TextAlignmentOptions.Left, "Gabarito-Medium");
            CompactDivision(row, e.Division, 810, height, 56, 205);
        }
        else
        {
            Label(row, "Members", "—", 14, Muted, 500, 10, 275, 44,
                TextAlignmentOptions.Left, "Gabarito-Medium");
            CompactDivision(row, null, 810, height, 56, 205);
        }
        Label(row, "Points", $"{e.Points:N0} PTS", 16, selected ? Cyan : rank,
            1060, 10, 148, 44, TextAlignmentOptions.Right, "Inter-Bold");
    }

    private void PinnedPlayer(RectTransform parent, WDRankingEntry e)
    {
        RectTransform row = Surface(parent, "LocalPosition", 24, PinnedRowY, 1232, 56,
            new Color(.055f, .145f, .20f, 1), 8, Gold);
        Label(row, "Position", $"{e.Position}/7", 12, Muted, 16, 7, 70, 42,
            TextAlignmentOptions.Left, "Inter-Regular");
        Avatar(row, 70, 8, 40, Muted, e);
        Label(row, "Identity", "SUA POSIÇÃO · " + e.DisplayName, 13, Text, 120, 7, 500, 42,
            TextAlignmentOptions.Left, "Gabarito-Bold");
        CompactDivision(row, e.Division, 822, 56, 48, 198);
        Label(row, "Points", $"{e.Points:N0} PTS", 15, Cyan, 1060, 7, 148, 42,
            TextAlignmentOptions.Right, "Inter-Bold");
    }

    private void PinnedClan(RectTransform parent)
    {
        RectTransform row = Surface(parent, "LocalClanPosition", 24, PinnedRowY, 1232, 56,
            new Color(.055f, .145f, .20f, 1), 8, Gold);
        Label(row, "Position", "—/7", 12, Muted, 16, 7, 70, 42,
            TextAlignmentOptions.Left, "Inter-Regular");
        Avatar(row, 70, 8, 40, Muted, null);
        string clan = repository.Profiles.LocalProfile.Clan;
        Label(row, "Identity", "SEU CLÃ · " + (string.IsNullOrWhiteSpace(clan) ? "CLÃ DO JOGADOR" : clan),
            13, Text, 120, 7, 600, 42, TextAlignmentOptions.Left, "Gabarito-Bold");
        Label(row, "Points", $"{repository.Profiles.LocalProfile.DivisionPoints:N0} PTS",
            15, Cyan, 1060, 7, 148, 42, TextAlignmentOptions.Right, "Inter-Bold");
    }

    // 05. O modal lê o mesmo catálogo e marca a divisão real; Veteran mantém
    // seu contrato aberto em uma faixa separada.
    private void OpenDivisions()
    {
        if (divisionsModal != null) return;
        RectTransform modal = WDHomeUIFactory.Rect("RankingDivisionsModal", transform);
        WDHomeUIFactory.Stretch(modal);
        divisionsModal = modal.gameObject;
        RectTransform backdrop = WDHomeUIFactory.Rect("Backdrop", modal);
        WDHomeUIFactory.Stretch(backdrop);
        Image veil = backdrop.gameObject.AddComponent<Image>();
        veil.color = new Color(.01f, .02f, .04f, .86f);
        veil.raycastTarget = true;
        Button dismiss = backdrop.gameObject.AddComponent<Button>();
        dismiss.targetGraphic = veil;
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(CloseDivisions);
        RectTransform frame = Rect("ModalReferenceFrame", modal, 0, 0, 1920, 1080);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.anchoredPosition = Vector2.zero;
        RectTransform panel = Surface(frame, "Panel", 210, 90, 1500, 900,
            PanelColor, 16, Gold);
        panel.GetComponent<Image>().raycastTarget = true;
        Label(panel, "Title", "TODAS AS DIVISÕES", 28, Text, 32, 22, 1436, 38,
            TextAlignmentOptions.Left, "Gabarito-ExtraBold");
        Label(panel, "Intro", "Progrida vencendo batalhas para alcançar pelas divisões de War Dominion.",
            14, Muted, 32, 58, 1436, 24, TextAlignmentOptions.Left, "Gabarito-Medium");
        WDDivisionDefinition current = repository.Profiles.LocalProfile.DivisionDefinition;
        var regular = repository.Divisions.Ordered.Where(d => !d.HasOpenUpperLimit).ToArray();
        for (int i = 0; i < regular.Length; i++)
            DivisionCard(panel, regular[i], regular[i].Id == current.Id,
                32 + i % 5 * 292, 92 + i / 5 * 298, 268, 280);
        WDDivisionDefinition veteran = repository.Divisions.Ordered.FirstOrDefault(d => d.HasOpenUpperLimit);
        if (veteran != null) Veteran(panel, veteran, veteran.Id == current.Id);
        Button(panel, "Close", "VOLTAR AO RANKING", CloseDivisions,
            32, 824, 1436, 52, false);
    }

    private void DivisionCard(RectTransform p, WDDivisionDefinition d, bool current,
        float x, float y, float w, float h)
    {
        RectTransform card = Surface(p, "Division_" + d.Id, x, y, w, h,
            current ? new Color(.11f, .10f, .075f, 1) : RowA, 9, current ? Gold : Border);
        const float emblemSide = 150f;
        Emblem(card, "Emblem", d, (w - emblemSide) * .5f, 18, emblemSide);
        Label(card, "Name", d.DisplayName, d.DisplayName.Length > 15 ? 14 : 16,
            current ? Gold : Text, 12, 178, w - 24, 28,
            TextAlignmentOptions.Center, "Gabarito-Bold");
        Label(card, "Order", $"DIVISÃO {d.DisplayOrder / 10:00}", 11, Muted,
            12, 207, w - 24, 20,
            TextAlignmentOptions.Center, "Gabarito-Medium");
        if (current) Label(card, "Current", "DIVISÃO ATUAL", 10, Ink,
            42, 238, w - 84, 26, TextAlignmentOptions.Center,
            "Gabarito-Black", 0, Gold);
    }

    private void Veteran(RectTransform p, WDDivisionDefinition d, bool current)
    {
        RectTransform card = Surface(p, "DivisionVeteran", 32, 694, 1436, 112,
            current ? new Color(.11f, .10f, .075f, 1) : RowA, 9, current ? Gold : Border);
        const float emblemSide = 88f;
        Emblem(card, "Emblem", d, 18, 12, emblemSide);
        Label(card, "Name", "VETERAN", 20, Gold, 126, 25, 420, 30,
            TextAlignmentOptions.Left, "Gabarito-ExtraBold");
        Label(card, "Limit", "SEM LIMITE SUPERIOR", 13, Muted, 126, 58, 420, 24,
            TextAlignmentOptions.Left, "Gabarito-Medium");
        if (current) Label(card, "Current", "DIVISÃO ATUAL", 11, Ink,
            1192, 38, 214, 36, TextAlignmentOptions.Center,
            "Gabarito-Black", 0, Gold);
    }

    private void ApplySearch() { query.Search = searchInput?.text; Render(); }
    private void SetScope(WDRankingScope value) { query.Scope = value; query.Search = ""; Render(); }
    private void SetEntity(WDRankingEntity value) { query.Entity = value; query.Search = ""; Render(); }
    private void CloseDivisions()
    {
        if (divisionsModal != null) DestroyNow(divisionsModal);
        divisionsModal = null;
    }

    // 06. Primitivas locais seguem os valores medidos no Figma e reutilizam a
    // borda contínua de 1 px já aprovada em Configurações.
    private RectTransform Surface(Transform p, string n, float x, float y, float w, float h,
        Color fill, int radius, Color? border = null)
    {
        RectTransform r = Rect(n, p, x, y, w, h);
        Image image = r.gameObject.AddComponent<Image>();
        image.color = fill; image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, radius);
        if (border.HasValue) WDHomeUIFactory.AddThinRoundedBorder(r, border.Value, radius);
        return r;
    }

    private void Segment(RectTransform p, string n, string value, UnityAction action,
        float x, float y, float w, float h, bool selected, bool primary)
    {
        RectTransform r = Surface(p, n, x, y, w, h,
            selected ? (primary ? Gold : C("0A2633")) : Color.clear, primary ? 8 : 7,
            selected ? (primary ? Gold : Cyan) : (Color?)null);
        Button b = r.gameObject.AddComponent<Button>();
        r.GetComponent<Image>().raycastTarget = true;
        b.transition = Selectable.Transition.None; b.onClick.AddListener(action);
        Label(r, "Label", value, primary ? 16 : 12,
            selected ? (primary ? Ink : Text) : Muted, 0, 0, w, h,
            TextAlignmentOptions.Center, "Gabarito-Black");
    }

    private RectTransform Button(Transform p, string n, string value, UnityAction action,
        float x, float y, float w, float h, bool primary, bool pill = false)
    {
        int radius = pill ? Mathf.RoundToInt(h / 2) : 8;
        RectTransform r = Surface(p, n, x, y, w, h,
            primary ? Gold : new Color(.106f, .114f, .133f, .70f), radius, Gold);
        Button b = r.gameObject.AddComponent<Button>();
        r.GetComponent<Image>().raycastTarget = true;
        b.targetGraphic = r.GetComponent<Image>(); b.onClick.AddListener(action);
        Label(r, "Label", value, pill ? 16 : 13, primary ? Ink : Gold, 8, 0, w - 16, h,
            TextAlignmentOptions.Center, "Gabarito-Bold", pill ? 1.5f : .56f);
        return r;
    }

    private TMP_InputField Input(Transform p, string n, string hint, string value,
        float x, float y, float w, float h)
    {
        RectTransform r = Surface(p, n, x, y, w, h, C("060E17"), 8, Border);
        r.GetComponent<Image>().raycastTarget = true;
        TMP_InputField input = r.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect("TextViewport", r, 15, 0, w - 30, h);
        TextMeshProUGUI text = Label(viewport, "Text", value ?? "", 12, Text, 0, 0, w - 30, h,
            TextAlignmentOptions.Left, "Gabarito-Medium");
        TextMeshProUGUI placeholder = Label(viewport, "Placeholder", hint, 12, Muted,
            0, 0, w - 30, h, TextAlignmentOptions.Left, "Gabarito-Black");
        input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = WDUIContentContracts.SearchMaxLength;
        input.text = value ?? "";
        input.onSubmit.AddListener(_ => ApplySearch());
        Image focusBorder = r.Find("ThinBorder")?.GetComponent<Image>();
        WDHomeUIFactory.ConfigureEditableInputVisuals(
            input, r.GetComponent<Image>(), focusBorder, Border, Cyan);
        return input;
    }

    private void CompactDivision(Transform p, WDDivisionDefinition d, float x,
        float rowHeight, float slotSide, float width)
    {
        float y = (rowHeight - slotSide) * .5f;
        if (d == null)
        {
            Label(p, "Division", "—", 14, Muted, x, y, width, slotSide,
                TextAlignmentOptions.Left, "Gabarito-Medium");
            return;
        }
        Emblem(p, "DivisionEmblem", d, x, y, slotSide);
        Label(p, "Division", d.DisplayName, 13, Muted, x + slotSide + 8, y,
            width - slotSide - 8, slotSide,
            TextAlignmentOptions.Left, "Gabarito-Medium");
    }

    private void Emblem(Transform p, string n, WDDivisionDefinition d,
        float x, float y, float side)
    {
        RectTransform r = Rect(n, p, x, y, side, side);
        RawImage medallion = r.gameObject.AddComponent<RawImage>();
        medallion.texture = WDDivisionVisuals.ResolveMedallion();
        medallion.color = Color.white;
        medallion.raycastTarget = false;
        RectTransform symbolRect = Rect("CanonicalSymbol", r, 0f, 0f, side, side);
        RawImage symbol = symbolRect.gameObject.AddComponent<RawImage>();
        symbol.texture = WDDivisionVisuals.ResolveEmblem(d);
        symbol.color = Color.white;
        symbol.raycastTarget = false;
        WDDivisionVisuals.ApplyCalibration(symbolRect, d, side);
    }

    private void Avatar(Transform p, float x, float y, float side, Color border,
        WDRankingEntry entry)
    {
        RectTransform r = Surface(p, "Avatar", x, y, side, side, Border, 6, border);
        WDProfileData profile = entry == null ? null : repository.Profiles.AllProfiles
            .FirstOrDefault(item => item.PlayerId == entry.EntityId);
        Texture2D texture = profile != null
            ? WDProfileIdentityVisuals.ResolveAvatar(profile)
            : Resources.Load<Texture2D>(EmblemRoot + "ProfileUser");
        RawImage image = Rect("Image", r, side * .05f, side * .05f, side * .90f, side * .90f)
            .gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = texture != null ? Color.white : Muted;
        image.raycastTarget = false;
    }

    private Scrollbar CreateScrollbar(Transform p, float x, float y, float width, float height)
    {
        RectTransform track = Surface(p, "VerticalScrollbar", x, y, width, height, Border, 3);
        track.GetComponent<Image>().raycastTarget = true;
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.numberOfSteps = 0;
        RectTransform sliding = WDHomeUIFactory.Rect("SlidingArea", track);
        WDHomeUIFactory.Stretch(sliding);
        RectTransform handle = Surface(sliding, "Handle", 0, 0, width, height, Cyan, 3);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        handle.GetComponent<Image>().raycastTarget = true;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        return scrollbar;
    }

    private void Rule(Transform p, float x, float y, float w)
    {
        RectTransform r = Rect("Divider", p, x, y, w, 1);
        Image i = r.gameObject.AddComponent<Image>(); i.color = Border; i.raycastTarget = false;
    }

    private TextMeshProUGUI Label(Transform p, string n, string value, float size, Color color,
        float x, float y, float w, float h, TextAlignmentOptions align, string font,
        float spacing = 0, Color? background = null)
    {
        RectTransform r = Rect(n, p, x, y, w, h);
        RectTransform textRect = r;
        if (background.HasValue)
        {
            Image bg = r.gameObject.AddComponent<Image>(); bg.color = background.Value;
            bg.raycastTarget = false; WDHomeUIFactory.ApplyRounded(bg, 5);
            textRect = WDHomeUIFactory.Rect("Text", r);
            WDHomeUIFactory.Stretch(textRect);
        }
        TextMeshProUGUI label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value ?? ""; label.fontSize = size; label.color = color;
        label.alignment = align; label.characterSpacing = spacing;
        label.font = WDHomeTypography.Load(font); label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        return label;
    }

    private static void CenterTop(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    private static void StretchBody(RectTransform rect, float left, float right, float top, float bottom)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }

    private static RectTransform Rect(string n, Transform p, float x, float y, float w, float h)
    {
        RectTransform r = WDHomeUIFactory.Rect(n, p);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        return r;
    }
    private static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
    private static void Clear(Transform p)
    {
        for (int i = p.childCount - 1; i >= 0; i--) DestroyNow(p.GetChild(i).gameObject);
    }
    private static void DestroyNow(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}

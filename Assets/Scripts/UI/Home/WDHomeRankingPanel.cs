using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeRankingPanel : MonoBehaviour
{
    // ============================================================
    // 01. DEPENDÊNCIAS E ESTADO RESTAURÁVEL
    // ============================================================

    private WarDominionUITheme theme;
    private WDRankingRepository repository;
    private WDNavigationStack navigation;
    private Action<string> openPublicProfile;
    private WDRankingQuery query;
    private RectTransform content;
    private GameObject divisionsModal;
    private TMP_InputField searchInput;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsDivisionsModalOpen => divisionsModal != null;
    public WDRankingQuery CurrentQuery => query.Clone();

    public void Build(WarDominionUITheme newTheme, WDRankingRepository rankingRepository,
        WDNavigationStack navigationStack, Action<string> publicProfileAction)
    {
        theme = newTheme;
        repository = rankingRepository;
        navigation = navigationStack;
        openPublicProfile = publicProfileAction;
        query = new WDRankingQuery { Season = repository.CurrentSeason };
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.96f);
        blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("RankingContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 40f, 54f, 40f);
        gameObject.SetActive(false);
    }

    public void Open() => navigation.Push(new WDNavigationContext(
        "ranking", Restore, CaptureState));

    public void HideFromNavigation()
    {
        CloseDivisions();
        gameObject.SetActive(false);
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
        transform.SetAsLastSibling();
        Render();
    }

    // ============================================================
    // 02. CABEÇALHO E DIMENSÕES DO RANKING
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "<  VOLTAR", () => navigation.TryBack(),
            new Vector2(0.01f, 0.92f), new Vector2(0.13f, 0.99f));
        AddText(content, "Title", "RANKING", 30f, theme.TextoPrincipal,
            new Vector2(0.42f, 0.92f), new Vector2(0.58f, 0.99f), TextAlignmentOptions.Center);

        RectTransform side = Panel("DivisionSummary", content,
            new Vector2(0.02f, 0.10f), new Vector2(0.26f, 0.88f));
        RectTransform main = Panel("RankingTable", content,
            new Vector2(0.28f, 0.10f), new Vector2(0.98f, 0.88f));
        RenderDivisionSummary(side);
        RenderControls(main);
        RenderTable(main);
    }

    private void RenderControls(RectTransform parent)
    {
        AddButton(parent, "General", "GERAL", () => SetScope(WDRankingScope.General),
            new Vector2(0.03f, 0.88f), new Vector2(0.18f, 0.97f), query.Scope == WDRankingScope.General);
        AddButton(parent, "Season", "TEMPORADA", () => SetScope(WDRankingScope.Season),
            new Vector2(0.19f, 0.88f), new Vector2(0.36f, 0.97f), query.Scope == WDRankingScope.Season);
        if (query.Scope == WDRankingScope.Season)
        {
            AddButton(parent, "PreviousSeason", "<", () => MoveSeason(1),
                new Vector2(0.38f, 0.88f), new Vector2(0.43f, 0.97f));
            AddText(parent, "SeasonName", query.Season.DisplayName, 16f, theme.TextoPrincipal,
                new Vector2(0.43f, 0.88f), new Vector2(0.56f, 0.97f), TextAlignmentOptions.Center);
            AddButton(parent, "NextSeason", ">", () => MoveSeason(-1),
                new Vector2(0.56f, 0.88f), new Vector2(0.61f, 0.97f));
        }
        AddButton(parent, "Players", "JOGADORES", () => SetEntity(WDRankingEntity.Players),
            new Vector2(0.03f, 0.77f), new Vector2(0.20f, 0.86f), query.Entity == WDRankingEntity.Players);
        AddButton(parent, "Clans", "CLÃS", () => SetEntity(WDRankingEntity.Clans),
            new Vector2(0.21f, 0.77f), new Vector2(0.36f, 0.86f), query.Entity == WDRankingEntity.Clans);
        searchInput = AddInput(parent, "Search", query.Entity == WDRankingEntity.Players
            ? "BUSCAR NOME OU ID" : "BUSCAR CLÃ", query.Search,
            new Vector2(0.63f, 0.77f), new Vector2(0.86f, 0.86f));
        AddButton(parent, "SearchButton", "BUSCAR", ApplySearch,
            new Vector2(0.87f, 0.77f), new Vector2(0.97f, 0.86f));
    }

    private void RenderDivisionSummary(RectTransform parent)
    {
        WDProfileData local = repository.Profiles.LocalProfile;
        WDDivisionDefinition next = repository.Divisions.Ordered.FirstOrDefault(definition =>
            definition.DisplayOrder > local.DivisionDefinition.DisplayOrder);
        AddText(parent, "Heading", "SUA DIVISÃO ATUAL", 15f, theme.Acento,
            new Vector2(0.07f, 0.86f), new Vector2(0.93f, 0.95f));
        AddText(parent, "Current", $"{local.Division}\n{local.DivisionPoints:N0} PONTOS", 22f,
            theme.TextoPrincipal, new Vector2(0.07f, 0.58f), new Vector2(0.93f, 0.83f),
            TextAlignmentOptions.Center);
        AddText(parent, "Next", next == null ? "SEM PRÓXIMA DIVISÃO" : $"PRÓXIMA: {next.DisplayName}",
            14f, theme.TextoSecundario, new Vector2(0.07f, 0.45f), new Vector2(0.93f, 0.56f),
            TextAlignmentOptions.Center);
        string filter = string.IsNullOrWhiteSpace(query.DivisionId) ? "TODAS AS DIVISÕES" :
            repository.Divisions.Find(query.DivisionId)?.DisplayName ?? "TODAS AS DIVISÕES";
        AddText(parent, "Filter", $"RECORTE ATUAL\n{filter}", 14f, theme.TextoPrincipal,
            new Vector2(0.07f, 0.27f), new Vector2(0.93f, 0.42f), TextAlignmentOptions.Center);
        AddButton(parent, "AllDivisions", "VER TODAS AS DIVISÕES", OpenDivisions,
            new Vector2(0.07f, 0.12f), new Vector2(0.93f, 0.23f));
    }

    // ============================================================
    // 03. TABELAS, BUSCA E PERFIL PÚBLICO
    // ============================================================

    private void RenderTable(RectTransform parent)
    {
        WDRankingResult result = repository.Query(query);
        AddRankingHeader(parent);
        if (result.State != WDRankingResultState.Ready)
        {
            string state = result.State switch
            {
                WDRankingResultState.Loading => "CARREGANDO RANKING...",
                WDRankingResultState.Unavailable => "RANKING INDISPONÍVEL / OFFLINE.",
                WDRankingResultState.Error => "NÃO FOI POSSÍVEL CARREGAR. TENTE NOVAMENTE.",
                _ => "NENHUM RESULTADO PARA O RECORTE ATUAL."
            };
            AddText(parent, "Empty", state, 17f,
                theme.TextoSecundario, new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.58f),
                TextAlignmentOptions.Center);
            return;
        }
        int visible = Mathf.Min(7, result.Entries.Count);
        for (int i = 0; i < visible; i++)
        {
            WDRankingEntry entry = result.Entries[i];
            float top = 0.68f - i * 0.075f;
            UnityAction action = query.Entity == WDRankingEntity.Players
                ? () => openPublicProfile?.Invoke(entry.PublicPlayerId) : null;
            AddRankingRow(parent, $"Entry_{entry.EntityId}", entry, action,
                new Vector2(0.04f, top - 0.065f), new Vector2(0.96f, top),
                entry.IsLocalPlayer, false);
        }
        if (query.Entity == WDRankingEntity.Players && result.LocalPlayer != null &&
            !result.Entries.Take(visible).Contains(result.LocalPlayer))
        {
            WDRankingEntry local = result.LocalPlayer;
            AddRankingRow(parent, "LocalPinned", local,
                () => openPublicProfile?.Invoke(local.PublicPlayerId),
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.11f), true, true);
        }
        if (query.Scope == WDRankingScope.Season)
            AddText(parent, "Rewards", "RECOMPENSAS DA TEMPORADA · VALORES E FAIXAS A DEFINIR",
                12f, theme.TextoSecundario, new Vector2(0.04f, 0.00f), new Vector2(0.96f, 0.035f),
                TextAlignmentOptions.Center);
    }

    private void AddRankingHeader(RectTransform parent)
    {
        RectTransform header = WDHomeUIFactory.Rect("Columns", parent);
        header.anchorMin = new Vector2(0.04f, 0.69f);
        header.anchorMax = new Vector2(0.96f, 0.75f);
        header.offsetMin = header.offsetMax = Vector2.zero;
        AddColumnText(header, "Position", "POS.", 0.00f, 0.08f, TextAlignmentOptions.Center,
            theme.TextoSecundario, 13f);
        if (query.Entity == WDRankingEntity.Players)
        {
            AddColumnText(header, "Player", "JOGADOR / ID", 0.09f, 0.42f,
                TextAlignmentOptions.Left, theme.TextoSecundario, 13f);
            AddColumnText(header, "Clan", "CLÃ", 0.43f, 0.63f,
                TextAlignmentOptions.Left, theme.TextoSecundario, 13f);
            AddColumnText(header, "Division", "DIVISÃO", 0.64f, 0.82f,
                TextAlignmentOptions.Center, theme.TextoSecundario, 13f);
        }
        else
        {
            AddColumnText(header, "Clan", "CLÃ", 0.09f, 0.82f,
                TextAlignmentOptions.Left, theme.TextoSecundario, 13f);
        }
        AddColumnText(header, "Points", "PONTOS", 0.83f, 1.00f,
            TextAlignmentOptions.Right, theme.TextoSecundario, 13f);
    }

    private void AddRankingRow(Transform parent, string name, WDRankingEntry entry,
        UnityAction action, Vector2 min, Vector2 max, bool selected, bool pinned)
    {
        WDUIPremiumButton rowButton = AddButton(parent, name, string.Empty, action, min, max, selected);
        RectTransform row = rowButton.transform as RectTransform;
        AddColumnText(row, "Position", $"#{entry.Position}", 0.00f, 0.08f,
            TextAlignmentOptions.Center, theme.TextoPrincipal, 14f);
        if (query.Entity == WDRankingEntity.Players)
        {
            string identity = $"{entry.DisplayName} · {entry.PublicPlayerId}";
            if (pinned) identity = "SUA POSIÇÃO · " + identity;
            AddColumnText(row, "Player", identity, 0.09f, 0.42f,
                TextAlignmentOptions.Left, theme.TextoPrincipal, 14f);
            AddColumnText(row, "Clan", entry.ClanName, 0.43f, 0.63f,
                TextAlignmentOptions.Left, theme.TextoPrincipal, 14f);
            AddColumnText(row, "Division", entry.Division?.DisplayName ?? string.Empty,
                0.64f, 0.82f, TextAlignmentOptions.Center, theme.TextoPrincipal, 14f);
        }
        else
        {
            AddColumnText(row, "Clan", entry.DisplayName, 0.09f, 0.82f,
                TextAlignmentOptions.Left, theme.TextoPrincipal, 14f);
        }
        AddColumnText(row, "Points", $"{entry.Points:N0} PTS", 0.83f, 1.00f,
            TextAlignmentOptions.Right, theme.TextoPrincipal, 14f);
    }

    private void AddColumnText(Transform parent, string name, string value, float minX,
        float maxX, TextAlignmentOptions alignment, Color color, float size)
    {
        TextMeshProUGUI label = AddText(parent, name, value ?? string.Empty, size, color,
            new Vector2(minX, 0f), new Vector2(maxX, 1f), alignment);
        label.rectTransform.offsetMin = new Vector2(6f, 0f);
        label.rectTransform.offsetMax = new Vector2(-6f, 0f);
        label.raycastTarget = false;
    }

    private void ApplySearch() { query.Search = searchInput?.text; Render(); }
    private void SetScope(WDRankingScope value) { query.Scope = value; query.Search = string.Empty; Render(); }
    private void SetEntity(WDRankingEntity value) { query.Entity = value; query.Search = string.Empty; Render(); }
    private void MoveSeason(int delta)
    {
        int index = -1;
        for (int i = 0; i < repository.Seasons.Count; i++)
            if (repository.Seasons[i].Equals(query.Season)) { index = i; break; }
        int next = Mathf.Clamp(index + delta, 0, repository.Seasons.Count - 1);
        query.Season = repository.Seasons[next]; Render();
    }

    // ============================================================
    // 04. MODAL DATA-DRIVEN DE DIVISÕES
    // ============================================================

    private void OpenDivisions()
    {
        RectTransform modal = WDHomeUIFactory.Rect("RankingDivisionsModal", transform);
        WDHomeUIFactory.Stretch(modal);
        divisionsModal = modal.gameObject;
        Image blocker = divisionsModal.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.78f); blocker.raycastTarget = true;
        RectTransform panel = Panel("Panel", modal, new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.84f));
        AddText(panel, "Title", "TODAS AS DIVISÕES", 24f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.97f));
        AddButton(panel, "All", "TODAS AS DIVISÕES", () => SelectDivision(null),
            new Vector2(0.04f, 0.77f), new Vector2(0.96f, 0.86f), string.IsNullOrEmpty(query.DivisionId));
        for (int i = 0; i < repository.Divisions.Ordered.Count; i++)
        {
            WDDivisionDefinition division = repository.Divisions.Ordered[i];
            int column = i % 3; int row = i / 3;
            float left = 0.04f + column * 0.31f; float top = 0.72f - row * 0.15f;
            string suffix = division.HasOpenUpperLimit ? " · SEM LIMITE SUPERIOR" : string.Empty;
            AddButton(panel, $"Division_{division.Id}", division.DisplayName + suffix,
                () => SelectDivision(division.Id), new Vector2(left, top - 0.11f),
                new Vector2(left + 0.28f, top), query.DivisionId == division.Id);
        }
        AddButton(panel, "Close", "VOLTAR AO RANKING", CloseDivisions,
            new Vector2(0.30f, 0.04f), new Vector2(0.70f, 0.12f));
    }

    private void SelectDivision(string divisionId)
    {
        if (!string.IsNullOrEmpty(divisionId) && query.Entity == WDRankingEntity.Clans)
            query.Entity = WDRankingEntity.Players;
        query.DivisionId = divisionId;
        CloseDivisions(); Render();
    }

    private void CloseDivisions()
    {
        if (divisionsModal != null) DestroyNow(divisionsModal);
        divisionsModal = null;
    }

    // ============================================================
    // 05. COMPONENTES VISUAIS PROVISÓRIOS
    // ============================================================

    private TMP_InputField AddInput(Transform parent, string name, string placeholder,
        string value, Vector2 min, Vector2 max)
    {
        RectTransform field = Panel(name, parent, min, max);
        TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = WDHomeUIFactory.Rect("TextViewport", field);
        WDHomeUIFactory.Stretch(viewport, 10f, 3f, 10f, 3f);
        TextMeshProUGUI text = AddText(viewport, "Text", value ?? string.Empty, 15f,
            theme.TextoPrincipal, Vector2.zero, Vector2.one);
        TextMeshProUGUI hint = AddText(viewport, "Placeholder", placeholder, 15f,
            theme.TextoSecundario, Vector2.zero, Vector2.one);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine; input.text = value ?? string.Empty;
        return input;
    }

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = theme.BackgroundElevated;
        image.raycastTarget = true;
        Outline outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = theme.BorderNeutral;
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size,
        Color color, Vector2 min, Vector2 max,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    private WDUIPremiumButton AddButton(Transform parent, string name, string label,
        UnityAction action, Vector2 min, Vector2 max, bool selected = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action); button.SetSelected(selected); return button;
    }

    private static void Clear(Transform parent)
    { for (int i = parent.childCount - 1; i >= 0; i--) DestroyNow(parent.GetChild(i).gameObject); }
    private static void DestroyNow(UnityEngine.Object target)
    { if (target == null) return; if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
}

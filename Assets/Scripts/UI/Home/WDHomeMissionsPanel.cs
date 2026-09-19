using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeMissionsPanel : MonoBehaviour
{
    private WarDominionUITheme theme;
    private WDMissionRepository repository;
    private WDNavigationStack navigation;
    private RectTransform content;
    private GameObject detail;
    private int currentPage;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsDetailOpen => detail != null;
    public string CurrentDetailMissionId { get; private set; }
    public WDMissionRepository Repository => repository;

    // ============================================================
    // 01. CONSTRUÇÃO E NAVEGAÇÃO GLOBAL ÚNICA
    // ============================================================

    public void Build(WarDominionUITheme newTheme, WDMissionRepository missionRepository,
        WDNavigationStack navigationStack)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        repository = missionRepository ?? throw new ArgumentNullException(nameof(missionRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.96f); blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("MissionsContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 40f, 54f, 40f);
        gameObject.SetActive(false);
    }

    public void Open()
    {
        currentPage = 0;
        navigation.Push(new WDNavigationContext("missions", _ =>
        {
            transform.SetAsLastSibling();
            Render();
        }));
    }

    public void HideFromNavigation()
    {
        CloseDetail();
        gameObject.SetActive(false);
    }

    public bool TryHandleLocalBack()
    {
        if (!IsDetailOpen) return false;
        CloseDetail();
        return true;
    }

    // ============================================================
    // 02. COLEÇÃO, DESCOBERTAS E ESTADOS UNIVERSAIS
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "<  VOLTAR", () => navigation.TryBack(),
            new Vector2(0.01f, 0.91f), new Vector2(0.13f, 0.99f));
        AddText(content, "Title", "MISSÕES", 30f, theme.TextoPrincipal,
            new Vector2(0.38f, 0.91f), new Vector2(0.62f, 0.99f), TextAlignmentOptions.Center);

        if (repository.State != WDMissionCollectionState.Ready || repository.TotalCount == 0)
        {
            string message = repository.State switch
            {
                WDMissionCollectionState.Loading => "CARREGANDO MISSÕES...",
                WDMissionCollectionState.Error => "NÃO FOI POSSÍVEL CARREGAR · TENTAR NOVAMENTE",
                WDMissionCollectionState.Unavailable => "MISSÕES INDISPONÍVEIS / OFFLINE",
                _ => "NENHUMA MISSÃO DISPONÍVEL"
            };
            AddText(content, "UniversalState", message, 20f, theme.TextoSecundario,
                new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.62f), TextAlignmentOptions.Center);
            return;
        }

        int discovered = repository.DiscoveredCount;
        AddText(content, "Discoveries", $"DESCOBERTAS\n{discovered} / {repository.TotalCount}",
            16f, theme.Acento, new Vector2(0.03f, 0.79f), new Vector2(0.18f, 0.89f));
        RectTransform bar = Panel("DiscoveryBar", content, new Vector2(0.19f, 0.82f), new Vector2(0.70f, 0.86f));
        Image fill = WDHomeUIFactory.Image("Fill", bar, theme.Acento);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(repository.TotalCount == 0 ? 0f :
            (float)discovered / repository.TotalCount, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        AddText(content, "Collection", $"COLEÇÃO PERMANENTE · {repository.TotalCount} MISSÕES",
            14f, theme.TextoSecundario, new Vector2(0.03f, 0.72f), new Vector2(0.45f, 0.79f));

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(repository.TotalCount / 8f));
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        if (currentPage > 0)
            AddButton(content, "PreviousPage", "< ANTERIORES", () => ChangePage(-1),
                new Vector2(0.70f, 0.73f), new Vector2(0.82f, 0.79f));
        AddText(content, "Page", $"{currentPage + 1}/{pageCount}", 13f, theme.TextoSecundario,
            new Vector2(0.82f, 0.73f), new Vector2(0.88f, 0.79f), TextAlignmentOptions.Center);
        if (currentPage < pageCount - 1)
            AddButton(content, "NextPage", "PRÓXIMAS >", () => ChangePage(1),
                new Vector2(0.88f, 0.73f), new Vector2(0.98f, 0.79f));

        WDMissionPresentation[] visible = repository.PresentAll()
            .Skip(currentPage * 8).Take(8).ToArray();
        for (int i = 0; i < visible.Length; i++)
        {
            int column = i % 4; int row = i / 4;
            float left = 0.03f + column * 0.242f;
            float top = 0.70f - row * 0.30f;
            WDMissionPresentation mission = visible[i];
            string state = mission.IsHidden ? "NÃO DESCOBERTA" : mission.Status == WDMissionStatus.Completed
                ? mission.IsRepeatable ? $"CONQUISTADA ×{mission.CompletionCount}" : "CONQUISTADA"
                : $"EM PROGRESSO · {mission.Current}/{mission.Target}";
            WDUIPremiumButton card = AddButton(content, $"Mission_{mission.MissionId}", string.Empty,
                () => OpenDetail(mission.MissionId), new Vector2(left, top - 0.25f),
                new Vector2(left + 0.225f, top));
            AddText(card.transform, "MissionTitle", mission.Title, 17f, theme.TextoPrincipal,
                new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.88f), TextAlignmentOptions.Center);
            AddText(card.transform, "MissionState", mission.IsHidden ? "MISSÃO SECRETA\nNÃO DESCOBERTA" : state,
                13f, mission.IsHidden ? theme.TextoSecundario : theme.Acento,
                new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.48f), TextAlignmentOptions.Center);
        }
    }

    // ============================================================
    // 03. DETALHE INTERNO SEM NOVO NÍVEL DE NAVEGAÇÃO
    // ============================================================

    private void OpenDetail(string missionId)
    {
        CloseDetail();
        WDMissionPresentation mission = repository.Present(missionId);
        if (mission == null) return;
        CurrentDetailMissionId = missionId;
        RectTransform overlay = WDHomeUIFactory.Rect("MissionDetail", transform);
        WDHomeUIFactory.Stretch(overlay);
        detail = overlay.gameObject;
        Image veil = detail.AddComponent<Image>(); veil.color = new Color(0f, 0f, 0f, 0.80f); veil.raycastTarget = true;
        RectTransform panel = Panel("Panel", overlay, new Vector2(0.24f, 0.20f), new Vector2(0.76f, 0.80f));
        string body = mission.IsHidden
            ? "MISSÃO SECRETA\nNÃO DESCOBERTA"
            : $"{mission.Description}\n\nCONDIÇÃO\n{mission.Condition}\n\n" +
              $"PROGRESSO: {mission.Current}/{mission.Target}\nESTADO: {StatusLabel(mission)}\n" +
              (mission.IsRepeatable ? $"CONCLUSÕES: {mission.CompletionCount}\n" : string.Empty) +
              (string.IsNullOrEmpty(mission.Reward) ? string.Empty : $"RECOMPENSA: {mission.Reward}");
        AddText(panel, "DetailTitle", mission.Title, 25f, theme.TextoPrincipal,
            new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.94f), TextAlignmentOptions.Center);
        AddText(panel, "DetailBody", body, 16f, theme.TextoSecundario,
            new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.75f), TextAlignmentOptions.Center);
        AddButton(panel, "CloseDetail", "VOLTAR À COLEÇÃO", CloseDetail,
            new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.18f));
    }

    private static string StatusLabel(WDMissionPresentation mission) =>
        mission.Status == WDMissionStatus.Completed
            ? mission.IsRepeatable ? $"CONQUISTADA ×{mission.CompletionCount}" : "CONQUISTADA"
            : "EM PROGRESSO";

    private void ChangePage(int delta)
    {
        currentPage += delta;
        Render();
    }

    private void CloseDetail()
    {
        if (detail != null) DestroyNow(detail);
        detail = null; CurrentDetailMissionId = null;
    }

    // ============================================================
    // 04. COMPONENTES VISUAIS PROVISÓRIOS
    // ============================================================

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = theme.BackgroundElevated;
        Outline outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = theme.BorderNeutral;
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size,
        Color color, Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI text = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }

    private WDUIPremiumButton AddButton(Transform parent, string name, string label,
        UnityAction action, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action); return button;
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) DestroyNow(parent.GetChild(i).gameObject);
    }

    private static void DestroyNow(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}

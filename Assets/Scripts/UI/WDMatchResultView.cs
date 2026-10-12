using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDMatchResultView : MonoBehaviour
{
    // 01. Tela funcional provisória sobre as duas variantes do HUD.
    private TextMeshProUGUI title;
    private TextMeshProUGUI details;
    private RectTransform resultPanel;
    private bool leaving;

    public void Build(WarDominionUITheme theme)
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch;
        blocker.raycastTarget = true;

        resultPanel = WDUIFactory.Rect("ResultPanel", root);
        resultPanel.anchorMin = new Vector2(0.33f, 0.30f);
        resultPanel.anchorMax = new Vector2(0.67f, 0.70f);
        resultPanel.offsetMin = resultPanel.offsetMax = Vector2.zero;
        resultPanel.gameObject.AddComponent<WDUIPanel>().Build(theme, true);

        title = WDUIFactory.Text("Title", resultPanel, theme, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        SetBounds(title.rectTransform, 0.08f, 0.70f, 0.92f, 0.90f);
        WDUITextPolicies.Apply(title, WDUITextPolicy.BoundedWrap, 2);
        details = WDUIFactory.Text("Details", resultPanel, theme, theme.TypeBody,
            theme.TextoSecundario, TextAlignmentOptions.Center);
        SetBounds(details.rectTransform, 0.08f, 0.28f, 0.92f, 0.70f);
        WDUITextPolicies.Apply(details, WDUITextPolicy.BoundedWrap, 6);

        RectTransform action = WDUIFactory.Rect("Continue", resultPanel);
        action.anchorMin = new Vector2(0.20f, 0.08f);
        action.anchorMax = new Vector2(0.80f, 0.22f);
        action.offsetMin = action.offsetMax = Vector2.zero;
        action.gameObject.AddComponent<WDUIPremiumButton>().Build(
            theme, "CONTINUAR", Continue);
        gameObject.SetActive(false);
    }

    public void Present(WDMatchCompletion outcome, int totalTrophies)
    {
        if (outcome == null) return;
        bool wideResult = outcome.ModeId != "1v1";
        resultPanel.anchorMin = wideResult ? new Vector2(0.24f, 0.25f) : new Vector2(0.33f, 0.30f);
        resultPanel.anchorMax = wideResult ? new Vector2(0.76f, 0.75f) : new Vector2(0.67f, 0.70f);
        title.text = "BATALHA ENCERRADA\n" +
            (outcome.IsDraw ? "— EMPATE —" : outcome.IsLocalWinner ? "— VITÓRIA —" : "— DERROTA —");
        string placement = outcome.ModeId == "ffa" ? $"\nCOLOCAÇÃO: {outcome.Placement}º" : string.Empty;
        details.text = $"MODO: {outcome.ModeId.ToUpperInvariant()}" +
            (string.IsNullOrEmpty(outcome.SubmodeId) ? string.Empty :
                $" · {outcome.SubmodeId.ToUpperInvariant()}") + placement +
            $"\nTROFÉUS RECEBIDOS: +{outcome.LocalTrophies}" +
            $"\nTOTAL DE TROFÉUS: {totalTrophies}";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    // 02. Clique repetido não solicita duas transições de cena.
    private void Continue()
    {
        if (leaving) return;
        leaving = true;
        WDMatchSetupContext.ExitToHome();
    }

    private static void SetBounds(RectTransform rect, float left, float bottom,
        float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

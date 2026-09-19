using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDMatchResultView : MonoBehaviour
{
    // 01. Tela funcional provisória sobre as duas variantes do HUD.
    private TextMeshProUGUI title;
    private TextMeshProUGUI details;
    private bool leaving;

    public void Build(WarDominionUITheme theme)
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.82f);
        blocker.raycastTarget = true;

        RectTransform panel = WDUIFactory.Rect("ResultPanel", root);
        panel.anchorMin = new Vector2(0.30f, 0.27f);
        panel.anchorMax = new Vector2(0.70f, 0.73f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<WDUIPanel>().Build(theme, true);

        title = WDUIFactory.Text("Title", panel, theme, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        SetBounds(title.rectTransform, 0.08f, 0.70f, 0.92f, 0.90f);
        details = WDUIFactory.Text("Details", panel, theme, theme.TypeBody,
            theme.TextoSecundario, TextAlignmentOptions.Center);
        SetBounds(details.rectTransform, 0.08f, 0.28f, 0.92f, 0.70f);

        RectTransform action = WDUIFactory.Rect("Continue", panel);
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
        title.text = outcome.IsDraw ? "EMPATE" : outcome.IsLocalWinner ? "VITÓRIA" : "DERROTA";
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

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDInitialTerritorySelectionView : MonoBehaviour
{
    // ============================================================
    // 01. REFERÊNCIAS DA CONFIRMAÇÃO PROVISÓRIA
    // ============================================================

    private GameManager gameManager;
    private GameObject panel;
    private GameObject announcementBlocker;
    private TextMeshProUGUI stageText;
    private TextMeshProUGUI territoryText;
    private WDUIPremiumButton confirmButton;
    private string lastKey = string.Empty;

    public void Build(WarDominionUITheme theme, GameManager manager)
    {
        gameManager = manager;
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        announcementBlocker = CreateBlocker(root);
        panel = CreatePanel(root, theme);
        BuildContent(panel.transform, theme);
        Refresh(true);
    }

    // ============================================================
    // 02. PAINEL FUNCIONAL DA ESCOLHA 1/2 E 2/2
    // ============================================================

    private void BuildContent(Transform parent, WarDominionUITheme theme)
    {
        stageText = WDUIFactory.Text("Stage", parent, theme, theme.TypeSection,
            theme.TextoSecundario, TextAlignmentOptions.Center);
        territoryText = WDUIFactory.Text("Territory", parent, theme, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        WDUITextPolicies.Apply(stageText, WDUITextPolicy.FixedLabel);
        WDUITextPolicies.Apply(territoryText, WDUITextPolicy.SingleLineEllipsis);
        confirmButton = CreateButton(parent, theme, "CONFIRMAR",
            () => gameManager?.ConfirmarEscolhaInicialProvisoria());
        CreateButton(parent, theme, "CANCELAR",
            () => gameManager?.CancelarEscolhaInicialProvisoria());
    }

    private void Update() => Refresh(false);

    public void RefreshInputState() => Refresh(true);

    private void Refresh(bool force)
    {
        bool selection = gameManager != null && gameManager.EmSelecaoInicial;
        string territoryId = selection ? gameManager.TerritorioProvisorioInicialId : string.Empty;
        string key = $"{selection}|{gameManager?.EtapaSelecaoInicial}|{territoryId}|" +
            $"{gameManager?.BloqueandoEntradaNoRoundUm}";
        if (!force && key == lastKey)
            return;
        lastKey = key;

        panel.SetActive(selection && !string.IsNullOrEmpty(territoryId));
        announcementBlocker.SetActive(gameManager != null &&
            gameManager.BloqueandoEntradaNoRoundUm);
        if (!panel.activeSelf)
            return;

        stageText.text = $"ESCOLHA {gameManager.EtapaSelecaoInicial} DE 2";
        territoryText.text = ReadableName(territoryId);
        confirmButton.SetInteractable(gameManager.PodeConfirmarTerritorioInicial);
    }

    // ============================================================
    // 03. COMPOSIÇÃO PROVISÓRIA E BLOQUEIO DO ANÚNCIO
    // ============================================================

    private static GameObject CreateBlocker(Transform parent)
    {
        RectTransform rect = WDUIFactory.Rect("RoundOneInputBlocker", parent);
        Stretch(rect);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        rect.gameObject.SetActive(false);
        return rect.gameObject;
    }

    private static GameObject CreatePanel(Transform parent, WarDominionUITheme theme)
    {
        RectTransform rect = WDUIFactory.Rect("ConfirmationPanel", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 36f);
        rect.sizeDelta = new Vector2(620f, 160f);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = theme.SuperficieSecundaria;
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;
        return rect.gameObject;
    }

    private static WDUIPremiumButton CreateButton(Transform parent,
        WarDominionUITheme theme, string label, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(label, parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        return button;
    }

    private static string ReadableName(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (i > 0 && char.IsUpper(current) && char.IsLower(value[i - 1]))
                result.Append(' ');
            result.Append(current);
        }
        return result.ToString().ToUpperInvariant();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

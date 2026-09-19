using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WeaponResolutionPresenter : MonoBehaviour, IResolutionVisualPresenter
{
    private WarDominionUITheme theme;

    public void Configure(WarDominionUITheme value) => theme = value;

    public bool Supports(ResolutionVisualEventType type) =>
        type == ResolutionVisualEventType.Card;

    public IEnumerator Present(ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        if (context?.Overlay == null || theme == null)
            yield break;
        string[] parts = (visualEvent.ContentId ?? string.Empty).Split('|');
        string weapon = parts.Length > 0 ? parts[0] : "ARMA";
        string status = parts.Length > 1 ? parts[1] : string.Empty;
        bool failed = status.Contains("Blocked") || status.Contains("Invalid") ||
            status.Contains("Superseded") || status.Contains("NoValid");
        bool intercepted = status.Contains("Intercepted");

        RectTransform card = WDUIFactory.Rect("WeaponResolutionCard", context.Overlay);
        card.anchorMin = card.anchorMax = new Vector2(0.78f, 0.78f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(330f, 170f);
        Image background = card.gameObject.AddComponent<Image>();
        background.color = failed ? new Color(0.08f, 0.08f, 0.09f, 0.96f) :
            new Color(0.02f, 0.02f, 0.025f, 0.96f);
        TextMeshProUGUI label = WDUIFactory.Text("Label", card, theme,
            theme.TypeSection, failed ? theme.Danger : theme.TextoPrincipal,
            TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(12f, 12f);
        label.rectTransform.offsetMax = new Vector2(-12f, -12f);
        string icon = failed ? "X" : intercepted ? "DEFESA" : "OK";
        label.text = $"{weapon}\n{icon}\n{status.ToUpperInvariant()}";
        yield return context.Wait(0.8f);
        Destroy(card.gameObject);
    }
}

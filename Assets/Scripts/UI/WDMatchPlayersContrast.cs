using UnityEngine;
using UnityEngine.UI;

public sealed class WDMatchPlayersContrast : MonoBehaviour
{
    // 01. Apresentação passiva: mede apenas entries existentes, sem consultar/criar jogadores.
    private RectTransform content;
    private const float Padding = 8f;

    public void Bind(RectTransform entries)
    {
        content = entries;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    // 02. Mesmos anchors da lista na Safe Area; padding não move avatares ou identidade.
    public void Refresh()
    {
        if (content == null) return;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        int count = 0;
        float height = 0;
        foreach (Transform child in content)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            var entry = child as RectTransform;
            var element = child.GetComponent<LayoutElement>();
            if (entry == null || (element != null && element.ignoreLayout)) continue;
            height += LayoutUtility.GetPreferredHeight(entry);
            count++;
        }
        if (count > 1) height += (count - 1) * layout.spacing;
        // Mantém a altura preferida das linhas: o layout não comprime listas maiores em 282 px.
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        var rect = (RectTransform)transform;
        rect.anchorMin = content.anchorMin;
        rect.anchorMax = content.anchorMax;
        rect.pivot = content.pivot;
        rect.anchoredPosition = content.anchoredPosition + new Vector2(Padding, Padding);
        rect.sizeDelta = new Vector2(content.sizeDelta.x + 2 * Padding, height + 2 * Padding);
        // Não desativa o componente: lista vazia pode receber entries no snapshot seguinte.
        foreach (var graphic in GetComponentsInChildren<Graphic>()) graphic.enabled = count > 0;
    }
}

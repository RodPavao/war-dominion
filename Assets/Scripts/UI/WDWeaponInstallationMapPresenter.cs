using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDWeaponInstallationMapPresenter : MonoBehaviour
{
    private RectTransform overlay;
    private WarDominionUITheme theme;
    private WDWeaponMatchUIController weaponUI;
    private RectTransform content;
    private string snapshotKey = string.Empty;

    public void Configure(RectTransform targetOverlay, WarDominionUITheme value,
        WDWeaponMatchUIController controller)
    {
        overlay = targetOverlay;
        theme = value;
        weaponUI = controller;
        content = WDUIFactory.Rect("VisibleWeaponInstallations", overlay);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;
        StartCoroutine(RefreshLoop());
    }

    private IEnumerator RefreshLoop()
    {
        var wait = new WaitForSecondsRealtime(0.25f);
        while (enabled)
        {
            RefreshVisibleInstallations();
            yield return wait;
        }
    }

    private void RefreshVisibleInstallations()
    {
        if (weaponUI == null || content == null || MapaAtivo.Instance == null)
            return;
        WDWeaponHudInstallationState[] visible = weaponUI.GetVisibleInstallations().ToArray();
        string key = string.Join("|", visible.Select(item =>
            $"{item.InstallationId}:{item.TerritoryId}:{item.Charges}:{item.Duration}:{item.Barrier}:{item.IsDisabled}:{item.IsAbandoned}"));
        if (key == snapshotKey)
            return;
        snapshotKey = key;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        Camera camera = Camera.main;
        foreach (WDWeaponHudInstallationState installation in visible)
        {
            if (!MapaAtivo.Instance.TentarObterTerritorio(installation.TerritoryId,
                out TerritorioClique territory) || camera == null ||
                !MapOverlayProjection.TryWorldToOverlay(content, territory.transform.position,
                    camera, out Vector2 position))
                continue;
            RectTransform badge = WDUIFactory.Rect("Installation_" + installation.InstallationId,
                content);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.sizeDelta = new Vector2(118f, 34f);
            badge.anchoredPosition = position + new Vector2(0f, 32f);
            Image background = badge.gameObject.AddComponent<Image>();
            background.color = installation.IsAbandoned || installation.IsDisabled
                ? new Color(0.16f, 0.16f, 0.17f, 0.88f)
                : new Color(0.02f, 0.02f, 0.025f, 0.9f);
            TextMeshProUGUI label = WDUIFactory.Text("Label", badge, theme,
                theme.TypeMicro, installation.IsDisabled ? theme.Danger : theme.TextoPrincipal,
                TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            int remaining = installation.Charges > 0 ? installation.Charges :
                installation.Duration > 0 ? installation.Duration : installation.Barrier;
            label.text = remaining > 0 ? $"{installation.DisplayName}  {remaining}" :
                installation.DisplayName;
        }
    }
}

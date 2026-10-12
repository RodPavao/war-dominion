using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Modal de parâmetros da Preparation aprovado 155:65, não um Hub de Ação.
// Transporte Aéreo não é instalação persistente e não possui Hub de Ação próprio.
// A escolha visual futura é de um elemento transportável elegível como parâmetro
// da própria ação; não seleciona instalação territorial previamente implantada.
// O contrato ConfirmTransport permanece intacto.
public sealed class WDMatchSupportTransportView : MonoBehaviour
{
    private WDMatchSupportView owner;
    private WDWeaponMatchUIController weapons;
    private RectTransform layer;
    private TextMeshProUGUI origin, destination, amount, capacity;
    private Button minus, plus, confirm;
    private Button installationSelection;
    private TextMeshProUGUI installationValue;
    private RectTransform installationOptions;
    private string selectedInstallationId = string.Empty;
    private static readonly Color Gold = WDMatchSupportView.Gold;
    private static readonly Color White = WDMatchSupportView.White;
    private static readonly Color Muted = new Color32(138, 143, 152, 255);
    private static readonly Color Border = new Color(197f / 255, 155f / 255, 99f / 255, .2f);
    private static readonly Color Card = new Color(11f / 255, 14f / 255, 18f / 255, .8f);
    private static readonly Color Inner = new Color(10f / 255, 16f / 255, 24f / 255, .8f);
    private readonly Vector3[] corners = new Vector3[4];
    private readonly System.Collections.Generic.List<Sprite> icons = new();

    public void Build(WDMatchSupportView parent)
    {
        owner = parent;
        layer = WDMatchSupportView.Rect("SupportTransport", transform, 0, 0, 0, 0);
        layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        layer.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .55f);
        layer.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        var panel = owner.Surface("Panel", layer, 0, 0, 760, 726,
            new Color(7f / 255, 17f / 255, 29f / 255, .8f), 24, Gold);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Text(panel, "Brand", "WAR DOMINION", 28, 28, 300, 15, 12, Gold);
        Text(panel, "Title", "TRANSPORTE AÉREO", 28, 49, 480, 34, 28, White);
        var chip = owner.Surface("RouteStatus", panel, 587, 40, 145, 31, Inner, 15, Border);
        Text(chip, "Label", "ROTA SELECIONADA", 12, 8, 121, 15, 12, Gold);
        var route = owner.Surface("Route", panel, 28, 107, 704, 144, Card, 18, Border);
        Text(route, "Label", "ROTA", 20, 20, 300, 15, 12, Gold);
        var from = owner.Surface("Origin", route, 20, 51, 220, 73, Inner, 14, Border);
        Text(from, "Label", "ORIGEM", 16, 16, 188, 13, 11, Muted);
        origin = Text(from, "Value", "", 16, 35, 188, 22, 18, White);
        WDUITextPolicies.Apply(origin, WDUITextPolicy.SingleLineEllipsis);
        var arrow = owner.Surface("Arrow", route, 256, 65.5f, 44, 44, Gold, 22, Color.clear);
        Icon(arrow, "ArrowRight", 13, 13);
        var to = owner.Surface("Destination", route, 316, 51, 220, 73, Inner, 14, Border);
        Text(to, "Label", "DESTINO", 16, 16, 188, 13, 11, Muted);
        destination = Text(to, "Value", "", 16, 35, 188, 22, 18, White);
        WDUITextPolicies.Apply(destination, WDUITextPolicy.SingleLineEllipsis);
        var troops = owner.Surface("Troops", panel, 28, 275, 704, 182, Card, 18, Border);
        Text(troops, "Label", "TROPAS A TRANSPORTAR", 20, 20, 500, 15, 12, Gold);
        minus = Control(troops, "Minus", "−", 20, 51, 56, () => Change(-1));
        var value = owner.Surface("Value", troops, 88, 51, 528, 56, Inner, 16, Border);
        amount = Text(value, "Amount", "", 0, 0, 528, 56, 28, White);
        amount.alignment = TextAlignmentOptions.Center;
        plus = Control(troops, "Plus", "+", 628, 51, 56, () => Change(1));
        var limit = owner.Surface("Capacity", troops, 20, 121, 664, 41, Inner, 14, Border);
        Text(limit, "Label", "CAPACIDADE DE TROPAS", 14, 12, 350, 17, 13, Muted);
        capacity = Text(limit, "Value", "", 364, 12, 286, 17, 14, White);
        capacity.alignment = TextAlignmentOptions.MidlineRight;
        capacity.enableAutoSizing = true;
        capacity.fontSizeMin = 11f;
        capacity.fontSizeMax = 14f;
        capacity.overflowMode = TextOverflowModes.Truncate;
        var installation = owner.Surface("Installation", panel, 28, 481, 704, 137, Card, 18, Border);
        Text(installation, "Label", "TRANSPORTAR INSTALAÇÃO", 20, 25, 470, 15, 12, Gold);
        var optional = owner.Surface("OptionalChip", installation, 598, 20, 86, 25, Inner, 12, Border);
        Text(optional, "Optional", "OPCIONAL", 10, 6, 66, 13, 11, Muted);
        var dropdown = owner.Surface("InstallationSelection", installation, 20, 61, 664, 56, Inner, 16, Border);
        installationSelection = dropdown.gameObject.AddComponent<Button>();
        installationSelection.transition = Selectable.Transition.None;
        installationSelection.onClick.AddListener(ToggleInstallationOptions);
        installationValue = Text(dropdown, "Value", "NENHUMA", 16, 16, 600, 24, 16, White);
        WDUITextPolicies.Apply(installationValue, WDUITextPolicy.SingleLineEllipsis);
        Icon(dropdown, "ChevronDown", 630, 19);
        installationOptions = owner.Surface("InstallationOptions", panel,
            48, 530, 664, 0, new Color(7f / 255, 17f / 255, 29f / 255, .98f),
            14, Gold);
        installationOptions.gameObject.SetActive(false);
        Control(panel, "Cancel", "CANCELAR", 28, 642, 345,
            () => { weapons?.CancelSelection(); owner.Changed(); });
        confirm = Control(panel, "Confirm", "CONFIRMAR TRANSPORTE", 389, 642, 343,
            () => { weapons?.ConfirmTransport(weapons.TransportTroops, selectedInstallationId); owner.Changed(); }, true);
        layer.gameObject.SetActive(false);
    }

    private void Change(int delta) { weapons?.ChangeTransportTroops(delta); owner.Changed(); }

    public void Refresh(WDWeaponMatchUIController controller, bool suppressed)
    {
        weapons = controller;
        bool visible = !suppressed && weapons?.CanProgramSupport == true &&
            weapons.Stage == WDWeaponHudSelectionStage.Parameters &&
            weapons.SelectedWeaponId == WDSupportWeaponIds.AirTransport;
        if (layer.gameObject.activeSelf && !visible)
        {
            selectedInstallationId = string.Empty;
            installationOptions.gameObject.SetActive(false);
            owner.ConsumeThisFrame();
        }
        layer.gameObject.SetActive(visible);
        if (!visible) return;
        origin.text = weapons.CurrentOriginId;
        destination.text = weapons.CurrentTargetId;
        amount.text = weapons.TransportTroops.ToString();
        capacity.text = $"{weapons.TransportTroops} / {weapons.MaximumTransportTroops} DE CAPACIDADE";
        minus.interactable = weapons.TransportTroops > 1;
        plus.interactable = weapons.TransportTroops < weapons.MaximumTransportTroops;
        confirm.interactable = weapons.TransportTroops >= 1 && weapons.TransportTroops <= weapons.MaximumTransportTroops;
        IReadOnlyList<WDWeaponHudInstallationState> eligible =
            weapons.GetEligibleTransportInstallations();
        WDWeaponHudInstallationState selected = eligible.FirstOrDefault(item =>
            item.InstallationId == selectedInstallationId);
        if (selected == null) selectedInstallationId = string.Empty;
        installationValue.text = selected?.DisplayName?.ToUpperInvariant() ?? "NENHUMA";
        installationSelection.interactable = eligible.Count > 0;
        if (eligible.Count == 0)
            installationOptions.gameObject.SetActive(false);
        // Captura também a margem fora da Safe Area; painel continua centralizado nela.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(corners);
        RectTransform parent = (RectTransform)transform;
        Vector3 min = parent.InverseTransformPoint(corners[0]);
        Vector3 max = parent.InverseTransformPoint(corners[2]);
        layer.offsetMin = (Vector2)min - parent.rect.min;
        layer.offsetMax = (Vector2)max - parent.rect.max;
        ((RectTransform)layer.Find("Panel")).position = parent.TransformPoint(parent.rect.center);
    }

    private void ToggleInstallationOptions()
    {
        if (weapons == null) return;
        IReadOnlyList<WDWeaponHudInstallationState> eligible =
            weapons.GetEligibleTransportInstallations();
        if (eligible.Count == 0 || installationOptions.gameObject.activeSelf)
        {
            installationOptions.gameObject.SetActive(false);
            return;
        }
        RebuildInstallationOptions(eligible);
        owner.ConsumeThisFrame();
    }

    private void RebuildInstallationOptions(
        IReadOnlyList<WDWeaponHudInstallationState> eligible)
    {
        for (int i = installationOptions.childCount - 1; i >= 0; i--)
        {
            GameObject child = installationOptions.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        const float rowHeight = 46f;
        int rowCount = eligible.Count + 1;
        float height = rowCount * rowHeight + 8f;
        installationOptions.sizeDelta = new Vector2(664f, height);
        installationOptions.anchoredPosition = new Vector2(48f, -(537f - height));

        AddInstallationOption("NENHUMA", string.Empty, 4f, rowHeight);
        for (int i = 0; i < eligible.Count; i++)
            AddInstallationOption(eligible[i].DisplayName.ToUpperInvariant(),
                eligible[i].InstallationId, 4f + (i + 1) * rowHeight, rowHeight);
        installationOptions.SetAsLastSibling();
        installationOptions.gameObject.SetActive(true);
    }

    private void AddInstallationOption(string label, string installationId,
        float y, float height)
    {
        RectTransform row = owner.Surface("Option_" +
            (string.IsNullOrEmpty(installationId) ? "None" : installationId),
            installationOptions, 4, y, 656, height - 2, Inner, 10, Border);
        Button button = row.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            selectedInstallationId = installationId;
            installationValue.text = label;
            installationOptions.gameObject.SetActive(false);
            owner.ConsumeThisFrame();
            owner.Changed();
        });
        Text(row, "Label", label, 14, 0, 628, height - 2, 15, White);
    }

    private TextMeshProUGUI Text(Transform parent, string name, string text,
        float x, float y, float w, float h, float size, Color color)
    {
        var label = owner.Label(parent, name, text, x, y, w, h, size, color, true);
        if (size == 11) label.font = owner.Font("Inter-Regular");
        else if (size == 13 || (size == 16 && name == "Value") || parent.name == "RouteStatus")
            label.font = owner.Font("Inter-SemiBold");
        return label;
    }

    private Button Control(Transform parent, string name, string title,
        float x, float y, float width, System.Action callback, bool gold = false)
    {
        var rect = owner.Surface(name, parent, x, y, width, 56, gold ? Gold : Inner, 16, Border);
        var button = rect.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { owner.ConsumeThisFrame(); callback(); });
        Text(rect, "Label", title, 0, 0, width, 56, width == 56 ? 28 : 16,
            gold ? new Color32(7, 17, 29, 255) : White).alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void Icon(Transform parent, string name, float x, float y)
    {
        Texture2D texture = Resources.Load<Texture2D>("UI/Match/Support/" + name);
        if (texture == null) return;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
        icons.Add(sprite);
        Image image = WDMatchSupportView.Rect(name, parent, x, y, 18, 18).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in icons)
            if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
    }
}

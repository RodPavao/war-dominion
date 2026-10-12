using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Laboratório manual e isolado para comparar o estado serializado do Arsenal V2
/// com as mutações feitas pelas APIs visuais existentes.
/// </summary>
[DisallowMultipleComponent]
public sealed class WDArmoryV2VisualIsolationController : MonoBehaviour
{
    private const string LogPrefix = "[WD-ARMORY-VISUAL-ISOLATION]";
    private const string SettingsPath = "UI/ArmoryV2/WDArmoryV2VisualSettings";

    private WDArmoryScreenV2View view;
    private WDArmoryV2VisualSettings settings;
    private int currentStage;
    private string status = "ETAPA 0 — PREFAB PURO";

    // ============================================================
    // 01. INICIALIZAÇÃO SEM PRESENTER OU REFRESH
    // ============================================================
    private void Awake()
    {
        view = FindAnyObjectByType<WDArmoryScreenV2View>();
        settings = Resources.Load<WDArmoryV2VisualSettings>(SettingsPath);
        if (view == null)
        {
            status = "ERRO: WDArmoryScreenV2View não encontrado";
            Debug.LogError($"{LogPrefix} {status}");
            return;
        }

        LogFullState("ETAPA 0", "Prefab serializado; nenhum método visual executado pelo laboratório");
    }

    // ============================================================
    // 02. CONTROLES MANUAIS: UMA ÚNICA ETAPA POR CLIQUE
    // ============================================================
    private void OnGUI()
    {
        const float width = 340f;
        GUILayout.BeginArea(new Rect(18f, 18f, width, 286f), GUI.skin.box);
        GUILayout.Label("ARSENAL V2 · ISOLAMENTO VISUAL");
        GUILayout.Label(status);

        if (GUILayout.Button("ETAPA 0 · Recarregar Prefab puro")) ReloadPurePrefab();
        GUI.enabled = currentStage == 0 && view != null && settings != null;
        if (GUILayout.Button("ETAPA 1 · Aplicar configurações")) RunStage1();
        GUI.enabled = currentStage == 1 && view != null;
        if (GUILayout.Button("ETAPA 2 · Aplicar dados aos slots")) RunStage2();
        GUI.enabled = currentStage == 2 && view != null;
        if (GUILayout.Button("ETAPA 3 · Aplicar dados aos cards")) RunStage3();
        GUI.enabled = currentStage >= 2 && view != null;
        if (GUILayout.Button("ETAPA 2 · Alternar Outline selecionados")) ToggleSelectedSlotOutlines();
        GUI.enabled = true;
        if (GUILayout.Button("ETAPA 4 · Informar limitação")) ReportStage4Limitation();
        if (GUILayout.Button("Registrar estado atual")) LogFullState($"ETAPA {currentStage}", "Captura manual");
        GUILayout.EndArea();
    }

    private void ReloadPurePrefab()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void RunStage1()
    {
        RunMeasuredStage(1, "WDArmoryScreenV2View.ApplyVisualSettings", () => view.ApplyVisualSettings(settings));
    }

    private void RunStage2()
    {
        Texture sample = FindSampleArtwork();
        RunMeasuredStage(2, "WDArmorySlotView.SetEmpty/SetWeapon -> ApplySelection", () =>
        {
            if (view.MainSlots.Length > 0) view.MainSlots[0]?.SetWeapon("ARMA PRINCIPAL DE TESTE", 25, sample, false);
            if (view.MainSlots.Length > 1) view.MainSlots[1]?.SetEmpty(false, true);
            if (view.SupportSlots.Length > 0) view.SupportSlots[0]?.SetWeapon("ARMA APOIO DE TESTE", 15, sample, false);
            if (view.SupportSlots.Length > 1) view.SupportSlots[1]?.SetEmpty(true, true);
        });
    }

    private void RunStage3()
    {
        Texture sample = FindSampleArtwork();
        RunMeasuredStage(3, "WDArmoryWeaponCardView.SetData -> ApplyState", () =>
        {
            if (view.CatalogCards.Length > 0)
                view.CatalogCards[0]?.SetData(new WDArmoryWeaponCardView.ViewData(
                    "isolation_available", "Card disponível", "Especial", 1, 20, 25, 100,
                    WDArmoryWeaponCardView.VisualState.Available), sample);
            if (view.CatalogCards.Length > 1)
                view.CatalogCards[1]?.SetData(new WDArmoryWeaponCardView.ViewData(
                    "isolation_selected", "Card selecionado", "Apoio", 2, 15, 60, 100,
                    WDArmoryWeaponCardView.VisualState.Selected), sample);
            if (view.CatalogCards.Length > 2)
                view.CatalogCards[2]?.SetData(new WDArmoryWeaponCardView.ViewData(
                    "isolation_equipped", "Card equipado", "Tática", 3, 25, 100, 100,
                    WDArmoryWeaponCardView.VisualState.Equipped), sample);
            if (view.CatalogCards.Length > 3)
                view.CatalogCards[3]?.SetData(new WDArmoryWeaponCardView.ViewData(
                    "isolation_locked", "Card bloqueado", "Estratégica", 0, 30, 0, 100,
                    WDArmoryWeaponCardView.VisualState.Locked), sample);
        });
    }

    private void ReportStage4Limitation()
    {
        status = "ETAPA 4 indisponível neste laboratório isolado";
        Debug.LogWarning($"{LogPrefix} ETAPA 4 NÃO EXECUTADA. O Presenter funcional depende do contexto da Home, " +
            "perfil e serviços privados. Conectá-lo aqui exigiria outro Presenter ou alteração do fluxo principal, ambos fora do escopo.");
    }

    // ============================================================
    // 03. MEDIÇÃO ANTES/DEPOIS E ATRIBUIÇÃO DO MÉTODO RESPONSÁVEL
    // ============================================================
    private void RunMeasuredStage(int stage, string method, Action mutation)
    {
        Dictionary<string, string> before = CaptureState();
        mutation();
        Canvas.ForceUpdateCanvases();
        Dictionary<string, string> after = CaptureState();

        currentStage = stage;
        status = $"ETAPA {stage} concluída; consultar Console";
        LogDiff(stage, method, before, after);
    }

    private Dictionary<string, string> CaptureState()
    {
        Dictionary<string, string> state = new(StringComparer.Ordinal);
        CaptureGroup(state, "MAIN", view.MainSlots);
        CaptureGroup(state, "APOIO", view.SupportSlots);
        CaptureGroup(state, "CARD", view.CatalogCards);
        CaptureUpgradeButton(state, view.UpgradeButton);
        return state;
    }

    private static void CaptureGroup<T>(Dictionary<string, string> state, string group, T[] items) where T : Component
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            T item = items[i];
            if (item == null) continue;
            string root = $"{group}[{i}]/{HierarchyPath(item.transform)}";

            foreach (Image image in item.GetComponentsInChildren<Image>(true))
            {
                string key = $"{root}/{HierarchyPath(image.transform, item.transform)}:Image";
                state[key] = GraphicText(image) + $"; sprite={AssetText(image.sprite)}";
            }
            foreach (RawImage image in item.GetComponentsInChildren<RawImage>(true))
            {
                string key = $"{root}/{HierarchyPath(image.transform, item.transform)}:RawImage";
                state[key] = GraphicText(image) + $"; texture={AssetText(image.texture)}";
            }
            foreach (Outline outline in item.GetComponentsInChildren<Outline>(true))
            {
                string key = $"{root}/{HierarchyPath(outline.transform, item.transform)}:Outline";
                state[key] = $"active={outline.gameObject.activeInHierarchy}; effectColor={ColorText(outline.effectColor)}; " +
                    $"effectDistance={outline.effectDistance}; useGraphicAlpha={outline.useGraphicAlpha}";
            }
            foreach (Selectable selectable in item.GetComponentsInChildren<Selectable>(true))
            {
                string key = $"{root}/{HierarchyPath(selectable.transform, item.transform)}:{selectable.GetType().Name}";
                ColorBlock colors = selectable.colors;
                state[key] = $"active={selectable.gameObject.activeInHierarchy}; interactable={selectable.interactable}; " +
                    $"transition={selectable.transition}; target={HierarchyPath(selectable.targetGraphic?.transform, item.transform)}; " +
                    $"normal={ColorText(colors.normalColor)}; highlighted={ColorText(colors.highlightedColor)}; " +
                    $"pressed={ColorText(colors.pressedColor)}; selected={ColorText(colors.selectedColor)}; " +
                    $"disabled={ColorText(colors.disabledColor)}; multiplier={colors.colorMultiplier:F3}; fade={colors.fadeDuration:F3}";
            }
            foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
            {
                string key = $"{root}/{HierarchyPath(child, item.transform)}:GameObject";
                state[key] = $"activeSelf={child.gameObject.activeSelf}; activeInHierarchy={child.gameObject.activeInHierarchy}";
            }
        }
    }

    // ============================================================
    // 04. COR EFETIVA DO GRAPHIC E CONTROLE EVOLUIR
    // ============================================================
    private static string GraphicText(Graphic graphic)
    {
        CanvasRenderer renderer = graphic.canvasRenderer;
        string maskable = graphic is MaskableGraphic masked ? masked.maskable.ToString() : "n/a";
        return $"active={graphic.gameObject.activeInHierarchy}; color={ColorText(graphic.color)}; " +
            $"rendererColor={ColorText(renderer.GetColor())}; rendererAlpha={renderer.GetAlpha():F4}; " +
            $"material={AssetText(graphic.material)}; raycast={graphic.raycastTarget}; maskable={maskable}";
    }

    private static void CaptureUpgradeButton(Dictionary<string, string> state, Button button)
    {
        if (button == null) return;
        string root = $"UPGRADE/{HierarchyPath(button.transform)}";
        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
        {
            string key = $"{root}/{HierarchyPath(graphic.transform, button.transform)}:{graphic.GetType().Name}";
            state[key] = GraphicText(graphic);
        }

        ColorBlock colors = button.colors;
        state[$"{root}:Button"] = $"active={button.gameObject.activeInHierarchy}; interactable={button.interactable}; " +
            $"transition={button.transition}; target={HierarchyPath(button.targetGraphic?.transform, button.transform)}; " +
            $"normal={ColorText(colors.normalColor)}; highlighted={ColorText(colors.highlightedColor)}; " +
            $"pressed={ColorText(colors.pressedColor)}; selected={ColorText(colors.selectedColor)}; " +
            $"disabled={ColorText(colors.disabledColor)}; multiplier={colors.colorMultiplier:F3}; fade={colors.fadeDuration:F3}";
    }

    // ============================================================
    // 05. TESTE DECISIVO DO OUTLINE DOS SLOTS SELECIONADOS
    // ============================================================
    private void ToggleSelectedSlotOutlines()
    {
        WDArmorySlotView main = view.MainSlots != null && view.MainSlots.Length > 1 ? view.MainSlots[1] : null;
        WDArmorySlotView support = view.SupportSlots != null && view.SupportSlots.Length > 1 ? view.SupportSlots[1] : null;
        Outline mainOutline = main != null ? main.GetComponent<Outline>() : null;
        Outline supportOutline = support != null ? support.GetComponent<Outline>() : null;
        bool enable = !((mainOutline != null && mainOutline.enabled) ||
            (supportOutline != null && supportOutline.enabled));

        StringBuilder log = new();
        log.AppendLine($"{LogPrefix} ETAPA 2 · TOGGLE OUTLINE => {(enable ? "LIGADO" : "DESLIGADO")}");
        ToggleOutline(main, mainOutline, enable, "MAIN[1]", log);
        ToggleOutline(support, supportOutline, enable, "APOIO[1]", log);
        Canvas.ForceUpdateCanvases();
        Debug.Log(log.ToString());
    }

    private static void ToggleOutline(WDArmorySlotView slot, Outline outline, bool enabled,
        string label, StringBuilder log)
    {
        if (slot == null || outline == null)
        {
            log.AppendLine($"- {label}: slot ou Outline ausente");
            return;
        }

        Image image = slot.GetComponent<Image>();
        Color imageBefore = image != null ? image.color : default;
        Color rendererBefore = image != null ? image.canvasRenderer.GetColor() : default;
        float alphaBefore = image != null ? image.canvasRenderer.GetAlpha() : 0f;
        bool outlineBefore = outline.enabled;
        outline.enabled = enabled;

        log.AppendLine($"- {label}: Outline {outlineBefore} -> {outline.enabled}; " +
            $"effectColor={ColorText(outline.effectColor)}; effectDistance={outline.effectDistance}; " +
            $"useGraphicAlpha={outline.useGraphicAlpha}");
        log.AppendLine($"  Image.color {ColorText(imageBefore)} -> {ColorText(image != null ? image.color : default)}");
        log.AppendLine($"  CanvasRenderer.color {ColorText(rendererBefore)} -> " +
            $"{ColorText(image != null ? image.canvasRenderer.GetColor() : default)}; " +
            $"alpha {alphaBefore:F4} -> {(image != null ? image.canvasRenderer.GetAlpha() : 0f):F4}");
    }

    private void LogDiff(int stage, string method, Dictionary<string, string> before, Dictionary<string, string> after)
    {
        StringBuilder log = new();
        log.AppendLine($"{LogPrefix} ETAPA {stage}");
        log.AppendLine($"Método responsável: {method}");
        int changes = 0;
        foreach (KeyValuePair<string, string> entry in after)
        {
            before.TryGetValue(entry.Key, out string previous);
            if (string.Equals(previous, entry.Value, StringComparison.Ordinal)) continue;
            changes++;
            log.AppendLine($"- {entry.Key}");
            log.AppendLine($"  ANTES: {previous ?? "<ausente>"}");
            log.AppendLine($"  DEPOIS: {entry.Value}");
        }
        log.AppendLine($"Total de propriedades/estados alterados: {changes}");
        Debug.Log(log.ToString());
    }

    private void LogFullState(string stage, string method)
    {
        if (view == null) return;
        StringBuilder log = new();
        log.AppendLine($"{LogPrefix} {stage}");
        log.AppendLine($"Origem: {method}");
        foreach (KeyValuePair<string, string> entry in CaptureState())
            if (entry.Key.EndsWith(":Image", StringComparison.Ordinal) ||
                entry.Key.EndsWith(":RawImage", StringComparison.Ordinal) ||
                entry.Key.EndsWith(":Outline", StringComparison.Ordinal))
                log.AppendLine($"- {entry.Key} => {entry.Value}");
        Debug.Log(log.ToString());
    }

    // ============================================================
    // 06. IDENTIFICAÇÃO DE ASSET E CAMINHO SEM ALTERAR REFERÊNCIAS
    // ============================================================
    private Texture FindSampleArtwork()
    {
        foreach (WDArmoryWeaponCardView card in view.CatalogCards)
        {
            RawImage image = GetPrivateField<RawImage>(card, "artwork");
            if (image != null && image.texture != null) return image.texture;
        }
        return null;
    }

    private static T GetPrivateField<T>(object owner, string name) where T : class
    {
        FieldInfo field = owner?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        while (field == null && owner?.GetType().BaseType != null)
        {
            Type type = owner.GetType().BaseType;
            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            break;
        }
        return field?.GetValue(owner) as T;
    }

    private static string AssetText(UnityEngine.Object asset)
    {
        if (asset == null) return "<null>";
#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(asset);
        return $"{asset.name} ({asset.GetType().Name}) path={path}";
#else
        return $"{asset.name} ({asset.GetType().Name}) path=<disponível somente no Editor>";
#endif
    }

    private static string ColorText(Color color) =>
        $"RGBA({color.r:F4},{color.g:F4},{color.b:F4},{color.a:F4}) #{ColorUtility.ToHtmlStringRGBA(color)}";

    private static string HierarchyPath(Transform target, Transform relativeTo = null)
    {
        if (target == null) return "<null>";
        Stack<string> names = new();
        Transform current = target;
        while (current != null && current != relativeTo)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }
}

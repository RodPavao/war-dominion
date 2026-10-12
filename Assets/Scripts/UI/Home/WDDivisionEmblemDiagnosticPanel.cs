using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Ferramenta temporária de inspeção. Não participa da navegação comercial.
public sealed class WDDivisionEmblemDiagnosticPanel : MonoBehaviour
{
    private const float MedalSide = 160f;
    private const float OffsetStep = 2f;
    private const float FineOffsetStep = .5f;
    private const float ScaleStep = .02f;
    private const float FineScaleStep = .005f;
    private const float MinimumScale = .1f;
    private GameObject overlay;
    private readonly List<CalibrationItem> items = new();
    private CalibrationItem selected;

    private sealed class CalibrationItem
    {
        public WDDivisionDefinition Division;
        public RectTransform Symbol;
        public TextMeshProUGUI Values;
        public Image Surface;
        public float Scale = 1f;
        public float OffsetX;
        public float OffsetY;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (FindAnyObjectByType<WDDivisionEmblemDiagnosticPanel>() != null)
            return;
        var host = new GameObject("WD Division Emblem Diagnostic [F8]");
        DontDestroyOnLoad(host);
        host.AddComponent<WDDivisionEmblemDiagnosticPanel>();
#endif
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f8Key.wasPressedThisFrame)
        {
            if (overlay == null)
                Build();
            else
                overlay.SetActive(!overlay.activeSelf);
        }

        if (overlay == null || !overlay.activeSelf)
            return;

        if (keyboard.cKey.wasPressedThisFrame)
            PrintCalibration();

        if (selected == null)
            return;

        bool fine = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        float offsetStep = fine ? FineOffsetStep : OffsetStep;
        float scaleStep = fine ? FineScaleStep : ScaleStep;
        bool changed = false;

        if (keyboard.aKey.wasPressedThisFrame) { selected.OffsetX -= offsetStep; changed = true; }
        if (keyboard.dKey.wasPressedThisFrame) { selected.OffsetX += offsetStep; changed = true; }
        if (keyboard.wKey.wasPressedThisFrame) { selected.OffsetY += offsetStep; changed = true; }
        if (keyboard.sKey.wasPressedThisFrame) { selected.OffsetY -= offsetStep; changed = true; }
        if (keyboard.qKey.wasPressedThisFrame) { selected.Scale = Mathf.Max(MinimumScale, selected.Scale - scaleStep); changed = true; }
        if (keyboard.eKey.wasPressedThisFrame) { selected.Scale += scaleStep; changed = true; }
        if (keyboard.rKey.wasPressedThisFrame)
        {
            selected.Scale = 1f;
            selected.OffsetX = 0f;
            selected.OffsetY = 0f;
            changed = true;
        }

        if (changed)
            Apply(selected);
    }

    // 01. Canvas independente em referência 1920x1080.
    private void Build()
    {
        overlay = new GameObject("DivisionEmblemDiagnosticOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        CanvasGroup inputGate = overlay.GetComponent<CanvasGroup>();
        inputGate.interactable = true;
        inputGate.blocksRaycasts = true;

        RectTransform veil = Rect("Veil", overlay.transform, 0f, 0f, 1920f, 1080f);
        Image veilImage = veil.gameObject.AddComponent<Image>();
        veilImage.color = new Color(.01f, .018f, .03f, .97f);
        veilImage.raycastTarget = true;

        Text(veil, "Title", "DIAGNÓSTICO CANÔNICO — EMBLEMAS DE DIVISÃO",
            28f, new Color(.97f, .98f, .99f, 1f), 64f, 28f, 1792f, 42f,
            TextAlignmentOptions.Center, "Gabarito-ExtraBold");
        Text(veil, "Instructions",
            "CLIQUE: SELECIONAR  ·  A/D: X  ·  W/S: Y  ·  Q/E: SCALE  ·  SHIFT: FINO  ·  R: RESET  ·  C: CONSOLE  ·  F8: FECHAR",
            14f, new Color(0f, .9f, 1f, 1f), 64f, 72f, 1792f, 28f,
            TextAlignmentOptions.Center, "Gabarito-Medium");

        WDDivisionCatalog catalog = WDDivisionCatalog.CreatePrototype();
        for (int i = 0; i < catalog.Ordered.Count; i++)
        {
            int column = i % 4;
            int row = i / 4;
            float x = 132f + column * 430f;
            float y = 126f + row * 300f;
            BuildItem(veil, catalog.Ordered[i], x, y);
        }

        if (items.Count > 0)
            Select(items[0]);
    }

    // 02. Cada item mantém calibração temporária sem alterar autoridades comerciais.
    private void BuildItem(RectTransform parent, WDDivisionDefinition division,
        float x, float y)
    {
        RectTransform card = Rect("Diagnostic_" + division.Id, parent, x, y, 360f, 272f);
        Image surface = card.gameObject.AddComponent<Image>();
        surface.color = new Color(.035f, .067f, .11f, .92f);
        surface.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(surface, 12);
        WDHomeUIFactory.AddThinRoundedBorder(card,
            new Color(.773f, .608f, .388f, .45f), 12);

        Text(card, "Name", division.DisplayName, 17f, Color.white,
            12f, 12f, 336f, 28f, TextAlignmentOptions.Center, "Gabarito-Bold");
        TextMeshProUGUI values = Text(card, "Values", "Scale: 1.000    X: 0.0    Y: 0.0", 12f,
            new Color(.58f, .64f, .72f, 1f), 12f, 42f, 336f, 22f,
            TextAlignmentOptions.Center, "Inter-Regular");

        RectTransform medal = Rect("Medallion", card,
            (360f - MedalSide) * .5f, 78f, MedalSide, MedalSide);
        RawImage baseImage = medal.gameObject.AddComponent<RawImage>();
        baseImage.texture = WDDivisionVisuals.ResolveMedallion();
        baseImage.color = Color.white;
        baseImage.raycastTarget = false;

        // Guias entre o medalhão e o símbolo: centro geométrico exato.
        Guide(medal, "HorizontalGuide", 0f, MedalSide * .5f - .5f, MedalSide, 1f,
            new Color(0f, 1f, 1f, .72f));
        Guide(medal, "VerticalGuide", MedalSide * .5f - .5f, 0f, 1f, MedalSide,
            new Color(1f, .25f, .32f, .72f));

        float symbolSide = MedalSide;
        RectTransform symbolRect = Rect("CanonicalSymbol", medal,
            (MedalSide - symbolSide) * .5f, (MedalSide - symbolSide) * .5f,
            symbolSide, symbolSide);
        RawImage symbol = symbolRect.gameObject.AddComponent<RawImage>();
        symbol.texture = WDDivisionVisuals.ResolveEmblem(division);
        symbol.color = Color.white;
        symbol.raycastTarget = false;

        WDDivisionVisuals.EmblemCalibration calibration =
            WDDivisionVisuals.ResolveCalibration(division);
        var item = new CalibrationItem
        {
            Division = division,
            Symbol = symbolRect,
            Values = values,
            Surface = surface,
            Scale = calibration.Scale,
            OffsetX = calibration.OffsetX,
            OffsetY = calibration.OffsetY
        };
        items.Add(item);
        Apply(item);

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Select(item));
    }

    // 03. Seleção e transformação afetam exclusivamente o pictograma.
    private void Select(CalibrationItem item)
    {
        selected = item;
        foreach (CalibrationItem candidate in items)
        {
            candidate.Surface.color = candidate == selected
                ? new Color(.075f, .14f, .21f, .98f)
                : new Color(.035f, .067f, .11f, .92f);
        }
    }

    private static void Apply(CalibrationItem item)
    {
        var calibration = new WDDivisionVisuals.EmblemCalibration(
            item.Scale, item.OffsetX, item.OffsetY);
        WDDivisionVisuals.ApplyCalibration(item.Symbol, calibration, MedalSide);
        item.Values.text = string.Format(CultureInfo.InvariantCulture,
            "Scale: {0:0.000}    X: {1:+0.0;-0.0;0.0}    Y: {2:+0.0;-0.0;0.0}",
            item.Scale, item.OffsetX, item.OffsetY);
    }

    // 04. Saída única e copiável com os onze valores atuais.
    private void PrintCalibration()
    {
        var table = new StringBuilder("[WD-DIVISION-CALIBRATION]\n");
        foreach (CalibrationItem item in items)
        {
            table.Append(item.Division.DisplayName)
                .Append(": Scale=").Append(item.Scale.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" X=").Append(item.OffsetX.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" Y=").Append(item.OffsetY.ToString("0.0", CultureInfo.InvariantCulture))
                .AppendLine();
        }
        Debug.Log(table.ToString().TrimEnd());
    }

    private static void Guide(Transform parent, string name, float x, float y,
        float width, float height, Color color)
    {
        RectTransform line = Rect(name, parent, x, y, width, height);
        Image image = line.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string value,
        float size, Color color, float x, float y, float width, float height,
        TextAlignmentOptions alignment, string font)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.font = WDHomeTypography.Load(font);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
}

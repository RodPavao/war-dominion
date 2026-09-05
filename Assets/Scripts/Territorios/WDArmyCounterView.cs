using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WDArmyCounterView : MonoBehaviour
{
    // =====================================================
    // 1. ESTADO E CONTRATO DE APRESENTAÇÃO
    // =====================================================

    private TerritorioClique territorio;
    private TextMeshPro troopNumber;
    private SpriteRenderer outerFrame;
    private SpriteRenderer shadowRenderer;
    private Sprite shadowSpriteRuntime;
    private static Sprite centerSprite;
    private static Sprite outerFrameSprite;

    [Header("Shadow Settings")]
    [SerializeField] private bool shadowEnabled = true;
    [SerializeField, Range(0f, 1f)] private float shadowOpacity = 0.30f;
    [SerializeField] private float shadowOffsetX = 1.5f;
    [SerializeField] private float shadowOffsetY = -1.5f;
    [SerializeField, Min(0f)] private float shadowExtraSize = 1f;
    [SerializeField, Min(0.5f)] private float shadowFeather = 4f;

    private const float PixelsPerUnit = 100f;

    public TerritorioClique Territorio => territorio;
    public int QuantidadeExibida { get; private set; }

    public void Configurar(TerritorioClique territorioAlvo)
    {
        territorio = territorioAlvo;
        GarantirComposicao();
        ExibirQuantidade(territorio != null ? territorio.Tropas : 0);
    }

    private void OnValidate()
    {
        shadowOpacity = Mathf.Clamp01(shadowOpacity);
        shadowExtraSize = Mathf.Max(0f, shadowExtraSize);
        shadowFeather = Mathf.Max(0.5f, shadowFeather);

        if (Application.isPlaying)
            AtualizarSombra();
    }

    private void OnDestroy()
    {
        DescartarSpriteSombraAnterior();
    }

    public void ExibirQuantidade(int quantidade)
    {
        QuantidadeExibida = Mathf.Max(0, quantidade);
        if (troopNumber == null)
            GarantirComposicao();
        if (troopNumber != null)
            troopNumber.text = QuantidadeExibida.ToString();
    }

    public void Exibir(int quantidade, Color corFaixa)
    {
        ExibirQuantidade(quantidade);
        AplicarEstiloFaixa(corFaixa);
    }

    public void AplicarEstiloFaixa(
        Color cor,
        Sprite sprite = null,
        Material material = null)
    {
        if (outerFrame == null)
            GarantirComposicao();
        if (outerFrame == null)
            return;

        outerFrame.sprite = sprite != null ? sprite : outerFrameSprite;
        if (material != null)
            outerFrame.sharedMaterial = material;
        cor.a = 1f;
        outerFrame.color = cor;
    }

    // =====================================================
    // 2. COMPOSIÇÃO COM RECORTE REALMENTE TRANSPARENTE
    // =====================================================

    private void GarantirComposicao()
    {
        Transform root = transform.Find("WDArmyCounter");
        if (root == null)
        {
            root = new GameObject("WDArmyCounter").transform;
            root.SetParent(transform, false);
        }
        root.localScale = Vector3.one;

        if (centerSprite == null)
            centerSprite = CriarSpriteCentroRetangular();
        if (outerFrameSprite == null)
            outerFrameSprite = CriarSpriteFaixaExterna();
        GarantirSombra(root);
        outerFrame = GarantirSprite(
            root,
            "OuterFrame",
            outerFrameSprite,
            Color.white,
            23,
            Vector3.one);

        Transform reveal = root.Find("TerritoryReveal");
        if (reveal == null)
        {
            reveal = new GameObject("TerritoryReveal").transform;
            reveal.SetParent(root, false);
        }
        reveal.localScale = Vector3.one;
        SpriteRenderer revealRenderer = reveal.GetComponent<SpriteRenderer>();
        if (revealRenderer != null)
            revealRenderer.enabled = false;

        GarantirSprite(
            root,
            "CenterBackground",
            centerSprite,
            new Color(0.012f, 0.012f, 0.015f, 1f),
            24,
            Vector3.one);
        GarantirNumero(root);
    }

    // =====================================================
    // 3. SOMBRA PROCEDURAL AJUSTÁVEL
    // =====================================================

    private void GarantirSombra(Transform root)
    {
        Transform shadow = root.Find("Shadow");
        if (shadow == null)
        {
            shadow = new GameObject("Shadow").transform;
            shadow.SetParent(root, false);
            shadow.SetAsFirstSibling();
        }

        shadow.localScale = Vector3.one;
        shadowRenderer = shadow.GetComponent<SpriteRenderer>();
        if (shadowRenderer == null)
            shadowRenderer = shadow.gameObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sortingOrder = 22;
        AtualizarSombra();
    }

    private void AtualizarSombra()
    {
        if (shadowRenderer == null)
        {
            Transform root = transform.Find("WDArmyCounter");
            Transform shadow = root != null ? root.Find("Shadow") : null;
            shadowRenderer = shadow != null
                ? shadow.GetComponent<SpriteRenderer>()
                : null;
        }

        if (shadowRenderer == null)
            return;

        shadowRenderer.enabled = shadowEnabled;
        if (!shadowEnabled)
            return;

        DescartarSpriteSombraAnterior();
        shadowSpriteRuntime = CriarSpriteSombraSuave(
            shadowExtraSize,
            shadowFeather);
        shadowSpriteRuntime.hideFlags = HideFlags.DontSave;
        shadowRenderer.sprite = shadowSpriteRuntime;
        shadowRenderer.color = new Color(0f, 0f, 0f, shadowOpacity);
        shadowRenderer.transform.localPosition = new Vector3(
            shadowOffsetX / PixelsPerUnit,
            shadowOffsetY / PixelsPerUnit,
            0f);
    }

    private void DescartarSpriteSombraAnterior()
    {
        if (shadowSpriteRuntime == null)
            return;

        Texture2D texture = shadowSpriteRuntime.texture;
        if (Application.isPlaying)
        {
            Destroy(shadowSpriteRuntime);
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(shadowSpriteRuntime);
            DestroyImmediate(texture);
        }

        shadowSpriteRuntime = null;
    }

    private void GarantirNumero(Transform root)
    {
        Transform existing = root.Find("TroopNumber");
        if (existing == null)
        {
            existing = new GameObject("TroopNumber").transform;
            existing.SetParent(root, false);
        }
        existing.localScale = Vector3.one;

        troopNumber = existing.GetComponent<TextMeshPro>();
        if (troopNumber == null)
            troopNumber = existing.gameObject.AddComponent<TextMeshPro>();

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(
            "Fonts & Materials/BebasNeue-Regular SDF");
        if (font != null)
            troopNumber.font = font;
        troopNumber.color = Color.white;
        troopNumber.fontSize = 4.2f;
        troopNumber.fontStyle = FontStyles.Bold;
        troopNumber.fontWeight = FontWeight.SemiBold;
        troopNumber.alignment = TextAlignmentOptions.Center;
        troopNumber.verticalAlignment = VerticalAlignmentOptions.Middle;
        troopNumber.sortingOrder = 25;
        troopNumber.enableAutoSizing = false;
        troopNumber.GetComponent<RectTransform>().sizeDelta = new Vector2(0.62f, 0.52f);
    }

    // =====================================================
    // 4. PRIMITIVAS VISUAIS PROVISÓRIAS
    // =====================================================

    private static SpriteRenderer GarantirSprite(
        Transform root,
        string name,
        Sprite sprite,
        Color color,
        int sortingOrder,
        Vector3 localScale)
    {
        Transform child = root.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(root, false);
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.enabled = true;
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        child.localScale = localScale;
        return renderer;
    }

    private static Sprite CriarSpriteFaixaExterna()
    {
        const int width = 67;
        const int height = 64;
        Texture2D texture = new Texture2D(
            width, height, TextureFormat.RGBA32, false);
        texture.name = "WDArmyCounter_OuterFrame_Runtime";
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool visible = DentroRetanguloArredondado(
                x, y, width, height, 0f, 0f, 21f);
            texture.SetPixel(x, y, visible ? Color.white : Color.clear);
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CriarSpriteSombraSuave(
        float extraSize,
        float feather)
    {
        const float outerWidth = 67f;
        const float outerHeight = 64f;
        const float outerRadius = 21f;
        int margin = Mathf.CeilToInt(extraSize + feather);
        int width = Mathf.CeilToInt(outerWidth) + margin * 2;
        int height = Mathf.CeilToInt(outerHeight) + margin * 2;
        Texture2D texture = new Texture2D(
            width, height, TextureFormat.RGBA32, false);
        texture.name = "WDArmyCounter_Shadow_Runtime";
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.DontSave;

        float halfWidth = outerWidth * 0.5f + extraSize;
        float halfHeight = outerHeight * 0.5f + extraSize;
        float radius = outerRadius + extraSize;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float px = x + 0.5f - width * 0.5f;
            float py = y + 0.5f - height * 0.5f;
            float distance = DistanciaRetanguloArredondado(
                px, py, halfWidth, halfHeight, radius);
            float alpha = distance <= 0f
                ? 1f
                : Mathf.Clamp01(1f - distance / feather);
            alpha *= alpha;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }

    private static float DistanciaRetanguloArredondado(
        float x,
        float y,
        float halfWidth,
        float halfHeight,
        float radius)
    {
        float qx = Mathf.Abs(x) - (halfWidth - radius);
        float qy = Mathf.Abs(y) - (halfHeight - radius);
        float outsideX = Mathf.Max(qx, 0f);
        float outsideY = Mathf.Max(qy, 0f);
        float outside = Mathf.Sqrt(
            outsideX * outsideX + outsideY * outsideY);
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - radius;
    }

    private static Sprite CriarSpriteCentroRetangular()
    {
        const int width = 67;
        const int height = 64;
        Texture2D texture = new Texture2D(
            width, height, TextureFormat.RGBA32, false);
        texture.name = "WDArmyCounter_Center_Runtime";
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            // Centro 53 x 50 px, deixando uma faixa visível de 7 px.
            bool visible = DentroRetanguloArredondado(
                x, y, width, height, 7f, 7f, 15f);
            texture.SetPixel(x, y, visible ? Color.white : Color.clear);
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool DentroRetanguloArredondado(
        int x,
        int y,
        int width,
        int height,
        float insetX,
        float insetY,
        float radius)
    {
        float px = x + 0.5f;
        float py = y + 0.5f;
        float left = insetX;
        float right = width - insetX;
        float bottom = insetY;
        float top = height - insetY;
        float centerX = Mathf.Clamp(px, left + radius, right - radius);
        float centerY = Mathf.Clamp(py, bottom + radius, top - radius);
        float dx = px - centerX;
        float dy = py - centerY;
        return dx * dx + dy * dy <= radius * radius;
    }
}

using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WDArmyCounterView : MonoBehaviour
{
    public enum VisualState { Normal, Selected, Reinforcement, AttackActive, Damage, Neutral, Empty }
    public const float FrameWidth = 0.59f;
    public const float NumberWidth = 0.48f;
    private static readonly Color NeutralAccent = new(0.86f, 0.76f, 0.57f, 1f);
    private static Sprite bodySprite;
    private TerritorioClique territorio;
    private Transform visualRoot;
    private SpriteRenderer body;
    private TextMeshPro troopNumber;
    private Coroutine stateAnimation;
    private Color ownerColor = NeutralAccent;
    private Color lastOwnedColor = NeutralAccent;
    private VisualState state;
    private WarDominionUITheme gameplayTheme;
    private bool labGlassEnabled;
    private Color labGlassIdentity = Color.white;
    private WDGameplayGlassStyle labGlassStyle;
    public TerritorioClique Territorio => territorio;
    public int QuantidadeExibida { get; private set; }
    public VisualState EstadoVisual => state;
    public Color CorDoAro => body != null ? body.color : ownerColor;
    public Transform VisualRoot => visualRoot;
    public TextMeshPro TroopText => troopNumber;
    public SpriteRenderer BodyRenderer => body;

    public void Configurar(TerritorioClique alvo)
    {
        territorio = alvo;
        ExibirQuantidade(territorio != null ? territorio.Tropas : 0);
    }
    public void ExibirQuantidade(int quantidade)
    {
        QuantidadeExibida = Mathf.Max(0, quantidade);
        if (!GarantirComposicao()) return;
        troopNumber.text = QuantidadeExibida.ToString();
        troopNumber.fontSizeMax = troopNumber.text.Length <= 1 ? 4.2f
            : troopNumber.text.Length == 2 ? 3.7f : 2.8f;
        troopNumber.fontSizeMin = 1.1f;
        troopNumber.enableAutoSizing = true;
        troopNumber.ForceMeshUpdate(true, true);
    }
    public void Exibir(int quantidade, Color cor) => Exibir(quantidade, cor, VisualState.Normal);
    public void Exibir(int quantidade, Color cor, VisualState novoEstado)
    {
        ExibirQuantidade(quantidade);
        ownerColor = Opaca(cor);
        if (novoEstado != VisualState.Neutral && novoEstado != VisualState.Empty && cor != NeutralAccent)
            lastOwnedColor = ownerColor;
        AplicarEstado(novoEstado, false);
    }
    // Preserve the public adapter; the approved body cannot be replaced by cosmetic layers.
    public void AplicarEstiloFaixa(Color cor, Sprite sprite = null, Material material = null)
    {
        ownerColor = lastOwnedColor = Opaca(cor);
        if (GarantirComposicao()) AplicarPaletaAtual();
    }

    // ============================================================
    // 02.1. BADGE VIAJANTE PREMIUM — opt-in exclusivo dos presenters do lab
    // ============================================================
    public void AplicarEstiloBadgeViajante(Color identidade)
    {
        if (!GarantirComposicao() || body == null)
            return;
        Color graphite = new(16f / 255f, 24f / 255f, 32f / 255f, .94f);
        body.color = graphite;
        troopNumber.color = Color.white;
        troopNumber.fontStyle = FontStyles.Bold;

        SpriteRenderer shadow = GarantirCamadaBadge("TravellingShadow", 21);
        shadow.color = new Color(0f, 0f, 0f, .34f);
        shadow.transform.localPosition = new Vector3(.018f, -.025f, .02f);
        shadow.transform.localScale = body.transform.localScale * 1.10f;

        SpriteRenderer identity = GarantirCamadaBadge("TravellingIdentity", 22);
        Color accent = identidade;
        accent.a = .86f;
        identity.color = accent;
        identity.transform.localPosition = new Vector3(0f, 0f, .01f);
        identity.transform.localScale = body.transform.localScale * 1.075f;

        SpriteRenderer silver = GarantirCamadaBadge("TravellingSilver", 23);
        silver.color = new Color(.88f, .91f, .94f, .96f);
        silver.transform.localPosition = Vector3.zero;
        silver.transform.localScale = body.transform.localScale * 1.035f;
    }

    // ============================================================
    // 02.2. GLASS PREMIUM 2.0 — opt-in e reversível somente no laboratório
    // ============================================================
    public void ConfigurarGlassLaboratorio(Color identidade,
        WDGameplayGlassStyle style)
    {
        Color opaqueIdentity = Opaca(identidade);
        if (labGlassEnabled == style.Enabled &&
            labGlassIdentity == opaqueIdentity && SameGlassStyle(labGlassStyle, style))
            return;
        labGlassEnabled = style.Enabled;
        labGlassIdentity = opaqueIdentity;
        labGlassStyle = style;
        if (!GarantirComposicao())
            return;
        DefinirCamadasGlassAtivas(labGlassEnabled);
        AplicarPaletaAtual();
    }

    private static bool SameGlassStyle(WDGameplayGlassStyle a,
        WDGameplayGlassStyle b) => a.Enabled == b.Enabled &&
        Mathf.Approximately(a.GlassIntensity, b.GlassIntensity) &&
        Mathf.Approximately(a.GradientStrength, b.GradientStrength) &&
        Mathf.Approximately(a.ReflectionIntensity, b.ReflectionIntensity) &&
        Mathf.Approximately(a.BevelStrength, b.BevelStrength) &&
        Mathf.Approximately(a.GlowIntensity, b.GlowIntensity) &&
        a.Variant == b.Variant;

    private void AplicarGlassAtual()
    {
        Color graphite = Color.Lerp(new Color(.035f, .055f, .075f, 1f),
            new Color(.015f, .025f, .038f, 1f),
            labGlassStyle.ProfiledGlassIntensity);
        body.color = graphite;
        troopNumber.color = Color.white;

        SpriteRenderer shadow = GarantirCamadaBadge("GlassShadow", 19);
        shadow.color = new Color(0f, 0f, 0f,
            Mathf.Lerp(.20f, .42f, labGlassStyle.ProfiledGlassIntensity));
        shadow.transform.localPosition = new Vector3(.018f, -.026f, .03f);
        shadow.transform.localScale = body.transform.localScale * 1.115f;

        SpriteRenderer lower = GarantirCamadaBadge("GlassOwnerLower", 20);
        lower.color = Color.Lerp(labGlassIdentity,
            Color.black, .18f + labGlassStyle.ProfiledGradientStrength * .24f);
        lower.transform.localPosition = Vector3.zero;
        lower.transform.localScale = body.transform.localScale * 1.09f;
        lower.enabled = labGlassStyle.Variant != WDGameplayGlassVariant.Clean;

        SpriteRenderer upper = GarantirCamadaBadge("GlassOwnerUpper", 21);
        Color upperColor = Color.Lerp(labGlassIdentity, Color.white,
            .12f + labGlassStyle.ProfiledGradientStrength * .25f);
        upperColor.a = Mathf.Lerp(.62f, .94f,
            labGlassStyle.ProfiledGlassIntensity);
        upper.color = upperColor;
        upper.transform.localPosition = new Vector3(0f, .014f, .02f);
        upper.transform.localScale = body.transform.localScale * 1.068f;

        SpriteRenderer bevel = GarantirCamadaBadge("GlassBevel", 22);
        bevel.color = new Color(.88f, .92f, .96f,
            Mathf.Lerp(.12f, .52f, labGlassStyle.ProfiledBevelStrength));
        bevel.transform.localPosition = Vector3.zero;
        bevel.transform.localScale = body.transform.localScale * 1.035f;

        SpriteRenderer reflection = GarantirCamadaBadge("GlassReflection", 25);
        reflection.color = new Color(1f, 1f, 1f,
            Mathf.Lerp(0f, .20f,
                labGlassStyle.ProfiledReflectionIntensity));
        reflection.transform.localPosition = new Vector3(0f, .012f, -.02f);
        reflection.transform.localScale = body.transform.localScale * .965f;
    }

    private void DefinirCamadasGlassAtivas(bool active)
    {
        if (visualRoot == null)
            return;
        foreach (string name in new[] { "GlassShadow", "GlassOwnerLower",
            "GlassOwnerUpper", "GlassBevel", "GlassReflection" })
        {
            Transform layer = visualRoot.Find(name);
            if (layer != null)
                layer.gameObject.SetActive(active);
        }
    }

    private SpriteRenderer GarantirCamadaBadge(string name, int sortingOrder)
    {
        Transform layer = GarantirObjeto(visualRoot, name);
        // 02.1.1. COMPONENTE NATIVO — UnityEngine.Object usa "fake null" para
        // componentes ausentes. A verificação explícita aciona o operador do
        // Unity e impede que um wrapper inválido chegue à atribuição do sprite.
        SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = layer.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = bodySprite;
        renderer.sharedMaterial = body != null ? body.sharedMaterial : null;
        renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = true;
        return renderer;
    }
    public void AplicarEstado(VisualState novoEstado, bool animar = true)
    {
        state = novoEstado;
        if (!GarantirComposicao()) return;
        AplicarPaletaAtual();
        if (stateAnimation != null) StopCoroutine(stateAnimation);
        stateAnimation = null;
        visualRoot.localScale = Vector3.one;
        if (animar && Application.isPlaying && isActiveAndEnabled)
            stateAnimation = StartCoroutine(AnimarEstado(novoEstado));
    }
    public void MostrarSelecionado(bool ativo) => AplicarEstado(ativo ? VisualState.Selected : VisualState.Normal, ativo);
    public void PulsoReforco() => AplicarEstado(VisualState.Reinforcement);
    public void PulsoDano() => AplicarEstado(VisualState.Damage);
    public void MostrarAtaqueAtivo(bool ativo) => AplicarEstado(ativo ? VisualState.AttackActive : VisualState.Normal, ativo);

    public void ResetTransientFeedback()
    {
        if (stateAnimation != null) StopCoroutine(stateAnimation);
        stateAnimation = null;
        state = VisualState.Normal;
        if (visualRoot != null) visualRoot.localScale = Vector3.one;
        AplicarPaletaAtual();
    }

    private bool GarantirComposicao()
    {
        foreach (string name in new[] { "WDArmyCounter", "ArmyCounterRoot", "ArmyCounterUIRoot" })
        {
            Transform old = transform.Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
        if (bodySprite == null)
        {
            GameObject reference = Resources.Load<GameObject>("UI/Match/WDArmyCounterFrameReference");
            bodySprite = reference != null ? reference.GetComponent<SpriteRenderer>()?.sprite : null;
        }
        if (bodySprite == null) return false;
        visualRoot = GarantirObjeto(transform, "ArmyCounterSpriteRoot");
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
        Transform graphic = GarantirObjeto(visualRoot, "Body");
        body = graphic.GetComponent<SpriteRenderer>();
        if (body == null) body = graphic.gameObject.AddComponent<SpriteRenderer>();
        body.sprite = bodySprite;
        body.enabled = true;
        body.sortingLayerID = 0;
        body.sortingOrder = 24;
        graphic.localPosition = Vector3.zero;
        graphic.localRotation = Quaternion.identity;
        graphic.localScale = Vector3.one * (FrameWidth / bodySprite.bounds.size.x);
        Transform text = GarantirObjeto(visualRoot, "TroopText");
        troopNumber = text.GetComponent<TextMeshPro>();
        if (troopNumber == null) troopNumber = text.gameObject.AddComponent<TextMeshPro>();
        text = troopNumber.transform;
        text.localPosition = new Vector3(0f, 0f, -0.01f);
        text.localRotation = Quaternion.identity;
        text.localScale = Vector3.one;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/BebasNeue-Regular SDF");
        if (font != null) troopNumber.font = font;
        // A caixa invisível continua dentro do frame de 0,59. Os 0,02 extras
        // acomodam o maior int completo no limite legível de fonte 1,1.
        troopNumber.rectTransform.sizeDelta = new Vector2(NumberWidth, 0.46f);
        troopNumber.enabled = true;
        troopNumber.fontStyle = FontStyles.Bold;
        troopNumber.fontWeight = FontWeight.Bold;
        troopNumber.alignment = TextAlignmentOptions.Center;
        troopNumber.verticalAlignment = VerticalAlignmentOptions.Middle;
        troopNumber.textWrappingMode = TextWrappingModes.NoWrap;
        troopNumber.overflowMode = TextOverflowModes.Truncate;
        troopNumber.enableAutoSizing = true;
        troopNumber.fontSizeMin = 1.1f;
        troopNumber.fontSizeMax = 4.2f;
        troopNumber.raycastTarget = false;
        troopNumber.sortingLayerID = body.sortingLayerID;
        troopNumber.sortingOrder = body.sortingOrder + 1;
        AplicarPaletaAtual();
        return true;
    }
    private Transform GarantirObjeto(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }
        child.gameObject.SetActive(true);
        child.gameObject.layer = gameObject.layer;
        return child;
    }
    private void AplicarPaletaAtual()
    {
        if (body == null) return;
        if (labGlassEnabled)
        {
            AplicarGlassAtual();
            return;
        }
        Color color = state == VisualState.Empty ? lastOwnedColor : ownerColor;
        if (state == VisualState.Selected || state == VisualState.AttackActive)
            color = Color.Lerp(color, Color.white, 0.16f);
        body.color = Opaca(color);
        troopNumber.color = Color.white;
        troopNumber.alpha = state == VisualState.Empty ? 0.78f : 1f;
    }
    private IEnumerator AnimarEstado(VisualState animatedState)
    {
        gameplayTheme ??= Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");
        if (gameplayTheme == null || !gameplayTheme.PremiumGameplayFeedbackEnabled)
        {
            yield return AnimarEstadoLegado(animatedState);
            yield break;
        }

        // ============================================================
        // 04. FASE 7.3A — ASSINATURAS CURTAS POR ESTADO DO CONTADOR
        // ============================================================
        float duration = ObterDuracaoPremium(animatedState);
        float peak = ObterEscalaPremium(animatedState);
        bool damage = animatedState == VisualState.Damage;
        Color initialBodyColor = body != null ? body.color : ownerColor;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float scale = damage
                ? Mathf.Lerp(1f, peak, envelope)
                : Mathf.Lerp(1f, peak, EaseOutBack(envelope));
            visualRoot.localScale = Vector3.one * scale;
            if (body != null)
            {
                float light = animatedState == VisualState.Reinforcement ? 0.22f :
                    animatedState == VisualState.AttackActive ? 0.14f :
                    damage ? 0.08f : 0.10f;
                body.color = Color.Lerp(initialBodyColor, Color.white, light * envelope);
            }
            yield return null;
        }
        FinalizarAnimacaoTemporaria(animatedState);
    }

    private IEnumerator AnimarEstadoLegado(VisualState animatedState)
    {
        float duration = animatedState == VisualState.Damage ? 0.20f : 0.30f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, animatedState == VisualState.Selected ? 1.045f : 1.08f, pulse);
            yield return null;
        }
        FinalizarAnimacaoTemporaria(animatedState);
    }

    private float ObterDuracaoPremium(VisualState animatedState)
    {
        if (gameplayTheme.ReducedGameplayMotion)
            return 0.08f;
        return animatedState switch
        {
            VisualState.Damage => gameplayTheme.CounterDamageDuration,
            VisualState.Reinforcement => gameplayTheme.CounterReinforcementDuration,
            VisualState.AttackActive => gameplayTheme.CounterAttackDuration,
            _ => gameplayTheme.CounterSelectionDuration
        };
    }

    private float ObterEscalaPremium(VisualState animatedState)
    {
        if (gameplayTheme.ReducedGameplayMotion)
            return animatedState == VisualState.Damage ? 0.98f : 1.015f;
        return animatedState switch
        {
            VisualState.Damage => gameplayTheme.CounterDamageCompression,
            VisualState.Reinforcement => gameplayTheme.CounterReinforcementScale,
            VisualState.AttackActive => gameplayTheme.CounterAttackScale,
            _ => 1.045f
        };
    }

    private void FinalizarAnimacaoTemporaria(VisualState animatedState)
    {
        if (visualRoot != null)
            visualRoot.localScale = Vector3.one;
        stateAnimation = null;
        if (animatedState == VisualState.Reinforcement || animatedState == VisualState.Damage)
            state = VisualState.Normal;
        AplicarPaletaAtual();
    }

    private static float EaseOutBack(float value)
    {
        float t = Mathf.Clamp01(value) - 1f;
        const float overshoot = 1.15f;
        return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
    }
    private void OnDisable()
    {
        if (stateAnimation != null) StopCoroutine(stateAnimation);
        stateAnimation = null;
        if (visualRoot != null) visualRoot.localScale = Vector3.one;
        AplicarPaletaAtual();
    }
    private static Color Opaca(Color color) { color.a = 1f; return color; }
}

using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WDArmyCounterView : MonoBehaviour
{
    public enum VisualState { Normal, Selected, Reinforcement, AttackActive, Damage, Neutral, Empty }
    public const float FrameWidth = 0.59f;
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
        troopNumber.fontSize = troopNumber.text.Length <= 1 ? 4.2f : troopNumber.text.Length == 2 ? 3.7f : 2.8f;
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
        troopNumber.rectTransform.sizeDelta = new Vector2(0.46f, 0.46f);
        troopNumber.enabled = true;
        troopNumber.fontStyle = FontStyles.Bold;
        troopNumber.fontWeight = FontWeight.Bold;
        troopNumber.alignment = TextAlignmentOptions.Center;
        troopNumber.verticalAlignment = VerticalAlignmentOptions.Middle;
        troopNumber.textWrappingMode = TextWrappingModes.NoWrap;
        troopNumber.overflowMode = TextOverflowModes.Overflow;
        troopNumber.enableAutoSizing = false;
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
        Color color = state == VisualState.Empty ? lastOwnedColor : ownerColor;
        if (state == VisualState.Selected || state == VisualState.AttackActive)
            color = Color.Lerp(color, Color.white, 0.16f);
        body.color = Opaca(color);
        troopNumber.color = Color.white;
        troopNumber.alpha = state == VisualState.Empty ? 0.78f : 1f;
    }
    private IEnumerator AnimarEstado(VisualState animatedState)
    {
        float duration = animatedState == VisualState.Damage ? 0.20f : 0.30f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, animatedState == VisualState.Selected ? 1.045f : 1.08f, pulse);
            yield return null;
        }
        visualRoot.localScale = Vector3.one;
        stateAnimation = null;
        if (animatedState == VisualState.Reinforcement || animatedState == VisualState.Damage)
        {
            state = VisualState.Normal;
            AplicarPaletaAtual();
        }
    }
    private void OnDisable()
    {
        if (stateAnimation != null) StopCoroutine(stateAnimation);
        stateAnimation = null;
        if (visualRoot != null) visualRoot.localScale = Vector3.one;
    }
    private static Color Opaca(Color color) { color.a = 1f; return color; }
}

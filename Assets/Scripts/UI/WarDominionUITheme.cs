using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WarDominionUITheme",
    menuName = "War Dominion/UI/Theme")]
public sealed class WarDominionUITheme : ScriptableObject
{
    // ============================================================
    // 01. TIPOGRAFIA CENTRALIZADA
    // ============================================================

    [Header("Tipografia")]
    [SerializeField] private TMP_FontAsset fonteDisplay;
    [SerializeField] private TMP_FontAsset fonteInterface;
    [SerializeField, Min(8f)] private float tamanhoTitulo = 26f;
    [SerializeField, Min(8f)] private float tamanhoEstado = 20f;
    [SerializeField, Min(8f)] private float tamanhoCronometro = 26f;

    // ============================================================
    // 02. PALETA E SUPERFÍCIES
    // ============================================================

    [Header("Paleta")]
    [SerializeField] private Color superficiePrincipal =
        new Color(0.035f, 0.055f, 0.075f, 0.94f);
    [SerializeField] private Color superficieSecundaria =
        new Color(0.075f, 0.105f, 0.13f, 0.82f);
    [SerializeField] private Color textoPrincipal =
        new Color(0.92f, 0.95f, 0.96f, 1f);
    [SerializeField] private Color textoSecundario =
        new Color(0.64f, 0.72f, 0.76f, 1f);
    [SerializeField] private Color acento =
        new Color(0.17f, 0.72f, 0.78f, 1f);
    [SerializeField] private Color moldura =
        new Color(0.31f, 0.39f, 0.43f, 0.72f);

    // ============================================================
    // 03. DIMENSÕES E ESPAÇAMENTO
    // ============================================================

    [Header("Layout")]
    [SerializeField, Min(48f)] private float alturaTopBar = 72f;
    [SerializeField, Min(640f)] private float larguraTopBar = 1320f;
    [SerializeField, Min(0f)] private float margemSuperior = 16f;
    [SerializeField, Min(0f)] private float margemLateral = 24f;

    // ============================================================
    // 04. ANÚNCIO TEMPORÁRIO DE ROUND
    // ============================================================

    [Header("Anúncio de Round")]
    [SerializeField, Min(0.01f)] private float duracaoEntradaAnuncio = 0.30f;
    [SerializeField, Min(0f)] private float duracaoVisivelAnuncio = 1.90f;
    [SerializeField, Min(0.01f)] private float duracaoSaidaAnuncio = 0.35f;
    [SerializeField, Min(320f)] private float larguraAnuncioRound = 680f;
    [SerializeField, Min(80f)] private float alturaAnuncioRound = 168f;
    [SerializeField] private float posicaoVerticalAnuncioRound = 70f;
    [SerializeField, Min(8f)] private float tamanhoRoundAnuncio = 42f;
    [SerializeField, Min(8f)] private float tamanhoFaseAnuncio = 18f;
    [SerializeField, Min(80f)] private float larguraLinhaAnuncio = 460f;
    [SerializeField, Min(1f)] private float espessuraLinhaAnuncio = 1f;
    [SerializeField, Min(8f)] private float larguraSegmentoAnuncio = 92f;
    [SerializeField, Min(1f)] private float espessuraSegmentoAnuncio = 3f;
    [SerializeField, Min(0f)] private float margemLinhaAnuncio = 18f;
    [SerializeField, Min(0f)] private float deslocamentoAnuncio = 34f;
    [SerializeField, Range(0.8f, 1f)] private float escalaInicialAnuncio = 0.94f;
    [SerializeField, Range(1f, 1.2f)] private float escalaFinalAnuncio = 1.025f;
    [SerializeField] private Color superficieAnuncio =
        new Color(0.025f, 0.045f, 0.06f, 0.78f);
    [SerializeField] private Color corLinhaAnuncio =
        new Color(0.17f, 0.72f, 0.78f, 0.32f);

    // ============================================================
    // 05. AÇÕES PREPARADAS SOBRE O MAPA
    // ============================================================

    [Header("Ações Preparadas")]
    [SerializeField, Min(1f)] private float preparedArrowThickness = 7f;
    [SerializeField, Min(8f)] private float preparedArrowHeadSize = 25f;
    [SerializeField, Min(0f)] private float preparedArrowEndpointInset = 24f;
    [SerializeField, Min(0.01f)] private float preparedArrowRevealDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float arrowColorOpacity = 0.82f;
    [SerializeField, Min(4f)] private float preparedArrowEnergyLength = 54f;
    [SerializeField, Min(1f)] private float preparedArrowEnergyThickness = 2f;
    [SerializeField, Min(0.05f)] private float planningBeamSpeed = 0.62f;
    [SerializeField, Min(2)] private int planningDashStride = 3;
    [SerializeField, Range(0f, 0.5f)] private float preparedArrowCurveFactor = 0.18f;
    [SerializeField, Min(0f)] private float preparedArrowMaxCurveHeight = 70f;
    [SerializeField, Min(8f)] private float preparedArrowAmountSize = 17f;
    [SerializeField, Min(0f)] private float preparedArrowAmountOffset = 18f;
    [SerializeField, Min(8f)] private float preparedOriginMarkerSize = 38f;

    // ============================================================
    // 06. PRIMEIRA COREOGRAFIA VISUAL DE ATAQUE
    // ============================================================

    [Header("Resolução de Ataque")]
    [SerializeField, Min(0.05f)] private float attackTravelDuration = 0.75f;
    [SerializeField, Min(0.05f)] private float attackImpactDuration = 0.30f;
    [SerializeField, Min(0f)] private float attackResultDuration = 0.80f;
    [SerializeField, Min(12f)] private float attackIndicatorSize = 42f;
    [SerializeField, Min(8f)] private float attackIndicatorTextSize = 18f;
    [SerializeField, Min(8f)] private float attackPulseSize = 54f;
    [SerializeField, Min(1f)] private float attackRouteThickness = 4f;
    [SerializeField, Range(0f, 1f)] private float attackImpactIntensity = 0.85f;
    [SerializeField, Min(8f)] private float attackResultTextSize = 20f;
    [SerializeField, Min(0f)] private float attackResultOffset = 42f;
    [SerializeField, Range(0f, 0.5f)] private float attackCurveFactor = 0.18f;
    [SerializeField, Min(0f)] private float attackMaxCurveHeight = 1.1f;

    // ============================================================
    // 07. REFORÇO E TRANSFERÊNCIA NA RESOLUÇÃO
    // ============================================================

    [Header("Resolução de Reforço")]
    [SerializeField, Min(0.05f)] private float reinforcementEntryDuration = 0.28f;
    [SerializeField, Min(0f)] private float reinforcementReactionDuration = 0.32f;
    [SerializeField, Min(0.05f)] private float reinforcementExitDuration = 0.28f;
    [SerializeField, Range(0.5f, 1f)] private float reinforcementInitialScale = 0.72f;
    [SerializeField, Min(8f)] private float reinforcementTextSize = 21f;
    [SerializeField, Min(8f)] private float reinforcementPulseSize = 58f;
    [SerializeField, Range(0f, 1f)] private float reinforcementPulseOpacity = 0.32f;
    [SerializeField, Min(0f)] private float reinforcementIndicatorOffset = 42f;

    [Header("Resolução de Transferência")]
    [SerializeField, Min(0.05f)] private float transferTransitionDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float transferReactionDuration = 0.42f;
    [SerializeField, Min(8f)] private float transferPulseSize = 62f;
    [SerializeField, Range(0f, 1f)] private float transferPulseOpacity = 0.38f;
    [SerializeField, Min(8f)] private float transferTextSize = 14f;
    [SerializeField, Min(0f)] private float transferTextOffset = 46f;

    // ============================================================
    // 08. DESIGN SYSTEM SEMÂNTICO DA INTERFACE FINAL
    // ============================================================

    [Header("Cores Semânticas")]
    [SerializeField] private Color backgroundPrimary = new Color(0.015f, 0.025f, 0.035f, 0.92f);
    [SerializeField] private Color backgroundElevated = new Color(0.035f, 0.055f, 0.07f, 0.94f);
    [SerializeField] private Color surfaceGlass = new Color(0.045f, 0.075f, 0.095f, 0.86f);
    [SerializeField] private Color borderNeutral = new Color(0.25f, 0.34f, 0.38f, 0.72f);
    [SerializeField] private Color success = new Color(0.22f, 0.72f, 0.48f, 1f);
    [SerializeField] private Color warning = new Color(0.95f, 0.67f, 0.20f, 1f);
    [SerializeField] private Color danger = new Color(0.88f, 0.28f, 0.25f, 1f);
    [SerializeField] private Color disabled = new Color(0.31f, 0.36f, 0.38f, 0.72f);

    [Header("Hierarquia Tipográfica")]
    [SerializeField, Min(8f)] private float typeDisplay = 34f;
    [SerializeField, Min(8f)] private float typeTitle = 24f;
    [SerializeField, Min(8f)] private float typeSection = 16f;
    [SerializeField, Min(8f)] private float typeButton = 15f;
    [SerializeField, Min(8f)] private float typeBody = 14f;
    [SerializeField, Min(8f)] private float typeNumber = 27f;
    [SerializeField, Min(8f)] private float typeMicro = 11f;

    [Header("Espaçamento e Componentes")]
    [SerializeField, Min(1f)] private float spacingXs = 4f;
    [SerializeField, Min(1f)] private float spacingSm = 8f;
    [SerializeField, Min(1f)] private float spacingMd = 14f;
    [SerializeField, Min(1f)] private float spacingLg = 22f;
    [SerializeField, Min(1f)] private float panelBorderWidth = 1f;
    [SerializeField, Min(24f)] private float buttonHeight = 42f;
    [SerializeField, Min(0.01f)] private float microAnimationDuration = 0.14f;

    // ============================================================
    // 09. AJUSTES ESTRUTURAIS CALIBRÁVEIS
    // ============================================================

    [Header("Fluxo e HUD")]
    [SerializeField, Range(1f, 1.5f)] private float mapThumbnailInnerScale = 1.18f;
    [SerializeField, Range(0.08f, 0.5f)] private float actionsDrawerDuration = 0.20f;

    public TMP_FontAsset FonteDisplay => fonteDisplay;
    public TMP_FontAsset FonteInterface => fonteInterface != null ? fonteInterface : fonteDisplay;
    public float TamanhoTitulo => tamanhoTitulo;
    public float TamanhoEstado => tamanhoEstado;
    public float TamanhoCronometro => tamanhoCronometro;
    public Color SuperficiePrincipal => superficiePrincipal;
    public Color SuperficieSecundaria => superficieSecundaria;
    public Color TextoPrincipal => textoPrincipal;
    public Color TextoSecundario => textoSecundario;
    public Color Acento => acento;
    public Color Moldura => moldura;
    public float AlturaTopBar => alturaTopBar;
    public float LarguraTopBar => larguraTopBar;
    public float MargemSuperior => margemSuperior;
    public float MargemLateral => margemLateral;
    public float DuracaoEntradaAnuncio => duracaoEntradaAnuncio;
    public float DuracaoVisivelAnuncio => duracaoVisivelAnuncio;
    public float DuracaoSaidaAnuncio => duracaoSaidaAnuncio;
    public float LarguraAnuncioRound => larguraAnuncioRound;
    public float AlturaAnuncioRound => alturaAnuncioRound;
    public float PosicaoVerticalAnuncioRound => posicaoVerticalAnuncioRound;
    public float TamanhoRoundAnuncio => tamanhoRoundAnuncio;
    public float TamanhoFaseAnuncio => tamanhoFaseAnuncio;
    public float LarguraLinhaAnuncio => larguraLinhaAnuncio;
    public float EspessuraLinhaAnuncio => espessuraLinhaAnuncio;
    public float LarguraSegmentoAnuncio => larguraSegmentoAnuncio;
    public float EspessuraSegmentoAnuncio => espessuraSegmentoAnuncio;
    public float MargemLinhaAnuncio => margemLinhaAnuncio;
    public float DeslocamentoAnuncio => deslocamentoAnuncio;
    public float EscalaInicialAnuncio => escalaInicialAnuncio;
    public float EscalaFinalAnuncio => escalaFinalAnuncio;
    public Color SuperficieAnuncio => superficieAnuncio;
    public Color CorLinhaAnuncio => corLinhaAnuncio;
    public float PreparedArrowThickness => preparedArrowThickness;
    public float PreparedArrowHeadSize => preparedArrowHeadSize;
    public float PreparedArrowEndpointInset => preparedArrowEndpointInset;
    public float PreparedArrowRevealDuration => preparedArrowRevealDuration;
    public float ArrowColorOpacity => arrowColorOpacity;
    public float PreparedArrowEnergyLength => preparedArrowEnergyLength;
    public float PreparedArrowEnergyThickness => preparedArrowEnergyThickness;
    public float PlanningBeamSpeed => planningBeamSpeed;
    public int PlanningDashStride => planningDashStride;
    public float PreparedArrowCurveFactor => preparedArrowCurveFactor;
    public float PreparedArrowMaxCurveHeight => preparedArrowMaxCurveHeight;
    public float PreparedArrowAmountSize => preparedArrowAmountSize;
    public float PreparedArrowAmountOffset => preparedArrowAmountOffset;
    public float PreparedOriginMarkerSize => preparedOriginMarkerSize;
    public float AttackTravelDuration => attackTravelDuration;
    public float AttackImpactDuration => attackImpactDuration;
    public float AttackResultDuration => attackResultDuration;
    public float AttackIndicatorSize => attackIndicatorSize;
    public float AttackIndicatorTextSize => attackIndicatorTextSize;
    public float AttackPulseSize => attackPulseSize;
    public float AttackRouteThickness => attackRouteThickness;
    public float AttackImpactIntensity => attackImpactIntensity;
    public float AttackResultTextSize => attackResultTextSize;
    public float AttackResultOffset => attackResultOffset;
    public float AttackCurveFactor => attackCurveFactor;
    public float AttackMaxCurveHeight => attackMaxCurveHeight;
    public float ReinforcementEntryDuration => reinforcementEntryDuration;
    public float ReinforcementReactionDuration => reinforcementReactionDuration;
    public float ReinforcementExitDuration => reinforcementExitDuration;
    public float ReinforcementInitialScale => reinforcementInitialScale;
    public float ReinforcementTextSize => reinforcementTextSize;
    public float ReinforcementPulseSize => reinforcementPulseSize;
    public float ReinforcementPulseOpacity => reinforcementPulseOpacity;
    public float ReinforcementIndicatorOffset => reinforcementIndicatorOffset;
    public float TransferTransitionDuration => transferTransitionDuration;
    public float TransferReactionDuration => transferReactionDuration;
    public float TransferPulseSize => transferPulseSize;
    public float TransferPulseOpacity => transferPulseOpacity;
    public float TransferTextSize => transferTextSize;
    public float TransferTextOffset => transferTextOffset;
    public Color BackgroundPrimary => backgroundPrimary;
    public Color BackgroundElevated => backgroundElevated;
    public Color SurfaceGlass => surfaceGlass;
    public Color BorderNeutral => borderNeutral;
    public Color Success => success;
    public Color Warning => warning;
    public Color Danger => danger;
    public Color Disabled => disabled;
    public float TypeDisplay => typeDisplay;
    public float TypeTitle => typeTitle;
    public float TypeSection => typeSection;
    public float TypeButton => typeButton;
    public float TypeBody => typeBody;
    public float TypeNumber => typeNumber;
    public float TypeMicro => typeMicro;
    public float SpacingXs => spacingXs;
    public float SpacingSm => spacingSm;
    public float SpacingMd => spacingMd;
    public float SpacingLg => spacingLg;
    public float PanelBorderWidth => panelBorderWidth;
    public float ButtonHeight => buttonHeight;
    public float MicroAnimationDuration => microAnimationDuration;
    public float MapThumbnailInnerScale => mapThumbnailInnerScale;
    public float ActionsDrawerDuration => actionsDrawerDuration;
}

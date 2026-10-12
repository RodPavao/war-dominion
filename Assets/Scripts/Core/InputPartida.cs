using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputPartida : MonoBehaviour
{
    public const float SensibilidadeZoomPorPasso = 0.055f;
    public const float DivisorDeltaRodaLegado = 120f;
    // =====================================================
    // 1. REFERÊNCIAS E ESTADO
    // =====================================================

    private Camera cameraPrincipal;
    private readonly List<RaycastResult> raycastsUiAtuais = new();

    // Contador que está sendo pressionado neste momento.
    private ContadorTropas contadorPressionado;

    // =====================================================
    // 2. INICIALIZAÇÃO
    // =====================================================

    private void Awake()
    {
        EncontrarCamera();
    }

    private void Update()
    {
        if (Pointer.current == null)
            return;

        if (cameraPrincipal == null)
        {
            EncontrarCamera();

            if (cameraPrincipal == null)
                return;
        }

        ProcessarZoomDoMapa();

        // =================================================
        // 3. SOLTOU O CONTADOR
        // =================================================

        if (contadorPressionado != null &&
            Pointer.current.press.wasReleasedThisFrame)
        {
            contadorPressionado
                .FinalizarPressaoInput();

            contadorPressionado = null;

            return;
        }

        // =================================================
        // 4. NOVO CLIQUE / TOQUE
        // =================================================

        if (!Pointer.current.press.wasPressedThisFrame)
            return;

        Vector2 posicaoPointer = Pointer.current.position.ReadValue();

        // O input persiste entre cenas; durante reloads o GameManager da cena
        // pode ficar temporariamente indisponível por um frame.
        if (GameManager.instance == null)
            return;

        // O drawer captura o primeiro toque externo mesmo antes do InputModule
        // atualizar seu cache; no frame de fechamento nenhum alvo subjacente é acionado.
        if (WDMatchActionsView.ConsumesMapPointer(posicaoPointer) ||
            WDMatchSupportView.ConsumesMapPointer(posicaoPointer) ||
            WDMatchInstallationHubView.ConsumesMapPointer(posicaoPointer) ||
            WDWeaponInstallationMapPresenter.ConsumesMapPointer(posicaoPointer) ||
            WDMatchInteractionView.ConsumesMapPointer(posicaoPointer))
            return;

        // A UI tem prioridade sobre o mapa. Isso impede que um clique em
        // controles sobre o viewport também alcance território/collider atrás.
        if (ExisteUiSobPonteiro(posicaoPointer))
            return;

        Vector2 posicaoTela = posicaoPointer;

        // Não processa clique fora do viewport do mapa.
        if (!cameraPrincipal.pixelRect.Contains(
                posicaoTela))
            return;

        Vector2 posicaoMundo =
            cameraPrincipal.ScreenToWorldPoint(
                posicaoTela
            );

        Collider2D[] atingidos =
            Physics2D.OverlapPointAll(
                posicaoMundo
            );

        ProcessarCliqueTerritorial(atingidos, posicaoMundo);
    }

    // 04.1. Consulta o raycast do gesto atual. IsPointerOverGameObject usa o cache
    // do InputModule e, na primeira entrada após Resolution, ainda pode descrever
    // a UI do round anterior até que esse primeiro PointerDown seja processado.
    private bool ExisteUiSobPonteiro(Vector2 posicaoTela)
    {
        EventSystem sistema = EventSystem.current;
        if (sistema == null)
            return false;
        raycastsUiAtuais.Clear();
        sistema.RaycastAll(new PointerEventData(sistema) { position = posicaoTela },
            raycastsUiAtuais);
        return raycastsUiAtuais.Count > 0;
    }

    // 4.2. Mesmo encaminhamento para mouse/touch, após UI e viewport.
    // Intenção tática só intercepta alvo válido; inválido continua neste gesto.
    private void ProcessarCliqueTerritorial(Collider2D[] atingidos, Vector2? gestureWorldPoint = null)
    {
        // =================================================
        // 5. ABERTURA 1x1: CONTADOR E MÁSCARA ESCOLHEM TERRITÓRIO
        // =================================================

        if (GameManager.instance.EmSelecaoInicial &&
            TentarSelecionarTerritorioInicial(atingidos))
            return;

        // =================================================
        // 6. PRIORIDADE CONTEXTUAL: MODO TRANSFERÊNCIA
        // =================================================

        if (GameManager.instance != null &&
            GameManager.instance.EmModoTransferencia &&
            TentarSelecionarTerritorioTransferencia(atingidos))
        {
            return;
        }

        // =================================================
        // 7. TARGETING DE ARMA TEM PRIORIDADE SOBRE O CONTADOR
        // =================================================

        if (GameManager.instance.TemTargetingArmaTerritorialAtivo &&
            TentarSelecionarTerritorioContextual(atingidos))
        {
            return;
        }

        // =================================================
        // 8. PRIORIDADE NORMAL: CONTADOR
        // =================================================

        foreach (Collider2D collider in atingidos)
        {
            ContadorTropas contador =
                collider.GetComponent<ContadorTropas>();

            if (contador == null)
            {
                contador =
                    collider.GetComponentInParent<
                        ContadorTropas
                    >();
            }

            if (contador != null)
            {
                var informational = GameManager.instance.GetComponentInChildren<WDMatchInformationalOverlayController>();
                if (contador.Territorio != null &&
                    informational?.TryConsumeTacticalIntent(contador.Territorio.idTerritorio, gestureWorldPoint) == true)
                    return;
                // Inclui reforço/hold: a intenção inválida não desvia para ClicarTerritorio.
                contadorPressionado =
                    contador;

                contadorPressionado
                    .IniciarPressaoInput();

                return;
            }
        }

        // =================================================
        // 9. SEGUNDA PRIORIDADE: TERRITÓRIO
        // =================================================

        foreach (Collider2D collider in atingidos)
        {
            TerritorioClique territorio =
                collider.GetComponent<
                    TerritorioClique
                >();

            if (territorio == null)
            {
                territorio =
                    collider.GetComponentInParent<
                        TerritorioClique
                    >();
            }

            if (territorio != null)
            {
                // 9.1. Mesma posição já resolvida no Update para Pointer (mouse/touch).
                // Inválido retorna false e continua o clique normal, sem replay.
                var informational = GameManager.instance.GetComponentInChildren<WDMatchInformationalOverlayController>();
                if (informational?.TryConsumeTacticalIntent(territorio.idTerritorio, gestureWorldPoint) == true)
                    return;
                territorio.Clicar();

                return;
            }
        }
    }

    private void ProcessarZoomDoMapa()
    {
        Vector2 scroll = Mouse.current != null
            ? Mouse.current.scroll.ReadValue()
            : Vector2.zero;
        if (Mathf.Approximately(scroll.y, 0f) ||
            MapaAtivo.Instance == null ||
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 posicaoTela = Pointer.current.position.ReadValue();
        if (!cameraPrincipal.pixelRect.Contains(posicaoTela))
            return;

        float tamanhoBase = MapaAtivo.Instance.TamanhoOrtograficoEnquadrado;
        if (tamanhoBase <= 0f)
            return;

        cameraPrincipal.orthographicSize = CalcularTamanhoZoom(
            cameraPrincipal.orthographicSize, scroll.y, tamanhoBase);
    }

    public static float CalcularTamanhoZoom(float tamanhoAtual, float deltaRoda,
        float tamanhoBase)
    {
        if (tamanhoBase <= 0f || Mathf.Approximately(deltaRoda, 0f))
            return tamanhoAtual;
        float deltaNormalizado = Mathf.Abs(deltaRoda) >= 10f
            ? deltaRoda / DivisorDeltaRodaLegado
            : deltaRoda;
        float fator = Mathf.Exp(-deltaNormalizado * SensibilidadeZoomPorPasso);
        return Mathf.Clamp(tamanhoAtual * fator, tamanhoBase * 0.25f,
            tamanhoBase);
    }

    public static bool TentarObterTerritorio(
        Collider2D collider, out TerritorioClique territorio)
    {
        territorio = collider?.GetComponent<TerritorioClique>() ??
            collider?.GetComponentInParent<TerritorioClique>();
        if (territorio != null)
            return true;
        ContadorTropas contador = collider?.GetComponent<ContadorTropas>() ??
            collider?.GetComponentInParent<ContadorTropas>();
        territorio = contador?.Territorio;
        return territorio != null;
    }

    private static bool TentarSelecionarTerritorioContextual(Collider2D[] atingidos)
    {
        foreach (Collider2D collider in atingidos)
            if (TentarObterTerritorio(collider, out TerritorioClique territorio))
            {
                territorio.Clicar();
                return true;
            }
        return false;
    }

    private static bool TentarSelecionarTerritorioInicial(Collider2D[] atingidos)
    {
        return TentarSelecionarTerritorioContextual(atingidos);
    }

    // =====================================================
    // 8. SELEÇÃO CONTEXTUAL DE TRANSFERÊNCIA
    // =====================================================

    private bool TentarSelecionarTerritorioTransferencia(
        Collider2D[] atingidos)
    {
        foreach (Collider2D collider in atingidos)
        {
            if (!TentarObterTerritorio(collider, out TerritorioClique territorio))
                continue;

            territorio.Clicar();
            return true;
        }

        return false;
    }

    // =====================================================
    // 9. CÂMERA
    // =====================================================

    private void EncontrarCamera()
    {
        cameraPrincipal =
            Camera.main;

        if (cameraPrincipal == null)
        {
            cameraPrincipal =
                FindAnyObjectByType<Camera>();
        }

        if (cameraPrincipal == null)
        {
            Debug.LogError(
                "INPUT | Main Camera não encontrada."
            );
        }
    }

    // =====================================================
    // 10. CRIAÇÃO AUTOMÁTICA
    // =====================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad
    )]
    private static void CriarAutomaticamente()
    {
        if (FindAnyObjectByType<InputPartida>() != null)
            return;

        GameObject objeto =
            new GameObject("InputPartida");

        DontDestroyOnLoad(objeto);

        objeto.AddComponent<InputPartida>();
    }
}

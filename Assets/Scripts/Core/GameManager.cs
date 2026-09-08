using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // =====================================================
    // 1. SELEÇÃO E JOGADOR LOCAL
    // =====================================================

    public WDMatchSetup MatchSetupAtual => WDMatchSetupContext.Current;
    public int LimiteRoundsDaPartida =>
        MatchSetupAtual != null ? Mathf.Max(1, MatchSetupAtual.RoundLimit) :
        GerenciadorRodada.TotalRoundsNormais;
    public bool MorteSubitaHabilitada =>
        MatchSetupAtual == null || MatchSetupAtual.SuddenDeathEnabled;
    public bool UsaRandomCards =>
        MatchSetupAtual != null && MatchSetupAtual.UsesSymmetricRandomCards;
    public IReadOnlyList<string> CartasRandomJogadorLocal =>
        randomCardsSession != null
            ? randomCardsSession.GetHand(ObterIdJogadorLocal())
            : System.Array.Empty<string>();
    public WDWeaponMatchRuntime WeaponRuntime => weaponRuntime;
    public bool EhPartidaUmContraUm => MatchSetupAtual != null && MatchSetupAtual.IsOneVsOne;
    public bool RequerSelecaoTerritorialInicial => MatchSetupAtual != null &&
        MatchSetupAtual.RequiresInitialTerritorySelection;
    public bool EmSelecaoInicial => initialSelectionSession != null &&
        !initialSelectionSession.IsComplete;
    public int EtapaSelecaoInicial => EmSelecaoInicial ? initialSelectionSession.Stage : 0;
    public string FeedbackSelecaoInicial { get; private set; } = string.Empty;
    public string TerritorioProvisorioInicialId => initialSelectionSession != null
        ? initialSelectionSession.GetPending(ObterIdJogadorLocal())
        : string.Empty;
    public bool PodeConfirmarTerritorioInicial =>
        EmSelecaoInicial && !string.IsNullOrEmpty(TerritorioProvisorioInicialId);
    public bool BloqueandoEntradaNoRoundUm => bloqueioInicioPartida && !EmSelecaoInicial;

    public TerritorioClique territorioSelecionado;
    public TerritorioClique territorioDestinoSelecionado;

    public TerritorioClique.Dono jogadorLocal =
        TerritorioClique.Dono.Jogador1;

    // =====================================================
    // 2. SISTEMAS
    // =====================================================

    private FilaAcoes filaAcoes;
    private SistemaAcoesTerrestres sistemaAcoesTerrestres;
    private ResolvedorCombate resolvedorCombate;
    private ResolvedorAcoesTerrestres resolvedorAcoesTerrestres;
    private FilaTransferencias filaTransferencias;
    private SistemaTransferencias sistemaTransferencias;
    private ResolvedorTransferencias resolvedorTransferencias;
    private ResolutionSequenceController sequenciaResolucaoVisualAtiva;
    private List<ResolutionVisualEvent> eventosReforcoPendentes =
        new List<ResolutionVisualEvent>();
    private GerenciadorRodada gerenciadorRodada;
    private readonly HistoricoReforcos historicoReforcos =
        new HistoricoReforcos();
    private WDRandomCardsSession randomCardsSession;
    private WDWeaponMatchRuntime weaponRuntime;
    private WDInitialTerritorySelectionSession initialSelectionSession;
    private UICompositionRoot uiCompositionRoot;
    private bool bloqueioInicioPartida = true;
    private readonly HashSet<TerritorioClique.Dono> jogadoresQueEnviaram = new();

    // =====================================================
    // 3. PREPARAÇÃO E MODOS
    // =====================================================

    public enum FaseTurno
    {
        Preparacao,
        Resolucao
    }

    public enum EstadoPreparacao
    {
        Preparando,
        Enviado,
        Resolvendo
    }

    public enum ModoAcao
    {
        Nenhum,
        AcaoTerrestre,
        Transferir
    }

    public FaseTurno faseAtual =
        FaseTurno.Preparacao;

    public EstadoPreparacao estadoPreparacao =
        EstadoPreparacao.Preparando;

    public ModoAcao modoAcao =
        ModoAcao.Nenhum;

    public TerritorioClique territorioTransferenciaSelecionado;
    private TerritorioClique.Dono destinatarioTransferenciaSelecionado =
        TerritorioClique.Dono.Neutro;
    public TerritorioClique.Dono DestinatarioTransferenciaSelecionado =>
        destinatarioTransferenciaSelecionado;
    public string feedbackTransferencia = string.Empty;

    // =====================================================
    // 4. REFORÇOS
    // =====================================================

    public int reforcosDisponiveis = 0;

    public System.Collections.Generic.IReadOnlyList<DistribuicaoReforco>
        DistribuicoesReforcos =>
            historicoReforcos.Distribuicoes;

    public bool PodeEditarPreparacao =>
        !EmSelecaoInicial && !bloqueioInicioPartida &&
        faseAtual == FaseTurno.Preparacao &&
        estadoPreparacao == EstadoPreparacao.Preparando;

    public bool AcoesEnviadas =>
        estadoPreparacao == EstadoPreparacao.Enviado;

    public bool EmModoTransferencia =>
        PodeEditarPreparacao &&
        modoAcao == ModoAcao.Transferir;

    // =====================================================
    // 5. AÇÕES TERRESTRES
    // =====================================================

    [Min(1)]
    public int quantidadeAcaoSelecionada = 1;

    // =====================================================
    // 6. CRONÔMETRO
    // =====================================================

    [Header("Preparação")]
    [SerializeField]
    private float duracaoPreparacao = 90f;

    private float tempoPreparacaoRestante;

    public float TempoPreparacaoRestante =>
        tempoPreparacaoRestante;

    // =====================================================
    // 7. CONSULTAS PARA O HUD
    // =====================================================

    public int QuantidadeOrdensPreparadas =>
        filaAcoes != null
            ? filaAcoes.QuantidadePara(jogadorLocal)
            : 0;

    public IReadOnlyList<OrdemTerrestre>
        OrdensPreparadas =>
            filaAcoes != null
                ? ObterOrdensDoJogadorLocal()
                : null;

    public string TipoAcaoSelecionadaEsperado =>
        SistemaAcoesTerrestres.ObterTipoEsperado(
            jogadorLocal,
            territorioDestinoSelecionado);

    public bool LimiteAcoesAtingido =>
        filaAcoes != null &&
        filaAcoes.EstaCheiaPara(jogadorLocal);

    public int RodadaAtual =>
        gerenciadorRodada != null
            ? gerenciadorRodada.RodadaAtual
            : 1;

    public GerenciadorRodada.EstadoPartida EstadoAtualPartida =>
        gerenciadorRodada != null
            ? gerenciadorRodada.EstadoAtual
            : GerenciadorRodada.EstadoPartida.EmPreparacao;

    public ResultadoPartida ResultadoPartidaAtual =>
        gerenciadorRodada != null
            ? gerenciadorRodada.ResultadoAtual
            : null;

    public bool PartidaEncerrada =>
        gerenciadorRodada != null && gerenciadorRodada.PartidaEncerrada;

    public int RoundMorteSubita =>
        gerenciadorRodada != null
            ? gerenciadorRodada.RoundMorteSubita
            : 0;

    public OrdemTransferencia TransferenciaPreparada =>
        filaTransferencias != null
            ? filaTransferencias.ObterPara(jogadorLocal)
            : null;

    public bool LimiteTransferenciaAtingido =>
        TransferenciaPreparada != null;

    public bool PodeConfirmarTransferencia =>
        EmModoTransferencia && territorioTransferenciaSelecionado != null &&
        !LimiteTransferenciaAtingido;

    public bool TransferenciaDisponivelParaJogadorLocal =>
        ObterDestinatariosTransferencia().Count > 0;

    public bool MatchSetupUsaEquipes
    {
        get
        {
            WDMatchSetup setup = MatchSetupAtual;
            if (setup == null)
                return true;

            for (int i = 0; i < setup.Participants.Count; i++)
                for (int j = i + 1; j < setup.Participants.Count; j++)
                    if (setup.Participants[i].TeamIndex == setup.Participants[j].TeamIndex)
                        return true;

            return false;
        }
    }

    public IReadOnlyList<TerritorioClique.Dono> ObterPrioridadeJogadoresParaUI() =>
        gerenciadorRodada != null
            ? gerenciadorRodada.ObterPrioridadeJogadores()
            : System.Array.Empty<TerritorioClique.Dono>();

    public bool PossuiPreparacaoParaEnviar =>
        QuantidadeOrdensPreparadas > 0 ||
        TransferenciaPreparada != null ||
        historicoReforcos.Distribuicoes.Count > 0;

    public bool TodosTerritoriosControladosPossuemTropa
    {
        get
        {
            foreach (TerritorioClique territorio in MapaAtivo.ObterTerritoriosOuCena())
                if (territorio != null && territorio.dono == jogadorLocal && territorio.Tropas < 1)
                    return false;
            return true;
        }
    }

    public int LimiteAcoesPorRodada =>
        FilaAcoes.MaximoAcoesPorRodada;

    public bool PodeCancelarEnvio =>
        faseAtual == FaseTurno.Preparacao &&
        estadoPreparacao == EstadoPreparacao.Enviado &&
        tempoPreparacaoRestante > 0f;

#if UNITY_EDITOR
    // =====================================================
    // 8. TESTE LOCAL DE PARTICIPANTES SIMULADOS
    // =====================================================

    private bool autoenviarJogadoresSimuladosEditor;

    [ContextMenu("Teste Editor/Ativar Autoenvio de Jogadores Simulados")]
    private void AtivarAutoenvioJogadoresSimuladosEditor()
    {
        autoenviarJogadoresSimuladosEditor = true;
        Debug.Log("TESTE LOCAL | Autoenvio dos jogadores simulados ATIVADO.");
    }

    [ContextMenu("Teste Editor/Desativar Autoenvio de Jogadores Simulados")]
    private void DesativarAutoenvioJogadoresSimuladosEditor()
    {
        autoenviarJogadoresSimuladosEditor = false;
        Debug.Log("TESTE LOCAL | Autoenvio dos jogadores simulados DESATIVADO.");
    }

    public void TesteEditorAtivarAutoenvio() => AtivarAutoenvioJogadoresSimuladosEditor();
    public void TesteEditorDesativarAutoenvio() => DesativarAutoenvioJogadoresSimuladosEditor();
#endif

    // =====================================================
    // 9. INICIALIZAÇÃO
    // =====================================================

    private void Awake()
    {
        instance = this;

        filaAcoes =
            GetComponent<FilaAcoes>();

        if (filaAcoes == null)
        {
            filaAcoes =
                gameObject.AddComponent<FilaAcoes>();
        }

        sistemaAcoesTerrestres =
            GetComponent<SistemaAcoesTerrestres>();

        if (sistemaAcoesTerrestres == null)
        {
            sistemaAcoesTerrestres =
                gameObject.AddComponent<SistemaAcoesTerrestres>();
        }

        resolvedorCombate =
            GetComponent<ResolvedorCombate>();

        if (resolvedorCombate == null)
        {
            resolvedorCombate =
                gameObject.AddComponent<ResolvedorCombate>();
        }

        resolvedorAcoesTerrestres =
            GetComponent<ResolvedorAcoesTerrestres>();

        if (resolvedorAcoesTerrestres == null)
        {
            resolvedorAcoesTerrestres =
                gameObject.AddComponent<ResolvedorAcoesTerrestres>();
        }

        filaTransferencias = GetComponent<FilaTransferencias>();

        if (filaTransferencias == null)
            filaTransferencias = gameObject.AddComponent<FilaTransferencias>();

        sistemaTransferencias = GetComponent<SistemaTransferencias>();

        if (sistemaTransferencias == null)
            sistemaTransferencias = gameObject.AddComponent<SistemaTransferencias>();

        resolvedorTransferencias = GetComponent<ResolvedorTransferencias>();

        if (resolvedorTransferencias == null)
            resolvedorTransferencias = gameObject.AddComponent<ResolvedorTransferencias>();

        gerenciadorRodada =
            GetComponent<GerenciadorRodada>();

        if (gerenciadorRodada == null)
        {
            gerenciadorRodada =
                gameObject.AddComponent<GerenciadorRodada>();
        }

        if (GetComponent<HUDPreparacao>() == null)
        {
            gameObject.AddComponent<HUDPreparacao>();
        }

        MatchUIPresenter matchUIPresenter =
            GetComponent<MatchUIPresenter>();

        if (matchUIPresenter == null)
        {
            matchUIPresenter =
                gameObject.AddComponent<MatchUIPresenter>();
        }

        uiCompositionRoot =
            GetComponent<UICompositionRoot>();

        if (uiCompositionRoot == null)
        {
            uiCompositionRoot =
                gameObject.AddComponent<UICompositionRoot>();
        }

        uiCompositionRoot.Inicializar(matchUIPresenter);

        sistemaAcoesTerrestres.Inicializar(filaAcoes);
        resolvedorAcoesTerrestres.Inicializar(resolvedorCombate);
        sistemaTransferencias.Inicializar(filaTransferencias);
    }

    private IEnumerator Start()
    {
        yield return null;
        DistribuidorTerritorios distribuidor =
            FindAnyObjectByType<DistribuidorTerritorios>();
        if (distribuidor != null)
            distribuidor.GarantirDistribuicaoInicial();
        else
            AplicarParticipantesDoMatchSetupAoTabuleiro();
        InicializarRandomCards();
        InicializarWeaponRuntime();
        if (RequerSelecaoTerritorialInicial)
            InicializarSelecaoTerritorial();
        else
            IniciarPartidaAposAnuncio();
    }

    // =====================================================
    // 10. ABERTURA TERRITORIAL MULTIPARTICIPANTE E ANÚNCIO DO ROUND 1
    // =====================================================

    private void InicializarSelecaoTerritorial()
    {
        gerenciadorRodada.PrepararPrioridadeRoundUm();
        IReadOnlyList<TerritorioClique.Dono> priority =
            gerenciadorRodada.ObterPrioridadeJogadores();
        var playerIds = new List<string>(priority.Count);
        foreach (TerritorioClique.Dono owner in priority)
            if (WDMatchSetupContext.TryGetParticipant(owner, out WDMatchParticipant participant))
                playerIds.Add(participant.PlayerId);

        var territoryIds = new List<string>();
        foreach (TerritorioClique territory in MapaAtivo.ObterTerritoriosOuCena())
            if (territory != null)
            {
                territory.DefinirNeutro();
                territoryIds.Add(territory.idTerritorio);
            }

        initialSelectionSession = new WDInitialTerritorySelectionSession(
            MatchSetupAtual.DeterministicSeed, playerIds, territoryIds);
        FeedbackSelecaoInicial = "ESCOLHA SEU PRIMEIRO TERRITÓRIO";
        GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
    }

    private void RegistrarEscolhaInicialLocal(TerritorioClique territory)
    {
        if (!territory.IsNeutral || !initialSelectionSession.IsAvailable(territory.idTerritorio))
            return;
        string localId = ObterIdJogadorLocal();
        if (!initialSelectionSession.SelectPending(localId, territory.idTerritorio))
            return;

        LimparDestaqueEscolhaInicial(territory.idTerritorio);
        territory.DestacarSelecao();
        FeedbackSelecaoInicial = $"SELEÇÃO PROVISÓRIA: {NomeTerritorioLegivel(territory.idTerritorio)}";
        GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
    }

    public void CancelarEscolhaInicialProvisoria()
    {
        if (!EmSelecaoInicial)
            return;
        string localId = ObterIdJogadorLocal();
        string territoryId = initialSelectionSession.GetPending(localId);
        if (!initialSelectionSession.CancelPending(localId))
            return;
        LimparDestaqueEscolhaInicial();
        FeedbackSelecaoInicial = initialSelectionSession.Stage == 1
            ? "ESCOLHA SEU PRIMEIRO TERRITÓRIO"
            : "ESCOLHA SEU SEGUNDO TERRITÓRIO";
        GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
    }

    public void ConfirmarEscolhaInicialProvisoria()
    {
        if (!EmSelecaoInicial)
            return;
        string localId = ObterIdJogadorLocal();
        string localChoice = initialSelectionSession.GetPending(localId);
        if (!initialSelectionSession.ConfirmPending(localId))
            return;

        LimparDestaqueEscolhaInicial();

        // Participantes simulados confirmam individualmente. A sessão resolve
        // colisões na mesma ordem autoritativa usada pela rodada.
        foreach (WDMatchParticipant participant in MatchSetupAtual.Participants)
        {
            if (participant.Kind == WDMatchParticipantKind.Local)
                continue;
            string mockChoice = initialSelectionSession.PickMockIntent(localChoice);
            initialSelectionSession.SubmitIntent(participant.PlayerId, mockChoice);
        }
        WDInitialTerritoryStageResult result = initialSelectionSession.ResolveStage();
        AplicarResultadoEscolhaInicial(result);
    }

    private void LimparDestaqueEscolhaInicial(string preservarId = null)
    {
        foreach (TerritorioClique candidate in MapaAtivo.ObterTerritoriosOuCena())
            if (candidate != null && candidate.idTerritorio != preservarId)
                candidate.RemoverDestaqueSelecao();
    }

    private static string NomeTerritorioLegivel(string technicalId)
    {
        if (string.IsNullOrEmpty(technicalId))
            return string.Empty;
        var output = new System.Text.StringBuilder(technicalId.Length + 8);
        for (int i = 0; i < technicalId.Length; i++)
        {
            char current = technicalId[i];
            if (i > 0 && char.IsUpper(current) &&
                (char.IsLower(technicalId[i - 1]) ||
                 (i + 1 < technicalId.Length && char.IsLower(technicalId[i + 1]))))
                output.Append(' ');
            output.Append(current);
        }
        return output.ToString();
    }

    private void AplicarResultadoEscolhaInicial(WDInitialTerritoryStageResult result)
    {
        if (result == null)
            return;
        foreach (WDInitialTerritoryAssignment assignment in result.Assignments)
        {
            WDMatchParticipant participant = null;
            foreach (WDMatchParticipant candidate in MatchSetupAtual.Participants)
                if (candidate.PlayerId == assignment.PlayerId)
                    participant = candidate;
            if (participant == null || MapaAtivo.Instance == null ||
                !MapaAtivo.Instance.TentarObterTerritorio(
                    assignment.TerritoryId, out TerritorioClique territory))
                continue;
            territory.DefinirDono((TerritorioClique.Dono)(participant.SlotIndex + 1));
            territory.DefinirTropas(1);
            territory.DestacarReforco();
            Debug.Log($"ABERTURA | Etapa {result.Stage} | {assignment.PlayerId} recebeu " +
                $"{assignment.TerritoryId}" + (assignment.WasFallback ? " por fallback." : "."));
        }

        if (initialSelectionSession.IsComplete)
        {
            LimparDestaqueEscolhaInicial();
            FeedbackSelecaoInicial = "TERRITÓRIOS INICIAIS DEFINIDOS";
            IniciarPartidaAposAnuncio();
        }
        else
            FeedbackSelecaoInicial = "ESCOLHA SEU SEGUNDO TERRITÓRIO";
        GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
    }

    private void IniciarPartidaAposAnuncio()
    {
        FeedbackSelecaoInicial = string.Empty;
        bloqueioInicioPartida = true;
        if (uiCompositionRoot == null)
            uiCompositionRoot = GetComponent<UICompositionRoot>();
        if (uiCompositionRoot != null)
            uiCompositionRoot.ExibirInicioRound(1, LiberarEIniciarPartida);
        else
            LiberarEIniciarPartida();
    }

    private void LiberarEIniciarPartida()
    {
        bloqueioInicioPartida = false;
        gerenciadorRodada.IniciarPartida();
    }

    // =====================================================
    // 11. PARTICIPANTES AUTORITATIVOS DO MATCH SETUP
    // =====================================================

    private void AplicarParticipantesDoMatchSetupAoTabuleiro()
    {
        WDMatchSetup setup = MatchSetupAtual;
        if (setup == null || setup.Participants.Count == 0)
            return;

        var owners = new List<TerritorioClique.Dono>();
        foreach (WDMatchParticipant participant in setup.Participants)
        {
            int ownerValue = participant.SlotIndex + 1;
            if (System.Enum.IsDefined(typeof(TerritorioClique.Dono), ownerValue))
                owners.Add((TerritorioClique.Dono)ownerValue);
        }

        if (owners.Count == 0)
            return;

        foreach (TerritorioClique territory in MapaAtivo.ObterTerritoriosOuCena())
        {
            if (territory == null || territory.dono == TerritorioClique.Dono.Neutro)
                continue;

            int serializedSlot = Mathf.Max(0, (int)territory.dono - 1);
            TerritorioClique.Dono expectedOwner = owners[serializedSlot % owners.Count];
            if (territory.dono != expectedOwner)
                territory.DefinirDono(expectedOwner);
            else
                territory.AtualizarCor();
        }

        WDMatchParticipant local = null;
        foreach (WDMatchParticipant participant in setup.Participants)
            if (participant.Kind == WDMatchParticipantKind.Local)
                local = participant;
        if (local != null)
            jogadorLocal = (TerritorioClique.Dono)(local.SlotIndex + 1);
    }

    // =====================================================
    // 11. CICLO DA PREPARAÇÃO
    // =====================================================

    private void Update()
    {
        if (bloqueioInicioPartida || EmSelecaoInicial)
            return;

        if (faseAtual != FaseTurno.Preparacao)
            return;

        tempoPreparacaoRestante -=
            Time.deltaTime;

        if (tempoPreparacaoRestante <= 0f)
        {
            tempoPreparacaoRestante = 0f;

            FecharPreparacaoEIniciarResolucao();
        }
    }

    public void IniciarPreparacaoRound()
    {
        faseAtual = FaseTurno.Preparacao;
        estadoPreparacao = EstadoPreparacao.Preparando;

        tempoPreparacaoRestante =
            duracaoPreparacao;

        modoAcao =
            ModoAcao.AcaoTerrestre;

        quantidadeAcaoSelecionada = 1;

        territorioTransferenciaSelecionado = null;
        feedbackTransferencia = string.Empty;
        filaTransferencias?.Limpar();
        jogadoresQueEnviaram.Clear();

        EntregarRandomCardsDoRound();
        AtualizarWeaponRuntimeParaRound();

        CancelarSelecaoAtual();

    }

    // =====================================================
    // 12. RANDOM CARDS SIMÉTRICO E DETERMINÍSTICO
    // =====================================================

    private void InicializarRandomCards()
    {
        if (!UsaRandomCards)
            return;

        WDRandomCardsConfig config =
            Resources.Load<WDRandomCardsConfig>("Cartas/WDRandomCardsConfig");
        if (config == null)
        {
            Debug.LogWarning("[Random Cards] Configuração não encontrada.");
            return;
        }

        var playerIds = new List<string>();
        foreach (WDMatchParticipant participant in MatchSetupAtual.Participants)
            playerIds.Add(participant.PlayerId);
        randomCardsSession = new WDRandomCardsSession(
            MatchSetupAtual.DeterministicSeed, config.Cards, playerIds);
    }

    private void EntregarRandomCardsDoRound()
    {
        if (randomCardsSession == null)
            return;

        WDRandomCardsRoundResult result =
            randomCardsSession.ProcessRound(RodadaAtual);
        if (result.DrawnCards.Count == 0)
        {
            Debug.Log($"[Random Cards] Round {RodadaAtual}: pool elegível esgotado.");
            return;
        }

        Debug.Log($"[Random Cards] Round {RodadaAtual}: " +
            $"sorteio comum [{string.Join(", ", result.DrawnCards)}].");
        foreach (KeyValuePair<string, bool> delivery in result.DeliveredByPlayer)
        {
            int occupied = 0;
            IReadOnlyList<string> hand = randomCardsSession.GetHand(delivery.Key);
            foreach (string cardId in hand)
                if (!string.IsNullOrEmpty(cardId))
                    occupied++;
            if (delivery.Value)
                Debug.Log($"[Random Cards] {delivery.Key}: recebeu; mão {occupied}/8.");
            else
                Debug.Log($"[Random Cards] {delivery.Key}: mão cheia; entrega perdida.");
        }
    }

    public bool ConsumirCartaRandomJogadorLocal(int slotIndex) =>
        !bloqueioInicioPartida && !EmSelecaoInicial && randomCardsSession != null &&
        randomCardsSession.Consume(ObterIdJogadorLocal(), slotIndex);

    // =====================================================
    // 13. RUNTIME DE ENERGY, COOLDOWN E LOADOUT SEM EXECUÇÃO
    // =====================================================

    private void InicializarWeaponRuntime()
    {
        WDMatchSetup setup = MatchSetupAtual;
        if (setup == null)
            return;

        WDArmoryCatalogConfig catalog =
            Resources.Load<WDArmoryCatalogConfig>("UI/Home/WDArmoryCatalogConfig");
        if (catalog == null)
        {
            Debug.LogError("Catálogo canônico de armas não encontrado.");
            return;
        }

        var playerIds = new List<string>();
        foreach (WDMatchParticipant participant in setup.Participants)
            playerIds.Add(participant.PlayerId);

        weaponRuntime = new WDWeaponMatchRuntime(
            catalog,
            playerIds,
            new WDMatchSetupWeaponLoadoutSource(setup, ObterCartasRandom),
            new WDUnavailableWeaponLevelSource());
        weaponRuntime.RefreshLoadouts();
    }

    private void AtualizarWeaponRuntimeParaRound()
    {
        if (weaponRuntime == null)
            return;

        weaponRuntime.RefreshLoadouts();
        weaponRuntime.AdvanceCooldownsForRound(RodadaAtual);
    }

    private IReadOnlyList<string> ObterCartasRandom(string playerId) =>
        randomCardsSession != null
            ? randomCardsSession.GetHand(playerId)
            : System.Array.Empty<string>();

    public WDWeaponAvailability ConsultarDisponibilidadeArma(
        string playerId, string weaponId) =>
        weaponRuntime != null
            ? weaponRuntime.GetAvailability(playerId, weaponId)
            : new WDWeaponAvailability(WDWeaponAvailabilityReason.PlayerNotFound);

    private string ObterIdJogadorLocal()
    {
        if (MatchSetupAtual != null)
            foreach (WDMatchParticipant participant in MatchSetupAtual.Participants)
                if (participant.Kind == WDMatchParticipantKind.Local)
                    return participant.PlayerId;
        return string.Empty;
    }

    // =====================================================
    // 10. DISTRIBUIÇÃO DE REFORÇOS
    // =====================================================

    public void DefinirReforcos(
        int quantidade)
    {
        reforcosDisponiveis =
            Mathf.Max(
                0,
                quantidade
            );

        historicoReforcos.Limpar();
    }

    public bool PodeAdicionarReforcoEm(
        TerritorioClique territorio)
    {
        return
            PodeEditarPreparacao &&
            territorio != null &&
            territorio.dono == jogadorLocal &&
            reforcosDisponiveis > 0;
    }

    public void TentarAdicionarReforco(
        TerritorioClique territorio)
    {
        DistribuirReforcos(territorio, 1);
    }

    public int DistribuirReforcos(
        TerritorioClique territorio,
        int quantidade)
    {
        if (!PodeAdicionarReforcoEm(territorio))
            return 0;

        int quantidadeAplicada =
            Mathf.Clamp(
                quantidade,
                0,
                reforcosDisponiveis
            );

        if (quantidadeAplicada <= 0)
            return 0;

        for (int i = 0; i < quantidadeAplicada; i++)
        {
            territorio.AdicionarTropa();
        }

        reforcosDisponiveis -= quantidadeAplicada;
        historicoReforcos.Registrar(
            territorio,
            quantidadeAplicada
        );

        return quantidadeAplicada;
    }

    public bool DesfazerDistribuicaoReforco(int id)
    {
        if (!PodeEditarPreparacao)
            return false;

        if (!historicoReforcos.TentarObter(
                id,
                out DistribuicaoReforco distribuicao))
            return false;

        if (distribuicao.Territorio == null ||
            !distribuicao.Territorio.RemoverTropas(
                distribuicao.Quantidade))
        {
            return false;
        }

        historicoReforcos.Remover(id);
        reforcosDisponiveis += distribuicao.Quantidade;
        return true;
    }

    // =====================================================
    // 11. SELEÇÃO DE MODO
    // =====================================================

    public void SelecionarModoAcaoTerrestre()
    {
        if (!PodeEditarPreparacao)
            return;

        modoAcao =
            ModoAcao.AcaoTerrestre;

        CancelarSelecaoTransferencia();
        CancelarSelecaoAtual();
    }

    public void SelecionarModoTransferencia()
    {
        if (!PodeEditarPreparacao)
            return;

        if (!TransferenciaDisponivelParaJogadorLocal)
        {
            feedbackTransferencia = "Transferência indisponível: não há aliado nesta partida.";
            modoAcao = ModoAcao.AcaoTerrestre;
            CancelarSelecaoTransferencia();
            return;
        }

        if (LimiteTransferenciaAtingido)
        {
            feedbackTransferencia = "Limite de 1 transferência atingido.";
            return;
        }

        modoAcao = ModoAcao.Transferir;
        feedbackTransferencia =
            "Selecione um território seu para transferir ao aliado.";

        CancelarSelecaoTransferencia();
        CancelarSelecaoAtual();
    }

    // =====================================================
    // 12. QUANTIDADE DA AÇÃO
    // =====================================================

    public void AumentarQuantidadeAcao()
    {
        if (!PodeEditarPreparacao)
            return;

        int maximo =
            ObterMaximoDisponivelOrigem();

        if (maximo <= 0)
            return;

        quantidadeAcaoSelecionada =
            Mathf.Min(
                quantidadeAcaoSelecionada + 1,
                maximo
            );
    }

    public void DiminuirQuantidadeAcao()
    {
        if (!PodeEditarPreparacao)
            return;

        quantidadeAcaoSelecionada =
            Mathf.Max(
                1,
                quantidadeAcaoSelecionada - 1
            );
    }

    private int ObterMaximoDisponivelOrigem()
    {
        if (territorioSelecionado == null ||
            filaAcoes == null)
        {
            return 999;
        }

        return filaAcoes
            .TropasDisponiveis(
                territorioSelecionado,
                jogadorLocal
            );
    }

    // =====================================================
    // 13. SELEÇÃO NO MAPA
    // =====================================================

    public void ClicarTerritorio(
        TerritorioClique territorio)
    {
        if (territorio == null)
            return;

        if (EmSelecaoInicial)
        {
            RegistrarEscolhaInicialLocal(territorio);
            return;
        }

        if (!PodeEditarPreparacao)
            return;

        if (modoAcao == ModoAcao.Transferir)
        {
            ClicarTerritorioTransferencia(territorio);
            return;
        }

        if (modoAcao !=
            ModoAcao.AcaoTerrestre)
            return;

        if (reforcosDisponiveis > 0)
            return;

        if (LimiteAcoesAtingido)
        {
            CancelarSelecaoAtual();
            return;
        }

        // =================================================
        // ESCOLHER ORIGEM
        // =================================================

        if (territorioSelecionado == null)
        {
            if (territorio.dono != jogadorLocal)
                return;

            int disponiveis =
                filaAcoes
                    .TropasDisponiveis(
                        territorio,
                        jogadorLocal
                    );

            if (disponiveis <= 0)
                return;

            territorioSelecionado =
                territorio;

            territorioSelecionado
                .DestacarSelecao();

            quantidadeAcaoSelecionada =
                disponiveis;

            AtualizarHighlightsPreparacao();

            return;
        }

        // =================================================
        // CANCELAR ORIGEM
        // =================================================

        if (territorio ==
            territorioSelecionado)
        {
            CancelarSelecaoAtual();
            return;
        }

        // =================================================
        // TROCAR ORIGEM (antes de existir destino)
        // =================================================

        if (territorio.dono == jogadorLocal &&
            territorioDestinoSelecionado == null &&
            !territorioSelecionado.EhVizinho(territorio))
        {
            territorioSelecionado
                .RemoverDestaqueSelecao();

            if (territorioDestinoSelecionado != null)
            {
                territorioDestinoSelecionado
                    .RemoverDestaqueSelecao();
            }

            territorioSelecionado =
                territorio;

            territorioDestinoSelecionado =
                null;

            territorioSelecionado
                .DestacarSelecao();

            int disponiveis =
                filaAcoes
                    .TropasDisponiveis(
                        territorio,
                        jogadorLocal
                    );

            if (disponiveis <= 0)
            {
                CancelarSelecaoAtual();
                return;
            }

            quantidadeAcaoSelecionada =
                Mathf.Max(
                    1,
                    disponiveis
                );

            AtualizarHighlightsPreparacao();

            return;
        }

        // =================================================
        // ESCOLHER DESTINO
        // =================================================

        if (!sistemaAcoesTerrestres.PodePreparar(
                territorioSelecionado,
                territorio,
                jogadorLocal))
        {
            return;
        }

        if (territorioDestinoSelecionado != null)
        {
            territorioDestinoSelecionado
                .RemoverDestaqueSelecao();
        }

        territorioDestinoSelecionado =
            territorio;

        territorioDestinoSelecionado
            .DestacarSelecao();

        int disponiveisOrigem =
            filaAcoes
                .TropasDisponiveis(
                    territorioSelecionado,
                    jogadorLocal
                );

        quantidadeAcaoSelecionada =
            Mathf.Clamp(
                quantidadeAcaoSelecionada,
                1,
                disponiveisOrigem
            );

        AtualizarHighlightsPreparacao();

    }

    // =====================================================
    // 14. REGISTRO DE AÇÃO TERRESTRE
    // =====================================================

    public void ConfirmarAcaoPreparada()
    {
        if (!PodeEditarPreparacao)
            return;

        if (reforcosDisponiveis > 0)
            return;

        if (territorioSelecionado == null ||
            territorioDestinoSelecionado == null)
            return;

        bool registrado =
            sistemaAcoesTerrestres
                .Registrar(
                    territorioSelecionado,
                    territorioDestinoSelecionado,
                    quantidadeAcaoSelecionada,
                    jogadorLocal
                );

        if (!registrado)
            return;

        CancelarSelecaoAtual();

        quantidadeAcaoSelecionada = 1;
    }

    // =====================================================
    // 15. CANCELAMENTO DE SELEÇÃO
    // =====================================================

    public void CancelarSelecaoAtual()
    {
        territorioSelecionado = null;
        territorioDestinoSelecionado = null;

        AtualizarHighlightsPreparacao();
    }

    public bool PodeSelecionarDestinoParaUI(TerritorioClique territorio)
    {
        if (!PodeEditarPreparacao || modoAcao != ModoAcao.AcaoTerrestre ||
            territorioSelecionado == null || territorio == null ||
            territorio == territorioSelecionado || filaAcoes == null)
        {
            return false;
        }

        return territorioSelecionado.dono == jogadorLocal &&
            territorioSelecionado.EhVizinho(territorio) &&
            filaAcoes.TropasDisponiveis(territorioSelecionado, jogadorLocal) > 0;
    }

    // =====================================================
    // 16. EDIÇÃO DE ORDENS
    // =====================================================

    public void CancelarUltimaOrdem()
    {
        if (!PodeEditarPreparacao ||
            filaAcoes == null)
            return;

        if (filaAcoes.RemoverUltima(jogadorLocal))
            AtualizarHighlightsPreparacao();
    }

    public bool CancelarOrdem(OrdemTerrestre ordem)
    {
        if (!PodeEditarPreparacao || filaAcoes == null ||
            ordem == null || ordem.Jogador != jogadorLocal)
            return false;

        bool removida = filaAcoes.Remover(ordem);

        if (removida)
            AtualizarHighlightsPreparacao();

        return removida;
    }

    // =====================================================
    // 17. PREPARAÇÃO DA TRANSFERÊNCIA
    // =====================================================

    private void ClicarTerritorioTransferencia(TerritorioClique territorio)
    {
        if (LimiteTransferenciaAtingido)
        {
            feedbackTransferencia = "Limite de 1 transferência atingido.";
            CancelarSelecaoTransferencia();
            return;
        }

        if (territorio.dono != jogadorLocal)
        {
            feedbackTransferencia = "Selecione um território que pertence a você.";
            territorioTransferenciaSelecionado = null;
            AtualizarHighlightsPreparacao();
            return;
        }

        territorioTransferenciaSelecionado = territorio;
        destinatarioTransferenciaSelecionado = TerritorioClique.Dono.Neutro;
        IReadOnlyList<TerritorioClique.Dono> destinatarios =
            ObterDestinatariosTransferencia();

        if (destinatarios.Count == 1)
        {
            feedbackTransferencia = "Território selecionado. Confirme a transferência.";
            AtualizarHighlightsPreparacao();
            return;
        }

        if (destinatarios.Count > 1)
        {
            feedbackTransferencia = "Território selecionado. Escolha o aliado destinatário.";
            AtualizarHighlightsPreparacao();
            return;
        }

        feedbackTransferencia = "Transferência indisponível: nenhum aliado encontrado.";
        AtualizarHighlightsPreparacao();
    }

    public void ConfirmarTransferenciaPreparada()
    {
        if (!PodeConfirmarTransferencia)
            return;

        IReadOnlyList<TerritorioClique.Dono> destinatarios =
            ObterDestinatariosTransferencia();
        if (destinatarios.Count == 0)
        {
            feedbackTransferencia = "Transferência indisponível: nenhum aliado encontrado.";
            return;
        }

        if (destinatarios.Count > 1 &&
            !destinatarios.Contains(destinatarioTransferenciaSelecionado))
        {
            feedbackTransferencia = "Escolha um aliado antes de confirmar a transferência.";
            return;
        }

        TentarPrepararTransferenciaPara(
            destinatarios.Count == 1
                ? destinatarios[0]
                : destinatarioTransferenciaSelecionado);
    }

    public void SelecionarDestinatarioTransferencia(
        TerritorioClique.Dono jogador)
    {
        if (!EmModoTransferencia || territorioTransferenciaSelecionado == null)
            return;

        IReadOnlyList<TerritorioClique.Dono> destinatarios =
            ObterDestinatariosTransferencia();
        if (!destinatarios.Contains(jogador))
        {
            feedbackTransferencia = "Selecione um aliado válido.";
            return;
        }

        destinatarioTransferenciaSelecionado = jogador;
        feedbackTransferencia = $"Destinatário: {jogador}. Confirme a transferência.";
    }

    private void TentarPrepararTransferenciaPara(
        TerritorioClique.Dono jogador)
    {
        if (!EmModoTransferencia || sistemaTransferencias == null)
            return;

        if (territorioTransferenciaSelecionado == null)
        {
            feedbackTransferencia = "Selecione primeiro um território seu.";
            return;
        }

        bool registrada = sistemaTransferencias.Registrar(
            territorioTransferenciaSelecionado,
            jogadorLocal,
            jogador,
            out string motivo);

        feedbackTransferencia = motivo;

        if (!registrada)
            return;

        territorioTransferenciaSelecionado = null;
        destinatarioTransferenciaSelecionado = TerritorioClique.Dono.Neutro;
        modoAcao = ModoAcao.AcaoTerrestre;
        AtualizarHighlightsPreparacao();
    }

    public IReadOnlyList<TerritorioClique.Dono> ObterJogadoresTransferencia()
    {
        return gerenciadorRodada != null
            ? gerenciadorRodada.ObterJogadoresNoTabuleiro()
            : new List<TerritorioClique.Dono>();
    }

    // =====================================================
    // 18. DESTINATÁRIOS VÁLIDOS DO MATCH SETUP
    // =====================================================

    public IReadOnlyList<TerritorioClique.Dono> ObterDestinatariosTransferencia()
    {
        var destinatarios = new List<TerritorioClique.Dono>();
        WDMatchSetup setup = MatchSetupAtual;

        if (setup != null)
        {
            destinatarios.AddRange(
                EquipesJogadores.ObterAliados(setup, jogadorLocal));
            return destinatarios;
        }

        foreach (TerritorioClique.Dono jogador in ObterJogadoresTransferencia())
        {
            if (jogador != jogadorLocal &&
                EquipesJogadores.SaoAliados(jogadorLocal, jogador))
            {
                destinatarios.Add(jogador);
            }
        }

        return destinatarios;
    }

    public void RemoverTransferencia()
    {
        if (!PodeEditarPreparacao || filaTransferencias == null)
            return;

        if (!filaTransferencias.RemoverPara(jogadorLocal))
            return;

        feedbackTransferencia = "Transferência removida.";
        AtualizarHighlightsPreparacao();
    }

    public void CancelarSelecaoTransferencia()
    {
        territorioTransferenciaSelecionado = null;
        destinatarioTransferenciaSelecionado = TerritorioClique.Dono.Neutro;
        AtualizarHighlightsPreparacao();
    }

    // =====================================================
    // 18. ENVIO LOCAL
    // =====================================================

    public void EnviarAcoes()
    {
        if (!PodeEditarPreparacao || reforcosDisponiveis > 0 ||
            !TodosTerritoriosControladosPossuemTropa)
            return;

        estadoPreparacao =
            EstadoPreparacao.Enviado;
        jogadoresQueEnviaram.Add(jogadorLocal);

        CancelarSelecaoTransferencia();
        CancelarSelecaoAtual();

#if UNITY_EDITOR
        if (autoenviarJogadoresSimuladosEditor)
        {
            foreach (TerritorioClique.Dono jogador in ObterParticipantesEsperados())
                jogadoresQueEnviaram.Add(jogador);
            Debug.Log(
                "TESTE LOCAL | Jogadores simulados considerados enviados; " +
                "seguindo pelo fechamento real da preparação.");
            if (TodosParticipantesEnviaram())
                FecharPreparacaoEIniciarResolucao();
        }
#endif
    }

    public void CancelarEnvio()
    {
        if (!PodeCancelarEnvio)
            return;

        estadoPreparacao =
            EstadoPreparacao.Preparando;
        jogadoresQueEnviaram.Remove(jogadorLocal);
    }

    // =====================================================
    // 19. SUBMISSÃO INDIVIDUAL DOS PARTICIPANTES
    // =====================================================

    private IReadOnlyList<TerritorioClique.Dono> ObterParticipantesEsperados()
    {
        var participantes = new List<TerritorioClique.Dono>();
        WDMatchSetup setup = MatchSetupAtual;
        if (setup == null)
        {
            participantes.Add(jogadorLocal);
            return participantes;
        }

        foreach (WDMatchParticipant participant in setup.Participants)
        {
            int ownerValue = participant.SlotIndex + 1;
            if (System.Enum.IsDefined(typeof(TerritorioClique.Dono), ownerValue))
                participantes.Add((TerritorioClique.Dono)ownerValue);
        }
        return participantes;
    }

    private bool TodosParticipantesEnviaram()
    {
        IReadOnlyList<TerritorioClique.Dono> esperados = ObterParticipantesEsperados();
        if (esperados.Count == 0)
            return false;
        foreach (TerritorioClique.Dono jogador in esperados)
            if (!jogadoresQueEnviaram.Contains(jogador))
                return false;
        return true;
    }

    // =====================================================
    // 20. FECHAMENTO DA PREPARAÇÃO
    // =====================================================

    public void FecharPreparacaoEIniciarResolucao()
    {
        if (faseAtual != FaseTurno.Preparacao ||
            estadoPreparacao == EstadoPreparacao.Resolvendo)
        {
            return;
        }

        estadoPreparacao =
            EstadoPreparacao.Resolvendo;

        eventosReforcoPendentes = CriarEventosVisuaisReforco(
            historicoReforcos.Distribuicoes);
        reforcosDisponiveis = 0;
        historicoReforcos.Limpar();

        CancelarSelecaoTransferencia();
        CancelarSelecaoAtual();

        ResolverRodadaAgora();
    }

    // =====================================================
    // 21. RESOLUÇÃO
    // =====================================================

    public void ResolverRodadaAgora()
    {
        if (filaAcoes == null ||
            resolvedorAcoesTerrestres == null ||
            filaTransferencias == null ||
            resolvedorTransferencias == null ||
            gerenciadorRodada == null)
        {
            return;
        }

        faseAtual =
            FaseTurno.Resolucao;

        estadoPreparacao =
            EstadoPreparacao.Resolvendo;

        gerenciadorRodada.NotificarInicioResolucao();
        GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();

        List<ResultadoTransferencia> resultadosTransferencias =
            resolvedorTransferencias.Resolver(
            filaTransferencias,
            gerenciadorRodada.ObterPrioridadeJogadores());

        List<ResultadoAcaoTerrestre> resultadosTerrestres =
            resolvedorAcoesTerrestres.Resolver(
            filaAcoes,
            gerenciadorRodada.ObterPrioridadeJogadores());

        CancelarSelecaoAtual();

        var eventosVisuais = new List<ResolutionVisualEvent>();
        eventosVisuais.AddRange(eventosReforcoPendentes);
        eventosVisuais.AddRange(
            CriarEventosVisuaisTransferencia(resultadosTransferencias));
        eventosVisuais.AddRange(CriarEventosVisuaisAcoesTerrestres(resultadosTerrestres));
        eventosReforcoPendentes.Clear();

        ResolutionSequenceController controller =
            FindAnyObjectByType<ResolutionSequenceController>();
        if (controller != null)
        {
            sequenciaResolucaoVisualAtiva = controller;
            controller.SequenceCompleted -= ConcluirResolucaoAposVisual;
            controller.SequenceCompleted += ConcluirResolucaoAposVisual;
            controller.PlayAfterAnnouncement(eventosVisuais);
            return;
        }

        gerenciadorRodada.ConcluirResolucao();
    }

    private static List<ResolutionVisualEvent> CriarEventosVisuaisAcoesTerrestres(
        IReadOnlyList<ResultadoAcaoTerrestre> resultados)
    {
        var eventos = new List<ResolutionVisualEvent>();
        if (resultados == null)
            return eventos;

        foreach (ResultadoAcaoTerrestre resultado in resultados)
        {
            if (resultado == null || !resultado.Executada || resultado.Ordem == null)
            {
                continue;
            }

            if (resultado.Tipo == ResultadoAcaoTerrestre.TipoResultado.Ataque &&
                resultado.Combate != null)
            {
                eventos.Add(ResolutionVisualEvent.Attack(
                    resultado.Ordem.Jogador,
                    resultado.Ordem.IdOrigem,
                    resultado.Ordem.IdDestino,
                    resultado.QuantidadeEfetiva,
                    resultado.TropasOrigemAntes,
                    resultado.TropasOrigemDepois,
                    resultado.TropasDestinoAntes,
                    resultado.TropasDestinoDepois,
                    resultado.Combate.Conquistou,
                    resultado.DonoDestinoAntes,
                    resultado.DonoDestinoDepois));
                continue;
            }

            eventos.Add(ResolutionVisualEvent.Movement(
                resultado.Ordem.Jogador,
                resultado.Ordem.IdOrigem,
                resultado.Ordem.IdDestino,
                resultado.QuantidadeEfetiva,
                resultado.TropasOrigemAntes,
                resultado.TropasOrigemDepois,
                resultado.TropasDestinoAntes,
                resultado.TropasDestinoDepois,
                resultado.DonoDestinoAntes,
                resultado.DonoDestinoDepois));
        }

        return eventos;
    }

    // =====================================================
    // 21. SNAPSHOTS VISUAIS DE REFORÇO E TRANSFERÊNCIA
    // =====================================================

    private static List<ResolutionVisualEvent> CriarEventosVisuaisReforco(
        IReadOnlyList<DistribuicaoReforco> distribuicoes)
    {
        var eventos = new List<ResolutionVisualEvent>();
        if (distribuicoes == null)
            return eventos;

        var totais = new Dictionary<TerritorioClique, int>();
        foreach (DistribuicaoReforco distribuicao in distribuicoes)
        {
            if (distribuicao?.Territorio == null || distribuicao.Quantidade <= 0)
                continue;
            totais.TryGetValue(distribuicao.Territorio, out int total);
            totais[distribuicao.Territorio] = total + distribuicao.Quantidade;
        }

        var valores = new Dictionary<TerritorioClique, int>();
        foreach (KeyValuePair<TerritorioClique, int> item in totais)
            valores[item.Key] = Mathf.Max(0, item.Key.Tropas - item.Value);

        foreach (DistribuicaoReforco distribuicao in distribuicoes)
        {
            TerritorioClique territorio = distribuicao?.Territorio;
            if (territorio == null || distribuicao.Quantidade <= 0)
                continue;

            int antes = valores[territorio];
            int depois = antes + distribuicao.Quantidade;
            valores[territorio] = depois;
            eventos.Add(ResolutionVisualEvent.Reinforcement(
                territorio.dono,
                territorio.idTerritorio,
                distribuicao.Quantidade,
                antes,
                depois));
        }

        return eventos;
    }

    private static List<ResolutionVisualEvent> CriarEventosVisuaisTransferencia(
        IReadOnlyList<ResultadoTransferencia> resultados)
    {
        var eventos = new List<ResolutionVisualEvent>();
        if (resultados == null)
            return eventos;

        foreach (ResultadoTransferencia resultado in resultados)
        {
            if (resultado == null || !resultado.Executada)
                continue;

            eventos.Add(ResolutionVisualEvent.TerritoryHandoff(
                resultado.DonoAntes,
                resultado.DonoDepois,
                resultado.IdTerritorio,
                resultado.TropasAntes,
                resultado.TropasDepois));
        }

        return eventos;
    }

    private void ConcluirResolucaoAposVisual()
    {
        if (sequenciaResolucaoVisualAtiva != null)
            sequenciaResolucaoVisualAtiva.SequenceCompleted -= ConcluirResolucaoAposVisual;
        sequenciaResolucaoVisualAtiva = null;
        gerenciadorRodada?.ConcluirResolucao();
    }

    private IReadOnlyList<OrdemTerrestre> ObterOrdensDoJogadorLocal()
    {
        List<OrdemTerrestre> locais = new List<OrdemTerrestre>();

        foreach (OrdemTerrestre ordem in filaAcoes.Ordens)
        {
            if (ordem.Jogador == jogadorLocal)
                locais.Add(ordem);
        }

        locais.Sort((a, b) =>
            a.PosicaoNaFila.CompareTo(b.PosicaoNaFila));

        return locais;
    }

    // =====================================================
    // 21. HIGHLIGHTS DERIVADOS DAS FILAS
    // =====================================================

    private void AtualizarHighlightsPreparacao()
    {
        foreach (TerritorioClique territorio in MapaAtivo.ObterTerritoriosOuCena())
        {
            if (DeveManterHighlight(territorio))
                territorio.DestacarSelecao();
            else
                territorio.RemoverDestaqueSelecao();
        }
    }

    private bool DeveManterHighlight(TerritorioClique territorio)
    {
        if (territorio == null)
            return false;

        bool preparacaoAtiva =
            faseAtual == FaseTurno.Preparacao &&
            estadoPreparacao != EstadoPreparacao.Resolvendo;

        if (!preparacaoAtiva)
            return false;

        if (territorio == territorioSelecionado ||
            territorio == territorioDestinoSelecionado ||
            territorio == territorioTransferenciaSelecionado)
            return true;

        if (filaAcoes == null)
            return false;

        foreach (OrdemTerrestre ordem in filaAcoes.Ordens)
        {
            if (ordem.Origem == territorio ||
                ordem.Destino == territorio)
                return true;
        }

        if (filaTransferencias != null)
        {
            foreach (OrdemTransferencia ordem in filaTransferencias.Ordens)
            {
                if (ordem.Territorio == territorio)
                    return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    // =====================================================
    // 22. ISOLAMENTO DOS CENÁRIOS DE TESTE DO EDITOR
    // =====================================================

    private bool resolucaoVisualTesteEditor;

    public bool PodeEntrarResolucaoTesteEditor =>
        Application.isPlaying && !PartidaEncerrada &&
        faseAtual == FaseTurno.Preparacao && gerenciadorRodada != null;

    public bool PodeAvancarResolucaoTesteEditor =>
        Application.isPlaying && !PartidaEncerrada &&
        resolucaoVisualTesteEditor && faseAtual == FaseTurno.Resolucao &&
        gerenciadorRodada != null;

    // =====================================================
    // 23. RESOLUÇÃO VISUAL MANUAL, SEM EXECUTAR ORDENS
    // =====================================================

    public void TesteEditorEntrarEmResolucao()
    {
        if (!PodeEntrarResolucaoTesteEditor)
            return;

        // Publica a preparação antes da transição mesmo entre dois polls da UI.
        MatchUIPresenter presenter = GetComponent<MatchUIPresenter>();
        presenter?.SolicitarSnapshotAtual();
        resolucaoVisualTesteEditor = true;
        faseAtual = FaseTurno.Resolucao;
        estadoPreparacao = EstadoPreparacao.Resolvendo;
        gerenciadorRodada.NotificarInicioResolucao();
        CancelarSelecaoTransferencia();
        CancelarSelecaoAtual();
        presenter?.SolicitarSnapshotAtual();
        // Update já interrompe o cronômetro fora de Preparacao.
        // Não chama ResolverRodadaAgora nem ConcluirResolucao.
    }

    public void TesteEditorAvancarParaProximoRound()
    {
        if (!PodeAvancarResolucaoTesteEditor)
            return;

        resolucaoVisualTesteEditor = false;
        // Ordens não executadas neste cenário não podem vazar para outro round.
        LimparPreparacaoParaTesteEditor();
        gerenciadorRodada.TesteEditorIniciarProximaRodada();
        GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
    }

    public void LimparPreparacaoParaTesteEditor()
    {
        filaAcoes?.Limpar();
        filaTransferencias?.Limpar();
        territorioSelecionado = null;
        territorioDestinoSelecionado = null;
        territorioTransferenciaSelecionado = null;
        AtualizarHighlightsPreparacao();
    }
#endif
}

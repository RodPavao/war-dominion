// ============================================================
// 01. CONTRATO DE COMANDOS DA INTERFACE
// ============================================================

public interface IMatchUICommands
{
    void SelecionarModoAcao();
    void SelecionarModoTransferencia();
    bool SelecionarTerritorio(string territorioId);
    int AdicionarReforco(string territorioId, int quantidade);
    void AumentarQuantidadeAcao();
    void DiminuirQuantidadeAcao();
    void ConfirmarAcao();
    void CancelarSelecao();
    void DesfazerUltimaAcao();
    bool RemoverAcao(int posicaoNaFila);
    bool RemoverDistribuicaoReforco(int distribuicaoId);
    void SelecionarDestinatarioTransferencia(TerritorioClique.Dono jogador);
    void ConfirmarTransferencia();
    void RemoverTransferencia();
    void CancelarSelecaoTransferencia();
    void EnviarAcoes();
    void CancelarEnvio();
    bool UsarCarta(int slotIndex);
}

// ============================================================
// 02. FACHADA SEM LÓGICA DE GAMEPLAY
// ============================================================

public sealed class MatchUICommands : IMatchUICommands
{
    private readonly GameManager gameManager;

    public MatchUICommands(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }

    public void SelecionarModoAcao()
    {
        gameManager?.SelecionarModoAcaoTerrestre();
        AtualizarApresentacao();
    }

    public void SelecionarModoTransferencia()
    {
        gameManager?.SelecionarModoTransferencia();
        AtualizarApresentacao();
    }

    public bool SelecionarTerritorio(string territorioId)
    {
        if (!TentarObterTerritorio(territorioId, out TerritorioClique territorio))
            return false;
        gameManager.ClicarTerritorio(territorio);
        AtualizarApresentacao();
        return true;
    }

    public int AdicionarReforco(string territorioId, int quantidade)
    {
        return TentarObterTerritorio(territorioId, out TerritorioClique territorio)
            ? gameManager.DistribuirReforcos(territorio, quantidade)
            : 0;
    }
    public void AumentarQuantidadeAcao()
    {
        gameManager?.AumentarQuantidadeAcao();
        AtualizarApresentacao();
    }

    public void DiminuirQuantidadeAcao()
    {
        gameManager?.DiminuirQuantidadeAcao();
        AtualizarApresentacao();
    }

    public void ConfirmarAcao()
    {
        gameManager?.ConfirmarAcaoPreparada();
        AtualizarApresentacao();
    }

    public void CancelarSelecao()
    {
        gameManager?.CancelarSelecaoAtual();
        AtualizarApresentacao();
    }

    public void DesfazerUltimaAcao()
    {
        gameManager?.CancelarUltimaOrdem();
        AtualizarApresentacao();
    }

    public bool RemoverAcao(int posicaoNaFila)
    {
        if (gameManager?.OrdensPreparadas == null)
            return false;

        foreach (OrdemTerrestre ordem in gameManager.OrdensPreparadas)
        {
            if (ordem.PosicaoNaFila == posicaoNaFila)
            {
                bool removed = gameManager.CancelarOrdem(ordem);
                AtualizarApresentacao();
                return removed;
            }
        }

        return false;
    }

    public bool RemoverDistribuicaoReforco(int distribuicaoId)
    {
        bool removed = gameManager != null && gameManager.DesfazerDistribuicaoReforco(distribuicaoId);
        AtualizarApresentacao();
        return removed;
    }

    public void SelecionarDestinatarioTransferencia(TerritorioClique.Dono jogador)
    { gameManager?.SelecionarDestinatarioTransferencia(jogador); AtualizarApresentacao(); }
    public void ConfirmarTransferencia()
    { gameManager?.ConfirmarTransferenciaPreparada(); AtualizarApresentacao(); }

    public void RemoverTransferencia()
    { gameManager?.RemoverTransferencia(); AtualizarApresentacao(); }
    public void CancelarSelecaoTransferencia()
    { gameManager?.CancelarSelecaoTransferencia(); AtualizarApresentacao(); }
    public void EnviarAcoes() => gameManager?.EnviarAcoes();
    public void CancelarEnvio() => gameManager?.CancelarEnvio();
    public bool UsarCarta(int slotIndex)
    {
        bool consumida = gameManager != null &&
            gameManager.ConsumirCartaRandomJogadorLocal(slotIndex);
        if (consumida)
            gameManager.GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();
        return consumida;
    }

    private void AtualizarApresentacao() =>
        gameManager?.GetComponent<MatchUIPresenter>()?.SolicitarSnapshotAtual();

    // ============================================================
    // 03. RESOLUÇÃO GENÉRICA POR ID, SEM REFERÊNCIAS DE VIEW
    // ============================================================

    private bool TentarObterTerritorio(
        string territorioId,
        out TerritorioClique territorio)
    {
        territorio = null;
        if (gameManager == null || string.IsNullOrWhiteSpace(territorioId))
            return false;

        if (MapaAtivo.Instance != null &&
            MapaAtivo.Instance.TentarObterTerritorio(territorioId, out territorio))
        {
            return territorio != null;
        }

        foreach (TerritorioClique candidato in MapaAtivo.ObterTerritoriosOuCena())
        {
            if (candidato != null && candidato.idTerritorio == territorioId)
            {
                territorio = candidato;
                return true;
            }
        }

        return false;
    }
}

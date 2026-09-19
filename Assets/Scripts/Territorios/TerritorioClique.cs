using UnityEngine;

public class TerritorioClique : MonoBehaviour
{
    private TerritorioVisual territorioVisual;
    private TerritorioContador territorioContador;
    private TerritorioTropas territorioTropas;
    private TerritorioFronteiras territorioFronteiras;

    // =====================================================
    // IDENTIFICAÇÃO
    // =====================================================

    public string idTerritorio;
    public string chaveTraducao;

    public enum Continente
    {
        AmericaDoNorte,
        AmericaDoSul,
        Africa,
        Europa,
        Asia,
        Oceania
    }

    public Continente continente;

    // =====================================================
    // JOGADOR
    // =====================================================

    public enum Dono
    {
        Neutro,
        Jogador1,
        Jogador2,
        Jogador3,
        Jogador4,
        Jogador5,
        Jogador6
    }

    public Dono dono = Dono.Neutro;

    public enum EstadoTerritorial
    {
        Ocupado,
        Neutro,
        Vazio
    }

    [SerializeField] private EstadoTerritorial estadoTerritorial =
        EstadoTerritorial.Neutro;

    public EstadoTerritorial Estado => estadoTerritorial;
    public bool IsNeutral => estadoTerritorial == EstadoTerritorial.Neutro;
    public bool IsEmpty => estadoTerritorial == EstadoTerritorial.Vazio;
    public bool IsOccupied => estadoTerritorial == EstadoTerritorial.Ocupado;
    public bool PossuiDono => IsOccupied && dono != Dono.Neutro;
    public bool UsaOverlaySemDono => IsNeutral || IsEmpty;

    // =====================================================
    // EXÉRCITO
    // =====================================================

    public int Tropas
    {
        get
        {
            if (territorioTropas == null)
                return 1;

            return territorioTropas.Quantidade;
        }
    }

    // =====================================================
    // INICIALIZAÇÃO
    // =====================================================

    private void Start()
    {
        // Migração segura dos dados legados serializados antes da existência
        // do estado territorial explícito. Vazio só é preservado quando foi
        // marcado explicitamente; ausência de dono nunca permanece Ocupado.
        if (dono != Dono.Neutro)
            estadoTerritorial = EstadoTerritorial.Ocupado;
        else if (estadoTerritorial != EstadoTerritorial.Vazio)
            estadoTerritorial = EstadoTerritorial.Neutro;

        territorioVisual =
            GetComponent<TerritorioVisual>();

        if (territorioVisual == null)
        {
            territorioVisual =
                gameObject.AddComponent<TerritorioVisual>();
        }

        territorioFronteiras =
            GetComponent<TerritorioFronteiras>();

        if (territorioFronteiras == null)
        {
            Debug.LogError(
                "TerritorioFronteiras ausente em " +
                name
            );
        }

        territorioContador =
            GetComponent<TerritorioContador>();

        if (territorioContador == null)
        {
            territorioContador =
                gameObject.AddComponent<TerritorioContador>();
        }

        territorioTropas =
            GetComponent<TerritorioTropas>();

        if (territorioTropas == null)
        {
            territorioTropas =
                gameObject.AddComponent<TerritorioTropas>();
        }

        territorioTropas.Inicializar();

        territorioContador.Inicializar();

        AtualizarIdentidadeVisual();
    }

    // =====================================================
    // DONO
    // =====================================================

    public void DefinirDono(
        Dono novoDono)
    {
        if (novoDono == Dono.Neutro)
        {
            DefinirNeutro();
            return;
        }

        dono = novoDono;
        estadoTerritorial = EstadoTerritorial.Ocupado;

        AtualizarIdentidadeVisual();
    }

    public void AtualizarIdentidadeVisual()
    {
        if (territorioVisual != null)
        {
            territorioVisual.AtualizarCor();
        }

        if (territorioContador != null)
        {
            territorioContador.Atualizar();
        }
    }

    // =====================================================
    // CLIQUE
    // =====================================================

    public void Clicar()
    {
        if (GameManager.instance == null)
        {
            Debug.LogError(
                "GameManager não encontrado na Scene."
            );

            return;
        }

        GameManager.instance
            .ClicarTerritorio(this);
    }

    // =====================================================
    // TROPAS
    // =====================================================

    public void AdicionarTropa()
    {
        if (territorioTropas == null)
            return;

        territorioTropas.Adicionar();

        territorioContador?.Atualizar();
    }

    public void RemoverTropa()
    {
        if (territorioTropas == null)
            return;

        territorioTropas.Remover();

        territorioContador?.Atualizar();
    }

    public bool RemoverTropas(
        int quantidade)
    {
        if (territorioTropas == null)
            return false;

        bool resultado =
            territorioTropas
                .RemoverQuantidade(
                    quantidade
                );

        territorioContador?.Atualizar();

        return resultado;
    }

    // =====================================================
    // TRANSIÇÃO EXPLÍCITA PARA TERRITÓRIO VAZIO
    // =====================================================

    public bool RemoverTropasPermitindoEsvaziamento(
        int quantidade)
    {
        if (territorioTropas == null)
            territorioTropas = GetComponent<TerritorioTropas>();

        if (!IsOccupied || territorioTropas == null ||
            quantidade <= 0 || quantidade > Tropas)
            return false;

        int restantes = Tropas - quantidade;
        if (restantes == 0)
        {
            DefinirVazio();
            return true;
        }

        territorioTropas.DefinirQuantidade(restantes);
        territorioContador?.Atualizar();
        return true;
    }

    public void DefinirTropas(
        int quantidade)
    {
        if (territorioTropas == null)
            return;

        territorioTropas
            .DefinirQuantidade(
                quantidade
            );

        territorioContador?.Atualizar();
    }

    // =====================================================
    // VIZINHANÇA
    // =====================================================

    public bool EhVizinho(
        TerritorioClique outro)
    {
        if (territorioFronteiras == null)
            territorioFronteiras = GetComponent<TerritorioFronteiras>();

        if (territorioFronteiras == null)
            return false;

        return
            territorioFronteiras
                .EhVizinho(outro);
    }

    // =====================================================
    // VISUAL
    // =====================================================

    public void AtualizarCor()
    {
        AtualizarIdentidadeVisual();
    }

    public void AplicarEstadoVisualResolucao(int tropasVisuais, Dono donoVisual)
    {
        if (territorioVisual == null)
            territorioVisual = GetComponent<TerritorioVisual>();
        territorioVisual?.AplicarDonoVisual(donoVisual);

        ContadorTropas contador = GetComponentInChildren<ContadorTropas>(true);
        contador?.AtualizarVisual(tropasVisuais, donoVisual);
    }

    public void RestaurarEstadoVisualLogico()
    {
        AtualizarIdentidadeVisual();
    }

    public void DefinirNeutro()
    {
        dono = Dono.Neutro;
        estadoTerritorial = EstadoTerritorial.Neutro;
        DefinirTropas(1);
        AtualizarIdentidadeVisual();
    }

    public void DefinirVazio()
    {
        dono = Dono.Neutro;
        estadoTerritorial = EstadoTerritorial.Vazio;
        if (territorioTropas == null)
            territorioTropas = GetComponent<TerritorioTropas>();
        territorioTropas?.DefinirQuantidadeZeroSemOcupante();
        AtualizarIdentidadeVisual();
    }

    // =====================================================
    // TRANSIÇÃO AUTORITATIVA ATÔMICA PARA ADAPTADORES
    // =====================================================

    public bool TentarAplicarEstadoAutoritativo(
        Dono novoDono, int novasTropas, EstadoTerritorial novoEstado)
    {
        bool estadoValido = novoEstado switch
        {
            EstadoTerritorial.Ocupado => novoDono != Dono.Neutro && novasTropas >= 1,
            EstadoTerritorial.Neutro => novoDono == Dono.Neutro && novasTropas == 1,
            EstadoTerritorial.Vazio => novoDono == Dono.Neutro && novasTropas == 0,
            _ => false
        };
        if (!estadoValido)
            return false;

        if (territorioTropas == null)
            territorioTropas = GetComponent<TerritorioTropas>();
        if (territorioTropas == null)
            return false;

        dono = novoDono;
        estadoTerritorial = novoEstado;
        if (novoEstado == EstadoTerritorial.Vazio)
            territorioTropas.DefinirQuantidadeZeroSemOcupante();
        else
            territorioTropas.DefinirQuantidade(novasTropas);
        AtualizarIdentidadeVisual();
        return true;
    }

    public void DestacarContinente(
        Color cor)
    {
        if (territorioVisual != null)
        {
            territorioVisual
                .DestacarContinente(cor);
        }
    }

    public void RestaurarCor()
    {
        if (territorioVisual != null)
        {
            territorioVisual.RestaurarCor();
        }
    }

    public void DestacarSelecao()
    {
        if (territorioVisual == null)
            territorioVisual = GetComponent<TerritorioVisual>();

        if (territorioVisual != null)
        {
            territorioVisual
                .DestacarSelecao();
        }

        GetComponentInChildren<WDArmyCounterView>(true)?.MostrarSelecionado(true);
    }

    public void RemoverDestaqueSelecao()
    {
        if (territorioVisual == null)
            territorioVisual = GetComponent<TerritorioVisual>();

        if (territorioVisual != null)
        {
            territorioVisual
                .RemoverDestaqueSelecao();
        }

        GetComponentInChildren<WDArmyCounterView>(true)?.MostrarSelecionado(false);
    }

    public void DestacarReforco()
    {
        if (territorioVisual == null)
            territorioVisual = GetComponent<TerritorioVisual>();

        territorioVisual?.DestacarReforcoBreve();
        GetComponentInChildren<WDArmyCounterView>(true)?.PulsoReforco();
    }

    public void DestacarConquistaRegiao()
    {
        if (territorioVisual == null)
            territorioVisual = GetComponent<TerritorioVisual>();

        territorioVisual?.DestacarConquistaRegiaoBreve();
    }
}

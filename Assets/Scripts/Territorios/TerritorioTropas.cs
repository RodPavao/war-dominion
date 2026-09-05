using UnityEngine;

public class TerritorioTropas : MonoBehaviour
{
    [SerializeField]
    private int quantidade = 1;

    private TerritorioContador territorioContador;

    public int Quantidade
    {
        get
        {
            return quantidade;
        }

        set
        {
            quantidade =
                Mathf.Max(1, value);

            AtualizarContador();
        }
    }

    private void Awake()
    {
        territorioContador =
            GetComponent<TerritorioContador>();
    }

    public void Inicializar()
    {
        TerritorioClique territorio = GetComponent<TerritorioClique>();
        quantidade = territorio != null && territorio.IsEmpty
            ? 0
            : Mathf.Max(1, quantidade);

    territorioContador =
        GetComponent<TerritorioContador>();

        AtualizarContador();
    }

    public void Adicionar()
{
    quantidade++;
}

    public void Remover()
{
    if (quantidade <= 1)
        return;

    quantidade--;
}

    public bool RemoverQuantidade(int valor)
    {
        if (valor <= 0)
            return false;

        if (quantidade - valor < 1)
            return false;

        quantidade -= valor;

        AtualizarContador();

        return true;
    }

    public void DefinirQuantidade(int valor)
    {
        quantidade =
            Mathf.Max(1, valor);

        AtualizarContador();
    }

    internal void DefinirQuantidadeZeroSemOcupante()
    {
        quantidade = 0;
        AtualizarContador();
    }
    
    private void AtualizarContador()
    {
        if (territorioContador == null)
        {
            territorioContador =
                GetComponent<TerritorioContador>();
        }

        if (territorioContador != null)
        {
            territorioContador.Atualizar();
        }
    }
}

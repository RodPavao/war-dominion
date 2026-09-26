using System;
using System.Collections.Generic;
using UnityEngine;

public enum TipoConexaoMapa
{
    Terrestre,
    Maritima,
    Especial
}

// ============================================================
// 1. ESTRUTURAS SERIALIZÁVEIS DO MAPA
// ============================================================

[Serializable]
public sealed class MetadadoMapa
{
    public string chave;
    public string valor;
}

[Serializable]
public sealed class DefinicaoTerritorioMapa
{
    public string id;
    public string nomeExibido;
    public string chaveTraducao;
    public string regiaoId;
    public Vector2 posicaoContador;
    public bool possuiPosicaoContador = true;
    [Min(0.1f)] public float escalaContador = 1f;
    public float EscalaContadorEfetiva => escalaContador > 0f
        ? Mathf.Max(0.1f, escalaContador)
        : 1f;
    public Vector2 posicaoNomeManual;
    public bool possuiPosicaoNomeManual;
    [Min(0)] public int tamanhoFonteNome;
    public List<MetadadoMapa> metadados = new List<MetadadoMapa>();
}

[Serializable]
public sealed class DefinicaoRegiaoMapa
{
    public string id;
    public string nomeExibido;
    public string descricao;
    public string chaveTraducao;
    [Min(0)] public int bonus;
    public int ordemExibicao;
    public Color corDestaque = Color.white;
    [Tooltip("Usa um deslocamento manual para a label da região em vez da posição automática.")]
    public bool possuiDeslocamentoLabelManual;
    public Vector2 deslocamentoLabelManual;
    public List<string> territorioIds = new List<string>();
    public List<MetadadoMapa> metadados = new List<MetadadoMapa>();
}

[Serializable]
public sealed class DefinicaoConexaoMapa
{
    public string origemId;
    public string destinoId;
    public TipoConexaoMapa tipo = TipoConexaoMapa.Terrestre;
    public bool bidirecional = true;
}

[Serializable]
public sealed class ReferenciaCondicaoEstrategicaMapa
{
    public string condicaoId;
    public string descricao;
    public List<string> territorioIds = new List<string>();
}

[CreateAssetMenu(fileName = "Mapa", menuName = "War Dominion/Definicao de Mapa")]
public sealed class DefinicaoMapa : ScriptableObject
{
    // ============================================================
    // 2. IDENTIDADE E APRESENTAÇÃO
    // ============================================================

    [Header("Identidade")]
    [SerializeField] private string mapaId;
    [SerializeField] private string nomeInterno;
    [SerializeField] private string nomeExibido;
    [SerializeField] private string descricao;
    [SerializeField] private string temaId;
    [SerializeField] private int versao = 1;

    [Header("Visual e enquadramento")]
    [SerializeField] private Sprite arteBase;
    [SerializeField] private Vector2 centroCamera;
    [SerializeField, Min(0.1f)] private float tamanhoOrtografico = 5f;
    [SerializeField] private Vector2 deslocamentoEnquadramento;

    [Header("Dados")]
    [SerializeField] private List<DefinicaoTerritorioMapa> territorios = new List<DefinicaoTerritorioMapa>();
    [SerializeField] private List<DefinicaoRegiaoMapa> regioes = new List<DefinicaoRegiaoMapa>();
    [SerializeField] private List<DefinicaoConexaoMapa> conexoes = new List<DefinicaoConexaoMapa>();
    [SerializeField] private List<ReferenciaCondicaoEstrategicaMapa> condicoesEstrategicas = new List<ReferenciaCondicaoEstrategicaMapa>();
    [SerializeField] private List<MetadadoMapa> metadados = new List<MetadadoMapa>();

    public string MapaId => mapaId;
    public string NomeInterno => nomeInterno;
    public string NomeExibido => nomeExibido;
    public string Descricao => descricao;
    public string TemaId => temaId;
    public int Versao => versao;
    public Sprite ArteBase => arteBase;
    public Vector2 CentroCamera => centroCamera;
    public float TamanhoOrtografico => tamanhoOrtografico;
    public Vector2 DeslocamentoEnquadramento => deslocamentoEnquadramento;
    public IReadOnlyList<DefinicaoTerritorioMapa> Territorios => territorios;
    public IReadOnlyList<DefinicaoRegiaoMapa> Regioes => regioes;
    public IReadOnlyList<DefinicaoConexaoMapa> Conexoes => conexoes;
    public IReadOnlyList<ReferenciaCondicaoEstrategicaMapa> CondicoesEstrategicas => condicoesEstrategicas;
    public IReadOnlyList<MetadadoMapa> Metadados => metadados;

#if UNITY_EDITOR
    public void ConfigurarNoEditor(
        string novoMapaId,
        string novoNomeInterno,
        string novoNomeExibido,
        string novaDescricao,
        string novoTemaId,
        Sprite novaArteBase,
        Vector2 novoCentroCamera,
        float novoTamanhoOrtografico,
        List<DefinicaoTerritorioMapa> novosTerritorios,
        List<DefinicaoRegiaoMapa> novasRegioes,
        List<DefinicaoConexaoMapa> novasConexoes)
    {
        mapaId = novoMapaId;
        nomeInterno = novoNomeInterno;
        nomeExibido = novoNomeExibido;
        descricao = novaDescricao;
        temaId = novoTemaId;
        arteBase = novaArteBase;
        centroCamera = novoCentroCamera;
        tamanhoOrtografico = novoTamanhoOrtografico;
        territorios = novosTerritorios;
        regioes = novasRegioes;
        conexoes = novasConexoes;
    }
#endif
}

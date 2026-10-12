using System;
using System.Linq;
using System.Collections;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine;

public class DistribuidorTerritorios : MonoBehaviour
{
    [Header("Distribuição automática da partida")]
    [SerializeField]
    private bool distribuirAoIniciar = true;
    private bool distribuicaoInicialExecutada;
    public bool DistribuicaoInicialExecutada => distribuicaoInicialExecutada;

    private IEnumerator Start()
    {
        // Todos os TerritorioClique precisam
        // terminar Start() primeiro.
        yield return null;

        if (distribuirAoIniciar)
            GarantirDistribuicaoInicial();
    }

    public void GarantirDistribuicaoInicial()
    {
        if (!distribuicaoInicialExecutada)
            Distribuir();
    }

    [ContextMenu("Distribuir Territórios da Partida")]
    public void Distribuir()
    {
        List<TerritorioClique> lista =
            new List<TerritorioClique>(
                MapaAtivo.ObterTerritoriosOuCena()
            );

        List<TerritorioClique.Dono> jogadores = ObterParticipantesAtivos();
        if (jogadores.Count == 0)
        {
            Debug.LogError("DISTRIBUIÇÃO | Nenhum participante ativo válido.");
            return;
        }

        if (EhPartidaUmContraUm())
        {
            foreach (TerritorioClique territorio in lista)
                territorio.DefinirNeutro();
            distribuicaoInicialExecutada = true;
            Debug.Log("ABERTURA 1x1 | Todos os territórios iniciaram neutros com 1 tropa.");
            return;
        }

        Embaralhar(lista);
        // 01. Plano integral antes de qualquer mutação de ownership/tropas.
        if (!TentarPlanejar(lista.Select(t => t.idTerritorio).ToArray(),
            MapaAtivo.Instance?.Definicao?.Regioes, jogadores, out var plano, out string erro))
        {
            Debug.LogError("DISTRIBUIÇÃO | " + erro);
            return;
        }
        for (int i = 0;
             i < lista.Count;
             i++)
        {
            TerritorioClique territorio =
                lista[i];

            TerritorioClique.Dono jogador = plano[i];

            territorio.DefinirDono(
                jogador
            );

            territorio.DefinirTropas(
                1
            );
        }

        distribuicaoInicialExecutada = true;
        WDMatchSetup setup = WDMatchSetupContext.Current;
        Debug.Log(
            $"Distribuição concluída | Participantes ativos: {jogadores.Count}" +
            (setup != null ? $" | Modo: {setup.ModeId}" : " | Legado sem Match Setup"));
    }

    // =====================================================
    // 1.1. PLANEJAMENTO POR CAPACIDADES: REGIÃO → EQUIPE → JOGADOR → COTA
    // Regiões nunca nascem sob monopólio individual nem, em modos de equipe,
    // sob monopólio de uma única equipe.
    // =====================================================
    public static bool TentarPlanejar(IReadOnlyList<string> ids,
        IReadOnlyList<DefinicaoRegiaoMapa> regioes, IReadOnlyList<TerritorioClique.Dono> jogadores,
        out TerritorioClique.Dono[] plano, out string erro)
    {
        plano = null; erro = null;
        if (ids == null || jogadores == null || jogadores.Count == 0 ||
            jogadores.Distinct().Count() != jogadores.Count || jogadores.Contains(TerritorioClique.Dono.Neutro))
        { erro = "Participantes/territórios inválidos."; return false; }
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
            if (string.IsNullOrWhiteSpace(ids[i]) || !indices.TryAdd(ids[i], i))
            { erro = "Território sem ID ou duplicado na distribuição."; return false; }
        var grupos = new List<List<int>>();
        var usados = new HashSet<int>();
        foreach (var regiao in regioes ?? Array.Empty<DefinicaoRegiaoMapa>())
        {
            if (regiao?.territorioIds == null || regiao.territorioIds.Count == 0)
            { erro = "Definição regional vazia/inválida."; return false; }
            var grupo = new List<int>();
            foreach (string id in regiao.territorioIds)
            {
                if (id == null || !indices.TryGetValue(id, out int index) || !usados.Add(index))
                { erro = $"Região {regiao.id}: território ausente, repetido ou sobreposto ({id})."; return false; }
                grupo.Add(index);
            }
            if (grupo.Count < 2 || jogadores.Count < 2)
            { erro = $"Região {regiao.id}: impossível evitar monopólio com {grupo.Count} território(s) e {jogadores.Count} jogador(es)."; return false; }
            grupos.Add(grupo);
        }
        if (grupos.Count == 0)
        {
            plano = Enumerable.Range(0, ids.Count).Select(i => jogadores[i % jogadores.Count]).ToArray();
            return true; // Sem regiões canônicas, conserva o round-robin legado exatamente.
        }
        // Ordem aleatória já fornecida pelo embaralhamento canônico, não por nomes/IDs.
        grupos.Sort((a, b) => a.Min().CompareTo(b.Min()));
        int restritos = grupos.Count;
        var livres = Enumerable.Range(0, ids.Count).Where(i => !usados.Contains(i)).ToList();
        if (livres.Count > 0) grupos.Add(livres);
        WDMatchSetup setup = WDMatchSetupContext.Current;
        bool usaEquipes = setup?.IsTeamMatch == true;
        int[] equipes = new int[jogadores.Count];
        int[] equipesDistintas = Array.Empty<int>();
        if (usaEquipes)
        {
            for (int j = 0; j < jogadores.Count; j++)
            {
                int slot = (int)jogadores[j] - 1;
                WDMatchParticipant participant = setup.Participants.FirstOrDefault(
                    item => item.SlotIndex == slot);
                if (participant == null)
                { erro = $"Participante sem TeamId para o slot {slot}."; return false; }
                equipes[j] = participant.TeamIndex;
            }
            equipesDistintas = equipes.Distinct().OrderBy(value => value).ToArray();
            if (equipesDistintas.Length < 2)
            { erro = "Partida de equipes sem ao menos dois times distintos."; return false; }
        }

        int teamGateStart = 1 + grupos.Count;
        int teamGateCount = usaEquipes ? restritos * equipesDistintas.Length : 0;
        int playerStart = teamGateStart + teamGateCount;
        int sink = playerStart + jogadores.Count;
        var capacidade = new int[sink + 1, sink + 1];
        var gatePorGrupoJogador = new int[grupos.Count, jogadores.Count];
        for (int g = 0; g < grupos.Count; g++)
        {
            capacidade[0, g + 1] = grupos[g].Count;
            for (int j = 0; j < jogadores.Count; j++)
            {
                if (usaEquipes && g < restritos)
                {
                    int teamOrdinal = Array.IndexOf(equipesDistintas, equipes[j]);
                    int gate = teamGateStart + g * equipesDistintas.Length + teamOrdinal;
                    gatePorGrupoJogador[g, j] = gate;
                    capacidade[g + 1, gate] = grupos[g].Count - 1;
                    capacidade[gate, playerStart + j] = grupos[g].Count - 1;
                }
                else
                {
                    gatePorGrupoJogador[g, j] = g + 1;
                    capacidade[g + 1, playerStart + j] =
                        grupos[g].Count - (g < restritos ? 1 : 0);
                }
            }
        }
        for (int j = 0; j < jogadores.Count; j++)
            capacidade[playerStart + j, sink] = ids.Count / jogadores.Count + (j < ids.Count % jogadores.Count ? 1 : 0);
        var residual = (int[,])capacidade.Clone();
        var ordem = Enumerable.Range(1, sink).ToArray();
        // Arestas aleatórias mantêm variedade sem retries de distribuições completas.
        for (int i = ordem.Length - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); (ordem[i], ordem[j]) = (ordem[j], ordem[i]); }
        int fluxo = 0;
        // 1.2. Preserva a preferência round-robin da ordem embaralhada. Cada
        // atribuição só entra no plano se respeitar a capacidade regional.
        var grupoPorIndice = new int[ids.Count];
        for (int g = 0; g < grupos.Count; g++) foreach (int index in grupos[g]) grupoPorIndice[index] = g + 1;
        for (int i = 0; i < ids.Count; i++)
        {
            int groupIndex = grupoPorIndice[i] - 1;
            int g = groupIndex + 1;
            int playerIndex = i % jogadores.Count;
            int player = playerStart + playerIndex;
            int gate = gatePorGrupoJogador[groupIndex, playerIndex];
            if (gate != g && residual[g, gate] == 0 ||
                residual[gate, player] == 0 ||
                residual[player, sink] == 0) continue;
            residual[0, g]--; residual[g, 0]++;
            if (gate != g) { residual[g, gate]--; residual[gate, g]++; }
            residual[gate, player]--; residual[player, gate]++;
            residual[player, sink]--; residual[sink, player]++;
            fluxo++;
        }
        while (fluxo < ids.Count)
        {
            var anterior = Enumerable.Repeat(-1, sink + 1).ToArray(); anterior[0] = 0;
            var fila = new Queue<int>(); fila.Enqueue(0);
            while (fila.Count > 0 && anterior[sink] < 0)
            {
                int atual = fila.Dequeue();
                foreach (int proximo in ordem)
                    if (anterior[proximo] < 0 && residual[atual, proximo] > 0)
                    { anterior[proximo] = atual; fila.Enqueue(proximo); }
            }
            if (anterior[sink] < 0)
            { erro = "Cotas e capacidades regionais incompatíveis; nenhuma posse foi aplicada."; return false; }
            int aumento = ids.Count - fluxo;
            for (int v = sink; v != 0; v = anterior[v]) aumento = Math.Min(aumento, residual[anterior[v], v]);
            for (int v = sink; v != 0; v = anterior[v])
            { residual[anterior[v], v] -= aumento; residual[v, anterior[v]] += aumento; }
            fluxo += aumento; // Inteiro positivo: no máximo ids.Count caminhos, sem retry infinito.
        }
        plano = new TerritorioClique.Dono[ids.Count];
        for (int g = 0; g < grupos.Count; g++)
        {
            var cotas = new int[jogadores.Count];
            for (int j = 0; j < jogadores.Count; j++)
                cotas[j] = residual[playerStart + j, gatePorGrupoJogador[g, j]];
            var pendentes = new List<int>();
            grupos[g].Sort();
            foreach (int index in grupos[g])
            {
                int preferido = index % jogadores.Count;
                if (cotas[preferido] > 0) { plano[index] = jogadores[preferido]; cotas[preferido]--; }
                else pendentes.Add(index);
            }
            foreach (int index in pendentes)
            {
                int inicio = Random.Range(0, jogadores.Count);
                for (int n = 0; n < jogadores.Count; n++)
                {
                    int j = (inicio + n) % jogadores.Count;
                    if (cotas[j] == 0) continue;
                    plano[index] = jogadores[j]; cotas[j]--; break;
                }
            }
        }
        return true;
    }

    private static bool EhPartidaUmContraUm()
    {
        WDMatchSetup setup = WDMatchSetupContext.Current;
        return setup != null && setup.IsOneVsOne;
    }

    // =====================================================
    // 2. PARTICIPANTES AUTORITATIVOS DO MATCH SETUP
    // =====================================================

    private static List<TerritorioClique.Dono> ObterParticipantesAtivos()
    {
        var jogadores = new List<TerritorioClique.Dono>();
        WDMatchSetup setup = WDMatchSetupContext.Current;
        if (setup != null)
        {
            foreach (WDMatchParticipant participant in setup.Participants)
            {
                int ownerValue = participant.SlotIndex + 1;
                if (System.Enum.IsDefined(typeof(TerritorioClique.Dono), ownerValue))
                    jogadores.Add((TerritorioClique.Dono)ownerValue);
            }
            return jogadores;
        }

        jogadores.Add(TerritorioClique.Dono.Jogador1);
        jogadores.Add(TerritorioClique.Dono.Jogador2);
        jogadores.Add(TerritorioClique.Dono.Jogador3);
        jogadores.Add(TerritorioClique.Dono.Jogador4);
        return jogadores;
    }

    // =====================================================
    // 3. EMBARALHAMENTO PRESERVADO
    // =====================================================

    private void Embaralhar(
        List<TerritorioClique> lista)
    {
        for (int i = lista.Count - 1;
             i > 0;
             i--)
        {
            int j =
                Random.Range(
                    0,
                    i + 1
                );

            TerritorioClique temp =
                lista[i];

            lista[i] =
                lista[j];

            lista[j] =
                temp;
        }
    }
}

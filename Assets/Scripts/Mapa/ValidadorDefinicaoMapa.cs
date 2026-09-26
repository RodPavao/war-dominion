using System;
using System.Collections.Generic;

public enum SeveridadeProblemaMapa
{
    Aviso,
    Erro
}

public readonly struct ProblemaValidacaoMapa
{
    public ProblemaValidacaoMapa(SeveridadeProblemaMapa severidade, string codigo, string mensagem)
    {
        Severidade = severidade;
        Codigo = codigo;
        Mensagem = mensagem;
    }

    public SeveridadeProblemaMapa Severidade { get; }
    public string Codigo { get; }
    public string Mensagem { get; }
    public override string ToString() => $"[{Severidade}] {Codigo}: {Mensagem}";
}

public static class ValidadorDefinicaoMapa
{
    // ============================================================
    // 1. VALIDAÇÃO ESTRUTURAL PURA
    // ============================================================

    public static List<ProblemaValidacaoMapa> Validar(DefinicaoMapa mapa)
    {
        List<ProblemaValidacaoMapa> problemas = new List<ProblemaValidacaoMapa>();
        if (mapa == null)
        {
            problemas.Add(new ProblemaValidacaoMapa(SeveridadeProblemaMapa.Erro, "MAPA_NULO", "Nenhuma definição foi fornecida."));
            return problemas;
        }

        if (string.IsNullOrWhiteSpace(mapa.MapaId))
            Erro(problemas, "MAPA_ID_VAZIO", "O mapa não possui ID estável.");
        if (mapa.ArteBase == null)
            Erro(problemas, "ARTE_AUSENTE", "A arte base do mapa não foi definida.");
        if (mapa.Territorios == null || mapa.Territorios.Count == 0)
            Erro(problemas, "TERRITORIOS_AUSENTES", "O mapa não possui territórios.");
        if (mapa.Regioes == null || mapa.Regioes.Count == 0)
            Erro(problemas, "REGIOES_AUSENTES", "O mapa não possui regiões.");

        HashSet<string> territorioIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefinicaoTerritorioMapa territorio in mapa.Territorios)
        {
            if (territorio == null || string.IsNullOrWhiteSpace(territorio.id))
            {
                Erro(problemas, "TERRITORIO_ID_VAZIO", "Existe território sem ID estável.");
                continue;
            }
            if (!territorioIds.Add(territorio.id))
                Erro(problemas, "TERRITORIO_ID_DUPLICADO", $"ID duplicado: {territorio.id}.");
            if (string.IsNullOrWhiteSpace(territorio.regiaoId))
                Erro(problemas, "TERRITORIO_SEM_REGIAO", $"{territorio.id} não referencia uma região.");
            if (!territorio.possuiPosicaoContador)
                Erro(problemas, "POSICAO_CONTADOR_AUSENTE", $"{territorio.id} não possui posição de contador registrada.");
        }

        HashSet<string> regiaoIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> membrosDeRegiao = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefinicaoRegiaoMapa regiao in mapa.Regioes)
        {
            if (regiao == null || string.IsNullOrWhiteSpace(regiao.id))
            {
                Erro(problemas, "REGIAO_ID_VAZIO", "Existe região sem ID estável.");
                continue;
            }
            if (!regiaoIds.Add(regiao.id))
                Erro(problemas, "REGIAO_ID_DUPLICADO", $"ID duplicado: {regiao.id}.");
            if (regiao.territorioIds == null || regiao.territorioIds.Count == 0)
                Erro(problemas, "REGIAO_VAZIA", $"A região {regiao.id} não possui territórios.");
            else
            {
                HashSet<string> membrosLocais = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in regiao.territorioIds)
                {
                    if (!territorioIds.Contains(id))
                        Erro(problemas, "REFERENCIA_TERRITORIO_INVALIDA", $"A região {regiao.id} referencia {id}, que não existe.");
                    if (!membrosLocais.Add(id))
                        Erro(problemas, "TERRITORIO_DUPLICADO_NA_REGIAO", $"{id} aparece mais de uma vez em {regiao.id}.");
                    if (!membrosDeRegiao.Add(id))
                        Erro(problemas, "TERRITORIO_EM_MULTIPLAS_REGIOES", $"{id} aparece em mais de uma região.");
                }
            }
        }

        foreach (DefinicaoTerritorioMapa territorio in mapa.Territorios)
        {
            if (territorio != null && !string.IsNullOrWhiteSpace(territorio.regiaoId) && !regiaoIds.Contains(territorio.regiaoId))
                Erro(problemas, "REFERENCIA_REGIAO_INVALIDA", $"{territorio.id} referencia a região inexistente {territorio.regiaoId}.");
            if (territorio == null || string.IsNullOrWhiteSpace(territorio.id))
                continue;
            DefinicaoRegiaoMapa declarada = null;
            foreach (DefinicaoRegiaoMapa regiao in mapa.Regioes)
                if (regiao != null && string.Equals(regiao.id, territorio.regiaoId,
                    StringComparison.Ordinal))
                    declarada = regiao;
            if (declarada != null && (declarada.territorioIds == null ||
                !declarada.territorioIds.Contains(territorio.id)))
                Erro(problemas, "REGIAO_NAO_LISTA_TERRITORIO",
                    $"{territorio.id} aponta para {declarada.id}, mas não consta em territorioIds.");
            foreach (DefinicaoRegiaoMapa regiao in mapa.Regioes)
                if (regiao?.territorioIds != null && regiao.territorioIds.Contains(territorio.id) &&
                    !string.Equals(regiao.id, territorio.regiaoId, StringComparison.Ordinal))
                    Erro(problemas, "REGIAO_LISTA_TERRITORIO_DIVERGENTE",
                        $"{regiao.id} lista {territorio.id}, cuja região declarada é {territorio.regiaoId}.");
        }

        HashSet<string> conexoes = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, int> quantidadeConexoes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in territorioIds)
            quantidadeConexoes[id] = 0;
        foreach (DefinicaoConexaoMapa conexao in mapa.Conexoes)
        {
            if (conexao == null)
            {
                Erro(problemas, "CONEXAO_NULA", "Existe uma conexão sem dados.");
                continue;
            }
            if (!territorioIds.Contains(conexao.origemId) || !territorioIds.Contains(conexao.destinoId))
                Erro(problemas, "CONEXAO_REFERENCIA_INVALIDA", $"Conexão {conexao.origemId} -> {conexao.destinoId} referencia território inexistente.");
            if (conexao.origemId == conexao.destinoId)
                Erro(problemas, "CONEXAO_AUTOREFERENTE", $"{conexao.origemId} conecta a si próprio.");
            string chave = ChaveConexao(conexao);
            if (!conexoes.Add(chave))
                Erro(problemas, "CONEXAO_DUPLICADA", $"Conexão duplicada: {conexao.origemId} -> {conexao.destinoId} ({conexao.tipo}).");
            if (quantidadeConexoes.ContainsKey(conexao.origemId))
                quantidadeConexoes[conexao.origemId]++;
            if (quantidadeConexoes.ContainsKey(conexao.destinoId))
                quantidadeConexoes[conexao.destinoId]++;
        }

        foreach (KeyValuePair<string, int> item in quantidadeConexoes)
            if (item.Value == 0)
                Erro(problemas, "TERRITORIO_ISOLADO", $"{item.Key} não possui nenhuma conexão.");

        foreach (ReferenciaCondicaoEstrategicaMapa condicao in mapa.CondicoesEstrategicas)
        {
            if (condicao == null || string.IsNullOrWhiteSpace(condicao.condicaoId))
            {
                Erro(problemas, "CONDICAO_ESTRATEGICA_INVALIDA", "Existe referência estratégica sem ID.");
                continue;
            }
            foreach (string id in condicao.territorioIds)
                if (!territorioIds.Contains(id))
                    Erro(problemas, "CONDICAO_REFERENCIA_INVALIDA", $"A condição {condicao.condicaoId} referencia o território inexistente {id}.");
        }

        return problemas;
    }

    private static string ChaveConexao(DefinicaoConexaoMapa conexao)
    {
        string origem = conexao.origemId ?? string.Empty;
        string destino = conexao.destinoId ?? string.Empty;
        if (conexao.bidirecional && string.CompareOrdinal(origem, destino) > 0)
            (origem, destino) = (destino, origem);
        return $"{origem}|{destino}|{conexao.tipo}|{conexao.bidirecional}";
    }

    private static void Erro(List<ProblemaValidacaoMapa> problemas, string codigo, string mensagem)
    {
        problemas.Add(new ProblemaValidacaoMapa(SeveridadeProblemaMapa.Erro, codigo, mensagem));
    }
}

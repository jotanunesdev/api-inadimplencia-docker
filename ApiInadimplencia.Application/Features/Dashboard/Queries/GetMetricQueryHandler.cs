using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Parsers;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Handles the query to get specific dashboard metrics.
/// </summary>
public sealed class GetMetricQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetMetricQuery, IReadOnlyList<Dictionary<string, object?>>>
{
    private static readonly Dictionary<string, string> MetricQueryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["carteira-atuacao"] = "Dashboard.CarteiraAtuacao",
        ["carteira-juridica"] = "Dashboard.CarteiraJuridica",
        ["carteira-juridica-detalhes"] = "Dashboard.CarteiraJuridicaDetalhes",
        ["carteira-juridica-processos"] = "Dashboard.CarteiraJuridicaProcessos",
        ["carteira-atuacao-detalhes"] = "Dashboard.CarteiraAtuacaoDetalhes",
        ["carteira-atuacao-contatos"] = "Dashboard.CarteiraAtuacaoContatos",
        ["recuperacao-parcelas"] = "Dashboard.RecuperacaoMensal",
        ["recuperacao-parcelas-detalhes"] = "Dashboard.RecuperacaoDetalhes",
        ["conversoes-quantidade"] = "Dashboard.ObservacoesMensais",
        ["conversoes-quantidade-detalhes"] = "Dashboard.ObservacoesDetalhes",
        ["carteira-inadimplente-detalhes"] = "Dashboard.CarteiraInadimplenteDetalhes",
        ["carteira-inadimplente-parcelas"] = "Dashboard.CarteiraInadimplenteParcelas",
        ["convertidos"] = "Dashboard.Convertidos",
        ["convertidos-mensais"] = "Dashboard.ConvertidosMensais",
        ["convertidos-mensais-detalhes"] = "Dashboard.ConvertidosMensaisDetalhes",
        ["situacao-clientes-detalhes"] = "Dashboard.SituacaoClientesDetalhes",
        ["situacao-parcelas-detalhes"] = "Dashboard.SituacaoParcelasDetalhes",
        ["vendas-negativadas"] = "Dashboard.Negativadas",
        ["ocorrencias-dia-detalhes"] = "Dashboard.OcorrenciasDiaDetalhes",
        ["vendas-por-responsavel"] = "Dashboard.VendasPorResponsavel",
        ["inadimplencia-por-empreendimento"] = "Dashboard.InadimplenciaPorEmpreendimento",
        ["clientes-por-empreendimento"] = "Dashboard.ClientesPorEmpreendimento",
        ["status-repasse"] = "Dashboard.StatusRepasse",
        ["blocos"] = "Dashboard.Blocos",
        ["unidades"] = "Dashboard.Unidades",
        ["usuarios-ativos"] = "Dashboard.UsuariosAtivos",
        ["ocorrencias-por-usuario"] = "Dashboard.OcorrenciasPorUsuario",
        ["ocorrencias-por-venda"] = "Dashboard.OcorrenciasPorVenda",
        ["ocorrencias-por-dia"] = "Dashboard.OcorrenciasPorDia",
        ["ocorrencias-por-hora"] = "Dashboard.OcorrenciasPorHora",
        ["ocorrencias-por-dia-hora"] = "Dashboard.OcorrenciasPorDiaHora",
        ["proximas-acoes-por-dia"] = "Dashboard.ProximasAcoesPorDia",
        ["acoes-definidas"] = "Dashboard.AcoesDefinidas",
        ["atendentes-por-proxima-acao"] = "Dashboard.AtendentesPorProximaAcao",
        ["atendentes-proxima-acao"] = "Dashboard.AtendentesPorProximaAcao",
        ["aging"] = "Dashboard.Aging",
        ["aging-detalhes"] = "Dashboard.AgingDetalhes",
        ["parcelas-inadimplentes"] = "Dashboard.ParcelasInadimplentes",
        ["parcelas-detalhes"] = "Dashboard.ParcelasDetalhes",
        ["score-saldo"] = "Dashboard.ScoreSaldo",
        ["score-saldo-detalhes"] = "Dashboard.ScoreSaldoDetalhes",
        ["saldo-por-mes-vencimento"] = "Dashboard.SaldoPorMesVencimento",
        ["perfil-risco-empreendimento"] = "Dashboard.PerfilRiscoEmpreendimento",
        ["ocorrencias"] = "Dashboard.Ocorrencias",
        ["responsaveis"] = "Dashboard.Responsaveis"
    };

    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dictionary<string, object?>>> HandleAsync(
        GetMetricQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var juridico = query.Metric.ToLowerInvariant() is "carteira-juridica" or "carteira-juridica-detalhes" or "carteira-juridica-processos";
        if (juridico)
        {
            if (new[] { query.DataInicio, query.DataFim, query.Faixa, query.Score, query.Qtd, query.Situacao }.Any(value => !string.IsNullOrWhiteSpace(value)))
                throw new ArgumentException("O Jurídico consulta a situação vigente; aceita somente cliente, nomeUsuario e paginação.");
            var processos = query.Metric.Equals("carteira-juridica-processos", StringComparison.OrdinalIgnoreCase);
            if (processos ? query.NumVenda is not > 0 : query.NumVenda is not null)
                throw new ArgumentException("Informe numVenda positivo somente para detalhar processos do cliente da venda.");
            if (query.NomeUsuario?.Trim().Length > 150)
                throw new ArgumentException("O responsável aceita até 150 caracteres.");
        }
        var atuacao = query.Metric.ToLowerInvariant() is "carteira-atuacao" or "carteira-atuacao-detalhes" or "carteira-atuacao-contatos";
        var categoriasAtuacao = new[] { "NAO_APTO", "APTO_SEM_REGISTRO", "CICLO_PENDENTE", "APTO_ACAO_FUTURA", "APTO_ACAO_VENCIDA", "SEM_SCORE", "RESPONSAVEL_AMBIGUO" };
        if (atuacao)
        {
            var resumo = query.Metric.Equals("carteira-atuacao", StringComparison.OrdinalIgnoreCase);
            var contatos = query.Metric.Equals("carteira-atuacao-contatos", StringComparison.OrdinalIgnoreCase);
            if (new[] { query.DataInicio, query.DataFim, query.Faixa, query.Score, query.Qtd }.Any(value => !string.IsNullOrWhiteSpace(value)))
                throw new ArgumentException("A atuação consulta a posição atual: use cliente e nomeUsuario; período não delimita o ciclo de inadimplência.");
            if (resumo ? !string.IsNullOrWhiteSpace(query.Situacao) : !categoriasAtuacao.Contains(query.Situacao?.Trim().ToUpperInvariant()))
                throw new ArgumentException("Informe uma categoria de atuação válida somente no detalhamento.");
            if (contatos ? query.NumVenda is not > 0 : query.NumVenda is not null)
                throw new ArgumentException("Informe numVenda positivo somente para consultar os contatos da atuação.");
            if (query.NomeUsuario?.Trim().Length > 150)
                throw new ArgumentException("O responsável aceita até 150 caracteres.");
        }
        var cliente = string.IsNullOrWhiteSpace(query.Cliente) ? null : query.Cliente.Trim();
        if (cliente is not null && ((!atuacao && !juridico && !query.Metric.Equals("carteira-inadimplente-detalhes", StringComparison.OrdinalIgnoreCase))
                || cliente.Length > 150))
            throw new ArgumentException("O filtro de cliente aceita até 150 caracteres no detalhamento da carteira atual e nos indicadores de atuação.");
        // Busca literal: os caracteres especiais de LIKE não ampliam a consulta.
        var clienteLike = cliente is null ? null : "%" + cliente.Replace("~", "~~")
            .Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";

        var observacoes = query.Metric.StartsWith("conversoes-quantidade", StringComparison.OrdinalIgnoreCase);
        if (observacoes && (new[] { query.Faixa, query.Score, query.Qtd, query.NomeUsuario }
                .Any(value => !string.IsNullOrWhiteSpace(value)) || query.NumVenda is not null))
            throw new ArgumentException("As observações aceitam período e, no detalhe, tipo de mudança. Não há histórico dos demais filtros.");

        var carteiraAtual = query.Metric.Equals("carteira-inadimplente-detalhes", StringComparison.OrdinalIgnoreCase)
            || query.Metric.Equals("carteira-inadimplente-parcelas", StringComparison.OrdinalIgnoreCase);
        if (carteiraAtual)
        {
            // Não aceitar filtros que não se aplicam à posição atual nem ignorá-los.
            if (new[] { query.DataInicio, query.DataFim, query.Faixa, query.Score, query.Qtd,
                    query.NomeUsuario, query.Situacao }.Any(value => !string.IsNullOrWhiteSpace(value)))
                throw new ArgumentException("A carteira atual não aceita filtros históricos ou de classificação; corresponde aos KPIs atuais.");
            if (query.Metric.Equals("carteira-inadimplente-parcelas", StringComparison.OrdinalIgnoreCase)
                ? query.NumVenda is not > 0 : query.NumVenda is not null)
                throw new ArgumentException("Informe numVenda positivo somente no detalhamento de parcelas da carteira.");
        }

        var recuperacao = query.Metric.Equals("recuperacao-parcelas", StringComparison.OrdinalIgnoreCase)
            || query.Metric.Equals("recuperacao-parcelas-detalhes", StringComparison.OrdinalIgnoreCase);
        if (query.SemResponsavel && ((!atuacao && !juridico && !recuperacao && !query.Metric.Equals("carteira-inadimplente-detalhes", StringComparison.OrdinalIgnoreCase)) || !string.IsNullOrWhiteSpace(query.NomeUsuario)))
            throw new ArgumentException("Sem responsável é permitido na carteira, Jurídico e recuperação; não combine com nomeUsuario.");
        if (recuperacao && (new[] { query.Faixa, query.Score, query.Qtd, query.NomeUsuario, query.Situacao }
                .Any(value => !string.IsNullOrWhiteSpace(value)) || query.NumVenda is not null))
            throw new ArgumentException("A recuperação financeira aceita período de baixa e paginação; não utiliza filtros de conversão de vendas.");

        // Validate metric name
        if (!MetricQueryMap.TryGetValue(query.Metric, out var queryKey))
        {
            throw new ArgumentException(
                $"Invalid metric '{query.Metric}'. Allowed values are: {string.Join(", ", MetricQueryMap.Keys)}",
                nameof(query.Metric));
        }

        // Validate and parse filters
        var faixa = FaixaParser.Parse(query.Faixa);
        var score = ScoreParser.Parse(query.Score);

        // Validate limit
        var limit = query.Limit ?? 1000;
        if (limit < 1 || limit > 1000 || query.Offset < 0)
        {
            throw new ArgumentException("Limit cannot exceed 1000.", nameof(query.Limit));
        }
        if (carteiraAtual) limit = Math.Min(limit, 100);
        if (atuacao) limit = Math.Min(limit, 100);
        if (juridico) limit = Math.Min(limit, 100);

        // Validate date format if provided
        DateTime? dataInicio = null;
        DateTime? dataFim = null;

        if (!string.IsNullOrWhiteSpace(query.DataInicio))
        {
            if (!DateTime.TryParseExact(query.DataInicio, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtInicio))
            {
                throw new ArgumentException("DataInicio must be in YYYY-MM-DD format.", nameof(query.DataInicio));
            }
            dataInicio = dtInicio;
        }

        if (!string.IsNullOrWhiteSpace(query.DataFim))
        {
            if (!DateTime.TryParseExact(query.DataFim, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtFim))
            {
                throw new ArgumentException("DataFim must be in YYYY-MM-DD format.", nameof(query.DataFim));
            }
            dataFim = dtFim;
        }

        if (dataInicio > dataFim)
            throw new ArgumentException("A data inicial deve ser anterior ou igual à data final.");
        if (recuperacao)
        {
            var detalhe = query.Metric.Equals("recuperacao-parcelas-detalhes", StringComparison.OrdinalIgnoreCase);
            if (dataInicio.HasValue != dataFim.HasValue || (detalhe && dataInicio is null))
                throw new ArgumentException("Informe dataInicio e dataFim juntos para o período de baixa.");
            if (dataInicio.HasValue && dataFim.HasValue && (dataFim.Value.Year - dataInicio.Value.Year) * 12 + dataFim.Value.Month - dataInicio.Value.Month > 60)
                throw new ArgumentException("Selecione no máximo cinco anos de recuperação financeira.");
            if (dataFim == DateTime.MaxValue.Date)
                throw new ArgumentException("Data final fora do intervalo suportado.");
            if (detalhe) limit = Math.Min(limit, 100);
        }
        if (observacoes)
        {
            var detalhe = query.Metric.Equals("conversoes-quantidade-detalhes", StringComparison.OrdinalIgnoreCase);
            if (dataInicio.HasValue != dataFim.HasValue || (detalhe && dataInicio is null))
                throw new ArgumentException("Informe dataInicio e dataFim juntos para consultar as observações.");
            if (dataInicio.HasValue && dataFim.HasValue && (dataFim.Value.Year-dataInicio.Value.Year)*12+dataFim.Value.Month-dataInicio.Value.Month > 60)
                throw new ArgumentException("Selecione no máximo cinco anos de observações.");
            if (dataFim == DateTime.MaxValue.Date)
                throw new ArgumentException("Data final fora do intervalo suportado.");
            if (!string.IsNullOrWhiteSpace(query.Situacao) && (!detalhe || !new[] { "ENTROU_INADIMPLENCIA", "REGULARIZADO", "SEM_COMPARACAO" }.Contains(query.Situacao.Trim().ToUpperInvariant())))
                throw new ArgumentException("Tipo de mudança inválido para as observações.");
            if (detalhe) limit = Math.Min(limit, 100);
        }
        if (query.Metric.Equals("ocorrencias-dia-detalhes", StringComparison.OrdinalIgnoreCase))
        {
            if (dataInicio is null || dataInicio != dataFim)
                throw new ArgumentException("Informe o mesmo dia em dataInicio e dataFim.");
            limit = Math.Min(limit, 100);
        }

        var situacao = string.IsNullOrWhiteSpace(query.Situacao) ? null : query.Situacao.Trim().ToUpperInvariant();
        if (!atuacao && situacao is not null && !new[] { "ADIMPLENTE", "INADIMPLENTE", "SEM_SITUACAO", "ENTROU_INADIMPLENCIA", "REGULARIZADO", "REINCIDENTE", "SEM_COMPARACAO", "SEM_MUDANCA", "VENDA_ENTROU", "VENDA_REGULARIZADA", "RECUPERADO_AINDA_INADIMPLENTE" }.Contains(situacao))
            throw new ArgumentException("Situação inválida.");
        if (query.Metric.Equals("situacao-parcelas-detalhes", StringComparison.OrdinalIgnoreCase) && query.NumVenda is not > 0)
            throw new ArgumentException("Informe numVenda válido para consultar parcelas.");
        if (query.Metric.Equals("convertidos-mensais-detalhes", StringComparison.OrdinalIgnoreCase)
            && (dataInicio is null || dataInicio != dataFim || dataInicio.Value.Day != DateTime.DaysInMonth(dataInicio.Value.Year, dataInicio.Value.Month)))
            throw new ArgumentException("Informe o mesmo fechamento mensal em dataInicio e dataFim.");
        if (query.Metric.StartsWith("convertidos-mensais", StringComparison.OrdinalIgnoreCase)
            && dataInicio.HasValue != dataFim.HasValue)
            throw new ArgumentException("Informe dataInicio e dataFim juntos para o histórico mensal.");
        if (query.Metric.StartsWith("convertidos-mensais", StringComparison.OrdinalIgnoreCase)
            && dataInicio.HasValue && dataFim.HasValue && (dataFim.Value.Year - dataInicio.Value.Year) * 12 + dataFim.Value.Month - dataInicio.Value.Month > 60)
            throw new ArgumentException("Selecione no máximo cinco anos para o histórico mensal.");
        if (query.Metric.ToLowerInvariant() is "situacao-clientes-detalhes" or "situacao-parcelas-detalhes" or "convertidos-mensais-detalhes")
            limit = Math.Min(limit, 100);

        // Build parameters
        var parameters = new Dictionary<string, object?>
        {
            ["limit"] = limit,
            ["offset"] = query.Offset,
            ["situacao"] = situacao,
            ["numVenda"] = query.NumVenda,
            ["cliente"] = clienteLike,
            ["semResponsavel"] = query.SemResponsavel,
            ["dataInicio"] = dataInicio,
            ["dataFim"] = dataFim,
            ["faixa"] = faixa,
            ["score"] = score,
            ["qtd"] = NormalizeQtd(query.Qtd),
            ["nomeUsuario"] = NormalizeUsername(query.NomeUsuario)
        };

        if (observacoes)
        {
            var availability = await _executor.QueryAsync("Dashboard.ObservacoesDisponibilidade",
                new Dictionary<string, object?>(), single: false, cancellationToken);
            var available = availability.Data as IReadOnlyList<Dictionary<string, object?>>;
            if (!availability.IsConfigured || available?.FirstOrDefault()?.GetValueOrDefault("DISPONIVEL")?.ToString() != "1")
                return [new Dictionary<string, object?> { ["DISPONIVEL"] = 0 }];
        }

        var result = await _executor.QueryAsync(
            queryKey,
            parameters,
            single: false,
            cancellationToken);

        if (!result.IsConfigured || result.Data is null)
        {
            return [];
        }

        var rows = (IReadOnlyList<Dictionary<string, object?>>)result.Data;

        // Apply additional filters if needed (faixa, score)
        if (!string.IsNullOrWhiteSpace(faixa) && faixa != "all")
        {
            rows = rows.Where(r =>
            {
                if (r.TryGetValue("FAIXA", out var faixaValue) && faixaValue != null)
                {
                    return faixaValue.ToString()!.Equals(faixa, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }).ToList();
        }

        if (!string.IsNullOrWhiteSpace(score) && score != "all")
        {
            rows = rows.Where(r =>
            {
                if (r.TryGetValue("SCORE", out var scoreValue) && scoreValue != null)
                {
                    return scoreValue.ToString()!.Equals(score, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }).ToList();
        }

        return rows;
    }

    private static int? NormalizeQtd(string? qtd)
    {
        if (string.IsNullOrWhiteSpace(qtd) || qtd.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!int.TryParse(qtd, out var value) || value < 0)
        {
            throw new ArgumentException("Qtd must be an integer value or null.", nameof(qtd));
        }

        return value;
    }

    private static string? NormalizeUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return username.Trim().ToLowerInvariant();
    }
}

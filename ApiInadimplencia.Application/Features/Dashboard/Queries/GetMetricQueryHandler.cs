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
        ["ocorrencias-por-usuario"] = "Dashboard.OcorrenciasPorUsuario",
        ["ocorrencias-por-venda"] = "Dashboard.OcorrenciasPorVenda",
        ["ocorrencias-por-dia"] = "Dashboard.OcorrenciasPorDia",
        ["ocorrencias-por-hora"] = "Dashboard.OcorrenciasPorHora",
        ["ocorrencias-por-dia-hora"] = "Dashboard.OcorrenciasPorDiaHora",
        ["proximas-acoes-por-dia"] = "Dashboard.ProximasAcoesPorDia",
        ["acoes-definidas"] = "Dashboard.AcoesDefinidas",
        ["atendentes-por-proxima-acao"] = "Dashboard.AtendentesPorProximaAcao",
        ["aging"] = "Dashboard.Aging",
        ["parcelas-inadimplentes"] = "Dashboard.ParcelasInadimplentes",
        ["score-saldo"] = "Dashboard.ScoreSaldo",
        ["saldo-por-mes-vencimento"] = "Dashboard.SaldoPorMesVencimento",
        ["perfil-risco-empreendimento"] = "Dashboard.PerfilRiscoEmpreendimento"
    };

    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dictionary<string, object?>>> HandleAsync(
        GetMetricQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

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
        if (limit > 1000)
        {
            throw new ArgumentException("Limit cannot exceed 1000.", nameof(query.Limit));
        }

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

        // Build parameters
        var parameters = new Dictionary<string, object?>
        {
            ["limit"] = limit
        };

        if (dataInicio.HasValue)
        {
            parameters["dataInicio"] = dataInicio.Value;
        }

        if (dataFim.HasValue)
        {
            parameters["dataFim"] = dataFim.Value;
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
}

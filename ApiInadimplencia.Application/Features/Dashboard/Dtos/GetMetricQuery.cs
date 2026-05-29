using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Query to get a specific dashboard metric.
/// </summary>
/// <param name="Metric">Metric name (e.g., ocorrencias-por-usuario, aging, score-saldo).</param>
/// <param name="DataInicio">Optional start date in YYYY-MM-DD format.</param>
/// <param name="DataFim">Optional end date in YYYY-MM-DD format.</param>
/// <param name="Limit">Optional limit (max 1000).</param>
/// <param name="Faixa">Optional aging range filter.</param>
/// <param name="Score">Optional score filter.</param>
/// <param name="Qtd">Optional installment quantity filter.</param>
public sealed record GetMetricQuery(
    string Metric,
    string? DataInicio = null,
    string? DataFim = null,
    int? Limit = null,
    string? Faixa = null,
    string? Score = null,
    string? Qtd = null) : IQuery<IReadOnlyList<Dictionary<string, object?>>>;

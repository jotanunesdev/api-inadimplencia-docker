using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Handles the query to get dashboard KPIs.
/// </summary>
public sealed class GetDashboardKpisQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetDashboardKpisQuery, DashboardKpisDto>
{
    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<DashboardKpisDto> HandleAsync(
        GetDashboardKpisQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_executor.IsConfigured)
        {
            return Empty();
        }

        var result = await _executor.QueryAsync(
            "Dashboard.Kpis",
            new Dictionary<string, object?>(),
            single: true,
            cancellationToken);

        if (!result.IsConfigured || result.Data is null)
        {
            return Empty();
        }

        var row = (Dictionary<string, object?>)result.Data;
        return MapToDto(row);
    }

    private static DashboardKpisDto MapToDto(Dictionary<string, object?> row)
    {
        return new DashboardKpisDto
        {
            TotalVendas = GetValue<int>(row, "TOTAL_VENDAS"),
            TotalClientes = GetValue<int>(row, "TOTAL_CLIENTES"),
            SaldoTotal = GetValue<decimal>(row, "TOTAL_SALDO", "SALDO_TOTAL"),
            ValorInadimplente = GetValue<decimal>(row, "TOTAL_INADIMPLENTE", "VALOR_INADIMPLENTE"),
            PercentualInadimplencia = GetValue<decimal>(row, "PERC_INADIMPLENTE", "PERCENTUAL_INADIMPLENCIA")
        };
    }

    private static DashboardKpisDto Empty() => new()
    {
        TotalVendas = 0,
        TotalClientes = 0,
        SaldoTotal = 0,
        ValorInadimplente = 0,
        PercentualInadimplencia = 0
    };

    private static T GetValue<T>(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.ContainsKey(key))
            {
                return RowValueConverter.GetValue<T>(row, key);
            }
        }

        return default!;
    }
}

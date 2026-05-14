namespace ApiInadimplencia.Application.Features.Dashboard.Dtos;

/// <summary>
/// DTO for dashboard KPIs.
/// </summary>
public sealed class DashboardKpisDto
{
    /// <summary>
    /// Total number of defaulted sales.
    /// </summary>
    public int TotalVendas { get; init; }

    /// <summary>
    /// Total number of unique clients.
    /// </summary>
    public int TotalClientes { get; init; }

    /// <summary>
    /// Total balance in open amounts.
    /// </summary>
    public decimal SaldoTotal { get; init; }

    /// <summary>
    /// Total defaulted amount.
    /// </summary>
    public decimal ValorInadimplente { get; init; }

    /// <summary>
    /// Percentage of default (ValorInadimplente / SaldoTotal * 100).
    /// </summary>
    public decimal PercentualInadimplencia { get; init; }
}

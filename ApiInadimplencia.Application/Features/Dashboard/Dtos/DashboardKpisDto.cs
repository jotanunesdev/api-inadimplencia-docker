using System.Text.Json.Serialization;

namespace ApiInadimplencia.Application.Features.Dashboard.Dtos;

/// <summary>
/// DTO for dashboard KPIs.
/// </summary>
public sealed class DashboardKpisDto
{
    /// <summary>
    /// Total number of defaulted sales.
    /// </summary>
    [JsonPropertyName("TOTAL_VENDAS")]
    public int TotalVendas { get; init; }

    /// <summary>
    /// Total number of unique clients.
    /// </summary>
    [JsonPropertyName("TOTAL_CLIENTES")]
    public int TotalClientes { get; init; }

    /// <summary>
    /// Total balance in open amounts.
    /// </summary>
    [JsonPropertyName("TOTAL_SALDO")]
    public decimal SaldoTotal { get; init; }

    /// <summary>
    /// Total defaulted amount.
    /// </summary>
    [JsonPropertyName("TOTAL_INADIMPLENTE")]
    public decimal ValorInadimplente { get; init; }

    /// <summary>
    /// Percentage of default (ValorInadimplente / SaldoTotal * 100).
    /// </summary>
    [JsonPropertyName("PERC_INADIMPLENTE")]
    public decimal PercentualInadimplencia { get; init; }
}

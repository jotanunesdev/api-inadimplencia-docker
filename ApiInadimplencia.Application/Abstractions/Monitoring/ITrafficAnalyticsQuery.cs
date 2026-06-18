using ApiInadimplencia.Application.Features.TrafficMonitoring;

namespace ApiInadimplencia.Application.Abstractions.Monitoring;

/// <summary>
/// Reads aggregated API traffic data.
/// </summary>
public interface ITrafficAnalyticsQuery
{
    /// <summary>
    /// Gets traffic analytics for the selected period and filters.
    /// </summary>
    /// <param name="periodDays">Number of days included in the period.</param>
    /// <param name="apiName">Optional API filter.</param>
    /// <param name="environment">Optional environment filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated traffic dashboard.</returns>
    Task<TrafficDashboardDto> GetDashboardAsync(
        int periodDays,
        string? apiName,
        string? environment,
        bool excludeLoadTestTraffic,
        CancellationToken cancellationToken = default);
}

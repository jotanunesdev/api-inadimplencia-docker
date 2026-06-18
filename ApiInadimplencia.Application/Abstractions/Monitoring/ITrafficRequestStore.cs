using ApiInadimplencia.Application.Features.TrafficMonitoring;

namespace ApiInadimplencia.Application.Abstractions.Monitoring;

/// <summary>
/// Persists captured traffic records.
/// </summary>
public interface ITrafficRequestStore
{
    /// <summary>
    /// Persists a batch of captured requests.
    /// </summary>
    /// <param name="records">Requests to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteBatchAsync(
        IReadOnlyCollection<TrafficRequestRecord> records,
        CancellationToken cancellationToken = default);
}

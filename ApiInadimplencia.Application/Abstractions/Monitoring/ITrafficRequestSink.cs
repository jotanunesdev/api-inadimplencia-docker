using ApiInadimplencia.Application.Features.TrafficMonitoring;

namespace ApiInadimplencia.Application.Abstractions.Monitoring;

/// <summary>
/// Receives completed HTTP requests without blocking the request pipeline.
/// </summary>
public interface ITrafficRequestSink
{
    /// <summary>
    /// Attempts to enqueue a completed request for persistence.
    /// </summary>
    /// <param name="record">Completed request data.</param>
    /// <returns><see langword="true"/> when the record was accepted.</returns>
    bool TryWrite(TrafficRequestRecord record);
}

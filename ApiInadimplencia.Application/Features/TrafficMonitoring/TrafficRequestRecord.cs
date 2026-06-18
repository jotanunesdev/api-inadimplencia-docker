namespace ApiInadimplencia.Application.Features.TrafficMonitoring;

/// <summary>
/// Represents one completed HTTP request captured by the traffic middleware.
/// </summary>
public sealed record TrafficRequestRecord(
    Guid Id,
    DateTime RequestedAtUtc,
    string HttpMethod,
    string Endpoint,
    string RawPath,
    int StatusCode,
    long DurationMs,
    string UserName,
    string? SourceIp,
    string ApiName,
    string Environment,
    string? SourceSystem,
    string? UserAgent,
    string? TraceId);

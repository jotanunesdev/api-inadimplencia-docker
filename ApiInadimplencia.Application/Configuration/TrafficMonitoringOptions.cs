using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Application.Configuration;

/// <summary>
/// Configures API traffic auditing and analytics.
/// </summary>
public sealed class TrafficMonitoringOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TrafficMonitoring";

    /// <summary>
    /// Gets whether request monitoring is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the logical API name stored with each request.
    /// </summary>
    [Required]
    public string ApplicationName { get; init; } = "api-inadimplencia";

    /// <summary>
    /// Gets the request header used to identify the calling system.
    /// </summary>
    [Required]
    public string SourceSystemHeader { get; init; } = "X-Source-System";

    /// <summary>
    /// Gets the maximum number of records waiting for persistence.
    /// </summary>
    [Range(100, 100_000)]
    public int ChannelCapacity { get; init; } = 10_000;

    /// <summary>
    /// Gets the maximum number of records written in one batch.
    /// </summary>
    [Range(1, 5_000)]
    public int BatchSize { get; init; } = 250;

    /// <summary>
    /// Gets the maximum interval between database writes.
    /// </summary>
    [Range(1, 60)]
    public int FlushIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Gets the maximum period accepted by analytics queries.
    /// </summary>
    [Range(1, 365)]
    public int MaxAnalyticsPeriodDays { get; init; } = 90;

    /// <summary>
    /// Gets path prefixes that must not be monitored.
    /// </summary>
    public string[] ExcludedPathPrefixes { get; init; } =
    [
        "/traffic-monitoring",
        "/health",
        "/metrics",
        "/swagger",
    ];
}

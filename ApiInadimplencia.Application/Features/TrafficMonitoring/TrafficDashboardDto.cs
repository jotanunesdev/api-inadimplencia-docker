namespace ApiInadimplencia.Application.Features.TrafficMonitoring;

/// <summary>
/// Aggregated API traffic dashboard.
/// </summary>
public sealed record TrafficDashboardDto(
    DateTime GeneratedAtUtc,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    TrafficSummaryDto Summary,
    IReadOnlyList<TrafficStatusCountDto> StatusCodes,
    IReadOnlyList<TrafficTimelinePointDto> Timeline,
    IReadOnlyList<TrafficMinutePointDto> RequestsPerMinute,
    IReadOnlyList<TrafficError500PointDto> Errors500ByHour,
    IReadOnlyList<TrafficEndpointMetricDto> TopEndpoints,
    IReadOnlyList<TrafficEndpointMetricDto> SlowestEndpoints,
    IReadOnlyList<TrafficEndpointMetricDto> TopErrorEndpoints,
    IReadOnlyList<TrafficSlowEndpointByUserDto> SlowestEndpointsByUser,
    IReadOnlyList<TrafficConsumerMetricDto> TopConsumers,
    IReadOnlyList<TrafficUserMetricDto> TopUsers,
    IReadOnlyList<TrafficDimensionMetricDto> RequestsByApi,
    IReadOnlyList<TrafficDimensionMetricDto> RequestsByEnvironment,
    IReadOnlyList<TrafficRecentErrorDto> RecentErrors,
    TrafficFilterOptionsDto Filters);

/// <summary>
/// Dashboard summary values.
/// </summary>
public sealed record TrafficSummaryDto(
    long TotalRequests,
    double AverageDurationMs,
    long ErrorRequests,
    double ErrorRate,
    double RequestsPerMinute,
    int PeakRequestsPerMinute,
    int UniqueUsers,
    int UniqueSystems);

/// <summary>
/// Number of responses for one HTTP status code.
/// </summary>
public sealed record TrafficStatusCountDto(int StatusCode, long Total);

/// <summary>
/// Request volume and errors in one hourly bucket.
/// </summary>
public sealed record TrafficTimelinePointDto(
    DateTime TimestampUtc,
    long Total,
    long Errors,
    double AverageDurationMs);

/// <summary>
/// Request volume in one minute.
/// </summary>
public sealed record TrafficMinutePointDto(DateTime TimestampUtc, long Total);

/// <summary>
/// Number of HTTP 500 responses in one hourly bucket.
/// </summary>
public sealed record TrafficError500PointDto(DateTime TimestampUtc, long Total);

/// <summary>
/// Aggregated metrics for one normalized endpoint.
/// </summary>
public sealed record TrafficEndpointMetricDto(
    string HttpMethod,
    string Endpoint,
    long Total,
    long Errors,
    double AverageDurationMs,
    long MaximumDurationMs);

/// <summary>
/// Slow endpoint metric grouped by user.
/// </summary>
public sealed record TrafficSlowEndpointByUserDto(
    string UserName,
    string HttpMethod,
    string Endpoint,
    long Total,
    double AverageDurationMs,
    long MaximumDurationMs);

/// <summary>
/// Request volume for one user or source system.
/// </summary>
public sealed record TrafficConsumerMetricDto(
    string Consumer,
    string ConsumerType,
    long Total,
    long Errors,
    double AverageDurationMs);

/// <summary>
/// Request volume and errors for one authenticated user.
/// </summary>
public sealed record TrafficUserMetricDto(
    string UserName,
    long Total,
    long Errors,
    double AverageDurationMs);

/// <summary>
/// Request volume for a named dimension.
/// </summary>
public sealed record TrafficDimensionMetricDto(string Name, long Total);

/// <summary>
/// Recent failed request.
/// </summary>
public sealed record TrafficRecentErrorDto(
    DateTime RequestedAtUtc,
    string HttpMethod,
    string Endpoint,
    int StatusCode,
    long DurationMs,
    string UserName,
    string? SourceSystem,
    string? TraceId);

/// <summary>
/// Available dashboard filter values.
/// </summary>
public sealed record TrafficFilterOptionsDto(
    IReadOnlyList<string> ApiNames,
    IReadOnlyList<string> Environments);

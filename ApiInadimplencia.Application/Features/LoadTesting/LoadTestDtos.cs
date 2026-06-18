namespace ApiInadimplencia.Application.Features.LoadTesting;

public sealed record LoadTestProfileDto(
    string Key,
    string Name,
    string Description,
    string ScriptName,
    int ExpectedDurationSeconds,
    int MaxVirtualUsers);

public sealed record LoadTestTimelinePointDto(
    DateTime TimestampUtc,
    int ElapsedSeconds,
    long Requests,
    long Failures,
    double AverageDurationMs,
    double P95DurationMs,
    int ActiveVirtualUsers);

public sealed record LoadTestThresholdResultDto(
    string Metric,
    bool Passed,
    IReadOnlyList<string> Rules);

public sealed record LoadTestEndpointMetricDto(
    string Name,
    string HttpMethod,
    string Endpoint,
    string ExecutionMode,
    long Requests,
    long Failures,
    double ErrorRate,
    double AverageDurationMs,
    double MinimumDurationMs,
    double MaximumDurationMs,
    double P95DurationMs,
    double P99DurationMs);

public sealed record LoadTestRunListItemDto(
    Guid RunId,
    string ProfileKey,
    string ProfileName,
    string Status,
    string TargetBaseUrl,
    string TriggeredBy,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int ExpectedDurationSeconds,
    long TotalRequests,
    long FailedRequests,
    double ErrorRate,
    double AverageDurationMs,
    double P95DurationMs,
    double P99DurationMs,
    int PeakVirtualUsers,
    double MaxRequestsPerSecond,
    bool? ThresholdsPassed);

public sealed record LoadTestRunDetailDto(
    Guid RunId,
    string ProfileKey,
    string ProfileName,
    string Status,
    string TargetBaseUrl,
    string TriggeredBy,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int ExpectedDurationSeconds,
    double ProgressPercent,
    long TotalRequests,
    long FailedRequests,
    double ErrorRate,
    double AverageDurationMs,
    double P95DurationMs,
    double P99DurationMs,
    int PeakVirtualUsers,
    double CurrentVirtualUsers,
    double MaxRequestsPerSecond,
    bool? ThresholdsPassed,
    string? SummaryJson,
    IReadOnlyList<LoadTestThresholdResultDto> ThresholdResults,
    IReadOnlyList<LoadTestTimelinePointDto> Timeline,
    IReadOnlyList<LoadTestEndpointMetricDto> EndpointMetrics);

public sealed record StartLoadTestRequestDto(
    string ProfileKey,
    string? TargetBaseUrl);

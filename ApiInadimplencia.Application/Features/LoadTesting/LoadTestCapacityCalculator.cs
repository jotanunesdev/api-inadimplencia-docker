namespace ApiInadimplencia.Application.Features.LoadTesting;

/// <summary>
/// Calculates the observed API capacity for the managed limit-identification profile.
/// </summary>
public static class LoadTestCapacityCalculator
{
    /// <summary>
    /// Profile key used by the progressive capacity test.
    /// </summary>
    public const string ProfileKey = "identificar-limite";

    /// <summary>
    /// Maximum number of virtual users configured for the capacity test.
    /// </summary>
    public const int MaximumTargetUsers = 5000;

    /// <summary>
    /// Calculates the estimated stable limit and the reason why the test stopped.
    /// </summary>
    /// <param name="profileKey">Executed load-test profile key.</param>
    /// <param name="peakObservedUsers">Maximum observed concurrent virtual users.</param>
    /// <param name="timeline">Per-second load-test timeline.</param>
    /// <param name="isFinished">Whether the execution has finished.</param>
    /// <param name="processSucceeded">Whether k6 finished with exit code zero.</param>
    /// <returns>The capacity result for the limit profile, or <see langword="null"/> for other profiles.</returns>
    public static LoadTestCapacityResultDto? Calculate(
        string profileKey,
        int peakObservedUsers,
        IReadOnlyList<LoadTestTimelinePointDto> timeline,
        bool isFinished,
        bool processSucceeded)
    {
        if (!string.Equals(profileKey, ProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var orderedTimeline = timeline.OrderBy(point => point.ElapsedSeconds).ToList();
        var firstFailure = FindAvailabilityLimitPoint(orderedTimeline);
        var failureStartedAtUsers = firstFailure?.ActiveVirtualUsers;
        var stablePoints = firstFailure is null
            ? orderedTimeline
            : orderedTimeline
                .Where(point => point.ElapsedSeconds < firstFailure.ElapsedSeconds)
                .ToList();
        var lastStableUsers = stablePoints.Count == 0
            ? 0
            : stablePoints.Max(point => point.ActiveVirtualUsers);
        var estimatedLimitUsers = firstFailure is null
            ? Math.Min(peakObservedUsers, MaximumTargetUsers)
            : Math.Min(lastStableUsers, MaximumTargetUsers);

        return new LoadTestCapacityResultDto(
            MaximumTargetUsers,
            peakObservedUsers,
            estimatedLimitUsers,
            failureStartedAtUsers,
            ResolveStopReason(
                firstFailure is not null,
                peakObservedUsers,
                isFinished,
                processSucceeded));
    }

    private static LoadTestTimelinePointDto? FindAvailabilityLimitPoint(
        IReadOnlyList<LoadTestTimelinePointDto> timeline)
    {
        long cumulativeRequests = 0;
        long cumulativeFailures = 0;

        foreach (var point in timeline)
        {
            cumulativeRequests += point.Requests;
            cumulativeFailures += point.Failures;

            if (point.ElapsedSeconds < 15 || cumulativeRequests == 0)
            {
                continue;
            }

            if (cumulativeFailures * 100d / cumulativeRequests >= 5d)
            {
                return point;
            }
        }

        return null;
    }

    private static string ResolveStopReason(
        bool hasFailures,
        int peakObservedUsers,
        bool isFinished,
        bool processSucceeded)
    {
        if (!isFinished)
        {
            return "running";
        }

        if (hasFailures)
        {
            return "api-unavailable";
        }

        if (processSucceeded && peakObservedUsers >= MaximumTargetUsers)
        {
            return "maximum-reached";
        }

        return "interrupted";
    }
}

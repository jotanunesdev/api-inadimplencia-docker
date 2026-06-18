using ApiInadimplencia.Application.Features.LoadTesting;

namespace ApiInadimplencia.Application.Tests.Features.LoadTesting;

public sealed class LoadTestCapacityCalculatorTests
{
    [Fact]
    public void Calculate_WhenApiBecomesUnavailable_ReportsLastStableUserLevel()
    {
        var timeline = new[]
        {
            Point(15, 480, requests: 100, failures: 0),
            Point(16, 500, requests: 100, failures: 0),
            Point(17, 525, requests: 100, failures: 20),
        };

        var result = LoadTestCapacityCalculator.Calculate(
            LoadTestCapacityCalculator.ProfileKey,
            peakObservedUsers: 525,
            timeline,
            isFinished: true,
            processSucceeded: false);

        Assert.NotNull(result);
        Assert.Equal(500, result.EstimatedLimitUsers);
        Assert.Equal(525, result.FailureStartedAtUsers);
        Assert.Equal("api-unavailable", result.StopReason);
    }

    [Fact]
    public void Calculate_WhenMaximumIsReached_ReportsConfiguredMaximum()
    {
        var timeline = new[]
        {
            Point(100, 4500, requests: 100, failures: 0),
            Point(110, 5000, requests: 100, failures: 0),
        };

        var result = LoadTestCapacityCalculator.Calculate(
            LoadTestCapacityCalculator.ProfileKey,
            peakObservedUsers: 5000,
            timeline,
            isFinished: true,
            processSucceeded: true);

        Assert.NotNull(result);
        Assert.Equal(5000, result.EstimatedLimitUsers);
        Assert.Null(result.FailureStartedAtUsers);
        Assert.Equal("maximum-reached", result.StopReason);
    }

    [Fact]
    public void Calculate_ForAnotherProfile_ReturnsNull()
    {
        var result = LoadTestCapacityCalculator.Calculate(
            "baseline",
            peakObservedUsers: 8,
            [],
            isFinished: true,
            processSucceeded: true);

        Assert.Null(result);
    }

    private static LoadTestTimelinePointDto Point(
        int elapsedSeconds,
        int activeVirtualUsers,
        long requests,
        long failures)
        => new(
            DateTime.UtcNow.AddSeconds(elapsedSeconds),
            elapsedSeconds,
            requests,
            failures,
            AverageDurationMs: 25,
            P95DurationMs: 50,
            activeVirtualUsers);
}

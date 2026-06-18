using System.Text.Json;
using ApiInadimplencia.Application.Features.LoadTesting;

namespace ApiInadimplencia.Infrastructure.Monitoring;

internal static class LoadTestLiveSnapshotParser
{
    public static LoadTestRunDetailDto MergeLiveMetrics(
        LoadTestRunDetailDto run,
        string sampleFilePath,
        DateTime nowUtc)
    {
        if (!File.Exists(sampleFilePath))
        {
            return run with
            {
                ProgressPercent = CalculateProgress(run.StartedAtUtc, nowUtc, run.ExpectedDurationSeconds),
            };
        }

        var requestBuckets = new Dictionary<int, MutableTimelinePoint>();
        var endpointBuckets = new Dictionary<string, MutableEndpointMetric>(StringComparer.Ordinal);
        var allDurations = new List<double>();
        long totalRequests = 0;
        long failedRequests = 0;
        double durationSum = 0;
        long durationCount = 0;
        int peakVirtualUsers = 0;
        double currentVirtualUsers = 0;

        foreach (var line in File.ReadLines(sampleFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // k6 may still be writing the final NDJSON line during a live read.
                continue;
            }

            using (document)
            {
                var root = document.RootElement;

                if (!root.TryGetProperty("type", out var typeProperty) ||
                    !string.Equals(typeProperty.GetString(), "Point", StringComparison.Ordinal))
                {
                    continue;
                }

                var metric = root.GetProperty("metric").GetString();
                var data = root.GetProperty("data");
                var value = data.GetProperty("value").GetDouble();
                var timestampUtc = data.GetProperty("time").GetDateTime().ToUniversalTime();
                var tags = data.TryGetProperty("tags", out var tagsElement)
                    ? tagsElement
                    : default;
                var endpointName = ReadTag(tags, "name");
                var elapsedSeconds = (int)Math.Max(0, Math.Floor((timestampUtc - run.StartedAtUtc).TotalSeconds));

                if (!requestBuckets.TryGetValue(elapsedSeconds, out var bucket))
                {
                    bucket = new MutableTimelinePoint(timestampUtc, elapsedSeconds);
                    requestBuckets[elapsedSeconds] = bucket;
                }

                switch (metric)
                {
                    case "http_reqs":
                        var requestIncrement = Convert.ToInt64(Math.Round(value, MidpointRounding.AwayFromZero));
                        bucket.Requests += requestIncrement;
                        totalRequests += requestIncrement;
                        GetEndpointMetric(endpointBuckets, endpointName, tags).Requests += requestIncrement;
                        break;
                    case "endpoint_errors":
                        var failureIncrement = Convert.ToInt64(Math.Round(value, MidpointRounding.AwayFromZero));
                        bucket.Failures += failureIncrement;
                        failedRequests += failureIncrement;
                        GetEndpointMetric(endpointBuckets, endpointName, tags).Failures += failureIncrement;
                        break;
                    case "http_req_duration":
                        bucket.DurationSum += value;
                        bucket.DurationCount++;
                        bucket.Durations.Add(value);
                        durationSum += value;
                        durationCount++;
                        allDurations.Add(value);
                        GetEndpointMetric(endpointBuckets, endpointName, tags).Durations.Add(value);
                        break;
                    case "vus":
                        var vus = (int)Math.Round(value, MidpointRounding.AwayFromZero);
                        bucket.ActiveVirtualUsers = Math.Max(bucket.ActiveVirtualUsers, vus);
                        peakVirtualUsers = Math.Max(peakVirtualUsers, vus);
                        currentVirtualUsers = value;
                        break;
                }
            }
        }

        var timeline = requestBuckets
            .OrderBy(item => item.Key)
            .Select(item => item.Value.ToDto())
            .ToList();

        var averageDuration = durationCount == 0 ? 0 : durationSum / durationCount;
        var errorRate = totalRequests == 0 ? 0 : (failedRequests * 100d) / totalRequests;
        var p95 = CalculatePercentile(allDurations, 95);
        var p99 = CalculatePercentile(allDurations, 99);
        var maxRps = timeline.Count == 0 ? 0 : timeline.Max(point => (double)point.Requests);
        var endpointMetrics = endpointBuckets.Values
            .Where(metric => metric.Requests > 0 || metric.Durations.Count > 0)
            .OrderBy(metric => metric.Name, StringComparer.Ordinal)
            .Select(metric => metric.ToDto())
            .ToList();

        return run with
        {
            ProgressPercent = CalculateProgress(run.StartedAtUtc, nowUtc, run.ExpectedDurationSeconds),
            TotalRequests = totalRequests,
            FailedRequests = failedRequests,
            ErrorRate = errorRate,
            AverageDurationMs = averageDuration,
            P95DurationMs = p95,
            P99DurationMs = p99,
            PeakVirtualUsers = Math.Max(run.PeakVirtualUsers, peakVirtualUsers),
            CurrentVirtualUsers = currentVirtualUsers,
            MaxRequestsPerSecond = maxRps,
            Timeline = timeline,
            EndpointMetrics = endpointMetrics,
        };
    }

    private static MutableEndpointMetric GetEndpointMetric(
        IDictionary<string, MutableEndpointMetric> metrics,
        string? endpointName,
        JsonElement tags)
    {
        var name = string.IsNullOrWhiteSpace(endpointName) ? "unknown" : endpointName;
        if (metrics.TryGetValue(name, out var metric))
        {
            return metric;
        }

        metric = new MutableEndpointMetric(
            name,
            ReadTag(tags, "method") ?? "UNKNOWN",
            ReadTag(tags, "endpoint") ?? name,
            ReadTag(tags, "execution_mode") ?? "real");
        metrics[name] = metric;
        return metric;
    }

    private static string? ReadTag(JsonElement tags, string name)
    {
        if (tags.ValueKind != JsonValueKind.Object ||
            !tags.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static double CalculateProgress(
        DateTime startedAtUtc,
        DateTime nowUtc,
        int expectedDurationSeconds)
    {
        if (expectedDurationSeconds <= 0)
        {
            return 0;
        }

        var elapsed = Math.Max(0, (nowUtc - startedAtUtc).TotalSeconds);
        return Math.Min(100, elapsed * 100d / expectedDurationSeconds);
    }

    private static double CalculatePercentile(List<double> values, int percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var position = (int)Math.Ceiling((percentile / 100d) * values.Count) - 1;
        var index = Math.Clamp(position, 0, values.Count - 1);
        return values[index];
    }

    private sealed class MutableTimelinePoint(DateTime timestampUtc, int elapsedSeconds)
    {
        public DateTime TimestampUtc { get; } = timestampUtc;
        public int ElapsedSeconds { get; } = elapsedSeconds;
        public long Requests { get; set; }
        public long Failures { get; set; }
        public double DurationSum { get; set; }
        public long DurationCount { get; set; }
        public int ActiveVirtualUsers { get; set; }
        public List<double> Durations { get; } = [];

        public LoadTestTimelinePointDto ToDto()
            => new(
                TimestampUtc,
                ElapsedSeconds,
                Requests,
                Failures,
                DurationCount == 0 ? 0 : DurationSum / DurationCount,
                CalculatePercentile(Durations, 95),
                ActiveVirtualUsers);
    }

    private sealed class MutableEndpointMetric(
        string name,
        string httpMethod,
        string endpoint,
        string executionMode)
    {
        public string Name { get; } = name;
        public string HttpMethod { get; } = httpMethod;
        public string Endpoint { get; } = endpoint;
        public string ExecutionMode { get; } = executionMode;
        public long Requests { get; set; }
        public long Failures { get; set; }
        public List<double> Durations { get; } = [];

        public LoadTestEndpointMetricDto ToDto()
        {
            var durationAverage = Durations.Count == 0 ? 0 : Durations.Average();
            var failureRate = Requests == 0 ? 0 : Failures * 100d / Requests;

            return new LoadTestEndpointMetricDto(
                Name,
                HttpMethod,
                Endpoint,
                ExecutionMode,
                Requests,
                Failures,
                failureRate,
                durationAverage,
                Durations.Count == 0 ? 0 : Durations.Min(),
                Durations.Count == 0 ? 0 : Durations.Max(),
                CalculatePercentile(Durations, 95),
                CalculatePercentile(Durations, 99));
        }
    }
}

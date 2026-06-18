using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Features.LoadTesting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Infrastructure.Monitoring;

public sealed class K6LoadTestOrchestrator(
    ILoadTestRunRepository repository,
    IHostEnvironment hostEnvironment,
    ILogger<K6LoadTestOrchestrator> logger) : ILoadTestOrchestrator
{
    private readonly ILoadTestRunRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IHostEnvironment _hostEnvironment =
        hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    private readonly ILogger<K6LoadTestOrchestrator> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<Guid, ActiveLoadTestRun> _activeRuns = new();

    public IReadOnlyList<LoadTestProfileDto> GetProfiles()
        => LoadTestProfiles.All.Select(profile => profile.ToDto()).ToList();

    public async Task<LoadTestRunDetailDto> StartAsync(
        StartLoadTestRequestDto request,
        string triggeredBy,
        CancellationToken cancellationToken = default)
    {
        if (_activeRuns.Values.Any(run => run.Process is { HasExited: false }))
        {
            throw new InvalidOperationException("Ja existe um teste de carga em execucao.");
        }

        var profile = LoadTestProfiles.Get(request.ProfileKey);
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTime.UtcNow;
        var targetBaseUrl = string.IsNullOrWhiteSpace(request.TargetBaseUrl)
            ? "http://localhost:8080"
            : request.TargetBaseUrl.Trim().TrimEnd('/');

        await _repository.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var initialRun = new LoadTestRunDetailDto(
            runId,
            profile.Key,
            profile.Name,
            "running",
            targetBaseUrl,
            string.IsNullOrWhiteSpace(triggeredBy) ? "sistema" : triggeredBy,
            startedAtUtc,
            null,
            profile.ExpectedDurationSeconds,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            [],
            []);

        await _repository.InsertStartedRunAsync(ToListItem(initialRun), cancellationToken).ConfigureAwait(false);

        var scriptsRoot = Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, "..", "loadtests", "k6"));
        var resultRoot = Path.Combine(scriptsRoot, "results", "managed", runId.ToString("N"));
        Directory.CreateDirectory(resultRoot);

        var sampleFilePath = Path.Combine(resultRoot, "samples.ndjson");
        var summaryFilePath = Path.Combine(resultRoot, "summary.json");
        var process = StartK6Process(profile, scriptsRoot, sampleFilePath, summaryFilePath, targetBaseUrl);
        var activeRun = new ActiveLoadTestRun(initialRun, process, sampleFilePath, summaryFilePath, resultRoot);
        _activeRuns[runId] = activeRun;

        _ = MonitorCompletionAsync(activeRun);
        return initialRun;
    }

    public Task<IReadOnlyList<LoadTestRunListItemDto>> ListRunsAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => _repository.ListRunsAsync(limit, cancellationToken);

    public async Task<LoadTestRunDetailDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (_activeRuns.TryGetValue(runId, out var activeRun))
        {
            return LoadTestLiveSnapshotParser.MergeLiveMetrics(
                activeRun.Run,
                activeRun.SampleFilePath,
                DateTime.UtcNow);
        }

        return await _repository.GetRunAsync(runId, cancellationToken).ConfigureAwait(false);
    }

    private Process StartK6Process(
        LoadTestProfileDefinition profile,
        string scriptsRoot,
        string sampleFilePath,
        string summaryFilePath,
        string targetBaseUrl)
    {
        var scriptPath = Path.Combine(scriptsRoot, profile.ScriptName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Managed k6 script not found.", scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveK6Executable(),
            WorkingDirectory = Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, "..")),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--out");
        startInfo.ArgumentList.Add($"json={sampleFilePath}");
        startInfo.ArgumentList.Add("--summary-export");
        startInfo.ArgumentList.Add(summaryFilePath);
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"K6_PROFILE_KEY={profile.Key}");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"K6_BASE_URL={targetBaseUrl}");
        startInfo.ArgumentList.Add(scriptPath);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();
        return process;
    }

    private string ResolveK6Executable()
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { "k6.exe", "k6" }
            : new[] { "k6" };

        foreach (var candidate in candidates)
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    ArgumentList = { "version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                probe?.WaitForExit(2000);
                if (probe is { ExitCode: 0 })
                {
                    return candidate;
                }
            }
            catch
            {
                // noop
            }
        }

        throw new InvalidOperationException("k6 nao esta instalado ou nao esta no PATH do servidor.");
    }

    private async Task MonitorCompletionAsync(ActiveLoadTestRun activeRun)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var stdoutTask = ReadStreamAsync(activeRun.Process.StandardOutput, stdout);
        var stderrTask = ReadStreamAsync(activeRun.Process.StandardError, stderr);
        await activeRun.Process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        var finalStatus = activeRun.Process.ExitCode == 0 ? "completed" : "failed";
        var finishedAtUtc = DateTime.UtcNow;

        try
        {
            var liveSnapshot = LoadTestLiveSnapshotParser.MergeLiveMetrics(
                activeRun.Run,
                activeRun.SampleFilePath,
                finishedAtUtc);

            var summaryJson = File.Exists(activeRun.SummaryFilePath)
                ? await File.ReadAllTextAsync(activeRun.SummaryFilePath).ConfigureAwait(false)
                : BuildFallbackSummaryJson(activeRun.Process.ExitCode, stdout.ToString(), stderr.ToString());

            var completedRun = liveSnapshot with
            {
                Status = finalStatus,
                FinishedAtUtc = finishedAtUtc,
                ProgressPercent = 100,
                ThresholdsPassed = activeRun.Process.ExitCode == 0,
                SummaryJson = summaryJson,
                ThresholdResults = ParseThresholdResults(summaryJson),
            };

            await _repository.CompleteRunAsync(completedRun).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist load test run {RunId}.", activeRun.Run.RunId);
        }
        finally
        {
            _activeRuns.TryRemove(activeRun.Run.RunId, out _);
        }
    }

    private static IReadOnlyList<LoadTestThresholdResultDto> ParseThresholdResults(string summaryJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(summaryJson);
            if (!document.RootElement.TryGetProperty("metrics", out var metrics))
            {
                return [];
            }

            var results = new List<LoadTestThresholdResultDto>();
            foreach (var metric in metrics.EnumerateObject())
            {
                if (!metric.Value.TryGetProperty("thresholds", out var thresholds))
                {
                    continue;
                }

                foreach (var threshold in thresholds.EnumerateObject())
                {
                    var passed = threshold.Value.TryGetProperty("ok", out var ok) && ok.GetBoolean();
                    results.Add(new LoadTestThresholdResultDto(metric.Name, passed, [threshold.Name]));
                }
            }

            return results;
        }
        catch
        {
            return [];
        }
    }

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder buffer)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (buffer.Length < 64_000)
            {
                buffer.AppendLine(line);
            }
        }
    }

    private static string BuildFallbackSummaryJson(int exitCode, string stdout, string stderr)
        => $$"""
        {
          "exitCode": {{exitCode}},
          "stdout": {{System.Text.Json.JsonSerializer.Serialize(stdout)}},
          "stderr": {{System.Text.Json.JsonSerializer.Serialize(stderr)}}
        }
        """;

    private static LoadTestRunListItemDto ToListItem(LoadTestRunDetailDto run)
        => new(
            run.RunId,
            run.ProfileKey,
            run.ProfileName,
            run.Status,
            run.TargetBaseUrl,
            run.TriggeredBy,
            run.StartedAtUtc,
            run.FinishedAtUtc,
            run.ExpectedDurationSeconds,
            run.TotalRequests,
            run.FailedRequests,
            run.ErrorRate,
            run.AverageDurationMs,
            run.P95DurationMs,
            run.P99DurationMs,
            run.PeakVirtualUsers,
            run.MaxRequestsPerSecond,
            run.ThresholdsPassed);

    private sealed record ActiveLoadTestRun(
        LoadTestRunDetailDto Run,
        Process Process,
        string SampleFilePath,
        string SummaryFilePath,
        string ResultRootPath);
}

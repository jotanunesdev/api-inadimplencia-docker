using System.Data;
using System.Text.Json;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Features.LoadTesting;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Monitoring;

public sealed class SqlServerLoadTestRunRepository(AuditSqlConnectionFactory connectionFactory)
    : ILoadTestRunRepository
{
    private const string EnsureSchemaSql = """
        IF OBJECT_ID(N'dbo.API_LOAD_TEST_RUN', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.API_LOAD_TEST_RUN
            (
                RUN_ID uniqueidentifier NOT NULL PRIMARY KEY,
                PROFILE_KEY nvarchar(50) NOT NULL,
                PROFILE_NAME nvarchar(120) NOT NULL,
                STATUS nvarchar(30) NOT NULL,
                TARGET_BASE_URL nvarchar(500) NOT NULL,
                TRIGGERED_BY nvarchar(255) NOT NULL,
                STARTED_AT_UTC datetime2(3) NOT NULL,
                FINISHED_AT_UTC datetime2(3) NULL,
                EXPECTED_DURATION_SECONDS int NOT NULL,
                TOTAL_REQUESTS bigint NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_TOTAL_REQUESTS DEFAULT 0,
                FAILED_REQUESTS bigint NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_FAILED_REQUESTS DEFAULT 0,
                ERROR_RATE float NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_ERROR_RATE DEFAULT 0,
                AVERAGE_DURATION_MS float NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_AVG_DURATION DEFAULT 0,
                P95_DURATION_MS float NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_P95_DURATION DEFAULT 0,
                P99_DURATION_MS float NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_P99_DURATION DEFAULT 0,
                PEAK_VIRTUAL_USERS int NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_PEAK_VUS DEFAULT 0,
                MAX_REQUESTS_PER_SECOND float NOT NULL CONSTRAINT DF_API_LOAD_TEST_RUN_MAX_RPS DEFAULT 0,
                THRESHOLDS_PASSED bit NULL,
                SUMMARY_JSON nvarchar(max) NULL
            );

            CREATE INDEX IX_API_LOAD_TEST_RUN_STARTED_AT
                ON dbo.API_LOAD_TEST_RUN (STARTED_AT_UTC DESC);
        END;

        IF OBJECT_ID(N'dbo.API_LOAD_TEST_TIMELINE', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.API_LOAD_TEST_TIMELINE
            (
                RUN_ID uniqueidentifier NOT NULL,
                TIMESTAMP_UTC datetime2(3) NOT NULL,
                ELAPSED_SECONDS int NOT NULL,
                REQUESTS bigint NOT NULL,
                FAILURES bigint NOT NULL,
                AVERAGE_DURATION_MS float NOT NULL,
                P95_DURATION_MS float NOT NULL,
                ACTIVE_VIRTUAL_USERS int NOT NULL,
                CONSTRAINT PK_API_LOAD_TEST_TIMELINE PRIMARY KEY (RUN_ID, ELAPSED_SECONDS),
                CONSTRAINT FK_API_LOAD_TEST_TIMELINE_RUN
                    FOREIGN KEY (RUN_ID) REFERENCES dbo.API_LOAD_TEST_RUN (RUN_ID) ON DELETE CASCADE
            );
        END;
        """;

    private readonly AuditSqlConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private int _schemaEnsured;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectionFactory.IsConfigured || Interlocked.Exchange(ref _schemaEnsured, 1) == 1)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            EnsureSchemaSql,
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InsertStartedRunAsync(
        LoadTestRunListItemDto run,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            INSERT INTO dbo.API_LOAD_TEST_RUN
            (
                RUN_ID,
                PROFILE_KEY,
                PROFILE_NAME,
                STATUS,
                TARGET_BASE_URL,
                TRIGGERED_BY,
                STARTED_AT_UTC,
                FINISHED_AT_UTC,
                EXPECTED_DURATION_SECONDS,
                TOTAL_REQUESTS,
                FAILED_REQUESTS,
                ERROR_RATE,
                AVERAGE_DURATION_MS,
                P95_DURATION_MS,
                P99_DURATION_MS,
                PEAK_VIRTUAL_USERS,
                MAX_REQUESTS_PER_SECOND,
                THRESHOLDS_PASSED,
                SUMMARY_JSON
            )
            VALUES
            (
                @RunId,
                @ProfileKey,
                @ProfileName,
                @Status,
                @TargetBaseUrl,
                @TriggeredBy,
                @StartedAtUtc,
                @FinishedAtUtc,
                @ExpectedDurationSeconds,
                @TotalRequests,
                @FailedRequests,
                @ErrorRate,
                @AverageDurationMs,
                @P95DurationMs,
                @P99DurationMs,
                @PeakVirtualUsers,
                @MaxRequestsPerSecond,
                @ThresholdsPassed,
                NULL
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            run,
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CompleteRunAsync(
        LoadTestRunDetailDto run,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string updateRunSql = """
            UPDATE dbo.API_LOAD_TEST_RUN
            SET STATUS = @Status,
                FINISHED_AT_UTC = @FinishedAtUtc,
                TOTAL_REQUESTS = @TotalRequests,
                FAILED_REQUESTS = @FailedRequests,
                ERROR_RATE = @ErrorRate,
                AVERAGE_DURATION_MS = @AverageDurationMs,
                P95_DURATION_MS = @P95DurationMs,
                P99_DURATION_MS = @P99DurationMs,
                PEAK_VIRTUAL_USERS = @PeakVirtualUsers,
                MAX_REQUESTS_PER_SECOND = @MaxRequestsPerSecond,
                THRESHOLDS_PASSED = @ThresholdsPassed,
                SUMMARY_JSON = @SummaryJson
            WHERE RUN_ID = @RunId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateRunSql,
            run,
            transaction,
            _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.API_LOAD_TEST_TIMELINE WHERE RUN_ID = @RunId;",
            new { run.RunId },
            transaction,
            _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        using var timelineTable = new DataTable();
        timelineTable.Columns.Add("RUN_ID", typeof(Guid));
        timelineTable.Columns.Add("TIMESTAMP_UTC", typeof(DateTime));
        timelineTable.Columns.Add("ELAPSED_SECONDS", typeof(int));
        timelineTable.Columns.Add("REQUESTS", typeof(long));
        timelineTable.Columns.Add("FAILURES", typeof(long));
        timelineTable.Columns.Add("AVERAGE_DURATION_MS", typeof(double));
        timelineTable.Columns.Add("P95_DURATION_MS", typeof(double));
        timelineTable.Columns.Add("ACTIVE_VIRTUAL_USERS", typeof(int));

        foreach (var point in run.Timeline)
        {
            timelineTable.Rows.Add(
                run.RunId,
                point.TimestampUtc,
                point.ElapsedSeconds,
                point.Requests,
                point.Failures,
                point.AverageDurationMs,
                point.P95DurationMs,
                point.ActiveVirtualUsers);
        }

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction)
        {
            DestinationTableName = "[dbo].[API_LOAD_TEST_TIMELINE]",
            BatchSize = Math.Max(1, timelineTable.Rows.Count),
            BulkCopyTimeout = _connectionFactory.CommandTimeoutSeconds,
        };

        foreach (DataColumn column in timelineTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        if (timelineTable.Rows.Count > 0)
        {
            await bulkCopy.WriteToServerAsync(timelineTable, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LoadTestRunListItemDto>> ListRunsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            UPDATE dbo.API_LOAD_TEST_RUN
            SET STATUS = 'failed',
                FINISHED_AT_UTC = SYSUTCDATETIME(),
                THRESHOLDS_PASSED = 0,
                SUMMARY_JSON = COALESCE(
                    SUMMARY_JSON,
                    N'{"startupFailure":true,"error":"Execucao abandonada apos reinicio ou falha do processo k6."}')
            WHERE STATUS = 'running'
              AND DATEADD(SECOND, EXPECTED_DURATION_SECONDS + 300, STARTED_AT_UTC) < SYSUTCDATETIME();

            SELECT TOP (@Limit)
                RUN_ID AS RunId,
                PROFILE_KEY AS ProfileKey,
                PROFILE_NAME AS ProfileName,
                STATUS AS Status,
                TARGET_BASE_URL AS TargetBaseUrl,
                TRIGGERED_BY AS TriggeredBy,
                STARTED_AT_UTC AS StartedAtUtc,
                FINISHED_AT_UTC AS FinishedAtUtc,
                EXPECTED_DURATION_SECONDS AS ExpectedDurationSeconds,
                TOTAL_REQUESTS AS TotalRequests,
                FAILED_REQUESTS AS FailedRequests,
                ERROR_RATE AS ErrorRate,
                AVERAGE_DURATION_MS AS AverageDurationMs,
                P95_DURATION_MS AS P95DurationMs,
                P99_DURATION_MS AS P99DurationMs,
                PEAK_VIRTUAL_USERS AS PeakVirtualUsers,
                MAX_REQUESTS_PER_SECOND AS MaxRequestsPerSecond,
                THRESHOLDS_PASSED AS ThresholdsPassed
            FROM dbo.API_LOAD_TEST_RUN
            ORDER BY STARTED_AT_UTC DESC;
            """;

        return (await connection.QueryAsync<LoadTestRunListItemDto>(new CommandDefinition(
            sql,
            new { Limit = Math.Clamp(limit, 1, 100) },
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
    }

    public async Task<LoadTestRunDetailDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            SELECT
                RUN_ID AS RunId,
                PROFILE_KEY AS ProfileKey,
                PROFILE_NAME AS ProfileName,
                STATUS AS Status,
                TARGET_BASE_URL AS TargetBaseUrl,
                TRIGGERED_BY AS TriggeredBy,
                STARTED_AT_UTC AS StartedAtUtc,
                FINISHED_AT_UTC AS FinishedAtUtc,
                EXPECTED_DURATION_SECONDS AS ExpectedDurationSeconds,
                CAST(CASE WHEN FINISHED_AT_UTC IS NULL THEN 0 ELSE 100 END AS float) AS ProgressPercent,
                TOTAL_REQUESTS AS TotalRequests,
                FAILED_REQUESTS AS FailedRequests,
                ERROR_RATE AS ErrorRate,
                AVERAGE_DURATION_MS AS AverageDurationMs,
                P95_DURATION_MS AS P95DurationMs,
                P99_DURATION_MS AS P99DurationMs,
                PEAK_VIRTUAL_USERS AS PeakVirtualUsers,
                CAST(0 AS float) AS CurrentVirtualUsers,
                MAX_REQUESTS_PER_SECOND AS MaxRequestsPerSecond,
                THRESHOLDS_PASSED AS ThresholdsPassed,
                SUMMARY_JSON AS SummaryJson
            FROM dbo.API_LOAD_TEST_RUN
            WHERE RUN_ID = @RunId;

            SELECT
                TIMESTAMP_UTC AS TimestampUtc,
                ELAPSED_SECONDS AS ElapsedSeconds,
                REQUESTS AS Requests,
                FAILURES AS Failures,
                AVERAGE_DURATION_MS AS AverageDurationMs,
                P95_DURATION_MS AS P95DurationMs,
                ACTIVE_VIRTUAL_USERS AS ActiveVirtualUsers
            FROM dbo.API_LOAD_TEST_TIMELINE
            WHERE RUN_ID = @RunId
            ORDER BY ELAPSED_SECONDS;
            """;

        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { RunId = runId },
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var row = await multi.ReadSingleOrDefaultAsync<StoredRunRow>().ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var timeline = (await multi.ReadAsync<LoadTestTimelinePointDto>().ConfigureAwait(false)).AsList();
        var thresholds = ParseThresholds(row.SummaryJson);

        return row.ToDto(timeline, thresholds);
    }

    private static IReadOnlyList<LoadTestThresholdResultDto> ParseThresholds(string? summaryJson)
    {
        if (string.IsNullOrWhiteSpace(summaryJson))
        {
            return [];
        }

        using var document = JsonDocument.Parse(summaryJson);
        if (!document.RootElement.TryGetProperty("metrics", out var metricsElement))
        {
            return [];
        }

        var results = new List<LoadTestThresholdResultDto>();

        foreach (var metric in metricsElement.EnumerateObject())
        {
            if (!metric.Value.TryGetProperty("thresholds", out var thresholdsElement))
            {
                continue;
            }

            foreach (var threshold in thresholdsElement.EnumerateObject())
            {
                var passed = ReadThresholdResult(threshold.Value);
                results.Add(new LoadTestThresholdResultDto(
                    metric.Name,
                    passed,
                    [threshold.Name]));
            }
        }

        return results;
    }

    private static bool ReadThresholdResult(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("ok", out var ok)
            && ok.ValueKind is JsonValueKind.True or JsonValueKind.False
            && ok.GetBoolean();
    }

    private sealed record StoredRunRow(
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
        string? SummaryJson)
    {
        public LoadTestRunDetailDto ToDto(
            IReadOnlyList<LoadTestTimelinePointDto> timeline,
            IReadOnlyList<LoadTestThresholdResultDto> thresholds)
            => new(
                RunId,
                ProfileKey,
                ProfileName,
                Status,
                TargetBaseUrl,
                TriggeredBy,
                StartedAtUtc,
                FinishedAtUtc,
                ExpectedDurationSeconds,
                ProgressPercent,
                TotalRequests,
                FailedRequests,
                ErrorRate,
                AverageDurationMs,
                P95DurationMs,
                P99DurationMs,
                PeakVirtualUsers,
                CurrentVirtualUsers,
                MaxRequestsPerSecond,
                ThresholdsPassed,
                SummaryJson,
                thresholds,
                timeline);
    }
}

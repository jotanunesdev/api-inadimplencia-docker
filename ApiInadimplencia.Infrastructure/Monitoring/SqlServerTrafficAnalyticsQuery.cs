using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.TrafficMonitoring;
using Dapper;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Monitoring;

/// <summary>
/// Reads aggregated API traffic metrics from SQL Server.
/// </summary>
public sealed class SqlServerTrafficAnalyticsQuery(
    AuditSqlConnectionFactory connectionFactory,
    IOptions<TrafficMonitoringOptions> options) : ITrafficAnalyticsQuery
{
    private const string DashboardSql = """
        DECLARE @PeriodMinutes float = CASE
            WHEN DATEDIFF(MINUTE, @FromUtc, @ToUtc) < 1 THEN 1
            ELSE DATEDIFF(MINUTE, @FromUtc, @ToUtc)
        END;
        DECLARE @MinuteFromUtc datetime2 = CASE
            WHEN @FromUtc > DATEADD(HOUR, -6, @ToUtc) THEN @FromUtc
            ELSE DATEADD(HOUR, -6, @ToUtc)
        END;

        SELECT
            COUNT_BIG(1) AS TotalRequests,
            COALESCE(AVG(CAST(DURATION_MS AS float)), 0) AS AverageDurationMs,
            COALESCE(SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END), 0) AS ErrorRequests,
            CASE WHEN COUNT_BIG(1) = 0 THEN 0
                 ELSE 100.0 * SUM(CASE WHEN STATUS_CODE >= 400 THEN 1.0 ELSE 0.0 END) / COUNT_BIG(1)
            END AS ErrorRate,
            COUNT_BIG(1) / @PeriodMinutes AS RequestsPerMinute,
            COALESCE((
                SELECT MAX(MinuteTotal)
                FROM (
                    SELECT COUNT(1) AS MinuteTotal
                    FROM dbo.API_TRAFFIC_AUDIT peak
                    WHERE peak.REQUESTED_AT_UTC >= @FromUtc
                      AND peak.REQUESTED_AT_UTC < @ToUtc
                      AND (@ApiName IS NULL OR peak.API_NAME = @ApiName)
                      AND (@Environment IS NULL OR peak.ENVIRONMENT = @Environment)
                    GROUP BY DATEADD(MINUTE, DATEDIFF(MINUTE, 0, peak.REQUESTED_AT_UTC), 0)
                ) peak_values
            ), 0) AS PeakRequestsPerMinute,
            COUNT(DISTINCT CASE WHEN USER_NAME <> 'anonymous' THEN USER_NAME END) AS UniqueUsers,
            COUNT(DISTINCT SOURCE_SYSTEM) AS UniqueSystems
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment);

        SELECT STATUS_CODE AS StatusCode, COUNT_BIG(1) AS Total
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY STATUS_CODE
        ORDER BY STATUS_CODE;

        SELECT
            DATEADD(HOUR, DATEDIFF(HOUR, 0, REQUESTED_AT_UTC), 0) AS TimestampUtc,
            COUNT_BIG(1) AS Total,
            SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
            AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY DATEADD(HOUR, DATEDIFF(HOUR, 0, REQUESTED_AT_UTC), 0)
        ORDER BY TimestampUtc;

        SELECT
            DATEADD(MINUTE, DATEDIFF(MINUTE, 0, REQUESTED_AT_UTC), 0) AS TimestampUtc,
            COUNT_BIG(1) AS Total
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @MinuteFromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY DATEADD(MINUTE, DATEDIFF(MINUTE, 0, REQUESTED_AT_UTC), 0)
        ORDER BY TimestampUtc;

        SELECT
            DATEADD(HOUR, DATEDIFF(HOUR, 0, REQUESTED_AT_UTC), 0) AS TimestampUtc,
            COUNT_BIG(1) AS Total
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND STATUS_CODE = 500
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY DATEADD(HOUR, DATEDIFF(HOUR, 0, REQUESTED_AT_UTC), 0)
        ORDER BY TimestampUtc;

        SELECT TOP (10)
            HTTP_METHOD AS HttpMethod,
            ENDPOINT AS Endpoint,
            COUNT_BIG(1) AS Total,
            SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
            AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs,
            MAX(DURATION_MS) AS MaximumDurationMs
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY HTTP_METHOD, ENDPOINT
        ORDER BY Total DESC, AverageDurationMs DESC;

        SELECT TOP (10)
            HTTP_METHOD AS HttpMethod,
            ENDPOINT AS Endpoint,
            COUNT_BIG(1) AS Total,
            SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
            AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs,
            MAX(DURATION_MS) AS MaximumDurationMs
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY HTTP_METHOD, ENDPOINT
        HAVING COUNT_BIG(1) >= 2
        ORDER BY AverageDurationMs DESC, MaximumDurationMs DESC;

        SELECT TOP (10)
            HTTP_METHOD AS HttpMethod,
            ENDPOINT AS Endpoint,
            COUNT_BIG(1) AS Total,
            SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
            AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs,
            MAX(DURATION_MS) AS MaximumDurationMs
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND STATUS_CODE >= 400
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY HTTP_METHOD, ENDPOINT
        ORDER BY Errors DESC, Total DESC;

        SELECT TOP (10)
            USER_NAME AS UserName,
            HTTP_METHOD AS HttpMethod,
            ENDPOINT AS Endpoint,
            COUNT_BIG(1) AS Total,
            AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs,
            MAX(DURATION_MS) AS MaximumDurationMs
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND USER_NAME <> 'anonymous'
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY USER_NAME, HTTP_METHOD, ENDPOINT
        ORDER BY AverageDurationMs DESC, MaximumDurationMs DESC;

        SELECT TOP (10) Consumer, ConsumerType, Total, Errors, AverageDurationMs
        FROM (
            SELECT
                USER_NAME AS Consumer,
                'user' AS ConsumerType,
                COUNT_BIG(1) AS Total,
                SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
                AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs
            FROM dbo.API_TRAFFIC_AUDIT
            WHERE REQUESTED_AT_UTC >= @FromUtc
              AND REQUESTED_AT_UTC < @ToUtc
              AND USER_NAME <> 'anonymous'
              AND (@ApiName IS NULL OR API_NAME = @ApiName)
              AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
            GROUP BY USER_NAME
            UNION ALL
            SELECT
                SOURCE_SYSTEM AS Consumer,
                'system' AS ConsumerType,
                COUNT_BIG(1) AS Total,
                SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
                AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs
            FROM dbo.API_TRAFFIC_AUDIT
            WHERE REQUESTED_AT_UTC >= @FromUtc
              AND REQUESTED_AT_UTC < @ToUtc
              AND SOURCE_SYSTEM IS NOT NULL
              AND (@ApiName IS NULL OR API_NAME = @ApiName)
              AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
            GROUP BY SOURCE_SYSTEM
        ) consumers
        ORDER BY Total DESC, Errors DESC;

        SELECT TOP (10)
            USER_NAME AS UserName,
            COUNT_BIG(1) AS Total,
            SUM(CASE WHEN STATUS_CODE >= 400 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Errors,
            AVG(CAST(DURATION_MS AS float)) AS AverageDurationMs
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND USER_NAME <> 'anonymous'
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY USER_NAME
        ORDER BY Total DESC, Errors DESC;

        SELECT API_NAME AS Name, COUNT_BIG(1) AS Total
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY API_NAME
        ORDER BY Total DESC;

        SELECT ENVIRONMENT AS Name, COUNT_BIG(1) AS Total
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        GROUP BY ENVIRONMENT
        ORDER BY Total DESC;

        SELECT TOP (20)
            REQUESTED_AT_UTC AS RequestedAtUtc,
            HTTP_METHOD AS HttpMethod,
            ENDPOINT AS Endpoint,
            STATUS_CODE AS StatusCode,
            DURATION_MS AS DurationMs,
            USER_NAME AS UserName,
            SOURCE_SYSTEM AS SourceSystem,
            TRACE_ID AS TraceId
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc
          AND REQUESTED_AT_UTC < @ToUtc
          AND STATUS_CODE >= 400
          AND (@ApiName IS NULL OR API_NAME = @ApiName)
          AND (@Environment IS NULL OR ENVIRONMENT = @Environment)
        ORDER BY REQUESTED_AT_UTC DESC;

        SELECT DISTINCT API_NAME
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc AND REQUESTED_AT_UTC < @ToUtc
        ORDER BY API_NAME;

        SELECT DISTINCT ENVIRONMENT
        FROM dbo.API_TRAFFIC_AUDIT
        WHERE REQUESTED_AT_UTC >= @FromUtc AND REQUESTED_AT_UTC < @ToUtc
        ORDER BY ENVIRONMENT;
        """;

    private readonly AuditSqlConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly TrafficMonitoringOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<TrafficDashboardDto> GetDashboardAsync(
        int periodDays,
        string? apiName,
        string? environment,
        CancellationToken cancellationToken = default)
    {
        if (!_connectionFactory.IsConfigured)
        {
            throw new InvalidOperationException(
                "Audit database connection string is not configured.");
        }

        var normalizedPeriod = Math.Clamp(periodDays, 1, _options.MaxAnalyticsPeriodDays);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-normalizedPeriod);
        var parameters = new
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ApiName = NormalizeFilter(apiName),
            Environment = NormalizeFilter(environment),
        };

        await using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var result = await connection.QueryMultipleAsync(
            new CommandDefinition(
                DashboardSql,
                parameters,
                commandTimeout: _connectionFactory.CommandTimeoutSeconds,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        var summary = await result.ReadSingleAsync<TrafficSummaryDto>().ConfigureAwait(false);
        var statusCodes = (await result.ReadAsync<TrafficStatusCountDto>().ConfigureAwait(false)).AsList();
        var timeline = (await result.ReadAsync<TrafficTimelinePointDto>().ConfigureAwait(false)).AsList();
        var requestsPerMinute = (await result.ReadAsync<TrafficMinutePointDto>().ConfigureAwait(false)).AsList();
        var errors500ByHour = (await result.ReadAsync<TrafficError500PointDto>().ConfigureAwait(false)).AsList();
        var topEndpoints = (await result.ReadAsync<TrafficEndpointMetricDto>().ConfigureAwait(false)).AsList();
        var slowestEndpoints = (await result.ReadAsync<TrafficEndpointMetricDto>().ConfigureAwait(false)).AsList();
        var topErrorEndpoints = (await result.ReadAsync<TrafficEndpointMetricDto>().ConfigureAwait(false)).AsList();
        var slowestByUser = (await result.ReadAsync<TrafficSlowEndpointByUserDto>().ConfigureAwait(false)).AsList();
        var topConsumers = (await result.ReadAsync<TrafficConsumerMetricDto>().ConfigureAwait(false)).AsList();
        var topUsers = (await result.ReadAsync<TrafficUserMetricDto>().ConfigureAwait(false)).AsList();
        var requestsByApi = (await result.ReadAsync<TrafficDimensionMetricDto>().ConfigureAwait(false)).AsList();
        var requestsByEnvironment = (await result.ReadAsync<TrafficDimensionMetricDto>().ConfigureAwait(false)).AsList();
        var recentErrors = (await result.ReadAsync<TrafficRecentErrorDto>().ConfigureAwait(false)).AsList();
        var apiNames = (await result.ReadAsync<string>().ConfigureAwait(false)).AsList();
        var environments = (await result.ReadAsync<string>().ConfigureAwait(false)).AsList();

        return new TrafficDashboardDto(
            toUtc,
            fromUtc,
            toUtc,
            summary,
            statusCodes,
            timeline,
            requestsPerMinute,
            errors500ByHour,
            topEndpoints,
            slowestEndpoints,
            topErrorEndpoints,
            slowestByUser,
            topConsumers,
            topUsers,
            requestsByApi,
            requestsByEnvironment,
            recentErrors,
            new TrafficFilterOptionsDto(apiNames, environments));
    }

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using System.Data;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Features.TrafficMonitoring;
using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Monitoring;

/// <summary>
/// Persists API traffic records in SQL Server using bulk copy.
/// </summary>
public sealed class SqlServerTrafficRequestStore(AuditSqlConnectionFactory connectionFactory)
    : ITrafficRequestStore
{
    private readonly AuditSqlConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task WriteBatchAsync(
        IReadOnlyCollection<TrafficRequestRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0 || !_connectionFactory.IsConfigured)
        {
            return;
        }

        using var table = CreateDataTable(records);
        await using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var bulkCopy = new SqlBulkCopy(connection)
        {
            DestinationTableName = "[dbo].[API_TRAFFIC_AUDIT]",
            BatchSize = records.Count,
            BulkCopyTimeout = _connectionFactory.CommandTimeoutSeconds,
        };

        AddColumnMappings(bulkCopy);
        await bulkCopy.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
    }

    private static DataTable CreateDataTable(IEnumerable<TrafficRequestRecord> records)
    {
        var table = new DataTable();
        table.Columns.Add("ID", typeof(Guid));
        table.Columns.Add("REQUESTED_AT_UTC", typeof(DateTime));
        table.Columns.Add("HTTP_METHOD", typeof(string));
        table.Columns.Add("ENDPOINT", typeof(string));
        table.Columns.Add("RAW_PATH", typeof(string));
        table.Columns.Add("STATUS_CODE", typeof(int));
        table.Columns.Add("DURATION_MS", typeof(long));
        table.Columns.Add("USER_NAME", typeof(string));
        table.Columns.Add("SOURCE_IP", typeof(string));
        table.Columns.Add("API_NAME", typeof(string));
        table.Columns.Add("ENVIRONMENT", typeof(string));
        table.Columns.Add("SOURCE_SYSTEM", typeof(string));
        table.Columns.Add("USER_AGENT", typeof(string));
        table.Columns.Add("TRACE_ID", typeof(string));

        foreach (var record in records)
        {
            table.Rows.Add(
                record.Id,
                record.RequestedAtUtc,
                record.HttpMethod,
                record.Endpoint,
                record.RawPath,
                record.StatusCode,
                record.DurationMs,
                record.UserName,
                DbValue(record.SourceIp),
                record.ApiName,
                record.Environment,
                DbValue(record.SourceSystem),
                DbValue(record.UserAgent),
                DbValue(record.TraceId));
        }

        return table;
    }

    private static object DbValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static void AddColumnMappings(SqlBulkCopy bulkCopy)
    {
        foreach (DataColumn column in CreateMappingTable().Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }
    }

    private static DataTable CreateMappingTable()
    {
        var table = new DataTable();
        foreach (var columnName in new[]
        {
            "ID",
            "REQUESTED_AT_UTC",
            "HTTP_METHOD",
            "ENDPOINT",
            "RAW_PATH",
            "STATUS_CODE",
            "DURATION_MS",
            "USER_NAME",
            "SOURCE_IP",
            "API_NAME",
            "ENVIRONMENT",
            "SOURCE_SYSTEM",
            "USER_AGENT",
            "TRACE_ID",
        })
        {
            table.Columns.Add(columnName);
        }

        return table;
    }
}

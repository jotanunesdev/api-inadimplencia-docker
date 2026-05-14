using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// SQL Server configuration used by the inadimplencia persistence adapter.
/// </summary>
public sealed class SqlServerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SqlServer";

    /// <summary>
    /// Optional SQL Server connection string. When empty, read/write endpoints return 503 and the API can still start.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}


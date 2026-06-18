using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// SQL Server configuration dedicated exclusively to API traffic auditing.
/// </summary>
public sealed class AuditDbOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "AuditDb";

    /// <summary>
    /// Gets the audit database connection string.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Gets the command timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 60;
}

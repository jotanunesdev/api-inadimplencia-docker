using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Monitoring;

/// <summary>
/// Creates SQL Server connections exclusively for the traffic audit database.
/// </summary>
public sealed class AuditSqlConnectionFactory(IOptions<AuditDbOptions> options)
{
    private readonly AuditDbOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Gets whether an audit database connection string was configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    /// <summary>
    /// Gets the configured command timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds => _options.CommandTimeoutSeconds;

    /// <summary>
    /// Creates and opens an audit database connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open SQL Server connection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the audit database connection string is missing.
    /// </exception>
    public async Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Audit database connection string is not configured.");
        }

        var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}

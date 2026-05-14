using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Creates SQL Server connections for the infrastructure layer.
/// </summary>
public sealed class SqlServerConnectionFactory(IOptions<SqlServerOptions> options)
{
    private readonly SqlServerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Gets whether a connection string was configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    /// <summary>
    /// Creates and opens a SQL Server connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open SQL connection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection string is not configured.</exception>
    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("SQL Server connection string is not configured.");
        }

        var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}


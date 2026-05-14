namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Executes parameterized SQL adapter operations during the incremental migration from the Node module.
/// </summary>
public interface ILegacySqlExecutor
{
    /// <summary>
    /// Gets whether the SQL Server connection was configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Executes a named read operation.
    /// </summary>
    /// <param name="queryKey">Stable query key.</param>
    /// <param name="parameters">Named SQL parameters.</param>
    /// <param name="single">Whether a single row should be returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Legacy SQL result.</returns>
    Task<LegacySqlResult> QueryAsync(
        string queryKey,
        IReadOnlyDictionary<string, object?> parameters,
        bool single,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a named write operation.
    /// </summary>
    /// <param name="commandKey">Stable command key.</param>
    /// <param name="parameters">Named SQL parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Legacy SQL result.</returns>
    Task<LegacySqlResult> ExecuteAsync(
        string commandKey,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result returned by the legacy SQL adapter.
/// </summary>
/// <param name="IsConfigured">Indicates whether the backing SQL Server is configured.</param>
/// <param name="Data">Returned row or row collection.</param>
/// <param name="RowsAffected">Rows affected by a command, when applicable.</param>
public sealed record LegacySqlResult(bool IsConfigured, object? Data, int? RowsAffected = null);


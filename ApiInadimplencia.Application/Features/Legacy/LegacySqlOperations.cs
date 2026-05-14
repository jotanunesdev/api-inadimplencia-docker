using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;

namespace ApiInadimplencia.Application.Features.Legacy;

/// <summary>
/// Query used by endpoint adapters while each use case is migrated from Node to typed handlers.
/// </summary>
/// <param name="QueryKey">Stable SQL query key.</param>
/// <param name="Parameters">Named SQL parameters.</param>
/// <param name="Single">Whether the endpoint expects a single row.</param>
public sealed record LegacySqlQuery(
    string QueryKey,
    IReadOnlyDictionary<string, object?> Parameters,
    bool Single = false) : IQuery<LegacySqlResult>;

/// <summary>
/// Command used by endpoint adapters while write flows are migrated to typed commands.
/// </summary>
/// <param name="CommandKey">Stable command key.</param>
/// <param name="Parameters">Named SQL parameters.</param>
public sealed record LegacySqlCommand(
    string CommandKey,
    IReadOnlyDictionary<string, object?> Parameters) : ICommand<LegacySqlResult>;

/// <summary>
/// Handles legacy SQL queries through the persistence port.
/// </summary>
/// <param name="executor">Legacy SQL executor.</param>
public sealed class LegacySqlQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<LegacySqlQuery, LegacySqlResult>
{
    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public Task<LegacySqlResult> HandleAsync(
        LegacySqlQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _executor.QueryAsync(query.QueryKey, query.Parameters, query.Single, cancellationToken);
    }
}

/// <summary>
/// Handles legacy SQL commands through the persistence port.
/// </summary>
/// <param name="executor">Legacy SQL executor.</param>
public sealed class LegacySqlCommandHandler(ILegacySqlExecutor executor)
    : ICommandHandler<LegacySqlCommand, LegacySqlResult>
{
    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public Task<LegacySqlResult> HandleAsync(
        LegacySqlCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _executor.ExecuteAsync(command.CommandKey, command.Parameters, cancellationToken);
    }
}


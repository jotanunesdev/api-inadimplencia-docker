namespace ApiInadimplencia.Application.Abstractions.Cqrs;

/// <summary>
/// Represents a read-only request.
/// </summary>
/// <typeparam name="TResponse">Query response type.</typeparam>
public interface IQuery<TResponse>
{
}

/// <summary>
/// Handles a query use case.
/// </summary>
/// <typeparam name="TQuery">Query type.</typeparam>
/// <typeparam name="TResponse">Query response type.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Executes the query.
    /// </summary>
    /// <param name="query">Query payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query response.</returns>
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}


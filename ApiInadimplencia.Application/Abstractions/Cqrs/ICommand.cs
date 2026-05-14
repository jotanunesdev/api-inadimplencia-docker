namespace ApiInadimplencia.Application.Abstractions.Cqrs;

/// <summary>
/// Represents an intent to change system state.
/// </summary>
/// <typeparam name="TResponse">Command response type.</typeparam>
public interface ICommand<TResponse>
{
}

/// <summary>
/// Handles a command use case.
/// </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
/// <typeparam name="TResponse">Command response type.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="command">Command payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Command response.</returns>
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}


using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Application.Features.Notifications;

/// <summary>
/// Port for dispatching notifications with persistence and SSE push in a single call site.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches a notification to a single user.
    /// Persists the notification and pushes via SSE (best-effort).
    /// </summary>
    /// <param name="tipo">Type of notification.</param>
    /// <param name="username">Target username (case-insensitive).</param>
    /// <param name="numVenda">Sale number (optional).</param>
    /// <param name="mensagem">Notification message.</param>
    /// <param name="proximaAcaoDia">Next action date for dedupe (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification ID.</returns>
    Task<Guid> DispatchAsync(
        NotificationType tipo,
        string username,
        int? numVenda,
        string mensagem,
        DateOnly? proximaAcaoDia = null,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a notification to multiple users.
    /// Each user receives a separate notification; failures are isolated.
    /// </summary>
    /// <param name="tipo">Type of notification.</param>
    /// <param name="usernames">Target usernames (case-insensitive).</param>
    /// <param name="numVenda">Sale number (optional).</param>
    /// <param name="mensagem">Notification message.</param>
    /// <param name="proximaAcaoDia">Next action date for dedupe (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping usernames to notification IDs (null if failed).</returns>
    Task<Dictionary<string, Guid?>> DispatchManyAsync(
        NotificationType tipo,
        IReadOnlyList<string> usernames,
        int? numVenda,
        string mensagem,
        DateOnly? proximaAcaoDia = null,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default);
}

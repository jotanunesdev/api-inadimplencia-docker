using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Notifications.Dtos;
using ApiInadimplencia.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Infrastructure.Notifications;

/// <summary>
/// Adapter for INotificationDispatcher that orchestrates persistence + SSE push.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly ICommandHandler<CreateNotificationCommand, Guid> _createNotificationHandler;
    private readonly INotificationRepository _notificationRepository;
    private readonly SseHub _sseHub;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ICommandHandler<CreateNotificationCommand, Guid> createNotificationHandler,
        INotificationRepository notificationRepository,
        SseHub sseHub,
        ILogger<NotificationDispatcher> logger)
    {
        _createNotificationHandler = createNotificationHandler;
        _notificationRepository = notificationRepository;
        _sseHub = sseHub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> DispatchAsync(
        NotificationType tipo,
        string username,
        int? numVenda,
        string mensagem,
        DateOnly? proximaAcaoDia = null,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default)
    {
        // Persist the notification (with dedupe)
        var command = new CreateNotificationCommand(
            tipo,
            username,
            numVenda ?? 0,
            proximaAcaoDia,
            mensagem,
            dedupeKey);

        var notificationId = await _createNotificationHandler.HandleAsync(command, cancellationToken);

        // Push via SSE (best-effort)
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);

            if (notification is not null)
            {
                var payload = NotificationSsePayloadMapper.ToSnapshotPayload(notification);
                await _sseHub.BroadcastNotificationAsync(username, payload, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't fail - persistence is guaranteed
            _logger.LogWarning(ex, "Failed to push notification via SSE for user {Username}. Notification was persisted.", username);
        }

        return notificationId;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Guid?>> DispatchManyAsync(
        NotificationType tipo,
        IReadOnlyList<string> usernames,
        int? numVenda,
        string mensagem,
        DateOnly? proximaAcaoDia = null,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DispatchManyAsync called. Tipo={Tipo} NumVenda={NumVenda} Targets={Targets}",
            tipo, numVenda, string.Join(",", usernames));

        var results = new Dictionary<string, Guid?>();

        foreach (var username in usernames)
        {
            try
            {
                var notificationId = await DispatchAsync(tipo, username, numVenda, mensagem, proximaAcaoDia, dedupeKey, cancellationToken);
                results[username] = notificationId;
                _logger.LogInformation(
                    "Notification dispatched. User={Username} Tipo={Tipo} NotificationId={NotificationId}",
                    username, tipo, notificationId);
            }
            catch (Exception ex)
            {
                // Isolate failures - continue with other users
                _logger.LogError(ex, "Failed to dispatch notification to user {Username} (Tipo={Tipo}). Continuing with other users.", username, tipo);
                results[username] = null;
            }
        }

        return results;
    }
}

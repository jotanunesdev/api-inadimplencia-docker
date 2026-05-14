using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Notifications.Dtos;
using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Application.Features.Notifications.Commands;

/// <summary>
/// Handles the creation of a new notification with deduplication.
/// </summary>
public class CreateNotificationCommandHandler : ICommandHandler<CreateNotificationCommand, Guid>
{
    private readonly INotificationRepository _notificationRepository;

    public CreateNotificationCommandHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    /// <inheritdoc />
    public async Task<Guid> HandleAsync(CreateNotificationCommand command, CancellationToken cancellationToken = default)
    {
        // Check for existing notification with same dedupe key
        var existing = await _notificationRepository.GetByDedupeKeyAsync(
            command.Tipo,
            command.Usuario,
            command.NumVenda,
            command.ProximaAcaoDia,
            cancellationToken);

        if (existing != null)
        {
            // Return existing notification ID (idempotent)
            return existing.Id;
        }

        // Create the notification domain entity
        var notificacao = InadNotificacao.Criar(
            command.Tipo,
            command.Usuario,
            command.NumVenda,
            command.ProximaAcaoDia,
            command.Mensagem);

        // Persist the notification
        await _notificationRepository.AddAsync(notificacao, cancellationToken);

        return notificacao.Id;
    }
}

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Application.Features.Notifications.Dtos;

/// <summary>
/// Command to create a notification.
/// </summary>
public record CreateNotificationCommand(
    NotificationType Tipo,
    string Usuario,
    int NumVenda,
    DateOnly? ProximaAcaoDia,
    string Mensagem,
    string? DedupeKey = null) : ICommand<Guid>;

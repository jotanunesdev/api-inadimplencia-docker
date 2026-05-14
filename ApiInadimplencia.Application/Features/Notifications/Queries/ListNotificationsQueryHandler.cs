using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Notifications.Dtos;
using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Application.Features.Notifications.Queries;

/// <summary>
/// Handles listing notifications for a user.
/// </summary>
public class ListNotificationsQueryHandler : IQueryHandler<ListNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly INotificationRepository _notificationRepository;

    public ListNotificationsQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    /// <inheritdoc />
    public async Task<PagedResult<NotificationDto>> HandleAsync(ListNotificationsQuery query, CancellationToken cancellationToken = default)
    {
        var username = query.Username.ToLowerInvariant();
        
        var notificacoes = await _notificationRepository.ListAsync(
            username,
            query.Lida,
            query.Page,
            query.PageSize,
            cancellationToken);

        var total = await _notificationRepository.CountAsync(username, query.Lida, cancellationToken);

        var dtos = notificacoes.Select(n => new NotificationDto
        {
            Id = n.Id,
            Tipo = n.Tipo.ToString(),
            Usuario = n.Usuario,
            NumVenda = n.NumVenda,
            ProximaAcaoDia = n.ProximaAcaoDia,
            Mensagem = n.Mensagem,
            Lida = n.Lida,
            CriadaEm = n.CriadaEm
        });

        return new PagedResult<NotificationDto>
        {
            Items = dtos,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}

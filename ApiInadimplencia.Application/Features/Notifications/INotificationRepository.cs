using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Application.Features.Notifications;

/// <summary>
/// Repository interface for notifications.
/// </summary>
public interface INotificationRepository
{
    Task AddAsync(InadNotificacao notificacao, CancellationToken cancellationToken);
    Task<InadNotificacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<InadNotificacao?> GetByDedupeKeyAsync(
        NotificationType tipo,
        string usuario,
        int numVenda,
        DateOnly? proximaAcaoDia,
        CancellationToken cancellationToken);
    Task UpdateAsync(InadNotificacao notificacao, CancellationToken cancellationToken);
    Task DeleteAsync(InadNotificacao notificacao, CancellationToken cancellationToken);
    Task<IEnumerable<InadNotificacao>> ListAsync(
        string username,
        bool? lida,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<int> CountAsync(string username, bool? lida, CancellationToken cancellationToken);
    Task MarkAllAsReadAsync(string username, CancellationToken cancellationToken);
}

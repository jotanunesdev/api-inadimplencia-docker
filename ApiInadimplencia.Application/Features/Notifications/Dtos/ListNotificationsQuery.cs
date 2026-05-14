using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Notifications.Dtos;

/// <summary>
/// Query to list notifications for a user.
/// </summary>
public record ListNotificationsQuery(
    string Username,
    int Page = 1,
    int PageSize = 20,
    bool? Lida = null) : IQuery<PagedResult<NotificationDto>>;

/// <summary>
/// Paged result wrapper.
/// </summary>
public record PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Notifications.Dtos;

/// <summary>
/// Command to mark all notifications as read for a user.
/// </summary>
public record MarkAllNotificationsAsReadCommand(
    string Username) : ICommand<int>;

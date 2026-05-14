namespace ApiInadimplencia.Domain.Notifications;

/// <summary>
/// Types persisted in dbo.INAD_NOTIFICACOES.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Sale was assigned to a responsible user.
    /// </summary>
    VendaAtribuida,

    /// <summary>
    /// Sale has an overdue next action.
    /// </summary>
    VendaAtrasada,
}


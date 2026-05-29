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

    /// <summary>
    /// Serasa negativacao request was created.
    /// </summary>
    SolicitacaoNegativacao,

    /// <summary>
    /// Serasa negativacao request was approved.
    /// </summary>
    AprovacaoNegativacao,

    /// <summary>
    /// Serasa negativacao request was rejected.
    /// </summary>
    RejeicaoNegativacao,

    /// <summary>
    /// Serasa returned successful negativacao result.
    /// </summary>
    RetornoSerasaSucesso,

    /// <summary>
    /// Serasa returned error in negativacao result.
    /// </summary>
    RetornoSerasaErro,
}


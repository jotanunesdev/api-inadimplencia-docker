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

    /// <summary>
    /// Serasa baixa (write-off) request was created and is awaiting approval.
    /// </summary>
    SolicitacaoBaixa,

    /// <summary>
    /// Serasa baixa request was approved/rejected (sent to requester).
    /// </summary>
    AprovacaoBaixa,

    /// <summary>
    /// Serasa baixa returned successful result via webhook.
    /// </summary>
    RetornoBaixaSucesso,

    /// <summary>
    /// Serasa baixa returned error result via webhook.
    /// </summary>
    RetornoBaixaErro,
}


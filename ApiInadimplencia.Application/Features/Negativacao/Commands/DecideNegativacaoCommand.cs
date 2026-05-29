using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Commands;

/// <summary>
/// Decision type for negativacao approval workflow.
/// </summary>
public enum DecisaoNegativacao
{
    /// <summary>Approve the solicitation and send to Serasa.</summary>
    APROVAR,
    
    /// <summary>Reject the solicitation with a justification.</summary>
    REJEITAR
}

/// <summary>
/// Command to approve or reject a negativacao solicitation.
/// Requires approver authorization and transaction password validation.
/// </summary>
/// <param name="SolicitacaoId">Solicitation ID to decide.</param>
/// <param name="Decisao">Decision type (APROVAR or REJEITAR).</param>
/// <param name="SenhaTransacao">Transaction password for authentication.</param>
/// <param name="Justificativa">Rejection justification (required for REJEITAR).</param>
public record DecideNegativacaoCommand(
    Guid SolicitacaoId,
    DecisaoNegativacao Decisao,
    string SenhaTransacao,
    string? Justificativa = null) : ICommand<bool>;

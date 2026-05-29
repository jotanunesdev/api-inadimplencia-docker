using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Commands;

/// <summary>
/// Command to request a negativacao through the approval workflow.
/// Creates a solicitation in AGUARDANDO_APROVACAO status, an occurrence, and notifies approvers.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="ParcelaIds">List of parcela IDs to include in the negativacao.</param>
/// <param name="IncluirFiadores">Whether to include guarantors in the negativacao.</param>
/// <param name="SenhaTransacao">Transaction password for authentication.</param>
public sealed record RequestNegativacaoFluxoCommand(
    int NumVenda,
    IReadOnlyList<long> ParcelaIds,
    bool IncluirFiadores,
    string SenhaTransacao) : ICommand<Guid>;

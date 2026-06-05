using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Query to list pending negativacao solicitations awaiting approval.
/// </summary>
/// <param name="Status">Optional status filter (default: AGUARDANDO_APROVACAO).</param>
/// <param name="NumVenda">Optional sale number filter.</param>
/// <param name="SolicitacaoId">Optional specific solicitation ID filter (when provided, other filters are ignored).</param>
/// <param name="SolicitanteUsername">Optional requester username filter.</param>
/// <param name="Take">Number of results to return (default: 50).</param>
/// <param name="Skip">Number of results to skip (default: 0).</param>
public sealed record ListSolicitacoesPendentesQuery(
    string? Status = "AGUARDANDO_APROVACAO",
    int? NumVenda = null,
    Guid? SolicitacaoId = null,
    string? SolicitanteUsername = null,
    int Take = 50,
    int Skip = 0) : IQuery<IReadOnlyList<SolicitacaoPendenteDto>>;

/// <summary>
/// DTO for a pending negativacao solicitation.
/// </summary>
/// <param name="Id">Solicitation ID.</param>
/// <param name="NumVenda">Sale number.</param>
/// <param name="Cliente">Client name.</param>
/// <param name="CpfMasked">Masked CPF of the debtor.</param>
/// <param name="SolicitanteUsername">Username of the requester.</param>
/// <param name="DtSolicitacao">Request timestamp (UTC).</param>
/// <param name="Status">Current status.</param>
/// <param name="Valor">Total debt value.</param>
public sealed record SolicitacaoPendenteDto(
    Guid Id,
    int NumVenda,
    string Cliente,
    string CpfMasked,
    string SolicitanteUsername,
    DateTime DtSolicitacao,
    string Status,
    decimal Valor,
    string Tipo = "NEGATIVACAO");

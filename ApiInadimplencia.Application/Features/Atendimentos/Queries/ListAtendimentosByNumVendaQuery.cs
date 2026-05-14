using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;

namespace ApiInadimplencia.Application.Features.Atendimentos.Queries;

/// <summary>
/// Query to list attendances by sale number.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
public sealed record ListAtendimentosByNumVendaQuery(int NumVenda) : IQuery<IReadOnlyList<AtendimentoDto>>;

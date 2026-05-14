using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;

namespace ApiInadimplencia.Application.Features.Fiadores.Queries;

/// <summary>
/// Query to get guarantors by sale number (NUM_VENDA).
/// </summary>
/// <param name="NumVenda">Sale number.</param>
public sealed record GetFiadoresByNumVendaQuery(int NumVenda) : IQuery<IReadOnlyList<FiadorDto>>;

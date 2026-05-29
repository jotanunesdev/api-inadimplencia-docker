using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Query to retrieve eligible debts (parcelas) for a sale.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
public sealed record GetDividasElegiveisQuery(int NumVenda) : IQuery<DividasElegiveisResponse>;

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Query to get a defaulted sale by NUM_VENDA.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
public sealed record GetInadimplenciaByNumVendaQuery(int NumVenda) : IQuery<InadimplenciaDto?>;

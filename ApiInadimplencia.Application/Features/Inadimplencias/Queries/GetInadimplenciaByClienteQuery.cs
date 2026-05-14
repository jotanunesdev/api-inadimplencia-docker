using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Query to get defaulted sales by customer name (LIKE search).
/// </summary>
/// <param name="NomeCliente">Customer name to search.</param>
public sealed record GetInadimplenciaByClienteQuery(string NomeCliente) : IQuery<IReadOnlyList<InadimplenciaDto>>;

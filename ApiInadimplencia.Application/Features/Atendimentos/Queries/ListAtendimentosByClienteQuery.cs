using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;

namespace ApiInadimplencia.Application.Features.Atendimentos.Queries;

/// <summary>
/// Query to list attendances by customer name.
/// </summary>
/// <param name="NomeCliente">Customer name.</param>
public sealed record ListAtendimentosByClienteQuery(string NomeCliente) : IQuery<IReadOnlyList<AtendimentoDto>>;

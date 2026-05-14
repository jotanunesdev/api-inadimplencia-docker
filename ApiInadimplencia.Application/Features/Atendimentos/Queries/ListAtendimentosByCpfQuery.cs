using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;

namespace ApiInadimplencia.Application.Features.Atendimentos.Queries;

/// <summary>
/// Query to list attendances by CPF.
/// </summary>
/// <param name="Cpf">Customer CPF (digits only).</param>
public sealed record ListAtendimentosByCpfQuery(string Cpf) : IQuery<IReadOnlyList<AtendimentoDto>>;

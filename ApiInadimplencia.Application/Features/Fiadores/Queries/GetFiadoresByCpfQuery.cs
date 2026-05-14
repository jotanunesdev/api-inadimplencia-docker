using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;

namespace ApiInadimplencia.Application.Features.Fiadores.Queries;

/// <summary>
/// Query to get guarantors by CPF/CNPJ (digits only).
/// </summary>
/// <param name="Cpf">CPF/CNPJ with non-digits removed.</param>
public sealed record GetFiadoresByCpfQuery(string Cpf) : IQuery<IReadOnlyList<FiadorDto>>;

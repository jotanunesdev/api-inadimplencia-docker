using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Query to get defaulted sales by CPF/CNPJ (digits only).
/// </summary>
/// <param name="Cpf">CPF/CNPJ with non-digits removed.</param>
public sealed record GetInadimplenciaByCpfQuery(string Cpf) : IQuery<IReadOnlyList<InadimplenciaDto>>;

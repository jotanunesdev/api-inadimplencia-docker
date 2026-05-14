using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Query to get defaulted sales by responsible user name (includes user color).
/// </summary>
/// <param name="Nome">Responsible user name.</param>
public sealed record GetInadimplenciaByResponsavelQuery(string Nome) : IQuery<IReadOnlyList<InadimplenciaDto>>;

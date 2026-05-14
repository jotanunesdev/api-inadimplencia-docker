using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Queries;

/// <summary>
/// Query to list all occurrences.
/// </summary>
public sealed record ListOcorrenciasQuery : IQuery<IReadOnlyList<OcorrenciaDto>>;

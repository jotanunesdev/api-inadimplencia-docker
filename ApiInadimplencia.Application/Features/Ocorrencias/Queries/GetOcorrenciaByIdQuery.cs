using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Queries;

/// <summary>
/// Query to get an occurrence by ID.
/// </summary>
/// <param name="Id">Occurrence ID.</param>
public sealed record GetOcorrenciaByIdQuery(Guid Id) : IQuery<OcorrenciaDto?>;

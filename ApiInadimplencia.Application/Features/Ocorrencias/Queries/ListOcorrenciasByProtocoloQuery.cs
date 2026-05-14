using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Queries;

/// <summary>
/// Query to list occurrences by protocol.
/// </summary>
/// <param name="Protocolo">Protocol number.</param>
public sealed record ListOcorrenciasByProtocoloQuery(string Protocolo) : IQuery<IReadOnlyList<OcorrenciaDto>>;

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Queries;

/// <summary>
/// Query to list occurrences by sale number.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
public sealed record ListOcorrenciasByNumVendaQuery(int NumVenda) : IQuery<IReadOnlyList<OcorrenciaDto>>;

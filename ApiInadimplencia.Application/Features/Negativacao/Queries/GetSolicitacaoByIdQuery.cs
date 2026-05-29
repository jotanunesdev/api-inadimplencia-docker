using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Query to get a single negativacao solicitation by ID with full details including parcelas and fiadores.
/// </summary>
/// <param name="Id">Solicitation ID.</param>
/// <param name="Username">Optional fallback username resolved by the HTTP endpoint.</param>
public sealed record GetSolicitacaoByIdQuery(Guid Id, string? Username = null) : IQuery<SolicitacaoDetalheDto?>;

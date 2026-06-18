using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Query to list all defaulted sales (inadimplências) with latest next action.
/// </summary>
public sealed record ListInadimplenciasQuery(
    int Page = 1,
    int PageSize = 5000) : IQuery<PagedInadimplenciaResult>;

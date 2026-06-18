using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Paged result for inadimplencia read endpoints.
/// </summary>
public sealed record PagedInadimplenciaResult(
    IReadOnlyList<InadimplenciaDto> Items,
    int Total,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Total number of available pages.
    /// </summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)Total / PageSize);
}

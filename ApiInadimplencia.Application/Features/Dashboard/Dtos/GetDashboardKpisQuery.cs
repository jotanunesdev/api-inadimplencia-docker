using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Query to get dashboard KPIs.
/// </summary>
public sealed record GetDashboardKpisQuery : IQuery<DashboardKpisDto>;

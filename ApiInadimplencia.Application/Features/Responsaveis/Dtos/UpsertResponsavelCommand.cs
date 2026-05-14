using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Responsaveis.Dtos;

/// <summary>
/// Command to upsert a responsible user assignment for a sale.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="Username">Username of the responsible user.</param>
/// <param name="AdminUserCode">Admin user code performing the assignment.</param>
public record UpsertResponsavelCommand(
    int NumVendaFk,
    string Username,
    string AdminUserCode) : ICommand<ResponsavelDto>;

using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Responsaveis.Dtos;

/// <summary>
/// Command to update a responsible user assignment for a sale.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="Username">New username of the responsible user.</param>
/// <param name="AdminUserCode">Admin user code performing the update.</param>
public record UpdateResponsavelCommand(
    int NumVendaFk,
    string Username,
    string AdminUserCode) : ICommand<ResponsavelDto>;

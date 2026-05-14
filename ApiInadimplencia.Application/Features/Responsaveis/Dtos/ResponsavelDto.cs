namespace ApiInadimplencia.Application.Features.Responsaveis.Dtos;

/// <summary>
/// DTO representing a responsible user assignment.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="Username">Username of the responsible user.</param>
/// <param name="AtribuidoEm">Timestamp when the assignment was created.</param>
/// <param name="AtribuidoPor">Username of the admin who performed the assignment.</param>
public record ResponsavelDto(
    int NumVendaFk,
    string Username,
    DateTime AtribuidoEm,
    string AtribuidoPor);

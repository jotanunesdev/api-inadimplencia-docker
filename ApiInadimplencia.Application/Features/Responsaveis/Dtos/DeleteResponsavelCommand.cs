using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Responsaveis.Dtos;

/// <summary>
/// Command to delete a responsible user assignment for a sale.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
public record DeleteResponsavelCommand(int NumVendaFk) : ICommand<bool>;

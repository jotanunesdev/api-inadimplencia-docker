using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Responsaveis.Dtos;
using ApiInadimplencia.Domain.Responsaveis;

namespace ApiInadimplencia.Application.Features.Responsaveis.Commands;

/// <summary>
/// Handles update of a responsible user assignment for a sale.
/// </summary>
public class UpdateResponsavelCommandHandler : ICommandHandler<UpdateResponsavelCommand, ResponsavelDto>
{
    private readonly IResponsavelRepository _responsavelRepository;
    private readonly IUsuarioValidator _usuarioValidator;

    public UpdateResponsavelCommandHandler(
        IResponsavelRepository responsavelRepository,
        IUsuarioValidator usuarioValidator)
    {
        _responsavelRepository = responsavelRepository;
        _usuarioValidator = usuarioValidator;
    }

    /// <inheritdoc />
    public async Task<ResponsavelDto> HandleAsync(UpdateResponsavelCommand command, CancellationToken cancellationToken = default)
    {
        // Validate that the admin user exists and has admin profile
        var isAdmin = await _usuarioValidator.IsAdminUserAsync(command.AdminUserCode, cancellationToken);
        if (!isAdmin)
        {
            throw new InvalidOperationException($"User '{command.AdminUserCode}' is not an admin or does not exist.");
        }

        var responsavel = await _responsavelRepository.GetByNumVendaAsync(command.NumVendaFk, cancellationToken);
        if (responsavel == null)
        {
            throw new InvalidOperationException($"No responsible assignment found for sale {command.NumVendaFk}.");
        }

        responsavel.AtualizarResponsavel(command.Username, command.AdminUserCode);
        await _responsavelRepository.UpsertAsync(responsavel, cancellationToken);

        return new ResponsavelDto(
            responsavel.NumVendaFk,
            responsavel.Username,
            responsavel.AtribuidoEm,
            responsavel.AtribuidoPor);
    }
}

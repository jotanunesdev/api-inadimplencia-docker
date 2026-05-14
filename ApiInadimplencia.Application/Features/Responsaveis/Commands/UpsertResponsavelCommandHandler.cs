using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Responsaveis.Dtos;
using ApiInadimplencia.Domain.Responsaveis;

namespace ApiInadimplencia.Application.Features.Responsaveis.Commands;

/// <summary>
/// Handles upsert of a responsible user assignment for a sale.
/// </summary>
public class UpsertResponsavelCommandHandler : ICommandHandler<UpsertResponsavelCommand, ResponsavelDto>
{
    private readonly IResponsavelRepository _responsavelRepository;
    private readonly IUsuarioValidator _usuarioValidator;

    public UpsertResponsavelCommandHandler(
        IResponsavelRepository responsavelRepository,
        IUsuarioValidator usuarioValidator)
    {
        _responsavelRepository = responsavelRepository;
        _usuarioValidator = usuarioValidator;
    }

    /// <inheritdoc />
    public async Task<ResponsavelDto> HandleAsync(UpsertResponsavelCommand command, CancellationToken cancellationToken = default)
    {
        // Validate that the admin user exists and has admin profile
        var isAdmin = await _usuarioValidator.IsAdminUserAsync(command.AdminUserCode, cancellationToken);
        if (!isAdmin)
        {
            throw new InvalidOperationException($"User '{command.AdminUserCode}' is not an admin or does not exist.");
        }

        // Try to find existing assignment
        var existing = await _responsavelRepository.GetByNumVendaAsync(command.NumVendaFk, cancellationToken);
        VendaResponsavel responsavel;

        if (existing != null)
        {
            // Update if username changed
            if (existing.Username != command.Username)
            {
                existing.AtualizarResponsavel(command.Username, command.AdminUserCode);
            }
            responsavel = existing;
        }
        else
        {
            // Create new assignment
            responsavel = VendaResponsavel.Criar(command.NumVendaFk, command.Username, command.AdminUserCode);
        }

        await _responsavelRepository.UpsertAsync(responsavel, cancellationToken);

        return new ResponsavelDto(
            responsavel.NumVendaFk,
            responsavel.Username,
            responsavel.AtribuidoEm,
            responsavel.AtribuidoPor);
    }
}

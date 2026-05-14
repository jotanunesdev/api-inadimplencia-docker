using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Usuarios.Dtos;
using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Application.Features.Usuarios.Commands;

/// <summary>
/// Handles upsert of a user (create if not exists, update if exists).
/// </summary>
public class UpsertUsuarioCommandHandler : ICommandHandler<UpsertUsuarioCommand, UpsertUsuarioResult>
{
    private readonly IUsuarioRepository _usuarioRepository;

    public UpsertUsuarioCommandHandler(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    /// <inheritdoc />
    public async Task<UpsertUsuarioResult> HandleAsync(UpsertUsuarioCommand command, CancellationToken cancellationToken = default)
    {
        // Determine default profile if not provided
        var perfil = command.Perfil ?? (command.UserCode.Equals("wffluig", StringComparison.OrdinalIgnoreCase) 
            ? UserProfile.Admin 
            : UserProfile.Operador);

        // Normalize color if provided
        var corHex = command.CorHex ?? "#FFFFFF";

        // Try to find existing user by UserCode or Nome
        var existingUsuario = await _usuarioRepository.GetByUserCodeAsync(command.UserCode, cancellationToken);
        if (existingUsuario == null)
        {
            existingUsuario = await _usuarioRepository.GetByNomeAsync(command.Nome, cancellationToken);
        }

        Usuario usuario;
        bool exists;

        if (existingUsuario != null)
        {
            // Update existing user
            existingUsuario.Atualizar(command.Nome, perfil, corHex);
            await _usuarioRepository.UpdateAsync(existingUsuario, cancellationToken);
            usuario = existingUsuario;
            exists = true;
        }
        else
        {
            // Create new user
            usuario = Usuario.Criar(command.UserCode, command.Nome, perfil, corHex);
            await _usuarioRepository.AddAsync(usuario, cancellationToken);
            exists = false;
        }

        var usuarioDto = new UsuarioDto(
            usuario.UserCode,
            usuario.Nome,
            usuario.Perfil,
            usuario.CorHex);

        return new UpsertUsuarioResult(exists, usuarioDto);
    }
}

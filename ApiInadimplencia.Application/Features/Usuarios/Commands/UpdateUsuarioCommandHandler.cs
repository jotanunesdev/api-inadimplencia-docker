using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Usuarios.Dtos;
using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Application.Features.Usuarios.Commands;

/// <summary>
/// Handles update of an existing user.
/// </summary>
public class UpdateUsuarioCommandHandler : ICommandHandler<UpdateUsuarioCommand, UsuarioDto>
{
    private readonly IUsuarioRepository _usuarioRepository;

    public UpdateUsuarioCommandHandler(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    /// <inheritdoc />
    public async Task<UsuarioDto> HandleAsync(UpdateUsuarioCommand command, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByUserCodeAsync(command.UserCode, cancellationToken);
        if (usuario == null)
        {
            throw new InvalidOperationException($"User with code '{command.UserCode}' not found.");
        }

        usuario.Atualizar(command.Nome, command.Perfil, command.CorHex);
        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);

        return new UsuarioDto(
            usuario.UserCode,
            usuario.Nome,
            usuario.Perfil,
            usuario.CorHex);
    }
}

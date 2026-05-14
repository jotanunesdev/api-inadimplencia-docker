using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Application.Features.Usuarios.Dtos;

/// <summary>
/// Command to update an existing user.
/// </summary>
/// <param name="UserCode">User code to update.</param>
/// <param name="Nome">New name (optional).</param>
/// <param name="Perfil">New profile (optional).</param>
/// <param name="CorHex">New hex color (optional).</param>
public record UpdateUsuarioCommand(
    string UserCode,
    string? Nome = null,
    UserProfile? Perfil = null,
    string? CorHex = null) : ICommand<UsuarioDto>;

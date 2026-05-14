using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Application.Features.Usuarios.Dtos;

/// <summary>
/// DTO representing a user.
/// </summary>
/// <param name="UserCode">User code.</param>
/// <param name="Nome">User name.</param>
/// <param name="Perfil">User profile.</param>
/// <param name="CorHex">Hex color.</param>
public record UsuarioDto(
    string UserCode,
    string Nome,
    UserProfile Perfil,
    string CorHex);

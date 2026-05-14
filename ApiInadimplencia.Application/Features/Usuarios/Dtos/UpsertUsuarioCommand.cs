using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Application.Features.Usuarios.Dtos;

/// <summary>
/// Command to upsert a user (create if not exists, update if exists).
/// </summary>
/// <param name="UserCode">User code (unique identifier).</param>
/// <param name="Nome">User name.</param>
/// <param name="Perfil">User profile (admin or operador). If null, defaults to operador (or admin for wffluig).</param>
/// <param name="CorHex">Hex color in #RRGGBB format (with or without #).</param>
public record UpsertUsuarioCommand(
    string UserCode,
    string Nome,
    UserProfile? Perfil = null,
    string? CorHex = null) : ICommand<UpsertUsuarioResult>;

/// <summary>
/// Result of upsert user operation.
/// </summary>
/// <param name="Exists">True if user already existed, false if created.</param>
/// <param name="Usuario">The user DTO.</param>
public record UpsertUsuarioResult(bool Exists, UsuarioDto Usuario);

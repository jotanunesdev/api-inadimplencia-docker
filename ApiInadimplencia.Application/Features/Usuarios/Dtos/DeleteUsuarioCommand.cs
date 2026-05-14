using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Usuarios.Dtos;

/// <summary>
/// Command to delete a user.
/// </summary>
/// <param name="UserCode">User code to delete.</param>
public record DeleteUsuarioCommand(string UserCode) : ICommand<bool>;

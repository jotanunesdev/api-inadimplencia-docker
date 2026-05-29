using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Commands;

/// <summary>
/// Command to set or update a user's transaction password.
/// </summary>
/// <param name="Username">Username of the user.</param>
/// <param name="SenhaAtual">Current password (required when updating).</param>
/// <param name="NovaSenha">New password to set.</param>
public sealed record SetSenhaTransacaoCommand(
    string Username,
    string? SenhaAtual,
    string NovaSenha) : ICommand<bool>;

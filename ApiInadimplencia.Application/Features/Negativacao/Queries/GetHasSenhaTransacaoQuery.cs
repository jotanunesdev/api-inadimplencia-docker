using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Query to check if a user has a transaction password configured.
/// </summary>
/// <param name="Username">Username to check.</param>
public sealed record GetHasSenhaTransacaoQuery(string Username) : IQuery<bool>;

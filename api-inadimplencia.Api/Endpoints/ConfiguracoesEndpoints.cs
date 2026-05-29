using Microsoft.AspNetCore.Mvc;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.Negativacao.Queries;

namespace ApiInadimplencia.Api.Endpoints;

/// <summary>
/// Maps configuration endpoints.
/// </summary>
public static class ConfiguracoesEndpoints
{
    /// <summary>
    /// Adds configuration endpoints to the application.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapConfiguracoesEndpoints(this IEndpointRouteBuilder app)
    {
        MapConfiguracoesGroup(app.MapGroup("/configuracoes").WithTags("Configuracoes"), "Public");
        MapConfiguracoesGroup(app.MapGroup("/inadimplencia/configuracoes").WithTags("Configuracoes"), "Legacy");

        return app;
    }

    private static void MapConfiguracoesGroup(RouteGroupBuilder configuracoes, string routeSuffix)
    {

        // GET /configuracoes/senha-transacao
        configuracoes.MapGet("/senha-transacao", async (
            [FromHeader(Name = "X-Username")] string? username,
            [FromServices] ICurrentUserService currentUserService,
            [FromServices] IQueryHandler<GetHasSenhaTransacaoQuery, bool> handler,
            CancellationToken ct) =>
        {
            username = ResolveUsername(username, currentUserService);

            if (string.IsNullOrWhiteSpace(username))
            {
                return Results.Unauthorized();
            }

            var hasSenha = await handler.HandleAsync(new GetHasSenhaTransacaoQuery(username), ct);
            return Results.Ok(new { hasSenha });
        })
        .WithName($"GetHasSenhaTransacao{routeSuffix}")
        .WithOpenApi();

        // POST /configuracoes/senha-transacao
        configuracoes.MapPost("/senha-transacao", async (
            [FromBody] SetSenhaTransacaoRequest request,
            [FromHeader(Name = "X-Username")] string? username,
            [FromServices] ICurrentUserService currentUserService,
            [FromServices] ICommandHandler<SetSenhaTransacaoCommand, bool> handler,
            CancellationToken ct) =>
        {
            username = ResolveUsername(username, currentUserService);

            if (string.IsNullOrWhiteSpace(username))
            {
                return Results.Unauthorized();
            }

            try
            {
                var command = new SetSenhaTransacaoCommand(
                    username,
                    request.SenhaAtual,
                    request.NovaSenha);

                await handler.HandleAsync(command, ct);
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message, code = "SENHA_INVALIDA" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.BadRequest(new { error = ex.Message, code = "SENHA_ATUAL_INCORRETA" });
            }
        })
        .WithName($"SetSenhaTransacao{routeSuffix}")
        .WithOpenApi();
    }

    private static string? ResolveUsername(string? headerUsername, ICurrentUserService currentUserService)
    {
        if (!string.IsNullOrWhiteSpace(currentUserService.Username))
        {
            return currentUserService.Username;
        }

        return headerUsername;
    }
}

/// <summary>
/// Request DTO for setting transaction password.
/// </summary>
public sealed record SetSenhaTransacaoRequest
{
    /// <summary>
    /// Current password (required when updating).
    /// </summary>
    public string? SenhaAtual { get; init; }

    /// <summary>
    /// New password to set (minimum 6 characters).
    /// </summary>
    public string NovaSenha { get; init; } = string.Empty;
}

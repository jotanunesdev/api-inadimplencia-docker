using ApiInadimplencia.Infrastructure.Auth;
using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace ApiInadimplencia.Api.Endpoints;

/// <summary>
/// Maps the Jotaman desktop authentication endpoints.
/// These reuse the Microsoft Entra ID integration but enforce the dedicated
/// <c>jotaman:user</c> scope/role required to access the Jotaman automation tool.
/// </summary>
public static class JotamanSessionEndpoints
{
    /// <summary>Scope/role required to use the Jotaman tool.</summary>
    public const string RequiredScope = "jotaman:user";

    /// <summary>
    /// Adds /jotaman/session endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapJotamanSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var session = app.MapGroup("/jotaman/session").WithTags("Jotaman Session");

        session.MapGet("/entra/authorize-url", (
            [FromQuery] string? redirectUri,
            [FromQuery] string? state,
            [FromQuery] string? codeChallenge,
            [FromQuery] string? codeChallengeMethod,
            [FromQuery] string? prompt,
            [FromServices] IEntraIdAuthClient authClient) =>
        {
            try
            {
                return Results.Ok(authClient.BuildAuthorizationUrl(redirectUri, state, codeChallenge, codeChallengeMethod, prompt));
            }
            catch (AuthFailureException ex)
            {
                return Error(ex);
            }
        })
        .WithName("GetJotamanEntraAuthorizeUrl")
        .WithOpenApi();

        session.MapPost("/entra/token", async (
            [FromBody] EntraAuthorizationCodeRequest request,
            [FromServices] IEntraIdAuthClient authClient,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var login = await authClient.ExchangeAuthorizationCodeAsync(
                    request.Code ?? string.Empty,
                    request.RedirectUri ?? string.Empty,
                    request.CodeVerifier,
                    cancellationToken).ConfigureAwait(false);

                var scopes = login.User?.Scopes ?? [];
                AssertJotamanAccess(scopes);

                return Results.Ok(new
                {
                    authenticated = true,
                    accessToken = login.ResolvedToken,
                    tokenType = login.TokenType ?? "Bearer",
                    expiresIn = login.ExpiresIn,
                    refreshToken = login.RefreshToken,
                    scope = login.Scope,
                    scopes,
                    user = login.User,
                });
            }
            catch (AuthFailureException ex)
            {
                return Error(ex);
            }
        })
        .WithName("ExchangeJotamanEntraToken")
        .WithOpenApi();

        return app;
    }

    private static void AssertJotamanAccess(IReadOnlyList<string> scopes)
    {
        if (scopes.Any(scope => string.Equals(scope, RequiredScope, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new AuthFailureException(
            403,
            "Usuario sem permissao para acessar o Jotaman.",
            "JOTAMAN_SCOPE_FORBIDDEN");
    }

    private static IResult Error(AuthFailureException ex)
        => Results.Json(new { error = ex.Message, code = ex.Code }, statusCode: ex.StatusCode);
}

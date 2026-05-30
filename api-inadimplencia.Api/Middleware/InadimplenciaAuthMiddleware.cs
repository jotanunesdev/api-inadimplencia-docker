using ApiInadimplencia.Api.Endpoints;
using ApiInadimplencia.Infrastructure.Auth;
using ApiInadimplencia.Infrastructure.Configuration;

namespace ApiInadimplencia.Api.Middleware;

/// <summary>
/// Authenticates /inadimplencia data endpoints using Microsoft Entra ID Bearer tokens or the legacy session cookie.
/// </summary>
public sealed class InadimplenciaAuthMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/inadimplencia/health",
        "/inadimplencia/contracts",
    };

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>
    /// Executes the authentication middleware.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        AuthOptions options,
        IAuthServerClient authClient,
        IInadimplenciaSessionStore sessionStore)
    {
        if (!ShouldAuthenticate(context, options))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        AuthIdentity? identity;
        try
        {
            identity = await ResolveIdentityAsync(context, authClient, sessionStore).ConfigureAwait(false);
        }
        catch (AuthFailureException ex)
        {
            await WriteErrorAsync(context, ex.StatusCode, ex.Message, ex.Code).ConfigureAwait(false);
            return;
        }

        if (identity is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "Token de autenticacao ausente.", "AUTH_TOKEN_MISSING").ConfigureAwait(false);
            return;
        }

        if (!HasRequiredScope(context.Request.Method, identity.Scopes, options, out var requiredScope))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Usuario sem permissao para acessar este modulo.",
                code = "MODULE_SCOPE_FORBIDDEN",
                requiredScope,
            }).ConfigureAwait(false);
            return;
        }

        context.User = identity.ToClaimsPrincipal();
        context.Items["InadimplenciaAuth"] = identity;
        await _next(context).ConfigureAwait(false);
    }

    private static bool ShouldAuthenticate(HttpContext context, AuthOptions options)
    {
        if (!options.RequireAuthenticatedInadimplencia)
        {
            return false;
        }

        var path = context.Request.Path;
        if (!path.StartsWithSegments("/inadimplencia", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWithSegments("/inadimplencia/session", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Webhooks Serasa PEFIN sao callbacks publicos do parceiro e nao trafegam token.
        // Liberamos tanto /inadimplencia/serasa-pefin/webhooks/* quanto qualquer outro
        // prefixo que contenha esse subcaminho (defesa em profundidade).
        var pathValue = path.Value ?? string.Empty;
        if (pathValue.Contains("/serasa-pefin/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !PublicPaths.Contains(pathValue);
    }

    private static async Task<AuthIdentity?> ResolveIdentityAsync(
        HttpContext context,
        IAuthServerClient authClient,
        IInadimplenciaSessionStore sessionStore)
    {
        var bearerToken = ExtractBearerToken(context);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            return await authClient.IntrospectAsync(bearerToken, context.RequestAborted).ConfigureAwait(false);
        }

        var session = sessionStore.Get(InadimplenciaSessionEndpoints.ReadSessionId(context));
        return session?.Auth;
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers["Authorization"].ToString().Trim();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
    }

    private static bool HasRequiredScope(string method, IReadOnlyList<string> scopes, AuthOptions options, out string requiredScope)
    {
        var isRead = string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, HttpMethods.Head, StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, HttpMethods.Options, StringComparison.OrdinalIgnoreCase);
        requiredScope = isRead ? "inadimplencia:read" : "inadimplencia:write";

        return ContainsScope(scopes, requiredScope)
            || ContainsScope(scopes, "inadimplencia:admin")
            || ContainsScope(scopes, options.EntraId.Scope)
            || ContainsScope(scopes, options.EntraId.ScopeName);
    }

    private static bool ContainsScope(IReadOnlyList<string> scopes, string scope)
        => !string.IsNullOrWhiteSpace(scope)
            && scopes.Any(value => string.Equals(value, scope, StringComparison.OrdinalIgnoreCase));

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string error, string code)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new { error, code });
    }
}

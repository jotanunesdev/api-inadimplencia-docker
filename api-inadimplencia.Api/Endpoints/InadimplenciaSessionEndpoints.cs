using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Infrastructure.Auth;
using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace ApiInadimplencia.Api.Endpoints;

/// <summary>
/// Maps inadimplencia session endpoints compatible with the legacy module.
/// </summary>
public static class InadimplenciaSessionEndpoints
{
    /// <summary>Legacy inadimplencia session cookie name.</summary>
    public const string SessionCookieName = "jnc_inadimplencia_session";

    /// <summary>
    /// Adds /inadimplencia/session endpoints.
    /// </summary>
    public static RouteGroupBuilder MapInadimplenciaSessionEndpoints(this RouteGroupBuilder inadimplencia)
    {
        var session = inadimplencia.MapGroup("/session").WithTags("Inadimplencia Session");

        session.MapGet("/credential-key", (
            CredentialTransportCrypto credentialTransport,
            HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(credentialTransport.GetPublicKey());
        })
        .WithName("GetInadimplenciaCredentialKey")
        .WithOpenApi();

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
        .WithName("GetInadimplenciaEntraAuthorizeUrl")
        .WithOpenApi();

        session.MapPost("/entra/token", async (
            HttpContext context,
            [FromBody] EntraAuthorizationCodeRequest request,
            [FromServices] IEntraIdAuthClient authClient,
            [FromServices] IEntraTokenRepository entraTokenRepository,
            [FromServices] CredentialCrypto credentialCrypto,
            [FromServices] IInadimplenciaSessionStore sessionStore,
            [FromServices] AuthOptions options,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = ResolveOwner(context);
                var login = await authClient.ExchangeAuthorizationCodeAsync(
                    request.Code ?? string.Empty,
                    request.RedirectUri ?? string.Empty,
                    request.CodeVerifier,
                    cancellationToken).ConfigureAwait(false);
                var identity = await BuildIdentityAsync(authClient, login, cancellationToken).ConfigureAwait(false);
                AssertInadimplenciaAccess(identity, options);
                var createdSession = sessionStore.Create(login, identity);
                SetSessionCookie(context, createdSession, options);
                var credentialsRegistered = await TryPersistEntraTokenAsync(
                    owner,
                    identity,
                    login,
                    entraTokenRepository,
                    credentialCrypto,
                    cancellationToken).ConfigureAwait(false);

                return Results.Ok(BuildSessionResponse(credentialsRegistered, login, createdSession, includeToken: true));
            }
            catch (AuthFailureException ex)
            {
                ClearSessionCookie(context, options);
                return Error(ex);
            }
        })
        .WithName("ExchangeInadimplenciaEntraToken")
        .WithOpenApi();

        session.MapPost("/entra/login", async (
            HttpContext context,
            [FromBody] EntraPasswordLoginRequest request,
            [FromServices] IAuthServerClient authClient,
            [FromServices] IEntraTokenRepository entraTokenRepository,
            [FromServices] CredentialCrypto credentialCrypto,
            [FromServices] IInadimplenciaSessionStore sessionStore,
            [FromServices] AuthOptions options,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = ResolveOwner(context);
                var username = (request.Username ?? string.Empty).Trim();
                var password = request.Password ?? string.Empty;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                {
                    return Results.BadRequest(new
                    {
                        error = "Usuario Entra ID e senha sao obrigatorios.",
                        code = "ENTRA_CREDENTIALS_REQUIRED",
                    });
                }

                var login = await authClient.LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
                var identity = await BuildIdentityAsync(authClient, login, cancellationToken).ConfigureAwait(false);
                AssertInadimplenciaAccess(identity, options);
                var createdSession = sessionStore.Create(login, identity);
                SetSessionCookie(context, createdSession, options);
                var credentialsRegistered = await TryPersistEntraTokenAsync(
                    owner,
                    identity,
                    login,
                    entraTokenRepository,
                    credentialCrypto,
                    cancellationToken).ConfigureAwait(false);

                return Results.Ok(BuildSessionResponse(credentialsRegistered, login, createdSession, includeToken: true));
            }
            catch (AuthFailureException ex)
            {
                ClearSessionCookie(context, options);
                return Error(ex);
            }
        })
        .WithName("LoginInadimplenciaEntra")
        .WithOpenApi();

        session.MapGet("/status", async (
            HttpContext context,
            [FromServices] IAdCredentialRepository credentialRepository,
            [FromServices] IEntraTokenRepository entraTokenRepository,
            [FromServices] IInadimplenciaSessionStore sessionStore,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var currentSession = sessionStore.Get(ReadSessionId(context));
                var owner = ResolveOwner(context);
                if (currentSession is not null)
                {
                    var sessionOwnerCredential = string.IsNullOrWhiteSpace(owner.OwnerKey)
                        ? null
                        : await credentialRepository.FindByOwnerKeyAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);
                    var sessionOwnerEntraToken = string.IsNullOrWhiteSpace(owner.OwnerKey)
                        ? null
                        : await entraTokenRepository.FindByOwnerKeyAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);

                    return Results.Ok(new
                    {
                        authenticated = true,
                        credentialsRegistered = sessionOwnerEntraToken is not null || sessionOwnerCredential is not null,
                        user = currentSession.User,
                        scopes = currentSession.Auth.Scopes,
                        expiresAt = currentSession.ExpiresAt.ToString("O"),
                    });
                }

                RequireOwner(owner);
                var entraToken = await entraTokenRepository.FindByOwnerKeyAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);
                var credential = await credentialRepository.FindByOwnerKeyAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);

                return Results.Ok(new
                {
                    authenticated = false,
                    credentialsRegistered = entraToken is not null || credential is not null,
                    user = (object?)null,
                    scopes = Array.Empty<string>(),
                    expiresAt = (string?)null,
                });
            }
            catch (AuthFailureException ex)
            {
                return Error(ex);
            }
        })
        .WithName("GetInadimplenciaSessionStatus")
        .WithOpenApi();

        session.MapPost("/bootstrap", async (
            HttpContext context,
            [FromServices] IAdCredentialRepository credentialRepository,
            [FromServices] IEntraTokenRepository entraTokenRepository,
            [FromServices] CredentialCrypto credentialCrypto,
            [FromServices] IAuthServerClient authClient,
            [FromServices] IEntraIdAuthClient entraAuthClient,
            [FromServices] IInadimplenciaSessionStore sessionStore,
            [FromServices] AuthOptions options,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = ResolveOwner(context);
                RequireOwner(owner);
                var entraToken = await entraTokenRepository.FindByOwnerKeyAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);
                if (entraToken is not null)
                {
                    try
                    {
                        var refreshToken = credentialCrypto.Decrypt(new EncryptedSecret(
                            entraToken.RefreshTokenCipher,
                            entraToken.RefreshTokenIv,
                            entraToken.RefreshTokenTag));
                        var entraLogin = await entraAuthClient.RefreshTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                        var entraIdentity = await BuildIdentityAsync(entraAuthClient, entraLogin, cancellationToken).ConfigureAwait(false);
                        AssertInadimplenciaAccess(entraIdentity, options);
                        var entraSession = sessionStore.Create(entraLogin, entraIdentity);
                        SetSessionCookie(context, entraSession, options);
                        await TryPersistEntraTokenAsync(
                            owner,
                            entraIdentity,
                            entraLogin,
                            entraTokenRepository,
                            credentialCrypto,
                            cancellationToken).ConfigureAwait(false);
                        await entraTokenRepository.MarkLastLoginAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);

                        return Results.Ok(BuildSessionResponse(true, entraLogin, entraSession, includeToken: true));
                    }
                    catch (AuthFailureException ex) when (ex.StatusCode is 400 or 401)
                    {
                        await entraTokenRepository.DeleteAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);
                    }
                }

                var credential = await credentialRepository.FindByOwnerKeyAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);
                if (credential is null)
                {
                    ClearSessionCookie(context, options);
                    return Results.Ok(new
                    {
                        authenticated = false,
                        credentialsRegistered = false,
                        user = (object?)null,
                        scopes = Array.Empty<string>(),
                        expiresAt = (string?)null,
                        code = "CREDENTIALS_MISSING",
                    });
                }

                var password = credentialCrypto.Decrypt(new EncryptedSecret(
                    credential.PasswordCipher,
                    credential.PasswordIv,
                    credential.PasswordTag));
                var login = await authClient.LoginAsync(credential.AdUsername, password, cancellationToken).ConfigureAwait(false);
                var identity = await BuildIdentityAsync(authClient, login, cancellationToken).ConfigureAwait(false);
                AssertInadimplenciaAccess(identity, options);
                var createdSession = sessionStore.Create(login, identity);
                SetSessionCookie(context, createdSession, options);
                await credentialRepository.MarkLastLoginAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);

                return Results.Ok(BuildSessionResponse(true, login, createdSession, includeToken: true));
            }
            catch (AuthFailureException ex)
            {
                ClearSessionCookie(context, options);
                return Error(ex);
            }
        })
        .WithName("BootstrapInadimplenciaSession")
        .WithOpenApi();

        session.MapPost("/credentials", async (
            HttpContext context,
            [FromBody] SaveCredentialsRequest request,
            [FromServices] IAdCredentialRepository credentialRepository,
            [FromServices] CredentialCrypto credentialCrypto,
            [FromServices] CredentialTransportCrypto credentialTransport,
            [FromServices] IAuthServerClient authClient,
            [FromServices] IInadimplenciaSessionStore sessionStore,
            [FromServices] AuthOptions options,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = ResolveOwner(context);
                RequireOwner(owner);
                var adUsername = (request.Username ?? string.Empty).Trim();
                var password = ResolveCredentialPassword(request, credentialTransport);

                if (string.IsNullOrWhiteSpace(adUsername) || string.IsNullOrEmpty(password))
                {
                    return Results.BadRequest(new
                    {
                        error = "Usuario Entra ID e senha sao obrigatorios.",
                        code = "AD_CREDENTIALS_REQUIRED",
                    });
                }

                var login = await authClient.LoginAsync(adUsername, password, cancellationToken).ConfigureAwait(false);
                var identity = await BuildIdentityAsync(authClient, login, cancellationToken).ConfigureAwait(false);
                AssertInadimplenciaAccess(identity, options);
                var createdSession = sessionStore.Create(login, identity);
                SetSessionCookie(context, createdSession, options);

                await credentialRepository.UpsertAsync(
                    owner.OwnerKey,
                    owner.FluigUserName,
                    owner.FluigUserCode,
                    adUsername,
                    credentialCrypto.Encrypt(password),
                    cancellationToken).ConfigureAwait(false);
                await credentialRepository.MarkLastLoginAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);

                return Results.Ok(BuildSessionResponse(true, login, createdSession));
            }
            catch (AuthFailureException ex)
            {
                ClearSessionCookie(context, options);
                return Error(ex);
            }
        })
        .WithName("SaveInadimplenciaCredentials")
        .WithOpenApi();

        session.MapDelete("/", (
            HttpContext context,
            [FromServices] IInadimplenciaSessionStore sessionStore,
            [FromServices] AuthOptions options) =>
        {
            sessionStore.Remove(ReadSessionId(context));
            ClearSessionCookie(context, options);
            return Results.NoContent();
        })
        .WithName("LogoutInadimplenciaSession")
        .WithOpenApi();

        return inadimplencia;
    }

    private static async Task<AuthIdentity> BuildIdentityAsync(
        IAuthServerClient authClient,
        LoginResponse login,
        CancellationToken cancellationToken)
    {
        var identity = await authClient.IntrospectAsync(login.ResolvedToken, cancellationToken).ConfigureAwait(false);
        if (identity is not null)
        {
            return identity;
        }

        var user = login.User;
        if (user is null || string.IsNullOrWhiteSpace(user.Username))
        {
            throw new AuthFailureException(502, "Auth nao retornou usuario valido.", "AUTH_USER_MISSING");
        }

        var expiresAt = login.ExpiresIn is > 0 ? DateTimeOffset.UtcNow.AddSeconds(login.ExpiresIn.Value) : (DateTimeOffset?)null;
        return new AuthIdentity(user.Username, user.Email, user.Scopes, "ldap", expiresAt);
    }

    private static void AssertInadimplenciaAccess(AuthIdentity identity, AuthOptions options)
    {
        if (ContainsScope(identity.Scopes, "inadimplencia:read")
            || ContainsScope(identity.Scopes, "inadimplencia:admin")
            || ContainsScope(identity.Scopes, options.EntraId.Scope)
            || ContainsScope(identity.Scopes, options.EntraId.ScopeName))
        {
            return;
        }

        throw new AuthFailureException(403, "Usuario sem permissao para acessar dados de inadimplencia.", "INADIMPLENCIA_SCOPE_FORBIDDEN");
    }

    private static bool ContainsScope(IReadOnlyList<string> scopes, string scope)
        => !string.IsNullOrWhiteSpace(scope)
            && scopes.Any(value => string.Equals(value, scope, StringComparison.OrdinalIgnoreCase));

    private static object BuildSessionResponse(
        bool credentialsRegistered,
        LoginResponse login,
        CreatedInadimplenciaSession session,
        bool includeToken = false)
        => new
        {
            authenticated = true,
            credentialsRegistered,
            accessToken = includeToken ? login.ResolvedToken : null,
            tokenType = includeToken ? login.TokenType ?? "Bearer" : null,
            expiresIn = includeToken ? login.ExpiresIn : null,
            refreshToken = includeToken ? login.RefreshToken : null,
            scope = includeToken ? login.Scope : null,
            user = login.User,
            scopes = login.User?.Scopes ?? session.Session.Auth.Scopes,
            expiresAt = session.ExpiresAt.ToString("O"),
        };

    private static async Task<bool> TryPersistEntraTokenAsync(
        OwnerContext owner,
        AuthIdentity identity,
        LoginResponse login,
        IEntraTokenRepository entraTokenRepository,
        CredentialCrypto credentialCrypto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner.OwnerKey)
            || string.IsNullOrWhiteSpace(login.RefreshToken)
            || !entraTokenRepository.IsConfigured)
        {
            return false;
        }

        await entraTokenRepository.UpsertAsync(
            owner.OwnerKey,
            owner.FluigUserName,
            owner.FluigUserCode,
            identity.Subject,
            credentialCrypto.Encrypt(login.RefreshToken),
            cancellationToken).ConfigureAwait(false);

        await entraTokenRepository.MarkLastLoginAsync(owner.OwnerKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string ResolveCredentialPassword(SaveCredentialsRequest request, CredentialTransportCrypto credentialTransport)
    {
        if (request.PasswordEncrypted is not null)
        {
            return credentialTransport.Decrypt(request.PasswordEncrypted);
        }

        return request.Password ?? string.Empty;
    }

    private static OwnerContext ResolveOwner(HttpContext context)
    {
        var fluigUserCode = Header(context, "X-User-Code");
        var fluigUserName = Header(context, "X-User-Name");
        if (string.IsNullOrWhiteSpace(fluigUserName))
        {
            fluigUserName = Header(context, "X-Username");
        }

        var ownerKey = NormalizeOwnerKey(!string.IsNullOrWhiteSpace(fluigUserCode) ? fluigUserCode : fluigUserName);
        return new OwnerContext(ownerKey, fluigUserCode, fluigUserName);
    }

    private static string NormalizeOwnerKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static void RequireOwner(OwnerContext owner)
    {
        if (string.IsNullOrWhiteSpace(owner.OwnerKey))
        {
            throw new AuthFailureException(400, "Usuario Fluig nao identificado para credenciais.", "FLUIG_USER_MISSING");
        }
    }

    private static string Header(HttpContext context, string name)
        => context.Request.Headers.TryGetValue(name, out var values) ? values.ToString().Trim() : string.Empty;

    /// <summary>Reads the session id from the request cookie.</summary>
    public static string? ReadSessionId(HttpContext context)
        => context.Request.Cookies.TryGetValue(SessionCookieName, out var sessionId) ? sessionId : null;

    /// <summary>Sets the session cookie.</summary>
    public static void SetSessionCookie(HttpContext context, CreatedInadimplenciaSession session, AuthOptions options)
    {
        context.Response.Cookies.Append(SessionCookieName, session.SessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = ShouldUseSecureCookie(context, options),
            SameSite = ResolveSameSite(options),
            MaxAge = session.MaxAge,
            Path = "/inadimplencia",
        });
    }

    /// <summary>Clears the session cookie.</summary>
    public static void ClearSessionCookie(HttpContext context, AuthOptions options)
    {
        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = ShouldUseSecureCookie(context, options),
            SameSite = ResolveSameSite(options),
            Path = "/inadimplencia",
        });
    }

    private static bool ShouldUseSecureCookie(HttpContext context, AuthOptions options)
        => options.SessionCookieSecure
            ?? (context.Request.IsHttps
                || string.Equals(context.Request.Headers["X-Forwarded-Proto"].ToString(), "https", StringComparison.OrdinalIgnoreCase));

    private static SameSiteMode ResolveSameSite(AuthOptions options)
        => options.SessionCookieSameSite.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            _ => SameSiteMode.Lax,
        };

    private static IResult Error(AuthFailureException ex)
        => Results.Json(new { error = ex.Message, code = ex.Code }, statusCode: ex.StatusCode);

    private sealed record OwnerContext(string OwnerKey, string? FluigUserCode, string? FluigUserName);
}

/// <summary>
/// Request body for saving AD credentials.
/// </summary>
public sealed record SaveCredentialsRequest(
    string? Username,
    string? Password,
    CredentialTransportEncryptedSecret? PasswordEncrypted);

/// <summary>
/// Request body for exchanging an Entra ID authorization code.
/// </summary>
public sealed record EntraAuthorizationCodeRequest(
    string? Code,
    string? RedirectUri,
    string? CodeVerifier);

/// <summary>
/// Request body for direct Entra ID username/password authentication.
/// </summary>
public sealed record EntraPasswordLoginRequest(
    string? Username,
    string? Password);

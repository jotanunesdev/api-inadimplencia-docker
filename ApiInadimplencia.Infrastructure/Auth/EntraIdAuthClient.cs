using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// Microsoft Entra ID OAuth client and JWT validator.
/// </summary>
public sealed class EntraIdAuthClient(HttpClient httpClient, AuthOptions options) : IEntraIdAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly AuthOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly JwtSecurityTokenHandler _tokenHandler = new()
    {
        MapInboundClaims = false,
    };

    private ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;

    /// <inheritdoc />
    public EntraAuthorizationUrl BuildAuthorizationUrl(
        string? redirectUri,
        string? state,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? prompt)
    {
        EnsureOAuthConfigured();

        var resolvedRedirectUri = ResolveRedirectUri(redirectUri);
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = _options.EntraId.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = resolvedRedirectUri,
            ["response_mode"] = "query",
            ["scope"] = BuildOAuthScope(),
            ["state"] = BlankToNull(state),
            ["code_challenge"] = BlankToNull(codeChallenge),
            ["code_challenge_method"] = BlankToNull(codeChallengeMethod) ?? (string.IsNullOrWhiteSpace(codeChallenge) ? null : "S256"),
            ["prompt"] = BlankToNull(prompt),
        };

        return new EntraAuthorizationUrl(
            AppendQueryString(_options.EntraId.AuthorizationEndpoint, parameters),
            _options.EntraId.TenantId,
            _options.EntraId.ClientId,
            _options.EntraId.Scope,
            resolvedRedirectUri);
    }

    /// <inheritdoc />
    public async Task<LoginResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        EnsureOAuthConfigured();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AuthFailureException(400, "Codigo de autorizacao Entra ID e obrigatorio.", "ENTRA_AUTH_CODE_REQUIRED");
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.EntraId.ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code.Trim(),
            ["redirect_uri"] = ResolveRedirectUri(redirectUri),
            ["scope"] = BuildOAuthScope(),
        };

        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            form["code_verifier"] = codeVerifier.Trim();
        }

        AddClientSecret(form);
        return await RequestTokenAsync(form, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        EnsurePasswordGrantConfigured();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new AuthFailureException(400, "Usuario e senha Entra ID sao obrigatorios.", "ENTRA_CREDENTIALS_REQUIRED");
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.EntraId.ClientId,
            ["grant_type"] = "password",
            ["username"] = username.Trim(),
            ["password"] = password,
            ["scope"] = BuildOAuthScope(),
        };

        AddClientSecret(form);
        return await RequestTokenAsync(form, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        EnsurePasswordGrantConfigured();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthFailureException(400, "Refresh token Entra ID e obrigatorio.", "ENTRA_REFRESH_TOKEN_REQUIRED");
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.EntraId.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken.Trim(),
            ["scope"] = BuildOAuthScope(),
        };

        AddClientSecret(form);
        return await RequestTokenAsync(form, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AuthIdentity?> IntrospectAsync(string token, CancellationToken cancellationToken = default)
    {
        EnsureJwtValidationConfigured();

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var configuration = await ConfigurationManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return ValidateToken(token, configuration);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            ConfigurationManager.RequestRefresh();
            configuration = await ConfigurationManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            return ValidateToken(token, configuration);
        }
        catch (SecurityTokenExpiredException)
        {
            throw new AuthFailureException(401, "Token Entra ID expirado.", "ENTRA_TOKEN_EXPIRED");
        }
        catch (SecurityTokenException)
        {
            throw new AuthFailureException(401, "Token Entra ID invalido.", "ENTRA_TOKEN_INVALID");
        }
        catch (ArgumentException)
        {
            throw new AuthFailureException(401, "Token Entra ID invalido.", "ENTRA_TOKEN_INVALID");
        }
    }

    private async Task<LoginResponse> RequestTokenAsync(
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.EntraId.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await ReadJsonOrNullAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw BuildEntraFailure(response.StatusCode, payload);
        }

        var token = payload.Deserialize<EntraTokenResponse>(JsonOptions)
            ?? throw new AuthFailureException(502, "Entra ID nao retornou payload valido.", "ENTRA_INVALID_RESPONSE");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new AuthFailureException(502, "Entra ID nao retornou access token.", "ENTRA_TOKEN_MISSING");
        }

        var identity = await IntrospectAsync(token.AccessToken, cancellationToken).ConfigureAwait(false)
            ?? throw new AuthFailureException(401, "Token Entra ID invalido.", "ENTRA_TOKEN_INVALID");

        return new LoginResponse
        {
            AccessToken = token.AccessToken,
            TokenType = token.TokenType,
            ExpiresIn = token.ExpiresIn,
            RefreshToken = token.RefreshToken,
            Scope = token.Scope,
            User = new AuthUserDto(identity.Subject, null, identity.Email, null, null, null, [], identity.Scopes),
        };
    }

    private AuthIdentity ValidateToken(string token, OpenIdConnectConfiguration configuration)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = [_options.EntraId.Issuer, _options.EntraId.LegacyIssuer],
            ValidateAudience = true,
            ValidAudiences = [_options.EntraId.Audience, _options.EntraId.ApiClientId],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(5),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles",
        };

        var principal = _tokenHandler.ValidateToken(token.Trim(), parameters, out var validatedToken);
        if (validatedToken is not JwtSecurityToken jwt
            || !jwt.Header.Alg.StartsWith("RS", StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Token algorithm is invalid.");
        }

        var subject = ClaimValue(principal, "preferred_username", "upn", "unique_name", "email", "name", "oid", "sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null!;
        }

        var email = ClaimValue(principal, "email", "preferred_username", "upn");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(((DateTimeOffset)validatedToken.ValidTo).ToUnixTimeSeconds());
        return new AuthIdentity(subject, email, NormalizeScopes(principal), "entra-id", expiresAt);
    }

    private IReadOnlyList<string> NormalizeScopes(ClaimsPrincipal principal)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSplitClaims(scopes, principal.FindAll("scp"));
        AddSplitClaims(scopes, principal.FindAll("scope"));

        foreach (var role in principal.FindAll("roles").Concat(principal.FindAll(ClaimTypes.Role)))
        {
            if (!string.IsNullOrWhiteSpace(role.Value))
            {
                scopes.Add(role.Value.Trim());
            }
        }

        if (HasConfiguredApiAccess(scopes))
        {
            scopes.Add(_options.EntraId.Scope);
            scopes.Add(_options.EntraId.ScopeName);
            scopes.Add("inadimplencia:read");
            scopes.Add("inadimplencia:write");
        }

        return scopes.ToArray();
    }

    private bool HasConfiguredApiAccess(IReadOnlySet<string> scopes)
        => scopes.Contains(_options.EntraId.Scope)
            || scopes.Contains(_options.EntraId.ScopeName);

    private static void AddSplitClaims(ISet<string> scopes, IEnumerable<Claim> claims)
    {
        foreach (var claim in claims)
        {
            foreach (var scope in claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                scopes.Add(scope);
            }
        }
    }

    private string ResolveRedirectUri(string? redirectUri)
    {
        var resolved = string.IsNullOrWhiteSpace(redirectUri)
            ? _options.EntraId.RedirectUris.FirstOrDefault()
            : redirectUri.Trim();
        var normalized = _options.EntraId.NormalizeRedirectUri(resolved);

        if (string.IsNullOrWhiteSpace(normalized) || !_options.EntraId.IsAllowedRedirectUri(normalized))
        {
            throw new AuthFailureException(400, "Redirect URI Entra ID nao permitido.", "ENTRA_REDIRECT_URI_NOT_ALLOWED");
        }

        return normalized;
    }

    private string BuildOAuthScope()
        => $"openid profile offline_access {_options.EntraId.Scope}".Trim();

    private void AddClientSecret(IDictionary<string, string> form)
    {
        if (!string.IsNullOrWhiteSpace(_options.EntraId.ClientSecret))
        {
            form["client_secret"] = _options.EntraId.ClientSecret.Trim();
        }
    }

    private void EnsureOAuthConfigured()
    {
        EnsurePasswordGrantConfigured();

        if (string.IsNullOrWhiteSpace(_options.EntraId.ClientId) || _options.EntraId.RedirectUris.Count == 0)
        {
            throw new AuthFailureException(500, "Configuracao OAuth Entra ID incompleta.", "ENTRA_CONFIG_MISSING");
        }
    }

    private void EnsurePasswordGrantConfigured()
    {
        EnsureJwtValidationConfigured();

        if (string.IsNullOrWhiteSpace(_options.EntraId.ClientId))
        {
            throw new AuthFailureException(500, "Configuracao Entra ID incompleta.", "ENTRA_CONFIG_MISSING");
        }
    }

    private void EnsureJwtValidationConfigured()
    {
        if (!_options.EntraId.IsConfigured())
        {
            throw new AuthFailureException(500, "Configuracao Entra ID incompleta.", "ENTRA_CONFIG_MISSING");
        }
    }

    private ConfigurationManager<OpenIdConnectConfiguration> ConfigurationManager
        => _configurationManager ??= new ConfigurationManager<OpenIdConnectConfiguration>(
            _options.EntraId.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever
            {
                RequireHttps = true,
            });

    private static AuthFailureException BuildEntraFailure(HttpStatusCode statusCode, JsonElement? payload)
    {
        var message = GetString(payload, "error_description")
            ?? GetString(payload, "message")
            ?? GetString(payload, "error")
            ?? "Falha ao autenticar no Entra ID.";
        var code = GetString(payload, "error") ?? "ENTRA_REQUEST_FAILED";
        return new AuthFailureException((int)statusCode, message, code.ToUpperInvariant());
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AuthFailureException(502, "Falha ao comunicar com o Entra ID.", "ENTRA_UNAVAILABLE");
        }
    }

    private static string AppendQueryString(string endpoint, IReadOnlyDictionary<string, string?> parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

        return string.IsNullOrWhiteSpace(query) ? endpoint : $"{endpoint}?{query}";
    }

    private static string? ClaimValue(ClaimsPrincipal principal, params string[] types)
    {
        foreach (var type in types)
        {
            var value = principal.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetString(JsonElement? payload, string propertyName)
        => payload is { ValueKind: JsonValueKind.Object } element
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    private static async Task<T> ReadJsonOrNullAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions)!;
    }

    private sealed record EntraTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        public string? Scope { get; init; }
    }
}

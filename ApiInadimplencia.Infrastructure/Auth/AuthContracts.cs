using System.Security.Claims;
using System.Text.Json.Serialization;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// Login request sent to the external Auth API.
/// </summary>
public sealed record LoginRequest(string? Username, string? Password);

/// <summary>
/// User payload returned by the external Auth API.
/// </summary>
public sealed record AuthUserDto(
    string Username,
    string? Name,
    string? Email,
    string? Department,
    string? Title,
    string? Company,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Scopes);

/// <summary>
/// Token response returned by the external Auth API.
/// </summary>
public sealed record LoginResponse
{
    /// <summary>OAuth-like access token.</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    /// <summary>Compatibility token field.</summary>
    public string? Token { get; init; }

    /// <summary>Token type.</summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>Expiration in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }

    /// <summary>Refresh token returned by the Auth API.</summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>Space-separated scope string.</summary>
    public string? Scope { get; init; }

    /// <summary>Authenticated user.</summary>
    public AuthUserDto? User { get; init; }

    /// <summary>Resolves the access token while honoring the legacy token field.</summary>
    public string ResolvedToken => !string.IsNullOrWhiteSpace(AccessToken) ? AccessToken! : Token ?? string.Empty;
}

/// <summary>
/// Introspection response returned by the external Auth API.
/// </summary>
public sealed record IntrospectionResponse
{
    /// <summary>Whether the token is active.</summary>
    public bool Active { get; init; }

    /// <summary>Subject/login.</summary>
    public string? Sub { get; init; }

    /// <summary>Preferred username.</summary>
    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; init; }

    /// <summary>Email claim.</summary>
    public string? Email { get; init; }

    /// <summary>Space-separated scopes.</summary>
    public string? Scope { get; init; }

    /// <summary>Array scopes.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>Auth source.</summary>
    public string? Source { get; init; }

    /// <summary>Issuer.</summary>
    public string? Iss { get; init; }

    /// <summary>Audience.</summary>
    public object? Aud { get; init; }

    /// <summary>Expiration unix timestamp.</summary>
    public long? Exp { get; init; }

    /// <summary>JWT id.</summary>
    public string? Jti { get; init; }
}

/// <summary>
/// Normalized authenticated identity used inside the inadimplencia API.
/// </summary>
public sealed record AuthIdentity(
    string Subject,
    string? Email,
    IReadOnlyList<string> Scopes,
    string? Source,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>Converts the identity to a claims principal.</summary>
    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Subject),
            new(ClaimTypes.Name, Subject),
            new("sub", Subject),
        };

        if (!string.IsNullOrWhiteSpace(Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, Email));
        }

        foreach (var scope in Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "AuthServer", ClaimTypes.Name, "scope"));
    }
}

/// <summary>
/// Authentication client contract used by the inadimplencia API.
/// </summary>
public interface IAuthServerClient
{
    /// <summary>Authenticates AD credentials when the configured provider supports direct credentials.</summary>
    Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Validates an access token and returns the normalized identity.</summary>
    Task<AuthIdentity?> IntrospectAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Microsoft Entra ID authentication client contract.
/// </summary>
public interface IEntraIdAuthClient : IAuthServerClient
{
    /// <summary>Builds the Microsoft Entra ID authorization URL for Authorization Code + PKCE.</summary>
    EntraAuthorizationUrl BuildAuthorizationUrl(
        string? redirectUri,
        string? state,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? prompt);

    /// <summary>Exchanges an authorization code for an Entra ID access token.</summary>
    Task<LoginResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default);

    /// <summary>Renews an Entra ID access token using a stored refresh token.</summary>
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Authorization URL payload returned to clients.
/// </summary>
public sealed record EntraAuthorizationUrl(
    string AuthorizationUrl,
    string TenantId,
    string ClientId,
    string Scope,
    string RedirectUri);

using Microsoft.Extensions.Configuration;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// Authentication integration and inadimplencia session configuration.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth";

    /// <summary>External Auth API base URL.</summary>
    public string AuthServerBaseUrl { get; init; } = "http://localhost:3013";

    /// <summary>Microsoft Entra ID configuration used for OAuth and token validation.</summary>
    public EntraIdOptions EntraId { get; init; } = new();

    /// <summary>Origin sent to the Auth API when logging in with AD credentials.</summary>
    public string InternalOrigin { get; init; } = "https://fluig.jotanunes.com";

    /// <summary>HTTP timeout in seconds for calls to the Auth API.</summary>
    public int AuthServerTimeoutSeconds { get; init; } = 15;

    /// <summary>Credential AES-GCM encryption key or passphrase.</summary>
    public string CredentialsEncryptionKey { get; init; } = string.Empty;

    /// <summary>Optional RSA private key PEM used for transport decryption.</summary>
    public string? CredentialsTransportPrivateKey { get; init; }

    /// <summary>Optional RSA public key PEM exposed to clients.</summary>
    public string? CredentialsTransportPublicKey { get; init; }

    /// <summary>Default session TTL in milliseconds when token expiration is unavailable.</summary>
    public long SessionTtlMs { get; init; } = 8 * 60 * 60 * 1000;

    /// <summary>Optional secure cookie override. Null auto-detects HTTPS.</summary>
    public bool? SessionCookieSecure { get; init; }

    /// <summary>Cookie SameSite mode: lax, strict, or none.</summary>
    public string SessionCookieSameSite { get; init; } = "lax";

    /// <summary>Requires Bearer token or inadimplencia session cookie on data endpoints.</summary>
    public bool RequireAuthenticatedInadimplencia { get; init; } = true;

    /// <summary>
    /// Builds options using ASP.NET configuration plus legacy environment fallback keys.
    /// </summary>
    public static AuthOptions FromConfiguration(IConfiguration configuration, string? environmentName)
    {
        var requireAuthConfigured = Value(configuration, "Auth:RequireAuthenticatedInadimplencia", "INAD_REQUIRE_AUTHENTICATED");
        var isTesting = string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        var requireAuth = isTesting
            ? false
            : ParseBoolean(requireAuthConfigured, true);

        return new AuthOptions
        {
            AuthServerBaseUrl = TrimTrailingSlash(Value(configuration, "Auth:AuthServerBaseUrl", "INAD_AUTH_URL", "AUTH_SERVER_BASE_URL") ?? "http://localhost:3013"),
            EntraId = EntraIdOptions.FromConfiguration(configuration),
            InternalOrigin = FirstCsv(Value(configuration, "Auth:InternalOrigin", "INAD_AUTH_INTERNAL_ORIGIN", "AUTH_CORS_ORIGIN", "CORS_ORIGIN") ?? "https://fluig.jotanunes.com"),
            AuthServerTimeoutSeconds = (int)ParsePositiveLong(Value(configuration, "Auth:AuthServerTimeoutSeconds", "INAD_AUTH_TIMEOUT_SECONDS"), 15),
            CredentialsEncryptionKey = Value(configuration, "Auth:CredentialsEncryptionKey", "INAD_CREDENTIALS_ENCRYPTION_KEY", "AUTH_JWT_SECRET", "JWT_SECRET", "INAD_AUTH_JWT_SECRET") ?? string.Empty,
            CredentialsTransportPrivateKey = NormalizePem(Value(configuration, "Auth:CredentialsTransportPrivateKey", "INAD_CREDENTIALS_TRANSPORT_PRIVATE_KEY")),
            CredentialsTransportPublicKey = NormalizePem(Value(configuration, "Auth:CredentialsTransportPublicKey", "INAD_CREDENTIALS_TRANSPORT_PUBLIC_KEY")),
            SessionTtlMs = ParsePositiveLong(Value(configuration, "Auth:SessionTtlMs", "INAD_SESSION_TTL_MS"), 8 * 60 * 60 * 1000),
            SessionCookieSecure = ParseNullableBoolean(Value(configuration, "Auth:SessionCookieSecure", "INAD_SESSION_COOKIE_SECURE")),
            SessionCookieSameSite = Value(configuration, "Auth:SessionCookieSameSite", "INAD_SESSION_COOKIE_SAMESITE") ?? "lax",
            RequireAuthenticatedInadimplencia = requireAuth,
        };
    }

    private static string? Value(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Trim('"');
            }
        }

        return null;
    }

    private static bool ParseBoolean(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "sim" or "yes" or "on" => true,
            "false" or "0" or "nao" or "no" or "off" => false,
            _ => fallback,
        };
    }

    private static bool? ParseNullableBoolean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseBoolean(value, false);

    private static long ParsePositiveLong(string? value, long fallback)
        => long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static string? NormalizePem(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Replace("\\n", "\n", StringComparison.Ordinal);

    private static string TrimTrailingSlash(string value)
        => value.Trim().TrimEnd('/');

    private static string FirstCsv(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
            ?? "https://fluig.jotanunes.com";
}

/// <summary>
/// Microsoft Entra ID OAuth configuration for the Fluig client and inadimplencia API resource.
/// </summary>
public sealed class EntraIdOptions
{
    /// <summary>Tenant/directory id.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>API application/client id.</summary>
    public string ApiClientId { get; init; } = string.Empty;

    /// <summary>Expected JWT audience.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Fluig client application id.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Optional Fluig client secret used by confidential client OAuth flows.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>Delegated API scope requested by the client.</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>Allowed OAuth redirect URIs.</summary>
    public IReadOnlyList<string> RedirectUris { get; init; } = [];

    /// <summary>Authorization endpoint.</summary>
    public string AuthorizationEndpoint => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize";

    /// <summary>Token endpoint.</summary>
    public string TokenEndpoint => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";

    /// <summary>OpenID Connect metadata endpoint.</summary>
    public string MetadataAddress => $"https://login.microsoftonline.com/{TenantId}/v2.0/.well-known/openid-configuration";

    /// <summary>Issuer expected for v2 access tokens.</summary>
    public string Issuer => $"https://login.microsoftonline.com/{TenantId}/v2.0";

    /// <summary>Legacy issuer used by v1 access tokens.</summary>
    public string LegacyIssuer => $"https://sts.windows.net/{TenantId}/";

    /// <summary>Scope claim value used by Entra for delegated permissions.</summary>
    public string ScopeName
    {
        get
        {
            var normalized = Scope.Trim();
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < normalized.Length - 1
                ? normalized[(lastSlash + 1)..]
                : normalized;
        }
    }

    /// <summary>Builds Entra options using ASP.NET configuration plus environment fallback keys.</summary>
    public static EntraIdOptions FromConfiguration(IConfiguration configuration)
        => new()
        {
            TenantId = Value(configuration, "Auth:EntraId:TenantId", "INAD_ENTRA_TENANT_ID", "AZURE_TENANT_ID") ?? string.Empty,
            ApiClientId = Value(configuration, "Auth:EntraId:ApiClientId", "INAD_ENTRA_API_CLIENT_ID", "AZURE_API_CLIENT_ID") ?? string.Empty,
            Audience = Value(configuration, "Auth:EntraId:Audience", "INAD_ENTRA_AUDIENCE", "AZURE_AD_AUDIENCE") ?? string.Empty,
            ClientId = Value(configuration, "Auth:EntraId:ClientId", "INAD_ENTRA_CLIENT_ID", "AZURE_CLIENT_ID") ?? string.Empty,
            ClientSecret = Value(configuration, "Auth:EntraId:ClientSecret", "INAD_ENTRA_CLIENT_SECRET", "AZURE_CLIENT_SECRET"),
            Scope = Value(configuration, "Auth:EntraId:Scope", "INAD_ENTRA_SCOPE", "AZURE_API_SCOPE") ?? string.Empty,
            RedirectUris = CsvValues(Value(configuration, "Auth:EntraId:RedirectUris", "INAD_ENTRA_REDIRECT_URIS", "AZURE_REDIRECT_URIS")),
        };

    /// <summary>Returns true when all required Entra values are configured.</summary>
    public bool IsConfigured()
        => !string.IsNullOrWhiteSpace(TenantId)
            && !string.IsNullOrWhiteSpace(ApiClientId)
            && !string.IsNullOrWhiteSpace(Audience)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(Scope);

    /// <summary>Checks if a redirect URI is configured as allowed.</summary>
    public bool IsAllowedRedirectUri(string redirectUri)
    {
        var normalizedRedirectUri = NormalizeRedirectUri(redirectUri);
        return !string.IsNullOrWhiteSpace(normalizedRedirectUri)
            && RedirectUris.Any(allowed => string.Equals(
                NormalizeRedirectUri(allowed),
                normalizedRedirectUri,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Normalizes redirect URIs for exact allow-list matching and OAuth requests.</summary>
    public string? NormalizeRedirectUri(string? redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return null;
        }

        var trimmed = redirectUri.Trim().Trim('"');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed.TrimEnd('/');
        }

        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? uri.GetLeftPart(UriPartial.Authority) : normalized;
    }

    private static string? Value(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Trim('"');
            }
        }

        return null;
    }

    private static IReadOnlyList<string> CsvValues(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

using System.Net;
using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Integrations.Fluig;

/// <summary>
/// Owns the Fluig session cookie. Authenticates against
/// <c>/portal/j_security_check</c> (form-urlencoded POST, manual redirect)
/// and caches the resulting cookie for up to <see cref="SessionTtl"/>.
/// Mirrors the legacy Node.js <c>fluigDataset.js</c> behavior.
/// </summary>
public sealed class FluigSessionManager
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<FluigOptions> _options;
    private readonly ILogger<FluigSessionManager> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedCookie;
    private DateTimeOffset _cachedAt;

    public FluigSessionManager(
        IHttpClientFactory httpClientFactory,
        IOptions<FluigOptions> options,
        ILogger<FluigSessionManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>HTTP client name used to issue the authentication request.</summary>
    public const string AuthHttpClientName = "Fluig.Auth";

    /// <summary>Returns a valid Fluig cookie, refreshing it when expired.</summary>
    public async Task<string> GetCookieAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedCookie(out var cookie))
        {
            return cookie;
        }

        return await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Forces a fresh authentication. Used when the API returns 401/403.</summary>
    public async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check inside the lock to avoid duplicate auth calls under load.
            if (TryGetCachedCookie(out var cookie))
            {
                return cookie;
            }

            var fresh = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            _cachedCookie = fresh;
            _cachedAt = DateTimeOffset.UtcNow;
            return fresh;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Invalidates the cached cookie so the next call re-authenticates.</summary>
    public void Invalidate()
    {
        _cachedCookie = null;
        _cachedAt = default;
    }

    private bool TryGetCachedCookie(out string cookie)
    {
        var snapshot = _cachedCookie;
        if (!string.IsNullOrEmpty(snapshot) && DateTimeOffset.UtcNow - _cachedAt < SessionTtl)
        {
            cookie = snapshot;
            return true;
        }

        cookie = string.Empty;
        return false;
    }

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        if (string.IsNullOrWhiteSpace(opts.Url))
        {
            throw new InvalidOperationException("Fluig:Url não configurado.");
        }
        if (string.IsNullOrWhiteSpace(opts.User) || string.IsNullOrWhiteSpace(opts.Password))
        {
            throw new InvalidOperationException("Fluig:User/Password não configurados.");
        }

        var baseUrl = opts.Url.TrimEnd('/');
        var loginUrl = $"{baseUrl}/portal/j_security_check";

        using var client = _httpClientFactory.CreateClient(AuthHttpClientName);
        using var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("j_username", opts.User),
            new KeyValuePair<string, string>("j_password", opts.Password),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, loginUrl) { Content = body };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        // Fluig replies with 302 on success when the credentials are valid; 200 may
        // also occur depending on the redirect target. Both are acceptable here.
        if (response.StatusCode != HttpStatusCode.Redirect
            && response.StatusCode != HttpStatusCode.Found
            && !response.IsSuccessStatusCode)
        {
            var detail = await SafeReadAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Falha ao autenticar no Fluig (status {(int)response.StatusCode}): {detail}");
        }

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            throw new InvalidOperationException("Cookie de sessão do Fluig não retornado.");
        }

        var cookieHeader = string.Join("; ", setCookieValues.Select(ExtractCookiePair).Where(s => !string.IsNullOrEmpty(s)));
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            throw new InvalidOperationException("Cookie de sessão do Fluig vazio.");
        }

        _logger.LogDebug("Fluig session authenticated (cookie segments: {Count})", cookieHeader.Split(';').Length);
        return cookieHeader;
    }

    private static string ExtractCookiePair(string setCookie)
    {
        // "JSESSIONID=abc; Path=/; HttpOnly" -> "JSESSIONID=abc"
        var sep = setCookie.IndexOf(';');
        return sep >= 0 ? setCookie[..sep].Trim() : setCookie.Trim();
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }
}

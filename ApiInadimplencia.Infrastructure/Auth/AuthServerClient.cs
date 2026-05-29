using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiInadimplencia.Infrastructure.Configuration;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// HTTP client for the external Auth API hosted at api-autenticacao-node.
/// </summary>
public sealed class AuthServerClient(HttpClient httpClient, AuthOptions options) : IAuthServerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly AuthOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(username, password), options: JsonOptions),
        };
        request.Headers.TryAddWithoutValidation("Origin", _options.InternalOrigin);

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await ReadJsonOrNullAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw BuildAuthFailure(response.StatusCode, payload);
        }

        var login = payload.Deserialize<LoginResponse>(JsonOptions)
            ?? throw new AuthFailureException(502, "Auth nao retornou payload valido.", "AUTH_INVALID_RESPONSE");

        if (string.IsNullOrWhiteSpace(login.ResolvedToken))
        {
            throw new AuthFailureException(502, "Auth nao retornou token.", "AUTH_TOKEN_MISSING");
        }

        return login;
    }

    /// <inheritdoc />
    public async Task<AuthIdentity?> IntrospectAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/introspect")
        {
            Content = JsonContent.Create(new { token }, options: JsonOptions),
        };
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await ReadJsonOrNullAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw BuildAuthFailure(response.StatusCode, payload);
        }

        var introspection = payload.Deserialize<IntrospectionResponse>(JsonOptions);
        if (introspection?.Active != true)
        {
            return null;
        }

        var subject = introspection.PreferredUsername ?? introspection.Sub;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var scopes = NormalizeScopes(introspection);
        DateTimeOffset? expiresAt = introspection.Exp is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(introspection.Exp.Value)
            : null;

        return new AuthIdentity(subject, introspection.Email, scopes, introspection.Source, expiresAt);
    }

    private static IReadOnlyList<string> NormalizeScopes(IntrospectionResponse introspection)
    {
        if (introspection.Scopes is { Count: > 0 })
        {
            return introspection.Scopes
                .Where(static scope => !string.IsNullOrWhiteSpace(scope))
                .Select(static scope => scope.Trim())
                .ToArray();
        }

        return string.IsNullOrWhiteSpace(introspection.Scope)
            ? []
            : introspection.Scope.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static AuthFailureException BuildAuthFailure(HttpStatusCode statusCode, JsonElement? payload)
    {
        var message = GetString(payload, "message") ?? GetString(payload, "error") ?? "Falha ao autenticar no Auth.";
        var code = GetString(payload, "code") ?? "AUTH_REQUEST_FAILED";
        return new AuthFailureException((int)statusCode, message, code);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AuthFailureException(502, "Falha ao comunicar com a API Auth.", "AUTH_SERVER_UNAVAILABLE");
        }
    }

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
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using ApiInadimplencia.Infrastructure.Configuration;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// In-memory inadimplencia session store compatible with the legacy module.
/// </summary>
public interface IInadimplenciaSessionStore
{
    /// <summary>Creates a session for a login result.</summary>
    CreatedInadimplenciaSession Create(LoginResponse loginResult, AuthIdentity identity);

    /// <summary>Gets an active session by id.</summary>
    InadimplenciaSession? Get(string? sessionId);

    /// <summary>Removes a session by id.</summary>
    void Remove(string? sessionId);
}

/// <summary>
/// Stored inadimplencia session payload.
/// </summary>
public sealed record InadimplenciaSession(
    string Token,
    AuthIdentity Auth,
    AuthUserDto User,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Created session details returned to the endpoint layer.
/// </summary>
public sealed record CreatedInadimplenciaSession(
    string SessionId,
    DateTimeOffset ExpiresAt,
    TimeSpan MaxAge,
    InadimplenciaSession Session);

/// <summary>
/// Default in-memory session store.
/// </summary>
public sealed class InMemoryInadimplenciaSessionStore(AuthOptions options) : IInadimplenciaSessionStore
{
    private readonly ConcurrentDictionary<string, InadimplenciaSession> _sessions = new(StringComparer.Ordinal);
    private readonly AuthOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public CreatedInadimplenciaSession Create(LoginResponse loginResult, AuthIdentity identity)
    {
        var sessionId = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var ttl = ResolveTtl(now, identity.ExpiresAt, loginResult.ExpiresIn);
        var expiresAt = now.Add(ttl);
        var token = loginResult.ResolvedToken;
        var user = loginResult.User ?? new AuthUserDto(identity.Subject, null, identity.Email, null, null, null, [], identity.Scopes);
        var session = new InadimplenciaSession(token, identity, user, expiresAt);
        _sessions[sessionId] = session;
        return new CreatedInadimplenciaSession(sessionId, expiresAt, ttl, session);
    }

    /// <inheritdoc />
    public InadimplenciaSession? Get(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        if (!_sessions.TryGetValue(sessionId.Trim(), out var session))
        {
            return null;
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(sessionId.Trim(), out _);
            return null;
        }

        return session;
    }

    /// <inheritdoc />
    public void Remove(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessions.TryRemove(sessionId.Trim(), out _);
        }
    }

    private TimeSpan ResolveTtl(DateTimeOffset now, DateTimeOffset? tokenExpiresAt, int? expiresIn)
    {
        if (tokenExpiresAt is not null)
        {
            var ttl = tokenExpiresAt.Value - now;
            return ttl < TimeSpan.Zero ? TimeSpan.Zero : ttl;
        }

        if (expiresIn is > 0)
        {
            return TimeSpan.FromSeconds(expiresIn.Value);
        }

        return TimeSpan.FromMilliseconds(_options.SessionTtlMs);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

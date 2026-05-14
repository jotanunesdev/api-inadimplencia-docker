namespace ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;

/// <summary>
/// In-memory cache for Serasa PEFIN bearer tokens with configurable buffer
/// </summary>
public class SerasaPefinTokenCache
{
    private string? _token;
    private DateTime _expiresAt;
    private readonly int _bufferSeconds;

    /// <summary>
    /// Initializes a new instance with 60 second buffer
    /// </summary>
    public SerasaPefinTokenCache() : this(bufferSeconds: 60)
    {
    }

    /// <summary>
    /// Initializes a new instance with custom buffer
    /// </summary>
    /// <param name="bufferSeconds">Buffer in seconds before actual expiration</param>
    public SerasaPefinTokenCache(int bufferSeconds)
    {
        _bufferSeconds = bufferSeconds;
        _expiresAt = DateTime.MinValue;
    }

    /// <summary>
    /// Gets the cached token if not expired, null otherwise
    /// </summary>
    public string? GetToken()
    {
        if (IsExpired())
        {
            return null;
        }

        return _token;
    }

    /// <summary>
    /// Sets a new token with expiration time
    /// </summary>
    /// <param name="token">Bearer token</param>
    /// <param name="expiresIn">Time until token expires</param>
    public void SetToken(string token, TimeSpan expiresIn)
    {
        _token = token;
        _expiresAt = DateTime.UtcNow.Add(expiresIn);
    }

    /// <summary>
    /// Checks if the cached token is expired (including buffer)
    /// </summary>
    public bool IsExpired()
    {
        if (_token == null)
        {
            return true;
        }

        var bufferExpiry = _expiresAt.AddSeconds(-_bufferSeconds);
        return DateTime.UtcNow >= bufferExpiry;
    }

    /// <summary>
    /// Clears the cached token
    /// </summary>
    public void Clear()
    {
        _token = null;
        _expiresAt = DateTime.MinValue;
    }
}

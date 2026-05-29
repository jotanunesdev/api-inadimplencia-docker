namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// Exception used to return the legacy auth error contract.
/// </summary>
public sealed class AuthFailureException : Exception
{
    /// <summary>
    /// Creates an auth failure exception.
    /// </summary>
    public AuthFailureException(int statusCode, string message, string code)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Machine-readable error code.</summary>
    public string Code { get; }
}

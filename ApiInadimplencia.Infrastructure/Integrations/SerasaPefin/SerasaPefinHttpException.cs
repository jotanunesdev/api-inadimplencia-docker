namespace ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;

/// <summary>
/// Exception thrown when Serasa PEFIN HTTP request fails with details for troubleshooting.
/// </summary>
public sealed class SerasaPefinHttpException : Exception
{
    /// <summary>HTTP status code returned by Serasa.</summary>
    public int StatusCode { get; }

    /// <summary>Response body from Serasa (preserved for troubleshooting).</summary>
    public string Body { get; }

    public SerasaPefinHttpException(int statusCode, string body, string message) : base(message)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public SerasaPefinHttpException(int statusCode, string body, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
        Body = body;
    }
}

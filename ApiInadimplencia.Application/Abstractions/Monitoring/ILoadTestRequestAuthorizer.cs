namespace ApiInadimplencia.Application.Abstractions.Monitoring;

/// <summary>
/// Authorizes short-lived credentials used exclusively by managed load-test processes.
/// </summary>
public interface ILoadTestRequestAuthorizer
{
    /// <summary>
    /// Returns whether the supplied ephemeral credential belongs to an active load-test run.
    /// </summary>
    bool IsAuthorized(string? credential);
}

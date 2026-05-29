namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Result of transaction password validation.
/// </summary>
public enum SenhaTransacaoValidationResult
{
    /// <summary>
    /// Password is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// Password is invalid.
    /// </summary>
    Invalid,

    /// <summary>
    /// Account is locked out due to too many failed attempts.
    /// </summary>
    LockedOut,

    /// <summary>
    /// No transaction password is set for the user.
    /// </summary>
    NotSet
}

/// <summary>
/// Interface for validating transaction passwords with lockout policy.
/// </summary>
public interface ISenhaTransacaoValidator
{
    /// <summary>
    /// Validates a transaction password for a user, applying lockout policy.
    /// </summary>
    Task<SenhaTransacaoValidationResult> ValidateAsync(
        string username,
        string senha,
        CancellationToken ct = default);
}

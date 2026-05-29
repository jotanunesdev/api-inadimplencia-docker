namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Interface for transaction password hashing and verification.
/// </summary>
public interface ISenhaTransacaoHasher
{
    /// <summary>
    /// Hashes a plain text password using PBKDF2.
    /// </summary>
    string Hash(string plain);

    /// <summary>
    /// Verifies a plain text password against a hash.
    /// </summary>
    bool Verify(string hash, string plain);
}

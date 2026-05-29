namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Encrypted AD credentials stored per Fluig user.
/// </summary>
public sealed record StoredAdCredential(
    int Id,
    string OwnerKey,
    string? FluigUserName,
    string? FluigUserCode,
    string AdUsername,
    string PasswordCipher,
    string PasswordIv,
    string PasswordTag,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);

/// <summary>
/// AES-GCM encrypted secret payload.
/// </summary>
public sealed record EncryptedSecret(string CipherText, string Iv, string Tag);

/// <summary>
/// Stores encrypted AD credentials used by the inadimplencia session bootstrap flow.
/// </summary>
public interface IAdCredentialRepository
{
    /// <summary>
    /// Indicates whether credential persistence is configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Finds the credential registered for a normalized owner key.
    /// </summary>
    Task<StoredAdCredential?> FindByOwnerKeyAsync(string ownerKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the credential for the owner key.
    /// </summary>
    Task<StoredAdCredential> UpsertAsync(
        string ownerKey,
        string? fluigUserName,
        string? fluigUserCode,
        string adUsername,
        EncryptedSecret encryptedPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last login timestamp for the owner key.
    /// </summary>
    Task MarkLastLoginAsync(string ownerKey, CancellationToken cancellationToken = default);
}

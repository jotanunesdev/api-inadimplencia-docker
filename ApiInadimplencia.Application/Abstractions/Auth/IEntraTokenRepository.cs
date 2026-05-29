namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Encrypted Microsoft Entra ID refresh token stored per Fluig user.
/// </summary>
public sealed record StoredEntraToken(
    int Id,
    string OwnerKey,
    string? FluigUserName,
    string? FluigUserCode,
    string Subject,
    string RefreshTokenCipher,
    string RefreshTokenIv,
    string RefreshTokenTag,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);

/// <summary>
/// Stores encrypted Microsoft Entra ID refresh tokens used by automatic session bootstrap.
/// </summary>
public interface IEntraTokenRepository
{
    /// <summary>
    /// Indicates whether token persistence is configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Finds the token registered for a normalized owner key.
    /// </summary>
    Task<StoredEntraToken?> FindByOwnerKeyAsync(string ownerKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the token for the owner key.
    /// </summary>
    Task<StoredEntraToken> UpsertAsync(
        string ownerKey,
        string? fluigUserName,
        string? fluigUserCode,
        string subject,
        EncryptedSecret encryptedRefreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last login timestamp for the owner key.
    /// </summary>
    Task MarkLastLoginAsync(string ownerKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the token registered for the owner key.
    /// </summary>
    Task DeleteAsync(string ownerKey, CancellationToken cancellationToken = default);
}

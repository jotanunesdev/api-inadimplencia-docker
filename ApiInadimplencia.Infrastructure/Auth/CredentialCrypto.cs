using System.Security.Cryptography;
using System.Text;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Infrastructure.Configuration;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// AES-GCM encryption for stored AD credentials.
/// </summary>
public sealed class CredentialCrypto(AuthOptions options)
{
    private const int IvLength = 12;
    private readonly AuthOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Encrypts a plaintext credential.</summary>
    public EncryptedSecret Encrypt(string value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var cipherText = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(BuildKey(), 16);
        aes.Encrypt(iv, plaintext, cipherText, tag);

        return new EncryptedSecret(
            Convert.ToBase64String(cipherText),
            Convert.ToBase64String(iv),
            Convert.ToBase64String(tag));
    }

    /// <summary>Decrypts a stored credential payload.</summary>
    public string Decrypt(EncryptedSecret payload)
    {
        var cipherText = Convert.FromBase64String(payload.CipherText);
        var iv = Convert.FromBase64String(payload.Iv);
        var tag = Convert.FromBase64String(payload.Tag);
        var plaintext = new byte[cipherText.Length];

        using var aes = new AesGcm(BuildKey(), 16);
        aes.Decrypt(iv, cipherText, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] BuildKey()
    {
        var secret = _options.CredentialsEncryptionKey.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new AuthFailureException(500, "Chave de criptografia das credenciais nao configurada.", "CREDENTIALS_ENCRYPTION_NOT_CONFIGURED");
        }

        if (secret.Length == 64 && secret.All(Uri.IsHexDigit))
        {
            return Convert.FromHexString(secret);
        }

        try
        {
            var base64 = Convert.FromBase64String(secret);
            if (base64.Length == 32)
            {
                return base64;
            }
        }
        catch (FormatException)
        {
            // Fall through to SHA-256 derivation.
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}

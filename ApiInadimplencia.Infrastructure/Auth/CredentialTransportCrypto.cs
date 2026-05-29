using System.Security.Cryptography;
using System.Text;
using ApiInadimplencia.Infrastructure.Configuration;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// RSA-OAEP transport encryption for credentials sent by the frontend.
/// </summary>
public sealed class CredentialTransportCrypto
{
    private readonly Lazy<KeyPair> _keyPair;

    /// <summary>
    /// Creates the transport crypto service.
    /// </summary>
    public CredentialTransportCrypto(AuthOptions options)
    {
        _keyPair = new Lazy<KeyPair>(() => LoadOrCreateKeyPair(options ?? throw new ArgumentNullException(nameof(options))));
    }

    /// <summary>Returns the public key contract used by /inadimplencia/session/credential-key.</summary>
    public CredentialTransportPublicKey GetPublicKey()
    {
        var pair = _keyPair.Value;
        return new CredentialTransportPublicKey(pair.KeyId, "RSA-OAEP-256", pair.PublicKeyPem);
    }

    /// <summary>Decrypts a frontend encrypted credential.</summary>
    public string Decrypt(CredentialTransportEncryptedSecret payload)
    {
        var pair = _keyPair.Value;
        if (string.IsNullOrWhiteSpace(payload.KeyId) || string.IsNullOrWhiteSpace(payload.CipherText))
        {
            throw new AuthFailureException(400, "Credencial criptografada invalida.", "INVALID_ENCRYPTED_CREDENTIAL");
        }

        if (!string.Equals(payload.KeyId, pair.KeyId, StringComparison.Ordinal))
        {
            throw new AuthFailureException(400, "Chave de credencial expirada. Reabra o cadastro e tente novamente.", "CREDENTIAL_TRANSPORT_KEY_EXPIRED");
        }

        try
        {
            var decrypted = pair.Rsa.Decrypt(Convert.FromBase64String(payload.CipherText), RSAEncryptionPadding.OaepSHA256);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new AuthFailureException(400, "Nao foi possivel descriptografar a credencial.", "CREDENTIAL_DECRYPTION_FAILED");
        }
    }

    private static KeyPair LoadOrCreateKeyPair(AuthOptions options)
    {
        var rsa = RSA.Create(2048);
        if (!string.IsNullOrWhiteSpace(options.CredentialsTransportPrivateKey))
        {
            rsa.ImportFromPem(options.CredentialsTransportPrivateKey);
        }

        var publicKeyPem = !string.IsNullOrWhiteSpace(options.CredentialsTransportPublicKey)
            ? NormalizePublicKey(options.CredentialsTransportPublicKey)
            : ExportPublicKeyPem(rsa);
        var keyId = Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem)));
        return new KeyPair(rsa, publicKeyPem, keyId);
    }

    private static string NormalizePublicKey(string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return ExportPublicKeyPem(rsa);
    }

    private static string ExportPublicKeyPem(RSA rsa)
    {
        var base64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record KeyPair(RSA Rsa, string PublicKeyPem, string KeyId);
}

/// <summary>
/// Public key response contract.
/// </summary>
public sealed record CredentialTransportPublicKey(string KeyId, string Algorithm, string PublicKey);

/// <summary>
/// Encrypted credential request payload.
/// </summary>
public sealed record CredentialTransportEncryptedSecret(string? KeyId, string? CipherText);

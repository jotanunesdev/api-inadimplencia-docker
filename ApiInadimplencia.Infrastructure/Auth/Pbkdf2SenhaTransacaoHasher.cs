using System.Buffers.Binary;
using System.Security.Cryptography;
using ApiInadimplencia.Application.Abstractions.Auth;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// PBKDF2-HMAC-SHA256 implementation of transaction password hasher.
/// Produces output binary-compatible with ASP.NET Core Identity V3 format
/// (prefix byte 0x01, PRF=HMACSHA256, 100000 iterations, 128-bit salt, 256-bit subkey),
/// encoded as Base64. Implemented with BCL primitives only to avoid external NuGet
/// dependencies in offline builds.
/// </summary>
public sealed class Pbkdf2SenhaTransacaoHasher : ISenhaTransacaoHasher
{
    private const byte FormatMarker = 0x01;
    private const int Pbkdf2PrfHmacSha256 = 1;
    private const int IterationCount = 100_000;
    private const int SaltSize = 128 / 8;
    private const int SubkeySize = 256 / 8;

    /// <inheritdoc />
    public string Hash(string plain)
    {
        if (string.IsNullOrWhiteSpace(plain))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(plain));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            plain,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            SubkeySize);

        var output = new byte[13 + salt.Length + subkey.Length];
        output[0] = FormatMarker;
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(1, 4), (uint)Pbkdf2PrfHmacSha256);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(5, 4), (uint)IterationCount);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(9, 4), (uint)salt.Length);
        Buffer.BlockCopy(salt, 0, output, 13, salt.Length);
        Buffer.BlockCopy(subkey, 0, output, 13 + salt.Length, subkey.Length);

        return Convert.ToBase64String(output);
    }

    /// <inheritdoc />
    public bool Verify(string hash, string plain)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Hash cannot be empty.", nameof(hash));
        }

        if (string.IsNullOrWhiteSpace(plain))
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length < 13 || decoded[0] != FormatMarker)
        {
            return false;
        }

        var prf = (int)BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(1, 4));
        var iterations = (int)BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(5, 4));
        var saltLength = (int)BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(9, 4));

        if (prf != Pbkdf2PrfHmacSha256 || iterations <= 0 || saltLength <= 0)
        {
            return false;
        }

        if (decoded.Length <= 13 + saltLength)
        {
            return false;
        }

        var salt = new byte[saltLength];
        Buffer.BlockCopy(decoded, 13, salt, 0, saltLength);

        var subkeyLength = decoded.Length - 13 - saltLength;
        var expectedSubkey = new byte[subkeyLength];
        Buffer.BlockCopy(decoded, 13 + saltLength, expectedSubkey, 0, subkeyLength);

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            plain,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            subkeyLength);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}

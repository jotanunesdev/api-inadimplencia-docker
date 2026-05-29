namespace ApiInadimplencia.Domain.Negativacao;

/// <summary>
/// Represents a user's transaction password with lockout policy for security.
/// </summary>
public class UsuarioSenhaTransacao
{
    /// <summary>
    /// Gets the username (primary key).
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the PBKDF2 hash of the transaction password.
    /// </summary>
    public string Hash { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the number of failed authentication attempts.
    /// </summary>
    public int TentativasFalhas { get; private set; }

    /// <summary>
    /// Gets the UTC datetime until which the account is locked out.
    /// </summary>
    public DateTime? BloqueadoAte { get; private set; }

    /// <summary>
    /// Gets the UTC datetime when the password was created.
    /// </summary>
    public DateTime CriadaEm { get; private set; }

    /// <summary>
    /// Gets the UTC datetime when the password was last updated.
    /// </summary>
    public DateTime AtualizadaEm { get; private set; }

    /// <summary>
    /// Creates a new transaction password record.
    /// </summary>
    public static UsuarioSenhaTransacao Criar(string username, string hash)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Hash is required.", nameof(hash));
        }

        return new UsuarioSenhaTransacao
        {
            Username = username.Trim(),
            Hash = hash,
            TentativasFalhas = 0,
            BloqueadoAte = null,
            CriadaEm = DateTime.UtcNow,
            AtualizadaEm = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reconstructs a transaction password record from persistence (for repository use only).
    /// </summary>
    public static UsuarioSenhaTransacao Reconstruct(
        string username,
        string hash,
        int tentativasFalhas,
        DateTime? bloqueadoAte,
        DateTime criadaEm,
        DateTime atualizadaEm)
    {
        return new UsuarioSenhaTransacao
        {
            Username = username,
            Hash = hash,
            TentativasFalhas = tentativasFalhas,
            BloqueadoAte = bloqueadoAte,
            CriadaEm = criadaEm,
            AtualizadaEm = atualizadaEm
        };
    }

    /// <summary>
    /// Updates the password hash and resets failure counters.
    /// </summary>
    public void AtualizarHash(string novoHash)
    {
        if (string.IsNullOrWhiteSpace(novoHash))
        {
            throw new ArgumentException("Hash is required.", nameof(novoHash));
        }

        Hash = novoHash;
        TentativasFalhas = 0;
        BloqueadoAte = null;
        AtualizadaEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Registers a failed authentication attempt and applies lockout if threshold reached.
    /// </summary>
    public void RegistrarTentativaInvalida(int maxTentativas, TimeSpan janelaLockout, DateTime utcNow)
    {
        TentativasFalhas++;

        // Reset counter if outside the lockout window
        if (BloqueadoAte.HasValue && utcNow > BloqueadoAte.Value)
        {
            TentativasFalhas = 1;
            BloqueadoAte = null;
        }

        // Apply lockout if threshold reached within the window
        if (TentativasFalhas >= maxTentativas)
        {
            BloqueadoAte = utcNow.Add(janelaLockout);
        }

        AtualizadaEm = utcNow;
    }

    /// <summary>
    /// Registers a successful authentication attempt, resetting failure counters.
    /// </summary>
    public void RegistrarTentativaValida()
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
        AtualizadaEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the account is currently locked out.
    /// </summary>
    public bool EstaBloqueado(DateTime utcNow)
    {
        return BloqueadoAte.HasValue && utcNow < BloqueadoAte.Value;
    }
}

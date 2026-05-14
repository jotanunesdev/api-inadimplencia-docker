namespace ApiInadimplencia.Domain.Users;

/// <summary>
/// Represents a user in the inadimplencia system.
/// </summary>
public class Usuario
{
    /// <summary>
    /// Gets the user code (unique identifier).
    /// </summary>
    public string UserCode { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user name.
    /// </summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user profile.
    /// </summary>
    public UserProfile Perfil { get; private set; }

    /// <summary>
    /// Gets the hex color for UI representation.
    /// </summary>
    public string CorHex { get; private set; } = string.Empty;

    /// <summary>
    /// Creates a new user with validations.
    /// </summary>
    /// <param name="userCode">User code.</param>
    /// <param name="nome">User name.</param>
    /// <param name="perfil">User profile (admin or operador).</param>
    /// <param name="corHex">Hex color in #RRGGBB format (with or without #).</param>
    /// <returns>A validated user instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static Usuario Criar(string userCode, string nome, UserProfile perfil, string corHex)
    {
        if (string.IsNullOrWhiteSpace(userCode))
        {
            throw new ArgumentException("User code is required.", nameof(userCode));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Name is required.", nameof(nome));
        }

        if (perfil is not UserProfile.Admin and not UserProfile.Operador)
        {
            throw new ArgumentException("Profile must be admin or operador.", nameof(perfil));
        }

        var normalizedColor = NormalizeCorHex(corHex);

        return new Usuario
        {
            UserCode = userCode.Trim(),
            Nome = nome.Trim(),
            Perfil = perfil,
            CorHex = normalizedColor
        };
    }

    /// <summary>
    /// Updates the user information.
    /// </summary>
    /// <param name="nome">New name (optional).</param>
    /// <param name="perfil">New profile (optional).</param>
    /// <param name="corHex">New hex color (optional).</param>
    public void Atualizar(string? nome = null, UserProfile? perfil = null, string? corHex = null)
    {
        if (nome != null)
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                throw new ArgumentException("Name cannot be empty.", nameof(nome));
            }
            Nome = nome.Trim();
        }

        if (perfil.HasValue)
        {
            if (perfil.Value is not UserProfile.Admin and not UserProfile.Operador)
            {
                throw new ArgumentException("Profile must be admin or operador.", nameof(perfil));
            }
            Perfil = perfil.Value;
        }

        if (corHex != null)
        {
            CorHex = NormalizeCorHex(corHex);
        }
    }

    /// <summary>
    /// Normalizes hex color to #RRGGBB format.
    /// </summary>
    /// <param name="corHex">Raw color value.</param>
    /// <returns>Normalized color in #RRGGBB format.</returns>
    /// <exception cref="ArgumentException">Thrown when format is invalid.</exception>
    private static string NormalizeCorHex(string corHex)
    {
        if (string.IsNullOrWhiteSpace(corHex))
        {
            throw new ArgumentException("Color is required.", nameof(corHex));
        }

        var normalized = corHex.Trim();
        if (!normalized.StartsWith('#'))
        {
            normalized = $"#{normalized}";
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, "^#[0-9a-fA-F]{6}$"))
        {
            throw new ArgumentException("Color must be in #RRGGBB format.", nameof(corHex));
        }

        return normalized.ToUpperInvariant();
    }
}

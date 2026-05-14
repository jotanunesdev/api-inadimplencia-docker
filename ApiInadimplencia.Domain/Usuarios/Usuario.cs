using ApiInadimplencia.Domain.Common;
using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Domain.Usuarios;

/// <summary>
/// Represents a user in the inadimplencia module.
/// </summary>
public class Usuario
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Gets the user code.
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
    /// Gets the hex color for the user.
    /// </summary>
    public HexColor CorHex { get; private set; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public static Usuario Criar(
        string userCode,
        string nome,
        UserProfile perfil,
        HexColor corHex)
    {
        return new Usuario
        {
            UserCode = userCode,
            Nome = nome,
            Perfil = perfil,
            CorHex = corHex,
            CriadoEm = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the user.
    /// </summary>
    public void Atualizar(
        string? nome = null,
        UserProfile? perfil = null,
        HexColor? corHex = null)
    {
        if (nome != null) Nome = nome;
        if (perfil.HasValue) Perfil = perfil.Value;
        if (corHex.HasValue) CorHex = corHex.Value;
    }
}

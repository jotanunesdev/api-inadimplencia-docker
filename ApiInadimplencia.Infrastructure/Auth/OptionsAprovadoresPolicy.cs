using ApiInadimplencia.Application.Abstractions.Auth;
// IMPORTANT: this must reference the NegativacaoOptions defined in
// ApiInadimplencia.Application.Configuration, because that's the type bound to
// the "Negativacao" config section in DependencyInjection. The duplicate type
// in ApiInadimplencia.Infrastructure.Configuration is NOT bound, so injecting
// it would yield default values (empty UsuariosAprovadores) and silently break
// notification dispatch to approvers.
using ApiInadimplencia.Application.Configuration;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// Adapter que implementa IAprovadoresPolicy usando IOptions<NegativacaoOptions>.
/// Registrado como Singleton no DI.
/// </summary>
public sealed class OptionsAprovadoresPolicy : IAprovadoresPolicy
{
    private readonly NegativacaoOptions _options;
    private readonly IReadOnlyList<string> _aprovadores;
    private readonly IReadOnlyList<string> _superDecisores;

    public OptionsAprovadoresPolicy(IOptions<NegativacaoOptions> options)
    {
        _options = options.Value;
        _aprovadores = _options.UsuariosAprovadores
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList()
            .AsReadOnly();
        _superDecisores = (_options.SuperDecisores ?? Array.Empty<string>())
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList()
            .AsReadOnly();
    }

    public bool IsAprovador(string? username)
    {
        var normalized = NormalizeUsername(username);
        if (normalized is null)
        {
            return false;
        }

        return _aprovadores.Any(allowed => MatchesUsername(allowed, normalized));
    }

    public IReadOnlyList<string> ListAprovadores()
    {
        return _aprovadores;
    }

    public bool IsSuperDecisor(string? username)
    {
        var normalized = NormalizeUsername(username);
        if (normalized is null)
        {
            return false;
        }

        return _superDecisores.Any(allowed => MatchesUsername(allowed, normalized));
    }

    /// <summary>
    /// Compara o username configurado com o recebido. Aceita:
    ///   - igualdade case-insensitive
    ///   - igualdade pelo local-part (parte antes do '@'), para tolerar
    ///     casos em que o provedor de identidade retorne o email completo
    ///     (ex.: "gustavo.trindade@jotanunes.com.br") enquanto a config
    ///     lista apenas "gustavo.trindade".
    /// </summary>
    private static bool MatchesUsername(string allowed, string received)
    {
        if (string.Equals(allowed, received, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var allowedLocal = ExtractLocalPart(allowed);
        var receivedLocal = ExtractLocalPart(received);
        if (string.Equals(allowedLocal, receivedLocal, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Forma canonica: ignora separadores comuns (espaco, ponto, hifen, underscore).
        // Cobre cenarios onde o provedor retorna display name "Gustavo Trindade"
        // mas a config lista "gustavo.trindade".
        return string.Equals(
            CanonicalizeUsername(allowedLocal),
            CanonicalizeUsername(receivedLocal),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return username.Trim();
    }

    private static string ExtractLocalPart(string username)
    {
        var atIndex = username.IndexOf('@');
        return atIndex > 0 ? username[..atIndex] : username;
    }

    private static string CanonicalizeUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[username.Length];
        var length = 0;
        foreach (var ch in username)
        {
            if (ch is ' ' or '.' or '-' or '_')
            {
                continue;
            }
            buffer[length++] = ch;
        }

        return new string(buffer[..length]);
    }
}

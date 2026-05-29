namespace ApiInadimplencia.Application.Features.Dashboard.Parsers;

/// <summary>
/// Parser and whitelist for faixa (aging range) parameter.
/// </summary>
public static class FaixaParser
{
    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "0-30",
        "31-90",
        "91-180",
        "180+",
        "31-60",
        "61-90",
        "91-120",
        "121-180",
        "181+",
        "0-30-dias",
        "31-60-dias",
        "61-90-dias",
        "91-120-dias",
        "121-180-dias",
        "181+-dias"
    };

    /// <summary>
    /// Validates and parses the faixa parameter.
    /// </summary>
    /// <param name="faixa">The faixa value to validate.</param>
    /// <returns>The validated faixa value.</returns>
    /// <exception cref="ArgumentException">Thrown when faixa is not in the whitelist.</exception>
    public static string Parse(string? faixa)
    {
        if (string.IsNullOrWhiteSpace(faixa))
        {
            return "all";
        }

        if (!AllowedValues.Contains(faixa))
        {
            throw new ArgumentException(
                $"Invalid faixa value '{faixa}'. Allowed values are: {string.Join(", ", AllowedValues)}",
                nameof(faixa));
        }

        return faixa;
    }

    /// <summary>
    /// Gets the allowed faixa values.
    /// </summary>
    public static IReadOnlySet<string> Allowed => AllowedValues;
}

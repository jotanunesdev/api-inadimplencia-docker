namespace ApiInadimplencia.Application.Features.Dashboard.Parsers;

/// <summary>
/// Parser and whitelist for qtd (quantity) parameter.
/// </summary>
public static class QuantidadeParser
{
    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "10",
        "1-5",
        "6-10",
        "10+",
        "all"
    };

    /// <summary>
    /// Validates and parses the qtd parameter.
    /// </summary>
    /// <param name="qtd">The qtd value to validate.</param>
    /// <returns>The validated qtd value.</returns>
    /// <exception cref="ArgumentException">Thrown when qtd is not in the whitelist.</exception>
    public static string Parse(string? qtd)
    {
        if (string.IsNullOrWhiteSpace(qtd))
        {
            return "all";
        }

        if (!AllowedValues.Contains(qtd))
        {
            throw new ArgumentException(
                $"Invalid qtd value '{qtd}'. Allowed values are: {string.Join(", ", AllowedValues)}",
                nameof(qtd));
        }

        return qtd;
    }

    /// <summary>
    /// Gets the allowed qtd values.
    /// </summary>
    public static IReadOnlySet<string> Allowed => AllowedValues;
}

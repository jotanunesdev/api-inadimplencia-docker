namespace ApiInadimplencia.Application.Features.Dashboard.Parsers;

/// <summary>
/// Parser and whitelist for score parameter.
/// </summary>
public static class ScoreParser
{
    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "A",
        "B",
        "C",
        "D",
        "E",
        "F",
        "G",
        "H",
        "A-B",
        "C-D",
        "E-F",
        "G-H",
        "alto",
        "medio",
        "baixo",
        "all"
    };

    /// <summary>
    /// Validates and parses the score parameter.
    /// </summary>
    /// <param name="score">The score value to validate.</param>
    /// <returns>The validated score value.</returns>
    /// <exception cref="ArgumentException">Thrown when score is not in the whitelist.</exception>
    public static string Parse(string? score)
    {
        if (string.IsNullOrWhiteSpace(score))
        {
            return "all";
        }

        if (!AllowedValues.Contains(score))
        {
            throw new ArgumentException(
                $"Invalid score value '{score}'. Allowed values are: {string.Join(", ", AllowedValues)}",
                nameof(score));
        }

        return score;
    }

    /// <summary>
    /// Gets the allowed score values.
    /// </summary>
    public static IReadOnlySet<string> Allowed => AllowedValues;
}

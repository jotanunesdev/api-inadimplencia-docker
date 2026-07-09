using System.Globalization;
using System.Text;

namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Compares usernames that may arrive as login, e-mail local part, or display-name-like text.
/// </summary>
public static class UsernameMatcher
{
    /// <summary>
    /// Returns true when two username representations identify the same user.
    /// </summary>
    public static bool Matches(string? left, string? right)
    {
        var normalizedLeft = NormalizeForComparison(left);
        var normalizedRight = NormalizeForComparison(right);

        return normalizedLeft is not null
            && normalizedRight is not null
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeForComparison(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var localPart = ExtractLocalPart(username.Trim());
        var decomposed = localPart.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '.' or '-' or '_')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.Length == 0
            ? null
            : builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ExtractLocalPart(string username)
    {
        var atIndex = username.IndexOf('@');
        return atIndex > 0 ? username[..atIndex] : username;
    }
}

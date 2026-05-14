using System.Text.RegularExpressions;

namespace ApiInadimplencia.Domain.Common;

/// <summary>
/// Identifies a sale in the inadimplencia domain.
/// </summary>
/// <param name="Value">Positive sale number.</param>
public readonly record struct NumVenda(int Value)
{
    /// <summary>
    /// Creates a validated sale number.
    /// </summary>
    /// <param name="value">Raw integer value.</param>
    /// <returns>A validated sale number.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not positive.</exception>
    public static NumVenda Create(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "NUM_VENDA must be positive.");
        }

        return new NumVenda(value);
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Stores a normalized CPF/CNPJ document using digits only.
/// </summary>
/// <param name="Value">Digits-only document value.</param>
public readonly record struct CpfCnpj(string Value)
{
    /// <summary>
    /// Creates a normalized CPF/CNPJ.
    /// </summary>
    /// <param name="value">Raw document value.</param>
    /// <returns>A normalized document value object.</returns>
    public static CpfCnpj Create(string value)
    {
        var digits = Regex.Replace(value ?? string.Empty, "\\D", string.Empty);
        if (digits.Length is not (11 or 14))
        {
            throw new ArgumentException("CPF/CNPJ must contain 11 or 14 digits.", nameof(value));
        }

        return new CpfCnpj(digits);
    }
}

/// <summary>
/// Represents a UI color stored as a normalized hex string.
/// </summary>
/// <param name="Value">Hex color in #RRGGBB format.</param>
public readonly record struct HexColor(string Value)
{
    /// <summary>
    /// Creates a normalized hex color.
    /// </summary>
    /// <param name="value">Raw color value.</param>
    /// <returns>A normalized hex color.</returns>
    public static HexColor Create(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (!normalized.StartsWith('#'))
        {
            normalized = $"#{normalized}";
        }

        if (!Regex.IsMatch(normalized, "^#[0-9a-fA-F]{6}$"))
        {
            throw new ArgumentException("Hex color must be in #RRGGBB format.", nameof(value));
        }

        return new HexColor(normalized.ToUpperInvariant());
    }
}

/// <summary>
/// Represents an atendimento protocol generated as AAAAMMDD#####.
/// </summary>
/// <param name="Value">Protocol text.</param>
public readonly record struct ProtocolNumber(string Value)
{
    /// <summary>
    /// Creates a validated protocol.
    /// </summary>
    /// <param name="value">Raw protocol text.</param>
    /// <returns>A protocol value object.</returns>
    public static ProtocolNumber Create(string value)
    {
        var normalized = value ?? string.Empty;
        if (!Regex.IsMatch(normalized, "^\\d{13}$"))
        {
            throw new ArgumentException("Protocol must use AAAAMMDD##### format.", nameof(value));
        }

        return new ProtocolNumber(normalized);
    }
}

using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// Fluig integration configuration used by the inadimplencia module.
/// </summary>
public sealed class FluigOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Fluig";

    /// <summary>
    /// Fluig base URL.
    /// </summary>
    [Required]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Fluig username.
    /// </summary>
    [Required]
    public string User { get; init; } = string.Empty;

    /// <summary>
    /// Fluig password.
    /// </summary>
    [Required]
    public string Password { get; init; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// TOTVS RM integration configuration used by the inadimplencia module.
/// </summary>
public sealed class RmOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Rm";

    /// <summary>
    /// RM coligada ID.
    /// </summary>
    [Required]
    public int Coligada { get; init; }

    /// <summary>
    /// RM report coligada ID.
    /// </summary>
    [Required]
    public int ReportColigada { get; init; }

    /// <summary>
    /// RM parameter coligada ID.
    /// </summary>
    [Required]
    public int ParamColigada { get; init; }

    /// <summary>
    /// RM report ID.
    /// </summary>
    [Required]
    public int ReportId { get; init; }

    /// <summary>
    /// Debug mode flag.
    /// </summary>
    public bool Debug { get; init; }
}

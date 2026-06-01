using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Application.Configuration;

/// <summary>
/// TOTVS RM integration configuration used by the inadimplencia module.
/// Bound from the <c>Rm</c> configuration section.
/// </summary>
public sealed class RmOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Rm";

    /// <summary>RM coligada ID (legacy compatibility — usually equal to ParamColigada).</summary>
    [Required]
    public int Coligada { get; init; }

    /// <summary>Coligada used to locate the report (PARAMETERS lookup).</summary>
    [Required]
    public int ReportColigada { get; init; }

    /// <summary>Coligada injected as PARAMETER value in the XML.</summary>
    [Required]
    public int ParamColigada { get; init; }

    /// <summary>RM report ID (PK in TOTVS report registry).</summary>
    [Required]
    public int ReportId { get; init; }

    /// <summary>
    /// RM report code (used to resolve report metadata via fallback dataset
    /// <c>ds_paiFilho_controleDeAcessoRMreportsFluig</c>). Defaults to ReportId stringified.
    /// </summary>
    public string? ReportCode { get; init; }

    /// <summary>
    /// RM report human-readable name (used as a secondary matcher when ReportCode
    /// is not found in the metadata dataset).
    /// </summary>
    public string ReportName { get; init; } = "Ficha Financeira";

    /// <summary>Debug flag — emits extra structured logs (parameter snapshots).</summary>
    public bool Debug { get; init; }
}

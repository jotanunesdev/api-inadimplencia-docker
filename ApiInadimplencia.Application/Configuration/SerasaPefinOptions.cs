using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Application.Configuration;

/// <summary>
/// Serasa PEFIN integration configuration used by the inadimplencia module.
/// </summary>
public sealed class SerasaPefinOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SerasaPefin";

    /// <summary>
    /// Environment (uat or prod).
    /// </summary>
    [Required]
    public string Env { get; init; } = "uat";

    /// <summary>
    /// Serasa authentication URL.
    /// </summary>
    [Required]
    public string AuthUrl { get; init; } = string.Empty;

    /// <summary>
    /// Serasa collection base URL.
    /// </summary>
    [Required]
    public string CollectionBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Serasa client ID.
    /// </summary>
    [Required]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Serasa client secret.
    /// </summary>
    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Serasa logon vinculado.
    /// </summary>
    [Required]
    public string LogonVinculado { get; init; } = string.Empty;

    /// <summary>
    /// Serasa CNPJ do contrato.
    /// </summary>
    [Required]
    public string CnpjContrato { get; init; } = string.Empty;

    /// <summary>
    /// Use UAT defaults flag.
    /// </summary>
    public bool UseUatDefaults { get; init; }

    /// <summary>
    /// Creditor document.
    /// </summary>
    public string? CreditorDocument { get; init; }

    /// <summary>
    /// Area informante.
    /// </summary>
    public string? AreaInformante { get; init; }

    /// <summary>
    /// HTTP timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Negativacao endpoint path.
    /// </summary>
    public string NegativacaoEndpoint { get; init; } = "/api/negativacao";
}

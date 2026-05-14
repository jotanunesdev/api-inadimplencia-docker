namespace ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

/// <summary>
/// Represents a defaulted sale (inadimplência) from the data warehouse.
/// </summary>
public sealed record InadimplenciaDto
{
    /// <summary>
    /// Customer name.
    /// </summary>
    public string? CLIENTE { get; init; }

    /// <summary>
    /// Customer CPF/CNPJ.
    /// </summary>
    public string? CPF_CNPJ { get; init; }

    /// <summary>
    /// Sale number (NUM_VENDA).
    /// </summary>
    public int NUM_VENDA { get; init; }

    /// <summary>
    /// Real estate development name.
    /// </summary>
    public string? EMPREENDIMENTO { get; init; }

    /// <summary>
    /// Block number.
    /// </summary>
    public string? BLOCO { get; init; }

    /// <summary>
    /// Unit number.
    /// </summary>
    public string? UNIDADE { get; init; }

    /// <summary>
    /// Number of defaulted installments.
    /// </summary>
    public int? QTD_PARCELAS_INADIMPLENTES { get; init; }

    /// <summary>
    /// Transfer status.
    /// </summary>
    public string? STATUS_REPASSE { get; init; }

    /// <summary>
    /// Score.
    /// </summary>
    public string? SCORE { get; init; }

    /// <summary>
    /// Suggestion.
    /// </summary>
    public string? SUGESTAO { get; init; }

    /// <summary>
    /// Oldest due date.
    /// </summary>
    public DateTime? VENCIMENTO_MAIS_ANTIGO { get; init; }

    /// <summary>
    /// Total open amount.
    /// </summary>
    public decimal? VALOR_TOTAL_EM_ABERTO { get; init; }

    /// <summary>
    /// Defaulted amount.
    /// </summary>
    public decimal? VALOR_INADIMPLENTE { get; init; }

    /// <summary>
    /// Non-contractual defaulted amount.
    /// </summary>
    public decimal? VALOR_NAO_CONTRATUAL_INAD { get; init; }

    /// <summary>
    /// Savings defaulted amount.
    /// </summary>
    public decimal? VALOR_POUPANCA_INAD { get; init; }

    /// <summary>
    /// Latest next action from occurrences.
    /// </summary>
    public string? PROXIMA_ACAO { get; init; }

    /// <summary>
    /// Responsible user name (included in ByResponsavel query).
    /// </summary>
    public string? RESPONSAVEL { get; init; }

    /// <summary>
    /// Responsible user hex color (included in ByResponsavel query).
    /// </summary>
    public string? RESPONSAVEL_COR_HEX { get; init; }
}

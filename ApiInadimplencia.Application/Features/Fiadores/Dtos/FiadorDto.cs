namespace ApiInadimplencia.Application.Features.Fiadores.Dtos;

/// <summary>
/// Represents a guarantor (fiador) from the data warehouse view.
/// </summary>
public sealed record FiadorDto
{
    /// <summary>
    /// Associate ID.
    /// </summary>
    public int? ID_ASSOCIADO { get; init; }

    /// <summary>
    /// Reservation ID.
    /// </summary>
    public int? ID_RESERVA { get; init; }

    /// <summary>
    /// Person ID.
    /// </summary>
    public int? ID_PESSOA { get; init; }

    /// <summary>
    /// Guarantor name.
    /// </summary>
    public string? NOME { get; init; }

    /// <summary>
    /// Document (CPF/CNPJ).
    /// </summary>
    public string? DOCUMENTO { get; init; }

    /// <summary>
    /// Registration date.
    /// </summary>
    public DateTime? DATA_CADASTRO { get; init; }

    /// <summary>
    /// Family income.
    /// </summary>
    public decimal? RENDA_FAMILIAR { get; init; }

    /// <summary>
    /// Association type.
    /// </summary>
    public string? TIPO_ASSOCIACAO { get; init; }

    /// <summary>
    /// Sale number.
    /// </summary>
    public int? NUM_VENDA { get; init; }

    /// <summary>
    /// Address.
    /// </summary>
    public string? ENDERECO { get; init; }
}

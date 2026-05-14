namespace ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

/// <summary>
/// DTO representing an occurrence.
/// </summary>
public record OcorrenciaDto
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Sale number.
    /// </summary>
    public int NumVendaFk { get; init; }

    /// <summary>
    /// Username of the user who registered the occurrence.
    /// </summary>
    public string NomeUsuarioFk { get; init; } = string.Empty;

    /// <summary>
    /// Description of the occurrence.
    /// </summary>
    public string Descricao { get; init; } = string.Empty;

    /// <summary>
    /// Status of the occurrence.
    /// </summary>
    public string StatusOcorrencia { get; init; } = string.Empty;

    /// <summary>
    /// Date of the occurrence.
    /// </summary>
    public DateTime DtOcorrencia { get; init; }

    /// <summary>
    /// Time of the occurrence.
    /// </summary>
    public string HoraOcorrencia { get; init; } = string.Empty;

    /// <summary>
    /// Next action date, when present.
    /// </summary>
    public string? ProximaAcao { get; init; }

    /// <summary>
    /// Protocol number, when present.
    /// </summary>
    public string? Protocolo { get; init; }
}

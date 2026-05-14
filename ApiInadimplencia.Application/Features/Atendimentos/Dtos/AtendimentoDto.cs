namespace ApiInadimplencia.Application.Features.Atendimentos.Dtos;

/// <summary>
/// DTO representing an attendance record.
/// </summary>
public record AtendimentoDto
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Protocol number in AAAAMMDD##### format.
    /// </summary>
    public string Protocolo { get; init; } = string.Empty;

    /// <summary>
    /// Customer CPF.
    /// </summary>
    public string Cpf { get; init; } = string.Empty;

    /// <summary>
    /// Sale number.
    /// </summary>
    public int NumVendaFk { get; init; }

    /// <summary>
    /// JSON snapshot of the sale data.
    /// </summary>
    public string DadosVendaJson { get; init; } = string.Empty;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CriadoEm { get; init; }
}

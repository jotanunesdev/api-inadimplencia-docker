namespace ApiInadimplencia.Application.Features.Negativacao.Dtos;

/// <summary>
/// DTO for a single parcela (installment) for the frontend.
/// </summary>
/// <param name="Id">Parcela ID.</param>
/// <param name="Valor">Parcela value.</param>
/// <param name="Vencimento">Due date.</param>
/// <param name="DiasAtraso">Days overdue (calculated from due date to current date).</param>
public sealed record ParcelaDto(
    int Id,
    decimal Valor,
    DateOnly Vencimento,
    int DiasAtraso);

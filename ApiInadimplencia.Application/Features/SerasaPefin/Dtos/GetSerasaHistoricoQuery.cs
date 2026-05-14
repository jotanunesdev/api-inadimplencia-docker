using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

/// <summary>
/// Query to get Serasa PEFIN history for a sale
/// </summary>
public record GetSerasaHistoricoQuery(int NumVenda) : IQuery<List<SerasaPefinHistoricoItem>>;

/// <summary>
/// Item in Serasa PEFIN history
/// </summary>
public record SerasaPefinHistoricoItem(
    Guid Id,
    int NumVendaFk,
    SerasaPefinRecordType TipoRegistro,
    string? TransactionId,
    SerasaPefinStatus Status,
    DateTime CriadoEm,
    DateTime? EnviadoEm,
    DateTime? CompletadoEm,
    string? ErrorMessage);

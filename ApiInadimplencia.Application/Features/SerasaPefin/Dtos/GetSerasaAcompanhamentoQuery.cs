using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

/// <summary>
/// Query to get Serasa PEFIN acompanhamento by transaction ID
/// </summary>
public record GetSerasaAcompanhamentoQuery(string TransactionId) : IQuery<SerasaPefinAcompanhamentoResponse?>;

/// <summary>
/// Response for Serasa PEFIN acompanhamento
/// </summary>
public record SerasaPefinAcompanhamentoResponse(
    string TransactionId,
    int NumVendaFk,
    SerasaPefinRecordType TipoRegistro,
    SerasaPefinStatus Status,
    DateTime CriadoEm,
    DateTime? EnviadoEm,
    DateTime? CompletadoEm,
    string? ErrorMessage,
    string? PayloadJson,
    string? RespostaJson);

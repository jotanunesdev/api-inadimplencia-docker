using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

/// <summary>
/// Command to request Serasa PEFIN negativacao for a sale.
/// Loads data from DW via IInadimplenciaQueryService and sends to Serasa.
/// </summary>
public record RequestNegativacaoCommand(
    int NumVenda,
    bool IncluirGarantidores = false,
    string Operador = "system",
    Guid? SolicitacaoIdExistente = null,
    IReadOnlyList<int>? ParcelaIds = null) : ICommand<RequestNegativacaoResponse>;

/// <summary>
/// Response for negativacao request containing transaction IDs and status for each solicitation.
/// </summary>
public record RequestNegativacaoResponse(
    IReadOnlyList<SerasaSolicitacaoResult> Solicitacoes,
    SerasaPefinStatus StatusAgregado);

/// <summary>
/// Result of a single solicitation (principal or guarantor).
/// </summary>
public record SerasaSolicitacaoResult(
    Guid SolicitacaoId,
    SerasaPefinRecordType TipoRegistro,
    string? TransactionId,
    SerasaPefinStatus Status,
    string? ErrorMessage = null,
    int? NumeroParcela = null);

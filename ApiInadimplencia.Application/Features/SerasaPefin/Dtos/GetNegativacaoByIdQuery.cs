using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Domain.SerasaPefin;
using System.Text.Json;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

/// <summary>
/// Query to get Serasa PEFIN solicitation by ID
/// </summary>
public record GetNegativacaoByIdQuery(Guid Id) : IQuery<SerasaPefinDetalheDto?>;

/// <summary>
/// Detailed response for a Serasa PEFIN solicitation
/// </summary>
public record SerasaPefinDetalheDto(
    Guid Id,
    int NumVendaFk,
    SerasaPefinRecordType TipoRegistro,
    string? IdSolicitacaoPrincipal,
    string? IdAssociado,
    string? TipoAssociacao,
    string DocumentoDevedorMascarado,
    string? DocumentoGarantidorMascarado,
    string DocumentoCredorMascarado,
    string ContractNumber,
    string CategoryId,
    string AreaInformante,
    decimal Valor,
    DateOnly DataVencimento,
    SerasaPefinStatus Status,
    string? TransactionId,
    string? CadusKey,
    string? CadusSerie,
    JsonElement? PayloadAuditoria,
    JsonElement? WebhookPayload,
    string? ErrorMessage,
    int? ErrorStatusCode,
    string Operador,
    string? SolicitanteUsername,
    string? AprovadorUsername,
    DateTime? DtAprovacao,
    string? Justificativa,
    DateTime CriadoEm,
    DateTime AtualizadoEm);

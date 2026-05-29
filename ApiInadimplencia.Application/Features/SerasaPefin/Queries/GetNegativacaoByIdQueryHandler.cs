using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Queries;

/// <summary>
/// Handler for GetNegativacaoByIdQuery
/// </summary>
public class GetNegativacaoByIdQueryHandler : IQueryHandler<GetNegativacaoByIdQuery, SerasaPefinDetalheDto?>
{
    private readonly ISerasaPefinRepository _repository;
    private readonly ILogger<GetNegativacaoByIdQueryHandler> _logger;

    public GetNegativacaoByIdQueryHandler(
        ISerasaPefinRepository repository,
        ILogger<GetNegativacaoByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SerasaPefinDetalheDto?> HandleAsync(GetNegativacaoByIdQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Serasa PEFIN solicitation by ID {Id}", query.Id);

        var solicitacao = await _repository.GetByIdAsync(query.Id, cancellationToken);
        
        if (solicitacao is null)
        {
            _logger.LogWarning("Serasa PEFIN solicitation with ID {Id} not found", query.Id);
            return null;
        }

        return MapToDto(solicitacao);
    }

    private static SerasaPefinDetalheDto MapToDto(SerasaPefinSolicitacaoCompleta s)
    {
        JsonElement? payloadAuditoria = null;
        JsonElement? webhookPayload = null;

        if (!string.IsNullOrWhiteSpace(s.PayloadAuditoria))
        {
            try
            {
                payloadAuditoria = JsonSerializer.Deserialize<JsonElement>(s.PayloadAuditoria);
            }
            catch (JsonException ex)
            {
                // Log but don't fail - payload might be malformed
                // In production, this should be logged
            }
        }

        if (!string.IsNullOrWhiteSpace(s.WebhookPayload))
        {
            try
            {
                webhookPayload = JsonSerializer.Deserialize<JsonElement>(s.WebhookPayload);
            }
            catch (JsonException ex)
            {
                // Log but don't fail - payload might be malformed
                // In production, this should be logged
            }
        }

        return new SerasaPefinDetalheDto(
            Id: s.Id,
            NumVendaFk: s.NumVendaFk,
            TipoRegistro: s.TipoRegistro,
            IdSolicitacaoPrincipal: s.IdSolicitacaoPrincipal?.ToString(),
            IdAssociado: s.IdAssociado,
            TipoAssociacao: s.TipoAssociacao,
            DocumentoDevedorMascarado: SerasaPefinPayloadBuilder.MaskDocument(s.DocumentoDevedor),
            DocumentoGarantidorMascarado: SerasaPefinPayloadBuilder.MaskDocument(s.DocumentoGarantidor),
            DocumentoCredorMascarado: SerasaPefinPayloadBuilder.MaskDocument(s.DocumentoCredor),
            ContractNumber: s.ContractNumber,
            CategoryId: s.CategoryId,
            AreaInformante: s.AreaInformante,
            Valor: s.Valor,
            DataVencimento: s.DataVencimento,
            Status: s.Status,
            TransactionId: s.TransactionId,
            CadusKey: s.CadusKey,
            CadusSerie: s.CadusSerie,
            PayloadAuditoria: payloadAuditoria,
            WebhookPayload: webhookPayload,
            ErrorMessage: s.ErrorMessage,
            ErrorStatusCode: s.ErrorStatusCode,
            Operador: s.Operador,
            SolicitanteUsername: s.SolicitanteUsername,
            AprovadorUsername: s.AprovadorUsername,
            DtAprovacao: s.DtAprovacao,
            Justificativa: s.Justificativa,
            CriadoEm: s.DtCriacao,
            AtualizadoEm: s.DtAtualizacao);
    }
}

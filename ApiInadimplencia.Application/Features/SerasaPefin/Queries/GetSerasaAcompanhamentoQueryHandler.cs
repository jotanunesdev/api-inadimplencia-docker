using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Queries;

/// <summary>
/// Handler for GetSerasaAcompanhamentoQuery
/// </summary>
public class GetSerasaAcompanhamentoQueryHandler : IQueryHandler<GetSerasaAcompanhamentoQuery, SerasaPefinAcompanhamentoResponse?>
{
    private readonly ISerasaPefinRepository _repository;
    private readonly ILogger<GetSerasaAcompanhamentoQueryHandler> _logger;

    public GetSerasaAcompanhamentoQueryHandler(
        ISerasaPefinRepository repository,
        ILogger<GetSerasaAcompanhamentoQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SerasaPefinAcompanhamentoResponse?> HandleAsync(GetSerasaAcompanhamentoQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Serasa PEFIN acompanhamento for transaction {TransactionId}", query.TransactionId);

        var solicitacao = await _repository.GetByTransactionIdAsync(query.TransactionId, cancellationToken);
        
        if (solicitacao is null)
        {
            _logger.LogWarning("Serasa PEFIN solicitation with transaction ID {TransactionId} not found", query.TransactionId);
            return null;
        }

        return MapToResponse(solicitacao);
    }

    private static SerasaPefinAcompanhamentoResponse MapToResponse(SerasaPefinSolicitacaoCompleta s)
    {
        return new SerasaPefinAcompanhamentoResponse(
            TransactionId: s.TransactionId ?? string.Empty,
            NumVendaFk: s.NumVendaFk,
            TipoRegistro: s.TipoRegistro,
            Status: s.Status,
            CriadoEm: s.DtCriacao,
            EnviadoEm: s.Status == SerasaPefinStatus.PendenteEnvio ? null : s.DtCriacao,
            CompletadoEm: s.Status is SerasaPefinStatus.NegativadoSucesso or SerasaPefinStatus.NegativadoErro or SerasaPefinStatus.BaixadoSucesso or SerasaPefinStatus.BaixadoErro ? s.DtAtualizacao : null,
            ErrorMessage: s.ErrorMessage,
            PayloadJson: s.PayloadAuditoria,
            RespostaJson: s.WebhookPayload);
    }
}

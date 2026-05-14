using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Queries;

/// <summary>
/// Handler for GetSerasaHistoricoQuery
/// </summary>
public class GetSerasaHistoricoQueryHandler : IQueryHandler<GetSerasaHistoricoQuery, List<SerasaPefinHistoricoItem>>
{
    private readonly ISerasaPefinRepository _repository;
    private readonly ILogger<GetSerasaHistoricoQueryHandler> _logger;

    public GetSerasaHistoricoQueryHandler(
        ISerasaPefinRepository repository,
        ILogger<GetSerasaHistoricoQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<SerasaPefinHistoricoItem>> HandleAsync(GetSerasaHistoricoQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Serasa PEFIN history for sale {NumVenda}", query.NumVenda);

        var solicitacoes = await _repository.ListByNumVendaAsync(query.NumVenda, cancellationToken);

        return solicitacoes
            .Select(MapToHistoricoItem)
            .ToList();
    }

    private static SerasaPefinHistoricoItem MapToHistoricoItem(SerasaPefinSolicitacaoCompleta s)
    {
        return new SerasaPefinHistoricoItem(
            Id: s.Id,
            NumVendaFk: s.NumVendaFk,
            TipoRegistro: s.TipoRegistro,
            TransactionId: s.TransactionId,
            Status: s.Status,
            CriadoEm: s.DtCriacao,
            EnviadoEm: s.Status == SerasaPefinStatus.PendenteEnvio ? null : s.DtCriacao,
            CompletadoEm: s.Status is SerasaPefinStatus.NegativadoSucesso or SerasaPefinStatus.NegativadoErro or SerasaPefinStatus.BaixadoSucesso or SerasaPefinStatus.BaixadoErro ? s.DtAtualizacao : null,
            ErrorMessage: s.ErrorMessage);
    }
}

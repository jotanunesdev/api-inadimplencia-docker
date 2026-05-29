using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Handler for ListSolicitacoesPendentesQuery.
/// Lists pending negativacao solicitations filtered by status and other criteria.
/// </summary>
public sealed class ListSolicitacoesPendentesQueryHandler : IQueryHandler<ListSolicitacoesPendentesQuery, IReadOnlyList<SolicitacaoPendenteDto>>
{
    private readonly ISerasaPefinRepository _serasaRepository;
    private readonly IInadimplenciaQueryService _queryService;

    public ListSolicitacoesPendentesQueryHandler(
        ISerasaPefinRepository serasaRepository,
        IInadimplenciaQueryService queryService)
    {
        _serasaRepository = serasaRepository;
        _queryService = queryService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SolicitacaoPendenteDto>> HandleAsync(ListSolicitacoesPendentesQuery query, CancellationToken cancellationToken = default)
    {
        // Parse status string to enum (default to AguardandoAprovacao)
        SerasaPefinStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = SerasaPefinConstants.ParseStatus(query.Status);
        }

        // When SolicitacaoId is provided, ignore other conflicting filters
        int? numVenda = query.SolicitacaoId.HasValue ? null : query.NumVenda;
        string? solicitanteUsername = query.SolicitacaoId.HasValue ? null : query.SolicitanteUsername;
        SerasaPefinStatus? effectiveStatus = query.SolicitacaoId.HasValue ? null : status;

        // Call repository with filters
        var solicitacoes = await _serasaRepository.ListByStatusAsync(
            effectiveStatus,
            numVenda,
            query.SolicitacaoId,
            solicitanteUsername,
            query.Take,
            query.Skip,
            cancellationToken);

        // Map to DTOs, enriching with client data from DW
        var dtos = new List<SolicitacaoPendenteDto>();
        foreach (var solicitacao in solicitacoes)
        {
            // Get client data from DW for this sale
            string cliente = "Cliente não encontrado";
            string cpfMasked = "***";

            try
            {
                var vendaData = await _queryService.GetDividasElegiveisAsync(solicitacao.NumVendaFk, 0, cancellationToken);
                if (vendaData != null)
                {
                    cliente = vendaData.Cliente ?? "Cliente não encontrado";
                    cpfMasked = NegativacaoOcorrenciaScripts.MaskDocument(vendaData.Cpf);
                }
            }
            catch
            {
                // If we can't get client data, use placeholders
                cliente = "Cliente não disponível";
                cpfMasked = "***";
            }

            dtos.Add(new SolicitacaoPendenteDto(
                Id: solicitacao.Id,
                NumVenda: solicitacao.NumVendaFk,
                Cliente: cliente,
                CpfMasked: cpfMasked,
                SolicitanteUsername: solicitacao.SolicitanteUsername ?? string.Empty,
                DtSolicitacao: solicitacao.DtCriacao,
                Status: solicitacao.Status.ToString(),
                Valor: solicitacao.Valor));
        }

        return dtos;
    }
}

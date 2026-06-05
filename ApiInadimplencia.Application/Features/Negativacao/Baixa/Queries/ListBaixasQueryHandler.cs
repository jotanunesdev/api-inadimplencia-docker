using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries;

/// <summary>
/// Handler para <see cref="ListBaixasQuery"/>. Faz parse opcional do status
/// (canonical UPPER_SNAKE_CASE), aplica limites razoáveis em <c>Take</c> e
/// retorna DTOs resumidos.
/// </summary>
public sealed class ListBaixasQueryHandler : IQueryHandler<ListBaixasQuery, IReadOnlyList<BaixaResumoDto>>
{
    /// <summary>Limite máximo de itens retornados em uma única página.</summary>
    public const int MaxTake = 200;

    private readonly ISerasaPefinBaixaRepository _repository;

    public ListBaixasQueryHandler(ISerasaPefinBaixaRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BaixaResumoDto>> HandleAsync(ListBaixasQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        SerasaPefinBaixaStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = SerasaPefinBaixaStatusExtensions.ParseBaixaStatus(query.Status);
        }

        var take = Math.Clamp(query.Take <= 0 ? 50 : query.Take, 1, MaxTake);
        var skip = Math.Max(0, query.Skip);

        var baixas = await _repository.ListByStatusAsync(
            status,
            query.NumVenda,
            query.SolicitanteUsername,
            take,
            skip,
            cancellationToken);

        return baixas
            .Select(b => new BaixaResumoDto(
                Id: b.Id,
                NumVenda: b.NumVendaFk,
                NumeroParcela: b.NumeroParcela,
                ContractNumber: b.ContractNumber,
                MotivoCodigo: b.Motivo.Codigo,
                MotivoDescricao: b.Motivo.Descricao,
                Status: b.Status.ToDbValue(),
                SolicitanteUsername: b.SolicitanteUsername,
                AprovadorUsername: b.AprovadorUsername,
                Tentativas: b.Tentativas,
                DtCriacao: b.DtCriacao,
                DtAtualizacao: b.DtAtualizacao))
            .ToList();
    }
}

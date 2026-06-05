using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries;

/// <summary>
/// Handler para <see cref="GetBaixaByIdQuery"/>. Retorna o detalhe completo da
/// baixa com documentos mascarados para exibição segura.
/// </summary>
public sealed class GetBaixaByIdQueryHandler : IQueryHandler<GetBaixaByIdQuery, BaixaDetalheDto?>
{
    private readonly ISerasaPefinBaixaRepository _repository;

    public GetBaixaByIdQueryHandler(ISerasaPefinBaixaRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<BaixaDetalheDto?> HandleAsync(GetBaixaByIdQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var baixa = await _repository.GetByIdAsync(query.Id, cancellationToken);
        if (baixa is null)
        {
            return null;
        }

        return new BaixaDetalheDto(
            Id: baixa.Id,
            IdSolicitacaoNegativacao: baixa.IdSolicitacaoNegativacao,
            NumVenda: baixa.NumVendaFk,
            NumeroParcela: baixa.NumeroParcela,
            ContractNumber: baixa.ContractNumber,
            DocumentoDevedorMasked: NegativacaoOcorrenciaScripts.MaskDocument(baixa.DocumentoDevedor),
            DocumentoCredorMasked: NegativacaoOcorrenciaScripts.MaskDocument(baixa.DocumentoCredor),
            MotivoCodigo: baixa.Motivo.Codigo,
            MotivoDescricao: baixa.Motivo.Descricao,
            Status: baixa.Status.ToDbValue(),
            SolicitanteUsername: baixa.SolicitanteUsername,
            AprovadorUsername: baixa.AprovadorUsername,
            DtAprovacao: baixa.DtAprovacao,
            Justificativa: baixa.Justificativa,
            TransactionId: baixa.TransactionId,
            ErrorMessage: baixa.ErrorMessage,
            ErrorStatusCode: baixa.ErrorStatusCode,
            Tentativas: baixa.Tentativas,
            DtCriacao: baixa.DtCriacao,
            DtAtualizacao: baixa.DtAtualizacao);
    }
}

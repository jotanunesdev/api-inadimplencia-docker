using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries;

/// <summary>
/// Query para obter os detalhes completos de uma solicitação de baixa.
/// </summary>
/// <param name="Id">Identificador da solicitação de baixa.</param>
public sealed record GetBaixaByIdQuery(Guid Id) : IQuery<BaixaDetalheDto?>;

/// <summary>
/// DTO detalhado de uma solicitação de baixa.
/// </summary>
public sealed record BaixaDetalheDto(
    Guid Id,
    Guid IdSolicitacaoNegativacao,
    int NumVenda,
    int? NumeroParcela,
    string ContractNumber,
    string DocumentoDevedorMasked,
    string DocumentoCredorMasked,
    byte MotivoCodigo,
    string MotivoDescricao,
    string Status,
    string SolicitanteUsername,
    string? AprovadorUsername,
    DateTime? DtAprovacao,
    string? Justificativa,
    string? TransactionId,
    string? ErrorMessage,
    int? ErrorStatusCode,
    byte Tentativas,
    DateTime DtCriacao,
    DateTime DtAtualizacao);

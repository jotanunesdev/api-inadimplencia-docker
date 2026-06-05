using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries;

/// <summary>
/// Query para listar solicitações de baixa filtradas por status/venda/solicitante.
/// </summary>
/// <param name="Status">Filtro opcional de status (UPPER_SNAKE_CASE). Quando ausente, lista todos os status.</param>
/// <param name="NumVenda">Filtro opcional de número de venda.</param>
/// <param name="SolicitanteUsername">Filtro opcional de solicitante.</param>
/// <param name="Take">Quantidade de itens por página (default 50, máx 200).</param>
/// <param name="Skip">Offset para paginação (default 0).</param>
public sealed record ListBaixasQuery(
    string? Status = null,
    int? NumVenda = null,
    string? SolicitanteUsername = null,
    int Take = 50,
    int Skip = 0) : IQuery<IReadOnlyList<BaixaResumoDto>>;

/// <summary>
/// DTO resumido de baixa para listagem.
/// </summary>
public sealed record BaixaResumoDto(
    Guid Id,
    int NumVenda,
    int? NumeroParcela,
    string ContractNumber,
    byte MotivoCodigo,
    string MotivoDescricao,
    string Status,
    string SolicitanteUsername,
    string? AprovadorUsername,
    byte Tentativas,
    DateTime DtCriacao,
    DateTime DtAtualizacao);

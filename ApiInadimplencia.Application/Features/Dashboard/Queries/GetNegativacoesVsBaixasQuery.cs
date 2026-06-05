using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Query para o card "Negativações vs Baixas (mensal)" do dashboard.
/// </summary>
/// <param name="Meses">Janela rolante em meses (1..24, default 12).</param>
public sealed record GetNegativacoesVsBaixasQuery(int Meses = 12) : IQuery<IReadOnlyList<NegativacaoBaixaMensalDto>>;

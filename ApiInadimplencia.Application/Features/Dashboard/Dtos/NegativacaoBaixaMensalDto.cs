namespace ApiInadimplencia.Application.Features.Dashboard.Dtos;

/// <summary>
/// DTO de uma bucket mensal do gráfico misto Negativações vs Baixas
/// (alimenta <c>vw_serasa_pefin_negativacao_baixa_mensal</c>).
/// </summary>
/// <param name="AnoMes">Ano-mês no formato <c>YYYY-MM</c>.</param>
/// <param name="QtdNegativacoes">Total de negativações concluídas no mês.</param>
/// <param name="QtdBaixas">Total de baixas concluídas no mês.</param>
public sealed record NegativacaoBaixaMensalDto(
    string AnoMes,
    long QtdNegativacoes,
    long QtdBaixas);

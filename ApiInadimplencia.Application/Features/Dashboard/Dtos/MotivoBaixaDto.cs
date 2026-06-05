namespace ApiInadimplencia.Application.Features.Dashboard.Dtos;

/// <summary>
/// DTO de uma linha do gráfico de motivos de baixa Serasa PEFIN
/// (alimenta <c>vw_serasa_pefin_baixa_motivos</c>).
/// </summary>
/// <param name="Motivo">Código do motivo (1, 2, 3, 4, 19, 43, 45).</param>
/// <param name="Descricao">Descrição amigável (UPPER_CASE).</param>
/// <param name="Qtd">Quantidade de baixas concluídas com este motivo na janela.</param>
/// <param name="Percentual">Percentual sobre o total da janela (decimal(6,2)).</param>
public sealed record MotivoBaixaDto(
    byte Motivo,
    string Descricao,
    long Qtd,
    decimal Percentual);

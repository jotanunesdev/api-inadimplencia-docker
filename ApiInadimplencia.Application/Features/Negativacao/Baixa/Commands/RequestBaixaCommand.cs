using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Comando para solicitar a baixa (write-off) Serasa PEFIN de uma ou mais parcelas
/// previamente negativadas com sucesso. Cria uma <c>SerasaPefinBaixaSolicitacao</c>
/// por parcela em status <c>AGUARDANDO_APROVACAO</c>, registra ocorrência e dispara
/// notificações para os aprovadores.
/// </summary>
/// <param name="NumVenda">Número da venda.</param>
/// <param name="ParcelaIds">Lista de IDs (NumeroParcela) das parcelas a baixar.</param>
/// <param name="MotivoBaixa">Código do motivo Serasa (whitelist 1, 2, 3, 4, 19, 43, 45).</param>
/// <param name="SenhaTransacao">Senha de transação do usuário autenticado.</param>
/// <param name="Justificativa">Justificativa opcional do solicitante (vai para a ocorrência).</param>
public sealed record RequestBaixaCommand(
    int NumVenda,
    IReadOnlyList<int> ParcelaIds,
    byte MotivoBaixa,
    string SenhaTransacao,
    string? Justificativa = null) : ICommand<Guid>;

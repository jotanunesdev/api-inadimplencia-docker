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
/// <param name="Rm">
/// Quando <c>true</c>, indica que a solicitação vem da integração TOTVS RM
/// (Fórmula Visual). Nesse modo: (a) ignora autenticação Bearer / sessão e
/// senha de transação, (b) ignora <c>ParcelaIds</c> e exige <c>NumeroDocumento</c>,
/// (c) chama a Serasa diretamente via DELETE usando o número do documento como
/// contract-number, (d) NÃO persiste nada em <c>SERASA_PEFIN_BAIXAS</c> — a
/// rastreabilidade fica a cargo do próprio RM. Use somente na rota direta
/// <c>/negativacao/...</c> (rede interna), nunca via proxy público.
/// </param>
/// <param name="NumeroDocumento">
/// Número do documento (<c>NUMERO_DOCUMENTO</c>) a ser baixado na Serasa. Obrigatório
/// quando <c>Rm=true</c>. Enviado como <c>contract-number</c> diretamente ao Serasa,
/// sem qualquer sufixo de parcela. Ignorado quando <c>Rm=false</c>.
/// </param>
public sealed record RequestBaixaCommand(
    int NumVenda,
    IReadOnlyList<int> ParcelaIds,
    byte MotivoBaixa,
    string SenhaTransacao,
    string? Justificativa = null,
    bool Rm = false,
    string? NumeroDocumento = null) : ICommand<Guid>;

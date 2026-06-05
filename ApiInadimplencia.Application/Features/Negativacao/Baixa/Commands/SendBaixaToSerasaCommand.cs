using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Comando interno disparado após aprovação para enviar uma solicitação de baixa
/// específica ao Serasa via DELETE por contrato. Em caso de sucesso, transiciona o
/// agregado para <c>BAIXA_AGUARDANDO_RETORNO</c>; em caso de falha HTTP, transiciona
/// para <c>APROVADA_FALHA_ENVIO</c> e propaga a exceção.
/// </summary>
/// <param name="BaixaId">Identificador da <c>SerasaPefinBaixaSolicitacao</c> a enviar.</param>
public sealed record SendBaixaToSerasaCommand(Guid BaixaId) : ICommand<bool>;

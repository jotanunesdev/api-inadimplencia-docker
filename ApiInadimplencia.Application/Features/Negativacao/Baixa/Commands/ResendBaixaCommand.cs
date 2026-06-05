using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Resultado de um reenvio bem-sucedido de baixa Serasa.
/// </summary>
/// <param name="BaixaId">Identificador da solicitação de baixa.</param>
/// <param name="TransactionId">Novo <c>transactionId</c> retornado pelo Serasa.</param>
/// <param name="Tentativas">Contador de tentativas após o reenvio (1..3).</param>
public sealed record ResendBaixaResult(Guid BaixaId, string TransactionId, byte Tentativas);

/// <summary>
/// Comando para reenviar ao Serasa uma solicitação de baixa que ficou em
/// <c>BAIXADO_ERRO</c> via webhook. O reenvio NÃO requer nova aprovação,
/// apenas que o solicitante (ou um super-decisor) acione e que o limite
/// de 3 tentativas ainda não tenha sido atingido.
/// </summary>
/// <param name="SolicitacaoId">Identificador da solicitação de baixa.</param>
public sealed record ResendBaixaCommand(Guid SolicitacaoId) : ICommand<ResendBaixaResult>;

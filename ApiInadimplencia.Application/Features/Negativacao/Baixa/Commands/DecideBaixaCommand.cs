using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Negativacao.Commands;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Comando para aprovar ou rejeitar uma solicitação de baixa Serasa PEFIN.
/// Reusa <see cref="DecisaoNegativacao"/> (APROVAR / REJEITAR) para alinhar
/// contratos com o fluxo de negativação. Em aprovação dispara
/// <see cref="SendBaixaToSerasaCommand"/> via handler. Em rejeição grava
/// a justificativa no agregado e notifica o solicitante.
/// </summary>
/// <param name="SolicitacaoId">Identificador da solicitação de baixa.</param>
/// <param name="Decisao">Decisão (APROVAR ou REJEITAR).</param>
/// <param name="SenhaTransacao">Senha de transação do aprovador autenticado.</param>
/// <param name="Justificativa">Justificativa obrigatória em REJEITAR; opcional em APROVAR.</param>
public sealed record DecideBaixaCommand(
    Guid SolicitacaoId,
    DecisaoNegativacao Decisao,
    string SenhaTransacao,
    string? Justificativa = null) : ICommand<bool>;

# PRD - Envio Individual por Parcela ao Serasa (Backend)

## Contexto

Hoje o `RequestNegativacaoCommandHandler` envia para o Serasa **um unico registro principal por venda** (com valor total e dataVencimento unica) + N fiadores. A regra de negocio correta e enviar **uma negativacao Serasa por parcela** elegivel, pois cada parcela representa um titulo distinto com vencimento e valor proprios.

Sintomas / motivacoes:

- Serasa pode rejeitar agrupamento por exigir titulos individuais.
- Retornos (webhooks) ficam ambiguos: nao da para saber qual parcela foi efetivada.
- Auditoria/ocorrencia atual em `DecideNegativacaoCommandHandler` usa `new List<long> { 1 }` como placeholder.

## Objetivo

Implementar envio de uma chamada Serasa **por parcela elegivel** durante a aprovacao da solicitacao, com persistencia, retorno e status individualizados, mantendo a solicitacao "pai" como agregador.

## Escopo

- Refatorar `RequestNegativacaoCommandHandler` para iterar nas parcelas selecionadas.
- Persistir 1 `SerasaPefinSolicitacaoCompleta` (ou nova entidade `SerasaPefinSolicitacaoParcela`) por parcela enviada.
- Vincular fiadores corretamente (1 chamada por fiador POR parcela ou 1 por fiador no agregado? - decisao na techspec).
- Tratar falhas parciais: algumas parcelas enviadas, outras com erro.
- Atualizar webhook handler para casar retorno por parcela.
- Atualizar `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` para chave por parcela.
- Atualizar `DecideNegativacaoCommandHandler` para passar os ids reais de parcela.

## Fora do escopo

- Alteracao no contrato `POST /solicitacoes/{id}/decisao` (continua aceitando senha + justificativa).
- UI nova do frontend (sera coberta em PRD `prd-negativacao-envio-por-parcela-frontend`).

## Personas

- Operacao financeira / Aprovadores: precisam visibilidade do status real por parcela.
- Serasa: recebe 1 titulo individual por parcela, conforme contrato.

## Criterios de aceite

- Ao aprovar uma solicitacao com N parcelas, sao realizadas N chamadas Serasa (principal) + N*F (fiadores se aplicavel), persistindo registros individualizados.
- Solicitacao "pai" tem status agregado: `EnviadoTodas`, `EnviadoParcial`, `FalhaTodas`.
- Webhooks atualizam status da parcela correspondente (matched por transactionId).
- Auditoria e ocorrencias listam parcelas reais.
- Index unico previne duplicacao por (numVenda, contractNumber, documentoDevedor, documentoGarantidor, tipoRegistro, parcelaId).
- Testes E2E cobrem sucesso total, falha parcial e falha total.

## Dependencias

- Frontend coordenado em PRD `prd-negativacao-envio-por-parcela-frontend` para exibir status por parcela.
- Entrega A (`prd-fix-listagem-e-detalhe-solicitacao-negativacao`) deve estar concluida para que o DTO de detalhe ja contenha parcelas, simplificando integracao.

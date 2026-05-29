# Resumo de Tarefas - Envio Individual por Parcela ao Serasa

## Tarefas

- [x] 1.0 Migration: novas colunas (`NumeroParcela`, `ParcelaIdOrigem`, `IdSolicitacaoPai`) e atualizacao do index unico (Complexidade: MEDIUM)
- [x] 2.0 Estender aggregate `SerasaPefinSolicitacaoCompleta` com campos de parcela (Complexidade: MEDIUM)
- [x] 3.0 Atualizar `SerasaPefinRepository` (SQL mappings, AddAsync/UpdateAsync, leitura) (Complexidade: MEDIUM)
- [x] 4.0 Refatorar `PayloadBuilder` para receber dados da parcela (Complexidade: MEDIUM)
- [x] 5.0 Refatorar `RequestNegativacaoCommandHandler` para iterar nas parcelas (Complexidade: HIGH)
- [x] 6.0 Atualizar `DecideNegativacaoCommandHandler` (status agregado e mensagem de notificacao) (Complexidade: MEDIUM)
- [x] 7.0 Testes unitarios e E2E cobrindo sucesso total, parcial e total falha (Complexidade: HIGH)

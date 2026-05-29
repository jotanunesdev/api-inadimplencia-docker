# Tarefa 5.0: Refatorar `RequestNegativacaoCommandHandler` para iterar nas parcelas

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>HIGH</complexity>

Substituir o envio unico por venda por um loop que envia uma chamada Serasa por parcela elegivel + fiadores associados a cada parcela.

<requirements>
- Carregar parcelas elegiveis (via `GetDividasElegiveisQuery` ou repositorio dedicado).
- Iterar nas parcelas. Para cada uma:
  - Criar 1 `SerasaPefinSolicitacaoCompleta` PRINCIPAL com dados da parcela.
  - Persistir e enviar ao gateway.
  - Tratar excecoes individualmente (nao abortar o loop).
  - Para cada fiador, criar 1 garantidor ligado a essa parcela.
- Encadear todas as solicitacoes via `IdSolicitacaoPai` (primeira gerada e pai logico).
- Retornar lista agregada de `SerasaSolicitacaoResult` por (parcela, fiador).
</requirements>

## Subtarefas

- [ ] 5.1 Implementar carregamento das parcelas selecionadas.
- [ ] 5.2 Refatorar loop principal preservando logs e auditoria.
- [ ] 5.3 Tratar erros parciais sem corromper o estado.
- [ ] 5.4 Atualizar idempotencia: solicitacao ja com `AguardandoRetorno`/`Negativado` para a mesma parcela nao deve ser reenviada.
- [ ] 5.5 Atualizar logging para incluir `NumeroParcela`.

## Detalhes de Implementacao

Ver pseudo-codigo na techspec, secao `RequestNegativacaoCommandHandler`.

Cuidado especial:

- `SerasaPefinDuplicateActiveException` ao tentar reenviar uma parcela que ja esta ativa deve apenas ser logada como warning e o loop continuar.
- Throttling: se necessario, adicionar `await Task.Delay(...)` configuravel entre chamadas.

## Criterios de Sucesso

- 1 chamada Serasa por parcela.
- Falha em 1 parcela nao impede envio das demais.
- Resultado retornado para o caller agrega todos os resultados.

## Testes da Tarefa

- [ ] Sucesso total (N parcelas).
- [ ] Falha parcial (1 parcela falha, outras ok).
- [ ] Reenvio idempotente (parcela ja ativa nao reenvia).
- [ ] Com fiadores: numero correto de chamadas.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommand.cs`
- `ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandler.cs`
- `ApiInadimplencia.Application.Tests/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandlerTests.cs`

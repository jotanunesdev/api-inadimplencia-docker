# Tarefa 4.0: Refatorar `PayloadBuilder` para parcela

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Atualizar a interface e implementacao do `PayloadBuilder` para construir o payload usando dados da parcela, nao da venda agregada.

<requirements>
- Acrescentar parametro `Parcela` (valor, vencimento, numero, idOrigem) em `BuildMain`/`BuildGuarantor`.
- Atualizar `contractNumber` para incluir sufixo `-P{numeroParcela}` (ou outra convencao alinhada com Serasa).
- Atualizar PayloadInputs e tests.
</requirements>

## Subtarefas

- [ ] 4.1 Atualizar contrato `IPayloadBuilder` (+ Inputs).
- [ ] 4.2 Implementar logica usando dados da parcela.
- [ ] 4.3 Atualizar callers (RequestNegativacaoCommandHandler) - sera completo na tarefa 5.0.
- [ ] 4.4 Atualizar testes do builder.

## Criterios de Sucesso

- Payload por parcela contem `valor` e `dataVencimento` corretos.
- Identificacao no Serasa permite casar o webhook.

## Testes da Tarefa

- [ ] Unit tests cobrindo construcao do payload com parcela A vs parcela B.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/SerasaPefin/Services/IPayloadBuilder.cs`
- `ApiInadimplencia.Infrastructure/SerasaPefin/PayloadBuilder.cs`
- `ApiInadimplencia.Infrastructure.Tests/SerasaPefin/PayloadBuilderTests.cs`

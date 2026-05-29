# Tarefa 7.0: Testes unitarios e E2E completos

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>HIGH</complexity>

Garantir que o fluxo de envio por parcela esta coberto ponta-a-ponta, incluindo cenarios de falha parcial e idempotencia.

<requirements>
- Testes unit nos handlers atualizados (Request/Decide).
- Testes E2E em `FluxoNegativacaoE2ETests` cobrindo:
  - Aprovar venda com 5 parcelas -> 5 chamadas Serasa principal + N fiadores -> webhooks chegam e atualizam status individuais.
  - Aprovar com falha em 2 parcelas -> agregacao `AprovadaParcial` -> notificacao com resumo correto.
  - Reaprovar (idempotencia): nao duplica.
- Verificar logs (estruturados) com `NumeroParcela`.
</requirements>

## Subtarefas

- [ ] 7.1 Cenario sucesso total.
- [ ] 7.2 Cenario falha parcial.
- [ ] 7.3 Cenario falha total.
- [ ] 7.4 Cenario idempotencia.
- [ ] 7.5 Garantir cobertura de webhook ja existente continua valida.

## Detalhes de Implementacao

Usar fixture E2E ja existente (`FluxoNegativacaoFixture`). Estender com helpers para simular respostas Serasa por parcela.

## Criterios de Sucesso

- Suite CI verde.
- Cobertura aceitavel nos handlers refatorados.

## Testes da Tarefa

- [ ] Suites unit + E2E referenciadas acima.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `api-inadimplencia.Api.Tests/E2E/FluxoNegativacaoE2ETests.cs`
- `api-inadimplencia.Api.Tests/E2E/FluxoNegativacaoFixture.cs`
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Commands/DecideNegativacaoCommandHandlerTests.cs`
- `ApiInadimplencia.Application.Tests/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandlerTests.cs`

# Tarefa 5.0: Testes unitarios e de integracao

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Garantir cobertura ponta-a-ponta dos novos endpoints, handlers e repositorio.

<requirements>
- Cobrir caminhos felizes e de erro.
- Validar paginacao e ordenacao.
- Validar autorizacao em `podeDecidir`.
</requirements>

## Subtarefas

- [ ] 5.1 Testes do repositorio (filtros, ordenacao, paginacao).
- [ ] 5.2 Testes dos handlers (mock do repositorio).
- [ ] 5.3 Testes de integracao do endpoint (status codes, payload).

## Detalhes de Implementacao

Seguir convencoes existentes em `ApiInadimplencia.Application.Tests` e `api-inadimplencia.Api.Tests`.

## Criterios de Sucesso

- Toda a suite CI verde.
- Sem regressao em fluxos existentes.

## Testes da Tarefa

- [ ] Suites unit + integration referenciadas acima.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application.Tests/Features/Negativacao/*`
- `ApiInadimplencia.Infrastructure.Tests/*`
- `api-inadimplencia.Api.Tests/Features/Negativacao/NegativacaoFluxoEndpointsIntegrationTests.cs`

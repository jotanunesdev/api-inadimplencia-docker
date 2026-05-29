# Tarefa 4.0: Endpoints REST `GET /solicitacoes` e `GET /solicitacoes/{id}`

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>LOW</complexity>

Atualizar os endpoints REST para usar os novos handlers e expor o detalhe por id.

<requirements>
- Expandir parametros de query no `GET /solicitacoes`.
- Adicionar `GET /solicitacoes/{id:guid}`.
- Retornar 404 `{ error: "NAO_ENCONTRADA" }` quando handler devolve null.
- Documentar via OpenAPI (`.WithOpenApi()`).
</requirements>

## Subtarefas

- [ ] 4.1 Expandir mapeamento do listar.
- [ ] 4.2 Adicionar mapeamento do detalhe.
- [ ] 4.3 Verificar serializacao camelCase.

## Detalhes de Implementacao

Ver techspec, secao Endpoints.

## Criterios de Sucesso

- Endpoints retornam dados conforme contrato.
- Swagger reflete novos parametros.

## Testes da Tarefa

- [ ] Integration tests em `NegativacaoFluxoEndpointsIntegrationTests.cs` cobrindo:
  - GET por id (200 / 404).
  - GET listar por numVenda + status.
  - GET listar por solicitacaoId.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs`
- `api-inadimplencia.Api.Tests/Features/Negativacao/NegativacaoFluxoEndpointsIntegrationTests.cs`

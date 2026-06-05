# Tarefa 7.0: API — endpoints `/negativacao/baixa/...` (e espelho `/inadimplencia/...`)

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Expor os commands e queries de baixa via HTTP, sob o mesmo padrão de duplo prefixo já usado por `NegativacaoFluxoEndpoints` (raiz `/negativacao` + alias `/inadimplencia/negativacao` para o proxy Sophos). Inclui testes E2E HTTP via `WebApplicationFactory`.

<requirements>
- Sub-grupo `/negativacao/baixa/...` registrado em `NegativacaoFluxoEndpoints` (ou novo arquivo `BaixaFluxoEndpoints` se ficar muito grande — preferir reuso).
- Espelho automático sob `/inadimplencia/negativacao/baixa/...`.
- Endpoints:
  - `POST /solicitacoes` → 201 `{ solicitacaoId }`
  - `GET /solicitacoes/{id}` → 200 detalhe / 404
  - `GET /solicitacoes?status=&numVenda=&take=&skip=` → 200 lista
  - `POST /solicitacoes/{id}/decisao` → 200
  - `POST /solicitacoes/{id}/reenvio` → 200 `{ transactionId }`
- Mapeamento de exceções para HTTP:
  - `UnauthorizedAccessException` → 401
  - `ArgumentException` → 400
  - `InvalidOperationException` com `JA_*` → 409
  - `KeyNotFoundException` → 404
  - `SerasaPefinDuplicateActiveException` → 409
- DTOs de request validados (motivo na whitelist via validação ou conversão para VO).
- OpenAPI com nomes únicos (`...Baixa{Action}`).
</requirements>

## Subtarefas

- [x] 7.1 Criar query `GetBaixaByIdQuery` + handler (somente leitura, retorna DTO detalhado).
- [x] 7.2 Criar query `ListBaixasQuery` + handler (paginação, mesmo padrão de `ListSolicitacoesPendentes`).
- [x] 7.3 Estender `NegativacaoFluxoEndpoints.MapNegativacaoGroup` com sub-grupo `/baixa/...`.
- [x] 7.4 Mapear corretamente o motivo do request → VO `SerasaPefinBaixaMotivo`.
- [x] 7.5 Atualizar `ListSolicitacoesPendentes` (ou wrapper de fila unificada) para incluir baixas com campo `tipo` no DTO (preparação para a UI de fila diferenciada).
- [x] 7.6 Api.Tests cobrindo todos os endpoints e mapeamento de erros.

## Detalhes de Implementação

Ver Tech Spec — “Endpoints de API”. Arquivo de referência: `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` (estrutura de double-mount já estabelecida).

## Critérios de Sucesso

- Todos os 5 endpoints respondem corretamente nos dois prefixos.
- Mapeamento de erros conforme tabela acima.
- Swagger lista os endpoints com nomes únicos.
- Api.Tests verdes; nenhuma regressão nos testes existentes.

## Testes da Tarefa

- [x] Testes E2E HTTP (`Api.Tests/Features/Baixa/`): caminho feliz de solicitação → decisão → reenvio.
- [x] Testes de mapeamento de erros (401, 400, 404, 409).
- [x] Teste verifica que ambos os prefixos (`/negativacao/...` e `/inadimplencia/negativacao/...`) funcionam.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` (modificado)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Queries/GetBaixaByIdQuery.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Queries/ListBaixasQuery.cs` (novo)
- `api-inadimplencia.Api.Tests/Features/Baixa/*` (novos)
- `ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQueryHandler.cs` (modificado)

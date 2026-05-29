# Tarefa 1.0: `ListByStatusAsync` em `ISerasaPefinRepository`

<critical>Ler `prd.md` e `techspec.md` desta pasta antes de comecar. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Adicionar contrato e implementacao SQL de listagem filtrada por status, numVenda, solicitacaoId, solicitanteUsername. Paginacao basica via take/skip.

<requirements>
- Atualizar `ISerasaPefinRepository.cs` com a nova assinatura.
- Implementar em `SerasaPefinRepository.cs` (SQL Server) com query parametrizada.
- Adicionar index `IX_SerasaPefinSolicitacoes_Status_NumVenda` via migration ou script DB (db/).
- Manter idempotencia e auditoria.
</requirements>

## Subtarefas

- [ ] 1.1 Definir contrato `ListByStatusAsync` na port.
- [ ] 1.2 Implementar consulta com `WHERE` condicional (ou EF Core IQueryable composavel).
- [ ] 1.3 Criar script de migration para o novo index.
- [ ] 1.4 Atualizar mocks/fakes nos testes existentes que implementam `ISerasaPefinRepository`.

## Detalhes de Implementacao

Ver techspec, secao Repository. Ordenar por `DtSolicitacao DESC`. Default `take = 50`.

## Criterios de Sucesso

- Build verde com mocks atualizados.
- Query no banco com filtros usa o novo index.

## Testes da Tarefa

- [ ] Teste de integracao do repositorio com TestContainers/SQL real ou mock funcional.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Abstractions/Persistence/ISerasaPefinRepository.cs`
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs`
- `db/` (scripts ou migrations)

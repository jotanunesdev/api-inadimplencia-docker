# Tarefa 1.0: Validar persistência via SQL scripts + testes integração Repository

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Antes de qualquer feature funcionar, é preciso garantir que as tabelas
`SERASA_PEFIN_SOLICITACOES` e `SERASA_PEFIN_WEBHOOKS` existem no banco `dwjnc`
com o schema correto e que o `SerasaPefinRepository` (já implementado) opera
corretamente: insert, update, dedupe via índice único, leitura por id /
transaction id / numVenda, e inserção de webhook.

Esta tarefa segue **TDD**: criar testes primeiro (red), depois ajustar
implementação se necessário (green/refactor).

<requirements>
- Scripts `db/003_serasa_pefin.sql` e `db/004_serasa_pefin_baixa_status.sql` executados no SQL Server (`dwjnc`).
- Tabelas, constraints, FKs e índices criados (validar via `sys.indexes` e `sys.check_constraints`).
- `SerasaPefinRepository` coberto por testes de integração reais contra o SQL Server (não mocks).
- Cobertura mínima: AddAsync sucesso, AddAsync dedupe → SerasaPefinDuplicateActiveException, UpdateAsync, GetByIdAsync, GetByTransactionIdAsync, ListByNumVendaAsync, ExistsActiveAsync, AddWebhookAsync.
- Testes devem ser idempotentes (limpam o que criam).
</requirements>

## Subtarefas

- [ ] 1.1 Executar `db/003_serasa_pefin.sql` no banco `dwjnc` via sqlcmd
- [ ] 1.2 Executar `db/004_serasa_pefin_baixa_status.sql`
- [ ] 1.3 Validar criação via query: `SELECT name FROM sys.tables WHERE name LIKE 'SERASA_PEFIN%'`
- [ ] 1.4 Validar constraints/índices em `sys.indexes` e `sys.check_constraints`
- [ ] 1.5 Criar `ApiInadimplencia.Infrastructure.Tests/Persistence/SqlServer/SerasaPefinRepositoryIntegrationTests.cs`
- [ ] 1.6 Escrever testes (red) para os 7 cenários listados em requirements
- [ ] 1.7 Rodar `dotnet test` e iterar até verde
- [ ] 1.8 Documentar passos no `db/README.md` (já parcialmente feito)

## Detalhes de Implementação

Ver Tech Spec §2 (Modelo de dados) e §5.1 (Isolation level).

Padrão dos testes seguir `AtendimentoRepositoryIntegrationTests.cs` já existente
(`@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure.Tests\Persistence\SqlServer\AtendimentoRepositoryIntegrationTests.cs`).

Helpers de cleanup:
```sql
DELETE FROM dbo.SERASA_PEFIN_WEBHOOKS WHERE TRANSACTION_ID LIKE 'TEST-%';
DELETE FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE OPERADOR = 'test-runner';
```

## Critérios de Sucesso

- 8 tabelas/índices/constraints criados conforme `db/003_*.sql`.
- `dotnet test --filter SerasaPefinRepositoryIntegrationTests` retorna 0 falhas.
- `SqlException Number 2601 / 2627` é convertida em `SerasaPefinDuplicateActiveException`.
- `IsolationLevel.Serializable` é efetivamente aplicada (verificar via `DBCC USEROPTIONS` ou trace flag).

## Testes da Tarefa

- [ ] Teste integração: `AddAsync_NewSolicitacao_PersistsRow`
- [ ] Teste integração: `AddAsync_DuplicateActive_ThrowsSerasaPefinDuplicateActiveException`
- [ ] Teste integração: `UpdateAsync_PersistsStatusChange`
- [ ] Teste integração: `GetByIdAsync_ReturnsHydratedAggregate`
- [ ] Teste integração: `GetByTransactionIdAsync_FindsRow`
- [ ] Teste integração: `ListByNumVendaAsync_ReturnsRowsOrderedDesc`
- [ ] Teste integração: `ExistsActiveAsync_DetectsActiveDuplicate`
- [ ] Teste integração: `AddWebhookAsync_PersistsWebhookRow`

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\db\003_serasa_pefin.sql`
- `@c:\api-inadimplencia-docker\db\004_serasa_pefin_baixa_status.sql`
- `@c:\api-inadimplencia-docker\db\README.md`
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Persistence\SqlServer\SerasaPefinRepository.cs`
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Abstractions\Persistence\ISerasaPefinRepository.cs`
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure.Tests\Persistence\SqlServer\` (nova pasta de testes)

# Tarefa 3.0: Atualizar `SerasaPefinRepository` para os novos campos

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Persistir e ler `NumeroParcela`, `ParcelaIdOrigem`, `IdSolicitacaoPai` no SQL Server. Capturar violacao do novo index unico via `SerasaPefinDuplicateActiveException`.

<requirements>
- Atualizar `AddAsync` para incluir novas colunas no INSERT.
- Atualizar `UpdateAsync` se necessario.
- Atualizar `GetByIdAsync` e `ListByNumVendaAsync` para selecionar novas colunas.
- Mapear violacoes do novo index.
</requirements>

## Subtarefas

- [ ] 3.1 Atualizar SQL/mapeamento.
- [ ] 3.2 Atualizar leitores.
- [ ] 3.3 Atualizar testes de integracao do repositorio.

## Detalhes de Implementacao

Caso o projeto use EF Core, alterar a configuracao de mapeamento. Caso use Dapper/ADO.NET puro, atualizar os scripts inline.

## Criterios de Sucesso

- Persistir solicitacao com `NumeroParcela=2` e ler de volta.
- Index unico bloqueia duplicacao para a mesma parcela.

## Testes da Tarefa

- [ ] Integration tests do repositorio.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs`
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Mappings/*`
- `ApiInadimplencia.Infrastructure.Tests/Persistence/SerasaPefinRepositoryTests.cs`

# Tarefa 1.0: Migration de schema (parcela + index)

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Adicionar colunas necessarias e atualizar o index unico para suportar 1 registro por parcela.

<requirements>
- Adicionar colunas em `dbo.SERASA_PEFIN_SOLICITACOES`:
  - `NumeroParcela INT NULL`
  - `ParcelaIdOrigem NVARCHAR(64) NULL`
  - `IdSolicitacaoPai UNIQUEIDENTIFIER NULL`
- Atualizar (ou recriar) `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` para incluir `NumeroParcela` com filtro `WHERE NumeroParcela IS NOT NULL AND <status ativos>`.
- Manter index legado para registros antigos (`NumeroParcela IS NULL`).
- Criar script idempotente em `db/migrations/`.
</requirements>

## Subtarefas

- [ ] 1.1 Criar arquivo SQL de migration.
- [ ] 1.2 Validar em ambiente de teste (TestContainers/local).
- [ ] 1.3 Documentar plano de rollback.

## Detalhes de Implementacao

Migration usa `IF COL_LENGTH(...) IS NULL ALTER TABLE ADD ...`. Index recriado com `DROP_EXISTING = ON`.

## Criterios de Sucesso

- Migration aplica sem perder dados.
- Index novo suporta filtro condicional.

## Testes da Tarefa

- [ ] Aplicar em DB de teste e validar `sp_help` da tabela.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `db/migrations/2026XX_serasa_parcela.sql`

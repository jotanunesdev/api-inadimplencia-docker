# Tarefa 1.0: Migrations SQL — USUARIO_SENHA_TRANSACAO + extensão SERASA_PEFIN_SOLICITACOES

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>LOW</complexity>

Criar dois scripts de migration SQL idempotentes que (a) criam a tabela `dbo.USUARIO_SENHA_TRANSACAO` para guardar hash de senha de transação por usuário e (b) ampliam o constraint `CK_SERASA_PEFIN_SOLICITACOES_STATUS`, adicionam novos campos de fluxo (`SOLICITANTE_USERNAME`, `APROVADOR_USERNAME`, `DT_APROVACAO`, `JUSTIFICATIVA`) e atualizam o índice único filtrado para também cobrir os status `AGUARDANDO_APROVACAO` e `APROVADA`.

<requirements>
- Scripts idempotentes (`IF NOT EXISTS` / `IF OBJECT_ID IS NULL`).
- Não quebrar dados existentes em `SERASA_PEFIN_SOLICITACOES`.
- Aplicar em SQL Server `dwjnc` (UAT primeiro, depois prod).
- Seguir padrão dos scripts em `db/003_*.sql` e `db/004_*.sql`.
- Documentar no README a ordem de execução.
</requirements>

## Subtarefas

- [ ] 1.1 Criar `db/005_negativacao_fluxo.sql` com tabela `USUARIO_SENHA_TRANSACAO` (PK `USERNAME`, `HASH`, `TENTATIVAS_FALHAS`, `BLOQUEADO_ATE`, `CRIADA_EM`, `ATUALIZADA_EM`).
- [ ] 1.2 Criar `db/006_serasa_pefin_status_extensao.sql`:
  - `DROP CONSTRAINT CK_SERASA_PEFIN_SOLICITACOES_STATUS` se existir.
  - `ADD CONSTRAINT` com novos status: `AGUARDANDO_APROVACAO`, `APROVADA`, `REJEITADA`, `APROVADA_FALHA_ENVIO` + status já existentes.
  - `ALTER TABLE ADD` colunas `SOLICITANTE_USERNAME VARCHAR(100) NULL`, `APROVADOR_USERNAME VARCHAR(100) NULL`, `DT_APROVACAO DATETIME2 NULL`, `JUSTIFICATIVA NVARCHAR(500) NULL`.
  - `DROP INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA` e recriar incluindo `AGUARDANDO_APROVACAO` e `APROVADA` no `WHERE`.
- [ ] 1.3 Atualizar `README.md` (ou doc específico) com nota da ordem de execução: `001 → 002 → 003 → 004 → 005 → 006`.
- [ ] 1.4 Aplicar manualmente em UAT e validar via `SELECT` nas constraints (`sys.check_constraints`, `sys.indexes`).

## Detalhes de Implementação

Ver `techspec.md` seção **Modelos de Dados** (subseções `db/005_negativacao_fluxo.sql` e `db/006_serasa_pefin_status_extensao.sql`) para o SQL completo.

## Critérios de Sucesso

- `SELECT * FROM dbo.USUARIO_SENHA_TRANSACAO` executa sem erro (tabela vazia).
- `SELECT name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES') AND name = 'CK_SERASA_PEFIN_SOLICITACOES_STATUS'` retorna 1 linha cuja definição contém os 4 novos status.
- `INSERT INTO SERASA_PEFIN_SOLICITACOES ... STATUS='AGUARDANDO_APROVACAO'` aceito.
- `INSERT` duplicado para mesma `(NUM_VENDA_FK, CONTRACT_NUMBER, DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, TIPO_REGISTRO)` em status `AGUARDANDO_APROVACAO` é bloqueado pelo índice único filtrado.
- Re-execução do script é noop (idempotência).

## Testes da Tarefa

- [ ] **Teste de integração SQL** (`SerasaPefinRepositoryIntegrationTests`):
  - Inserir solicitação com `STATUS='AGUARDANDO_APROVACAO'` (deve funcionar).
  - Tentar inserir 2ª linha com mesma chave em `AGUARDANDO_APROVACAO` (deve falhar com `SqlException` 2601/2627).
  - Inserir status inválido (ex: `XPTO`) (deve falhar com `CHECK constraint`).
- [ ] **Teste manual de idempotência**: rodar script 2x; segunda execução não pode falhar.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `db/005_negativacao_fluxo.sql` (novo)
- `db/006_serasa_pefin_status_extensao.sql` (novo)
- `README.md` (atualizar seção de migrations)
- `ApiInadimplencia.Infrastructure.Tests/Persistence/SerasaPefinRepositoryIntegrationTests.cs` (estender)

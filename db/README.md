# Scripts SQL - api-inadimplencia

## Banco exclusivo de auditoria

O script `014_api_traffic_audit.sql` deve ser executado no banco configurado em
`AuditDb`, atualmente alimentado pelas variaveis `AUDIT_DB_*`. Ele cria apenas
a estrutura de monitoramento no banco `GERENCIAMENTO`; nao deve ser executado no
banco operacional de inadimplencia.

```powershell
sqlcmd -S "$env:AUDIT_DB_SERVER,$env:AUDIT_DB_PORT" `
  -d "$env:AUDIT_DB_DATABASE" `
  -U "$env:AUDIT_DB_USER" `
  -P "$env:AUDIT_DB_PASSWORD" `
  -C -i db\014_api_traffic_audit.sql
```

Scripts para provisionar o schema do módulo **inadimplência** no SQL Server
apontado pelo `.env` (banco `dwjnc` em `192.168.79.240\bi,10433`).

## Ordem de execução

1. `001_schema_inadimplencia.sql` - **obrigatório**. Cria as tabelas base do módulo:
   `OCORRENCIAS`, `ATENDIMENTOS`, `USUARIO`, `VENDA_RESPONSAVEL`, `KANBAN_STATUS`,
   `INAD_NOTIFICACOES`.
2. `002_masstransit_outbox.sql` - **opcional**. Só aplique se for reabilitar o
   bloco `AddEntityFrameworkOutbox<InadimplenciaDbContext>` em
   `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (atualmente comentado
   para evitar o loop de erros "Invalid object name 'OutboxState'").
3. `003_serasa_pefin.sql` - **obrigatório para módulo Serasa PEFIN**. Cria
   `SERASA_PEFIN_SOLICITACOES` e `SERASA_PEFIN_WEBHOOKS` com constraints, FKs e
   índices (incluindo índice único filtrado para bloqueio de duplicidade ativa).
4. `004_serasa_pefin_baixa_status.sql` - **obrigatório para módulo Serasa PEFIN**.
   Atualiza constraint `CHECK` da coluna `STATUS` para incluir status de baixa
   (`BAIXA_ENVIADA`, `BAIXA_AGUARDANDO_RETORNO`, `BAIXADO_SUCESSO`, `BAIXADO_ERRO`).
5. `005_negativacao_fluxo.sql` - **obrigatório para fluxo de aprovação**. Cria
   `USUARIO_SENHA_TRANSACAO` para armazenamento de hash de senha de transação.
6. `006_serasa_pefin_status_extensao.sql` - **obrigatório para fluxo de aprovação**.
   Estende `SERASA_PEFIN_SOLICITACOES` com novos status de aprovação
   (`AGUARDANDO_APROVACAO`, `APROVADA`, `REJEITADA`, `APROVADA_FALHA_ENVIO`) e
   campos de rastreabilidade (`SOLICITANTE_USERNAME`, `APROVADOR_USERNAME`,
   `DT_APROVACAO`, `JUSTIFICATIVA`).
7. `011_serasa_pefin_baixas.sql` - **obrigatório para fluxo de baixa Serasa**. Cria
   `SERASA_PEFIN_BAIXAS` com FK para `SERASA_PEFIN_SOLICITACOES`, CHECK constraints
   (motivo whitelist `{1,2,3,4,19,43,45}`, status, tentativas 1..3), índices de
   navegação/idempotência e índice único filtrado `UX_SERASA_PEFIN_BAIXAS_ATIVA`
   que impede baixa ativa duplicada por parcela.
8. `012_views_baixa_dashboard.sql` - **obrigatório para dashboard de baixa**. Cria
   as views agregadas `vw_serasa_pefin_baixa_motivos` (distribuição percentual dos
   motivos nos últimos 12 meses) e `vw_serasa_pefin_negativacao_baixa_mensal`
   (série mensal com negativações e baixas concluídas, últimos 12 meses).

## Como executar

### Via `sqlcmd` (host Windows)

```powershell
# Scripts base do módulo inadimplência
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U fluig -P "fluig@2019" -C -i db\001_schema_inadimplencia.sql
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U fluig -P "fluig@2019" -C -i db\002_masstransit_outbox.sql

# Scripts Serasa PEFIN (usar usuário dwbi para permissões ALTER TABLE)
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C -i db\003_serasa_pefin.sql
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C -i db\004_serasa_pefin_baixa_status.sql
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C -i db\005_negativacao_fluxo.sql
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C -i db\006_serasa_pefin_status_extensao.sql

# Scripts do fluxo de baixa Serasa
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C -i db\011_serasa_pefin_baixas.sql
sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C -i db\012_views_baixa_dashboard.sql
```

### Via container da API (sem sqlcmd local)

```powershell
# Scripts base do módulo inadimplência
docker run --rm -i --network api-inadimplencia-docker_default `
  -v "${PWD}\db:/scripts" `
  mcr.microsoft.com/mssql-tools `
  /opt/mssql-tools/bin/sqlcmd `
    -S "192.168.79.240\bi,10433" -d dwjnc -U fluig -P "fluig@2019" -C `
    -i /scripts/001_schema_inadimplencia.sql

# Scripts Serasa PEFIN (usar usuário dwbi para permissões ALTER TABLE)
docker run --rm -i --network api-inadimplencia-docker_default `
  -v "${PWD}\db:/scripts" `
  mcr.microsoft.com/mssql-tools `
  /opt/mssql-tools/bin/sqlcmd `
    -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C `
    -i /scripts/003_serasa_pefin.sql
docker run --rm -i --network api-inadimplencia-docker_default `
  -v "${PWD}\db:/scripts" `
  mcr.microsoft.com/mssql-tools `
  /opt/mssql-tools/bin/sqlcmd `
    -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C `
    -i /scripts/004_serasa_pefin_baixa_status.sql
docker run --rm -i --network api-inadimplencia-docker_default `
  -v "${PWD}\db:/scripts" `
  mcr.microsoft.com/mssql-tools `
  /opt/mssql-tools/bin/sqlcmd `
    -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C `
    -i /scripts/005_negativacao_fluxo.sql
docker run --rm -i --network api-inadimplencia-docker_default `
  -v "${PWD}\db:/scripts" `
  mcr.microsoft.com/mssql-tools `
  /opt/mssql-tools/bin/sqlcmd `
    -S "192.168.79.240\bi,10433" -d dwjnc -U dwbi -P "4bi@2023" -C `
    -i /scripts/006_serasa_pefin_status_extensao.sql
```

Todos os scripts são idempotentes (`IF NOT EXISTS`), portanto podem ser rodados
novamente sem efeitos colaterais.

## Notas

- **Usuário dwbi**: Os scripts `003_serasa_pefin.sql`, `004_serasa_pefin_baixa_status.sql`,
  `005_negativacao_fluxo.sql`, `006_serasa_pefin_status_extensao.sql`,
  `011_serasa_pefin_baixas.sql` e `012_views_baixa_dashboard.sql`
  requerem o usuário `dwbi` (senha: `4bi@2023`) devido às permissões de `ALTER TABLE`
  necessárias para criar/drop constraints. O usuário `fluig` não possui essas permissões.
- Os tipos foram derivados de
  `ApiInadimplencia.Infrastructure/Persistence/SqlServer/InadimplenciaDbContext.cs`
  (mapeamentos `modelBuilder.Entity<T>`).
- Enums são persistidos como `INT` (default do EF Core).
- `DateOnly` foi mapeado para `DATE`, `DateTime` para `DATETIME2(7)`.
- O índice único de `INAD_NOTIFICACOES` usa filtro `WHERE ProximaAcaoDia IS NOT NULL`
  para permitir múltiplas notificações sem `ProximaAcaoDia` (SQL Server trata
  múltiplos NULL como conflito em índices UNIQUE sem filtro).

## Validação

Os scripts 003 e 004 foram validados no ambiente UAT. Consulte `documentos/serasa-pefin-validacao-uat.md`
para detalhes sobre os resultados dos testes, incluindo:
- Criação bem-sucedida das tabelas `SERASA_PEFIN_SOLICITACOES` e `SERASA_PEFIN_WEBHOOKS`
- Validação do índice único filtrado `UX_SERASA_PEFIN_SOLICITACOES_ATIVA`
- Processamento correto de webhooks através da tabela `SERASA_PEFIN_WEBHOOKS`

**Nota**: Durante a validação, foi identificado que o bloqueio de duplicidade não está funcionando
conforme esperado (veja relatório de validação para detalhes). Isso pode indicar um problema
com o índice único ou a lógica de verificação de duplicidade.

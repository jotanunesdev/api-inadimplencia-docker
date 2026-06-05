# Tarefa 1.0: Migrations SQL — tabela `SERASA_PEFIN_BAIXAS` e views do dashboard

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>LOW</complexity>

Criar as migrations SQL idempotentes que desbloqueiam todo o fluxo: tabela `dbo.SERASA_PEFIN_BAIXAS` (com índices) e duas views agregadas usadas pelos gráficos do dashboard.

<requirements>
- Scripts devem ser idempotentes (uso de `IF NOT EXISTS`, `IF OBJECT_ID(...) IS NULL`, `IF COL_LENGTH(...) IS NULL` no padrão de `db/009_serasa_pefin_parcela.sql`).
- Não executar diretamente em produção; aplicar de forma controlada em dev/UAT.
- Tabela com PK `UNIQUEIDENTIFIER`, FK para `SERASA_PEFIN_SOLICITACOES.ID` e CHECK na coluna `MOTIVO` (whitelist {1,2,3,4,19,43,45}).
- Índice único filtrado `UX_SERASA_PEFIN_BAIXAS_ATIVA` por `(NUM_VENDA_FK, CONTRACT_NUMBER, NUMERO_PARCELA)` para estados ativos.
- Duas views: `vw_serasa_pefin_baixa_motivos` (últimos 12 meses, `STATUS='BAIXADO_SUCESSO'`) e `vw_serasa_pefin_negativacao_baixa_mensal` (série mensal de negativações e baixas).
</requirements>

## Subtarefas

- [ ] 1.1 Criar `db/011_serasa_pefin_baixas.sql` com a tabela, CHECK no motivo e índice único filtrado.
- [ ] 1.2 Criar `db/012_views_baixa_dashboard.sql` com as duas views agregadas.
- [ ] 1.3 Atualizar `db/README.md` documentando os novos scripts e ordem de aplicação.
- [ ] 1.4 Validar idempotência rodando os scripts duas vezes contra um banco de dev (sem erros, sem duplicação de índices).
- [ ] 1.5 Validar que o CHECK rejeita motivos fora da whitelist via INSERT manual de teste.

## Detalhes de Implementação

Ver Tech Spec — Modelos de Dados (tabela `dbo.SERASA_PEFIN_BAIXAS`) e seção “Views” em `techspec.md`. Seguir o padrão de migration idempotente de `db/009_serasa_pefin_parcela.sql`.

## Critérios de Sucesso

- Scripts aplicados duas vezes em sequência não falham e não duplicam objetos.
- INSERT na tabela com motivo inválido (ex.: `99`) é rejeitado pelo CHECK.
- Views retornam dados consistentes para janela de 12 meses (zero baixas → resultado vazio, sem erro).
- `db/README.md` atualizado.

## Testes da Tarefa

- [ ] Testes manuais SQL: aplicar `011_*.sql` 2× → verificar índice/PK/FK; aplicar `012_*.sql` 2× → verificar views.
- [ ] Teste de regra: INSERT com motivo `99` → erro de CHECK; INSERT com motivo `3` → sucesso.
- [ ] Smoke das views: `SELECT TOP 5 * FROM vw_serasa_pefin_baixa_motivos;` e `SELECT TOP 5 * FROM vw_serasa_pefin_negativacao_baixa_mensal;`.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `db/011_serasa_pefin_baixas.sql`
- `db/012_views_baixa_dashboard.sql`
- `db/README.md`
- `db/009_serasa_pefin_parcela.sql` (referência de padrão idempotente)

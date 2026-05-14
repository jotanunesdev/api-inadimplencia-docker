# Resumo de Tarefas de Implementação de Serasa PEFIN Completo

> PRD: `tasks/prd-serasa-pefin-completo/prd.md`
> Tech Spec: `tasks/prd-serasa-pefin-completo/techspec.md`

## Tarefas

- [x] 1.0 Validar persistência via SQL scripts + testes integração Repository (Complexidade: HIGH)
- [x] 2.0 Query Service de Inadimplência (DW.fat_analise_inadimplencia_v4 + DW.vw_fiadores_por_venda) (Complexidade: MEDIUM)
- [x] 3.0 Refatorar `GetSerasaPreviewQueryHandler` para consultar banco + validar (Complexidade: HIGH)
- [x] 4.0 Corrigir `SerasaPefinClient` para endpoints reais (`/collection/debt/` e `/collection/debt/guarantor`) (Complexidade: MEDIUM)
- [x] 5.0 Refatorar `RequestNegativacaoCommandHandler` (PayloadBuilder + Repository SERIALIZABLE + cliente real) (Complexidade: HIGH)
- [x] 6.0 Endpoints de Histórico e Detalhe (`GET /historico`, `GET /negativacoes/{id}`) (Complexidade: MEDIUM)
- [x] 7.0 Webhooks (6 endpoints + `SerasaWebhookHandler` idempotente) (Complexidade: HIGH)
- [x] 8.0 Rotas de Teste (`auth`, `debt`, `documents`, `simulate-webhook`) gateadas por Env=uat (Complexidade: LOW)
- [x] 9.0 Validação end-to-end Serasa UAT + documentação final (Complexidade: MEDIUM)

## Dependências

```
1.0 ──► 2.0 ──► 3.0 ─┐
                     ├─► 5.0 ──► 6.0 ──► 9.0
        4.0 ────────┘
                     └─► 7.0 ──► 8.0 ───┘
```

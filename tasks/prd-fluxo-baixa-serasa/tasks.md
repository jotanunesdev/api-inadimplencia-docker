# Resumo de Tarefas de Implementação — Fluxo de Baixa de Dívida Serasa

> PRD: `prd.md` · Tech Spec: `techspec.md` (mesma pasta).

## Tarefas

- [x] 1.0 Migrations SQL — tabela `SERASA_PEFIN_BAIXAS` e views do dashboard (Complexidade: LOW)
- [x] 2.0 Domain — aggregate `SerasaPefinBaixaSolicitacao`, VO `SerasaPefinBaixaMotivo` e enum de status (Complexidade: HIGH — TDD)
- [x] 3.0 Infrastructure — `SerasaPefinBaixaRepository` + extensão do `SerasaPefinClient` com `DeleteByContractAsync` (Complexidade: MEDIUM)
- [x] 4.0 Application — `RequestBaixaCommand` + `SendBaixaToSerasaCommand` (Complexidade: HIGH — TDD)
- [x] 5.0 Application — `DecideBaixaCommand` + `ResendBaixaCommand` (Complexidade: HIGH — TDD)
- [x] 6.0 Webhook — branch de baixa no `SerasaWebhookHandler` com idempotência e notificações (Complexidade: MEDIUM)
- [x] 7.0 API — endpoints `/negativacao/baixa/...` (e espelho `/inadimplencia/...`) (Complexidade: MEDIUM)
- [x] 8.0 Dashboard backend — queries `GetMotivosBaixa` e `GetNegativacoesVsBaixas` + endpoints (Complexidade: MEDIUM)
- [x] 9.0 Frontend — modal modo baixa, confirmação com motivo, provider/hook e diferenciação visual (Complexidade: HIGH)
- [x] 10.0 Frontend dashboard + E2E Playwright — 2 charts no DashboardPage e fluxo ponta-a-ponta (Complexidade: MEDIUM) — **parcial: charts e testes Vitest concluídos; E2E Playwright (10.6) e validação UAT (10.7) pendentes a pedido do usuário**

## Sequenciamento

- **1.0** desbloqueia todas as demais.
- **2.0** depende de 1.0.
- **3.0** depende de 2.0.
- **4.0** depende de 2.0 e 3.0.
- **5.0** depende de 4.0.
- **6.0** depende de 3.0 e 5.0.
- **7.0** depende de 4.0, 5.0 e 6.0.
- **8.0** depende de 1.0 (paralelizável com 3.0–7.0).
- **9.0** depende de 7.0.
- **10.0** depende de 8.0 e 9.0.

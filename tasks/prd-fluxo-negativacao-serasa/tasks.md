# Resumo de Tarefas de Implementação — Fluxo de Negativação Serasa

> PRD: `prd.md` · Tech Spec: `techspec.md` (mesma pasta).

## Tarefas

- [x] 1.0 Migrations SQL (USUARIO_SENHA_TRANSACAO + extensão SERASA_PEFIN_SOLICITACOES) (Complexidade: LOW)
- [x] 2.0 Senha de Transação — entidade, repositório, hasher PBKDF2 e endpoints `/configuracoes/senha-transacao` (Complexidade: MEDIUM)
- [x] 3.0 Infra de autenticação e políticas — `ICurrentUserService`, `IAprovadoresPolicy`, `NegativacaoOptions` (Complexidade: LOW)
- [x] 4.0 Extensão de `SerasaPefinSolicitacaoCompleta` — novos status, campos de fluxo e métodos de transição (Complexidade: MEDIUM)
- [x] 5.0 Reativar Notificações + SSE + criar `INotificationDispatcher` (Complexidade: MEDIUM)
- [x] 6.0 Consulta de dívidas elegíveis — `GetDividasElegiveisQuery` + endpoint `GET /negativacao/vendas/{numVenda}/dividas` (Complexidade: LOW)
- [x] 7.0 Solicitação de negativação — Command + endpoint `POST /negativacao/solicitacoes` + Ocorrência + notificações (Complexidade: HIGH — TDD)
- [x] 8.0 Decisão (aprovar/rejeitar) — Command + endpoint + refatorar `RequestNegativacaoCommandHandler` para reuso (Complexidade: HIGH — TDD)
- [x] 9.0 Estender `SerasaWebhookHandler` para notificar solicitante e aprovador no retorno final (Complexidade: MEDIUM)
- [x] 10.0 Testes E2E + validação manual UAT (Complexidade: MEDIUM)

## Sequenciamento

- **1.0** desbloqueia todas as demais.
- **2.0**, **3.0**, **4.0**, **5.0** podem ser executadas em paralelo após 1.0.
- **6.0** depende de 3.0.
- **7.0** depende de 2.0, 3.0, 4.0, 5.0 e 6.0.
- **8.0** depende de 7.0.
- **9.0** depende de 5.0 e 8.0.
- **10.0** é a etapa final de validação.

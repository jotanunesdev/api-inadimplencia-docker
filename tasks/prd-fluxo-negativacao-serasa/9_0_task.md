# Tarefa 9.0: Estender `SerasaWebhookHandler` para notificar solicitante e aprovador no retorno final

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Estender o `SerasaWebhookHandler` (já existente, módulo `prd-serasa-pefin-completo` Task 7.0) para que, **após** processar o webhook de inclusão (sucesso ou erro) da Serasa, dispare uma notificação in-app + SSE para:
- O **solicitante** (`SOLICITANTE_USERNAME` da entidade ampliada na Task 4.0).
- O **aprovador** (`APROVADOR_USERNAME`).

Deve preservar a idempotência atual do webhook (UUID-based) e não introduzir efeitos colaterais quando o webhook for reentrante.

<requirements>
- Notificação só é disparada **uma vez** por webhook (idempotência preservada).
- Mensagens diferenciadas por tipo:
  - Sucesso: "Cliente {nome} foi negativado com sucesso (venda nº {numVenda})."
  - Erro: "Erro ao negativar cliente {nome} (venda nº {numVenda}): {errorMessage}."
- Dispatch falha não pode reverter o processamento do webhook (catch + log).
- Tipos novos: `RetornoSerasaSucesso`, `RetornoSerasaErro` (já adicionados na Task 5.0).
</requirements>

## Subtarefas

- [ ] 9.1 Injetar `INotificationDispatcher` em `SerasaWebhookHandler`.
- [ ] 9.2 Após `ApplyWebhookTransactionalAsync` retornar com sucesso, identificar usernames a notificar:
  - `solicitacao.SolicitanteUsername` (pode ser null em registros legados — pular se null).
  - `solicitacao.AprovadorUsername` (idem).
- [ ] 9.3 Para webhook de **inclusão sucesso** (`SerasaPefinStatus.NegativadoSucesso`), montar mensagem padrão e disparar `DispatchManyAsync` com tipo `RetornoSerasaSucesso`.
- [ ] 9.4 Para webhook de **inclusão erro** (`SerasaPefinStatus.NegativadoErro`), montar mensagem com `ErrorMessage` e disparar com tipo `RetornoSerasaErro`.
- [ ] 9.5 Tratar webhooks de baixa (sucesso/erro) como **fora deste escopo** (apenas log info; notificações de baixa ficam para fase futura).
- [ ] 9.6 Garantir que dispatch só ocorre quando o webhook é processado pela primeira vez (não em reentrada idempotente).
- [ ] 9.7 Encapsular dispatch em try/catch, logando warning sem propagar.

## Detalhes de Implementação

Ver `techspec.md` seções **Componentes a reativar/refatorar** (item `SerasaWebhookHandler`) e **Fluxo de Dados** (passo "Serasa webhook").

## Critérios de Sucesso

- Webhook de sucesso: ambos os usernames recebem notificação `RetornoSerasaSucesso`.
- Webhook de erro: ambos recebem notificação `RetornoSerasaErro` com `errorMessage` correto.
- Webhook reentrante (mesmo UUID) **não** duplica notificações.
- Falha do `INotificationDispatcher` não impede o `200 OK` do webhook.
- Solicitação legada (sem `SolicitanteUsername`/`AprovadorUsername`) é processada normalmente sem notificações.

## Testes da Tarefa

- [ ] **Unitários** estender `SerasaWebhookHandlerTests`:
  - Sucesso: `Dispatcher.DispatchManyAsync` chamado com 2 usernames + tipo `RetornoSerasaSucesso`.
  - Erro: chamada com tipo `RetornoSerasaErro` e mensagem incluindo `errorMessage`.
  - Reentrada (UUID já processado): dispatch **não** é chamado.
  - Solicitação sem usernames: dispatch **não** chamado; webhook ainda processa.
  - Falha do dispatcher: webhook completa normalmente; warning logado.
- [ ] **Integração** `SerasaWebhookEndToEndTests`:
  - Cenário completo: criar solicitação (Task 7.0) → aprovar (Task 8.0) → simular webhook Serasa → verificar 2 linhas em `INAD_NOTIFICACOES`.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookHandler.cs` (modificar)
- `ApiInadimplencia.Application.Tests/Features/SerasaPefin/Webhooks/SerasaWebhookHandlerTests.cs` (estender)
- `api-inadimplencia.Api.Tests/Endpoints/SerasaWebhookEndToEndTests.cs` (novo)

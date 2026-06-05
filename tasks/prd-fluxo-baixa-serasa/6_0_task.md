# Tarefa 6.0: Webhook — branch de baixa no `SerasaWebhookHandler` com idempotência e notificações

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Estender o `SerasaWebhookHandler` existente para resolver e finalizar solicitações de baixa via o novo `ISerasaPefinBaixaRepository`, mantendo a idempotência por UUID já implementada. Adicionar notificação in-app para o solicitante.

<requirements>
- Quando `eventType == WebhookEventType.Baixa`, resolver via `ISerasaPefinBaixaRepository.GetByTransactionIdAsync` (não usar `ISerasaPefinRepository`).
- Aplicar `AplicarWebhookSucesso` ou `AplicarWebhookErro` no aggregate de baixa.
- Persistir webhook bruto na tabela existente `SERASA_PEFIN_WEBHOOKS` (sem mudar schema).
- Idempotência por `uuid`: payload já processado retorna `WebhookResult.AlreadyProcessed`.
- Disparar notificação in-app ao solicitante:
  - Sucesso: “Baixa concluída com sucesso para parcela X da venda Y.”
  - Erro: “Baixa rejeitada pela Serasa: <mensagem>. Reenvie a solicitação se necessário.” (com link/`solicitacaoId`).
- Manter o caminho atual de webhooks de negativação intocado (sem regressão).
</requirements>

## Subtarefas

- [x] 6.1 Adicionar dependência de `ISerasaPefinBaixaRepository` ao `SerasaWebhookHandler` via DI.
- [x] 6.2 Implementar branch no `HandleAsync` para resolver baixa pelo novo repositório.
- [x] 6.3 Atualizar método de despacho de notificações para tratar `WebhookEventType.Baixa` (hoje retorna cedo — substituir pela lógica de notificação ao solicitante).
- [x] 6.4 Garantir transação serializável ao persistir webhook + atualizar aggregate (reusar padrão de `ApplyWebhookTransactionalAsync` ou criar variante no novo repo).
- [x] 6.5 Atualizar `SerasaWebhookHandlerTests` com cenários de baixa: sucesso, erro, reentrada idempotente.

## Detalhes de Implementação

Ver Tech Spec — “Fluxo de Dados” (passo 4) e “Pontos de Integração — Webhook Serasa”. Arquivo de referência: `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookHandler.cs` (caminho atual de negativação).

## Critérios de Sucesso

- Webhook de baixa sucesso → solicitação em `BaixadoSucesso` + notificação in-app ao solicitante.
- Webhook de baixa erro → solicitação em `BaixadoErro` + notificação com mensagem original da Serasa.
- Reentrada com mesmo `uuid` não altera estado e retorna `AlreadyProcessed`.
- Testes do fluxo de negativação atuais continuam verdes (zero regressão).

## Testes da Tarefa

- [x] Testes unitários: 3 novos cenários para baixa (sucesso, erro, idempotência).
- [x] Teste de regressão: caminho de negativação continua resolvendo via `ISerasaPefinRepository`.
- [x] Teste de notificação: payload do dispatcher contém `solicitanteUsername`, `solicitacaoId`, `status`, mensagem amigável.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookHandler.cs` (modificado)
- `ApiInadimplencia.Application.Tests/Features/SerasaPefin/Webhooks/SerasaWebhookHandlerTests.cs` (modificado)
- `ApiInadimplencia.Application/Abstractions/Persistence/ISerasaPefinBaixaRepository.cs` (referência, da tarefa 3.0)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinBaixaRepository.cs` (pode receber `ApplyWebhookTransactionalAsync`)

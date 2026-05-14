# Tarefa 7.0: Implementar Integração Serasa PEFIN

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar HttpClient tipado para integração Serasa PEFIN com cache de token, command handlers para preview e solicitação de negativação, e webhook handlers para processamento de eventos. Esta tarefa envolve comunicação com API externa crítica, outbox pattern, mascaramento de dados sensíveis e exige TDD (Red-Green-Refactor) devido à alta complexidade.

<requirements>
- Implementar HttpClient tipado para Serasa PEFIN com cache de token (buffer 60s)
- Implementar command handler para preview de negativação por venda
- Implementar command handler para solicitação de negativação principal e garantidor
- Validar documentos UAT, valor mínimo 10.00, data de vencimento e endereço
- Persistir solicitações pendentes antes de chamar Serasa
- Marcar erro se principal falhar
- Continuar enviando garantidores sequencialmente mesmo se um falhar
- Implementar webhook handlers para inclusão, avalista e baixa (sucesso/erro)
- Exigir uuid em webhooks para idempotência
- Implementar retry uma vez em 401
- Mascarar documentos em respostas e logs
- Implementar outbox pattern via MassTransit
- Criar DTOs de entrada/saída
- Testes de unidade e integração (TDD recomendado)
</requirements>

## Subtarefas

- [ ] 7.1 Criar interface ISerasaPefinGateway
- [ ] 7.2 Implementar HttpClient tipado SerasaPefinClient
- [ ] 7.3 Implementar SerasaPefinGateway com cache de token
- [ ] 7.4 Implementar SerasaPefinTokenCache com buffer 60s
- [ ] 7.5 Criar entidade SerasaPefinSolicitacao
- [ ] 7.6 Criar entidade SerasaPefinWebhook
- [ ] 7.7 Criar DTOs para commands Serasa
- [ ] 7.8 Implementar GetSerasaPreviewQueryHandler
- [ ] 7.9 Implementar RequestNegativacaoCommandHandler com outbox
- [ ] 7.10 Implementar GetSerasaHistoricoQueryHandler
- [ ] 7.11 Implementar GetSerasaAcompanhamentoQueryHandler
- [ ] 7.12 Implementar webhook handler para inclusão
- [ ] 7.13 Implementar webhook handler para avalista
- [ ] 7.14 Implementar webhook handler para baixa
- [ ] 7.15 Implementar middleware de mascaramento para documentos Serasa
- [ ] 7.16 Configurar DI e MassTransit para Serasa
- [ ] 7.17 Mapear endpoints REST Serasa
- [ ] 7.18 Escrever testes de unidade (TDD)
- [ ] 7.19 Escrever testes de integração

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Interfaces Principais**: Porta para integração Serasa PEFIN
- **Endpoints de API**: Serasa PEFIN
- **Pontos de Integração**: Serasa Experian PEFIN, dbo.SERASA_PEFIN_SOLICITACOES, dbo.SERASA_PEFIN_WEBHOOKS
- **Abordagem de Testes**: Cenários de teste críticos (idempotência, validações, outbox)

**Interface:**
- Criar `ApiInadimplencia.Application/Abstractions/Integrations/ISerasaPefinGateway.cs`
- Métodos já definidos na techspec

**HttpClient Tipado Serasa:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinClient.cs`
- Autenticação: Basic Auth → Bearer Token
- Endpoints: API de negativação principal/garantidor
- Timeout 10s

**SerasaPefinGateway:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinGateway.cs`
- Implementar ISerasaPefinGateway
- Implementar cache de token com buffer 60s
- Retry 1x em 401
- Mascarar documentos em logs/respostas

**SerasaPefinTokenCache:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinTokenCache.cs`
- Cache em memória com timestamp
- Se token expirado ou não existe, buscar novo
- Buffer de 60s antes de expirar

**Entidades:**
- Criar `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacao.cs`
- Criar `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinWebhook.cs`
- Enums já existem em `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinEnums.cs`

**Query Handler Preview:**
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Queries/GetSerasaPreviewQueryHandler.cs`
- Chamar SerasaPefinGateway.GetPreviewAsync
- Retornar preview de negativação

**Command Handler Negativação:**
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandler.cs`
- Validar documentos UAT
- Validar valor mínimo 10.00
- Validar data de vencimento
- Validar endereço
- Persistir SerasaPefinSolicitacao em EF Core
- Usar outbox MassTransit para garantir entrega após commit DB
- Enviar para Serasa via SerasaPefinGateway
- Marcar erro se principal falhar
- Continuar com garantidores sequencialmente

**Query Handlers Histórico/Acompanhamento:**
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Queries/GetSerasaHistoricoQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Queries/GetSerasaAcompanhamentoQueryHandler.cs`
- Consultar dbo.SERASA_PEFIN_SOLICITACOES

**Webhook Handlers:**
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookInclusaoHandler.cs`
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookAvalistaHandler.cs`
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookBaixaHandler.cs`
- Exigir uuid para idempotência
- Verificar se webhook com uuid já processado
- Persistir SerasaPefinWebhook
- Atualizar SerasaPefinSolicitacao com resultado

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Dtos/SerasaPefinPreview.cs`
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Dtos/RequestNegativacaoCommand.cs`
- Criar `ApiInadimplencia.Application/Features/SerasaPefin/Dtos/SerasaPefinRequest.cs`
- Criar DTOs para webhooks

**Middleware Mascaramento:**
- Atualizar `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs`
- Mascarar CPF/CNPJ em logs/respostas Serasa
- Mascarar tokens Serasa
- Mascarar payloads Serasa

**DI:**
- Registrar ISerasaPefinGateway
- Registrar HttpClient factory com Polly
- Configurar MassTransit consumer para webhooks

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `GET /serasa-pefin/vendas/{numVenda}/preview` → GetSerasaPreviewQuery
  - `POST /serasa-pefin/vendas/{numVenda}/negativacoes` → RequestNegativacaoCommand
  - `GET /serasa-pefin/vendas/{numVenda}/negativacoes` → GetSerasaHistoricoQuery
  - `GET /serasa-pefin/acompanhamento/{transactionId}` → GetSerasaAcompanhamentoQuery
  - `GET /serasa-pefin/negativacoes/{id}` → Query
  - `POST /serasa-pefin/webhooks/{tipo}/{resultado}` → Webhook handlers

## Critérios de Sucesso

- Token cache funcionando com buffer 60s
- Preview de negativação retornado
- Solicitação de negativação enviada com outbox
- Validações de documentos, valor, data, endereço funcionando
- Erro marcado se principal falhar
- Garantidores enviados sequencialmente
- Webhooks processados com idempotência
- Documentos mascarados em logs/respostas
- Retry funcionando em 401
- Endpoints REST funcionando
- Testes de unidade passam (TDD)
- Testes de integração passam com WireMock

## Testes da Tarefa

- [ ] Testes de unidade (TDD - escrever antes da implementação)
  - Testar cache de token com buffer
  - Testar validações de documentos, valor, data, endereço
  - Testar idempotência de webhooks
  - Testar outbox pattern
  - Testar retry em 401
  - Testar mascaramento de documentos
- [ ] Testes de integração
  - Usar WireMock para mockar Serasa
  - Testar preview de negativação
  - Testar solicitação de negativação
  - Testar webhooks com idempotência
  - Testar outbox com RabbitMQ
  - Testar endpoints REST via HttpClient

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>
<critical>PARA TAREFAS COMPLEXIDADE HIGH, SEGUIR PROCESSO RED-GREEN-REFACTOR (TDD) ONDE OS TESTES SÃO CRIADOS ANTES DA IMPLEMENTAÇÃO</critical>

## Arquivos relevantes
- `ApiInadimplencia.Application/Abstractions/Integrations/ISerasaPefinGateway.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinClient.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinGateway.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinTokenCache.cs` (novo)
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacao.cs` (novo)
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinWebhook.cs` (novo)
- `ApiInadimplencia.Application/Features/SerasaPefin/Queries/` (novo)
- `ApiInadimplencia.Application/Features/SerasaPefin/Commands/` (novo)
- `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/` (novo)
- `ApiInadimplencia.Application/Features/SerasaPefin/Dtos/` (novo)
- `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs` (atualizar)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (atualizar)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)

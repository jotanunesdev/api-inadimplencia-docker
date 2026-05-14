# Tarefa 8.0: Implementar Sistema de Notificações SSE e Scanner

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar sistema de notificações em tempo real via SSE nativo, command handlers para criação/marcação/exclusão de notificações, e BackgroundService para scanner de vencidos com trava de reentrada. Esta tarefa envolve comunicação assíncrona, dedupe de notificações e exige TDD (Red-Green-Refactor) devido à alta complexidade.

<requirements>
- Implementar Hub SSE nativo com snapshot inicial e heartbeat 15s
- Implementar command handler para criação de notificação VENDA_ATRIBUIDA
- Implementar command handler para criação de notificação VENDA_ATRASADA
- Implementar command handler para marcar uma notificação como lida
- Implementar command handler para marcar todas notificações como lidas
- Implementar command handler para exclusão lógica de notificações (valida lida)
- Implementar BackgroundService para scanner de vencidos com trava de reentrada
- Scanner deve criar notificações VENDA_ATRASADA para vendas com kanban todo e PROXIMA_ACAO anterior a hoje
- Normalizar username para lowercase
- Implementar dedupe por TIPO|USUARIO|NUM_VENDA|PROXIMA_ACAO_DIA
- Persistir notificação antes de broadcast SSE
- Exigir notificação lida para exclusão (retornar 409 se não lida)
- Considerar responsabilidade atual na listagem
- Criar DTOs de entrada/saída
- Testes de unidade e integração (TDD recomendado)
</requirements>

## Subtarefas

- [ ] 8.1 Criar entidade InadNotificacao
- [ ] 8.2 Criar DTOs para commands de notificações
- [ ] 8.3 Implementar CreateNotificationCommandHandler
- [ ] 8.4 Implementar MarkNotificationAsReadCommandHandler
- [ ] 8.5 Implementar MarkAllNotificationsAsReadCommandHandler
- [ ] 8.6 Implementar DeleteNotificationCommandHandler
- [ ] 8.7 Implementar query handler para listagem de notificações
- [ ] 8.8 Criar Hub SSE nativo SseHub
- [ ] 8.9 Implementar snapshot inicial de notificações
- [ ] 8.10 Implementar heartbeat a cada 15s
- [ ] 8.11 Implementar broadcast de eventos SSE
- [ ] 8.12 Criar BackgroundService OverdueScanner
- [ ] 8.13 Implementar trava de reentrada in-memory
- [ ] 8.14 Implementar lógica de scanner (vendas com kanban todo e PROXIMA_ACAO vencida)
- [ ] 8.15 Implementar dedupe de notificações
- [ ] 8.16 Implementar handler de VENDA_ATRIBUIDA event → broadcast SSE
- [ ] 8.17 Configurar DI para SSE e BackgroundService
- [ ] 8.18 Mapear endpoints REST de notificações
- [ ] 8.19 Escrever testes de unidade (TDD)
- [ ] 8.20 Escrever testes de integração

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Modelos de Dados**: Entidades de Domínio
- **Endpoints de API**: Notificações
- **Pontos de Integração**: dbo.INAD_NOTIFICACOES
- **Abordagem de Testes**: Cenários de teste críticos (dedupe, scanner, SSE)

**Entidade InadNotificacao:**
- Criar `ApiInadimplencia.Domain/Notifications/InadNotificacao.cs`
- Campos: Id, Tipo, Username, NumVenda, ProximaAcaoDia, CriadoEm, Lida, Excluida
- Tipo já definido em `ApiInadimplencia.Domain/Notifications/NotificationTypes.cs`
- Método de fábrica estático com validações
- Normalizar username para lowercase

**Command Handlers Notificações:**
- Criar `ApiInadimplencia.Application/Features/Notifications/Commands/CreateNotificationCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Notifications/Commands/MarkNotificationAsReadCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Notifications/Commands/MarkAllNotificationsAsReadCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Notifications/Commands/DeleteNotificationCommandHandler.cs`
- Dedupe por TIPO|USUARIO|NUM_VENDA|PROXIMA_ACAO_DIA antes de persistir
- Validar notificação lida antes de exclusão (retornar 409)
- Persistir antes de broadcast SSE

**Query Handler Listagem:**
- Criar `ApiInadimplencia.Application/Features/Notifications/Queries/ListNotificationsQuery.cs`
- Criar `ApiInadimplencia.Application/Features/Notifications/Queries/ListNotificationsQueryHandler.cs`
- Filtrar por username (normalizado)
- Filtrar por lida
- Paginação (page, pageSize)
- Considerar responsabilidade atual

**SSE Hub:**
- Criar `ApiInadimplencia.Infrastructure/Notifications/SseHub.cs`
- Endpoint: `GET /notifications/stream?username=`
- Snapshot inicial: listar notificações não lidas do usuário
- Heartbeat a cada 15s: enviar evento keepalive
- Broadcast: enviar evento quando notificação criada/marcada
- Usar Server-Sent Events nativo do ASP.NET Core

**BackgroundService Scanner:**
- Criar `ApiInadimplencia.Infrastructure/BackgroundServices/OverdueScanner.cs`
- Executar a cada X minutos (configurável)
- Trava de reentrada in-memory (flag bool)
- Query: buscar vendas com kanban status = todo e PROXIMA_ACAO < hoje
- Para cada venda, criar notificação VENDA_ATRASADA se não existe dedupe
- Usar CreateNotificationCommandHandler

**Handler Evento VENDA_ATRIBUIDA:**
- Criar `ApiInadimplencia.Application/Features/Notifications/EventHandlers/VendaAtribuidaEventHandler.cs`
- Consumir ResponsavelAtribuidoEvent via MassTransit
- Criar notificação VENDA_ATRIBUIDA para novo responsável
- Broadcast SSE para usuário

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/Notifications/Dtos/CreateNotificationCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Notifications/Dtos/NotificationDto.cs`
- Criar `ApiInadimplencia.Application/Features/Notifications/Dtos/ListNotificationsQuery.cs`

**DI:**
- Registrar SseHub como singleton
- Registrar OverdueScanner como hosted service
- Registrar handlers de eventos MassTransit

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `GET /notifications?username=&page=&pageSize=&lida=` → ListNotificationsQuery
  - `GET /notifications/stream?username=` → SSE Hub
  - `PUT /notifications/{id}/read?username=` → MarkNotificationAsReadCommand
  - `PUT /notifications/read-all?username=` → MarkAllNotificationsAsReadCommand
  - `DELETE /notifications/{id}?username=` → DeleteNotificationCommand

## Critérios de Sucesso

- Notificações criadas com dedupe funcionando
- Notificações marcadas como lidas
- Notificações excluídas apenas se lidas
- SSE conectando com snapshot inicial
- Heartbeat funcionando a cada 15s
- Broadcast SSE funcionando
- Scanner executando com trava de reentrada
- Notificações VENDA_ATRASADA criadas para vendas vencidas
- Handler VENDA_ATRIBUIDA criando notificações
- Username normalizado para lowercase
- Endpoints REST funcionando
- Testes de unidade passam (TDD)
- Testes de integração passam

## Testes da Tarefa

- [ ] Testes de unidade (TDD - escrever antes da implementação)
  - Testar dedupe de notificações
  - Testar normalização de username
  - Testar validação de notificação lida antes de exclusão
  - Testar trava de reentrada do scanner
  - Testar lógica de scanner (vendas vencidas)
  - Mock de SSE hub
- [ ] Testes de integração
  - Testar criação de notificações
  - Testar marcação como lida
  - Testar exclusão (sucesso e falha se não lida)
  - Testar SSE connection e eventos
  - Testar scanner com dados reais
  - Testar handler VENDA_ATRIBUIDA via MassTransit
  - Testar endpoints REST via HttpClient

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>
<critical>PARA TAREFAS COMPLEXIDADE HIGH, SEGUIR PROCESSO RED-GREEN-REFACTOR (TDD) ONDE OS TESTES SÃO CRIADOS ANTES DA IMPLEMENTAÇÃO</critical>

## Arquivos relevantes
- `ApiInadimplencia.Domain/Notifications/InadNotificacao.cs` (novo)
- `ApiInadimplencia.Application/Features/Notifications/Commands/` (novo)
- `ApiInadimplencia.Application/Features/Notifications/Queries/` (novo)
- `ApiInadimplencia.Application/Features/Notifications/Dtos/` (novo)
- `ApiInadimplencia.Application/Features/Notifications/EventHandlers/` (novo)
- `ApiInadimplencia.Infrastructure/Notifications/SseHub.cs` (novo)
- `ApiInadimplencia.Infrastructure/BackgroundServices/OverdueScanner.cs` (novo)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (atualizar)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)

# Tarefa 5.0: Reativar Notificações + SSE + criar `INotificationDispatcher`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Reativar a infraestrutura de notificações que está atualmente desabilitada no projeto:
- Renomear `NotificationRepository.cs.disabled` → `.cs`.
- Descomentar registros de DI (repository, command/query handlers).
- Reativar `SseHub` para push em tempo real.
- Expor endpoint `GET /notifications/stream` (SSE).
- Criar a porta `INotificationDispatcher` (Application) e seu adapter (Infrastructure) que orquestra **persistir** + **push SSE** num único call site, evitando duplicação nos handlers de negócio.
- Adicionar novos `NotificationType`: `SolicitacaoNegativacao`, `AprovacaoNegativacao`, `RejeicaoNegativacao`, `RetornoSerasaSucesso`, `RetornoSerasaErro`.

<requirements>
- `INotificationRepository` e `InadNotificacao` já existem (não recriar).
- Persistência **garantida**: notificação é gravada mesmo se o cliente SSE estiver offline (push é best-effort).
- SSE multiplexado por `Username` (cada cliente recebe apenas suas notificações).
- Backpressure básico: se o `SseHub` estiver cheio, descartar push (mas manter persistência).
- Reaproveitar dedupe key (tipo + usuário + numVenda) já implementado em `CreateNotificationCommandHandler`.
</requirements>

## Subtarefas

- [ ] 5.1 Renomear `Infrastructure/Persistence/SqlServer/NotificationRepository.cs.disabled` → `NotificationRepository.cs`.
- [ ] 5.2 Descomentar em `DependencyInjection.cs`:
  - `services.AddScoped<INotificationRepository, NotificationRepository>();`
  - `services.AddScoped<ICommandHandler<CreateNotificationCommand, Guid>, CreateNotificationCommandHandler>();`
  - Demais command/query handlers de Notifications.
  - `services.AddSingleton<SseHub>();`
- [ ] 5.3 Adicionar valores ao enum `Domain/Notifications/NotificationType.cs`: `SolicitacaoNegativacao`, `AprovacaoNegativacao`, `RejeicaoNegativacao`, `RetornoSerasaSucesso`, `RetornoSerasaErro`.
- [ ] 5.4 Criar port `Application/Features/Notifications/INotificationDispatcher.cs`:
  - `DispatchAsync(NotificationType, username, numVenda, mensagem, ct)`.
  - `DispatchManyAsync(IReadOnlyList<string> usernames, ...)`.
- [ ] 5.5 Criar adapter `Infrastructure/Notifications/NotificationDispatcher.cs`:
  - Resolve `ICommandHandler<CreateNotificationCommand, Guid>` (persiste + dedupe).
  - Resolve `SseHub` para push (`hub.PushAsync(username, dto)`).
  - Em caso de falha do hub, log warning e continua.
- [ ] 5.6 Reativar `SseHub` (verificar se há classe legada/disabled; senão criar com `ConcurrentDictionary<string, Channel<NotificationDto>>`).
- [ ] 5.7 Criar endpoint SSE em `api-inadimplencia.Api/Endpoints/NotificationsSseEndpoints.cs`:
  - `GET /notifications/stream` lê `ICurrentUserService.Username`, registra canal no hub e faz streaming `text/event-stream`.
  - Heartbeat a cada 20s para manter conexão (`: ping\n\n`).
  - Cancela e remove canal no disconnect.
- [ ] 5.8 Mapear endpoint em `Program.cs` ou no grupo de endpoints de notificações já existente.
- [ ] 5.9 Verificar/criar tabela `dbo.INAD_NOTIFICACOES` se ainda não existir (script `db/007_inad_notificacoes.sql` se faltar — verificar antes).

## Detalhes de Implementação

Ver `techspec.md` seções **Componentes a reativar/refatorar** e **Endpoints de API**.

## Critérios de Sucesso

- `GET /notifications` (já existente) retorna lista populada após `INotificationDispatcher.DispatchAsync` ser chamado.
- `GET /notifications/stream` mantém conexão aberta; envia eventos formatados como `data: {json}\n\n`.
- Cliente A conectado **não** recebe notificação destinada a cliente B.
- Reativação não introduz erros de boot do `WebApplicationFactory`.
- Dedupe (mesma `tipo+username+numVenda`) não duplica linha em `INAD_NOTIFICACOES`.

## Testes da Tarefa

- [ ] **Unitários** `NotificationDispatcherTests` com mocks:
  - `DispatchAsync` chama `CreateNotificationCommand` e `SseHub.PushAsync` na ordem correta.
  - `DispatchManyAsync` itera lista e tolera falha individual sem interromper outras.
  - Falha no `SseHub` **não** propaga exceção; persistência ocorreu.
- [ ] **Unitários** `SseHubTests`:
  - `Subscribe` cria canal por username.
  - `PushAsync` entrega ao subscriber correto.
  - `Unsubscribe` libera recursos.
- [ ] **Integração** `NotificationsSseEndpointTests` (`WebApplicationFactory` + `HttpClient`):
  - Conexão `GET /notifications/stream` permanece aberta (timeout > heartbeat).
  - Após `DispatchAsync` server-side, cliente recebe payload no stream.
- [ ] **Não-regressão**: rodar testes existentes de notificações e validar build verde.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/NotificationRepository.cs.disabled` → `.cs` (renomear)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (descomentar registros)
- `ApiInadimplencia.Domain/Notifications/NotificationType.cs` (modificar)
- `ApiInadimplencia.Application/Features/Notifications/INotificationDispatcher.cs` (novo)
- `ApiInadimplencia.Infrastructure/Notifications/NotificationDispatcher.cs` (novo)
- `ApiInadimplencia.Infrastructure/Notifications/SseHub.cs` (reativar/criar)
- `api-inadimplencia.Api/Endpoints/NotificationsSseEndpoints.cs` (novo)
- `api-inadimplencia.Api/Program.cs` (mapear endpoint)
- `db/007_inad_notificacoes.sql` (se necessário)

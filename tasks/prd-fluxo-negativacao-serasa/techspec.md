# Tech Spec — Fluxo de Solicitação e Aprovação de Negativação Serasa

> Skills aplicadas: `clean-ddd-hexagonal`, `cqrs-implementation`, `dotnet-best-practices`.
> PRD: `prd.md` na mesma pasta.

## Resumo Executivo

Esta feature adiciona um **workflow de solicitação → aprovação → envio** sobre o módulo Serasa PEFIN existente. A entidade `SerasaPefinSolicitacaoCompleta` é estendida com novos status (`AGUARDANDO_APROVACAO`, `APROVADA`, `REJEITADA`, `APROVADA_FALHA_ENVIO`). Uma nova entidade `UsuarioSenhaTransacao` armazena hash PBKDF2 da senha de transação por usuário. Aprovadores são uma lista hardcoded em `appsettings`. Notificações in-app são reativadas (`NotificationRepository.cs.disabled` → `.cs`) e o `SseHub` é reativado para push em tempo real. O envio à Serasa após aprovação **reusa** o `RequestNegativacaoCommandHandler` existente. O `SerasaWebhookHandler` é estendido para disparar notificações finais ao solicitante e aprovador.

## Arquitetura do Sistema

### Visão Geral dos Componentes

**Novos componentes (Domain)**
- `UsuarioSenhaTransacao` — aggregate root com hash PBKDF2 + métricas de tentativas/lockout.
- `NegativacaoOcorrenciaScripts` — value object/serviço estático para gerar as mensagens padrão de Ocorrência (solicitação, aprovação, rejeição) a partir de templates.

**Componentes a estender (Domain)**
- `SerasaPefinStatus` (enum em `Domain/SerasaPefin/`) — adicionar `AguardandoAprovacao`, `Aprovada`, `Rejeitada`, `AprovadaFalhaEnvio`.
- `SerasaPefinSolicitacaoCompleta` — novos métodos: `MarcarAguardandoAprovacao`, `MarcarAprovada(string aprovador)`, `MarcarRejeitada(string aprovador, string justificativa)`, `MarcarAprovadaFalhaEnvio(string err)`. Novos campos: `Aprovador`, `DtAprovacao`, `Justificativa`, `SolicitanteUsername` (rastreabilidade).

**Novos componentes (Application)**
- `ICurrentUserService` (port) — expõe `Username`, `IsAuthenticated`.
- `ISenhaTransacaoRepository` (port).
- `ISenhaTransacaoHasher` (port) — abstrai PBKDF2 para testabilidade.
- `IAprovadoresPolicy` (port) — `bool IsAprovador(string username)`, `IReadOnlyList<string> ListAprovadores()`.
- `INotificationDispatcher` (port) — encapsula persistir + push SSE.
- Features `NegativacaoFluxo`:
  - Commands: `SetSenhaTransacaoCommand`, `RequestNegativacaoFluxoCommand`, `DecideNegativacaoCommand`.
  - Queries: `GetDividasElegiveisQuery`, `ListSolicitacoesPendentesQuery`, `GetHasSenhaTransacaoQuery`.
  - DTOs e validators.

**Novos componentes (Infrastructure)**
- `CurrentUserService` — `IHttpContextAccessor` adapter.
- `SenhaTransacaoRepository` (SQL adapter).
- `Pbkdf2SenhaTransacaoHasher` — usa `Microsoft.AspNetCore.Identity.PasswordHasher<UsuarioSenhaTransacao>`.
- `OptionsAprovadoresPolicy` — lê `Negativacao:UsuariosAprovadores` de `IOptions<NegativacaoOptions>`.
- `NotificationDispatcher` — agrega `INotificationRepository` + `SseHub`.
- Migrations SQL (`db/005_negativacao_fluxo.sql` e `db/006_serasa_pefin_status_extensao.sql`).

**Componentes a reativar/refatorar**
- `NotificationRepository.cs.disabled` → renomear para `.cs`, registrar em DI.
- `INotificationRepository`, `CreateNotificationCommandHandler`, `MarkNotificationAsRead*`, `ListNotificationsQueryHandler` — descomentar registros em `DependencyInjection.cs`.
- `SseHub` — reativar e expor endpoint `GET /notifications/stream` (SSE).
- `SerasaWebhookHandler` — estender `ApplyWebhook*` para disparar `INotificationDispatcher` quando status final for `NEGATIVADO_SUCESSO` / `NEGATIVADO_ERRO`.
- `RequestNegativacaoCommandHandler` — refatorar para receber `solicitacaoIdJaCriada` opcional (reusa registro pré-existente em vez de criar novo).
- `InadimplenciaQueryService` — adicionar método `GetParcelasElegiveisAsync(numVenda, ct)`.

**Endpoints novos** (em `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs`)
- `GET /negativacao/vendas/{numVenda}/dividas`
- `GET /negativacao/solicitacoes?status=...`
- `POST /negativacao/solicitacoes`
- `POST /negativacao/solicitacoes/{id}/decisao`
- `GET /configuracoes/senha-transacao`
- `POST /configuracoes/senha-transacao`
- `GET /notifications/stream` (SSE)

### Fluxo de Dados (alto nível)

```
[UI] ── GET /dividas ──► Query handler ──► InadimplenciaQueryService ──► DW.fat_analise_inadimplencia_parcelas
[UI] ── POST /solicitacoes ─► Command:
   1. CurrentUser.Username
   2. Hasher.Verify(senhaTransacao) via SenhaTransacaoRepository
   3. ParcelasElegiveis (re-validação server-side)
   4. Repository.AddAsync(SerasaPefinSolicitacaoCompleta status=AGUARDANDO_APROVACAO)
   5. OcorrenciaRepository.AddAsync(status="Solicitação de negativação")
   6. NotificationDispatcher.NotifyAprovadores(...)
[Aprovador] ── POST /decisao (APROVAR) ─► Command:
   1. AprovadoresPolicy.IsAprovador(currentUser)
   2. Hasher.Verify(senhaTransacao)
   3. solicitante != aprovador
   4. Repository.UpdateAsync(MarcarAprovada)
   5. OcorrenciaRepository.AddAsync("Aprovação Negativação Serasa")
   6. RequestNegativacaoCommand (reuso) → atualiza status=ENVIADA_SERASA
   7. NotificationDispatcher.NotifyAmbos("Enviada ao Serasa, retorno em breve")
[Serasa webhook] ─► SerasaWebhookHandler:
   1. Aplica status (NEGATIVADO_SUCESSO/ERRO) [já existe]
   2. NotificationDispatcher.NotifyAmbos(resultado) [novo]
```

## Design de Implementação

### Interfaces Principais

```csharp
// Application/Abstractions/Auth/ICurrentUserService.cs
public interface ICurrentUserService
{
    string? Username { get; }
    bool IsAuthenticated { get; }
}

// Application/Abstractions/Persistence/ISenhaTransacaoRepository.cs
public interface ISenhaTransacaoRepository
{
    Task<UsuarioSenhaTransacao?> GetByUsernameAsync(string username, CancellationToken ct);
    Task UpsertAsync(UsuarioSenhaTransacao senha, CancellationToken ct);
}

// Application/Abstractions/Auth/ISenhaTransacaoHasher.cs
public interface ISenhaTransacaoHasher
{
    string Hash(string plain);
    bool Verify(string hash, string plain);
}

// Application/Abstractions/Auth/IAprovadoresPolicy.cs
public interface IAprovadoresPolicy
{
    bool IsAprovador(string username);
    IReadOnlyList<string> ListAprovadores();
}

// Application/Features/Notifications/INotificationDispatcher.cs
public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationType tipo, string username,
        int numVenda, string mensagem, CancellationToken ct);
    Task DispatchManyAsync(IReadOnlyList<string> usernames,
        NotificationType tipo, int numVenda, string mensagem, CancellationToken ct);
}
```

### Modelos de Dados

**Domain — `UsuarioSenhaTransacao`**
```csharp
public class UsuarioSenhaTransacao
{
    public string Username { get; private set; }   // PK
    public string Hash { get; private set; }
    public int TentativasFalhas { get; private set; }
    public DateTime? BloqueadoAte { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime AtualizadaEm { get; private set; }

    public static UsuarioSenhaTransacao Criar(string username, string hash);
    public void AtualizarHash(string novoHash);
    public void RegistrarTentativaInvalida(int maxTentativas, TimeSpan janelaLockout);
    public void RegistrarTentativaValida();
    public bool EstaBloqueado(DateTime utcNow);
}
```

**Schema SQL — `db/005_negativacao_fluxo.sql`**
```sql
CREATE TABLE dbo.USUARIO_SENHA_TRANSACAO (
  USERNAME            VARCHAR(100) NOT NULL PRIMARY KEY,
  HASH                NVARCHAR(500) NOT NULL,
  TENTATIVAS_FALHAS   INT NOT NULL DEFAULT 0,
  BLOQUEADO_ATE       DATETIME2 NULL,
  CRIADA_EM           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  ATUALIZADA_EM       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
```

**Schema SQL — `db/006_serasa_pefin_status_extensao.sql`**
```sql
ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
  DROP CONSTRAINT CK_SERASA_PEFIN_SOLICITACOES_STATUS;
ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES WITH CHECK
  ADD CONSTRAINT CK_SERASA_PEFIN_SOLICITACOES_STATUS CHECK (STATUS IN (
    'AGUARDANDO_APROVACAO','APROVADA','REJEITADA','APROVADA_FALHA_ENVIO',
    'PENDENTE_ENVIO','ENVIADO_SERASA','AGUARDANDO_RETORNO',
    'NEGATIVADO_SUCESSO','NEGATIVADO_ERRO',
    'BAIXA_ENVIADA','BAIXA_AGUARDANDO_RETORNO','BAIXADO_SUCESSO','BAIXADO_ERRO'
  ));

ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
  ADD SOLICITANTE_USERNAME VARCHAR(100) NULL,
      APROVADOR_USERNAME   VARCHAR(100) NULL,
      DT_APROVACAO         DATETIME2 NULL,
      JUSTIFICATIVA        NVARCHAR(500) NULL;

-- Índice único filtrado: incluir AGUARDANDO_APROVACAO/APROVADA para evitar duplicidade do fluxo
DROP INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA ON dbo.SERASA_PEFIN_SOLICITACOES;
CREATE UNIQUE INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA
  ON dbo.SERASA_PEFIN_SOLICITACOES (NUM_VENDA_FK, CONTRACT_NUMBER,
        DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, TIPO_REGISTRO)
  WHERE STATUS IN ('AGUARDANDO_APROVACAO','APROVADA','PENDENTE_ENVIO',
        'ENVIADO_SERASA','AGUARDANDO_RETORNO');
```

**Configuração — `NegativacaoOptions`**
```csharp
public class NegativacaoOptions
{
    public const string SectionName = "Negativacao";
    public string[] UsuariosAprovadores { get; set; } = [];
    public int QuorumAprovacao { get; set; } = 1;
    public int DiasAtrasoMinimo { get; set; } = 60;
    public int MaxTentativasSenha { get; set; } = 3;
    public int LockoutMinutos { get; set; } = 15;
    public int JanelaTentativasMinutos { get; set; } = 5;
}
```

### Endpoints de API

| Método | Caminho | Descrição |
|---|---|---|
| `GET` | `/negativacao/vendas/{numVenda}/dividas` | Lista parcelas com flag `elegivel` |
| `GET` | `/negativacao/solicitacoes?status=AGUARDANDO_APROVACAO` | Lista solicitações pendentes |
| `POST` | `/negativacao/solicitacoes` | Body: `{ numVenda, parcelaIds[], incluirFiadores, senhaTransacao }` → `201` `{ id }` |
| `POST` | `/negativacao/solicitacoes/{id}/decisao` | Body: `{ decisao, senhaTransacao, justificativa? }` → `200` |
| `GET` | `/configuracoes/senha-transacao` | `200 { hasSenha: bool }` |
| `POST` | `/configuracoes/senha-transacao` | Body: `{ senhaAtual?, novaSenha }` → `204` |
| `GET` | `/notifications/stream` | SSE multiplexado por `Username` |

Erros: `problem+json` com `code` (`SENHA_INVALIDA`, `SENHA_BLOQUEADA`, `NAO_ELEGIVEL`, `JA_EM_APROVACAO`, `NAO_AUTORIZADO`, `JA_DECIDIDA`, `SOLICITANTE_NAO_PODE_APROVAR`).

## Pontos de Integração

- **Serasa PEFIN** — apenas via reuso de `RequestNegativacaoCommand`. Nenhuma chamada HTTP nova.
- **Webhooks Serasa** — interceptados em `SerasaWebhookHandler` (existente); novo `INotificationDispatcher` injetado.
- **SQL Server `dwjnc`** — novas tabelas `USUARIO_SENHA_TRANSACAO`; `INAD_NOTIFICACOES` reativada; `SERASA_PEFIN_SOLICITACOES` ampliada.
- **`IHttpContextAccessor`** — registrado para `CurrentUserService`. Adicionar `services.AddHttpContextAccessor()` em `DependencyInjection.cs`.

## Abordagem de Testes

### Unitários
- `UsuarioSenhaTransacao`: regras de lockout (3 tentativas/5min → bloqueio 15min), reset em sucesso.
- `Pbkdf2SenhaTransacaoHasher`: hash diferente p/ mesma senha (salt), `Verify` correto.
- `OptionsAprovadoresPolicy`: `IsAprovador` case-insensitive, lista vazia → falha segura.
- `NegativacaoOcorrenciaScripts`: templates renderizados com placeholders corretos (CPF mascarado, fiadores).
- Command handlers (mocks de portas):
  - `SetSenhaTransacaoCommandHandler` — política mínima 6 chars, diferente da atual.
  - `RequestNegativacaoFluxoCommandHandler` — bloqueia se senha inválida; bloqueia se não há parcela elegível; bloqueia se já há ativa.
  - `DecideNegativacaoCommandHandler` — bloqueia não-aprovador, bloqueia auto-aprovação, em aprovar invoca `IRequestNegativacaoOrchestrator` (port), em rejeitar não invoca; gerencia `APROVADA_FALHA_ENVIO`.
- `SerasaWebhookHandler` (testes existentes ampliados): garante `INotificationDispatcher` chamado para ambos usernames quando `NEGATIVADO_*`.

### Integração
- `SenhaTransacaoRepository`: roundtrip + concorrência (upsert).
- Migration `006`: aplica em SQL real, índice único filtrado bloqueia 2ª solicitação `AGUARDANDO_APROVACAO`.
- Endpoint `POST /negativacao/solicitacoes` ↔ DB: cria solicitação + ocorrência + notificação na mesma unit-of-work.
- Endpoint `POST /decisao` (APROVAR): orquestra `RequestNegativacaoCommand` em sequência; se mock Serasa devolver erro → status `APROVADA_FALHA_ENVIO`.
- SSE: ao criar notificação, cliente conectado em `/notifications/stream` recebe evento ≤2s.

### E2E
- (Postergar até PRD da UI) Playwright MCP com fluxo completo: solicitante cria → aprovador aprova → webhook simulado → ambos veem notificação.

## Sequenciamento de Desenvolvimento

1. **Migrations SQL** (`db/005`, `db/006`) — destrava todo o resto.
2. **`UsuarioSenhaTransacao` + repo + hasher + endpoints `/configuracoes/senha-transacao`** (independente).
3. **`ICurrentUserService` + `IAprovadoresPolicy` + `NegativacaoOptions`** (infra base).
4. **Estender `SerasaPefinSolicitacaoCompleta` + status enum + repo updates**.
5. **Reativar `INotificationRepository` + `SseHub` + `INotificationDispatcher`**.
6. **Query `GetDividasElegiveisQuery` + endpoint `/dividas`**.
7. **Command `RequestNegativacaoFluxoCommand` + endpoint `POST /solicitacoes`**.
8. **Command `DecideNegativacaoCommand` + endpoint `POST /decisao`** (depende de 7 e do `RequestNegativacaoCommand` refatorado).
9. **Refatorar `RequestNegativacaoCommandHandler`** para aceitar `solicitacaoIdExistente`.
10. **Estender `SerasaWebhookHandler`** com dispatch de notificações finais.
11. **Endpoint SSE `/notifications/stream`**.
12. **Testes E2E (manual + Playwright na fase do PRD UI)**.

### Dependências Técnicas
- SQL Server acessível em UAT/dev (`dwjnc`).
- Módulo `prd-serasa-pefin-completo` totalmente concluído (Tasks 1.0–9.0). Em particular, `RequestNegativacaoCommandHandler` precisa estar funcional para o passo 8.
- Pacote NuGet: `Microsoft.AspNetCore.Identity` (já presente como transitivo da ASP.NET Core; verificar `PasswordHasher<T>` standalone via `Microsoft.Extensions.Identity.Core`).

## Monitoramento e Observabilidade

- **Métricas (Prometheus via OpenTelemetry já configurado):**
  - `negativacao_solicitacoes_total{status}`
  - `negativacao_decisoes_total{decisao}`
  - `senha_transacao_falhas_total`
  - `negativacao_envio_serasa_duration_seconds`
- **Logs estruturados** com `numVenda`, `solicitacaoId`, `username` (mascarado quando CPF), `decisao`. Senha **nunca** logada — proteger via `SensitiveDataMaskingMiddleware`.
- **Tracing:** spans `Negativacao.Solicitar`, `Negativacao.Decidir`, `Notificacao.Dispatch`.
- **Healthcheck:** acrescentar `senha-transacao-store` (SELECT TOP 1) ao `/health` se ficar relevante.

## Considerações Técnicas

### Decisões Principais

- **Reuso de `SerasaPefinSolicitacaoCompleta` (PRD)** — aceita; trade-off: a entidade carrega campos vazios (`TRANSACTION_ID`, `PAYLOAD_AUDITORIA`) durante `AGUARDANDO_APROVACAO`/`APROVADA`. Mitigação: tornar nullable e validar invariantes apenas na transição para `PENDENTE_ENVIO`.
- **PBKDF2 via `PasswordHasher<T>` (sem nova lib)** — alinhado com stack ASP.NET; `BCrypt.Net-Next` rejeitado por adicionar dependência sem ganho mensurável.
- **`ICurrentUserService` via `IHttpContextAccessor`** — alinha com Clean Architecture (port em Application, adapter em Infrastructure). Evita acoplar handlers ao HTTP.
- **`OptionsAprovadoresPolicy` em `appsettings`** — simples, refatorar para tabela `APROVADORES` é trivial mantendo a port.
- **Índice único ampliado** — incluir `AGUARDANDO_APROVACAO`/`APROVADA` evita duas solicitações simultâneas para a mesma venda (concorrência entre operadores).
- **SSE em vez de WebSocket** — `SseHub` já existia; reuso garante simplicidade e compatibilidade com proxies.

### Riscos Conhecidos

| Risco | Mitigação |
|---|---|
| Aprovador também é o solicitante | Validação explícita (`solicitante != aprovador`) + teste unitário |
| `APROVADA_FALHA_ENVIO` esquecido | Endpoint `POST /negativacao/solicitacoes/{id}/reenviar` (fora deste escopo) ou alerta para aprovadores |
| Senha de transação resetada por engano | UI requer senha atual em troca; admin reset fica fora de escopo (questão aberta no PRD) |
| Migration 006 quebra dados existentes | `WITH CHECK` apenas adiciona valores ao `IN`; valores existentes seguem válidos |
| Notificação não chega via SSE (cliente offline) | Persistência em `INAD_NOTIFICACOES` garante recuperação no próximo `GET /notifications` |
| Lista hardcoded em `appsettings` viaja em deploy | Documentar no README + considerar feature flag |

### Conformidade com Padrões

- `.windsurf/rules/techspec-codebase.md` — Clean Architecture + CQRS respeitados (Domain isolado, ports em Application, adapters em Infrastructure).
- `clean-ddd-hexagonal` — entidade rica (`UsuarioSenhaTransacao` com métodos), ports/adapters claros, sem lógica de domínio em endpoints.
- `dotnet-best-practices` — Options pattern para `NegativacaoOptions`, `IHttpContextAccessor` registrado, `PasswordHasher<T>` nativo.
- `cqrs-implementation` — Commands separados de Queries; cada handler é único e testável.

### Arquivos relevantes e dependentes

**Novos**
```
ApiInadimplencia.Domain/Negativacao/UsuarioSenhaTransacao.cs
ApiInadimplencia.Domain/Negativacao/NegativacaoOcorrenciaScripts.cs
ApiInadimplencia.Application/Abstractions/Auth/ICurrentUserService.cs
ApiInadimplencia.Application/Abstractions/Auth/ISenhaTransacaoHasher.cs
ApiInadimplencia.Application/Abstractions/Auth/IAprovadoresPolicy.cs
ApiInadimplencia.Application/Abstractions/Persistence/ISenhaTransacaoRepository.cs
ApiInadimplencia.Application/Features/Negativacao/Commands/SetSenhaTransacaoCommand(+Handler).cs
ApiInadimplencia.Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommand(+Handler).cs
ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommand(+Handler).cs
ApiInadimplencia.Application/Features/Negativacao/Queries/GetDividasElegiveisQuery(+Handler).cs
ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQuery(+Handler).cs
ApiInadimplencia.Application/Features/Notifications/INotificationDispatcher.cs
ApiInadimplencia.Infrastructure/Auth/CurrentUserService.cs
ApiInadimplencia.Infrastructure/Auth/Pbkdf2SenhaTransacaoHasher.cs
ApiInadimplencia.Infrastructure/Auth/OptionsAprovadoresPolicy.cs
ApiInadimplencia.Infrastructure/Persistence/SqlServer/SenhaTransacaoRepository.cs
ApiInadimplencia.Infrastructure/Notifications/NotificationDispatcher.cs
ApiInadimplencia.Infrastructure/Configuration/NegativacaoOptions.cs
api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs
api-inadimplencia.Api/Endpoints/ConfiguracoesEndpoints.cs
api-inadimplencia.Api/Endpoints/NotificationsSseEndpoints.cs
db/005_negativacao_fluxo.sql
db/006_serasa_pefin_status_extensao.sql
```

**Modificados**
```
ApiInadimplencia.Domain/SerasaPefin/SerasaPefinStatus.cs
ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacaoCompleta.cs
ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandler.cs
ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookHandler.cs
ApiInadimplencia.Application/Abstractions/Persistence/IInadimplenciaQueryService.cs (GetParcelasElegiveis)
ApiInadimplencia.Infrastructure/Persistence/SqlServer/InadimplenciaQueryService.cs
ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs (novos campos/status)
ApiInadimplencia.Infrastructure/Persistence/SqlServer/NotificationRepository.cs.disabled → .cs
ApiInadimplencia.Infrastructure/DependencyInjection.cs (descomentar Notification, registrar novos)
api-inadimplencia.Api/Program.cs (AddHttpContextAccessor)
```

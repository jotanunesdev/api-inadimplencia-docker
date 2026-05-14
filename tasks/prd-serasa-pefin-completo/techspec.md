# Tech Spec — Serasa PEFIN completo (.NET 9)

> Skills aplicadas: `.agents/skills/clean-ddd-hexagonal`, `.agents/skills/dotnet-best-practices`,
> `.agents/skills/cqrs-implementation`.

## 1. Arquitetura

Mantém o padrão Clean Architecture + CQRS do projeto:

```
api-inadimplencia.Api               (Endpoints minimal API)
└── ApiInadimplencia.Application    (CQRS handlers, DTOs, Payload Builder, Ports)
    └── ApiInadimplencia.Domain     (Entidades, Constants, Enums)
└── ApiInadimplencia.Infrastructure (SQL Repository, HttpClient, DI)
```

Pontos-chave:

- **Domain isolado**: `SerasaPefinSolicitacaoCompleta` é aggregate root sem dependências externas.
- **Porta `ISerasaPefinRepository`** em `Application.Abstractions.Persistence` (já criada).
- **Adaptador SQL** `SerasaPefinRepository` em `Infrastructure.Persistence.SqlServer` (já criado).
- **Validadores e payload builder** vivem em `Application.Features.SerasaPefin.Payloads`.
- **Cliente HTTP** `SerasaPefinClient` é a única classe que conhece URLs Serasa.

## 2. Modelo de Dados

### `dbo.SERASA_PEFIN_SOLICITACOES`
Definida por `db/003_serasa_pefin.sql`:

- PK: `ID uniqueidentifier`
- Índice único filtrado **`UX_SERASA_PEFIN_SOLICITACOES_ATIVA`** garante 1 ativa por
  `(NUM_VENDA_FK, CONTRACT_NUMBER, DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, TIPO_REGISTRO)`
  quando `STATUS IN ('PENDENTE_ENVIO','ENVIADO_SERASA','AGUARDANDO_RETORNO')`.
- Constraint `CK_..._STATUS` mantida em sincronia via `db/004_serasa_pefin_baixa_status.sql`.

### `dbo.SERASA_PEFIN_WEBHOOKS`
Definida por `db/003_serasa_pefin.sql`:

- Dedupe por `UUID` extraído do payload (chave de idempotência da Serasa).
- FK opcional para `SERASA_PEFIN_SOLICITACOES.ID`.

## 3. Fluxos principais

### 3.1 Preview

```
Endpoint → Query handler → IInadimplenciaQueryService → SQL DW
                                                   ↘ ISerasaPefinRepository.ExistsActiveAsync
                        → SerasaPefinPayloadBuilder.ValidateOnly()  // sem chamar Serasa
                        → PreviewDto (com blocks[])
```

Validações executadas no preview:
- UAT documents (`SerasaPefinConstants.UatAuthorizedDocuments`)
- Valor mínimo, data, endereço, contractNumber, areaInformante
- Duplicate ativo (best-effort, não bloqueia preview se SQL falhar)

### 3.2 Negativação

```
Endpoint → Command handler:
  1. Carrega venda + fiadores (Query Service)
  2. PayloadBuilder.BuildMainDebt + BuildGuarantor para cada
  3. Em TRANSACTION SERIALIZABLE:
     - Repository.AddAsync(principal)            [com PAYLOAD_AUDITORIA mascarado]
     - Repository.AddAsync(guarantor)            [N vezes]
  4. Fora da transação:
     - Client.PostMainDebt(payload)              → recebe transactionId
     - Repository.UpdateAsync(MarcarAguardandoRetorno)
     - Client.PostGuarantor(payload)             → idem
  5. Retorna lista de {tipo, id, transactionId, status}
```

Tratamento de erros:
- HTTP error do Serasa → `MarcarFalhaEnvio` + log
- Duplicate (índice único) → HTTP 409
- Validation error (PayloadBuilder) → HTTP 400 + missingFields[]/blockedDocuments[]

### 3.3 Webhooks

```
POST /serasa-pefin/webhooks/{tipo}/{resultado}
  1. Parse JSON, extrair UUID
  2. Verificar idempotência: SELECT WHERE UUID = @uuid
  3. Se existir → 200 OK (já processado)
  4. Caso contrário:
     - Repository.GetByTransactionIdAsync(transactionId)
     - Aplicar evento na entidade (AplicarWebhookSucesso/Erro)
     - Repository.UpdateAsync
     - Repository.AddWebhookAsync(record com PROCESSADO=true)
  5. Retorna 200 OK
```

### 3.4 Histórico e detalhe

- `historico` → `Repository.ListByNumVendaAsync` + mapper para DTO (CPF mascarado).
- `detalhe/{id}` → `Repository.GetByIdAsync`.
- `acompanhamento/{transactionId}` → `Repository.GetByTransactionIdAsync`.

## 4. Componentes a implementar / refatorar

| Componente | Localização | Estado |
|---|---|---|
| `SerasaPefinConstants` | `Domain/SerasaPefin/` | ✅ pronto |
| `SerasaPefinSolicitacaoCompleta` | `Domain/SerasaPefin/` | ✅ pronto |
| `ISerasaPefinRepository` | `Application/Abstractions/Persistence/` | ✅ pronto |
| `SerasaPefinRepository` | `Infrastructure/Persistence/SqlServer/` | ✅ pronto |
| `SerasaPefinPayloadBuilder` | `Application/Features/SerasaPefin/Payloads/` | ✅ pronto |
| HttpClient (UseProxy=false + relax SSL UAT) | `Infrastructure/DependencyInjection.cs` | ✅ pronto |
| `IInadimplenciaQueryService` | `Application/Abstractions/Persistence/` | 🟡 Task 2.0 |
| `InadimplenciaQueryService` (SQL) | `Infrastructure/Persistence/SqlServer/` | 🟡 Task 2.0 |
| `GetSerasaPreviewQueryHandler` | `Application/Features/SerasaPefin/Queries/` | 🔴 Task 3.0 (reescrever) |
| `SerasaPefinClient` (endpoints reais) | `Infrastructure/Integrations/SerasaPefin/` | 🔴 Task 4.0 |
| `RequestNegativacaoCommandHandler` | `Application/Features/SerasaPefin/Commands/` | 🔴 Task 5.0 (reescrever) |
| `GetNegativacaoByIdQueryHandler` | `Application/Features/SerasaPefin/Queries/` | 🟡 Task 6.0 |
| `GetSerasaHistoricoQueryHandler` | `Application/Features/SerasaPefin/Queries/` | 🟡 Task 6.0 (refatorar) |
| Webhook endpoints + `SerasaWebhookHandler` | `Application/Features/SerasaPefin/Webhooks/` | 🟡 Task 7.0 |
| Test routes | `api-inadimplencia.Api/Endpoints/` | 🟡 Task 8.0 |

## 5. Decisões técnicas

### 5.1 Isolation level
Toda operação que envolve dedupe usa `IsolationLevel.Serializable` (já no Repository).
Justificativa: Node usa `ISOLATION_LEVEL.SERIALIZABLE` em `createPendingSolicitations`.

### 5.2 Idempotência de webhook
Implementada por consulta `SELECT` antes do INSERT em `SERASA_PEFIN_WEBHOOKS` (UUID
como chave lógica). Não criamos índice único físico para permitir auditoria de
tentativas duplicadas (também armazenamos webhooks com `PROCESSADO=false` quando
falham).

### 5.3 SSL UAT
`HttpClientHandler.ServerCertificateCustomValidationCallback` retorna `true` somente
quando `SerasaPefin:Env == "uat"`. Em produção, validação estrita.

### 5.4 Proxy corporativo (Docker Desktop / Sophos)
`HttpClientHandler.UseProxy = false` no `SerasaPefinClient`. Os hosts
`api.serasa.dev` e `uat-api.serasaexperian.com.br` precisam estar liberados no
firewall corporativo.

### 5.5 Mascaramento
Todos os documentos persistidos em `PAYLOAD_AUDITORIA` passam por
`SerasaPefinPayloadBuilder.MaskDocument`. Logs estruturados usam o
`SensitiveDataMaskingMiddleware` existente + atributos por campo.

### 5.6 Testes
- **Unitários**: PayloadBuilder, validações, mapeamentos de DTO, regras de status.
- **Integração**: Repository contra SQL real (test db), webhook idempotente,
  duplicate index, transação serializable.
- **End-to-end manual**: chamadas reais ao Serasa UAT com mass documents.

### 5.7 Contratos de URL Serasa
| Operação | URL UAT | URL Prod |
|---|---|---|
| Auth | `https://uat-api.serasaexperian.com.br/security/iam/v1/client-identities/login` | `https://api.serasaexperian.com.br/...` |
| Inclusão principal | `https://api.serasa.dev/collection/debt/` | `https://api.serasa.com.br/collection/debt/` |
| Inclusão avalista | `https://api.serasa.dev/collection/debt/guarantor` | `https://api.serasa.com.br/collection/debt/guarantor` |

## 6. Sequência de implementação

1. Task 1.0: Aplicar SQL + cobrir Repository com testes (sem isso o resto não roda).
2. Task 2.0: Query Service de inadimplência (dependência do preview).
3. Tasks 3.0 e 4.0 em paralelo.
4. Task 5.0 (depende de 3.0 + 4.0).
5. Task 6.0 (lista/detalhe).
6. Task 7.0 (webhooks).
7. Task 8.0 (test routes).
8. Task 9.0 (validação E2E).

## 7. Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Certificado UAT instável | Fallback SSL configurável; logar `ServerCertificateCustomValidationCallback` apenas para `api.serasa.dev` |
| Webhook reentrante | Idempotência por UUID + SELECT antes do INSERT |
| Duplicate concorrente (race) | Índice único filtrado + tratar `SqlException Number=2601/2627` |
| Token Serasa expira | `SerasaPefinTokenCache` existente + retry com refresh em 401 |
| Sophos volta a interceptar | Documentar hosts a liberar no README |
| Performance preview com muitos fiadores | Query única por venda + sem N+1 |

## 8. Arquivos a modificar (resumo)

```
ApiInadimplencia.Application/
  Abstractions/Persistence/IInadimplenciaQueryService.cs          (novo)
  Features/SerasaPefin/
    Commands/RequestNegativacaoCommandHandler.cs                  (reescrever)
    Queries/GetSerasaPreviewQueryHandler.cs                       (reescrever)
    Queries/GetSerasaHistoricoQueryHandler.cs                     (refatorar)
    Queries/GetSerasaAcompanhamentoQueryHandler.cs                (refatorar)
    Queries/GetNegativacaoByIdQueryHandler.cs                     (novo)
    Webhooks/SerasaWebhookHandler.cs                              (reescrever)
    Webhooks/SerasaWebhookCommands.cs                             (novo)
    Dtos/PreviewDto.cs (e correlatos)                             (atualizar)
ApiInadimplencia.Infrastructure/
  Persistence/SqlServer/InadimplenciaQueryService.cs              (novo)
  Integrations/SerasaPefin/SerasaPefinClient.cs                   (reescrever)
  Integrations/SerasaPefin/SerasaPefinGateway.cs                  (refatorar / remover GetPreview)
  Configuration/SerasaPefinOptions.cs                             (remover PreviewEndpoint)
api-inadimplencia.Api/
  Endpoints/InadimplenciaEndpoints.cs                             (adicionar webhooks + detalhe + test routes)
```

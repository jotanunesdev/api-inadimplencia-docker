# Tech Spec — Fluxo de Baixa de Dívida no Serasa PEFIN

## Resumo Executivo

A solução introduz um **bounded context dedicado de baixa** (`SerasaPefinBaixaSolicitacao`) ao lado do contexto de negativação atual (`SerasaPefinSolicitacaoCompleta`), preservando o fluxo existente intacto. A baixa reutiliza a infraestrutura transversal (senha de transação, política de aprovadores, notificações in-app, webhooks) mas tem **aggregate, tabela, repositório e gateway dedicados**, com endpoints sob `/negativacao/baixa/...`.

A integração externa usa `DELETE /collection/debt/contract` com `contract-number` no header (reaproveitando o número de contrato já persistido). O frontend reutiliza o `NegativacaoDebtsModal` adicionando uma prop `modo`, e o dashboard ganha dois cards alimentados por **duas views SQL agregadas** consumidas via novas queries CQRS, renderizados com `@mui/x-charts` (já presente).

## Arquitetura do Sistema

### Visão Geral dos Componentes

**Componentes novos (Domain):**
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinBaixaSolicitacao.cs` — aggregate root da baixa
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinBaixaMotivo.cs` — value object com os 7 motivos válidos
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinBaixaStatus.cs` — enum dedicado de status (subconjunto + reenvio)

**Componentes novos (Application):**
- `Abstractions/Persistence/ISerasaPefinBaixaRepository.cs`
- `Features/Negativacao/Baixa/Commands/RequestBaixaCommand{Handler}.cs`
- `Features/Negativacao/Baixa/Commands/DecideBaixaCommand{Handler}.cs`
- `Features/Negativacao/Baixa/Commands/ResendBaixaCommand{Handler}.cs`
- `Features/Negativacao/Baixa/Commands/SendBaixaToSerasaCommand{Handler}.cs` (chamada interna pós-aprovação)
- `Features/Negativacao/Baixa/Queries/GetBaixaByIdQueryHandler.cs`
- `Features/Negativacao/Baixa/Queries/ListBaixasQueryHandler.cs`
- `Features/Negativacao/Baixa/Dtos/*` (request/response)
- `Features/Dashboard/Queries/GetMotivosBaixaQueryHandler.cs`
- `Features/Dashboard/Queries/GetNegativacoesVsBaixasQueryHandler.cs`

**Componentes novos (Infrastructure):**
- `Persistence/SqlServer/SerasaPefinBaixaRepository.cs`
- `Persistence/SqlServer/Mapping/SerasaPefinBaixaRowMapper.cs`

**Componentes modificados (Application/Infrastructure):**
- `Abstractions/Integrations/ISerasaPefinGateway.cs` — adicionar `DeleteByContractAsync`
- `Infrastructure/Integrations/SerasaPefin/SerasaPefinClient.cs` — método `DeleteByContractAsync` para o `DELETE`
- `Infrastructure/Integrations/SerasaPefin/SerasaPefinGateway.cs` — orquestração token + DELETE
- `Application/Features/SerasaPefin/Webhooks/SerasaWebhookHandler.cs` — branch para resolver baixa via `ISerasaPefinBaixaRepository` quando `WebhookEventType.Baixa` (hoje resolve via `ISerasaPefinRepository`)
- `Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQueryHandler.cs` — incluir baixas (campo `tipo` no DTO) para fila unificada de aprovações
- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` — sub-grupo `/baixa/...`
- `Infrastructure/DependencyInjection.cs` — registrar novo repository e handlers

**Componentes novos (Migrations SQL):**
- `db/011_serasa_pefin_baixas.sql` — tabela `SERASA_PEFIN_BAIXAS` + índices
- `db/012_views_baixa_dashboard.sql` — `vw_serasa_pefin_baixa_motivos`, `vw_serasa_pefin_negativacao_baixa_mensal`

**Componentes novos (Frontend):**
- `src/shared/types/baixa.ts` — tipos TypeScript
- `src/shared/services/baixa.ts` — wrapper de API
- `src/shared/hooks/useBaixaDecisionFlow.ts`
- `src/app/providers/BaixaDecisionProvider.tsx`
- `src/pages/main/dashboard/components/MotivosBaixaChart.tsx`
- `src/pages/main/dashboard/components/NegativacoesVsBaixasChart.tsx`

**Componentes modificados (Frontend):**
- `src/shared/ui/negativacao/NegativacaoDebtsModal.tsx` — prop `modo: "negativacao" | "baixa"`
- `src/shared/ui/negativacao/NegativacaoConfirmModal.tsx` — campo `motivoBaixa` quando `modo === "baixa"`
- `src/pages/main/DashboardPage.tsx` — montar os dois novos cards
- `src/pages/main/dashboard/api.ts` — novos fetchers

### Fluxo de Dados

1. **Solicitação**: Frontend (modo baixa) → `POST /negativacao/baixa/solicitacoes` → `RequestBaixaCommandHandler` valida senha+elegibilidade+duplicidade → persiste 1 `SerasaPefinBaixaSolicitacao` por parcela com `Status = AguardandoAprovacao` → cria `Ocorrencia` + dispara `NotificationDispatcher` para aprovadores.
2. **Decisão**: `POST /negativacao/baixa/solicitacoes/{id}/decisao` → `DecideBaixaCommandHandler` valida aprovador+senha → transição para `Aprovada` → invoca `SendBaixaToSerasaCommandHandler`.
3. **Envio Serasa**: `SendBaixaToSerasaCommandHandler` chama `ISerasaPefinGateway.DeleteByContractAsync` → recebe `transactionId` → aggregate transita para `BaixaAguardandoRetorno` (`MarcarBaixaAguardandoRetorno(transactionId)`).
4. **Webhook**: `SerasaWebhookHandler` (existente) já recebe `/webhooks/baixa/sucesso|erro`; quando `WebhookEventType.Baixa`, resolve via `ISerasaPefinBaixaRepository` e aplica `AplicarWebhookSucesso`/`AplicarWebhookErro` → notifica solicitante (in-app).
5. **Reenvio**: `POST /negativacao/baixa/solicitacoes/{id}/reenvio` → `ResendBaixaCommandHandler` valida `Status == BaixadoErro` e `Tentativas < 3` → incrementa `Tentativas` → reusa `SendBaixaToSerasaCommandHandler`.
6. **Dashboard**: Frontend → `GET /inadimplencia/dashboard/baixa/motivos` e `.../baixa/comparativo-mensal` → handlers leem das views.

## Design de Implementação

### Interfaces Principais

```csharp
// Gateway: adicionar método DELETE
public interface ISerasaPefinGateway
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
    Task<SerasaInclusionResponse> PostMainDebtAsync(object payload, CancellationToken ct = default);
    Task<SerasaInclusionResponse> PostGuarantorAsync(object payload, CancellationToken ct = default);

    Task<SerasaBaixaResponse> DeleteByContractAsync(
        SerasaBaixaRequest request,
        CancellationToken ct = default);
}

public sealed record SerasaBaixaRequest(
    string CreditorDocument,
    string DebtorDocument,
    string ContractNumber,
    int Reason);

public sealed record SerasaBaixaResponse(string TransactionId);
```

```csharp
// Repository dedicado
public interface ISerasaPefinBaixaRepository
{
    Task<Guid> AddAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken ct);
    Task AddManyAsync(IReadOnlyCollection<SerasaPefinBaixaSolicitacao> baixas, CancellationToken ct);
    Task UpdateAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken ct);
    Task<SerasaPefinBaixaSolicitacao?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<SerasaPefinBaixaSolicitacao?> GetByTransactionIdAsync(string transactionId, CancellationToken ct);
    Task<bool> ExistsActiveAsync(int numVenda, string contractNumber, int? numeroParcela, CancellationToken ct);
    Task<IReadOnlyList<SerasaPefinBaixaSolicitacao>> ListByStatusAsync(
        SerasaPefinBaixaStatus? status, int? numVenda, string? solicitante, int take, int skip, CancellationToken ct);
}
```

### Modelos de Dados

**Aggregate `SerasaPefinBaixaSolicitacao` (Domain):**

```csharp
public sealed class SerasaPefinBaixaSolicitacao
{
    public Guid Id { get; private set; }
    public Guid IdSolicitacaoNegativacao { get; private set; } // FK para SERASA_PEFIN_SOLICITACOES
    public int NumVendaFk { get; private set; }
    public int? NumeroParcela { get; private set; }
    public string ContractNumber { get; private set; }
    public string DocumentoDevedor { get; private set; }
    public string DocumentoCredor { get; private set; }
    public SerasaPefinBaixaMotivo Motivo { get; private set; } // VO
    public SerasaPefinBaixaStatus Status { get; private set; }
    public string SolicitanteUsername { get; private set; }
    public string? AprovadorUsername { get; private set; }
    public DateTime? DtAprovacao { get; private set; }
    public string? Justificativa { get; private set; }
    public string? TransactionId { get; private set; }
    public string? WebhookPayload { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? ErrorStatusCode { get; private set; }
    public int Tentativas { get; private set; }      // 1..3
    public DateTime DtCriacao { get; private set; }
    public DateTime DtAtualizacao { get; private set; }

    // Factory + transições: CriarParaAprovacao, MarcarAprovada, MarcarRejeitada,
    // MarcarPendenteEnvio, MarcarBaixaAguardandoRetorno, AplicarWebhookSucesso,
    // AplicarWebhookErro, MarcarFalhaEnvio, RegistrarTentativaReenvio.
}
```

**Tabela `dbo.SERASA_PEFIN_BAIXAS` (migration `011_serasa_pefin_baixas.sql`):**

| Coluna | Tipo | Notas |
|---|---|---|
| `ID` | `UNIQUEIDENTIFIER` PK | |
| `ID_SOLICITACAO_NEGATIVACAO` | `UNIQUEIDENTIFIER` FK | Aponta para `SERASA_PEFIN_SOLICITACOES.ID` |
| `NUM_VENDA_FK` | `INT` | |
| `NUMERO_PARCELA` | `INT NULL` | |
| `CONTRACT_NUMBER` | `NVARCHAR(20)` | |
| `DOCUMENTO_DEVEDOR` | `NVARCHAR(15)` | |
| `DOCUMENTO_CREDOR` | `NVARCHAR(14)` | |
| `MOTIVO` | `TINYINT` | Códigos 1,2,3,4,19,43,45 (validado em CHECK) |
| `STATUS` | `NVARCHAR(40)` | |
| `SOLICITANTE_USERNAME` | `NVARCHAR(100)` | |
| `APROVADOR_USERNAME` | `NVARCHAR(100) NULL` | |
| `DT_APROVACAO` | `DATETIME2 NULL` | |
| `JUSTIFICATIVA` | `NVARCHAR(500) NULL` | |
| `TRANSACTION_ID` | `NVARCHAR(100) NULL` | UUID Serasa |
| `WEBHOOK_PAYLOAD` | `NVARCHAR(MAX) NULL` | |
| `ERROR_MESSAGE` | `NVARCHAR(MAX) NULL` | |
| `ERROR_STATUS_CODE` | `INT NULL` | |
| `TENTATIVAS` | `TINYINT NOT NULL DEFAULT 1` | |
| `DT_CRIACAO` | `DATETIME2` | |
| `DT_ATUALIZACAO` | `DATETIME2` | |

**Índice único filtrado** para impedir baixa duplicada ativa por parcela:
`UX_SERASA_PEFIN_BAIXAS_ATIVA (NUM_VENDA_FK, CONTRACT_NUMBER, NUMERO_PARCELA) WHERE STATUS IN ('AGUARDANDO_APROVACAO','APROVADA','PENDENTE_ENVIO','BAIXA_ENVIADA','BAIXA_AGUARDANDO_RETORNO')`.

**Views (`012_views_baixa_dashboard.sql`):**
- `vw_serasa_pefin_baixa_motivos` → `MOTIVO, DESCRICAO, QTD, PERCENTUAL` (últimos 12 meses, `STATUS = 'BAIXADO_SUCESSO'`).
- `vw_serasa_pefin_negativacao_baixa_mensal` → `ANO_MES (YYYY-MM), QTD_NEGATIVACOES, QTD_BAIXAS` (UNION agregando `SERASA_PEFIN_SOLICITACOES` por `NEGATIVADO_SUCESSO` e `SERASA_PEFIN_BAIXAS` por `BAIXADO_SUCESSO`, últimos 12 meses).

### Endpoints de API

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/negativacao/baixa/solicitacoes` | Cria solicitações de baixa (N parcelas) — body inclui `motivoBaixa` |
| `GET` | `/negativacao/baixa/solicitacoes/{id}` | Detalhe da solicitação de baixa |
| `GET` | `/negativacao/baixa/solicitacoes?status=&numVenda=&take=&skip=` | Listagem com filtros |
| `POST` | `/negativacao/baixa/solicitacoes/{id}/decisao` | Aprovar/rejeitar (mesmo contrato `DecideNegativacaoRequest`) |
| `POST` | `/negativacao/baixa/solicitacoes/{id}/reenvio` | Reenviar baixa em `BAIXADO_ERRO` (limite 3) |
| `GET` | `/inadimplencia/dashboard/baixa/motivos?meses=12` | Dados do gráfico de motivos |
| `GET` | `/inadimplencia/dashboard/baixa/comparativo-mensal?meses=12` | Dados do gráfico misto |

Todas as rotas devem ser mapeadas também sob `/inadimplencia/...` para compatibilidade com o proxy Sophos, seguindo o padrão de `NegativacaoFluxoEndpoints.MapNegativacaoGroup`.

**Body `POST /negativacao/baixa/solicitacoes`:**
```json
{
  "numVenda": 295,
  "parcelaIds": [1, 2, 3],
  "motivoBaixa": 3,
  "senhaTransacao": "***",
  "justificativa": "Cliente quitou todas as parcelas selecionadas"
}
```

## Pontos de Integração

**Serasa PEFIN — DELETE por contrato** (ver `documentos/documentacao-serasa-pefin-v8.md:537-578`):
- Endpoint Homologação: `DELETE https://api.serasa.dev/collection/debt/contract`
- Endpoint Produção: `DELETE https://api.serasa.com.br/collection/debt/contract`
- Headers: `Authorization: Bearer`, `creditor-document`, `debtor-document`, `contract-number`, `reason`, `type: PEFIN`
- Body: vazio. Retorno: `200 OK` com `{ "transactionId": "<uuid>" }`.
- Autenticação reusa `SerasaPefinClient.GetTokenAsync` + cache existente (`SerasaPefinTokenCache`).
- Retry em `401`: 1 retentativa (mesmo padrão atual).
- Timeout: configurado em `SerasaPefinOptions.TimeoutSeconds` (default 10s).
- Mascaramento: documento devedor e credor mascarados em logs.

**Webhook Serasa**: já implementado em `/webhooks/baixa/sucesso|erro`. Ajuste: `SerasaWebhookHandler.HandleAsync` deve, quando `eventType == WebhookEventType.Baixa`, primeiro tentar resolver via `ISerasaPefinBaixaRepository.GetByTransactionIdAsync` (novo). Idempotência por `uuid` continua intacta.

## Abordagem de Testes

### Testes Unitários (Domain.Tests)
- `SerasaPefinBaixaSolicitacaoTests` — transições válidas/inválidas (factory rejeita motivo fora da whitelist; `RegistrarTentativaReenvio` falha em `Tentativas >= 3`; webhook só aplica em `BaixaAguardandoRetorno`).
- `SerasaPefinBaixaMotivoTests` — only 1,2,3,4,19,43,45 aceitos; mapeamento de descrição.

### Testes Unitários (Application.Tests)
- `RequestBaixaCommandHandlerTests` — caminhos: senha inválida/bloqueada/não cadastrada; parcela não negativada; baixa duplicada ativa; sucesso cria N agregados + ocorrência + notifica aprovadores. Mock de `ISerasaPefinBaixaRepository`, `ISenhaTransacaoValidator`, `IAprovadoresPolicy`, `INotificationDispatcher`.
- `DecideBaixaCommandHandlerTests` — aprovador inválido; senha; estado inválido; aprovação dispara envio; rejeição registra justificativa.
- `ResendBaixaCommandHandlerTests` — apenas em `BAIXADO_ERRO`; limite de 3 tentativas; sucesso incrementa contador e gera novo `transactionId`.
- `SerasaWebhookHandlerTests` — adicionar testes para baixa no novo repository (sucesso, erro, idempotência por uuid já existente).

### Testes de Integração (Infrastructure.Tests)
- `SerasaPefinBaixaRepositoryTests` — testes contra LocalDB/SQL com migration aplicada: insert+read, índice único filtrado rejeita duplicata ativa, update transacional.
- `SerasaPefinClientDeleteTests` — `HttpMessageHandler` mock validando headers obrigatórios, status `200`, parsing de `transactionId`, retry em `401`.

### Testes E2E (Api.Tests + Playwright MCP)
- Reuso de `FluxoNegativacaoFixture` para criar dados base e estender:
  - `FluxoBaixaE2ETests` cobrindo: solicitar baixa → aprovar → mock Serasa retorna `transactionId` → simular webhook sucesso → estado final `BAIXADO_SUCESSO` e parcela elegível para nova negativação.
- Frontend (Playwright MCP): abrir modal em modo baixa, selecionar parcelas negativadas, escolher motivo, confirmar; validar toast e card no dashboard.

## Sequenciamento de Desenvolvimento

1. **Domain + migrations** — aggregate, VO, status enum e tabela `011_*.sql` (sem código consumidor ainda).
2. **Repository SQL** + testes de integração.
3. **Gateway DELETE** — extensão do client + gateway + testes unitários do client.
4. **Commands de baixa** (`Request`, `Send`, `Decide`, `Resend`) + testes unitários, **sem endpoints ainda**.
5. **Webhook handler** — adaptar para resolver baixa via novo repo (com idempotência preservada).
6. **Endpoints HTTP** + Api.Tests cobrindo erros 400/401/404/409 e caminho feliz.
7. **Views SQL + Queries de dashboard** (`012_*.sql` + 2 handlers).
8. **Frontend — modal e fluxo de decisão** (prop `modo`, combo motivo, provider, hook).
9. **Frontend — dashboard charts** (dois cards usando `BarChart` e composição `ChartContainer + BarPlot + LinePlot`).
10. **E2E + ajuste do `NegativacaoConfirmModal`** + diferenciação visual de fila de aprovações.

### Dependências Técnicas
- Migrations `011_*.sql` e `012_*.sql` aplicadas em UAT antes de qualquer deploy do backend.
- Aprovadores configurados em `appsettings` (lista compartilhada com fluxo de negativação).
- `SerasaPefinOptions` deve cobrir URL base de coleção (já existente — basta confirmar que `CollectionBaseUrl` termina em `/collection`).

## Monitoramento e Observabilidade

- **Métricas Prometheus** (extensão do `Metrics` existente):
  - `serasa_pefin_baixa_solicitacao_total{result="created|rejected|approved"}`
  - `serasa_pefin_baixa_envio_total{result="success|error"}`
  - `serasa_pefin_baixa_webhook_total{resultado="sucesso|erro"}`
  - `serasa_pefin_baixa_envio_duration_seconds` (histograma)
- **Logs** (`ILogger<>` com escopo `SolicitacaoId`):
  - `Information` para transições e envio bem-sucedido (sem documentos crus).
  - `Warning` para senha inválida, parcela não elegível, duplicata.
  - `Error` para falha HTTP no gateway (com `statusCode` e body mascarado).
- **Dashboards Grafana**: card de baixa por estado + latência de envio adicionado ao painel existente do Serasa PEFIN.

## Considerações Técnicas

### Decisões Principais

- **Aggregate dedicado** (Opção 2 do PRD) — separa contextos, evita poluir `SerasaPefinSolicitacaoCompleta` com campos opcionais e mantém invariants específicos da baixa (motivo obrigatório, limite de reenvio).
- **DELETE por `contract-number`** — `cadusKey` não é persistido por parcela hoje; usar contrato evita migration retroativa e segue o caminho recomendado pela documentação Serasa.
- **Views SQL agregadas** — encapsulam o pesado `GROUP BY` mensal e o `CASE WHEN MOTIVO IN (1,2,3,4,19,43,45)` em um único lugar, facilitam reuso e mantêm os handlers de query simples.
- **`@mui/x-charts` com composição (`ChartContainer + BarPlot + LinePlot`)** para o gráfico misto, conforme padrão recomendado pela própria MUI X v8 para combinar séries de tipos diferentes.

### Riscos Conhecidos

- **Inconsistência negativação ↔ baixa**: se a parcela ainda não está em `NEGATIVADO_SUCESSO` no momento do envio, a Serasa pode rejeitar — mitigado pelo `RequestBaixaCommandHandler` validando o estado antes de persistir e pelo webhook handler aplicando idempotência.
- **Webhook chega antes do retorno HTTP DELETE**: cenário improvável mas tratado por `SerasaWebhookHandler` (ApplyWebhookTransactionalAsync); o repository assegura que `Status` final só é gravado uma vez por UUID.
- **Reenvio gerando novo `transactionId` órfão**: o agregado guarda apenas o último; histórico de tentativas registrado em `Tentativas` (contador) e em log estruturado. Caso surja necessidade de auditoria detalhada por tentativa, evolução pode tabela-filha `SERASA_PEFIN_BAIXA_TENTATIVAS` (fora de escopo).
- **Volume das views**: filtro de 12 meses + índices sobre `STATUS`, `DT_ATUALIZACAO` mantém performance; se o painel evoluir para tempo real, considerar materialização (PRD declara fora de escopo).

### Conformidade com Padrões (`@.windsurf/rules/techspec-codebase.md` + `documentos/techspec-codebase.md`)

- ✅ Clean Architecture + CQRS preservada — Domain sem dependência de HTTP/SQL; Application orquestra; Infrastructure implementa portas.
- ✅ DDD: aggregate `SerasaPefinBaixaSolicitacao` com factory, invariants e transições explícitas.
- ✅ Event-Driven: webhook permanece com idempotência por uuid; envio Serasa pode evoluir para outbox (não exigido nesta entrega).
- ✅ SQL parametrizado (Dapper + parâmetros nomeados no novo repository).
- ✅ Mascaramento de documentos em logs reusa `SensitiveDataMaskingMiddleware` e helpers já existentes.
- ✅ Docker multi-stage / non-root inalterados.
- ✅ Endpoints sob `/negativacao/...` e `/inadimplencia/negativacao/...` (proxy Sophos).

### Arquivos Relevantes e Dependentes

- `documentos/documentacao-serasa-pefin-v8.md` — contrato externo
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacaoCompleta.cs` — espelho de padrões de aggregate
- `ApiInadimplencia.Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommandHandler.cs` — template do handler de solicitação
- `ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommandHandler.cs` — template do handler de decisão
- `ApiInadimplencia.Application/Features/SerasaPefin/Webhooks/SerasaWebhookHandler.cs` — ponto de extensão para baixa
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinClient.cs` — host do novo método DELETE
- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` — registrar novo sub-grupo `/baixa/...`
- `db/009_serasa_pefin_parcela.sql` — padrão de migration idempotente
- `c:/fluig/.../shared/ui/negativacao/NegativacaoDebtsModal.tsx` — recebe nova prop `modo`
- `c:/fluig/.../shared/ui/negativacao/NegativacaoConfirmModal.tsx` — combo motivo
- `c:/fluig/.../pages/main/DashboardPage.tsx` — novos cards

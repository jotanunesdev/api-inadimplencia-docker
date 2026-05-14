# Tech Spec: Migração do Módulo Inadimplencia para .NET 8

## Resumo Executivo

Esta tech spec define a abordagem técnica para migrar o módulo `inadimplencia` de Node.js/Express para C#/.NET 8 seguindo Clean Architecture + CQRS. A solução utiliza MassTransit com RabbitMQ para event-driven architecture e outbox pattern, EF Core para operações de escrita complexas, Dapper para queries de leitura otimizadas, SSE nativo do ASP.NET Core para notificações, xUnit para testes, e OpenTelemetry para observabilidade. A migração será dividida em fases sequenciais começando por queries simples, evoluindo para commands com eventos, e finalizando com integrações externas críticas.

## Arquitetura do Sistema

### Visão Geral dos Componentes

**ApiInadimplencia.Domain** - Camada de domínio puro sem dependências externas
- `Common/ValueObjects.cs` - Value objects: NumVenda, CpfCnpj, HexColor, ProtocolNumber (já implementado)
- `Events/InadimplenciaEvents.cs` - Eventos de domínio: ResponsavelAtribuidoEvent, OcorrenciaRegistradaEvent, SerasaPefinWebhookRecebidoEvent (já implementado)
- `Kanban/KanbanStatus.cs` - Enum/VO para status normalizados (todo, inProgress, done)
- `Notifications/NotificationTypes.cs` - Tipos de notificação (VENDA_ATRIBUIDA, VENDA_ATRASADA)
- `SerasaPefin/SerasaPefinEnums.cs` - Status e tipos de registro Serasa (já implementado)
- `Users/UserProfile.cs` - Perfis de usuário (admin, operador) (já implementado)
- `Inadimplencias/` - Entidades/aggregates para carteira inadimplente
- `Ocorrencias/` - Entidade Ocorrencia com validações de domínio
- `Atendimentos/` - Aggregate root Atendimento com geração de protocolo
- `Responsaveis/` - Entidade Responsavel com regras de atribuição
- `Fiadores/` - Read-only DTOs para fiadores

**ApiInadimplencia.Application** - Camada de aplicação com CQRS
- `Abstractions/Cqrs/ICommand.cs`, `IQuery.cs` - Interfaces base CQRS (já implementado)
- `Abstractions/Persistence/ILegacySqlExecutor.cs` - Porta para SQL legado durante migração (já implementado)
- `Features/Legacy/LegacySqlOperations.cs` - Adapter temporário para SQL legado (já implementado)
- `Features/{Feature}/Commands/` - Command handlers para escrita
- `Features/{Feature}/Queries/` - Query handlers para leitura
- `Features/{Feature}/Dtos/` - DTOs de entrada/saída
- `Abstractions/Integrations/` - Portas para integrações externas (IFluigDatasetGateway, IRmReportGateway, ISerasaPefinGateway)

**ApiInadimplencia.Infrastructure** - Adapters de infraestrutura
- `Persistence/SqlServer/` - Implementações de EF Core (DbContext, repositories) e Dapper (query executors)
- `Integrations/Fluig/` - HttpClient tipado para datasets Fluig
- `Integrations/Rm/` - HttpClient tipado para relatórios RM
- `Integrations/SerasaPefin/` - HttpClient tipado com cache de token para Serasa PEFIN
- `Messaging/MassTransit/` - Configuração MassTransit com RabbitMQ e outbox
- `Notifications/SseHub.cs` - Hub SSE nativo para notificações em tempo real
- `BackgroundServices/OverdueScanner.cs` - BackgroundService com flag in-memory para scanner de vencidos

**api-inadimplencia.Api** - Camada de apresentação
- `Endpoints/InadimplenciaEndpoints.cs` - Minimal APIs mapeando contrato REST (já parcialmente implementado)
- `Middleware/` - Middleware para mascaramento de dados sensíveis
- `Program.cs` - Composition root com DI, MassTransit, OpenTelemetry (já implementado parcialmente)

Fluxo de dados: Frontend → Minimal APIs → Command/Query Handlers → Domain/Infrastructure → SQL Server/Integrações Externas → Eventos MassTransit → Handlers de Eventos → Notificações SSE/Webhooks

## Design de Implementação

### Interfaces Principais

```csharp
// Command base (já existe em Application/Abstractions/Cqrs)
public interface ICommand<TResponse> { }

// Query base (já existe em Application/Abstractions/Cqrs)
public interface IQuery<TResponse> { }

// Porta para EF Core repositories
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
}

// Porta para Dapper queries otimizadas
public interface IReadModelQuery
{
    Task<IEnumerable<T>> ExecuteAsync<T>(string sql, object parameters, CancellationToken ct = default);
    Task<T?> ExecuteSingleAsync<T>(string sql, object parameters, CancellationToken ct = default);
}

// Porta para integração Fluig
public interface IFluigDatasetGateway
{
    Task<string> GetDatasetAsync(string datasetName, Dictionary<string, string> parameters, CancellationToken ct = default);
}

// Porta para integração RM
public interface IRmReportGateway
{
    Task<string> GenerateReportAsync(string reportId, Dictionary<string, string> parameters, CancellationToken ct = default);
}

// Porta para integração Serasa PEFIN
public interface ISerasaPefinGateway
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
    Task<SerasaPefinPreview> GetPreviewAsync(int numVenda, CancellationToken ct = default);
    Task<string> RequestNegativacaoAsync(SerasaPefinRequest request, CancellationToken ct = default);
}

// Event bus via MassTransit
public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
```

### Modelos de Dados

**Entidades de Domínio Principais:**

```csharp
// Ocorrencia - Aggregate root
public class Ocorrencia
{
    public Guid Id { get; private set; }
    public int NumVendaFk { get; private set; }
    public string NomeUsuarioFk { get; private set; }
    public string Descricao { get; private set; }
    public string StatusOcorrencia { get; private set; }
    public DateTime DtOcorrencia { get; private set; }
    public string HoraOcorrencia { get; private set; }
    public string? ProximaAcao { get; private set; }
    public string? Protocolo { get; private set; }

    // Método de fábrica com validações
    public static Ocorrencia Criar(int numVendaFk, string nomeUsuarioFk, string descricao, 
        string statusOcorrencia, DateTime dtOcorrencia, string horaOcorrencia, 
        string? proximaAcao = null, string? protocolo = null);
}

// Atendimento - Aggregate root com geração de protocolo
public class Atendimento
{
    public Guid Id { get; private set; }
    public string Protocolo { get; private set; }
    public string Cpf { get; private set; }
    public int NumVendaFk { get; private set; }
    public string DadosVendaJson { get; private set; }
    public DateTime CriadoEm { get; private set; }

    // Gera protocolo AAAAMMDD##### transacionalmente
    public static async Task<Atendimento> CriarAsync(IProtocoloGenerator generator, 
        string cpf, int numVenda, object dadosVenda, CancellationToken ct);
}

// Responsavel - Entidade com eventos
public class Responsavel
{
    public int NumVendaFk { get; private set; }
    public string Username { get; private set; }
    public DateTime AtribuidoEm { get; private set; }
    public string AtribuidoPor { get; private set; }

    // Dispara evento de domínio ao mudar responsável
    public Responsavel Attribuir(string novoUsername, string adminUserCode);
}
```

**DTOs de Requisição/Resposta:**

```csharp
// CreateOcorrenciaCommand
public record CreateOcorrenciaCommand(
    int NumVendaFk,
    string NomeUsuarioFk,
    string Descricao,
    string StatusOcorrencia,
    DateTime DtOcorrencia,
    string HoraOcorrencia,
    string? ProximaAcao = null,
    string? Protocolo = null) : ICommand<Guid>;

// ListInadimplenciasQuery
public record ListInadimplenciasQuery(
    string? Cpf = null,
    int? NumVenda = null,
    string? Responsavel = null,
    string? Cliente = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<InadimplenciaDto>>;
```

### Endpoints de API

Endpoints mapeados em `InadimplenciaEndpoints.cs` seguindo contrato existente:

**Carteira Inadimplente:**
- `GET /inadimplencia` - Lista todas inadimplências com última PROXIMA_ACAO
- `GET /inadimplencia/cpf/{cpf}` - Consulta por CPF/CNPJ (normalizado dígitos)
- `GET /inadimplencia/num-venda/{numVenda}` - Consulta por NUM_VENDA
- `GET /inadimplencia/responsavel/{nome}` - Consulta por responsável com cor
- `GET /inadimplencia/cliente/{nomeCliente}` - Consulta por nome (LIKE)

**Ocorrências:**
- `GET /ocorrencias` - Lista ocorrências
- `POST /ocorrencias` - Cria ocorrência com FK guard
- `GET /ocorrencias/{id}` - Detalhe por ID
- `PUT /ocorrencias/{id}` - Atualiza ocorrência
- `DELETE /ocorrencias/{id}` - Exclui ocorrência
- `GET /ocorrencias/num-venda/{numVenda}` - Lista por venda
- `GET /ocorrencias/protocolo/{protocolo}` - Lista por protocolo

**Atendimentos:**
- `POST /atendimentos` - Cria atendimento com geração de protocolo transacional
- `GET /atendimentos/cpf/{cpf}` - Consulta por CPF
- `GET /atendimentos/num-venda/{numVenda}` - Consulta por venda
- `GET /atendimentos/protocolo/{protocolo}` - Consulta por protocolo
- `GET /atendimentos/cliente/{nomeCliente}` - Consulta por nome

**Usuários:**
- `GET /usuarios` - Lista usuários
- `POST /usuarios` - Upsert idempotente (200 se existe, 201 se criado)
- `GET /usuarios/{nome}` - Detalhe por nome
- `PUT /usuarios/{nome}` - Atualiza usuário
- `DELETE /usuarios/{nome}` - Exclui usuário

**Responsáveis:**
- `GET /responsaveis` - Lista responsáveis
- `POST /responsaveis` - Atribui responsável (valida admin, cria notificação)
- `GET /responsaveis/{numVenda}` - Detalhe por venda
- `PUT /responsaveis/{numVenda}` - Atualiza responsável
- `DELETE /responsaveis/{numVenda}` - Remove responsável (remove notificações)

**Kanban:**
- `GET /kanban-status` - Lista status
- `POST /kanban-status` - Upsert com normalização de aliases

**Fiadores:**
- `GET /fiadores/num-venda/{numVenda}` - Lista fiadores por venda
- `GET /fiadores/cpf/{cpf}` - Lista fiadores por CPF

**Dashboard:**
- `GET /dashboard/kpis` - KPIs principais
- `GET /dashboard/{metric}` - Métricas específicas (vendas-por-responsavel, inadimplencia-por-empreendimento, etc.)

**Notificações:**
- `GET /notifications?username=&page=&pageSize=&lida=` - Listagem paginada
- `GET /notifications/stream?username=` - SSE stream nativo
- `PUT /notifications/{id}/read?username=` - Marca como lida
- `PUT /notifications/read-all?username=` - Marca todas como lidas
- `DELETE /notifications/{id}?username=` - Exclusão lógica (exige lida)

**Relatórios:**
- `GET /relatorios/ficha-financeira?numVenda=&codColigada=&reportColigada=&reportId=` - Gera via Fluig/RM

**Serasa PEFIN:**
- `GET /serasa-pefin/vendas/{numVenda}/preview` - Preview de negativação
- `POST /serasa-pefin/vendas/{numVenda}/negativacoes` - Solicita negativação (outbox)
- `GET /serasa-pefin/vendas/{numVenda}/negativacoes` - Histórico por venda
- `GET /serasa-pefin/acompanhamento/{transactionId}` - Acompanhamento
- `GET /serasa-pefin/negativacoes/{id}` - Detalhe
- `POST /serasa-pefin/webhooks/{tipo}/{resultado}` - Webhook idempotente

## Pontos de Integração

**SQL Server DW/Operacional:**
- `DW.fat_analise_inadimplencia_v4` - Fonte principal para carteira e dashboard
- `dbo.OCORRENCIAS` - Persistência de ocorrências (EF Core)
- `dbo.ATENDIMENTOS` - Persistência de atendimentos (EF Core com transação SERIALIZABLE)
- `dbo.USUARIO` - Gestão de usuários (EF Core)
- `dbo.VENDA_RESPONSAVEL` - Atribuição de responsáveis (EF Core)
- `dbo.KANBAN_STATUS` - Status kanban (EF Core)
- `dbo.INAD_NOTIFICACOES` - Notificações persistidas (EF Core)
- `dbo.SERASA_PEFIN_SOLICITACOES` - Solicitações Serasa (EF Core com outbox)
- `dbo.SERASA_PEFIN_WEBHOOKS` - Webhooks Serasa (EF Core)
- `DW.vw_fiadores_por_venda` - Fiadores (Dapper read-only)

**Fluig (TOTVS):**
- Endpoint: `j_security_check` para autenticação
- Endpoint: `dataset-handle/search` para datasets
- Datasets: `ds_paramsRel`, `dsIntegraFacilRM`, `ds_paiFilho_controleDeAcessoRMreportsFluig`
- Autenticação: Cookies Fluig (mascarados em logs)
- Tratamento de erros: Retry com Polly, timeout 10s, fallback para dataset secundário

**TOTVS RM:**
- Integração via Fluig datasets
- Operação: `OPC=6` para gerar PDF
- Retorno: URL do PDF gerado
- Tratamento de erros: Retry, timeout 30s, mascaramento de XML/parâmetros

**Serasa Experian PEFIN:**
- Autenticação: Basic Auth → Bearer Token (cache com buffer 60s)
- Endpoints: API de negativação principal/garantidor
- Webhooks: Inclusão, avalista, baixa (sucesso/erro)
- Idempotência: UUID em webhooks
- Tratamento de erros: Retry 1x em 401, timeout 10s
- Mascaramento: Documentos, tokens, payloads em logs/respostas
- Outbox: MassTransit garante entrega após commit DB

**RabbitMQ:**
- Uso: MassTransit para event bus e outbox
- Exchanges: `inadimplencia.events`, `serasa.webhooks`
- Queues: Por consumer type (notifications, webhook-processor)
- Tratamento de erros: Retry com exponential backoff, dead-letter queue após falhas

**Frontend jnc_inadimplencia:**
- Contrato REST: 100% compatível com Node.js existente
- SSE: Stream nativo com snapshot inicial, heartbeat 15s, eventos
- CORS: Configurado para origins permitidos
- Formatos: camelCase e UPPER_SNAKE aceitos em payloads

## Abordagem de Testes

### Testes Unidade

**Componentes principais a testar:**
- Value objects (NumVenda, CpfCnpj, HexColor, ProtocolNumber) - validações de formato
- Entidades de domínio (Ocorrencia, Atendimento, Responsavel) - regras de negócio
- Command handlers - lógica de coordenação, validações, eventos
- Query handlers - SQL parametrizado, mapeamento de resultados
- Event handlers - processamento de eventos de domínio

**Requisitos de mock:**
- Repositories EF Core (usando InMemory provider)
- Dapper executors (mock de ILegacySqlExecutor)
- HttpClient para integrações externas (usando IHttpClientFactory com mock handler)
- Event bus MassTransit (usando InMemory test harness)
- SSE hub (mock de IResponseWriter)

**Cenários de teste críticos:**
- Geração de protocolo transacional com isolamento SERIALIZABLE
- Validação de FK de venda antes de inserir ocorrência
- Upsert idempotente de usuários por USER_CODE/NOME
- Atribuição de responsável com validação de admin e criação de notificação
- Normalização de status kanban com aliases PT-BR/EN
- Dedupe de notificações por TIPO|USUARIO|NUM_VENDA|PROXIMA_ACAO_DIA
- Mascaramento de dados sensíveis em logs/respostas

### Testes de Integração

**Componentes a testar juntos:**
- Command handler + EF Core repository + Domain events + MassTransit
- Query handler + Dapper executor + SQL Server (container test)
- SSE hub + Notification repository + BackgroundService scanner
- Serasa gateway + Outbox + RabbitMQ (container test)

**Requisitos de dados de teste:**
- SQL Server local ou container com schema de teste
- RabbitMQ container para testes de mensageria
- Dados de seed para usuários, vendas, ocorrências
- Mock de integrações externas (Fluig, RM, Serasa) usando WireMock

### Testes de E2E

**Testar frontend junto com backend usando Playwright MCP:**
- Fluxo completo: consulta carteira → registro ocorrência → atribuição responsável → notificação SSE
- Validação de contrato REST via OpenAPI/Swagger
- Teste de SSE connection e eventos em tempo real
- Teste de webhook Serasa com idempotência

## Sequenciamento de Desenvolvimento

### Ordem de Construção

**Fase 1: Infraestrutura Base e Queries Simples (2-3 semanas)**
1. Configurar EF Core com DbContext para tabelas de escrita (OCORRENCIAS, ATENDIMENTOS, USUARIO, VENDA_RESPONSAVEL, KANBAN_STATUS, INAD_NOTIFICACOES, SERASA_PEFIN_SOLICITACOES, SERASA_PEFIN_WEBHOOKS)
2. Implementar repositories EF Core básicos
3. Migrar queries de leitura existentes para Dapper handlers (carteira, usuários, responsáveis, kanban, fiadores, Serasa queries)
4. Implementar DTOs e query handlers para dashboard KPIs
5. Configurar xUnit e FluentAssertions
6. Escrever testes unitários para value objects e query handlers

**Fase 2: Commands com Regras de Domínio (3-4 semanas)**
1. Implementar command handlers para Ocorrencias (create/update/delete) com FK guard
2. Implementar command handler para Atendimentos com geração de protocolo transacional (SERIALIZABLE, UPDLOCK/HOLDLOCK)
3. Implementar command handlers para Usuarios (upsert idempotente, validação de perfil/cor)
4. Implementar command handlers para Responsaveis (validação admin, MERGE, eventos de domínio)
5. Implementar command handlers para Kanban (normalização de aliases)
6. Escrever testes unitários e integração para commands

**Fase 3: Event-Driven com MassTransit (2-3 semanas)**
1. Configurar MassTransit com RabbitMQ
2. Implementar in-memory outbox para commands críticos
3. Implementar event bus interface e handlers de eventos de domínio
4. Implementar handler de ResponsavelAtribuidoEvent → cria notificação VENDA_ATRIBUIDA
5. Implementar handler de OcorrenciaRegistradaEvent → limpa notificações vencidas
6. Escrever testes de integração com RabbitMQ container

**Fase 4: Notificações SSE e Scanner (2 semanas)**
1. Implementar SSE hub nativo com snapshot inicial e heartbeat 15s
2. Implementar BackgroundService para scanner de vencidos com flag in-memory
3. Implementar commands para marcar notificações como lidas (uma/todas)
4. Implementar command para exclusão lógica de notificações (valida lida)
5. Integrar handler de VENDA_ATRIBUIDA event → broadcast SSE
6. Escrever testes de integração para SSE e scanner

**Fase 5: Integrações Externas (3-4 semanas)**
1. Implementar HttpClient tipado para Fluig datasets com Polly retry
2. Implementar HttpClient tipado para RM reports
3. Implementar HttpClient tipado para Serasa PEFIN com cache de token
4. Implementar command para relatórios ficha-financeira
5. Implementar command/queries para Serasa preview, negativação, histórico
6. Implementar webhook handler idempotente para Serasa (inclusão, avalista, baixa)
7. Implementar middleware de mascaramento de dados sensíveis
8. Escrever testes de integração com WireMock para externas

**Fase 6: Dashboard Completo e Observabilidade (2 semanas)**
1. Migrar todas as métricas de dashboard restantes para query handlers
2. Implementar whitelist/parsers para faixa, qtd, score (sem SQL livre)
3. Configurar OpenTelemetry para tracing e métricas
4. Configurar ILogger nativo com níveis apropriados
5. Adicionar health checks para SQL Server, RabbitMQ, integrações
6. Escrever testes E2E com Playwright para fluxos críticos

**Fase 7: Integração e Testes Finais (2 semanas)**
1. Validação completa de contrato REST com frontend
2. Testes de carga para endpoints críticos (dashboard, notificações)
3. Validação de Docker multi-stage e healthcheck
4. Documentação Swagger completa
5. Limpeza de código legado (LegacySqlOperations)
6. Code review e ajustes finais

### Dependências Técnicas

**Infraestrutura requerida:**
- SQL Server (DW/operacional) - já existe, não será substituído
- RabbitMQ - novo, necessário para MassTransit event bus
- Container Docker para desenvolvimento e testes

**Disponibilidade de serviço externo:**
- Fluig/TOTVS RM - já existe, credenciais documentadas
- Serasa Experian PEFIN - já existe, credenciais documentadas
- Ambiente de teste UAT para integrações - necessário validar disponibilidade

## Monitoramento e Observabilidade

**Métricas a expor (formato Prometheus via OpenTelemetry):**
- `http_requests_total` - Contagem de requisições HTTP por endpoint, método, status
- `http_request_duration_seconds` - Latência de requisições por endpoint
- `sql_queries_total` - Contagem de queries por tipo (read/write)
- `sql_query_duration_seconds` - Latência de queries SQL
- `mass_transit_messages_total` - Contagem de mensagens publicadas/consumidas
- `mass_transit_message_duration_seconds` - Latência de processamento de mensagens
- `sse_connections_active` - Conexões SSE ativas
- `background_service_scans_total` - Contagem de execuções do scanner de vencidos
- `serasa_requests_total` - Contagem de requisições Serasa por endpoint, status
- `serasa_token_cache_hits` - Hits no cache de token Serasa

**Logs principais e níveis de log:**
- Information: Início/fim de requests, publicação de eventos, execução de background services
- Warning: Retries de integrações externas, timeouts, falhas não críticas
- Error: Falhas de integrações externas após retries, erros de validação, exceções não tratadas
- Debug: Parâmetros de queries (mascarados), detalhes de processamento de eventos
- Critical: Falhas de conexão com SQL Server/RabbitMQ, erros de configuração

**Integração com dashboards Grafana existentes:**
- Exportar métricas Prometheus para endpoint `/metrics`
- Configurar datasource Prometheus no Grafana
- Criar dashboards para: performance de APIs, health de integrações, volume de notificações, latency de Serasa
- Alertas para: alta taxa de erros 5xx, falhas de integrações externas, conexões SSE anormais

## Considerações Técnicas

### Decisões Principais

**Clean Architecture + CQRS:**
- Justificativa: Separação clara de responsabilidades, facilita testes e evolução, alinhado com techspec-codebase.md
- Trade-offs: Curva de aprendizado inicial, boilerplate para handlers simples
- Alternativas rejeitadas: MVC tradicional (acoplamento alto), n-tier (viola Dependency Rule)

**MassTransit com RabbitMQ:**
- Justificativa: Outbox pattern integrado evita perda de mensagens, suporte a retry/dlq, ecossistema maduro
- Trade-offs: Complexidade adicional de infraestrutura (RabbitMQ), curva de aprendizado
- Alternativas rejeitadas: Event grid Azure (custo), Kafka (overkill), custom in-memory (sem persistência)

**EF Core para Write + Dapper para Read:**
- Justificativa: EF Core para transações complexas e change tracking, Dapper para performance em queries pesadas de dashboard
- Trade-offs: Duas tecnologias de acesso a dados para manter
- Alternativas rejeitadas: Apenas EF Core (performance em queries pesadas), apenas Dapper (sem change tracking)

**SSE Nativo ao invés de SignalR:**
- Justificativa: Simplicidade para scenario unidirecional (server→client), compatibilidade com frontend existente
- Trade-offs: Sem fallback automático para WebSockets, sem suporte nativo a binary
- Alternativas rejeitadas: SignalR (overkill para broadcast simples), polling (ineficiente)

**xUnit ao invés de MSTest:**
- Justificativa: Sintaxe mais moderna, melhor suporte a theory tests, amplamente adotado na comunidade .NET
- Trade-offs: Nenhuma significativa
- Alternativas rejeitadas: MSTest (sintaxe verbosa), NUnit (menos popular em .NET Core)

**OpenTelemetry para Observabilidade:**
- Justificativa: Padrão de indústria, vendor-neutral, suporte nativo no .NET 8, gratuito
- Trade-offs: Curva de aprendizado para configuração
- Alternativas rejeitadas: Application Insights (custo), Serilog (sem tracing distribuído nativo)

**Fases de Migração Sequenciais:**
- Justificativa: Reduz risco, permite validação incremental, facilita rollback
- Trade-offs: Tempo total mais longo, complexidade de gerenciar código legado temporário
- Alternativas rejeitadas: Big bang (alto risco), paralelo (complexidade de sincronização)

### Riscos Conhecidos

**Desafios potenciais:**
- Transação SERIALIZABLE para geração de protocolo pode causar contenção em alto volume
- Queries de dashboard podem saturar pool de SQL Server sem índices adequados
- SSE não escala horizontalmente sem Redis Pub/Sub (futuro)
- Integrações externas (Fluig, RM, Serasa) podem ter downtime não documentado
- Mascaramento de dados sensíveis pode quebrar debugging se excessivo
- Outbox pattern pode acumular mensagens se RabbitMQ ficar indisponível

**Abordagens de mitigação:**
- Protocolo: Monitorar lock waits, considerar sequence table alternativo
- Dashboard: Implementar cache de métricas, paginação estrita, limit 1000
- SSE: Documentar limitação de instância única, planejar Redis Pub/Sub futuro
- Integrações: Implementar circuit breaker com Polly, health checks, fallback
- Mascaramento: Configurar níveis de log (Debug mostra dados, Production não)
- Outbox: Monitorar tamanho da fila, alertas para acúmulo, retry com backoff

**Áreas precisando pesquisa:**
- Disponibilidade de ambiente UAT para integrações Fluig/RM/Serasa
- Documentação atualizada de APIs Fluig e Serasa PEFIN
- Performance baseline de queries dashboard no SQL Server atual
- Limites de rate limiting das APIs externas (Serasa especialmente)

### Conformidade com Padrões

**Regras de @.windsurf/rules/techspec-codebase.md aplicadas:**
- ✅ Clean Architecture + CQRS - Domain sem dependências de infraestrutura
- ✅ DDD aplicado onde há regra forte (protocolo, ocorrências, kanban, responsáveis, notificações, Serasa)
- ✅ Event-Driven para integrações e processamento assíncrono (scanner, webhooks, SSE)
- ✅ Domain não depende de ASP.NET, SQL Server, Dapper, EF, HTTP, Docker
- ✅ API chama commands/queries da Application, endpoints não acessam repositories diretamente
- ✅ Infrastructure implementa portas de Application para SQL Server, Fluig, RM, Serasa, SSE
- ✅ SQL sempre parametrizado (Dapper DynamicParameters, EF Core parameters)
- ✅ Dados sensíveis mascarados em logs/respostas (CPF/CNPJ, tokens, secrets, cookies, payloads)
- ✅ Docker multi-stage build e runtime non-root (já implementado)
- ✅ Contrato REST mantido compatível (catalogado em InadimplenciaRouteCatalog)

**Desvios com justificativa:**
- Nenhum desvio planejado em relação ao techspec-codebase.md

### Arquivos relevantes e dependentes

**Arquivos existentes a serem mantidos/evoluídos:**
- `api-inadimplencia.Api/Program.cs` - Composition root (adicionar MassTransit, OpenTelemetry)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` - Mapeamento de endpoints (substituir NotMigrated por handlers reais)
- `ApiInadimplencia.Domain/Common/ValueObjects.cs` - Value objects (já implementado, manter)
- `ApiInadimplencia.Domain/Events/InadimplenciaEvents.cs` - Eventos de domínio (já implementado, manter)
- `ApiInadimplencia.Application/Abstractions/Cqrs/` - Interfaces CQRS (já implementado, manter)
- `ApiInadimplencia.Application/Features/Routes/InadimplenciaRouteCatalog.cs` - Catálogo de rotas (atualizar status)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` - DI (adicionar EF Core, MassTransit, HttpClient)
- `Dockerfile` - Multi-stage build (já implementado, manter)
- `docker-compose.yml` - Adicionar RabbitMQ container

**Arquivos a serem criados:**
- `ApiInadimplencia.Domain/Inadimplencias/` - Entidades/aggregates
- `ApiInadimplencia.Domain/Ocorrencias/Ocorrencia.cs` - Entidade Ocorrencia
- `ApiInadimplencia.Domain/Atendimentos/Atendimento.cs` - Entidade Atendimento
- `ApiInadimplencia.Domain/Responsaveis/Responsavel.cs` - Entidade Responsavel
- `ApiInadimplencia.Application/Features/{Feature}/Commands/` - Command handlers
- `ApiInadimplencia.Application/Features/{Feature}/Queries/` - Query handlers
- `ApiInadimplencia.Application/Features/{Feature}/Dtos/` - DTOs
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/DbContext.cs` - EF Core DbContext
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Repositories/` - EF Core repositories
- `ApiInadimplencia.Infrastructure/Integrations/Fluig/` - HttpClient Fluig
- `ApiInadimplencia.Infrastructure/Integrations/Rm/` - HttpClient RM
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/` - HttpClient Serasa
- `ApiInadimplencia.Infrastructure/Messaging/MassTransit/` - Configuração MassTransit
- `ApiInadimplencia.Infrastructure/Notifications/SseHub.cs` - Hub SSE
- `ApiInadimplencia.Infrastructure/BackgroundServices/OverdueScanner.cs` - Scanner
- `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs` - Middleware mascaramento
- Test projects: `*.Tests.csproj` com xUnit + FluentAssertions

**Dependências de projeto:**
- `ApiInadimplencia.Domain` - Sem dependências externas além de BCL
- `ApiInadimplencia.Application` - Depende de Domain
- `ApiInadimplencia.Infrastructure` - Depende de Application + Domain, pacotes: EF Core, Dapper, MassTransit.RabbitMQ, Microsoft.Extensions.Http, Polly
- `api-inadimplencia.Api` - Depende de Application + Infrastructure, pacotes: ASP.NET Core, Swashbuckle, OpenTelemetry

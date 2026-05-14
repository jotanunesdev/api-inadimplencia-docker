# Tarefa 9.0: Implementar Middleware de Mascaramento e Observabilidade

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar middleware completo para mascaramento de dados sensíveis em logs e respostas, configurar OpenTelemetry completo para tracing e métricas, e configurar health checks para SQL Server, RabbitMQ e integrações externas.

<requirements>
- Implementar middleware de mascaramento para CPF/CNPJ, tokens, secrets, cookies Fluig, payloads Serasa
- Configurar OpenTelemetry para tracing distribuído
- Configurar OpenTelemetry para métricas Prometheus
- Configurar ILogger nativo com níveis apropriados
- Configurar health checks para SQL Server
- Configurar health checks para RabbitMQ
- Configurar health checks para integrações externas (Fluig, RM, Serasa)
- Expor métricas em endpoint /metrics
- Testes de unidade e integração
</requirements>

## Subtarefas

- [ ] 9.1 Implementar SensitiveDataMaskingMiddleware completo
- [ ] 9.2 Implementar mascaramento de CPF/CNPJ
- [ ] 9.3 Implementar mascaramento de tokens e secrets
- [ ] 9.4 Implementar mascaramento de cookies Fluig
- [ ] 9.5 Implementar mascaramento de payloads Serasa
- [ ] 9.6 Configurar OpenTelemetry para tracing
- [ ] 9.7 Configurar OpenTelemetry para métricas
- [ ] 9.8 Configurar ILogger com níveis apropriados
- [ ] 9.9 Configurar health check para SQL Server
- [ ] 9.10 Configurar health check para RabbitMQ
- [ ] 9.11 Configurar health check para integrações externas
- [ ] 9.12 Expor endpoint /metrics
- [ ] 9.13 Mapear endpoint /health
- [ ] 9.14 Escrever testes de unidade
- [ ] 9.15 Escrever testes de integração

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Monitoramento e Observabilidade**: Métricas a expor, Logs principais e níveis
- **Considerações Técnicas**: OpenTelemetry para Observabilidade
- **Pontos de Integração**: SQL Server, RabbitMQ, Fluig, RM, Serasa

**SensitiveDataMaskingMiddleware:**
- Atualizar `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs`
- Mascarar CPF/CNPJ: manter primeiros 3 e últimos 2 dígitos
- Mascarar tokens: mostrar apenas primeiros 8 caracteres
- Mascarar secrets: mostrar apenas "*****"
- Mascarar cookies Fluig: identificar padrões e mascarar valores
- Mascarar payloads Serasa: identificar documentos e mascarar
- Aplicar a logs (ILogger) e respostas HTTP
- Configurar níveis de log: Debug mostra dados, Production não

**OpenTelemetry Tracing:**
- Configurar em `api-inadimplencia.Api/Program.cs`
- Adicionar pacotes NuGet: `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`
- Configurar ASP.NET Core instrumentation
- Configurar EF Core instrumentation
- Configurar MassTransit instrumentation
- Exportar para console (dev) ou Jaeger/Zipkin (prod)

**OpenTelemetry Metrics:**
- Configurar em `api-inadimplencia.Api/Program.cs`
- Adicionar pacote NuGet: `OpenTelemetry.Exporter.Prometheus`
- Configurar métricas:
  - http_requests_total
  - http_request_duration_seconds
  - sql_queries_total
  - sql_query_duration_seconds
  - mass_transit_messages_total
  - mass_transit_message_duration_seconds
  - sse_connections_active
  - background_service_scans_total
  - serasa_requests_total
  - serasa_token_cache_hits
- Expor em endpoint `/metrics`

**ILogger Configuration:**
- Configurar em `api-inadimplencia.Api/Program.cs`
- Níveis:
  - Information: Início/fim de requests, publicação de eventos, execução de background services
  - Warning: Retries de integrações externas, timeouts, falhas não críticas
  - Error: Falhas de integrações externas após retries, erros de validação, exceções não tratadas
  - Debug: Parâmetros de queries (mascarados), detalhes de processamento de eventos
  - Critical: Falhas de conexão com SQL Server/RabbitMQ, erros de configuração

**Health Checks:**
- Configurar em `api-inadimplencia.Api/Program.cs`
- Adicionar pacote NuGet: `AspNetCore.HealthChecks.SqlServer`, `AspNetCore.HealthChecks.RabbitMQ`, `AspNetCore.HealthChecks.Uris`
- Health check SQL Server: verificar conexão
- Health check RabbitMQ: verificar conexão
- Health check Fluig: verificar endpoint j_security_check
- Health check RM: verificar endpoint dsIntegraFacilRM
- Health check Serasa: verificar endpoint de token
- Expor em endpoint `/health`

**DI:**
- Registrar middleware no pipeline
- Configurar OpenTelemetry no DI
- Configurar health checks no DI

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Program.cs`
- Mapear:
  - `/metrics` → Prometheus metrics
  - `/health` → Health checks

## Critérios de Sucesso

- Middleware mascarando CPF/CNPJ, tokens, secrets, cookies, payloads
- Logs com dados sensíveis mascarados
- Respostas HTTP com dados sensíveis mascarados
- OpenTelemetry tracing funcionando
- OpenTelemetry metrics funcionando
- Métricas expostas em /metrics
- ILogger com níveis apropriados
- Health check SQL Server funcionando
- Health check RabbitMQ funcionando
- Health check integrações externas funcionando
- Health check exposto em /health
- Testes de unidade passam
- Testes de integração passam

## Testes da Tarefa

- [ ] Testes de unidade
  - Testar mascaramento de CPF/CNPJ
  - Testar mascaramento de tokens
  - Testar mascaramento de secrets
  - Testar mascaramento de cookies
  - Testar mascaramento de payloads
  - Testar níveis de log
- [ ] Testes de integração
  - Testar middleware em pipeline
  - Testar tracing com requests reais
  - Testar metrics com requests reais
  - Testar /metrics endpoint
  - Testar health checks
  - Testar /health endpoint

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs` (atualizar)
- `api-inadimplencia.Api/Program.cs` (atualizar)
- `api-inadimplencia.Api/appsettings.json` (atualizar)

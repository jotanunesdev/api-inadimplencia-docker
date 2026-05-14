# Tarefa 1.0: Setup Infraestrutura Base

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Configurar a infraestrutura base da aplicação .NET 8 seguindo Clean Architecture + CQRS. Esta tarefa cria a fundação para todas as outras tarefas, incluindo EF Core DbContext, MassTransit com RabbitMQ, Dapper, injeção de dependências e configuração de OpenTelemetry.

<requirements>
- Configurar EF Core DbContext para tabelas de escrita (OCORRENCIAS, ATENDIMENTOS, USUARIO, VENDA_RESPONSAVEL, KANBAN_STATUS, INAD_NOTIFICACOES, SERASA_PEFIN_SOLICITACOES, SERASA_PEFIN_WEBHOOKS)
- Configurar MassTransit com RabbitMQ para event bus e outbox pattern
- Configurar Dapper para queries de leitura otimizadas
- Configurar injeção de dependências para todas as camadas
- Configurar OpenTelemetry básico para tracing e métricas
- Criar projects de teste xUnit para todas as camadas
- Configurar ILogger nativo
</requirements>

## Subtarefas

- [ ] 1.1 Criar DbContext EF Core com entidades e mapeamentos
- [ ] 1.2 Configurar MassTransit com RabbitMQ no DependencyInjection
- [ ] 1.3 Configurar Dapper e ILegacySqlExecutor
- [ ] 1.4 Configurar injeção de dependências completa no DependencyInjection.cs
- [ ] 1.5 Configurar OpenTelemetry básico no Program.cs
- [ ] 1.6 Criar projects de teste xUnit para Domain, Application, Infrastructure e API
- [ ] 1.7 Adicionar FluentAssertions e Moq aos projects de teste
- [ ] 1.8 Configurar appsettings.json com connection strings e configurações

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Arquitetura do Sistema**: Componentes ApiInadimplencia.Domain, Application, Infrastructure, API
- **Interfaces Principais**: IRepository, IReadModelQuery, IEventBus
- **Pontos de Integração**: SQL Server DW/Operacional, RabbitMQ
- **Monitoramento e Observabilidade**: Métricas Prometheus e níveis de log

**DbContext EF Core:**
- Criar `ApiInadimplencia.Infrastructure/Persistence/SqlServer/InadimplenciaDbContext.cs`
- Mapear entidades: Ocorrencia, Atendimento, Usuario, VendaResponsavel, KanbanStatus, InadNotificacao, SerasaPefinSolicitacao, SerasaPefinWebhook
- Configurar connection string via SqlServerOptions
- Configurar migrations se necessário

**MassTransit com RabbitMQ:**
- Configurar em `ApiInadimplencia.Infrastructure/DependencyInjection.cs`
- Usar `AddMassTransit(x => { x.UsingRabbitMq(...) })`
- Configurar exchanges: `inadimplencia.events`, `serasa.webhooks`
- Configurar outbox pattern para commands críticos
- Adicionar pacote NuGet: `MassTransit.RabbitMQ`

**Dapper:**
- Implementar `DapperReadModelQuery` usando `ILegacySqlExecutor`
- Adicionar pacote NuGet: `Dapper`
- Configurar para queries de leitura otimizadas

**DI:**
- Atualizar `ApiInadimplencia.Infrastructure/DependencyInjection.cs`
- Registrar repositories, query executors, gateways, event bus
- Configurar HttpClient factories para integrações externas

**OpenTelemetry:**
- Configurar em `api-inadimplencia.Api/Program.cs`
- Adicionar pacotes NuGet: `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.Prometheus`
- Configurar tracing para ASP.NET Core e EF Core
- Expor métricas em endpoint `/metrics`

**Test Projects:**
- Criar `ApiInadimplencia.Domain.Tests.csproj`
- Criar `ApiInadimplencia.Application.Tests.csproj`
- Criar `ApiInadimplencia.Infrastructure.Tests.csproj`
- Criar `api-inadimplencia.Api.Tests.csproj`
- Adicionar xUnit, FluentAssertions, Moq

## Critérios de Sucesso

- DbContext configurado com todas as entidades e mapeamentos
- MassTransit conectado ao RabbitMQ local ou container
- Dapper configurado para executar queries SQL parametrizadas
- DI configurada sem erros de startup
- OpenTelemetry expondo métricas em `/metrics`
- Projects de teste criados e compilando sem erros
- Application inicia sem erros de configuração

## Testes da Tarefa

- [ ] Testes de unidade
  - Testar DbContext com InMemory provider
  - Testar configuração de DI
  - Testar configuração de MassTransit com InMemory test harness
- [ ] Testes de integração
  - Testar conexão com SQL Server (container ou local)
  - Testar conexão com RabbitMQ (container)
  - Testar que DbContext pode persistir e recuperar entidades

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/InadimplenciaDbContext.cs` (novo)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (atualizar)
- `api-inadimplencia.Api/Program.cs` (atualizar)
- `api-inadimplencia.Api/appsettings.json` (atualizar)
- `ApiInadimplencia.Domain.Tests/` (novo)
- `ApiInadimplencia.Application.Tests/` (novo)
- `ApiInadimplencia.Infrastructure.Tests/` (novo)
- `api-inadimplencia.Api.Tests/` (novo)

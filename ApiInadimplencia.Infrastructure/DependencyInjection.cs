using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Features.Atendimentos.Commands;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Queries;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using ApiInadimplencia.Application.Features.Legacy;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Notifications.Commands;
using ApiInadimplencia.Application.Features.Notifications.Dtos;
using ApiInadimplencia.Application.Features.Notifications.Queries;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Application.Features.Negativacao.Queries;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.Ocorrencias.Commands;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Application.Features.Ocorrencias.Queries;
using ApiInadimplencia.Application.Features.Atendimentos.Queries;
using ApiInadimplencia.Application.Features.SerasaPefin.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Application.Features.SerasaPefin.Queries;
using ApiInadimplencia.Application.Features.SerasaPefin.Webhooks;
// using ApiInadimplencia.Application.Features.Relatorios.Commands;
// using ApiInadimplencia.Infrastructure.BackgroundServices;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Infrastructure.Auth;
using ApiInadimplencia.Infrastructure.Configuration;
// using ApiInadimplencia.Infrastructure.Integrations.Fluig;
// using ApiInadimplencia.Infrastructure.Integrations.Rm;
using ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;
using ApiInadimplencia.Infrastructure.Notifications;
using ApiInadimplencia.Infrastructure.Monitoring;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiInadimplencia.Infrastructure;

/// <summary>
/// Registers infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure adapters for the inadimplencia module.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? environmentName = null)
    {
        var sqlServerOptions = configuration.GetSection(SqlServerOptions.SectionName).Get<SqlServerOptions>() ?? new SqlServerOptions();
        var hasSqlServer = !string.IsNullOrWhiteSpace(sqlServerOptions.ConnectionString);
        var isTestingEnvironment = string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        var authOptions = AuthOptions.FromConfiguration(configuration, environmentName);

        // SQL Server configuration
        services
            .AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName))
            .ValidateDataAnnotations();

        services
            .AddOptions<AuditDbOptions>()
            .Bind(configuration.GetSection(AuditDbOptions.SectionName))
            .ValidateDataAnnotations();

        services
            .AddOptions<TrafficMonitoringOptions>()
            .Bind(configuration.GetSection(TrafficMonitoringOptions.SectionName))
            .ValidateDataAnnotations();

        // RabbitMQ configuration
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations();

        // Fluig configuration (credentials checked at call time by the gateway,
        // so the app can boot in environments that don't use ficha-financeira).
        services
            .AddOptions<FluigOptions>()
            .Bind(configuration.GetSection(FluigOptions.SectionName));

        // RM configuration (Application-layer Options so the handler can be in Application).
        services
            .AddOptions<ApiInadimplencia.Application.Configuration.RmOptions>()
            .Bind(configuration.GetSection(ApiInadimplencia.Application.Configuration.RmOptions.SectionName));

        // Serasa PEFIN configuration
        services
            .AddOptions<SerasaPefinOptions>()
            .Bind(configuration.GetSection(SerasaPefinOptions.SectionName))
            .ValidateDataAnnotations();

        // Negativacao configuration
        services
            .AddOptions<ApiInadimplencia.Application.Configuration.NegativacaoOptions>()
            .Bind(configuration.GetSection(ApiInadimplencia.Application.Configuration.NegativacaoOptions.SectionName))
            .ValidateDataAnnotations();

        // OverdueScanner configuration
        // services
        //     .AddOptions<OverdueScannerOptions>()
        //     .Bind(configuration.GetSection(OverdueScannerOptions.SectionName))
        //     .ValidateDataAnnotations();

        // HTTP Context accessor for CurrentUserService
        services.AddHttpContextAccessor();

        // Auth integration and inadimplencia session services
        services.AddSingleton(authOptions);
        services.AddHttpClient<EntraIdAuthClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(authOptions.AuthServerTimeoutSeconds);
        });
        services.AddTransient<IEntraIdAuthClient>(sp => sp.GetRequiredService<EntraIdAuthClient>());
        services.AddTransient<IAuthServerClient>(sp => sp.GetRequiredService<EntraIdAuthClient>());
        services.AddSingleton<CredentialCrypto>();
        services.AddSingleton<CredentialTransportCrypto>();
        services.AddSingleton<IInadimplenciaSessionStore, InMemoryInadimplenciaSessionStore>();
        services.AddScoped<IAdCredentialRepository, SqlServerAdCredentialRepository>();
        services.AddScoped<IEntraTokenRepository, SqlServerEntraTokenRepository>();

        // Auth services for negativacao fluxo
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IAprovadoresPolicy, OptionsAprovadoresPolicy>();

        // SQL Server connection factory
        services.AddSingleton<SqlServerConnectionFactory>();
        services.AddSingleton<AuditSqlConnectionFactory>();
        services.AddSingleton<ITrafficRequestStore, SqlServerTrafficRequestStore>();
        services.AddSingleton<ITrafficAnalyticsQuery, SqlServerTrafficAnalyticsQuery>();
        services.AddSingleton<ILoadTestRunRepository, SqlServerLoadTestRunRepository>();
        services.AddSingleton<K6LoadTestOrchestrator>();
        services.AddSingleton<ILoadTestOrchestrator>(provider =>
            provider.GetRequiredService<K6LoadTestOrchestrator>());
        services.AddSingleton<ILoadTestRequestAuthorizer>(provider =>
            provider.GetRequiredService<K6LoadTestOrchestrator>());
        services.AddSingleton<TrafficRecordChannel>();
        services.AddSingleton<ITrafficRequestSink>(provider =>
            provider.GetRequiredService<TrafficRecordChannel>());
        services.AddHostedService(provider =>
            provider.GetRequiredService<TrafficRecordChannel>());

        // EF Core DbContext
        services.AddDbContext<InadimplenciaDbContext>((serviceProvider, options) =>
        {
            if (hasSqlServer)
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var sqlOptions = configuration.GetSection(SqlServerOptions.SectionName).Get<SqlServerOptions>()
                    ?? throw new InvalidOperationException("SqlServerOptions not configured.");

                options.UseSqlServer(sqlOptions.ConnectionString, builder =>
                {
                    builder.CommandTimeout((int)TimeSpan.FromSeconds(sqlOptions.CommandTimeoutSeconds).TotalSeconds);
                    builder.EnableRetryOnFailure(maxRetryCount: 3);
                });
                return;
            }

            options.UseInMemoryDatabase("Inadimplencia_NoSqlConfig");
        });

        // MassTransit with RabbitMQ
        services.AddMassTransit(x =>
        {
            if (isTestingEnvironment)
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                        ?? throw new InvalidOperationException("RabbitMqOptions not configured.");

                    cfg.Host(rabbitMqOptions.Host, "/", h =>
                    {
                        h.Username(rabbitMqOptions.Username);
                        h.Password(rabbitMqOptions.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }

            // Outbox pattern for commands. Requires tables InboxState, OutboxMessage
            // and OutboxState in dbo (see db/002_masstransit_outbox.sql).
            if (hasSqlServer && !isTestingEnvironment)
            {
                x.AddEntityFrameworkOutbox<InadimplenciaDbContext>(o =>
                {
                    o.QueryDelay = TimeSpan.FromSeconds(5);
                    o.UseSqlServer();
                    o.UseBusOutbox();
                });
            }
        });

        // Legacy SQL executor
        services.AddScoped<ILegacySqlExecutor, LegacySqlExecutor>();
        services.AddScoped<IQueryHandler<LegacySqlQuery, LegacySqlResult>, LegacySqlQueryHandler>();
        services.AddScoped<ICommandHandler<LegacySqlCommand, LegacySqlResult>, LegacySqlCommandHandler>();

        // Inadimplencia query handlers
        services.AddScoped<IQueryHandler<ListInadimplenciasQuery, PagedInadimplenciaResult>, ListInadimplenciasQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByCpfQuery, PagedInadimplenciaResult>, GetInadimplenciaByCpfQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByNumVendaQuery, InadimplenciaDto?>, GetInadimplenciaByNumVendaQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByResponsavelQuery, PagedInadimplenciaResult>, GetInadimplenciaByResponsavelQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByClienteQuery, PagedInadimplenciaResult>, GetInadimplenciaByClienteQueryHandler>();

        // Fiadores query handlers
        services.AddScoped<IQueryHandler<GetFiadoresByNumVendaQuery, IReadOnlyList<FiadorDto>>, GetFiadoresByNumVendaQueryHandler>();
        services.AddScoped<IQueryHandler<GetFiadoresByCpfQuery, IReadOnlyList<FiadorDto>>, GetFiadoresByCpfQueryHandler>();

        // Dashboard query handlers
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Dashboard.Queries.GetDashboardKpisQuery, ApiInadimplencia.Application.Features.Dashboard.Dtos.DashboardKpisDto>, ApiInadimplencia.Application.Features.Dashboard.Queries.GetDashboardKpisQueryHandler>();
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Dashboard.Queries.GetMetricQuery, IReadOnlyList<Dictionary<string, object?>>>, ApiInadimplencia.Application.Features.Dashboard.Queries.GetMetricQueryHandler>();
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Dashboard.Queries.GetMotivosBaixaQuery, IReadOnlyList<ApiInadimplencia.Application.Features.Dashboard.Dtos.MotivoBaixaDto>>, ApiInadimplencia.Application.Features.Dashboard.Queries.GetMotivosBaixaQueryHandler>();
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Dashboard.Queries.GetNegativacoesVsBaixasQuery, IReadOnlyList<ApiInadimplencia.Application.Features.Dashboard.Dtos.NegativacaoBaixaMensalDto>>, ApiInadimplencia.Application.Features.Dashboard.Queries.GetNegativacoesVsBaixasQueryHandler>();

        // Serasa PEFIN query/command handlers
        services.AddScoped<IQueryHandler<GetSerasaPreviewQuery, SerasaPefinPreviewResponse>, GetSerasaPreviewQueryHandler>();
        services.AddScoped<IQueryHandler<GetSerasaAcompanhamentoQuery, SerasaPefinAcompanhamentoResponse?>, GetSerasaAcompanhamentoQueryHandler>();
        services.AddScoped<IQueryHandler<GetSerasaHistoricoQuery, List<SerasaPefinHistoricoItem>>, GetSerasaHistoricoQueryHandler>();
        services.AddScoped<IQueryHandler<GetNegativacaoByIdQuery, SerasaPefinDetalheDto?>, GetNegativacaoByIdQueryHandler>();
        services.AddScoped<ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>, RequestNegativacaoCommandHandler>();

        // Protocolo generator
        services.AddScoped<IProtocoloGenerator, ProtocoloGenerator>();

        // Ocorrencia repository and validator
        services.AddScoped<IOcorrenciaRepository, OcorrenciaRepository>();
        services.AddScoped<IVendaValidator, VendaValidator>();

        // Atendimento repository
        services.AddScoped<IAtendimentoRepository, AtendimentoRepository>();

        // Ocorrencia command handlers
        services.AddScoped<ICommandHandler<CreateOcorrenciaCommand, Guid>, CreateOcorrenciaCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateOcorrenciaCommand, bool>, UpdateOcorrenciaCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteOcorrenciaCommand, bool>, DeleteOcorrenciaCommandHandler>();

        // Ocorrencia query handlers
        services.AddScoped<IQueryHandler<ListOcorrenciasQuery, IReadOnlyList<OcorrenciaDto>>, ListOcorrenciasQueryHandler>();
        services.AddScoped<IQueryHandler<GetOcorrenciaByIdQuery, OcorrenciaDto?>, GetOcorrenciaByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListOcorrenciasByNumVendaQuery, IReadOnlyList<OcorrenciaDto>>, ListOcorrenciasByNumVendaQueryHandler>();
        services.AddScoped<IQueryHandler<ListOcorrenciasByProtocoloQuery, IReadOnlyList<OcorrenciaDto>>, ListOcorrenciasByProtocoloQueryHandler>();

        // Atendimento command handlers
        services.AddScoped<ICommandHandler<CreateAtendimentoCommand, string>, CreateAtendimentoCommandHandler>();

        // Atendimento query handlers
        services.AddScoped<IQueryHandler<ListAtendimentosByCpfQuery, IReadOnlyList<AtendimentoDto>>, ListAtendimentosByCpfQueryHandler>();
        services.AddScoped<IQueryHandler<ListAtendimentosByNumVendaQuery, IReadOnlyList<AtendimentoDto>>, ListAtendimentosByNumVendaQueryHandler>();
        services.AddScoped<IQueryHandler<GetAtendimentoByProtocoloQuery, AtendimentoDto?>, GetAtendimentoByProtocoloQueryHandler>();
        services.AddScoped<IQueryHandler<ListAtendimentosByClienteQuery, IReadOnlyList<AtendimentoDto>>, ListAtendimentosByClienteQueryHandler>();

        // HttpClient for Serasa PEFIN (AuthUrl and CollectionBaseUrl are full URLs used directly inside the client)
        services.AddHttpClient<SerasaPefinClient>(client =>
        {
            var serasaOptions = configuration.GetSection(SerasaPefinOptions.SectionName).Get<SerasaPefinOptions>()
                ?? throw new InvalidOperationException("SerasaPefinOptions not configured.");
            client.Timeout = TimeSpan.FromSeconds(serasaOptions.TimeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            // Serasa UAT sandbox (api.serasa.dev) returns an incomplete certificate chain (PartialChain).
            // We relax the validation only when the configured Env is "uat"; production keeps strict validation.
            // Also disable system/Docker Desktop proxy so that calls go directly to the Serasa endpoints
            // (Docker Desktop on Windows may force a corporate proxy such as sophosjotanunes:8090 that is
            // not reachable from within the container network).
            var serasaOptions = configuration.GetSection(SerasaPefinOptions.SectionName).Get<SerasaPefinOptions>();
            var handler = new HttpClientHandler
            {
                UseProxy = false,
            };

            if (serasaOptions is not null
                && string.Equals(serasaOptions.Env, "uat", StringComparison.OrdinalIgnoreCase))
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            return handler;
        });

        // Serasa PEFIN services
        services.AddSingleton<SerasaPefinTokenCache>();
        services.AddScoped<SerasaPefinPayloadBuilder>();
        services.AddScoped<SerasaWebhookHandler>();
        services.AddScoped<ISerasaPefinGateway, SerasaPefinGateway>();
        services.AddScoped<ISerasaPefinRepository, SerasaPefinRepository>();
        services.AddScoped<ISerasaPefinBaixaRepository, SerasaPefinBaixaRepository>();
        services.AddScoped<IInadimplenciaQueryService, InadimplenciaQueryService>();
        services.AddScoped<IInadimplenciaParcelaWriteService, InadimplenciaParcelaWriteService>();

        // Negativacao services
        services.AddScoped<ISenhaTransacaoRepository, SenhaTransacaoRepository>();
        services.AddScoped<ISenhaTransacaoHasher, Pbkdf2SenhaTransacaoHasher>();
        services.AddScoped<ISenhaTransacaoValidator, SenhaTransacaoValidator>();
        services.AddScoped<ICommandHandler<SetSenhaTransacaoCommand, bool>, SetSenhaTransacaoCommandHandler>();
        services.AddScoped<IQueryHandler<GetHasSenhaTransacaoQuery, bool>, GetHasSenhaTransacaoQueryHandler>();
        services.AddScoped<IQueryHandler<GetDividasElegiveisQuery, ApiInadimplencia.Application.Features.Negativacao.Dtos.DividasElegiveisResponse>, GetDividasElegiveisQueryHandler>();
        services.AddScoped<ICommandHandler<RequestNegativacaoFluxoCommand, Guid>, RequestNegativacaoFluxoCommandHandler>();
        services.AddScoped<ICommandHandler<ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.RequestBaixaCommand, Guid>, ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.RequestBaixaCommandHandler>();
        services.AddScoped<ICommandHandler<ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.SendBaixaToSerasaCommand, bool>, ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.SendBaixaToSerasaCommandHandler>();
        services.AddScoped<ICommandHandler<ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.DecideBaixaCommand, bool>, ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.DecideBaixaCommandHandler>();
        services.AddScoped<ICommandHandler<ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.ResendBaixaCommand, ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.ResendBaixaResult>, ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands.ResendBaixaCommandHandler>();
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries.GetBaixaByIdQuery, ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries.BaixaDetalheDto?>, ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries.GetBaixaByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries.ListBaixasQuery, IReadOnlyList<ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries.BaixaResumoDto>>, ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries.ListBaixasQueryHandler>();
        services.AddScoped<ICommandHandler<DecideNegativacaoCommand, bool>, DecideNegativacaoCommandHandler>();
        services.AddScoped<IQueryHandler<ListSolicitacoesPendentesQuery, IReadOnlyList<SolicitacaoPendenteDto>>, ListSolicitacoesPendentesQueryHandler>();
        services.AddScoped<IQueryHandler<GetSolicitacaoByIdQuery, SolicitacaoDetalheDto?>, GetSolicitacaoByIdQueryHandler>();

        // Fluig HTTP clients.
        // The auth client must NOT follow redirects automatically because Fluig
        // signals successful login via 302 and we need to read Set-Cookie from
        // that response. The dataset client uses normal redirects.
        services.AddHttpClient(ApiInadimplencia.Infrastructure.Integrations.Fluig.FluigSessionManager.AuthHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15));

        services.AddHttpClient(ApiInadimplencia.Infrastructure.Integrations.Fluig.FluigDatasetGateway.DatasetHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
            })
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(60));

        // FluigSessionManager keeps the cookie cache in memory for up to 10 min
        // (matching the legacy Node.js behavior); a singleton instance is required.
        services.AddSingleton<ApiInadimplencia.Infrastructure.Integrations.Fluig.FluigSessionManager>();
        services.AddScoped<IFluigDatasetGateway, ApiInadimplencia.Infrastructure.Integrations.Fluig.FluigDatasetGateway>();

        // Relatorios command handler.
        services.AddScoped<
            ICommandHandler<ApiInadimplencia.Application.Features.Relatorios.Dtos.GenerateFichaFinanceiraCommand, string>,
            ApiInadimplencia.Application.Features.Relatorios.Commands.GenerateFichaFinanceiraCommandHandler>();

        // Notification repository
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Notification command handlers
        services.AddScoped<ICommandHandler<CreateNotificationCommand, Guid>, CreateNotificationCommandHandler>();

        // Notification query handler
        services.AddScoped<IQueryHandler<ListNotificationsQuery, PagedResult<NotificationDto>>, ListNotificationsQueryHandler>();

        // SSE Hub as singleton
        services.AddSingleton<SseHub>();

        // Notification dispatcher (orchestrates persistence + SSE push)
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // OverdueScanner as hosted service (disabled - incomplete implementation)
        // services.AddHostedService<OverdueScanner>();

        // MassTransit consumer for VendaAtribuidaEvent (disabled - incomplete implementation)
        // services.AddMassTransit(x =>
        // {
        //     x.AddConsumer<VendaAtribuidaEventHandler>();
        // });

        return services;
    }
}

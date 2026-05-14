using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Atendimentos.Commands;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Queries;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using ApiInadimplencia.Application.Features.Legacy;
// using ApiInadimplencia.Application.Features.Notifications;
// using ApiInadimplencia.Application.Features.Notifications.Commands;
// using ApiInadimplencia.Application.Features.Notifications.Dtos;
// using ApiInadimplencia.Application.Features.Notifications.EventHandlers;
// using ApiInadimplencia.Application.Features.Notifications.Queries;
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
using ApiInadimplencia.Infrastructure.Configuration;
// using ApiInadimplencia.Infrastructure.Integrations.Fluig;
// using ApiInadimplencia.Infrastructure.Integrations.Rm;
using ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;
// using ApiInadimplencia.Infrastructure.Notifications;
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
        IConfiguration configuration)
    {
        // SQL Server configuration
        services
            .AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName))
            .ValidateDataAnnotations();

        // RabbitMQ configuration
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations();

        // Fluig configuration (disabled - incomplete implementation)
        // services
        //     .AddOptions<FluigOptions>()
        //     .Bind(configuration.GetSection(FluigOptions.SectionName))
        //     .ValidateDataAnnotations();

        // RM configuration (disabled - incomplete implementation)
        // services
        //     .AddOptions<RmOptions>()
        //     .Bind(configuration.GetSection(RmOptions.SectionName))
        //     .ValidateDataAnnotations();

        // Serasa PEFIN configuration
        services
            .AddOptions<SerasaPefinOptions>()
            .Bind(configuration.GetSection(SerasaPefinOptions.SectionName))
            .ValidateDataAnnotations();

        // OverdueScanner configuration
        // services
        //     .AddOptions<OverdueScannerOptions>()
        //     .Bind(configuration.GetSection(OverdueScannerOptions.SectionName))
        //     .ValidateDataAnnotations();

        // SQL Server connection factory
        services.AddSingleton<SqlServerConnectionFactory>();

        // EF Core DbContext
        services.AddDbContext<InadimplenciaDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var sqlOptions = configuration.GetSection(SqlServerOptions.SectionName).Get<SqlServerOptions>()
                ?? throw new InvalidOperationException("SqlServerOptions not configured.");

            options.UseSqlServer(sqlOptions.ConnectionString, builder =>
            {
                builder.CommandTimeout((int)TimeSpan.FromSeconds(sqlOptions.CommandTimeoutSeconds).TotalSeconds);
                builder.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        // MassTransit with RabbitMQ
        services.AddMassTransit(x =>
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

            // Outbox pattern for commands. Requires tables InboxState, OutboxMessage
            // and OutboxState in dbo (see db/002_masstransit_outbox.sql).
            x.AddEntityFrameworkOutbox<InadimplenciaDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.UseSqlServer();
                o.UseBusOutbox();
            });
        });

        // Legacy SQL executor
        services.AddScoped<ILegacySqlExecutor, LegacySqlExecutor>();
        services.AddScoped<IQueryHandler<LegacySqlQuery, LegacySqlResult>, LegacySqlQueryHandler>();
        services.AddScoped<ICommandHandler<LegacySqlCommand, LegacySqlResult>, LegacySqlCommandHandler>();

        // Inadimplencia query handlers
        services.AddScoped<IQueryHandler<ListInadimplenciasQuery, IReadOnlyList<InadimplenciaDto>>, ListInadimplenciasQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByCpfQuery, IReadOnlyList<InadimplenciaDto>>, GetInadimplenciaByCpfQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByNumVendaQuery, InadimplenciaDto?>, GetInadimplenciaByNumVendaQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByResponsavelQuery, IReadOnlyList<InadimplenciaDto>>, GetInadimplenciaByResponsavelQueryHandler>();
        services.AddScoped<IQueryHandler<GetInadimplenciaByClienteQuery, IReadOnlyList<InadimplenciaDto>>, GetInadimplenciaByClienteQueryHandler>();

        // Fiadores query handlers
        services.AddScoped<IQueryHandler<GetFiadoresByNumVendaQuery, IReadOnlyList<FiadorDto>>, GetFiadoresByNumVendaQueryHandler>();
        services.AddScoped<IQueryHandler<GetFiadoresByCpfQuery, IReadOnlyList<FiadorDto>>, GetFiadoresByCpfQueryHandler>();

        // Dashboard query handlers
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Dashboard.Queries.GetDashboardKpisQuery, ApiInadimplencia.Application.Features.Dashboard.Dtos.DashboardKpisDto>, ApiInadimplencia.Application.Features.Dashboard.Queries.GetDashboardKpisQueryHandler>();
        services.AddScoped<IQueryHandler<ApiInadimplencia.Application.Features.Dashboard.Queries.GetMetricQuery, IReadOnlyList<Dictionary<string, object?>>>, ApiInadimplencia.Application.Features.Dashboard.Queries.GetMetricQueryHandler>();

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
        services.AddScoped<IInadimplenciaQueryService, InadimplenciaQueryService>();

        // HttpClient for Fluig with Polly (disabled - incomplete implementation)
        // services.AddHttpClient<FluigDatasetClient>(client =>
        // {
        //     var fluigOptions = configuration.GetSection(FluigOptions.SectionName).Get<FluigOptions>()
        //         ?? throw new InvalidOperationException("FluigOptions not configured.");
        //     client.BaseAddress = new Uri(fluigOptions.BaseUrl);
        //     client.Timeout = TimeSpan.FromSeconds(fluigOptions.TimeoutSeconds);
        // })
        // .AddTransientHttpErrorPolicy(policy => policy
        //     .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        // .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10)));

        // HttpClient for RM with Polly (disabled - incomplete implementation)
        // services.AddHttpClient<RmReportClient>(client =>
        // {
        //     var rmOptions = configuration.GetSection(RmOptions.SectionName).Get<RmOptions>()
        //         ?? throw new InvalidOperationException("RmOptions not configured.");
        //     client.BaseAddress = new Uri(rmOptions.BaseUrl);
        //     client.Timeout = TimeSpan.FromSeconds(rmOptions.TimeoutSeconds);
        // })
        // .AddTransientHttpErrorPolicy(policy => policy
        //     .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        // .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));

        // Fluig and RM gateways (disabled - incomplete implementation)
        // services.AddScoped<IFluigDatasetGateway, FluigDatasetGateway>();
        // services.AddScoped<IRmReportGateway, RmReportGateway>();

        // Relatorios command handler (disabled - incomplete implementation)
        // services.AddScoped<ICommandHandler<GenerateFichaFinanceiraCommand, string>, GenerateFichaFinanceiraCommandHandler>();

        // Notification repository (disabled - incomplete implementation)
        // services.AddScoped<INotificationRepository, NotificationRepository>();

        // Notification command handlers (disabled - incomplete implementation)
        // services.AddScoped<ICommandHandler<CreateNotificationCommand, Guid>, CreateNotificationCommandHandler>();
        // services.AddScoped<ICommandHandler<MarkNotificationAsReadCommand>, MarkNotificationAsReadCommandHandler>();
        // services.AddScoped<ICommandHandler<MarkAllNotificationsAsReadCommand, int>, MarkAllNotificationsAsReadCommandHandler>();
        // services.AddScoped<ICommandHandler<DeleteNotificationCommand>, DeleteNotificationCommandHandler>();

        // Notification query handler (disabled - incomplete implementation)
        // services.AddScoped<IQueryHandler<ListNotificationsQuery, PagedResult<NotificationDto>>, ListNotificationsQueryHandler>();

        // SSE Hub as singleton (disabled - incomplete implementation)
        // services.AddSingleton<SseHub>();

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


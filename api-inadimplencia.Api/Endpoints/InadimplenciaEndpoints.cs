using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using ApiInadimplencia.Application.Features.Atendimentos.Commands;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using ApiInadimplencia.Application.Features.Atendimentos.Queries;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Queries;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using ApiInadimplencia.Application.Features.Kanban.Commands;
using ApiInadimplencia.Application.Features.Kanban.Dtos;
using ApiInadimplencia.Application.Features.Legacy;
using ApiInadimplencia.Application.Features.Notifications.Commands;
using ApiInadimplencia.Application.Features.Notifications.Dtos;
using ApiInadimplencia.Application.Features.Notifications.Queries;
using ApiInadimplencia.Application.Features.Ocorrencias.Commands;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Application.Features.Ocorrencias.Queries;
using ApiInadimplencia.Application.Features.Responsaveis.Commands;
using ApiInadimplencia.Application.Features.Responsaveis.Dtos;
using ApiInadimplencia.Application.Features.Routes;
using ApiInadimplencia.Application.Features.Relatorios.Dtos;
using ApiInadimplencia.Application.Features.Usuarios.Commands;
using ApiInadimplencia.Application.Features.Usuarios.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Application.Features.SerasaPefin.Queries;
using ApiInadimplencia.Application.Features.SerasaPefin.Webhooks;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Domain.SerasaPefin;
using ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;
using ApiInadimplencia.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Api.Endpoints;

/// <summary>
/// Maps the inadimplencia HTTP contract.
/// </summary>
public static class InadimplenciaEndpoints
{
    /// <summary>
    /// Adds inadimplencia endpoints to the application.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapInadimplenciaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "api-inadimplencia",
            timestamp = DateTimeOffset.UtcNow,
        }))
        .WithName("GlobalHealth")
        .WithOpenApi();

        var inadimplencia = app.MapGroup("/inadimplencia")
            .WithTags("Inadimplencia");

        inadimplencia.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            module = "inadimplencia",
            architecture = "Clean Architecture + CQRS",
            timestamp = DateTimeOffset.UtcNow,
        }))
        .WithName("InadimplenciaHealth")
        .WithOpenApi();

        inadimplencia.MapGet("/contracts", () => Results.Ok(new
        {
            source = @"C:\api-inadimplencia\src\modules\inadimplencia",
            documentation = "documentos/techspec-codebase.md",
            endpoints = InadimplenciaRouteCatalog.Endpoints,
        }))
        .WithName("InadimplenciaContracts")
        .WithOpenApi();

        inadimplencia.MapGet("/", async ([FromServices] IQueryHandler<ListInadimplenciasQuery, IReadOnlyList<InadimplenciaDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListInadimplenciasQuery(), ct);
            return Results.Ok(new { data = result });
        });
        inadimplencia.MapGet("/cpf/{cpf}", async (string cpf, [FromServices] IQueryHandler<GetInadimplenciaByCpfQuery, IReadOnlyList<InadimplenciaDto>> handler, CancellationToken ct) =>
        {
            var normalizedCpf = DigitsOnly(cpf);
            var result = await handler.HandleAsync(new GetInadimplenciaByCpfQuery(normalizedCpf), ct);
            return Results.Ok(new { data = result });
        });
        inadimplencia.MapGet("/num-venda/{numVenda:int}", async (int numVenda, [FromServices] IQueryHandler<GetInadimplenciaByNumVendaQuery, InadimplenciaDto?> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInadimplenciaByNumVendaQuery(numVenda), ct);
            if (result is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { data = result });
        });
        inadimplencia.MapGet("/responsavel/{nome}", async (string nome, [FromServices] IQueryHandler<GetInadimplenciaByResponsavelQuery, IReadOnlyList<InadimplenciaDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInadimplenciaByResponsavelQuery(nome), ct);
            return Results.Ok(new { data = result });
        });
        inadimplencia.MapGet("/cliente/{nomeCliente}", async (string nomeCliente, [FromServices] IQueryHandler<GetInadimplenciaByClienteQuery, IReadOnlyList<InadimplenciaDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInadimplenciaByClienteQuery(nomeCliente), ct);
            return Results.Ok(new { data = result });
        });

        MapProximasAcoes(app);
        MapUsuarios(app);
        MapResponsaveis(app);
        MapKanban(app);
        MapFiadores(app);
        MapDashboard(app);
        MapNotifications(app);
        MapSerasaPefin(app);
        MapSerasaPefinTestRoutes(app);
        MapPlannedOperationalEndpoints(app);

        return app;
    }

    private static void MapProximasAcoes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/proximas-acoes").WithTags("Proximas Acoes");
        group.MapGet("/", Query("ProximasAcoes.List"));
        group.MapGet("/{numVenda:int}", Query("ProximasAcoes.ByNumVenda", single: true));
        group.MapPost("/", () => BlockedProximaAcao());
        group.MapPut("/{numVenda:int}", () => BlockedProximaAcao());
        group.MapDelete("/{numVenda:int}", () => BlockedProximaAcao());
    }

    private static void MapUsuarios(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/usuarios").WithTags("Usuarios");
        group.MapGet("/", Query("Usuarios.List"));
        group.MapGet("/{nome}", Query("Usuarios.ByNome", single: true));
        
        group.MapPost("/", async (
            UpsertUsuarioCommand command,
            [FromServices] ICommandHandler<UpsertUsuarioCommand, UpsertUsuarioResult> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            var statusCode = result.Exists ? StatusCodes.Status200OK : StatusCodes.Status201Created;
            return Results.Json(new { data = result.Usuario, exists = result.Exists }, statusCode: statusCode);
        });
        
        group.MapPut("/{nome}", async (
            string nome,
            UpdateUsuarioCommand command,
            [FromServices] ICommandHandler<UpdateUsuarioCommand, UsuarioDto> handler,
            CancellationToken cancellationToken) =>
        {
            var updateCommand = command with { UserCode = nome };
            var result = await handler.HandleAsync(updateCommand, cancellationToken);
            return Results.Ok(new { data = result });
        });
        
        group.MapDelete("/{nome}", async (
            string nome,
            [FromServices] ICommandHandler<DeleteUsuarioCommand, bool> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteUsuarioCommand(nome), cancellationToken);
            return result ? Results.Ok(new { success = true }) : Results.NotFound();
        });
    }

    private static void MapResponsaveis(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/responsaveis").WithTags("Responsaveis");
        group.MapGet("/", Query("Responsaveis.List"));
        group.MapGet("/{numVenda:int}", Query("Responsaveis.ByNumVenda", single: true));
        
        group.MapPost("/", async (
            UpsertResponsavelCommand command,
            [FromServices] ICommandHandler<UpsertResponsavelCommand, ResponsavelDto> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(new { data = result });
        });
        
        group.MapPut("/{numVenda:int}", async (
            int numVenda,
            UpdateResponsavelCommand command,
            [FromServices] ICommandHandler<UpdateResponsavelCommand, ResponsavelDto> handler,
            CancellationToken cancellationToken) =>
        {
            var updateCommand = command with { NumVendaFk = numVenda };
            var result = await handler.HandleAsync(updateCommand, cancellationToken);
            return Results.Ok(new { data = result });
        });
        
        group.MapDelete("/{numVenda:int}", async (
            int numVenda,
            [FromServices] ICommandHandler<DeleteResponsavelCommand, bool> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteResponsavelCommand(numVenda), cancellationToken);
            return result ? Results.Ok(new { success = true }) : Results.NotFound();
        });
    }

    private static void MapKanban(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/kanban-status").WithTags("Kanban");
        group.MapGet("/", Query("KanbanStatus.List"));
        
        group.MapPost("/", async (
            UpsertKanbanStatusCommand command,
            [FromServices] ICommandHandler<UpsertKanbanStatusCommand, KanbanStatusDto> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(new { data = result });
        });
    }

    private static void MapFiadores(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/fiadores").WithTags("Fiadores");
        group.MapGet("/num-venda/{numVenda:int}", async (int numVenda, [FromServices] IQueryHandler<GetFiadoresByNumVendaQuery, IReadOnlyList<FiadorDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetFiadoresByNumVendaQuery(numVenda), ct);
            return Results.Ok(new { data = result });
        });
        group.MapGet("/cpf/{cpf}", async (string cpf, [FromServices] IQueryHandler<GetFiadoresByCpfQuery, IReadOnlyList<FiadorDto>> handler, CancellationToken ct) =>
        {
            var normalizedCpf = DigitsOnly(cpf);
            var result = await handler.HandleAsync(new GetFiadoresByCpfQuery(normalizedCpf), ct);
            return Results.Ok(new { data = result });
        });
    }

    private static void MapDashboard(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dashboard").WithTags("Dashboard");
        
        group.MapGet("/kpis", async ([FromServices] IQueryHandler<GetDashboardKpisQuery, DashboardKpisDto> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetDashboardKpisQuery(), ct);
            return Results.Ok(new { data = result });
        });

        group.MapGet("/{metric}", async (
            string metric,
            string? dataInicio,
            string? dataFim,
            int? limit,
            string? faixa,
            string? score,
            [FromServices] IQueryHandler<GetMetricQuery, IReadOnlyList<Dictionary<string, object?>>> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetMetricQuery(metric, dataInicio, dataFim, limit, faixa, score), ct);
            return Results.Ok(new { data = result });
        });
    }

    private static void MapNotifications(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").WithTags("Notifications");
        
        group.MapGet("/", async (
            string username,
            int? page,
            int? pageSize,
            bool? lida,
            [FromServices] IQueryHandler<ListNotificationsQuery, PagedResult<NotificationDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var normalizedPage = Math.Max(page ?? 1, 1);
            var normalizedPageSize = Math.Clamp(pageSize ?? 20, 1, 100);
            var query = new ListNotificationsQuery(username, normalizedPage, normalizedPageSize, lida);
            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(new { data = result.Items, total = result.Total, page = result.Page, pageSize = result.PageSize, totalPages = result.TotalPages });
        });

        group.MapGet("/stream", async (
            string username,
            [FromServices] SseHub sseHub,
            [FromServices] IQueryHandler<ListNotificationsQuery, PagedResult<NotificationDto>> queryHandler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");

            var streamWriter = new StreamWriter(context.Response.Body);
            sseHub.AddConnection(username, streamWriter);

            // Send initial snapshot of unread notifications
            var snapshotQuery = new ListNotificationsQuery(username, 1, 50, Lida: false);
            var snapshot = await queryHandler.HandleAsync(snapshotQuery, cancellationToken);
            await sseHub.SendSnapshotAsync(username, snapshot.Items, cancellationToken);

            // Keep connection alive with heartbeat
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                    await sseHub.SendHeartbeatAsync(cancellationToken);
                }
            }
            finally
            {
                sseHub.RemoveConnection(username);
            }

            return Results.Ok();
        });

        group.MapPut("/read-all", async (
            string username,
            [FromServices] ICommandHandler<MarkAllNotificationsAsReadCommand, int> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new MarkAllNotificationsAsReadCommand(username);
            var count = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(new { success = true, markedAsRead = count });
        });

        // TODO: Re-enable when MarkNotificationAsReadCommand is implemented
        // group.MapPut("/{id:guid}/read", async (
        //     Guid id,
        //     string username,
        //     ICommandHandler<MarkNotificationAsReadCommand, Unit> handler,
        //     CancellationToken cancellationToken) =>
        // {
        //     var command = new MarkNotificationAsReadCommand(id, username);
        //     await handler.HandleAsync(command, cancellationToken);
        //     return Results.Ok(new { success = true });
        // });

        // TODO: Re-enable when DeleteNotificationCommand is implemented
        // group.MapDelete("/{id:guid}", async (
        //     Guid id,
        //     string username,
        //     ICommandHandler<DeleteNotificationCommand, Unit> handler,
        //     CancellationToken cancellationToken) =>
        // {
        //     var command = new DeleteNotificationCommand(id, username);
        //     await handler.HandleAsync(command, cancellationToken);
        //     return Results.Ok(new { success = true });
        // });
    }

    private static void MapSerasaPefin(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/serasa-pefin").WithTags("Serasa PEFIN");
        
        group.MapGet("/vendas/{numVenda:int}/preview", async (
            int numVenda,
            [FromServices] IQueryHandler<GetSerasaPreviewQuery, SerasaPefinPreviewResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetSerasaPreviewQuery(numVenda), cancellationToken);
            return Results.Ok(new { data = result });
        });

        group.MapPost("/negativar", async (
            RequestNegativacaoCommand command,
            [FromServices] ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse> handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                // Get operator from claims or fallback to system
                var operador = context.User?.Identity?.Name ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
                var commandWithOperador = command with { Operador = operador };

                var result = await handler.HandleAsync(commandWithOperador, cancellationToken);
                return Results.Ok(new { data = result });
            }
            catch (SerasaPefinDuplicateActiveException ex)
            {
                return Results.Conflict(new
                {
                    error = "Solicitação ativa duplicada",
                    message = ex.Message
                });
            }
            catch (SerasaPefinValidationException ex)
            {
                return Results.BadRequest(new
                {
                    error = "Validação falhou",
                    code = ex.Code,
                    statusCode = ex.StatusCode,
                    missingFields = ex.MissingFields,
                    blockedDocuments = ex.BlockedDocuments
                });
            }
            catch (DomainNotFoundException ex)
            {
                return Results.NotFound(new
                {
                    error = "Venda não encontrada",
                    message = ex.Message
                });
            }
        });

        group.MapGet("/vendas/{numVenda:int}/negativacoes", async (
            int numVenda,
            [FromServices] IQueryHandler<GetSerasaHistoricoQuery, List<SerasaPefinHistoricoItem>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetSerasaHistoricoQuery(numVenda), cancellationToken);
            return Results.Ok(new { data = result });
        });

        group.MapGet("/acompanhamento/{transactionId}", async (
            string transactionId,
            [FromServices] IQueryHandler<GetSerasaAcompanhamentoQuery, SerasaPefinAcompanhamentoResponse?> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetSerasaAcompanhamentoQuery(transactionId), cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { data = result });
        });

        group.MapGet("/negativacoes/{id:guid}", async (
            Guid id,
            [FromServices] IQueryHandler<GetNegativacaoByIdQuery, SerasaPefinDetalheDto?> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetNegativacaoByIdQuery(id), cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { data = result });
        });

        // Webhooks - 6 endpoints for Serasa PEFIN callbacks
        group.MapPost("/webhooks/inclusao/sucesso", async (
            [FromBody] string rawJson,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(WebhookEventType.Inclusao, WebhookResultado.Sucesso, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/inclusao/erro", async (
            [FromBody] string rawJson,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(WebhookEventType.Inclusao, WebhookResultado.Erro, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/avalista/sucesso", async (
            [FromBody] string rawJson,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(WebhookEventType.Avalista, WebhookResultado.Sucesso, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/avalista/erro", async (
            [FromBody] string rawJson,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(WebhookEventType.Avalista, WebhookResultado.Erro, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/baixa/sucesso", async (
            [FromBody] string rawJson,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(WebhookEventType.Baixa, WebhookResultado.Sucesso, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/baixa/erro", async (
            [FromBody] string rawJson,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(WebhookEventType.Baixa, WebhookResultado.Erro, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });
    }

    private static void MapPlannedOperationalEndpoints(IEndpointRouteBuilder app)
    {
        var ocorrencias = app.MapGroup("/ocorrencias").WithTags("Ocorrencias");
        ocorrencias.MapGet("/", async ([FromServices] IQueryHandler<ListOcorrenciasQuery, IReadOnlyList<OcorrenciaDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListOcorrenciasQuery(), ct);
            return Results.Ok(new { data = result });
        });
        ocorrencias.MapGet("/{id:guid}", async (Guid id, [FromServices] IQueryHandler<GetOcorrenciaByIdQuery, OcorrenciaDto?> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetOcorrenciaByIdQuery(id), ct);
            if (result is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { data = result });
        });
        ocorrencias.MapGet("/num-venda/{numVenda:int}", async (int numVenda, [FromServices] IQueryHandler<ListOcorrenciasByNumVendaQuery, IReadOnlyList<OcorrenciaDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListOcorrenciasByNumVendaQuery(numVenda), ct);
            return Results.Ok(new { data = result });
        });
        ocorrencias.MapGet("/protocolo/{protocolo}", async (string protocolo, [FromServices] IQueryHandler<ListOcorrenciasByProtocoloQuery, IReadOnlyList<OcorrenciaDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListOcorrenciasByProtocoloQuery(protocolo), ct);
            return Results.Ok(new { data = result });
        });
        
        ocorrencias.MapPost("/", async (
            CreateOcorrenciaCommand command,
            [FromServices] ICommandHandler<CreateOcorrenciaCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(new { data = result });
        });
        
        ocorrencias.MapPut("/{id:guid}", async (
            Guid id,
            CreateOcorrenciaCommand command,
            [FromServices] ICommandHandler<UpdateOcorrenciaCommand, bool> handler,
            CancellationToken cancellationToken) =>
        {
            var updateCommand = new UpdateOcorrenciaCommand(
                id,
                command.Descricao,
                command.StatusOcorrencia,
                command.DtOcorrencia,
                command.HoraOcorrencia,
                command.ProximaAcao,
                command.Protocolo);
            
            var result = await handler.HandleAsync(updateCommand, cancellationToken);
            return result ? Results.Ok(new { success = true }) : Results.NotFound();
        });
        
        ocorrencias.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICommandHandler<DeleteOcorrenciaCommand, bool> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteOcorrenciaCommand(id), cancellationToken);
            return result ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        var atendimentos = app.MapGroup("/atendimentos").WithTags("Atendimentos");
        
        atendimentos.MapPost("/", async (
            CreateAtendimentoCommand command,
            [FromServices] ICommandHandler<CreateAtendimentoCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(new { data = result });
        });
        
        atendimentos.MapGet("/cpf/{cpf}", async (string cpf, [FromServices] IQueryHandler<ListAtendimentosByCpfQuery, IReadOnlyList<AtendimentoDto>> handler, CancellationToken ct) =>
        {
            var normalizedCpf = DigitsOnly(cpf);
            var result = await handler.HandleAsync(new ListAtendimentosByCpfQuery(normalizedCpf), ct);
            return Results.Ok(new { data = result });
        });
        atendimentos.MapGet("/num-venda/{numVenda:int}", async (int numVenda, [FromServices] IQueryHandler<ListAtendimentosByNumVendaQuery, IReadOnlyList<AtendimentoDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListAtendimentosByNumVendaQuery(numVenda), ct);
            return Results.Ok(new { data = result });
        });
        atendimentos.MapGet("/protocolo/{protocolo}", async (string protocolo, [FromServices] IQueryHandler<GetAtendimentoByProtocoloQuery, AtendimentoDto?> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetAtendimentoByProtocoloQuery(protocolo), ct);
            if (result is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { data = result });
        });
        atendimentos.MapGet("/cliente/{nomeCliente}", async (string nomeCliente, [FromServices] IQueryHandler<ListAtendimentosByClienteQuery, IReadOnlyList<AtendimentoDto>> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListAtendimentosByClienteQuery(nomeCliente), ct);
            return Results.Ok(new { data = result });
        });

        var relatorios = app.MapGroup("/relatorios").WithTags("Relatorios");
        relatorios.MapGet("/ficha-financeira", async (
            int numVenda,
            string codColigada,
            string reportColigada,
            string reportId,
            [FromServices] ICommandHandler<GenerateFichaFinanceiraCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new GenerateFichaFinanceiraCommand(numVenda, codColigada, reportColigada, reportId);
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(new { data = new RelatorioDto(result, reportId, DateTime.UtcNow) });
        });
    }

    private static Func<HttpContext, IQueryHandler<LegacySqlQuery, LegacySqlResult>, CancellationToken, Task<IResult>> Query(
        string queryKey,
        Func<RouteValueDictionary, IReadOnlyDictionary<string, object?>>? parametersFactory = null,
        bool single = false)
    {
        return async (context, handler, cancellationToken) =>
        {
            var parameters = parametersFactory?.Invoke(context.Request.RouteValues)
                ?? RouteValuesToParameters(context.Request.RouteValues);

            return await ExecuteQueryAsync(queryKey, parameters, single, handler, cancellationToken);
        };
    }

    private static async Task<IResult> ExecuteQueryAsync(
        string queryKey,
        IReadOnlyDictionary<string, object?> parameters,
        bool single,
        IQueryHandler<LegacySqlQuery, LegacySqlResult> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(new LegacySqlQuery(queryKey, parameters, single), cancellationToken);
            if (!result.IsConfigured)
            {
                return Results.Problem(
                    title: "SQL Server nao configurado",
                    detail: "Configure SqlServer:ConnectionString ou a env var SqlServer__ConnectionString para habilitar endpoints de dados.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new { data = result.Data });
        }
        catch (NotImplementedException ex)
        {
            return NotMigrated(queryKey, ex.Message);
        }
    }

    private static IReadOnlyDictionary<string, object?> RouteValuesToParameters(RouteValueDictionary routeValues)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in routeValues)
        {
            parameters[key] = value;
        }

        return parameters;
    }

    private static string DigitsOnly(string? value)
        => Regex.Replace(value ?? string.Empty, "\\D", string.Empty);

    private static IResult BlockedProximaAcao()
        => Results.BadRequest(new
        {
            error = "Registro de PROXIMA_ACAO deve ser feito via /ocorrencias. Endpoint somente leitura.",
        });

    private static IResult NotMigrated(string feature, string detail)
        => Results.Problem(
            title: "Endpoint catalogado, implementacao pendente",
            detail: detail,
            statusCode: StatusCodes.Status501NotImplemented,
            extensions: new Dictionary<string, object?>
            {
                ["feature"] = feature,
                ["techspec"] = "documentos/techspec-codebase.md",
            });

    private static void MapSerasaPefinTestRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/serasa-pefin/test")
            .WithTags("Serasa PEFIN Test");

        // UAT-only filter - returns 404 if Env != uat
        group.AddEndpointFilter(async (ctx, next) =>
        {
            var options = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<SerasaPefinOptions>>();
            if (!string.Equals(options.Value.Env, "uat", StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            return await next(ctx);
        });

        // POST /test/auth - force new token (ignores cache)
        group.MapPost("/auth", async (
            [FromServices] SerasaPefinClient client,
            [FromServices] SerasaPefinTokenCache tokenCache,
            [FromServices] ILoggerFactory loggerFactory,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("SerasaPefinTestRoutes");
            // Get operator from claims or fallback to anonymous
            var operador = context.User?.Identity?.Name ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            logger.LogInformation("Test route /auth invoked by operator: {Operador}", operador);

            // Clear cache to force new token
            tokenCache.Clear();

            // Get new token
            var token = await client.GetTokenAsync(cancellationToken);

            // Mask token (show only last 6 characters)
            var masked = token.Length > 8 ? $"***{token[^6..]}" : "***";

            return Results.Ok(new
            {
                accessToken = masked,
                tokenType = "Bearer",
                expiresIn = "15 minutes (actual value not exposed in test route)"
            });
        })
        .ExcludeFromDescription(); // Exclude from Swagger in production

        // POST /test/debt - proxy arbitrary payload to Serasa
        group.MapPost("/debt", async (
            [FromBody] object payload,
            [FromServices] SerasaPefinClient client,
            [FromServices] SerasaPefinTokenCache tokenCache,
            [FromServices] ILoggerFactory loggerFactory,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("SerasaPefinTestRoutes");
            var operador = context.User?.Identity?.Name ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            logger.LogInformation("Test route /debt invoked by operator: {Operador}", operador);

            // Get or refresh token
            var token = tokenCache.GetToken();
            if (token == null)
            {
                token = await client.GetTokenAsync(cancellationToken);
                // Cache for 15 minutes (Serasa token expiration)
                tokenCache.SetToken(token, TimeSpan.FromMinutes(15));
            }

            // Send payload directly to Serasa
            var response = await client.PostMainDebtAsync(payload, token, cancellationToken);

            return Results.Ok(new { data = response });
        })
        .ExcludeFromDescription();

        // GET /test/documents - list UAT authorized documents
        group.MapGet("/documents", (
            [FromServices] ILoggerFactory loggerFactory,
            HttpContext context) =>
        {
            var logger = loggerFactory.CreateLogger("SerasaPefinTestRoutes");
            var operador = context.User?.Identity?.Name ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            logger.LogInformation("Test route /documents invoked by operator: {Operador}", operador);

            return Results.Ok(new
            {
                data = SerasaPefinConstants.UatAuthorizedDocuments.ToList(),
                count = SerasaPefinConstants.UatAuthorizedDocuments.Count
            });
        })
        .ExcludeFromDescription();

        // POST /test/simulate-webhook - simulate webhook call
        group.MapPost("/simulate-webhook", async (
            [FromBody] SimulateWebhookRequest request,
            [FromServices] SerasaWebhookHandler webhookHandler,
            [FromServices] ILoggerFactory loggerFactory,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("SerasaPefinTestRoutes");
            var operador = context.User?.Identity?.Name ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            logger.LogInformation("Test route /simulate-webhook invoked by operator: {Operador}, EventType: {EventType}, Resultado: {Resultado}",
                operador, request.EventType, request.Resultado);

            // Parse event type and result
            if (!Enum.TryParse<WebhookEventType>(request.EventType, true, out var eventType))
            {
                return Results.BadRequest(new { error = $"Invalid eventType: {request.EventType}" });
            }

            if (!Enum.TryParse<WebhookResultado>(request.Resultado, true, out var resultado))
            {
                return Results.BadRequest(new { error = $"Invalid resultado: {request.Resultado}" });
            }

            // Call webhook handler
            var result = await webhookHandler.HandleAsync(eventType, resultado, request.Payload, cancellationToken);

            return Results.Ok(new
            {
                processed = true,
                alreadyProcessed = result.WasAlreadyProcessed,
                uuid = result.Uuid
            });
        })
        .ExcludeFromDescription();
    }

    private record SimulateWebhookRequest(string EventType, string Resultado, string Payload);
}

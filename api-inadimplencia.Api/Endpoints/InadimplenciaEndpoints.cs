using System.Text.RegularExpressions;
using System.Text.Json;
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
using ApiInadimplencia.Application.Features.Notifications;
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
using ApiInadimplencia.Domain.Notifications;
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

        inadimplencia.MapInadimplenciaSessionEndpoints();

        inadimplencia.MapGet("/", async (
            int? page,
            int? pageSize,
            [FromServices] IQueryHandler<ListInadimplenciasQuery, PagedInadimplenciaResult> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new ListInadimplenciasQuery(NormalizePage(page), NormalizePageSize(pageSize)),
                ct);
            return Results.Ok(PagedResponse(result));
        });
        inadimplencia.MapGet("/cpf/{cpf}", async (
            string cpf,
            int? page,
            int? pageSize,
            [FromServices] IQueryHandler<GetInadimplenciaByCpfQuery, PagedInadimplenciaResult> handler,
            CancellationToken ct) =>
        {
            var normalizedCpf = DigitsOnly(cpf);
            var result = await handler.HandleAsync(
                new GetInadimplenciaByCpfQuery(normalizedCpf, NormalizePage(page), NormalizePageSize(pageSize)),
                ct);
            return Results.Ok(PagedResponse(result));
        });
        inadimplencia.MapGet("/num-venda/{numVenda:int}", async (int numVenda, [FromServices] IQueryHandler<GetInadimplenciaByNumVendaQuery, InadimplenciaDto?> handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInadimplenciaByNumVendaQuery(numVenda), ct);
            if (result is null)
            {
                return Results.NotFound(new
                {
                    error = "NAO_ENCONTRADA",
                    numVenda,
                });
            }
            return Results.Ok(new { data = result });
        });
        inadimplencia.MapGet("/responsavel/{nome}", async (
            string nome,
            int? page,
            int? pageSize,
            [FromServices] IQueryHandler<GetInadimplenciaByResponsavelQuery, PagedInadimplenciaResult> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new GetInadimplenciaByResponsavelQuery(nome, NormalizePage(page), NormalizePageSize(pageSize)),
                ct);
            return Results.Ok(PagedResponse(result));
        });
        inadimplencia.MapGet("/cliente/{nomeCliente}", async (
            string nomeCliente,
            int? page,
            int? pageSize,
            [FromServices] IQueryHandler<GetInadimplenciaByClienteQuery, PagedInadimplenciaResult> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new GetInadimplenciaByClienteQuery(nomeCliente, NormalizePage(page), NormalizePageSize(pageSize)),
                ct);
            return Results.Ok(PagedResponse(result));
        });

        MapProximasAcoes(inadimplencia);
        MapUsuarios(inadimplencia);
        MapResponsaveis(inadimplencia);
        MapKanban(inadimplencia);
        MapFiadores(inadimplencia);
        MapDashboard(inadimplencia);
        MapNotifications(inadimplencia);
        MapSerasaPefin(inadimplencia);
        MapSerasaPefinTestRoutes(inadimplencia);
        MapPlannedOperationalEndpoints(inadimplencia);

        return app;
    }

    private static void MapProximasAcoes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/proximas-acoes").WithTags("Proximas Acoes");
        group.MapGet("/", async (
            int? page,
            int? pageSize,
            [FromServices] IQueryHandler<LegacySqlQuery, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var normalizedPage = NormalizePage(page);
            var normalizedPageSize = NormalizePageSize(pageSize);
            var parameters = PagedParameters(normalizedPage, normalizedPageSize);
            var result = await handler.HandleAsync(new LegacySqlQuery("ProximasAcoes.List", parameters), cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var rows = ResultRows(result);
            var total = rows.Count > 0 ? Convert.ToInt32(rows[0].GetValueOrDefault("TOTAL_COUNT") ?? 0) : 0;
            return Results.Ok(new
            {
                data = rows,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                total,
                totalPages = TotalPages(total, normalizedPageSize),
            });
        });
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
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var nome = BodyString(body, "nome", "NOME");
            if (string.IsNullOrWhiteSpace(nome))
            {
                return Results.BadRequest(new { error = "NOME e obrigatorio." });
            }

            var userCode = BodyString(body, "userCode", "USER_CODE");
            var perfil = NormalizePerfil(BodyString(body, "perfil", "PERFIL"))
                ?? (string.Equals(userCode, "wffluig", StringComparison.OrdinalIgnoreCase) ? "admin" : "operador");
            var corHex = NormalizeHex(BodyString(body, "corHex", "COR_HEX"));
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Usuarios.Upsert", new Dictionary<string, object?>
                {
                    ["nome"] = nome.Trim(),
                    ["userCode"] = string.IsNullOrWhiteSpace(userCode) ? null : userCode.Trim(),
                    ["perfil"] = perfil,
                    ["cpfUsuario"] = BodyString(body, "cpfUsuario", "CPF_USUARIO"),
                    ["setor"] = BodyString(body, "setor", "SETOR"),
                    ["cargo"] = BodyString(body, "cargo", "CARGO"),
                    ["ativo"] = BodyBool(body, "ativo", "ATIVO") ?? true,
                    ["corHex"] = corHex,
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var statusCode = result.RowsAffected.GetValueOrDefault() > 0
                ? StatusCodes.Status201Created
                : StatusCodes.Status200OK;
            return Results.Json(new { data = result.Data }, statusCode: statusCode);
        });
        
        group.MapPut("/{nome}", async (
            string nome,
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var perfilRaw = BodyString(body, "perfil", "PERFIL");
            var perfil = NormalizePerfil(perfilRaw);
            if (!string.IsNullOrWhiteSpace(perfilRaw) && perfil is null)
            {
                return Results.BadRequest(new { error = "PERFIL invalido. Use admin ou operador." });
            }

            var corHexRaw = BodyString(body, "corHex", "COR_HEX");
            var corHex = NormalizeHex(corHexRaw);
            if (!string.IsNullOrWhiteSpace(corHexRaw) && corHex is null)
            {
                return Results.BadRequest(new { error = "COR_HEX invalida." });
            }

            var result = await handler.HandleAsync(
                new LegacySqlCommand("Usuarios.Update", new Dictionary<string, object?>
                {
                    ["nome"] = nome.Trim(),
                    ["userCode"] = BodyString(body, "userCode", "USER_CODE"),
                    ["perfil"] = perfil,
                    ["cpfUsuario"] = BodyString(body, "cpfUsuario", "CPF_USUARIO"),
                    ["setor"] = BodyString(body, "setor", "SETOR"),
                    ["cargo"] = BodyString(body, "cargo", "CARGO"),
                    ["ativo"] = BodyBool(body, "ativo", "ATIVO"),
                    ["corHex"] = corHex,
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return result.Data is null ? Results.NotFound() : Results.Ok(new { data = result.Data });
        });
        
        group.MapDelete("/{nome}", async (
            string nome,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Usuarios.Delete", new Dictionary<string, object?> { ["nome"] = nome.Trim() }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return (result.RowsAffected ?? 0) > 0 ? Results.Ok(new { success = true }) : Results.NotFound();
        });
    }

    private static void MapResponsaveis(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/responsaveis").WithTags("Responsaveis");
        group.MapGet("/", Query("Responsaveis.List"));
        group.MapGet("/{numVenda:int}", Query("Responsaveis.ByNumVenda", single: true));
        
        group.MapPost("/", async (
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var numVenda = BodyInt(body, "numVenda", "NUM_VENDA_FK", "NUM_VENDA");
            var nomeUsuario = BodyString(body, "nomeUsuario", "NOME_USUARIO_FK", "NOME");
            if (numVenda is null)
            {
                return Results.BadRequest(new { error = "NUM_VENDA_FK e obrigatorio." });
            }

            if (string.IsNullOrWhiteSpace(nomeUsuario))
            {
                return Results.BadRequest(new { error = "NOME_USUARIO_FK e obrigatorio." });
            }

            var result = await handler.HandleAsync(
                new LegacySqlCommand("Responsaveis.Upsert", new Dictionary<string, object?>
                {
                    ["numVenda"] = numVenda.Value,
                    ["nomeUsuario"] = nomeUsuario.Trim(),
                    ["adminUserCode"] = BodyString(body, "adminUserCode", "ADMIN_USER_CODE", "userCodeAdmin"),
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return Results.Json(new { data = result.Data }, statusCode: StatusCodes.Status201Created);
        });
        
        group.MapPut("/{numVenda:int}", async (
            int numVenda,
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var nomeUsuario = BodyString(body, "nomeUsuario", "NOME_USUARIO_FK", "NOME");
            if (string.IsNullOrWhiteSpace(nomeUsuario))
            {
                return Results.BadRequest(new { error = "NOME_USUARIO_FK e obrigatorio." });
            }

            var result = await handler.HandleAsync(
                new LegacySqlCommand("Responsaveis.Upsert", new Dictionary<string, object?>
                {
                    ["numVenda"] = numVenda,
                    ["nomeUsuario"] = nomeUsuario.Trim(),
                    ["adminUserCode"] = BodyString(body, "adminUserCode", "ADMIN_USER_CODE", "userCodeAdmin"),
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return Results.Ok(new { data = result.Data });
        });
        
        group.MapDelete("/{numVenda:int}", async (
            int numVenda,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Responsaveis.Delete", new Dictionary<string, object?> { ["numVenda"] = numVenda }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return (result.RowsAffected ?? 0) > 0 ? Results.Ok(new { success = true }) : Results.NotFound();
        });
    }

    private static void MapKanban(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/kanban-status").WithTags("Kanban");
        group.MapGet("/", Query("KanbanStatus.List"));
        
        group.MapPost("/", async (
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var numVenda = BodyInt(body, "numVenda", "NUM_VENDA_FK", "NUM_VENDA");
            var proximaAcao = BodyString(body, "proximaAcao", "PROXIMA_ACAO", "dataProximaAcao");
            var status = NormalizeKanbanStatus(BodyString(body, "status", "STATUS"));
            var statusData = NormalizeDateOnly(BodyString(body, "statusDate", "STATUS_DATA", "dataStatus"));
            if (numVenda is null)
            {
                return Results.BadRequest(new { error = "NUM_VENDA_FK e obrigatorio." });
            }

            if (string.IsNullOrWhiteSpace(proximaAcao))
            {
                return Results.BadRequest(new { error = "PROXIMA_ACAO e obrigatorio." });
            }

            if (status is null)
            {
                return Results.BadRequest(new { error = "STATUS invalido." });
            }

            if (statusData is null)
            {
                return Results.BadRequest(new { error = "STATUS_DATA e obrigatorio." });
            }

            var result = await handler.HandleAsync(
                new LegacySqlCommand("KanbanStatus.Upsert", new Dictionary<string, object?>
                {
                    ["numVenda"] = numVenda.Value,
                    ["proximaAcao"] = proximaAcao.Trim().Replace('T', ' ').Replace("Z", string.Empty).Split('.')[0],
                    ["status"] = status,
                    ["statusData"] = statusData,
                    ["nomeUsuario"] = BodyString(body, "nomeUsuario", "NOME_USUARIO_FK", "NOME_USUARIO"),
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return Results.Ok(new { data = result.Data });
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

        // GET /dashboard/baixa/motivos?meses=12
        group.MapGet("/baixa/motivos", async (
            int? meses,
            [FromServices] IQueryHandler<GetMotivosBaixaQuery, IReadOnlyList<MotivoBaixaDto>> handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(new GetMotivosBaixaQuery(meses ?? 12), ct);
                return Results.Ok(new { data = result });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // GET /dashboard/baixa/comparativo-mensal?meses=12
        group.MapGet("/baixa/comparativo-mensal", async (
            int? meses,
            [FromServices] IQueryHandler<GetNegativacoesVsBaixasQuery, IReadOnlyList<NegativacaoBaixaMensalDto>> handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(new GetNegativacoesVsBaixasQuery(meses ?? 12), ct);
                return Results.Ok(new { data = result });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/{metric}", async (
            string metric,
            string? dataInicio,
            string? dataFim,
            int? limit,
            string? faixa,
            string? score,
            string? qtd,
            string? nomeUsuario,
            [FromServices] IQueryHandler<GetMetricQuery, IReadOnlyList<Dictionary<string, object?>>> handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(new GetMetricQuery(metric, dataInicio, dataFim, limit, faixa, score, qtd, nomeUsuario), ct);
                object? data = IsSingleDashboardMetric(metric) ? result.FirstOrDefault() : result;
                return Results.Ok(new { data });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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
            [FromServices] IQueryHandler<LegacySqlQuery, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var normalizedPage = Math.Max(page ?? 1, 1);
            var normalizedPageSize = Math.Clamp(pageSize ?? 20, 1, 100);
            var normalizedUsername = NormalizeUsername(username);
            var parameters = NotificationParameters(normalizedUsername, normalizedPage, normalizedPageSize, lida);
            var result = await handler.HandleAsync(new LegacySqlQuery("Notifications.List", parameters), cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var rows = ResultRows(result);
            var total = rows.Count > 0 ? Convert.ToInt32(rows[0].GetValueOrDefault("Total") ?? 0) : 0;
            var unreadCount = rows.Count > 0 ? Convert.ToInt32(rows[0].GetValueOrDefault("UnreadCount") ?? 0) : 0;
            var notifications = rows.Select(MapNotificationRow).ToList();
            return Results.Ok(new
            {
                notifications,
                total,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                unreadCount,
            });
        });

        group.MapPut("/read-all", async (
            string username,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            [FromServices] SseHub sseHub,
            CancellationToken cancellationToken) =>
        {
            var normalizedUsername = NormalizeUsername(username);
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Notifications.MarkAllRead", new Dictionary<string, object?> { ["username"] = normalizedUsername }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            await sseHub.BroadcastUpdateAsync(username, NotificationSsePayloadMapper.ToReadAllPayload(), cancellationToken);

            return Results.Ok(new { success = true, markedAsRead = result.RowsAffected ?? 0 });
        });

        group.MapPut("/{id:guid}/read", async (
            Guid id,
            string username,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            [FromServices] INotificationRepository notificationRepository,
            [FromServices] SseHub sseHub,
            CancellationToken cancellationToken) =>
        {
            var normalizedUsername = NormalizeUsername(username);
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Notifications.MarkRead", new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["username"] = normalizedUsername,
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
            if (notification is not null)
            {
                await sseHub.BroadcastUpdateAsync(username, NotificationSsePayloadMapper.ToUpdatePayload(notification), cancellationToken);
            }

            return (result.RowsAffected ?? 0) > 0 ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            string username,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            [FromServices] INotificationRepository notificationRepository,
            [FromServices] SseHub sseHub,
            CancellationToken cancellationToken) =>
        {
            var normalizedUsername = NormalizeUsername(username);
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Notifications.Delete", new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["username"] = normalizedUsername,
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
            if (notification is not null)
            {
                await sseHub.BroadcastUpdateAsync(
                    username,
                    NotificationSsePayloadMapper.ToUpdatePayload(notification, deletedAt: DateTimeOffset.UtcNow),
                    cancellationToken);
            }

            return (result.RowsAffected ?? 0) > 0 ? Results.Ok(new { success = true }) : Results.NotFound();
        });
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

        group.MapGet("/negativacoes/{id}", async (
            string id,
            [FromServices] IQueryHandler<GetNegativacaoByIdQuery, SerasaPefinDetalheDto?> handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var parsedId))
            {
                return Results.Json(new { error = "ID_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await handler.HandleAsync(new GetNegativacaoByIdQuery(parsedId), cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { data = result });
        });

        // Webhooks - 6 endpoints for Serasa PEFIN callbacks.
        // IMPORTANT: Serasa sends raw JSON objects (e.g. {"uuid":"..."}). Using
        // [FromBody] string here would require a JSON-string literal body and would
        // reject Serasa's payload with HTTP 400. We read the request body directly
        // to preserve the raw JSON for persistence/audit and to deserialize in the handler.
        static async Task<string> ReadRawBodyAsync(HttpRequest request, CancellationToken ct)
        {
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            return await reader.ReadToEndAsync(ct);
        }

        group.MapPost("/webhooks/inclusao/sucesso", async (
            HttpRequest request,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rawJson = await ReadRawBodyAsync(request, cancellationToken);
            var result = await handler.HandleAsync(WebhookEventType.Inclusao, WebhookResultado.Sucesso, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/inclusao/erro", async (
            HttpRequest request,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rawJson = await ReadRawBodyAsync(request, cancellationToken);
            var result = await handler.HandleAsync(WebhookEventType.Inclusao, WebhookResultado.Erro, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/avalista/sucesso", async (
            HttpRequest request,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rawJson = await ReadRawBodyAsync(request, cancellationToken);
            var result = await handler.HandleAsync(WebhookEventType.Avalista, WebhookResultado.Sucesso, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/avalista/erro", async (
            HttpRequest request,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rawJson = await ReadRawBodyAsync(request, cancellationToken);
            var result = await handler.HandleAsync(WebhookEventType.Avalista, WebhookResultado.Erro, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/baixa/sucesso", async (
            HttpRequest request,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rawJson = await ReadRawBodyAsync(request, cancellationToken);
            var result = await handler.HandleAsync(WebhookEventType.Baixa, WebhookResultado.Sucesso, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });

        group.MapPost("/webhooks/baixa/erro", async (
            HttpRequest request,
            [FromServices] SerasaWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rawJson = await ReadRawBodyAsync(request, cancellationToken);
            var result = await handler.HandleAsync(WebhookEventType.Baixa, WebhookResultado.Erro, rawJson, cancellationToken);
            return Results.Ok(new { processed = true, alreadyProcessed = result.WasAlreadyProcessed });
        });
    }

    private static void MapPlannedOperationalEndpoints(IEndpointRouteBuilder app)
    {
        var ocorrencias = app.MapGroup("/ocorrencias").WithTags("Ocorrencias");
        ocorrencias.MapGet("/", Query("Ocorrencia.List"));
        ocorrencias.MapGet("/{id:guid}", Query("Ocorrencia.GetById", single: true));
        ocorrencias.MapGet("/num-venda/{numVenda:int}", Query("Ocorrencia.ByNumVenda"));
        ocorrencias.MapGet("/protocolo/{protocolo}", Query("Ocorrencia.ByProtocolo"));
        
        ocorrencias.MapPost("/", async (
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryBuildOcorrenciaParameters(body, Guid.NewGuid(), out var parameters, out var error))
            {
                return error!;
            }

            var result = await handler.HandleAsync(new LegacySqlCommand("Ocorrencia.Insert", parameters), cancellationToken);
            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return Results.Json(new { data = result.Data }, statusCode: StatusCodes.Status201Created);
        });
        
        ocorrencias.MapPut("/{id:guid}", async (
            Guid id,
            JsonElement body,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryBuildOcorrenciaParameters(body, id, out var parameters, out var error))
            {
                return error!;
            }

            var result = await handler.HandleAsync(new LegacySqlCommand("Ocorrencia.Update", parameters), cancellationToken);
            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return result.Data is null ? Results.NotFound() : Results.Ok(new { data = result.Data });
        });
        
        ocorrencias.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new LegacySqlCommand("Ocorrencia.Delete", new Dictionary<string, object?> { ["id"] = id }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            return (result.RowsAffected ?? 0) > 0 ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        var atendimentos = app.MapGroup("/atendimentos").WithTags("Atendimentos");
        
        atendimentos.MapPost("/", async (
            JsonElement body,
            [FromServices] IQueryHandler<LegacySqlQuery, LegacySqlResult> queryHandler,
            [FromServices] ICommandHandler<LegacySqlCommand, LegacySqlResult> commandHandler,
            CancellationToken cancellationToken) =>
        {
            var numVenda = BodyInt(body, "numVenda", "NUM_VENDA", "NUM_VENDA_FK");
            if (numVenda is null)
            {
                return Results.BadRequest(new { error = "NUM_VENDA_FK e obrigatorio." });
            }

            var vendaResult = await queryHandler.HandleAsync(
                new LegacySqlQuery("Inadimplencia.ByNumVenda", new Dictionary<string, object?> { ["numVenda"] = numVenda.Value }, Single: true),
                cancellationToken);
            if (!vendaResult.IsConfigured)
            {
                return SqlNotConfigured();
            }

            if (vendaResult.Data is not Dictionary<string, object?> venda)
            {
                return Results.NotFound(new { error = "Venda nao encontrada." });
            }

            var responsavelResult = await queryHandler.HandleAsync(
                new LegacySqlQuery("Responsaveis.ByNumVenda", new Dictionary<string, object?> { ["numVenda"] = numVenda.Value }, Single: true),
                cancellationToken);
            var responsavel = responsavelResult.Data as Dictionary<string, object?>;
            var snapshot = new Dictionary<string, object?>(venda, StringComparer.OrdinalIgnoreCase)
            {
                ["RESPONSAVEL"] = responsavel?.GetValueOrDefault("NOME_USUARIO_FK"),
                ["NOME_USUARIO_FK"] = responsavel?.GetValueOrDefault("NOME_USUARIO_FK"),
                ["COR_HEX"] = responsavel?.GetValueOrDefault("COR_HEX"),
                ["RESPONSAVEL_COR_HEX"] = responsavel?.GetValueOrDefault("COR_HEX"),
            };

            var result = await commandHandler.HandleAsync(
                new LegacySqlCommand("Atendimento.CreateFromVenda", new Dictionary<string, object?>
                {
                    ["numVenda"] = numVenda.Value,
                    ["cpfCnpj"] = snapshot.GetValueOrDefault("CPF_CNPJ"),
                    ["cliente"] = snapshot.GetValueOrDefault("CLIENTE"),
                    ["empreendimento"] = snapshot.GetValueOrDefault("EMPREENDIMENTO"),
                    ["dadosVenda"] = JsonSerializer.Serialize(snapshot),
                }),
                cancellationToken);

            if (!result.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var atendimento = AttachAtendimentoSnapshot(result.Data as Dictionary<string, object?>);
            if (Convert.ToBoolean(atendimento.GetValueOrDefault("ATENDIMENTO_ATIVO") ?? false))
            {
                var nomeResponsavel = atendimento.GetValueOrDefault("RESPONSAVEL") ?? atendimento.GetValueOrDefault("NOME_USUARIO_FK");
                return Results.Conflict(new
                {
                    error = nomeResponsavel is null
                        ? "Ja existe atendimento em andamento para esta venda."
                        : $"Ja existe atendimento em andamento por {nomeResponsavel}.",
                });
            }

            return Results.Json(new { data = atendimento }, statusCode: StatusCodes.Status201Created);
        });
        
        atendimentos.MapGet("/cpf/{cpf}", Query(
            "Atendimento.ByCpf",
            routeValues => new Dictionary<string, object?> { ["cpf"] = DigitsOnly(routeValues["cpf"]?.ToString()) }));
        atendimentos.MapGet("/num-venda/{numVenda:int}", Query("Atendimento.ByNumVenda"));
        atendimentos.MapGet("/protocolo/{protocolo}", async (
            string protocolo,
            [FromServices] IQueryHandler<LegacySqlQuery, LegacySqlResult> handler,
            CancellationToken ct) =>
        {
            var atendimentoResult = await handler.HandleAsync(
                new LegacySqlQuery("Atendimento.ByProtocolo", new Dictionary<string, object?> { ["protocolo"] = protocolo.Trim() }, Single: true),
                ct);

            if (!atendimentoResult.IsConfigured)
            {
                return SqlNotConfigured();
            }

            var atendimento = AttachAtendimentoSnapshot(atendimentoResult.Data as Dictionary<string, object?>);
            if (atendimento.Count == 0)
            {
                return Results.NotFound();
            }

            var ocorrenciasResult = await handler.HandleAsync(
                new LegacySqlQuery("Ocorrencia.ByProtocolo", new Dictionary<string, object?> { ["protocolo"] = protocolo.Trim() }),
                ct);
            var ocorrencias = ResultRows(ocorrenciasResult);
            return Results.Ok(new
            {
                data = new
                {
                    atendimento,
                    venda = atendimento.GetValueOrDefault("VENDA_SNAPSHOT"),
                    ocorrencias,
                },
            });
        });
        atendimentos.MapGet("/cliente/{nomeCliente}", Query("Atendimento.ByCliente"));

        var relatorios = app.MapGroup("/relatorios").WithTags("Relatorios");
        relatorios.MapGet("/ficha-financeira", async (
            int numVenda,
            int? codColigada,
            int? reportColigada,
            int? reportId,
            [FromServices] ICommandHandler<
                ApiInadimplencia.Application.Features.Relatorios.Dtos.GenerateFichaFinanceiraCommand,
                string> handler,
            [FromServices] IOptions<ApiInadimplencia.Application.Configuration.RmOptions> rmOptions,
            CancellationToken cancellationToken) =>
        {
            if (numVenda <= 0)
            {
                return Results.BadRequest(new { error = "numVenda é obrigatório." });
            }

            try
            {
                var command = new ApiInadimplencia.Application.Features.Relatorios.Dtos.GenerateFichaFinanceiraCommand(
                    NumVenda: numVenda,
                    CodColigada: codColigada,
                    ReportColigada: reportColigada,
                    ReportId: reportId);

                var url = await handler.HandleAsync(command, cancellationToken);
                var rm = rmOptions.Value;
                return Results.Ok(new
                {
                    url,
                    numVenda,
                    codColigada = codColigada ?? rm.ParamColigada,
                    reportColigada = reportColigada ?? rm.ReportColigada,
                    reportId = reportId ?? rm.ReportId,
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                // Configuration or upstream (Fluig/RM) failure → 502 Bad Gateway.
                return Results.Problem(
                    title: "Falha ao gerar ficha financeira",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
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

            if (single && result.Data is null)
            {
                return Results.NotFound(new
                {
                    error = "NAO_ENCONTRADO",
                    query = queryKey,
                });
            }

            return Results.Ok(new { data = result.Data });
        }
        catch (NotImplementedException ex)
        {
            return NotMigrated(queryKey, ex.Message);
        }
    }

    private static IReadOnlyDictionary<string, object?> NotificationParameters(
        string username,
        int page,
        int pageSize,
        bool? lida)
        => new Dictionary<string, object?>
        {
            ["username"] = username,
            ["page"] = page,
            ["pageSize"] = pageSize,
            ["offset"] = (page - 1) * pageSize,
            ["lida"] = lida,
        };

    private static IReadOnlyDictionary<string, object?> PagedParameters(int page, int pageSize)
        => new Dictionary<string, object?>
        {
            ["pageSize"] = pageSize,
            ["offset"] = (page - 1) * pageSize,
        };

    private static object PagedResponse(PagedInadimplenciaResult result)
        => new
        {
            data = result.Items,
            page = result.Page,
            pageSize = result.PageSize,
            total = result.Total,
            totalPages = result.TotalPages,
        };

    private static int NormalizePage(int? page)
        => Math.Max(page ?? 1, 1);

    private static int NormalizePageSize(int? pageSize)
        => Math.Clamp(pageSize ?? 50, 1, 200);

    private static int TotalPages(int total, int pageSize)
        => pageSize <= 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);

    /// <summary>
    /// Normalizes the username to match how <c>InadNotificacao.Criar</c> persists it
    /// (lowercase, trimmed). Without this, legacy SQL queries against USUARIO_DESTINATARIO
    /// can return zero matches when the caller uses different casing/whitespace and the
    /// SQL Server collation is case-sensitive, surfacing as spurious 404 responses on
    /// PUT /notifications/{id}/read and DELETE /notifications/{id}.
    /// </summary>
    private static string NormalizeUsername(string? username)
        => (username ?? string.Empty).Trim().ToLowerInvariant();

    private static IReadOnlyList<Dictionary<string, object?>> ResultRows(LegacySqlResult result)
        => result.Data as IReadOnlyList<Dictionary<string, object?>>
            ?? result.Data as List<Dictionary<string, object?>>
            ?? [];

    private static Dictionary<string, object?> MapNotificationRow(Dictionary<string, object?> row)
    {
        var payload = ParseNotificationPayload(row.GetValueOrDefault("PAYLOAD"));
        var tipo = StringValue(row.GetValueOrDefault("TIPO"));

        return new Dictionary<string, object?>
        {
            ["id"] = StringValue(row.GetValueOrDefault("ID")),
            ["tipo"] = tipo,
            ["type"] = string.Equals(tipo, "VENDA_ATRIBUIDA", StringComparison.OrdinalIgnoreCase) ? "assignment" : "sale_overdue",
            ["numVenda"] = row.GetValueOrDefault("NUM_VENDA"),
            ["cliente"] = payload.GetValueOrDefault("cliente"),
            ["cpfCnpj"] = payload.GetValueOrDefault("cpfCnpj"),
            ["empreendimento"] = payload.GetValueOrDefault("empreendimento"),
            ["valorInadimplente"] = payload.GetValueOrDefault("valorInadimplente"),
            ["score"] = row.GetValueOrDefault("SCORE") ?? payload.GetValueOrDefault("score"),
            ["responsavel"] = payload.GetValueOrDefault("responsavel"),
            ["proximaAcao"] = IsoString(row.GetValueOrDefault("PROXIMA_ACAO")) ?? payload.GetValueOrDefault("proximaAcao"),
            ["status"] = payload.GetValueOrDefault("status") ?? payload.GetValueOrDefault("statusKanban"),
            ["adminUserCode"] = row.GetValueOrDefault("ORIGEM_USUARIO"),
            ["lida"] = Convert.ToBoolean(row.GetValueOrDefault("LIDA") ?? false),
            ["createdAt"] = IsoString(row.GetValueOrDefault("DT_CRIACAO")),
            ["readAt"] = IsoString(row.GetValueOrDefault("DT_LEITURA")),
            ["deletedAt"] = IsoString(row.GetValueOrDefault("DT_EXCLUSAO")),
        };
    }

    private static IResult SqlNotConfigured()
        => Results.Problem(
            title: "SQL Server nao configurado",
            detail: "Configure SqlServer:ConnectionString ou a env var SqlServer__ConnectionString para habilitar endpoints de dados.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static Dictionary<string, object?> ParseNotificationPayload(object? raw)
    {
        if (raw is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(raw.ToString() ?? "{}");
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = JsonElementValue(property.Value);
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static object? JsonElementValue(System.Text.Json.JsonElement element)
        => element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            System.Text.Json.JsonValueKind.Number when element.TryGetDecimal(out var dec) => dec,
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    private static string? StringValue(object? value)
        => value?.ToString();

    private static string? IsoString(object? value)
        => value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            _ => value.ToString(),
        };

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

    private static bool IsSingleDashboardMetric(string metric)
        => string.Equals(metric, "acoes-definidas", StringComparison.OrdinalIgnoreCase);

    private static string? BodyString(JsonElement body, params string[] names)
    {
        foreach (var name in names)
        {
            if (!body.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.ToString();
        }

        return null;
    }

    private static int? BodyInt(JsonElement body, params string[] names)
    {
        var value = BodyString(body, names);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool? BodyBool(JsonElement body, params string[] names)
    {
        foreach (var name in names)
        {
            if (!body.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when property.TryGetInt32(out var number) => number != 0,
                JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
                JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number != 0,
                _ => null,
            };
        }

        return null;
    }

    private static string? NormalizePerfil(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "admin" or "operador" ? normalized : null;
    }

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!normalized.StartsWith('#'))
        {
            normalized = $"#{normalized}";
        }

        return Regex.IsMatch(normalized, "^#[0-9a-fA-F]{6}$") ? normalized : null;
    }

    private static string? NormalizeKanbanStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "todo" or "a fazer" or "a_fazer" or "a-fazer" => "todo",
            "inprogress" or "em andamento" or "em atendimento" => "inProgress",
            "done" or "concluido" or "concluído" => "done",
            _ => null,
        };
    }

    private static string? NormalizeDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length >= 10 && Regex.IsMatch(text[..10], "^\\d{4}-\\d{2}-\\d{2}$"))
        {
            return text[..10];
        }

        return DateTime.TryParse(text.Replace(' ', 'T'), out var parsed)
            ? parsed.ToString("yyyy-MM-dd")
            : null;
    }

    private static bool TryBuildOcorrenciaParameters(
        JsonElement body,
        Guid id,
        out Dictionary<string, object?> parameters,
        out IResult? error)
    {
        parameters = [];
        error = null;

        var numVenda = BodyInt(body, "numVenda", "NUM_VENDA_FK", "NUM_VENDA");
        var nomeUsuario = BodyString(body, "nomeUsuario", "NOME_USUARIO_FK", "nome_usuario_fk");
        var descricao = BodyString(body, "descricao", "DESCRICAO");
        var statusOcorrencia = BodyString(body, "statusOcorrencia", "STATUS_OCORRENCIA", "status_ocorrencia", "status", "STATUS");
        var dtOcorrencia = NormalizeDateOnly(BodyString(body, "dtOcorrencia", "DT_OCORRENCIA", "dataOcorrencia", "DATA_OCORRENCIA"));
        var horaOcorrencia = BodyString(body, "horaOcorrencia", "HORA_OCORRENCIA", "hora", "HORA");

        if (numVenda is null)
        {
            error = Results.BadRequest(new { error = "NUM_VENDA_FK e obrigatorio." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(nomeUsuario))
        {
            error = Results.BadRequest(new { error = "NOME_USUARIO_FK e obrigatorio." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            error = Results.BadRequest(new { error = "DESCRICAO e obrigatoria." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(statusOcorrencia))
        {
            error = Results.BadRequest(new { error = "STATUS_OCORRENCIA e obrigatorio." });
            return false;
        }

        if (dtOcorrencia is null)
        {
            error = Results.BadRequest(new { error = "DT_OCORRENCIA e obrigatoria." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(horaOcorrencia)
            || !Regex.IsMatch(horaOcorrencia, "^([01]\\d|2[0-3]):([0-5]\\d)(:([0-5]\\d))?$"))
        {
            error = Results.BadRequest(new { error = "HORA_OCORRENCIA invalida." });
            return false;
        }

        parameters = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["numVenda"] = numVenda.Value,
            ["nomeUsuario"] = nomeUsuario.Trim(),
            ["descricao"] = descricao.Trim(),
            ["statusOcorrencia"] = statusOcorrencia.Trim(),
            ["dtOcorrencia"] = dtOcorrencia,
            ["horaOcorrencia"] = horaOcorrencia.Trim(),
            ["proximaAcao"] = BodyString(body, "proximaAcao", "PROXIMA_ACAO"),
            ["protocolo"] = BodyString(body, "protocolo", "PROTOCOLO", "protocolo_fk", "PROTOCOLO_FK"),
        };

        return true;
    }

    private static Dictionary<string, object?> AttachAtendimentoSnapshot(Dictionary<string, object?>? row)
    {
        if (row is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var copy = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
        var snapshot = ParseNotificationPayload(copy.GetValueOrDefault("DADOS_VENDA"));
        copy["VENDA_SNAPSHOT"] = snapshot.Count > 0 ? snapshot : null;

        if (!copy.ContainsKey("RESPONSAVEL") && snapshot.TryGetValue("RESPONSAVEL", out var responsavel))
        {
            copy["RESPONSAVEL"] = responsavel;
        }

        return copy;
    }

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

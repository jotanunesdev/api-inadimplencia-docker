using Microsoft.AspNetCore.Mvc;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;
using ApiInadimplencia.Application.Features.Negativacao.Baixa.Queries;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.Negativacao.Queries;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using System.Text.Json.Serialization;

namespace ApiInadimplencia.Api.Endpoints;

/// <summary>
/// Maps the negativacao fluxo HTTP contract (solicitação → aprovação → envio Serasa).
/// </summary>
public static class NegativacaoFluxoEndpoints
{
    /// <summary>
    /// Adds negativacao fluxo endpoints to the application.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapNegativacaoFluxoEndpoints(this IEndpointRouteBuilder app)
    {
        // Registrado em dois prefixos para suportar tanto chamadas diretas (/negativacao/...)
        // quanto chamadas via proxy apifluig.jotanunes.com/inadimplencia/... (Sophos).
        MapNegativacaoGroup(app.MapGroup("/negativacao").WithTags("Negativacao Fluxo"), "");
        MapNegativacaoGroup(app.MapGroup("/inadimplencia/negativacao").WithTags("Negativacao Fluxo"), "Legacy");

        return app;
    }

    private static void MapNegativacaoGroup(RouteGroupBuilder negativacao, string nameSuffix)
    {
        // GET /negativacao/vendas/{numVenda}/dividas - List eligible debts for a sale
        negativacao.MapGet("/vendas/{numVenda}/dividas", async (
            string numVenda,
            [FromServices] IQueryHandler<GetDividasElegiveisQuery, DividasElegiveisResponse> handler,
            CancellationToken cancellationToken) =>
        {
            if (!int.TryParse(numVenda, out var parsedNumVenda))
            {
                return Results.Json(new { error = "NUM_VENDA_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await handler.HandleAsync(new GetDividasElegiveisQuery(parsedNumVenda), cancellationToken);
            return Results.Ok(new { data = result });
        })
        .WithName($"GetDividasElegiveis{nameSuffix}")
        .WithOpenApi();

        // GET /negativacao/parcelas/idlan/{idLan} - RM integration lookup by IDLAN.
        // Auth: o middleware permite bypass quando o header "rm: true" estiver presente.
        negativacao.MapGet("/parcelas/idlan/{idLan}", async (
            string idLan,
            [FromServices] IInadimplenciaQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            if (!long.TryParse(idLan, out var parsedIdLan) || parsedIdLan <= 0)
            {
                return Results.Json(new { error = "IDLAN_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
            }

            var parcela = await queryService.GetParcelaByIdLanAsync(parsedIdLan, cancellationToken);
            if (parcela is null)
            {
                return Results.NotFound(new { error = "IDLAN_NAO_ENCONTRADO", idLan = parsedIdLan });
            }

            return Results.Ok(new
            {
                idLan = parcela.IdLan,
                numVenda = parcela.NumVenda,
                numeroDocumento = parcela.NumeroDocumento,
                dataVencimento = parcela.DataVencimento.ToString("yyyy-MM-dd"),
                valor = parcela.Valor,
                inadimplente = parcela.Inadimplente,
                negativado = parcela.Negativado,
                diasAtraso = parcela.DiasAtraso,
            });
        })
        .WithName($"GetParcelaByIdLan{nameSuffix}")
        .WithOpenApi();

        // POST /negativacao/solicitacoes - Request a new negativacao solicitation
        negativacao.MapPost("/solicitacoes", async (
            [FromBody] RequestNegativacaoFluxoCommand command,
            [FromServices] ICommandHandler<RequestNegativacaoFluxoCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var solicitacaoId = await handler.HandleAsync(command, cancellationToken);
                return Results.Created($"/negativacao/solicitacoes/{solicitacaoId}", new { solicitacaoId });
            }
            catch (SerasaPefinDuplicateActiveException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JA_EM_APROVACAO", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName($"RequestNegativacaoSolicitacao{nameSuffix}")
        .WithOpenApi();

        // GET /negativacao/solicitacoes - List pending solicitations
        negativacao.MapGet("/solicitacoes", async (
            [FromQuery] string? status,
            [FromQuery] int? numVenda,
            [FromQuery] Guid? solicitacaoId,
            [FromQuery] string? solicitanteUsername,
            [FromQuery] int? take,
            [FromQuery] int? skip,
            [FromServices] IQueryHandler<ListSolicitacoesPendentesQuery, IReadOnlyList<SolicitacaoPendenteDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new ListSolicitacoesPendentesQuery(
                status ?? "AGUARDANDO_APROVACAO",
                numVenda,
                solicitacaoId,
                solicitanteUsername,
                take ?? 50,
                skip ?? 0);
            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(new { data = result });
        })
        .WithName($"ListSolicitacoesPendentes{nameSuffix}")
        .WithOpenApi();

        // GET /negativacao/solicitacoes/{id} - Get a specific solicitation by ID with full details
        negativacao.MapGet("/solicitacoes/{id}", async (
            string id,
            [FromQuery] string? username,
            [FromServices] IQueryHandler<GetSolicitacaoByIdQuery, SolicitacaoDetalheDto?> handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var parsedId))
            {
                return Results.Json(new { error = "ID_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await handler.HandleAsync(new GetSolicitacaoByIdQuery(parsedId, username), cancellationToken);
            return result is null ? Results.NotFound(new { error = "NAO_ENCONTRADA" }) : Results.Ok(result);
        })
        .WithName($"GetSolicitacaoById{nameSuffix}")
        .WithOpenApi();

        // POST /negativacao/solicitacoes/{id}/decisao - Decide (approve/reject) a pending solicitation
        negativacao.MapPost("/solicitacoes/{id}/decisao", async (
            string id,
            [FromBody] DecideNegativacaoRequest request,
            [FromServices] ICommandHandler<DecideNegativacaoCommand, bool> handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!Guid.TryParse(id, out var parsedId))
                {
                    return Results.Json(new { error = "ID_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
                }

                var command = new DecideNegativacaoCommand(
                    parsedId,
                    request.Decisao,
                    request.SenhaTransacao,
                    request.Justificativa);
                await handler.HandleAsync(command, cancellationToken);
                return Results.Ok(new { message = "Decisão registrada com sucesso." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName($"DecideNegativacaoSolicitacao{nameSuffix}")
        .WithOpenApi();

        // -------------------------------------------------------------
        // Sub-grupo /baixa/...
        // -------------------------------------------------------------
        var baixa = negativacao.MapGroup("/baixa").WithTags("Baixa Fluxo");

        // POST /baixa/solicitacoes - Solicitar baixa para 1+ parcelas.
        baixa.MapPost("/solicitacoes", async (
            [FromBody] RequestBaixaCommand command,
            [FromServices] ICommandHandler<RequestBaixaCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var solicitacaoId = await handler.HandleAsync(command, cancellationToken);
                return Results.Created($"/negativacao/baixa/solicitacoes/{solicitacaoId}", new { solicitacaoId });
            }
            catch (SerasaPefinBaixaDuplicateActiveException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JA_EM_APROVACAO", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName($"RequestBaixaSolicitacao{nameSuffix}")
        .WithOpenApi();

        // GET /baixa/solicitacoes/{id}
        baixa.MapGet("/solicitacoes/{id}", async (
            string id,
            [FromServices] IQueryHandler<GetBaixaByIdQuery, BaixaDetalheDto?> handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var parsedId))
            {
                return Results.Json(new { error = "ID_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await handler.HandleAsync(new GetBaixaByIdQuery(parsedId), cancellationToken);
            return result is null
                ? Results.NotFound(new { error = "NAO_ENCONTRADA" })
                : Results.Ok(result);
        })
        .WithName($"GetBaixaById{nameSuffix}")
        .WithOpenApi();

        // GET /baixa/solicitacoes
        baixa.MapGet("/solicitacoes", async (
            [FromQuery] string? status,
            [FromQuery] int? numVenda,
            [FromQuery] string? solicitanteUsername,
            [FromQuery] int? take,
            [FromQuery] int? skip,
            [FromServices] IQueryHandler<ListBaixasQuery, IReadOnlyList<BaixaResumoDto>> handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var query = new ListBaixasQuery(
                    status,
                    numVenda,
                    solicitanteUsername,
                    take ?? 50,
                    skip ?? 0);
                var result = await handler.HandleAsync(query, cancellationToken);
                return Results.Ok(new { data = result });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName($"ListBaixas{nameSuffix}")
        .WithOpenApi();

        // POST /baixa/solicitacoes/{id}/decisao
        baixa.MapPost("/solicitacoes/{id}/decisao", async (
            string id,
            [FromBody] DecideNegativacaoRequest request,
            [FromServices] ICommandHandler<DecideBaixaCommand, bool> handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!Guid.TryParse(id, out var parsedId))
                {
                    return Results.Json(new { error = "ID_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
                }

                var command = new DecideBaixaCommand(
                    parsedId,
                    request.Decisao,
                    request.SenhaTransacao,
                    request.Justificativa);
                await handler.HandleAsync(command, cancellationToken);
                return Results.Ok(new { message = "Decisão de baixa registrada com sucesso." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JA_DECIDIDA", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (SerasaPefinHttpException ex)
            {
                // Decisão foi persistida; falhou apenas o envio downstream ao Serasa.
                // O agregado já está em APROVADA_FALHA_ENVIO (vide SendBaixaToSerasaCommandHandler).
                return Results.Json(
                    new
                    {
                        error = "FALHA_ENVIO_SERASA",
                        message = "Decisão registrada, mas o envio ao Serasa falhou. Use o reenvio.",
                        serasaStatusCode = (int)ex.StatusCode,
                        detail = ex.Message,
                    },
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName($"DecideBaixaSolicitacao{nameSuffix}")
        .WithOpenApi();

        // POST /baixa/solicitacoes/{id}/reenvio
        baixa.MapPost("/solicitacoes/{id}/reenvio", async (
            string id,
            [FromServices] ICommandHandler<ResendBaixaCommand, ResendBaixaResult> handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!Guid.TryParse(id, out var parsedId))
                {
                    return Results.Json(new { error = "ID_INVALIDO" }, statusCode: StatusCodes.Status400BadRequest);
                }

                var result = await handler.HandleAsync(new ResendBaixaCommand(parsedId), cancellationToken);
                return Results.Ok(new
                {
                    solicitacaoId = result.BaixaId,
                    transactionId = result.TransactionId,
                    tentativas = result.Tentativas,
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("LIMITE_TENTATIVAS_ATINGIDO", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("ESTADO_INVALIDO", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (SerasaPefinHttpException ex)
            {
                return Results.Json(
                    new
                    {
                        error = "FALHA_ENVIO_SERASA",
                        message = "Falha ao reenviar a baixa ao Serasa.",
                        serasaStatusCode = (int)ex.StatusCode,
                        detail = ex.Message,
                    },
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName($"ResendBaixaSolicitacao{nameSuffix}")
        .WithOpenApi();
    }
}

public sealed record DecideNegativacaoRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    DecisaoNegativacao Decisao,
    string SenhaTransacao,
    string? Justificativa = null);

using Microsoft.AspNetCore.Mvc;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
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
        var negativacao = app.MapGroup("/negativacao")
            .WithTags("Negativacao Fluxo");

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
        .WithName("GetDividasElegiveis")
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
        .WithName("RequestNegativacaoSolicitacao")
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
        .WithName("ListSolicitacoesPendentes")
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
        .WithName("GetSolicitacaoById")
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
        .WithName("DecideNegativacaoSolicitacao")
        .WithOpenApi();

        return app;
    }
}

public sealed record DecideNegativacaoRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    DecisaoNegativacao Decisao,
    string SenhaTransacao,
    string? Justificativa = null);

using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Features.LoadTesting;

namespace ApiInadimplencia.Api.Endpoints;

/// <summary>
/// Maps API traffic analytics endpoints.
/// </summary>
public static class TrafficMonitoringEndpoints
{
    /// <summary>
    /// Maps traffic monitoring endpoints.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapTrafficMonitoringEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/traffic-monitoring")
            .WithTags("Traffic Monitoring");

        group.MapGet("/dashboard", async (
            int? periodDays,
            string? apiName,
            string? environment,
            bool? excludeLoadTestTraffic,
            ITrafficAnalyticsQuery query,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";

            try
            {
                var dashboard = await query.GetDashboardAsync(
                    periodDays ?? 7,
                    apiName,
                    environment,
                    excludeLoadTestTraffic ?? false,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(dashboard);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Traffic monitoring storage unavailable");
            }
        })
        .WithName("GetTrafficMonitoringDashboard")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/load-tests/profiles", (ILoadTestOrchestrator orchestrator) =>
            Results.Ok(new { profiles = orchestrator.GetProfiles() }))
        .WithName("ListLoadTestProfiles")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/load-tests/runs", async (
            int? limit,
            ILoadTestOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var runs = await orchestrator.ListRunsAsync(limit ?? 25, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { runs });
        })
        .WithName("ListLoadTestRuns")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/load-tests/runs/{runId:guid}", async (
            Guid runId,
            ILoadTestOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var run = await orchestrator.GetRunAsync(runId, cancellationToken).ConfigureAwait(false);
            return run is null
                ? Results.NotFound(new { error = "LOAD_TEST_NOT_FOUND" })
                : Results.Ok(run);
        })
        .WithName("GetLoadTestRun")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/load-tests/runs", async (
            StartLoadTestRequestDto request,
            ILoadTestOrchestrator orchestrator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var triggeredBy = context.User?.Identity?.Name
                    ?? context.Request.Headers["X-User-Name"].FirstOrDefault()
                    ?? context.Request.Headers["X-Username"].FirstOrDefault()
                    ?? "operador";
                var run = await orchestrator.StartAsync(request, triggeredBy, cancellationToken).ConfigureAwait(false);
                return Results.Ok(run);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Unable to start load test");
            }
        })
        .WithName("StartLoadTestRun")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

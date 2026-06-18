using ApiInadimplencia.Application.Abstractions.Monitoring;

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

        return app;
    }
}

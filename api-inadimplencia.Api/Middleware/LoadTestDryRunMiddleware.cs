using ApiInadimplencia.Application.Abstractions.Monitoring;
using Microsoft.AspNetCore.Routing;

namespace ApiInadimplencia.Api.Middleware;

/// <summary>
/// Stops managed load-test requests before handlers that could mutate data or call external systems.
/// </summary>
public sealed class LoadTestDryRunMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>
    /// Returns a successful route-level probe when a valid managed load test requests dry-run mode.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        ILoadTestRequestAuthorizer authorizer)
    {
        if (!IsDryRunRequest(context, authorizer))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var routeEndpoint = context.GetEndpoint() as RouteEndpoint;
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsJsonAsync(new
        {
            dryRun = true,
            method = context.Request.Method,
            endpoint = routeEndpoint?.RoutePattern.RawText ?? context.Request.Path.Value,
        }).ConfigureAwait(false);
    }

    private static bool IsDryRunRequest(
        HttpContext context,
        ILoadTestRequestAuthorizer authorizer)
    {
        if (!string.Equals(
                context.Request.Headers["X-Load-Test-Dry-Run"].ToString(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return authorizer.IsAuthorized(
            context.Request.Headers["X-Load-Test-Key"].ToString());
    }
}

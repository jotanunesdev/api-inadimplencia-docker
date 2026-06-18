using System.Diagnostics;
using System.Security.Claims;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.TrafficMonitoring;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Api.Middleware;

/// <summary>
/// Captures completed HTTP requests for traffic auditing and analytics.
/// </summary>
public sealed class RequestMonitoringMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>
    /// Captures request and response metadata without blocking on database I/O.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="sink">Asynchronous traffic record sink.</param>
    /// <param name="options">Traffic monitoring options.</param>
    /// <param name="environment">Host environment.</param>
    public async Task InvokeAsync(
        HttpContext context,
        ITrafficRequestSink sink,
        IOptions<TrafficMonitoringOptions> options,
        IHostEnvironment environment)
    {
        var settings = options.Value;
        if (!settings.Enabled || IsExcluded(context.Request.Path, settings.ExcludedPathPrefixes))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var requestedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var requestFailed = false;

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch
        {
            requestFailed = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            sink.TryWrite(CreateRecord(
                context,
                settings,
                environment.EnvironmentName,
                requestedAtUtc,
                stopwatch.ElapsedMilliseconds,
                requestFailed));
        }
    }

    private static TrafficRequestRecord CreateRecord(
        HttpContext context,
        TrafficMonitoringOptions options,
        string environment,
        DateTime requestedAtUtc,
        long durationMs,
        bool requestFailed)
    {
        var routeEndpoint = context.GetEndpoint() as RouteEndpoint;
        var normalizedEndpoint = routeEndpoint?.RoutePattern.RawText;
        var rawPath = context.Request.Path.Value ?? "/";

        return new TrafficRequestRecord(
            Guid.NewGuid(),
            requestedAtUtc,
            Limit(context.Request.Method, 10),
            Limit(string.IsNullOrWhiteSpace(normalizedEndpoint) ? rawPath : normalizedEndpoint, 500),
            Limit(rawPath, 1000),
            requestFailed && context.Response.StatusCode < 400
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode,
            durationMs,
            Limit(ResolveUserName(context), 255),
            LimitNullable(context.Connection.RemoteIpAddress?.ToString(), 64),
            Limit(options.ApplicationName, 150),
            Limit(environment, 50),
            LimitNullable(ResolveSourceSystem(context, options.SourceSystemHeader), 150),
            LimitNullable(context.Request.Headers.UserAgent.ToString(), 1000),
            LimitNullable(Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier, 64));
    }

    private static string ResolveUserName(HttpContext context)
    {
        var identityName = context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            return identityName.Trim();
        }

        var claimName = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(claimName))
        {
            return claimName.Trim();
        }

        foreach (var headerName in new[] { "X-Username", "X-User-Name", "X-User-Code" })
        {
            var headerValue = context.Request.Headers[headerName].ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.Trim();
            }
        }

        return "anonymous";
    }

    private static string? ResolveSourceSystem(HttpContext context, string configuredHeader)
    {
        foreach (var headerName in new[] { configuredHeader, "X-System-Name", "X-Client-Id" })
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                continue;
            }

            var headerValue = context.Request.Headers[headerName].ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.Trim();
            }
        }

        return null;
    }

    private static bool IsExcluded(PathString path, IEnumerable<string> excludedPrefixes)
        => excludedPrefixes.Any(prefix =>
            !string.IsNullOrWhiteSpace(prefix)
            && path.StartsWithSegments(prefix.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string Limit(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];

    private static string? LimitNullable(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value) ? null : Limit(value.Trim(), maximumLength);
}

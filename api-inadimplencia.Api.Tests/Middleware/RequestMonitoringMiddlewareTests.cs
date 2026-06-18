using System.Security.Claims;
using ApiInadimplencia.Api.Middleware;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.TrafficMonitoring;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace api_inadimplencia.Api.Tests.Middleware;

public sealed class RequestMonitoringMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CapturesNormalizedRouteAndAuthenticatedUser()
    {
        var sink = new RecordingSink();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/inadimplencia/clientes/123";
        context.Request.Headers["X-Source-System"] = "crm";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.15");
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/inadimplencia/clientes/{id:int}"),
            0,
            EndpointMetadataCollection.Empty,
            "cliente"));

        var middleware = new RequestMonitoringMiddleware(nextContext =>
        {
            nextContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "maria.silva")],
                "test"));
            nextContext.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            sink,
            Options.Create(CreateOptions()),
            new TestHostEnvironment());

        var record = Assert.Single(sink.Records);
        Assert.Equal("/inadimplencia/clientes/{id:int}", record.Endpoint);
        Assert.Equal("/inadimplencia/clientes/123", record.RawPath);
        Assert.Equal("maria.silva", record.UserName);
        Assert.Equal("crm", record.SourceSystem);
        Assert.Equal("10.0.0.15", record.SourceIp);
        Assert.Equal(200, record.StatusCode);
        Assert.Equal("Testing", record.Environment);
    }

    [Fact]
    public async Task InvokeAsync_RecordsUnhandledExceptionAsStatus500()
    {
        var sink = new RecordingSink();
        var context = new DefaultHttpContext();
        context.Request.Path = "/inadimplencia/falha";
        var middleware = new RequestMonitoringMiddleware(_ =>
            throw new InvalidOperationException("failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(
                context,
                sink,
                Options.Create(CreateOptions()),
                new TestHostEnvironment()));

        Assert.Equal(500, Assert.Single(sink.Records).StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCaptureExcludedPath()
    {
        var sink = new RecordingSink();
        var context = new DefaultHttpContext();
        context.Request.Path = "/traffic-monitoring/dashboard";
        var middleware = new RequestMonitoringMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(
            context,
            sink,
            Options.Create(CreateOptions()),
            new TestHostEnvironment());

        Assert.Empty(sink.Records);
    }

    private static TrafficMonitoringOptions CreateOptions() => new()
    {
        ApplicationName = "api-test",
        ExcludedPathPrefixes = ["/traffic-monitoring"],
    };

    private sealed class RecordingSink : ITrafficRequestSink
    {
        public List<TrafficRequestRecord> Records { get; } = [];

        public bool TryWrite(TrafficRequestRecord record)
        {
            Records.Add(record);
            return true;
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "api-test";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}

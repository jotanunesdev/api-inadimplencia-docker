using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Infrastructure.Notifications;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;

namespace api_inadimplencia.Api.Endpoints;

/// <summary>
/// SSE endpoints for real-time notification streaming.
/// </summary>
public static class NotificationsSseEndpoints
{
    /// <summary>
    /// Maps SSE endpoints.
    /// </summary>
    public static void MapNotificationsSseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/stream", StreamNotificationsAsync)
            .WithName("StreamNotifications")
            .WithOpenApi()
            .WithSummary("Stream notifications via SSE")
            .WithDescription("Opens a Server-Sent Events stream for real-time notifications for the authenticated user.");

        app.MapGet("/inadimplencia/notifications/stream", StreamNotificationsAsync)
            .WithName("StreamNotificationsInadimplencia")
            .WithOpenApi()
            .WithSummary("Stream notifications via SSE")
            .WithDescription("Opens a Server-Sent Events stream for real-time notifications for the authenticated user.");
    }

    /// <summary>
    /// Streams notifications via SSE for the authenticated user.
    /// </summary>
    private static async Task StreamNotificationsAsync(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] INotificationRepository notificationRepository,
        [FromServices] SseHub sseHub,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("NotificationsSse");
        var username = currentUserService.Username;

        if (string.IsNullOrWhiteSpace(username) &&
            context.Request.Query.TryGetValue("username", out var queryUsername))
        {
            username = queryUsername.ToString();
        }

        username = username?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username))
        {
            logger.LogWarning(
                "SSE connection rejected without username. TraceId={TraceId} Path={Path}",
                context.TraceIdentifier,
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized", cancellationToken);
            return;
        }

        logger.LogInformation(
            "Opening SSE connection. TraceId={TraceId} Username={Username} Path={Path} Origin={Origin}",
            context.TraceIdentifier,
            username,
            context.Request.Path,
            context.Request.Headers.Origin.ToString());

        logger.LogInformation(
            "Loading initial SSE snapshot before response start. TraceId={TraceId} Username={Username}",
            context.TraceIdentifier,
            username);

        List<object> snapshotPayload;
        try
        {
            var initialResult = await notificationRepository.ListAsync(username, false, 1, 100, cancellationToken);
            snapshotPayload = initialResult
                .Select(n => (object)NotificationSsePayloadMapper.ToSnapshotPayload(n))
                .ToList();
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "SSE snapshot load canceled before response start. TraceId={TraceId} Username={Username}",
                context.TraceIdentifier,
                username);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to load initial SSE snapshot. TraceId={TraceId} Username={Username}",
                context.TraceIdentifier,
                username);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync(
                $"Failed to load initial notifications snapshot: {ex.GetType().Name}: {ex.Message}",
                cancellationToken);
            return;
        }

        // Set SSE headers
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await context.Response.StartAsync(cancellationToken);

        // Get stream writer
        var streamWriter = new StreamWriter(context.Response.Body, new UTF8Encoding(false), leaveOpen: true);

        // Register connection in hub
        var connectionId = sseHub.AddConnection(username, streamWriter);
        var heartbeatCount = 0;

        try
        {
            logger.LogInformation(
                "Sending SSE snapshot. TraceId={TraceId} Username={Username} ConnectionId={ConnectionId} NotificationCount={NotificationCount}",
                context.TraceIdentifier,
                username,
                connectionId,
                snapshotPayload.Count);

            await sseHub.SendSnapshotAsync(username, connectionId, snapshotPayload, cancellationToken);

            // Send heartbeat every 20 seconds
            using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(20));

            while (await heartbeatTimer.WaitForNextTickAsync(cancellationToken))
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    break;
                }

                heartbeatCount++;
                logger.LogDebug(
                    "Sending SSE heartbeat. TraceId={TraceId} Username={Username} ConnectionId={ConnectionId} HeartbeatCount={HeartbeatCount}",
                    context.TraceIdentifier,
                    username,
                    connectionId,
                    heartbeatCount);

                await sseHub.SendHeartbeatAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(
                ex,
                "SSE connection canceled. TraceId={TraceId} Username={Username} ConnectionId={ConnectionId} RequestAborted={RequestAborted}",
                context.TraceIdentifier,
                username,
                connectionId,
                context.RequestAborted.IsCancellationRequested);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "SSE connection failed unexpectedly. TraceId={TraceId} Username={Username} ConnectionId={ConnectionId}",
                context.TraceIdentifier,
                username,
                connectionId);
            throw;
        }
        finally
        {
            logger.LogInformation(
                "Closing SSE connection. TraceId={TraceId} Username={Username} ConnectionId={ConnectionId} RequestAborted={RequestAborted} CancellationRequested={CancellationRequested} HeartbeatCount={HeartbeatCount}",
                context.TraceIdentifier,
                username,
                connectionId,
                context.RequestAborted.IsCancellationRequested,
                cancellationToken.IsCancellationRequested,
                heartbeatCount);

            // Remove connection from hub
            sseHub.RemoveConnection(username, connectionId);
        }
    }
}

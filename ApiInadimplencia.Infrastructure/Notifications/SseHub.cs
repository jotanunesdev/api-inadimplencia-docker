using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Infrastructure.Notifications;

/// <summary>
/// SSE hub for real-time notification streaming.
/// </summary>
public class SseHub
{
    private const string NotificationPrefix = "inadimplencia-notifications.";

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, StreamWriter>> _connections = new();
    private readonly ILogger<SseHub> _logger;

    public SseHub(ILogger<SseHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a new SSE connection for a user and returns its connection id.
    /// </summary>
    public virtual Guid AddConnection(string username, StreamWriter streamWriter)
    {
        var normalizedUsername = NormalizeUsername(username);
        var connectionId = Guid.NewGuid();
        var userConnections = _connections.GetOrAdd(normalizedUsername, _ => new ConcurrentDictionary<Guid, StreamWriter>());
        userConnections[connectionId] = streamWriter;
        _logger.LogInformation(
            "SSE connection registered. Username={Username} ConnectionId={ConnectionId} ActiveConnections={ActiveConnections}",
            normalizedUsername,
            connectionId,
            userConnections.Count);
        return connectionId;
    }

    /// <summary>
    /// Removes all SSE connections for a user.
    /// </summary>
    public virtual void RemoveConnection(string username)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (_connections.TryRemove(normalizedUsername, out var removedConnections))
        {
            _logger.LogInformation(
                "Removed all SSE connections for user. Username={Username} RemovedConnections={RemovedConnections}",
                normalizedUsername,
                removedConnections.Count);
        }
    }

    /// <summary>
    /// Removes a specific SSE connection for a user.
    /// </summary>
    public virtual void RemoveConnection(string username, Guid connectionId)
    {
        var normalizedUsername = NormalizeUsername(username);

        if (!_connections.TryGetValue(normalizedUsername, out var userConnections))
        {
            return;
        }

        userConnections.TryRemove(connectionId, out _);
        _logger.LogInformation(
            "Removed SSE connection. Username={Username} ConnectionId={ConnectionId} RemainingConnections={RemainingConnections}",
            normalizedUsername,
            connectionId,
            userConnections.Count);

        if (userConnections.IsEmpty)
        {
            _connections.TryRemove(normalizedUsername, out _);
            _logger.LogInformation(
                "Removed SSE user bucket after last connection closed. Username={Username}",
                normalizedUsername);
        }
    }

    /// <summary>
    /// Broadcasts a notification event to all active connections for a user.
    /// </summary>
    public virtual Task BroadcastNotificationAsync(string username, object notification, CancellationToken cancellationToken)
        => WriteToUserConnectionsAsync(username, $"{NotificationPrefix}new", notification, cancellationToken);

    /// <summary>
    /// Broadcasts a notification update event to all active connections for a user.
    /// </summary>
    public virtual Task BroadcastUpdateAsync(string username, object payload, CancellationToken cancellationToken)
        => WriteToUserConnectionsAsync(username, $"{NotificationPrefix}update", payload, cancellationToken);

    /// <summary>
    /// Sends a heartbeat to all connected clients.
    /// </summary>
    public virtual async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var tasks = SnapshotConnections()
            .Select(async connection =>
            {
                try
                {
                    await WriteEventAsync(connection.StreamWriter, "heartbeat", DateTime.UtcNow.ToString("O"), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to send SSE heartbeat. Username={Username} ConnectionId={ConnectionId}",
                        connection.Username,
                        connection.ConnectionId);
                }
            });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Sends the initial snapshot of unread notifications to a specific connection.
    /// </summary>
    public virtual async Task SendSnapshotAsync(
        string username,
        Guid connectionId,
        IEnumerable<object> notifications,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (!_connections.TryGetValue(normalizedUsername, out var userConnections) ||
            !userConnections.TryGetValue(connectionId, out var streamWriter))
        {
            return;
        }

        try
        {
            var snapshotData = JsonSerializer.Serialize(notifications);
            await WriteEventAsync(streamWriter, $"{NotificationPrefix}snapshot", snapshotData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send SSE snapshot. Username={Username} ConnectionId={ConnectionId}",
                normalizedUsername,
                connectionId);
            RemoveConnection(normalizedUsername, connectionId);
        }
    }

    /// <summary>
    /// Backwards-compatible snapshot broadcast to every connection of a user.
    /// </summary>
    public virtual async Task SendSnapshotAsync(string username, IEnumerable<object> notifications, CancellationToken cancellationToken)
    {
        var snapshotData = JsonSerializer.Serialize(notifications);
        await WriteToUserConnectionsAsync(username, $"{NotificationPrefix}snapshot", snapshotData, cancellationToken);
    }

    private async Task WriteToUserConnectionsAsync(string username, string eventName, object payload, CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (!_connections.TryGetValue(normalizedUsername, out var userConnections) || userConnections.IsEmpty)
        {
            return;
        }

        var serializedPayload = payload is string text ? text : JsonSerializer.Serialize(payload);
        var targets = userConnections.ToArray();

        foreach (var (connectionId, streamWriter) in targets)
        {
            try
            {
                await WriteEventAsync(streamWriter, eventName, serializedPayload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to write SSE event. Username={Username} ConnectionId={ConnectionId} EventName={EventName}",
                    normalizedUsername,
                    connectionId,
                    eventName);
                RemoveConnection(normalizedUsername, connectionId);
            }
        }
    }

    private static async Task WriteEventAsync(StreamWriter streamWriter, string eventName, string data, CancellationToken cancellationToken)
    {
        await streamWriter.WriteAsync($"event: {eventName}\n");
        await streamWriter.WriteAsync($"data: {data}\n\n");
        await streamWriter.FlushAsync(cancellationToken);
    }

    private IEnumerable<(string Username, Guid ConnectionId, StreamWriter StreamWriter)> SnapshotConnections()
        => _connections.SelectMany(userEntry => userEntry.Value.Select(connectionEntry => (userEntry.Key, connectionEntry.Key, connectionEntry.Value)));

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}

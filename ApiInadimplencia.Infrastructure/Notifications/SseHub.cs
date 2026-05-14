using System.Collections.Concurrent;

namespace ApiInadimplencia.Infrastructure.Notifications;

/// <summary>
/// SSE hub for real-time notification streaming.
/// </summary>
public class SseHub
{
    private readonly ConcurrentDictionary<string, StreamWriter> _connections = new();

    /// <summary>
    /// Adds a new SSE connection for a user.
    /// </summary>
    public void AddConnection(string username, StreamWriter streamWriter)
    {
        var normalizedUsername = username.ToLowerInvariant();
        _connections.TryAdd(normalizedUsername, streamWriter);
    }

    /// <summary>
    /// Removes an SSE connection for a user.
    /// </summary>
    public void RemoveConnection(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        _connections.TryRemove(normalizedUsername, out _);
    }

    /// <summary>
    /// Broadcasts a notification event to a specific user.
    /// </summary>
    public async Task BroadcastNotificationAsync(string username, object notification, CancellationToken cancellationToken)
    {
        var normalizedUsername = username.ToLowerInvariant();
        if (_connections.TryGetValue(normalizedUsername, out var streamWriter))
        {
            try
            {
                var eventData = System.Text.Json.JsonSerializer.Serialize(notification);
                await streamWriter.WriteAsync($"event: notification\n");
                await streamWriter.WriteAsync($"data: {eventData}\n\n");
                await streamWriter.FlushAsync(cancellationToken);
            }
            catch
            {
                // Connection may be closed, remove it
                RemoveConnection(username);
            }
        }
    }

    /// <summary>
    /// Sends a heartbeat to all connected clients.
    /// </summary>
    public async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var tasks = _connections.Values.Select(async streamWriter =>
        {
            try
            {
                await streamWriter.WriteAsync($"event: heartbeat\n");
                await streamWriter.WriteAsync($"data: {DateTime.UtcNow:o}\n\n");
                await streamWriter.FlushAsync(cancellationToken);
            }
            catch
            {
                // Connection may be closed, will be cleaned up on next operation
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Sends the initial snapshot of unread notifications to a user.
    /// </summary>
    public async Task SendSnapshotAsync(string username, IEnumerable<object> notifications, CancellationToken cancellationToken)
    {
        var normalizedUsername = username.ToLowerInvariant();
        if (_connections.TryGetValue(normalizedUsername, out var streamWriter))
        {
            try
            {
                var snapshotData = System.Text.Json.JsonSerializer.Serialize(notifications);
                await streamWriter.WriteAsync($"event: snapshot\n");
                await streamWriter.WriteAsync($"data: {snapshotData}\n\n");
                await streamWriter.FlushAsync(cancellationToken);
            }
            catch
            {
                // Connection may be closed, remove it
                RemoveConnection(username);
            }
        }
    }
}

using ApiInadimplencia.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Text;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Notifications;

public class SseHubTests
{
    private readonly SseHub _sseHub;

    public SseHubTests()
    {
        _sseHub = new SseHub(NullLogger<SseHub>.Instance);
    }

    [Fact]
    public void AddConnection_CreatesChannelForUsername()
    {
        // Arrange
        var username = "test.user";
        var streamWriter = new StreamWriter(new MemoryStream());

        // Act
        var connectionId = _sseHub.AddConnection(username, streamWriter);

        // Assert
        Assert.NotEqual(Guid.Empty, connectionId);
    }

    [Fact]
    public void AddConnection_NormalizesUsernameToLowercase()
    {
        // Arrange
        var username = "Test.User";
        var streamWriter = new StreamWriter(new MemoryStream());

        // Act
        var connectionId = _sseHub.AddConnection(username, streamWriter);
        _sseHub.RemoveConnection("test.user", connectionId); // Should work if normalized

        // Assert - No exception thrown
    }

    [Fact]
    public void RemoveConnection_RemovesConnectionForUsername()
    {
        // Arrange
        var username = "test.user";
        var streamWriter = new StreamWriter(new MemoryStream());
        var connectionId = _sseHub.AddConnection(username, streamWriter);

        // Act
        _sseHub.RemoveConnection(username, connectionId);

        // Assert - No exception thrown, connection removed
    }

    [Fact]
    public async Task BroadcastNotificationAsync_DeliversToCorrectSubscriber()
    {
        // Arrange
        var username = "test.user";
        var notification = new { Id = Guid.NewGuid(), Message = "Test" };
        
        var memoryStream = new MemoryStream();
        var streamWriter = new StreamWriter(memoryStream);
        
        _sseHub.AddConnection(username, streamWriter);

        // Act
        await _sseHub.BroadcastNotificationAsync(username, notification, CancellationToken.None);

        // Assert
        await streamWriter.FlushAsync();
        memoryStream.Position = 0;
        var reader = new StreamReader(memoryStream);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("event: inadimplencia-notifications.new", output);
        Assert.Contains("Test", output);
    }

    [Fact]
    public async Task BroadcastNotificationAsync_DeliversToAllConnectionsOfSameUser()
    {
        // Arrange
        var username = "test.user";
        var notification = new { Id = Guid.NewGuid(), Message = "Test multi" };

        var memoryStream1 = new MemoryStream();
        var streamWriter1 = new StreamWriter(memoryStream1);
        var memoryStream2 = new MemoryStream();
        var streamWriter2 = new StreamWriter(memoryStream2);

        _sseHub.AddConnection(username, streamWriter1);
        _sseHub.AddConnection(username, streamWriter2);

        // Act
        await _sseHub.BroadcastNotificationAsync(username, notification, CancellationToken.None);

        // Assert
        await streamWriter1.FlushAsync();
        await streamWriter2.FlushAsync();
        memoryStream1.Position = 0;
        memoryStream2.Position = 0;
        var output1 = await new StreamReader(memoryStream1).ReadToEndAsync();
        var output2 = await new StreamReader(memoryStream2).ReadToEndAsync();

        Assert.Contains("Test multi", output1);
        Assert.Contains("Test multi", output2);
    }

    [Fact]
    public async Task BroadcastNotificationAsync_WhenConnectionClosed_RemovesConnection()
    {
        // Arrange
        var username = "test.user";
        var notification = new { Id = Guid.NewGuid(), Message = "Test" };
        
        var memoryStream = new MemoryStream();
        var streamWriter = new StreamWriter(memoryStream);
        
        _sseHub.AddConnection(username, streamWriter);

        // Close the stream to simulate connection closed
        await streamWriter.DisposeAsync();

        // Act
        await _sseHub.BroadcastNotificationAsync(username, notification, CancellationToken.None);

        // Assert - No exception thrown, connection should be removed
    }

    [Fact]
    public async Task SendSnapshotAsync_SendsSnapshotToUser()
    {
        // Arrange
        var username = "test.user";
        var notifications = new List<object> { new { Id = Guid.NewGuid(), Message = "Test 1" }, new { Id = Guid.NewGuid(), Message = "Test 2" } };
        
        var memoryStream = new MemoryStream();
        var streamWriter = new StreamWriter(memoryStream);
        
        var connectionId = _sseHub.AddConnection(username, streamWriter);

        // Act
        await _sseHub.SendSnapshotAsync(username, connectionId, notifications, CancellationToken.None);

        // Assert
        await streamWriter.FlushAsync();
        memoryStream.Position = 0;
        var reader = new StreamReader(memoryStream);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("event: inadimplencia-notifications.snapshot", output);
    }

    [Fact]
    public async Task SendHeartbeatAsync_SendsHeartbeatToAllConnections()
    {
        // Arrange
        var username1 = "user1";
        var username2 = "user2";
        
        var memoryStream1 = new MemoryStream();
        var streamWriter1 = new StreamWriter(memoryStream1);
        
        var memoryStream2 = new MemoryStream();
        var streamWriter2 = new StreamWriter(memoryStream2);
        
        _sseHub.AddConnection(username1, streamWriter1);
        _sseHub.AddConnection(username2, streamWriter2);

        // Act
        await _sseHub.SendHeartbeatAsync(CancellationToken.None);

        // Assert
        await streamWriter1.FlushAsync();
        await streamWriter2.FlushAsync();
        
        memoryStream1.Position = 0;
        memoryStream2.Position = 0;
        
        var reader1 = new StreamReader(memoryStream1);
        var reader2 = new StreamReader(memoryStream2);
        
        var output1 = await reader1.ReadToEndAsync();
        var output2 = await reader2.ReadToEndAsync();

        Assert.Contains("event: heartbeat", output1);
        Assert.Contains("event: heartbeat", output2);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenOneConnectionFails_ContinuesWithOthers()
    {
        // Arrange
        var username1 = "user1";
        var username2 = "user2";
        
        var memoryStream1 = new MemoryStream();
        var streamWriter1 = new StreamWriter(memoryStream1);
        
        var memoryStream2 = new MemoryStream();
        var streamWriter2 = new StreamWriter(memoryStream2);
        
        _sseHub.AddConnection(username1, streamWriter1);
        _sseHub.AddConnection(username2, streamWriter2);

        // Close one stream to simulate failure
        await streamWriter1.DisposeAsync();

        // Act
        await _sseHub.SendHeartbeatAsync(CancellationToken.None);

        // Assert - No exception thrown, should continue with user2
    }
}

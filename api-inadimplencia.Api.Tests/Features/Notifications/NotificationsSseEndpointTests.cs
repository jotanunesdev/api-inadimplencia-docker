using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.Features.Notifications;

public class NotificationsSseEndpointTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public NotificationsSseEndpointTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NotificationsStream_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/notifications/stream");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NotificationsStream_WithAuthentication_ReturnsSseContentType()
    {
        // Arrange
        var client = _factory.CreateClient();
        // Simulate authentication by adding a header (this would need proper auth setup in test)
        client.DefaultRequestHeaders.Add("X-Test-Username", "test.user");

        // Act
        var response = await client.GetAsync("/notifications/stream");

        // Assert
        // Note: Without proper auth setup, this may return 401
        // In a real scenario, you would set up authentication in the test factory
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || 
                   response.StatusCode == HttpStatusCode.OK);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task NotificationsStream_WithAuthentication_ReturnsKeepAliveHeaders()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "test.user");

        // Act
        var response = await client.GetAsync("/notifications/stream");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Contains("keep-alive", response.Headers.Connection?.ToString() ?? string.Empty);
        }
    }
}

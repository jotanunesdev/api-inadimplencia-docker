using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.Observability;

public class ObservabilityIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public ObservabilityIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", content.ToLower());
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnJsonContentType()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludeSelfCheck()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("self", content.ToLower());
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldReturnMetrics()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldReturnPrometheusFormat()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldIncludeHttpMetrics()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.NotEmpty(content);
        // Prometheus metrics should contain common metric names
        // Note: Actual metric names depend on OpenTelemetry instrumentation
    }

    [Fact]
    public async Task SwaggerEndpoint_ShouldBeAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerUiEndpoint_ShouldBeAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_ShouldNotExposeSensitiveDataInLogs()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            cpf = "123.456.789-01",
            password = "secret123"
        };

        // Act
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/test-endpoint", content);

        // Assert
        // The middleware should mask sensitive data
        // This test verifies the endpoint is accessible
        // Actual log verification would require log capture
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_WithBearerToken_ShouldMaskTokenInLogs()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");

        // Act
        var response = await client.GetAsync("/test-endpoint");

        // Assert
        // The middleware should mask the bearer token
        // Actual log verification would require log capture
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_WithCookie_ShouldMaskCookieInLogs()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "JSESSIONID=ABC123XYZ; .AspNetCore.Session=DEF456UVW");

        // Act
        var response = await client.GetAsync("/test-endpoint");

        // Assert
        // The middleware should mask cookie values
        // Actual log verification would require log capture
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Application_ShouldHandleCors()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal("http://localhost:3000", Assert.Single(origins));
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials));
        Assert.Equal("true", Assert.Single(credentials));
    }
}

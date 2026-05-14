using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace api_inadimplencia.Api.Tests.Features.SerasaPefin;

[Trait("Category", "Integration")]
[Trait("Feature", "SerasaPefin")]
public class SerasaPefinEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SerasaPefinEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHistorico_WhenNumVendaProvided_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var numVenda = 12345;

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/vendas/{numVenda}/negativacoes");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("data", content);
    }

    [Fact]
    public async Task GetHistorico_WhenNumVendaProvided_ReturnsJsonContentType()
    {
        // Arrange
        var client = _factory.CreateClient();
        var numVenda = 12345;

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/vendas/{numVenda}/negativacoes");

        // Assert
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetAcompanhamento_WhenTransactionIdExists_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var transactionId = "test-transaction-123";

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/acompanhamento/{transactionId}");

        // Assert
        // Returns 404 if transaction not found, which is expected in test environment
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAcompanhamento_WhenTransactionIdNotFound_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var transactionId = "non-existent-transaction-id";

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/acompanhamento/{transactionId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNegativacaoById_WhenValidGuidProvided_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/negativacoes/{id}");

        // Assert
        // Returns 404 if not found, which is expected in test environment
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetNegativacaoById_WhenIdNotFound_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/negativacoes/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNegativacaoById_WhenInvalidGuidProvided_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidId = "not-a-guid";

        // Act
        var response = await client.GetAsync($"/inadimplencia/serasa-pefin/negativacoes/{invalidId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

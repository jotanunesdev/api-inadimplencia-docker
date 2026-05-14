using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace api_inadimplencia.Api.Tests.Features.Dashboard;

public class DashboardEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DashboardEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDashboardKpis_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/kpis");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMetric_WithValidMetric_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/aging");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMetric_WithInvalidMetric_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/invalid-metric");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMetric_WithDateFilters_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/ocorrencias-por-dia?dataInicio=2024-01-01&dataFim=2024-12-31");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMetric_WithInvalidDateFormat_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/ocorrencias-por-dia?dataInicio=invalid");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMetric_WithLimit_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/parcelas-inadimplentes?limit=100");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMetric_WithLimitExceeding1000_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/inadimplencia/dashboard/parcelas-inadimplentes?limit=1001");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ocorrencias-por-usuario")]
    [InlineData("ocorrencias-por-venda")]
    [InlineData("ocorrencias-por-dia")]
    [InlineData("ocorrencias-por-hora")]
    [InlineData("ocorrencias-por-dia-hora")]
    [InlineData("proximas-acoes-por-dia")]
    [InlineData("acoes-definidas")]
    [InlineData("atendentes-por-proxima-acao")]
    [InlineData("aging")]
    [InlineData("parcelas-inadimplentes")]
    [InlineData("score-saldo")]
    [InlineData("saldo-por-mes-vencimento")]
    [InlineData("perfil-risco-empreendimento")]
    public async Task GetMetric_AllowedMetrics_ReturnOk(string metric)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/inadimplencia/dashboard/{metric}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}

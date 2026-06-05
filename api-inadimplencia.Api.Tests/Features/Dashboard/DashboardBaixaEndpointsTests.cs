using System.Net;
using System.Text.Json;
using Xunit;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.Features.Dashboard;

/// <summary>
/// Testes de integração HTTP para os endpoints do dashboard de baixa Serasa.
/// Como o ambiente de teste roda sem SQL Server (ILegacySqlExecutor.IsConfigured=false),
/// os endpoints retornam <c>data: []</c> nos caminhos felizes.
/// </summary>
public class DashboardBaixaEndpointsTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public DashboardBaixaEndpointsTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------------
    // GET /inadimplencia/dashboard/baixa/motivos
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetMotivosBaixa_DefaultMeses_DeveRetornar200ComArrayVazio()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/dashboard/baixa/motivos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(0, doc.RootElement.GetProperty("data").GetArrayLength());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task GetMotivosBaixa_MesesValidos_DeveRetornar200(int meses)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/inadimplencia/dashboard/baixa/motivos?meses={meses}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(100)]
    public async Task GetMotivosBaixa_MesesForaDaFaixa_DeveRetornar400(int meses)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/inadimplencia/dashboard/baixa/motivos?meses={meses}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("MESES_INVALIDO", content);
    }

    // ---------------------------------------------------------------------
    // GET /inadimplencia/dashboard/baixa/comparativo-mensal
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetComparativoMensal_DefaultMeses_DeveRetornar200ComArrayVazio()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/dashboard/baixa/comparativo-mensal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(0, doc.RootElement.GetProperty("data").GetArrayLength());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task GetComparativoMensal_MesesValidos_DeveRetornar200(int meses)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/inadimplencia/dashboard/baixa/comparativo-mensal?meses={meses}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task GetComparativoMensal_MesesForaDaFaixa_DeveRetornar400(int meses)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/inadimplencia/dashboard/baixa/comparativo-mensal?meses={meses}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetComparativoMensal_ContentTypeJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/dashboard/baixa/comparativo-mensal");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}

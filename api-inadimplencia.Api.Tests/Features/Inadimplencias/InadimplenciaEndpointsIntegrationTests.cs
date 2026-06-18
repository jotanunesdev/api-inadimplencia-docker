using System.Net;
using System.Text.Json;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.Features.Inadimplencias;

public sealed class InadimplenciaEndpointsIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public InadimplenciaEndpointsIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetByNumVenda_WhenSaleIsMissing_ReturnsJson404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/num-venda/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("NAO_ENCONTRADA", json.RootElement.GetProperty("error").GetString());
        Assert.Equal(999999, json.RootElement.GetProperty("numVenda").GetInt32());
    }

    [Fact]
    public async Task List_WhenPagingParametersAreProvided_ReturnsPagedEnvelope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia?page=0&pageSize=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);

        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(200, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task List_WithoutPagingParameters_PreservesLegacyFullListWindow()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);

        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(5000, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task GetByCpf_WhenPagingParametersAreProvided_ReturnsPagedEnvelope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/cpf/12345678900?page=2&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);

        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(10, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("data").ValueKind);
    }
}

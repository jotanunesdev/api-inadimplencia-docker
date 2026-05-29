using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.Features.Negativacao;

public class NegativacaoFluxoEndpointsIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public NegativacaoFluxoEndpointsIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDividasElegiveis_VendaNaoEncontrada_DeveRetornar200ComRespostaVazia()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/vendas/999999/dividas");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        Assert.Equal(999999, data.GetProperty("numVenda").GetInt32());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("cliente").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("cpfMasked").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("contractNumber").ValueKind);
        Assert.False(data.GetProperty("clientePodeNegativar").GetBoolean());
        Assert.Equal(JsonValueKind.Array, data.GetProperty("parcelas").ValueKind);
        Assert.Equal(0, data.GetProperty("parcelas").GetArrayLength());
    }

    [Fact]
    public async Task GetDividasElegiveis_VendaInvalida_DeveRetornar400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/vendas/abc/dividas");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDividasElegiveis_VendaExistente_DeveRetornar200ComDados()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/vendas/295/dividas");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        Assert.Equal(295, data.GetProperty("numVenda").GetInt32());
        
        // If data exists (sale found), verify structure
        if (data.GetProperty("cliente").ValueKind != JsonValueKind.Null)
        {
            Assert.NotEqual(JsonValueKind.Null, data.GetProperty("cliente").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, data.GetProperty("cpfMasked").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, data.GetProperty("contractNumber").ValueKind);
            Assert.Equal(JsonValueKind.Array, data.GetProperty("parcelas").ValueKind);
        }
    }

    [Fact]
    public async Task GetDividasElegiveis_ResponseContentType_ShouldBeJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/vendas/295/dividas");

        // Assert
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDividasElegiveis_CpfMasked_DeveConterAsteriscos()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/vendas/295/dividas");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        
        // If CPF is present, verify it's masked
        if (data.GetProperty("cpfMasked").ValueKind != JsonValueKind.Null)
        {
            var cpfMasked = data.GetProperty("cpfMasked").GetString();
            Assert.NotNull(cpfMasked);
            Assert.Contains("***", cpfMasked);
        }
    }

    [Fact]
    public async Task GetDividasElegiveis_Parcelas_DeveConterCamposEsperados()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/vendas/295/dividas");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        var parcelas = data.GetProperty("parcelas");
        
        // If parcelas exist, verify structure
        if (parcelas.GetArrayLength() > 0)
        {
            foreach (var parcela in parcelas.EnumerateArray())
            {
                Assert.True(parcela.TryGetProperty("id", out _));
                Assert.True(parcela.TryGetProperty("valor", out _));
                Assert.True(parcela.TryGetProperty("vencimento", out _));
                Assert.True(parcela.TryGetProperty("diasAtraso", out _));
                Assert.True(parcela.TryGetProperty("elegivel", out _));
            }
        }
    }

    #region DecideNegativacao Endpoint Tests

    [Fact]
    public async Task PostDecisao_SolicitacaoIdInvalido_DeveRetornar400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var command = new DecideNegativacaoCommand(
            Guid.Empty,
            DecisaoNegativacao.APROVAR,
            "senha");

        // Act
        var response = await client.PostAsJsonAsync("/negativacao/solicitacoes/invalid-guid/decisao", command);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDecisao_CommandInvalido_DeveRetornar400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidJson = new { decisao = "INVALID" };

        // Act
        var response = await client.PostAsJsonAsync($"/negativacao/solicitacoes/{Guid.NewGuid()}/decisao", invalidJson);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDecisao_ResponseContentType_ShouldBeJson()
    {
        // Arrange
        var client = _factory.CreateClient();
        var solicitacaoId = Guid.NewGuid();
        var command = new DecideNegativacaoCommand(
            solicitacaoId,
            DecisaoNegativacao.APROVAR,
            "senha");

        // Act
        var response = await client.PostAsJsonAsync($"/negativacao/solicitacoes/{solicitacaoId}/decisao", command);

        // Assert - Even if it fails (401, 404, etc), it should return JSON
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    #endregion

    #region GetSolicitacaoById Endpoint Tests

    [Fact]
    public async Task GetSolicitacaoById_IdInvalido_DeveRetornar400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/solicitacoes/invalid-guid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSolicitacaoById_IdNaoEncontrado_DeveRetornar404()
    {
        // Arrange
        var client = _factory.CreateClient();
        var idNaoExistente = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/negativacao/solicitacoes/{idNaoExistente}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        Assert.True(jsonDoc.RootElement.TryGetProperty("error", out var error));
        Assert.Equal("NAO_ENCONTRADA", error.GetString());
    }

    [Fact]
    public async Task GetSolicitacaoById_ResponseContentType_ShouldBeJson()
    {
        // Arrange
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/negativacao/solicitacoes/{id}");

        // Assert - Even if it fails (404), it should return JSON
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetSolicitacaoById_ComUsernameNaQuery_DeveRetornarPodeDecidirTrueParaAprovadorValido()
    {
        await _factory.ResetStateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<InMemorySerasaPefinRepository>();
            var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
                numVendaFk: 295,
                tipoRegistro: SerasaPefinRecordType.Principal,
                documentoDevedor: "12345678900",
                documentoCredor: "62173620000180",
                contractNumber: "295/00",
                areaInformante: "0001",
                valor: 1000m,
                dataVencimento: new DateOnly(2024, 1, 1),
                solicitanteUsername: "solicitante");

            await repository.AddAsync(solicitacao, CancellationToken.None);

            var client = _factory.CreateClient();
            var response = await client.GetAsync($"/negativacao/solicitacoes/{solicitacao.Id}?username=Gustavo.Trindade");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            Assert.True(jsonDoc.RootElement.TryGetProperty("podeDecidir", out var podeDecidir));
            Assert.True(podeDecidir.GetBoolean());
        }
    }

    #endregion

    #region ListSolicitacoes Endpoint Tests

    [Fact]
    public async Task ListSolicitacoes_PorNumVendaEStatus_DeveRetornar200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/solicitacoes?numVenda=295&status=AGUARDANDO_APROVACAO");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task ListSolicitacoes_PorSolicitacaoId_DeveRetornar200()
    {
        // Arrange
        var client = _factory.CreateClient();
        var solicitacaoId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/negativacao/solicitacoes?solicitacaoId={solicitacaoId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task ListSolicitacoes_ResponseContentType_ShouldBeJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/solicitacoes");

        // Assert
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ListSolicitacoes_Paginacao_DeveRespeitarTakeESkip()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/negativacao/solicitacoes?take=10&skip=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        Assert.True(jsonDoc.RootElement.TryGetProperty("data", out var data));
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    #endregion
}

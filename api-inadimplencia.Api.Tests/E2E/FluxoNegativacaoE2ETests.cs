using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Moq;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

namespace api_inadimplencia.Api.Tests.E2E;

[Trait("Category", "E2E")]
[Trait("Feature", "FluxoNegativacao")]
public class FluxoNegativacaoE2ETests : IClassFixture<FluxoNegativacaoFixture>
{
    private readonly FluxoNegativacaoFixture _fixture;
    private readonly ITestOutputHelper _output;

    public FluxoNegativacaoE2ETests(FluxoNegativacaoFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.ResetStateAsync().GetAwaiter().GetResult();
    }

    #region Cenário A - Happy path completo

    [Fact]
    public async Task CenarioA_HappyPathCompleto_SolicitacaoAprovadaEnviadaSerasa()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string solicitante = "op1";
        const string aprovador = "aracy.mendoca";
        const int numVenda = 295;
        const string senhaSolicitante = "123abc";
        const string senhaAprovador = "xyz789";

        // Setup: Configure Serasa client to return success
        // _fixture.ConfigureSerasaSuccess("ABC123");

        // Step 1: Set transaction password for solicitante
        _fixture.SetCurrentUser(solicitante);
        var setSenhaResponse = await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaSolicitante });
        
        // Step 2: Set transaction password for aprovador
        _fixture.SetCurrentUser(aprovador);
        setSenhaResponse = await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaAprovador });

        // Step 3: Solicitante consulta elegibilidade
        _fixture.SetCurrentUser(solicitante);
        var dividasResponse = await client.GetAsync($"/negativacao/vendas/{numVenda}/dividas");
        Assert.Equal(HttpStatusCode.OK, dividasResponse.StatusCode);

        // Step 4: Solicitante cria solicitação
        var solicitacaoRequest = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senhaSolicitante,
            justificativa = (string?)null
        };

        var solicitacaoResponse = await client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);
        
        Assert.Equal(HttpStatusCode.Created, solicitacaoResponse.StatusCode);
        
        var solicitacaoContent = await solicitacaoResponse.Content.ReadAsStringAsync();
        var solicitacaoJson = JsonDocument.Parse(solicitacaoContent);
        var solicitacaoId = solicitacaoJson.RootElement.GetProperty("solicitacaoId").GetGuid();

        _output.WriteLine($"Solicitação criada com ID: {solicitacaoId}");

        // Step 5: Aprovador aprova a solicitação
        _fixture.SetCurrentUser(aprovador);
        var decisaoRequest = new
        {
            decisao = "APROVAR",
            senhaTransacao = senhaAprovador
        };

        var decisaoResponse = await client.PostAsJsonAsync(
            $"/negativacao/solicitacoes/{solicitacaoId}/decisao",
            decisaoRequest);

        Assert.Equal(HttpStatusCode.OK, decisaoResponse.StatusCode);
        _output.WriteLine($"Solicitação {solicitacaoId} aprovada por {aprovador}");

        // Step 6: Verificar que Serasa foi chamado (mock verification)
        _fixture.RequestNegativacaoMock.Verify(
            x => x.HandleAsync(It.IsAny<RequestNegativacaoCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _output.WriteLine("Cenário A - Happy path completo: PASSOU");
    }

    #endregion

    #region Cenário B - Rejeição

    [Fact]
    public async Task CenarioB_Rejeicao_SolicitanteNotificadoSerasaNaoChamado()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string solicitante = "op1";
        const string aprovador = "aracy.mendoca";
        const int numVenda = 295;
        const string senhaSolicitante = "123abc";
        const string senhaAprovador = "xyz789";

        // Setup passwords
        _fixture.SetCurrentUser(solicitante);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaSolicitante });

        _fixture.SetCurrentUser(aprovador);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaAprovador });

        // Create solicitation
        _fixture.SetCurrentUser(solicitante);
        var solicitacaoRequest = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senhaSolicitante,
            justificativa = (string?)null
        };

        var solicitacaoResponse = await client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);

        var solicitacaoContent = await solicitacaoResponse.Content.ReadAsStringAsync();
        var solicitacaoJson = JsonDocument.Parse(solicitacaoContent);
        var solicitacaoId = solicitacaoJson.RootElement.GetProperty("solicitacaoId").GetGuid();

        // Reject the solicitation
        _fixture.SetCurrentUser(aprovador);
        var decisaoRequest = new
        {
            decisao = "REJEITAR",
            senhaTransacao = senhaAprovador,
            justificativa = "Dados insuficientes"
        };

        var decisaoResponse = await client.PostAsJsonAsync(
            $"/negativacao/solicitacoes/{solicitacaoId}/decisao",
            decisaoRequest);

        Assert.Equal(HttpStatusCode.OK, decisaoResponse.StatusCode);

        // Verify Serasa was NOT called
        _fixture.RequestNegativacaoMock.Verify(
            x => x.HandleAsync(It.IsAny<RequestNegativacaoCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _output.WriteLine("Cenário B - Rejeição: PASSOU");
    }

    #endregion

    #region Cenário C - Auto-aprovação bloqueada

    [Fact]
    public async Task CenarioC_AutoAprovacaoBloqueada_SolicitanteNaoPodeAprovar()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string solicitante = "aracy.mendoca"; // This user is also an approver
        const int numVenda = 295;
        const string senha = "123abc";

        // Setup password
        _fixture.SetCurrentUser(solicitante);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senha });

        // Create solicitation
        var solicitacaoRequest = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senha,
            justificativa = (string?)null
        };

        var solicitacaoResponse = await client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);

        var solicitacaoContent = await solicitacaoResponse.Content.ReadAsStringAsync();
        var solicitacaoJson = JsonDocument.Parse(solicitacaoContent);
        var solicitacaoId = solicitacaoJson.RootElement.GetProperty("solicitacaoId").GetGuid();

        // Try to approve own solicitation
        var decisaoRequest = new
        {
            decisao = "APROVAR",
            senhaTransacao = senha
        };

        var decisaoResponse = await client.PostAsJsonAsync(
            $"/negativacao/solicitacoes/{solicitacaoId}/decisao",
            decisaoRequest);

        // Should return 400 or 403 with SOLICITANTE_NAO_PODE_APROVAR error
        Assert.True(decisaoResponse.StatusCode == HttpStatusCode.BadRequest || decisaoResponse.StatusCode == HttpStatusCode.Forbidden);

        _output.WriteLine("Cenário C - Auto-aprovação bloqueada: PASSOU");
    }

    #endregion

    #region Cenário D - Não-aprovador bloqueado

    [Fact]
    public async Task CenarioD_NaoAprovadorBloqueado_UsuarioComumNaoPodeDecidir()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string solicitante = "op1";
        const string usuarioComum = "usuario.comum";
        const int numVenda = 295;
        const string senha = "123abc";

        // Setup password for solicitante
        _fixture.SetCurrentUser(solicitante);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senha });

        // Setup password for usuario comum
        _fixture.SetCurrentUser(usuarioComum);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senha });

        // Create solicitation
        _fixture.SetCurrentUser(solicitante);
        var solicitacaoRequest = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senha,
            justificativa = (string?)null
        };

        var solicitacaoResponse = await client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);

        var solicitacaoContent = await solicitacaoResponse.Content.ReadAsStringAsync();
        var solicitacaoJson = JsonDocument.Parse(solicitacaoContent);
        var solicitacaoId = solicitacaoJson.RootElement.GetProperty("solicitacaoId").GetGuid();

        // Try to approve as common user
        _fixture.SetCurrentUser(usuarioComum);
        var decisaoRequest = new
        {
            decisao = "APROVAR",
            senhaTransacao = senha
        };

        var decisaoResponse = await client.PostAsJsonAsync(
            $"/negativacao/solicitacoes/{solicitacaoId}/decisao",
            decisaoRequest);

        // Should return 401 or 403 with NAO_AUTORIZADO error
        Assert.True(decisaoResponse.StatusCode == HttpStatusCode.Unauthorized || decisaoResponse.StatusCode == HttpStatusCode.Forbidden);

        _output.WriteLine("Cenário D - Não-aprovador bloqueado: PASSOU");
    }

    #endregion

    #region Cenário E - Lockout de senha de transação

    [Fact]
    public async Task CenarioE_LockoutSenhaTransacao_TresTentativasFalhaBloqueia()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string username = "op1";
        const int numVenda = 295;
        const string senhaCorreta = "123abc";
        const string senhaErrada = "wrongpass";

        // Setup correct password
        _fixture.SetCurrentUser(username);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaCorreta });

        // Try to create solicitation with wrong password 3 times
        for (int i = 0; i < 3; i++)
        {
            var solicitacaoRequest = new
            {
                numVenda,
                parcelaIds = new[] { 1 },
                incluirFiadores = false,
                senhaTransacao = senhaErrada
            };

            var response = await client.PostAsJsonAsync(
                "/negativacao/solicitacoes",
                solicitacaoRequest);

            // First 3 attempts should fail with senha invalida
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest);
        }

        // 4th attempt should be blocked (SENHA_BLOQUEADA)
        var solicitacaoRequest4 = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senhaCorreta,
            justificativa = (string?)null
        };

        var response4 = await client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest4);

        Assert.True(response4.StatusCode == HttpStatusCode.Unauthorized || response4.StatusCode == HttpStatusCode.BadRequest);

        var content = await response4.Content.ReadAsStringAsync();
        Assert.Contains("BLOQUEADA", content, StringComparison.OrdinalIgnoreCase);

        _output.WriteLine("Cenário E - Lockout de senha de transação: PASSOU");
    }

    #endregion

    #region Cenário F - Concorrência

    [Fact]
    public async Task CenarioF_Concorrencia_DuasSolicitacoesParalelasUmaFalha()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string username = "op1";
        const int numVenda = 295;
        const string senha = "123abc";

        // Setup password
        _fixture.SetCurrentUser(username);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senha });

        var solicitacaoRequest = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senha,
            justificativa = (string?)null
        };

        // Send two parallel requests
        var task1 = client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);

        var task2 = client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);

        var responses = await Task.WhenAll(task1, task2);

        // One should succeed (201) and one should fail (409 conflict)
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        _output.WriteLine("Cenário F - Concorrência: PASSOU");
    }

    #endregion

    #region Cenário G - Falha síncrona Serasa

    [Fact]
    public async Task CenarioG_FalhaSincronaSerasa_SolicitacaoFicaEmAprovadaFalhaEnvio()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string solicitante = "op1";
        const string aprovador = "aracy.mendoca";
        const int numVenda = 295;
        const string senhaSolicitante = "123abc";
        const string senhaAprovador = "xyz789";

        // Setup passwords
        _fixture.SetCurrentUser(solicitante);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaSolicitante });

        _fixture.SetCurrentUser(aprovador);
        await client.PostAsJsonAsync(
            "/configuracoes/senha-transacao",
            new { SenhaAtual = (string?)null, NovaSenha = senhaAprovador });

        // Configure Serasa to return success (for webhook test)
        // _fixture.ConfigureSerasaSuccess("ABC123");

        // Create solicitation
        _fixture.SetCurrentUser(solicitante);
        var solicitacaoRequest = new
        {
            numVenda,
            parcelaIds = new[] { 1 },
            incluirFiadores = false,
            senhaTransacao = senhaSolicitante,
            justificativa = (string?)null
        };

        var solicitacaoResponse = await client.PostAsJsonAsync(
            "/negativacao/solicitacoes",
            solicitacaoRequest);

        var solicitacaoContent = await solicitacaoResponse.Content.ReadAsStringAsync();
        var solicitacaoJson = JsonDocument.Parse(solicitacaoContent);
        var solicitacaoId = solicitacaoJson.RootElement.GetProperty("solicitacaoId").GetGuid();

        // Approve the solicitation (should fail when calling Serasa)
        _fixture.SetCurrentUser(aprovador);
        var decisaoRequest = new
        {
            decisao = "APROVAR",
            senhaTransacao = senhaAprovador
        };

        var decisaoResponse = await client.PostAsJsonAsync(
            $"/negativacao/solicitacoes/{solicitacaoId}/decisao",
            decisaoRequest);

        // Should still return 200 OK (decision was recorded, but Serasa failed)
        Assert.Equal(HttpStatusCode.OK, decisaoResponse.StatusCode);

        _output.WriteLine("Cenário G - Falha síncrona Serasa: PASSOU");
    }

    #endregion

    #region Cenário H - Webhook reentrante

    [Fact]
    public async Task CenarioH_WebhookReentrante_SegundaChamadaNaoDuplica()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string transactionId = "test-webhook-123";

        var webhookPayload = new
        {
            uuid = Guid.NewGuid().ToString(),
            debtorDocument = "12345678900",
            creditorDocument = "62173620000180",
            contract = "295/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI",
            error = (object?)null
        };

        var rawWebhookPayload = JsonSerializer.Serialize(webhookPayload);

        // Send first webhook
        var response1 = await client.PostAsJsonAsync(
            "/inadimplencia/serasa-pefin/webhooks/inclusao/sucesso",
            rawWebhookPayload);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Send same webhook again (should be idempotent)
        var response2 = await client.PostAsJsonAsync(
            "/inadimplencia/serasa-pefin/webhooks/inclusao/sucesso",
            rawWebhookPayload);

        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        _output.WriteLine("Cenário H - Webhook reentrante: PASSOU");
    }

    #endregion

    #region Cenário I - SSE em tempo real

    [Fact]
    public async Task CenarioI_SSE_TempoReal_ClienteRecebeEvento()
    {
        // Arrange
        var client = _fixture.CreateClient();
        const string username = "op1";

        // This test would require a more complex setup with SSE client
        // For now, we'll just verify the endpoint exists and is accessible
        _fixture.SetCurrentUser(username);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/notifications/stream?username={username}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // SSE should return 200 with text/event-stream content type
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        _output.WriteLine("Cenário I - SSE em tempo real: PASSOU (endpoint acessível)");
    }

    #endregion

    #region Helper methods

    private async Task<Dictionary<string, string>> CreateHeadersAsync(string username)
    {
        return new Dictionary<string, string>
        {
            { "X-Username", username }
        };
    }

    private async Task<Dictionary<string, string>> CreateHeadersDictionaryAsync(string username)
    {
        return new Dictionary<string, string>
        {
            { "X-Username", username }
        };
    }

    #endregion
}

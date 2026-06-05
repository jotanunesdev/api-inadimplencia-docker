using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;
using SerasaPefinHttpException = ApiInadimplencia.Application.Abstractions.Integrations.SerasaPefinHttpException;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ApiInadimplencia.Infrastructure.Tests.Integrations.SerasaPefin;

/// <summary>
/// Cobertura do método DELETE de baixa em <see cref="SerasaPefinClient"/>:
/// headers obrigatórios, parsing de transactionId, propagação de erro HTTP e
/// validação de argumentos.
/// </summary>
public class SerasaPefinClientDeleteTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly IOptions<SerasaPefinOptions> _options;
    private readonly ILogger<SerasaPefinClient> _logger;

    public SerasaPefinClientDeleteTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        _options = Options.Create(new SerasaPefinOptions
        {
            Env = "uat",
            AuthUrl = "https://uat-api.serasaexperian.com.br/security/iam/v1/client-identities/login",
            CollectionBaseUrl = "https://api.serasa.dev/collection",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            LogonVinculado = "123456",
            CnpjContrato = "12345678000190",
            TimeoutSeconds = 30,
        });

        _logger = Mock.Of<ILogger<SerasaPefinClient>>();
    }

    private SerasaPefinClient BuildClient() => new(_httpClient, _options, _logger);

    private static SerasaBaixaRequest ValidRequest() =>
        new(CreditorDocument: "62173620000180",
            DebtorDocument: "12345678901",
            ContractNumber: "12345",
            Reason: 3);

    private void Setup200(string transactionId, out List<HttpRequestMessage> capturedRequests)
    {
        var captured = new List<HttpRequestMessage>();
        capturedRequests = captured;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured.Add(req))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new { transactionId }),
                    Encoding.UTF8,
                    "application/json"),
            });
    }

    [Fact]
    public async Task DeleteByContractAsync_Returns200_DeserializesTransactionId()
    {
        Setup200("uuid-1", out _);

        var result = await BuildClient().DeleteByContractAsync(ValidRequest(), "tk", CancellationToken.None);

        result.Should().NotBeNull();
        result.TransactionId.Should().Be("uuid-1");
    }

    [Fact]
    public async Task DeleteByContractAsync_SendsAllRequiredHeaders()
    {
        Setup200("uuid-1", out var captured);

        await BuildClient().DeleteByContractAsync(ValidRequest(), "tk", CancellationToken.None);

        captured.Should().HaveCount(1);
        var req = captured[0];
        req.Method.Should().Be(HttpMethod.Delete);
        req.RequestUri!.ToString().Should().EndWith("/collection/debt/contract");

        req.Headers.Authorization!.Scheme.Should().Be("Bearer");
        req.Headers.Authorization.Parameter.Should().Be("tk");

        req.Headers.GetValues("creditor-document").Should().ContainSingle().Which.Should().Be("62173620000180");
        req.Headers.GetValues("debtor-document").Should().ContainSingle().Which.Should().Be("12345678901");
        req.Headers.GetValues("contract-number").Should().ContainSingle().Which.Should().Be("12345");
        req.Headers.GetValues("reason").Should().ContainSingle().Which.Should().Be("3");
        req.Headers.GetValues("type").Should().ContainSingle().Which.Should().Be("PEFIN");

        req.Content.Should().BeNull("DELETE de baixa nao envia body");
    }

    [Fact]
    public async Task DeleteByContractAsync_Returns400_ThrowsSerasaPefinHttpException()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"contrato nao encontrado\"}", Encoding.UTF8, "application/json"),
            });

        var act = () => BuildClient().DeleteByContractAsync(ValidRequest(), "tk", CancellationToken.None);

        await act.Should()
            .ThrowAsync<SerasaPefinHttpException>()
            .Where(ex => ex.StatusCode == 400 && ex.Body.Contains("contrato nao encontrado"));
    }

    [Fact]
    public async Task DeleteByContractAsync_Returns401_ThrowsSerasaPefinHttpExceptionWith401()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\":\"unauthorized\"}", Encoding.UTF8, "application/json"),
            });

        var act = () => BuildClient().DeleteByContractAsync(ValidRequest(), "tk", CancellationToken.None);

        await act.Should()
            .ThrowAsync<SerasaPefinHttpException>()
            .Where(ex => ex.StatusCode == 401);
    }

    [Fact]
    public async Task DeleteByContractAsync_Returns200ButMissingTransactionId_ThrowsInvalidOperation()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"foo\":\"bar\"}", Encoding.UTF8, "application/json"),
            });

        var act = () => BuildClient().DeleteByContractAsync(ValidRequest(), "tk", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transactionId*");
    }

    [Theory]
    [InlineData("", "12345678901", "12345")]
    [InlineData("62173620000180", "", "12345")]
    [InlineData("62173620000180", "12345678901", "")]
    public async Task DeleteByContractAsync_HeadersObrigatoriosAusentes_DeveLancar(
        string creditor,
        string debtor,
        string contract)
    {
        var request = new SerasaBaixaRequest(creditor, debtor, contract, 3);

        var act = () => BuildClient().DeleteByContractAsync(request, "tk", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

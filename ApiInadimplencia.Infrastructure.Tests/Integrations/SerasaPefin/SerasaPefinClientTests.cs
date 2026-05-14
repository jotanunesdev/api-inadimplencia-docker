using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;
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

public class SerasaPefinClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IOptions<SerasaPefinOptions>> _mockOptions;
    private readonly Mock<ILogger<SerasaPefinClient>> _mockLogger;
    private readonly SerasaPefinOptions _options;

    public SerasaPefinClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        
        _options = new SerasaPefinOptions
        {
            Env = "uat",
            AuthUrl = "https://uat-api.serasaexperian.com.br/security/iam/v1/client-identities/login",
            CollectionBaseUrl = "https://api.serasa.dev/collection",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            LogonVinculado = "123456",
            CnpjContrato = "12345678000190",
            TimeoutSeconds = 30
        };

        _mockOptions = new Mock<IOptions<SerasaPefinOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(_options);
        _mockLogger = new Mock<ILogger<SerasaPefinClient>>();
    }

    [Fact]
    public async Task PostMainDebtAsync_Returns200_DeserializesResponse()
    {
        // Arrange
        var expectedResponse = new SerasaInclusionResponse(
            TransactionId: "test-transaction-id",
            Status: "ACCEPTED");

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("/debt/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var client = new SerasaPefinClient(_httpClient, _mockOptions.Object, _mockLogger.Object);
        var payload = new { test = "data" };

        // Act
        var result = await client.PostMainDebtAsync(payload, "test-token", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TransactionId.Should().Be("test-transaction-id");
        result.Status.Should().Be("ACCEPTED");
    }

    [Fact]
    public async Task PostMainDebtAsync_Returns400_ThrowsSerasaPefinHttpException()
    {
        // Arrange
        var errorResponse = new { error = "Invalid payload" };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("/debt/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(
                    JsonSerializer.Serialize(errorResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var client = new SerasaPefinClient(_httpClient, _mockOptions.Object, _mockLogger.Object);
        var payload = new { test = "data" };

        // Act
        var act = async () => await client.PostMainDebtAsync(payload, "test-token", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ApiInadimplencia.Application.Abstractions.Integrations.SerasaPefinHttpException>()
            .Where(ex => ex.StatusCode == 400 && ex.Body.Contains("Invalid payload"));
    }

    [Fact]
    public async Task PostGuarantorAsync_SendsToGuarantorEndpoint()
    {
        // Arrange
        var expectedResponse = new SerasaInclusionResponse(
            TransactionId: "guarantor-transaction-id",
            Status: "ACCEPTED");

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("/debt/guarantor")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var client = new SerasaPefinClient(_httpClient, _mockOptions.Object, _mockLogger.Object);
        var payload = new { test = "guarantor-data" };

        // Act
        var result = await client.PostGuarantorAsync(payload, "test-token", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TransactionId.Should().Be("guarantor-transaction-id");
        result.Status.Should().Be("ACCEPTED");
    }

    [Fact]
    public async Task PostMainDebtAsync_SetsAuthorizationHeader()
    {
        // Arrange
        var expectedResponse = new SerasaInclusionResponse(
            TransactionId: "test-transaction-id",
            Status: "ACCEPTED");

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var client = new SerasaPefinClient(_httpClient, _mockOptions.Object, _mockLogger.Object);
        var payload = new { test = "data" };

        // Act
        await client.PostMainDebtAsync(payload, "test-token", CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("test-token");
    }
}

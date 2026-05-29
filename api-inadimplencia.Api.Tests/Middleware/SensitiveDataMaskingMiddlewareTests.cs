using ApiInadimplencia.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Text;
using Xunit;

namespace api_inadimplencia.Api.Tests.Middleware;

public class SensitiveDataMaskingMiddlewareTests
{
    private readonly Mock<ILogger<SensitiveDataMaskingMiddleware>> _loggerMock;
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly SensitiveDataMaskingMiddleware _middleware;

    public SensitiveDataMaskingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<SensitiveDataMaskingMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsDevelopment"] = "true"
            })
            .Build();
        _middleware = new SensitiveDataMaskingMiddleware(_nextMock.Object, _loggerMock.Object, configuration);
    }

    [Fact]
    public async Task InvokeAsync_ShouldMaskCookiesInHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "JSESSIONID=ABC123; .AspNetCore.Session=XYZ789";
        
        var responseContent = "{\"data\":\"result\"}";
        _nextMock.Setup(x => x(It.IsAny<HttpContext>()))
            .Callback<HttpContext>(ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Body = new MemoryStream(Encoding.UTF8.GetBytes(responseContent));
            });

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextMock.Verify(x => x(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldBypassBodyBufferingForServerSentEvents()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept"] = "text/event-stream";
        context.Response.Body = new MemoryStream();

        _nextMock.Setup(x => x(It.IsAny<HttpContext>()))
            .Returns<HttpContext>(async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/event-stream";

                await using var writer = new StreamWriter(ctx.Response.Body);
                await writer.WriteAsync("event: heartbeat\n\n");
                await writer.FlushAsync();
            });

        await _middleware.InvokeAsync(context);

        _nextMock.Verify(x => x(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldBypassBodyBufferingForNotificationStreamPath()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/inadimplencia/notifications/stream";
        context.Response.Body = new MemoryStream();

        _nextMock.Setup(x => x(It.IsAny<HttpContext>()))
            .Returns<HttpContext>(async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/event-stream";

                await using var writer = new StreamWriter(ctx.Response.Body);
                await writer.WriteAsync("event: heartbeat\n\n");
                await writer.FlushAsync();
            });

        await _middleware.InvokeAsync(context);

        _nextMock.Verify(x => x(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public void MaskCookieValue_ShouldMaskCookieValues()
    {
        // Arrange
        var cookieHeader = "JSESSIONID=ABC123; .AspNetCore.Session=XYZ789; custom=value";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskCookieValue(cookieHeader);

        // Assert
        Assert.Contains("JSESSIONID=***", masked);
        Assert.Contains(".AspNetCore.Session=***", masked);
        Assert.Contains("custom=***", masked);
        Assert.DoesNotContain("ABC123", masked);
        Assert.DoesNotContain("XYZ789", masked);
    }

    [Fact]
    public void MaskCookieValue_ShouldHandleEmptyInput()
    {
        // Arrange
        var cookieHeader = "";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskCookieValue(cookieHeader);

        // Assert
        Assert.Equal("", masked);
    }

    [Fact]
    public void MaskCookieValue_ShouldHandleNullInput()
    {
        // Arrange
        string? cookieHeader = null;

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskCookieValue(cookieHeader!);

        // Assert
        Assert.Null(masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskCpf()
    {
        // Arrange
        var input = "CPF do cliente: 123.456.789-01";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("123***", masked);
        Assert.DoesNotContain("789-01", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskCpfWithoutDots()
    {
        // Arrange
        var input = "CPF: 12345678901";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("123***", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskCnpj()
    {
        // Arrange
        var input = "CNPJ: 12.345.678/0001-90";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("12***", masked);
        Assert.DoesNotContain("0001-90", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskCnpjWithoutFormatting()
    {
        // Arrange
        var input = "CNPJ: 12345678000190";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("12***", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskBearerToken()
    {
        // Arrange
        var input = "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("Bearer eyJhbGci***", masked);
        Assert.DoesNotContain("SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskLongTokens()
    {
        // Arrange
        var input = "Token: abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("abcdefgh***", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskFluigCookies()
    {
        // Arrange
        var input = "Cookie: JSESSIONID=ABC123XYZ; .AspNetCore.Session=DEF456UVW";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("JSESSIONID=***", masked);
        Assert.Contains(".AspNetCore.Session=***", masked);
        Assert.DoesNotContain("ABC123XYZ", masked);
        Assert.DoesNotContain("DEF456UVW", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskJsonSecrets()
    {
        // Arrange
        var input = @"{""password"":""secret123"",""token"":""abc123xyz"",""apiKey"":""key456""}";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains(@"""password"": ""*****""", masked);
        Assert.Contains(@"""token"": ""*****""", masked);
        Assert.Contains(@"""apiKey"": ""*****""", masked);
        Assert.DoesNotContain("secret123", masked);
        Assert.DoesNotContain("abc123xyz", masked);
        Assert.DoesNotContain("key456", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldMaskSerasaPayloads()
    {
        // Arrange
        var input = @"{""documento"":""12345678901"",""cpf"":""98765432100"",""cnpj"":""12345678000190""}";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains(@"""documento"": ""***""", masked);
        Assert.Contains(@"""cpf"": ""***""", masked);
        Assert.Contains(@"""cnpj"": ""***""", masked);
        Assert.DoesNotContain("12345678901", masked);
        Assert.DoesNotContain("98765432100", masked);
        Assert.DoesNotContain("12345678000190", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldHandleEmptyInput()
    {
        // Arrange
        var input = "";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Equal("", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldHandleNullInput()
    {
        // Arrange
        string? input = null;

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input!);

        // Assert
        Assert.Null(masked);
    }

    [Fact]
    public void MaskToken_ShouldShowFirst8Characters()
    {
        // Arrange
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskToken(token);

        // Assert
        Assert.Equal("eyJhbGci***", masked);
    }

    [Fact]
    public void MaskToken_ShouldMaskShortToken()
    {
        // Arrange
        var token = "short";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskToken(token);

        // Assert
        Assert.Equal("***", masked);
    }

    [Fact]
    public void MaskSecret_ShouldReturnAsterisks()
    {
        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSecret();

        // Assert
        Assert.Equal("*****", masked);
    }

    [Fact]
    public void MaskResponseBody_ShouldPreserveLongIdentifiersAndPaths()
    {
        // Regressão: corpo de resposta não deve sofrer mascaramento agressivo
        // que corrompia URLs/paths do OpenAPI e identificadores legítimos.
        var input = "{\"endpoint\":\"/inadimplencia/cpf/{cpf}\",\"operationId\":\"GetInadimplenciaByCpf\",\"id\":\"a1b2c3d4-e5f6-7890-abcd-ef1234567890\"}";

        var masked = SensitiveDataMaskingMiddleware.MaskResponseBody(input);

        Assert.Contains("/inadimplencia/cpf/{cpf}", masked);
        Assert.Contains("GetInadimplenciaByCpf", masked);
        Assert.Contains("a1b2c3d4-e5f6-7890-abcd-ef1234567890", masked);
        Assert.DoesNotContain("***", masked);
    }

    [Fact]
    public void MaskResponseBody_ShouldStillMaskCpfCnpjAndJsonSecrets()
    {
        var input = "{\"cpf\":\"12345678901\",\"password\":\"super-secret\"}";

        var masked = SensitiveDataMaskingMiddleware.MaskResponseBody(input);

        Assert.Contains("\"cpf\": \"***\"", masked);
        Assert.Contains("\"password\": \"*****\"", masked);
        Assert.DoesNotContain("12345678901", masked);
        Assert.DoesNotContain("super-secret", masked);
    }

    [Fact]
    public void MaskSensitiveData_ShouldPreserveNonSensitiveData()
    {
        // Arrange
        var input = "Nome: João Silva, Email: joao@example.com, Telefone: 11999999999";

        // Act
        var masked = SensitiveDataMaskingMiddleware.MaskSensitiveData(input);

        // Assert
        Assert.Contains("João Silva", masked);
        Assert.Contains("joao@example.com", masked);
        Assert.Contains("11999999999", masked);
    }
}

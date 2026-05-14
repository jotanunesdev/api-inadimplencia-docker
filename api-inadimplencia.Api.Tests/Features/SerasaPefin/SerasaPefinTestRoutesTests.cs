using Xunit;
using ApiInadimplencia.Domain.SerasaPefin;

namespace api_inadimplencia.Api.Tests.Features.SerasaPefin;

[Trait("Category", "Unit")]
[Trait("Feature", "SerasaPefin")]
[Trait("SubFeature", "TestRoutes")]
public class SerasaPefinTestRoutesTests
{
    [Fact]
    public void UatAuthorizedDocuments_ShouldHaveExactly8Documents()
    {
        // Act
        var count = SerasaPefinConstants.UatAuthorizedDocuments.Count;

        // Assert
        Assert.Equal(8, count);
    }

    [Fact]
    public void UatAuthorizedDocuments_ShouldContainExpectedDocuments()
    {
        // Arrange
        var expectedDocuments = new[]
        {
            "00001209523", // CLIENTE TESTE ABCB
            "00008441448", // BJRNRNSD OIOIE
            "07420565899", // TESTE CPF SEM POSITIVO
            "04236798484", // NCUH KLCOHKKHH ECAJAE NCGMLU
            "16881670052", // TST PEFIN
            "11572467886", // TST FLEX
            "43557445000180", // ESFERA ARENA E NEGOCIOS SPE LTDA
            "00079854000105", // U F NXALWPULN ZK EWCQIXG
        };

        // Act & Assert
        foreach (var doc in expectedDocuments)
        {
            Assert.Contains(doc, SerasaPefinConstants.UatAuthorizedDocuments);
        }
    }

    [Fact]
    public void WebhookEventType_Inclusao_ShouldParseCorrectly()
    {
        // Act
        var result = Enum.TryParse<WebhookEventType>("inclusao", true, out var eventType);

        // Assert
        Assert.True(result);
        Assert.Equal(WebhookEventType.Inclusao, eventType);
    }

    [Fact]
    public void WebhookEventType_Avalista_ShouldParseCorrectly()
    {
        // Act
        var result = Enum.TryParse<WebhookEventType>("avalista", true, out var eventType);

        // Assert
        Assert.True(result);
        Assert.Equal(WebhookEventType.Avalista, eventType);
    }

    [Fact]
    public void WebhookEventType_Baixa_ShouldParseCorrectly()
    {
        // Act
        var result = Enum.TryParse<WebhookEventType>("baixa", true, out var eventType);

        // Assert
        Assert.True(result);
        Assert.Equal(WebhookEventType.Baixa, eventType);
    }

    [Fact]
    public void WebhookEventType_Invalid_ShouldNotParse()
    {
        // Act
        var result = Enum.TryParse<WebhookEventType>("invalid", true, out var eventType);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WebhookResultado_Sucesso_ShouldParseCorrectly()
    {
        // Act
        var result = Enum.TryParse<WebhookResultado>("sucesso", true, out var resultado);

        // Assert
        Assert.True(result);
        Assert.Equal(WebhookResultado.Sucesso, resultado);
    }

    [Fact]
    public void WebhookResultado_Erro_ShouldParseCorrectly()
    {
        // Act
        var result = Enum.TryParse<WebhookResultado>("erro", true, out var resultado);

        // Assert
        Assert.True(result);
        Assert.Equal(WebhookResultado.Erro, resultado);
    }

    [Fact]
    public void WebhookResultado_Invalid_ShouldNotParse()
    {
        // Act
        var result = Enum.TryParse<WebhookResultado>("invalid", true, out var resultado);

        // Assert
        Assert.False(result);
    }
}

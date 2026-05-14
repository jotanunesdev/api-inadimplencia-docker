using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class ProtocoloGeneratorIntegrationTests
{
    [Fact]
    public async Task GerarProtocoloAsync_DeveGerarProtocoloComFormatoCorreto()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ILegacySqlExecutor>();
        var protocoloEsperado = "2025011500001";
        
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, protocoloEsperado, 0));

        var generator = new ProtocoloGenerator(mockSqlExecutor.Object);

        // Act
        var result = await generator.GerarProtocoloAsync();

        // Assert
        result.Should().Be(protocoloEsperado);
        result.Should().MatchRegex(@"^\d{13}$"); // AAAAMMDD##### format
        mockSqlExecutor.Verify(e => e.ExecuteAsync(
            "Protocolo.Gerar",
            It.Is<Dictionary<string, object?>>(d => d.ContainsKey("DataAtual")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GerarProtocoloAsync_QuandoSqlRetornaVazio_DeveLancarExcecao()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ILegacySqlExecutor>();
        
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null, 0));

        var generator = new ProtocoloGenerator(mockSqlExecutor.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GerarProtocoloAsync());
    }

    [Fact]
    public async Task GerarProtocoloAsync_QuandoSqlNaoConfigurado_DeveLancarExcecao()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ILegacySqlExecutor>();
        
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LegacySqlResult?)null!);

        var generator = new ProtocoloGenerator(mockSqlExecutor.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GerarProtocoloAsync());
    }

    [Fact]
    public async Task GerarProtocoloAsync_DevePassarDataAtualComoParametro()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ILegacySqlExecutor>();
        var dataEsperada = DateTime.Now.ToString("yyyyMMdd");
        
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, "2025011500001", 0));

        var generator = new ProtocoloGenerator(mockSqlExecutor.Object);

        // Act
        await generator.GerarProtocoloAsync();

        // Assert
        mockSqlExecutor.Verify(e => e.ExecuteAsync(
            "Protocolo.Gerar",
            It.Is<Dictionary<string, object?>>(d => 
                d.ContainsKey("DataAtual")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

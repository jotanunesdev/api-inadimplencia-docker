using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Inadimplencias.Queries;

public class ListInadimplenciasQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsData_ReturnsMappedDtos()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var expectedData = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["CLIENTE"] = "João Silva",
                ["CPF_CNPJ"] = "12345678900",
                ["NUM_VENDA"] = 12345,
                ["EMPREENDIMENTO"] = "Residencial A",
                ["BLOCO"] = "1",
                ["UNIDADE"] = "101",
                ["QTD_PARCELAS_INADIMPLENTES"] = 3,
                ["STATUS_REPASSE"] = "Pendente",
                ["SCORE"] = "Alto",
                ["SUGESTAO"] = "Contatar",
                ["VENCIMENTO_MAIS_ANTIGO"] = new DateTime(2024, 1, 15),
                ["VALOR_TOTAL_EM_ABERTO"] = 15000.50,
                ["VALOR_INADIMPLENTE"] = 5000.25,
                ["VALOR_NAO_CONTRATUAL_INAD"] = null,
                ["VALOR_POUPANCA_INAD"] = null,
                ["PROXIMA_ACAO"] = "Ligar",
            }
        };

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.List",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, expectedData));

        var handler = new ListInadimplenciasQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new ListInadimplenciasQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].CLIENTE.Should().Be("João Silva");
        result[0].NUM_VENDA.Should().Be(12345);
        result[0].PROXIMA_ACAO.Should().Be("Ligar");
    }

    [Fact]
    public async Task HandleAsync_WhenExecutorNotConfigured_ReturnsEmptyList()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.List",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(false, null));

        var handler = new ListInadimplenciasQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new ListInadimplenciasQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsNull_ReturnsEmptyList()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.List",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null));

        var handler = new ListInadimplenciasQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new ListInadimplenciasQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}

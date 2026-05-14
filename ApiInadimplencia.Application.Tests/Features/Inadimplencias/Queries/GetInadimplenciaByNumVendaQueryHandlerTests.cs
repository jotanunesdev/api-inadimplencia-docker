using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Inadimplencias.Queries;

public class GetInadimplenciaByNumVendaQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsData_ReturnsMappedDto()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var numVenda = 12345;
        var expectedData = new Dictionary<string, object?>
        {
            ["CLIENTE"] = "Pedro Costa",
            ["CPF_CNPJ"] = "98765432100",
            ["NUM_VENDA"] = 12345,
            ["EMPREENDIMENTO"] = "Residencial C",
            ["BLOCO"] = "3",
            ["UNIDADE"] = "303",
            ["QTD_PARCELAS_INADIMPLENTES"] = 5,
            ["STATUS_REPASSE"] = "Cancelado",
            ["SCORE"] = "Baixo",
            ["SUGESTAO"] = "Negociar",
            ["VENCIMENTO_MAIS_ANTIGO"] = new DateTime(2024, 3, 10),
            ["VALOR_TOTAL_EM_ABERTO"] = 30000.00,
            ["VALOR_INADIMPLENTE"] = 10000.00,
            ["VALOR_NAO_CONTRATUAL_INAD"] = null,
            ["VALOR_POUPANCA_INAD"] = null,
            ["PROXIMA_ACAO"] = "Negociar",
        };

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.ByNumVenda",
                It.Is<IReadOnlyDictionary<string, object?>>(d => d["numVenda"]?.ToString() == numVenda.ToString()),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, expectedData));

        var handler = new GetInadimplenciaByNumVendaQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByNumVendaQuery(numVenda), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.CLIENTE.Should().Be("Pedro Costa");
        result.NUM_VENDA.Should().Be(12345);
        result.PROXIMA_ACAO.Should().Be("Negociar");
    }

    [Fact]
    public async Task HandleAsync_WhenExecutorNotConfigured_ReturnsNull()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.ByNumVenda",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(false, null));

        var handler = new GetInadimplenciaByNumVendaQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByNumVendaQuery(12345), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsNull_ReturnsNull()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.ByNumVenda",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null));

        var handler = new GetInadimplenciaByNumVendaQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByNumVendaQuery(12345), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

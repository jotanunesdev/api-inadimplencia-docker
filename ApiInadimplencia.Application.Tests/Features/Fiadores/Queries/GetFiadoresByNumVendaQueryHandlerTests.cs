using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Queries;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Fiadores.Queries;

public class GetFiadoresByNumVendaQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsData_ReturnsMappedDtos()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var numVenda = 12345;
        var expectedData = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["ID_ASSOCIADO"] = 1001,
                ["ID_RESERVA"] = 2001,
                ["ID_PESSOA"] = 3001,
                ["NOME"] = "Fiador 1",
                ["DOCUMENTO"] = "11122233344",
                ["DATA_CADASTRO"] = new DateTime(2023, 1, 15),
                ["RENDA_FAMILIAR"] = 5000.00,
                ["TIPO_ASSOCIACAO"] = "Fiador",
                ["NUM_VENDA"] = 12345,
                ["ENDERECO"] = "Rua A, 123",
            },
            new()
            {
                ["ID_ASSOCIADO"] = 1002,
                ["ID_RESERVA"] = 2002,
                ["ID_PESSOA"] = 3002,
                ["NOME"] = "Fiador 2",
                ["DOCUMENTO"] = "55566677788",
                ["DATA_CADASTRO"] = new DateTime(2023, 2, 20),
                ["RENDA_FAMILIAR"] = 6000.00,
                ["TIPO_ASSOCIACAO"] = "Fiador",
                ["NUM_VENDA"] = 12345,
                ["ENDERECO"] = "Rua B, 456",
            }
        };

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Fiadores.ByNumVenda",
                It.Is<IReadOnlyDictionary<string, object?>>(d => d["numVenda"]?.ToString() == numVenda.ToString()),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, expectedData));

        var handler = new GetFiadoresByNumVendaQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByNumVendaQuery(numVenda), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].NOME.Should().Be("Fiador 1");
        result[0].NUM_VENDA.Should().Be(12345);
        result[1].NOME.Should().Be("Fiador 2");
    }

    [Fact]
    public async Task HandleAsync_WhenExecutorNotConfigured_ReturnsEmptyList()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor
            .Setup(x => x.QueryAsync(
                "Fiadores.ByNumVenda",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(false, null));

        var handler = new GetFiadoresByNumVendaQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByNumVendaQuery(12345), CancellationToken.None);

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
                "Fiadores.ByNumVenda",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null));

        var handler = new GetFiadoresByNumVendaQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByNumVendaQuery(12345), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}

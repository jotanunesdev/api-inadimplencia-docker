using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;
using ApiInadimplencia.Application.Features.Fiadores.Queries;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Fiadores.Queries;

public class GetFiadoresByCpfQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsData_ReturnsMappedDtos()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var cpf = "11122233344";
        var expectedData = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["ID_ASSOCIADO"] = 1001,
                ["ID_RESERVA"] = 2001,
                ["ID_PESSOA"] = 3001,
                ["NOME"] = "João Fiador",
                ["DOCUMENTO"] = "11122233344",
                ["DATA_CADASTRO"] = new DateTime(2023, 1, 15),
                ["RENDA_FAMILIAR"] = 5000.00,
                ["TIPO_ASSOCIACAO"] = "Fiador",
                ["NUM_VENDA"] = 12345,
                ["ENDERECO"] = "Rua A, 123",
            }
        };

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Fiadores.ByCpf",
                It.Is<IReadOnlyDictionary<string, object?>>(d => d["cpf"]?.ToString() == cpf),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, expectedData));

        var handler = new GetFiadoresByCpfQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByCpfQuery(cpf), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].NOME.Should().Be("João Fiador");
        result[0].DOCUMENTO.Should().Be("11122233344");
    }

    [Fact]
    public async Task HandleAsync_WhenCpfIsNormalized_PassesNormalizedCpfToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var cpfWithMask = "111.222.333-44";
        var expectedCpf = "11122233344";

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Fiadores.ByCpf",
                It.Is<IReadOnlyDictionary<string, object?>>(d => d["cpf"]?.ToString() == expectedCpf),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));

        var handler = new GetFiadoresByCpfQueryHandler(mockExecutor.Object);

        // Act
        await handler.HandleAsync(new GetFiadoresByCpfQuery(cpfWithMask), CancellationToken.None);

        // Assert
        mockExecutor.Verify(x => x.QueryAsync(
            "Fiadores.ByCpf",
            It.Is<IReadOnlyDictionary<string, object?>>(d => d["cpf"]?.ToString() == expectedCpf),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenExecutorNotConfigured_ReturnsEmptyList()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor
            .Setup(x => x.QueryAsync(
                "Fiadores.ByCpf",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(false, null));

        var handler = new GetFiadoresByCpfQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByCpfQuery("11122233344"), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}

using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Inadimplencias.Queries;

public class GetInadimplenciaByCpfQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExecutorReturnsData_ReturnsMappedDtos()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var cpf = "12345678900";
        var expectedData = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["CLIENTE"] = "Maria Santos",
                ["CPF_CNPJ"] = "12345678900",
                ["NUM_VENDA"] = 54321,
                ["EMPREENDIMENTO"] = "Residencial B",
                ["BLOCO"] = "2",
                ["UNIDADE"] = "202",
                ["QTD_PARCELAS_INADIMPLENTES"] = 2,
                ["STATUS_REPASSE"] = "Atrasado",
                ["SCORE"] = "Médio",
                ["SUGESTAO"] = "Visitar",
                ["VENCIMENTO_MAIS_ANTIGO"] = new DateTime(2024, 2, 20),
                ["VALOR_TOTAL_EM_ABERTO"] = 20000.75,
                ["VALOR_INADIMPLENTE"] = 7500.50,
                ["VALOR_NAO_CONTRATUAL_INAD"] = null,
                ["VALOR_POUPANCA_INAD"] = null,
                ["PROXIMA_ACAO"] = "Visitar",
            }
        };

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.ByCpf",
                It.Is<IReadOnlyDictionary<string, object?>>(d => d["cpf"]?.ToString() == cpf),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, expectedData));

        var handler = new GetInadimplenciaByCpfQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByCpfQuery(cpf), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].CLIENTE.Should().Be("Maria Santos");
        result[0].NUM_VENDA.Should().Be(54321);
        result[0].PROXIMA_ACAO.Should().Be("Visitar");
    }

    [Fact]
    public async Task HandleAsync_WhenCpfIsNormalized_PassesNormalizedCpfToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var cpfWithMask = "123.456.789-00";
        var expectedCpf = "12345678900";

        mockExecutor
            .Setup(x => x.QueryAsync(
                "Inadimplencia.ByCpf",
                It.Is<IReadOnlyDictionary<string, object?>>(d => d["cpf"]?.ToString() == expectedCpf),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));

        var handler = new GetInadimplenciaByCpfQueryHandler(mockExecutor.Object);

        // Act
        await handler.HandleAsync(new GetInadimplenciaByCpfQuery(cpfWithMask), CancellationToken.None);

        // Assert
        mockExecutor.Verify(x => x.QueryAsync(
            "Inadimplencia.ByCpf",
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
                "Inadimplencia.ByCpf",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(false, null));

        var handler = new GetInadimplenciaByCpfQueryHandler(mockExecutor.Object);

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByCpfQuery("12345678900"), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}

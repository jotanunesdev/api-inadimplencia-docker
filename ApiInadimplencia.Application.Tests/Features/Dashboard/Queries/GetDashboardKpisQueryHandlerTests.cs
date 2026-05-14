using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Dashboard.Queries;

public class GetDashboardKpisQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenNotConfigured_ReturnsEmptyKpis()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor.Setup(e => e.IsConfigured).Returns(false);
        
        var handler = new GetDashboardKpisQueryHandler(mockExecutor.Object);
        var query = new GetDashboardKpisQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalVendas);
        Assert.Equal(0, result.TotalClientes);
        Assert.Equal(0, result.SaldoTotal);
        Assert.Equal(0, result.ValorInadimplente);
        Assert.Equal(0, result.PercentualInadimplencia);
    }

    [Fact]
    public async Task HandleAsync_WhenDataIsNull_ReturnsEmptyKpis()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor.Setup(e => e.IsConfigured).Returns(true);
        mockExecutor.Setup(e => e.QueryAsync(
            "Dashboard.Kpis",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null));
        
        var handler = new GetDashboardKpisQueryHandler(mockExecutor.Object);
        var query = new GetDashboardKpisQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalVendas);
        Assert.Equal(0, result.TotalClientes);
        Assert.Equal(0, result.SaldoTotal);
        Assert.Equal(0, result.ValorInadimplente);
        Assert.Equal(0, result.PercentualInadimplencia);
    }

    [Fact]
    public async Task HandleAsync_WhenDataExists_ReturnsMappedKpis()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["TOTAL_VENDAS"] = 100,
            ["TOTAL_CLIENTES"] = 80,
            ["SALDO_TOTAL"] = 500000.50m,
            ["VALOR_INADIMPLENTE"] = 250000.25m,
            ["PERCENTUAL_INADIMPLENCIA"] = 50.0m
        };

        var mockExecutor = new Mock<ILegacySqlExecutor>();
        mockExecutor.Setup(e => e.IsConfigured).Returns(true);
        mockExecutor.Setup(e => e.QueryAsync(
            "Dashboard.Kpis",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, data));
        
        var handler = new GetDashboardKpisQueryHandler(mockExecutor.Object);
        var query = new GetDashboardKpisQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalVendas);
        Assert.Equal(80, result.TotalClientes);
        Assert.Equal(500000.50m, result.SaldoTotal);
        Assert.Equal(250000.25m, result.ValorInadimplente);
        Assert.Equal(50.0m, result.PercentualInadimplencia);
    }

    [Fact]
    public void Constructor_WhenExecutorIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GetDashboardKpisQueryHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var mockExecutor = new Mock<ILegacySqlExecutor>();
        var handler = new GetDashboardKpisQueryHandler(mockExecutor.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            handler.HandleAsync(null!, CancellationToken.None));
    }
}

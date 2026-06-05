using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Dashboard.Queries;

/// <summary>
/// Testes para <c>GetMotivosBaixaQueryHandler</c>:
/// validação de janela, fallback quando SQL indisponível, mapping correto.
/// </summary>
public sealed class GetMotivosBaixaQueryHandlerTests
{
    private readonly Mock<ILegacySqlExecutor> _executorMock = new();

    private GetMotivosBaixaQueryHandler BuildHandler() => new(_executorMock.Object);

    [Fact]
    public void Constructor_ExecutorNull_DeveLancar()
    {
        Assert.Throws<ArgumentNullException>(() => new GetMotivosBaixaQueryHandler(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(100)]
    public async Task HandleAsync_MesesForaDaFaixa_DeveLancar(int meses)
    {
        var handler = BuildHandler();
        var act = () => handler.HandleAsync(new GetMotivosBaixaQuery(meses), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        ex.Which.Message.Should().Contain("MESES_INVALIDO");
    }

    [Fact]
    public async Task HandleAsync_NaoConfigurado_DeveRetornarListaVazia()
    {
        _executorMock.Setup(e => e.IsConfigured).Returns(false);

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetMotivosBaixaQuery(), CancellationToken.None);

        result.Should().BeEmpty();
        _executorMock.Verify(e => e.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_QueryRetornaVazio_DeveRetornarListaVazia()
    {
        _executorMock.Setup(e => e.IsConfigured).Returns(true);
        _executorMock
            .Setup(e => e.QueryAsync("Dashboard.BaixaMotivos", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetMotivosBaixaQuery(12), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_QueryComDados_DeveMapearCorretamente()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["MOTIVO"] = (byte)3,
                ["DESCRICAO"] = "POR SOLICITACAO DO CLIENTE",
                ["QTD"] = 10L,
                ["PERCENTUAL"] = 40.00m,
            },
            new()
            {
                ["MOTIVO"] = (byte)1,
                ["DESCRICAO"] = "PAGAMENTO DA DIVIDA",
                ["QTD"] = 15L,
                ["PERCENTUAL"] = 60.00m,
            },
        };

        _executorMock.Setup(e => e.IsConfigured).Returns(true);
        _executorMock
            .Setup(e => e.QueryAsync("Dashboard.BaixaMotivos", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, rows));

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetMotivosBaixaQuery(12), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Motivo.Should().Be(3);
        result[0].Descricao.Should().Be("POR SOLICITACAO DO CLIENTE");
        result[0].Qtd.Should().Be(10);
        result[0].Percentual.Should().Be(40.00m);
        result[1].Motivo.Should().Be(1);
        result[1].Qtd.Should().Be(15);
    }

    [Fact]
    public async Task HandleAsync_CoercaoNumericaCompatibility_QtdComoInt32_FuncionaIgual()
    {
        // SQL pode retornar QTD como Int32 (COUNT) em alguns drivers — RowValueConverter coerce.
        var rows = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["MOTIVO"] = 19,
                ["DESCRICAO"] = "RENEGOCIACAO DA DIVIDA POR ACORDO",
                ["QTD"] = 5,
                ["PERCENTUAL"] = 100.00,
            },
        };

        _executorMock.Setup(e => e.IsConfigured).Returns(true);
        _executorMock
            .Setup(e => e.QueryAsync("Dashboard.BaixaMotivos", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, rows));

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetMotivosBaixaQuery(12), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Motivo.Should().Be(19);
        result[0].Qtd.Should().Be(5);
        result[0].Percentual.Should().Be(100.00m);
    }
}

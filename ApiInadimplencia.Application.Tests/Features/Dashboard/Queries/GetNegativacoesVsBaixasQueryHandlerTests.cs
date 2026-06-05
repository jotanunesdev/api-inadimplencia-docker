using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Dashboard.Queries;

/// <summary>
/// Testes para <c>GetNegativacoesVsBaixasQueryHandler</c>:
/// validação de janela, fallback quando SQL indisponível, ordenação por
/// ANO_MES, truncamento em-memória para janelas menores que 12.
/// </summary>
public sealed class GetNegativacoesVsBaixasQueryHandlerTests
{
    private readonly Mock<ILegacySqlExecutor> _executorMock = new();

    private GetNegativacoesVsBaixasQueryHandler BuildHandler() => new(_executorMock.Object);

    [Fact]
    public void Constructor_ExecutorNull_DeveLancar()
    {
        Assert.Throws<ArgumentNullException>(() => new GetNegativacoesVsBaixasQueryHandler(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task HandleAsync_MesesForaDaFaixa_DeveLancar(int meses)
    {
        var handler = BuildHandler();
        var act = () => handler.HandleAsync(new GetNegativacoesVsBaixasQuery(meses), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task HandleAsync_NaoConfigurado_DeveRetornarListaVazia()
    {
        _executorMock.Setup(e => e.IsConfigured).Returns(false);

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetNegativacoesVsBaixasQuery(12), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_QueryComDados_DeveMapearCorretamente()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["ANO_MES"] = "2025-08", ["QTD_NEGATIVACOES"] = 5L, ["QTD_BAIXAS"] = 1L },
            new() { ["ANO_MES"] = "2025-07", ["QTD_NEGATIVACOES"] = 3L, ["QTD_BAIXAS"] = 2L },
        };

        _executorMock.Setup(e => e.IsConfigured).Returns(true);
        _executorMock
            .Setup(e => e.QueryAsync("Dashboard.NegativacaoBaixaMensal", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, rows));

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetNegativacoesVsBaixasQuery(12), CancellationToken.None);

        result.Should().HaveCount(2);
        // Ordenado por ANO_MES asc
        result[0].AnoMes.Should().Be("2025-07");
        result[0].QtdNegativacoes.Should().Be(3);
        result[0].QtdBaixas.Should().Be(2);
        result[1].AnoMes.Should().Be("2025-08");
    }

    [Fact]
    public async Task HandleAsync_MesesMenorQueDados_DeveTruncarMantendoMaisRecentes()
    {
        // 12 meses de dados retornados pela view; query pede apenas 3 meses.
        var rows = Enumerable.Range(1, 12)
            .Select(m => new Dictionary<string, object?>
            {
                ["ANO_MES"] = $"2025-{m:D2}",
                ["QTD_NEGATIVACOES"] = (long)m,
                ["QTD_BAIXAS"] = (long)(m * 2),
            })
            .Cast<Dictionary<string, object?>>()
            .ToList();

        _executorMock.Setup(e => e.IsConfigured).Returns(true);
        _executorMock
            .Setup(e => e.QueryAsync("Dashboard.NegativacaoBaixaMensal", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, rows));

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetNegativacoesVsBaixasQuery(3), CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].AnoMes.Should().Be("2025-10");
        result[1].AnoMes.Should().Be("2025-11");
        result[2].AnoMes.Should().Be("2025-12");
    }

    [Fact]
    public async Task HandleAsync_MesesMaiorQueDados_DeveRetornarTodosOsDados()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["ANO_MES"] = "2025-12", ["QTD_NEGATIVACOES"] = 1L, ["QTD_BAIXAS"] = 0L },
        };

        _executorMock.Setup(e => e.IsConfigured).Returns(true);
        _executorMock
            .Setup(e => e.QueryAsync("Dashboard.NegativacaoBaixaMensal", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, rows));

        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetNegativacoesVsBaixasQuery(24), CancellationToken.None);

        result.Should().HaveCount(1);
    }
}

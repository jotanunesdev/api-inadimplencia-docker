using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class RecuperaMetricQueryTests
{
    [Theory]
    [InlineData("  Maria  ", "%Maria%")]
    [InlineData("A%_[~", "%A~%~_~[~~%")]
    [InlineData("O'Neal", "%O'Neal%")]
    [InlineData("  ", null)]
    public async Task Carteira_FiltraClienteComParametroLiteral(string input, string? expected)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        executor.Setup(x => x.QueryAsync("Dashboard.CarteiraInadimplenteDetalhes",
                It.Is<IReadOnlyDictionary<string, object?>>(p => (string?)p["cliente"] == expected),
                false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery("carteira-inadimplente-detalhes", Cliente: input));
        executor.VerifyAll();
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("carteira-inadimplente-detalhes", 151)]
    [InlineData("carteira-inadimplente-parcelas", 3)]
    [InlineData("conversoes-quantidade", 3)]
    public async Task Cliente_InvalidoNaoConsultaSql(string metric, int length)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object)
            .HandleAsync(new GetMetricQuery(metric, Cliente: new string('A', length))));
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("carteira-inadimplente-detalhes", "Dashboard.CarteiraInadimplenteDetalhes", null)]
    [InlineData("CARTEIRA-INADIMPLENTE-PARCELAS", "Dashboard.CarteiraInadimplenteParcelas", 10)]
    public async Task CarteiraAtual_PaginaSemConsultarHistorico(string metric, string key, int? venda)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        executor.Setup(x => x.QueryAsync(key, It.Is<IReadOnlyDictionary<string, object?>>(p =>
                (int)p["limit"]! == 100 && (int)p["offset"]! == 20 && p["dataInicio"] == null
                && p["dataFim"] == null && (int?)p["numVenda"] == venda), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery(metric, Limit: 1000, Offset: 20, NumVenda: venda));
        executor.VerifyAll();
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("carteira-inadimplente-parcelas", null, null, 0)]
    [InlineData("carteira-inadimplente-parcelas", null, 0, 0)]
    [InlineData("carteira-inadimplente-detalhes", null, 1, 0)]
    [InlineData("carteira-inadimplente-detalhes", "2026-01-01", null, 0)]
    [InlineData("carteira-inadimplente-detalhes", null, null, -1)]
    public async Task CarteiraAtual_ValidaAntesDeConsultar(string metric, string? inicio, int? venda, int offset)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object)
            .HandleAsync(new GetMetricQuery(metric, DataInicio: inicio, Offset: offset, NumVenda: venda)));
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("situacao-parcelas-detalhes", null, null, null)]
    [InlineData("convertidos-mensais-detalhes", "2026-02-01", "2026-02-01", null)]
    [InlineData("convertidos-mensais", "2026-01-01", null, null)]
    [InlineData("convertidos-mensais", "2010-01-01", "2026-01-01", null)]
    [InlineData("situacao-clientes-detalhes", null, null, "INVALIDA")]
    public async Task Situacao_ValidaParametrosAntesDoSql(string metric, string? inicio, string? fim, string? situacao)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object)
            .HandleAsync(new GetMetricQuery(metric, inicio, fim, Situacao: situacao)));
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("convertidos-mensais", "Dashboard.ConvertidosMensais", 1000)]
    [InlineData("convertidos-mensais-detalhes", "Dashboard.ConvertidosMensaisDetalhes", 100)]
    [InlineData("situacao-clientes-detalhes", "Dashboard.SituacaoClientesDetalhes", 100)]
    [InlineData("situacao-parcelas-detalhes", "Dashboard.SituacaoParcelasDetalhes", 100)]
    public async Task Situacao_ReutilizaExecutorEPaginacao(string metric, string key, int limit)
    {
        var executor = new Mock<ILegacySqlExecutor>();
        IReadOnlyDictionary<string, object?>? received = null;
        executor.Setup(x => x.QueryAsync(key, It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, bool, CancellationToken>((_, parameters, _, _) => received = parameters)
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery(metric, "2026-02-28", "2026-02-28", 1000, Offset: 20, NumVenda: 1));
        Assert.Equal(limit, received!["limit"]);
        Assert.Equal(20, received["offset"]);
        Assert.Equal(1, received["numVenda"]);
    }

    [Theory]
    [InlineData("recuperacao-parcelas")]
    [InlineData("recuperacao-parcelas-detalhes")]
    [InlineData("RECUPERACAO-PARCELAS")]
    [InlineData("RECUPERACAO-PARCELAS-DETALHES")]
    public async Task RecuperacaoUsaFonteValidada(string metric)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        var detalhe = metric.EndsWith("-detalhes", StringComparison.OrdinalIgnoreCase);
        executor.Setup(x => x.QueryAsync(detalhe ? "Dashboard.RecuperacaoDetalhes" : "Dashboard.RecuperacaoMensal",
                It.Is<IReadOnlyDictionary<string, object?>>(p => (int)p["limit"]! == (detalhe ? 100 : 1000)
                    && (DateTime)p["dataInicio"]! == new DateTime(2026, 1, 1)
                    && (DateTime)p["dataFim"]! == new DateTime(2026, 2, 28)), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery(metric, "2026-01-01", "2026-02-28"));
        executor.VerifyAll();
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("recuperacao-parcelas-detalhes", null, null, null)]
    [InlineData("recuperacao-parcelas", "2026-01-01", null, null)]
    [InlineData("recuperacao-parcelas", "2026-03-01", "2026-02-01", null)]
    [InlineData("recuperacao-parcelas", "2010-01-01", "2026-01-01", null)]
    [InlineData("recuperacao-parcelas", "9999-01-01", "9999-12-31", null)]
    [InlineData("recuperacao-parcelas-detalhes", "2026-01-01", "2026-02-01", "REGULARIZADO")]
    public async Task RecuperacaoRejeitaPeriodoInvalidoEFiltroDeConversao(string metric, string? inicio, string? fim, string? situacao)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object)
            .HandleAsync(new GetMetricQuery(metric, inicio, fim, Situacao: situacao)));
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, null, 0, 20)]
    [InlineData("2026-09-10", "2026-09-11", 0, 20)]
    [InlineData("2026-09-10", "2026-09-10", -1, 20)]
    [InlineData("2026-09-10", "2026-09-10", 0, 0)]
    public async Task Detalhes_RejeitaDiaAusenteOuIntervaloEPaginacaoInvalidos(string? inicio, string? fim, int offset, int limit)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        var handler = new GetMetricQueryHandler(executor.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(new GetMetricQuery("ocorrencias-dia-detalhes", inicio, fim, limit, Offset: offset)));
        executor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Detalhes_EnviaDiaEOffsetELimitaPaginaNoServidor()
    {
        var executor = new Mock<ILegacySqlExecutor>();
        IReadOnlyDictionary<string, object?>? received = null;
        executor.Setup(x => x.QueryAsync("Dashboard.OcorrenciasDiaDetalhes", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, bool, CancellationToken>((_, parameters, _, _) => received = parameters)
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery("ocorrencias-dia-detalhes", "2026-09-10", "2026-09-10", 500, Offset: 20));
        Assert.Equal(100, received!["limit"]);
        Assert.Equal(20, received["offset"]);
        Assert.Equal(new DateTime(2026, 9, 10), received["dataInicio"]);
    }
}

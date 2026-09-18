using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class ObservacoesQueryTests
{
    [Theory]
    [InlineData("conversoes-quantidade", "Dashboard.ObservacoesMensais", 1000)]
    [InlineData("conversoes-quantidade-detalhes", "Dashboard.ObservacoesDetalhes", 100)]
    public async Task VerificaEstruturaEEnviaPeriodo(string metric, string key, int limit)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        executor.Setup(x => x.QueryAsync("Dashboard.ObservacoesDisponibilidade", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>> { new() { ["DISPONIVEL"] = 1 } }));
        executor.Setup(x => x.QueryAsync(key, It.Is<IReadOnlyDictionary<string, object?>>(p =>
                (int)p["limit"]! == limit && (DateTime)p["dataInicio"]! == new DateTime(2026, 9, 1)
                && (DateTime)p["dataFim"]! == new DateTime(2026, 9, 30)), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery(metric, "2026-09-01", "2026-09-30"));
        executor.VerifyAll();
        executor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SemMigrationNaoConsultaTabelasAusentesNemCriaCaptura()
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        executor.Setup(x => x.QueryAsync("Dashboard.ObservacoesDisponibilidade", It.IsAny<IReadOnlyDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>> { new() { ["DISPONIVEL"] = 0 } }));
        var rows = await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery("conversoes-quantidade"));
        Assert.Equal(0, rows.Single()["DISPONIVEL"]);
        executor.VerifyAll();
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("conversoes-quantidade", "2026-01-01", null, null)]
    [InlineData("conversoes-quantidade-detalhes", null, null, null)]
    [InlineData("conversoes-quantidade", "2010-01-01", "2026-01-01", null)]
    [InlineData("conversoes-quantidade", "2026-02-01", "2026-01-01", null)]
    [InlineData("conversoes-quantidade", null, null, "REGULARIZADO")]
    [InlineData("conversoes-quantidade-detalhes", "2026-01-01", "2026-01-31", "ADIMPLENTE")]
    public async Task ValidaFiltrosAntesDoSql(string metric, string? inicio, string? fim, string? situacao)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object)
            .HandleAsync(new GetMetricQuery(metric, inicio, fim, Situacao: situacao)));
        executor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NaoIgnoraFiltroSemHistorico()
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object)
            .HandleAsync(new GetMetricQuery("conversoes-quantidade", NomeUsuario: "operador")));
        executor.VerifyNoOtherCalls();
    }
}

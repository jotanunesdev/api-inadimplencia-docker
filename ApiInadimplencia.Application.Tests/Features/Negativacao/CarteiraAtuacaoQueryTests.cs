using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Queries;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class CarteiraAtuacaoQueryTests
{
    [Theory]
    [InlineData("carteira-atuacao", "Dashboard.CarteiraAtuacao", null, null)]
    [InlineData("carteira-juridica", "Dashboard.CarteiraJuridica", null, null)]
    [InlineData("carteira-juridica-detalhes", "Dashboard.CarteiraJuridicaDetalhes", null, null)]
    [InlineData("carteira-juridica-processos", "Dashboard.CarteiraJuridicaProcessos", null, 10)]
    [InlineData("carteira-atuacao-detalhes", "Dashboard.CarteiraAtuacaoDetalhes", "NAO_APTO", null)]
    [InlineData("CARTEIRA-ATUACAO-CONTATOS", "Dashboard.CarteiraAtuacaoContatos", "CICLO_PENDENTE", 10)]
    public async Task EncaminhaFiltrosEPaginacaoSemEscrita(string metric, string key, string? category, int? sale)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        executor.Setup(x => x.QueryAsync(key, It.Is<IReadOnlyDictionary<string, object?>>(p =>
            (int)p["limit"]! == 100 && (int)p["offset"]! == 20 && (string?)p["cliente"] == "%Maria~%%"
            && (string?)p["nomeUsuario"] == "operador" && (string?)p["situacao"] == category
            && (int?)p["numVenda"] == sale), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, new List<Dictionary<string, object?>>()));
        await new GetMetricQueryHandler(executor.Object).HandleAsync(new GetMetricQuery(metric,
            Limit: 1000, Offset: 20, Cliente: " Maria% ", NomeUsuario: " Operador ", Situacao: category, NumVenda: sale));
        executor.VerifyAll();
        executor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("carteira-atuacao", "2026-01-01", null, null)]
    [InlineData("carteira-juridica", "2026-01-01", null, null)]
    [InlineData("carteira-juridica", null, "ATIVO", null)]
    [InlineData("carteira-juridica-detalhes", null, null, 10)]
    [InlineData("carteira-juridica-processos", null, null, 0)]
    [InlineData("carteira-atuacao", null, "NAO_APTO", null)]
    [InlineData("carteira-atuacao-detalhes", null, null, null)]
    [InlineData("carteira-atuacao-detalhes", null, "APTO_COM_ACAO", null)]
    [InlineData("carteira-atuacao-detalhes", null, "NAO_APTO", 10)]
    [InlineData("carteira-atuacao-contatos", null, "CICLO_PENDENTE", null)]
    [InlineData("carteira-atuacao-contatos", null, "CICLO_PENDENTE", 0)]
    public async Task NaoAceitaFiltroIgnoradoOuClassificacaoPresumida(string metric, string? date, string? category, int? sale)
    {
        var executor = new Mock<ILegacySqlExecutor>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMetricQueryHandler(executor.Object).HandleAsync(
            new GetMetricQuery(metric, DataInicio: date, Situacao: category, NumVenda: sale)));
        executor.VerifyNoOtherCalls();
    }
}

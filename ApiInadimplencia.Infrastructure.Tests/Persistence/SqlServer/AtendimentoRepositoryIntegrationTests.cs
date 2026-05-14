using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Atendimentos.Commands;
using ApiInadimplencia.Domain.Atendimentos;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class AtendimentoRepositoryIntegrationTests
{
    [Fact]
    public async Task AddAsync_DevePersistirAtendimentoComJsonDadosVenda()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ILegacySqlExecutor>();
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null, 0));

        var repository = new AtendimentoRepository(mockSqlExecutor.Object);
        var dadosVenda = new Dictionary<string, object?>
        {
            { "NUM_VENDA", 12345 },
            { "NOME_CLIENTE", "Test Client" },
            { "VALOR", 100000.50 }
        };
        var dadosVendaJson = System.Text.Json.JsonSerializer.Serialize(dadosVenda);
        var atendimento = Atendimento.Criar(
            "2025011500001",
            "12345678901",
            12345,
            dadosVendaJson);

        // Act
        await repository.AddAsync(atendimento);

        // Assert
        mockSqlExecutor.Verify(e => e.ExecuteAsync(
            "Atendimento.Insert",
            It.Is<Dictionary<string, object?>>(d =>
                d["ID"] != null && d["ID"].ToString() == atendimento.Id.ToString() &&
                d["PROTOCOLO"] != null && d["PROTOCOLO"].ToString() == atendimento.Protocolo &&
                d["CPF"] != null && d["CPF"].ToString() == atendimento.Cpf &&
                Convert.ToInt32(d["NUM_VENDA_FK"]) == atendimento.NumVendaFk &&
                d["DADOS_VENDA_JSON"] != null && d["DADOS_VENDA_JSON"].ToString() == dadosVendaJson),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Criar_DeveGerarProtocoloFormatoCorreto()
    {
        // Arrange & Act
        var dadosVendaJson = "{\"cliente\":\"Test\"}";
        var atendimento = Atendimento.Criar(
            "2025011500001",
            "12345678901",
            12345,
            dadosVendaJson);

        // Assert
        atendimento.Protocolo.Should().Be("2025011500001");
        atendimento.DadosVendaJson.Should().Be(dadosVendaJson);
        atendimento.CriadoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}

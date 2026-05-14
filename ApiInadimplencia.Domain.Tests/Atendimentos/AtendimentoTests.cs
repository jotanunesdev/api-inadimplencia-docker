using ApiInadimplencia.Domain.Atendimentos;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Atendimentos;

public class AtendimentoTests
{
    [Fact]
    public void Criar_DeveCriarAtendimentoComDadosValidos()
    {
        // Arrange
        var protocolo = "2024011500001";
        var cpf = "12345678901";
        var numVendaFk = 12345;
        var dadosVendaJson = "{\"cliente\":\"Test\",\"valor\":1000.00}";

        // Act
        var atendimento = Atendimento.Criar(
            protocolo,
            cpf,
            numVendaFk,
            dadosVendaJson);

        // Assert
        atendimento.Should().NotBeNull();
        atendimento.Id.Should().NotBeEmpty();
        atendimento.Protocolo.Should().Be(protocolo);
        atendimento.Cpf.Should().Be(cpf);
        atendimento.NumVendaFk.Should().Be(numVendaFk);
        atendimento.DadosVendaJson.Should().Be(dadosVendaJson);
        atendimento.CriadoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Criar_DeveGerarIdUnico()
    {
        // Arrange
        var protocolo = "2024011500001";
        var cpf = "12345678901";
        var numVendaFk = 12345;
        var dadosVendaJson = "{\"cliente\":\"Test\"}";

        // Act
        var atendimento1 = Atendimento.Criar(protocolo, cpf, numVendaFk, dadosVendaJson);
        var atendimento2 = Atendimento.Criar(protocolo, cpf, numVendaFk, dadosVendaJson);

        // Assert
        atendimento1.Id.Should().NotBe(atendimento2.Id);
    }

    [Fact]
    public void Criar_DeveArmazenarJsonDadosVenda()
    {
        // Arrange
        var protocolo = "2024011500001";
        var cpf = "12345678901";
        var numVendaFk = 12345;
        var dadosVenda = new { cliente = "Test Customer", valor = 5000.50, parcelas = 10 };
        var dadosVendaJson = System.Text.Json.JsonSerializer.Serialize(dadosVenda);

        // Act
        var atendimento = Atendimento.Criar(protocolo, cpf, numVendaFk, dadosVendaJson);

        // Assert
        atendimento.DadosVendaJson.Should().Be(dadosVendaJson);
        atendimento.DadosVendaJson.Should().Contain("Test Customer");
    }

    [Fact]
    public void Criar_DeveDefinirTimestampDeCriacao()
    {
        // Arrange
        var protocolo = "2024011500001";
        var cpf = "12345678901";
        var numVendaFk = 12345;
        var dadosVendaJson = "{\"cliente\":\"Test\"}";
        var antes = DateTime.UtcNow;

        // Act
        var atendimento = Atendimento.Criar(protocolo, cpf, numVendaFk, dadosVendaJson);
        var depois = DateTime.UtcNow;

        // Assert
        atendimento.CriadoEm.Should().BeOnOrAfter(antes);
        atendimento.CriadoEm.Should().BeOnOrBefore(depois);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_ComProtocoloVazioOuNulo_DeveLancarExcecao(string? protocolo)
    {
        // Arrange & Act
        var action = () => Atendimento.Criar(
            protocolo!,
            "12345678901",
            12345,
            "{\"cliente\":\"Test\"}");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Protocolo*required*");
    }

    [Theory]
    [InlineData("20240115")] // Too short
    [InlineData("20240115000001")] // Too long
    [InlineData("20240115A0001")] // Contains letter
    public void Criar_ComProtocoloFormatoInvalido_DeveLancarExcecao(string protocoloInvalido)
    {
        // Arrange & Act
        var action = () => Atendimento.Criar(
            protocoloInvalido,
            "12345678901",
            12345,
            "{\"cliente\":\"Test\"}");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Protocolo*format*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_ComCpfVazioOuNulo_DeveLancarExcecao(string? cpf)
    {
        // Arrange & Act
        var action = () => Atendimento.Criar(
            "2024011500001",
            cpf!,
            12345,
            "{\"cliente\":\"Test\"}");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Cpf*required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Criar_ComNumVendaFkInvalido_DeveLancarExcecao(int numVendaFkInvalido)
    {
        // Arrange & Act
        var action = () => Atendimento.Criar(
            "2024011500001",
            "12345678901",
            numVendaFkInvalido,
            "{\"cliente\":\"Test\"}");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*NUM_VENDA*positive*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_ComDadosVendaJsonVazioOuNulo_DeveLancarExcecao(string? dadosVendaJson)
    {
        // Arrange & Act
        var action = () => Atendimento.Criar(
            "2024011500001",
            "12345678901",
            12345,
            dadosVendaJson!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DadosVendaJson*required*");
    }
}

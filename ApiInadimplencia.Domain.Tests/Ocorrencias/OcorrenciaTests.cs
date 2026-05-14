using ApiInadimplencia.Domain.Ocorrencias;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Ocorrencias;

public class OcorrenciaTests
{
    [Fact]
    public void Criar_DeveCriarOcorrenciaComDadosValidos()
    {
        // Arrange
        var numVendaFk = 12345;
        var nomeUsuarioFk = "testuser";
        var descricao = "Test occurrence";
        var statusOcorrencia = "ABERTO";
        var dtOcorrencia = new DateTime(2024, 1, 15);
        var horaOcorrencia = "14:30";
        var proximaAcao = "2024-01-20";
        var protocolo = "2024011500001";

        // Act
        var ocorrencia = Ocorrencia.Criar(
            numVendaFk,
            nomeUsuarioFk,
            descricao,
            statusOcorrencia,
            dtOcorrencia,
            horaOcorrencia,
            proximaAcao,
            protocolo);

        // Assert
        ocorrencia.Should().NotBeNull();
        ocorrencia.Id.Should().NotBeEmpty();
        ocorrencia.NumVendaFk.Should().Be(numVendaFk);
        ocorrencia.NomeUsuarioFk.Should().Be(nomeUsuarioFk);
        ocorrencia.Descricao.Should().Be(descricao);
        ocorrencia.StatusOcorrencia.Should().Be(statusOcorrencia);
        ocorrencia.DtOcorrencia.Should().Be(dtOcorrencia);
        ocorrencia.HoraOcorrencia.Should().Be(horaOcorrencia);
        ocorrencia.ProximaAcao.Should().Be(proximaAcao);
        ocorrencia.Protocolo.Should().Be(protocolo);
    }

    [Fact]
    public void Criar_DeveCriarOcorrenciaSemCamposOpcionais()
    {
        // Arrange
        var numVendaFk = 12345;
        var nomeUsuarioFk = "testuser";
        var descricao = "Test occurrence";
        var statusOcorrencia = "ABERTO";
        var dtOcorrencia = new DateTime(2024, 1, 15);
        var horaOcorrencia = "14:30";

        // Act
        var ocorrencia = Ocorrencia.Criar(
            numVendaFk,
            nomeUsuarioFk,
            descricao,
            statusOcorrencia,
            dtOcorrencia,
            horaOcorrencia);

        // Assert
        ocorrencia.Should().NotBeNull();
        ocorrencia.ProximaAcao.Should().BeNull();
        ocorrencia.Protocolo.Should().BeNull();
    }

    [Fact]
    public void Atualizar_DeveAtualizarCamposFornecidos()
    {
        // Arrange
        var ocorrencia = Ocorrencia.Criar(
            12345,
            "testuser",
            "Original description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        // Act
        ocorrencia.Atualizar(
            descricao: "Updated description",
            statusOcorrencia: "FECHADO",
            proximaAcao: "2024-01-25");

        // Assert
        ocorrencia.Descricao.Should().Be("Updated description");
        ocorrencia.StatusOcorrencia.Should().Be("FECHADO");
        ocorrencia.ProximaAcao.Should().Be("2024-01-25");
    }

    [Fact]
    public void Atualizar_DeveAtualizarApenasCamposNulosFornecidos()
    {
        // Arrange
        var ocorrencia = Ocorrencia.Criar(
            12345,
            "testuser",
            "Original description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30",
            "2024-01-20",
            "2024011500001");

        // Act
        ocorrencia.Atualizar(descricao: "Updated description");

        // Assert
        ocorrencia.Descricao.Should().Be("Updated description");
        ocorrencia.StatusOcorrencia.Should().Be("ABERTO");
        ocorrencia.DtOcorrencia.Should().Be(new DateTime(2024, 1, 15));
        ocorrencia.HoraOcorrencia.Should().Be("14:30");
        ocorrencia.ProximaAcao.Should().Be("2024-01-20");
        ocorrencia.Protocolo.Should().Be("2024011500001");
    }

    [Fact]
    public void Atualizar_DeveAtualizarDataEHora()
    {
        // Arrange
        var ocorrencia = Ocorrencia.Criar(
            12345,
            "testuser",
            "Original description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        var novaData = new DateTime(2024, 2, 20);
        var novaHora = "16:45";

        // Act
        ocorrencia.Atualizar(dtOcorrencia: novaData, horaOcorrencia: novaHora);

        // Assert
        ocorrencia.DtOcorrencia.Should().Be(novaData);
        ocorrencia.HoraOcorrencia.Should().Be(novaHora);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Criar_ComNumVendaFkInvalido_DeveLancarExcecao(int numVendaFkInvalido)
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            numVendaFkInvalido,
            "testuser",
            "Test occurrence",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*NUM_VENDA*positive*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_ComNomeUsuarioFkVazioOuNulo_DeveLancarExcecao(string? nomeUsuarioFk)
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            12345,
            nomeUsuarioFk!,
            "Test occurrence",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*NomeUsuarioFk*required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_ComDescricaoVaziaOuNula_DeveLancarExcecao(string? descricao)
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            12345,
            "testuser",
            descricao!,
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Descricao*required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_ComStatusOcorrenciaVazioOuNulo_DeveLancarExcecao(string? statusOcorrencia)
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            12345,
            "testuser",
            "Test occurrence",
            statusOcorrencia!,
            new DateTime(2024, 1, 15),
            "14:30");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*StatusOcorrencia*required*");
    }

    [Fact]
    public void Criar_ComHoraOcorrenciaVaziaOuNula_DeveLancarExcecao()
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            12345,
            "testuser",
            "Test occurrence",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*HoraOcorrencia*required*");
    }

    [Fact]
    public void Criar_ComHoraOcorrenciaFormatoInvalido_DeveLancarExcecao()
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            12345,
            "testuser",
            "Test occurrence",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "25:00");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*HoraOcorrencia*format*");
    }

    [Fact]
    public void Criar_ComDataOcorrenciaDefault_DeveLancarExcecao()
    {
        // Arrange & Act
        var action = () => Ocorrencia.Criar(
            12345,
            "testuser",
            "Test occurrence",
            "ABERTO",
            default,
            "14:30");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DtOcorrencia*required*");
    }
}

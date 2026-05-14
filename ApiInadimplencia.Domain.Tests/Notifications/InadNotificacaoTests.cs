using ApiInadimplencia.Domain.Notifications;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Notifications;

public class InadNotificacaoTests
{
    [Fact]
    public void Criar_DeveCriarNotificacaoComDadosValidos()
    {
        // Arrange
        var tipo = NotificationType.VendaAtribuida;
        var usuario = "TestUser";
        var numVenda = 12345;
        var proximaAcaoDia = new DateOnly(2024, 1, 20);
        var mensagem = "Venda atribuída a você";

        // Act
        var notificacao = InadNotificacao.Criar(
            tipo,
            usuario,
            numVenda,
            proximaAcaoDia,
            mensagem);

        // Assert
        notificacao.Should().NotBeNull();
        notificacao.Id.Should().NotBeEmpty();
        notificacao.Tipo.Should().Be(tipo);
        notificacao.Usuario.Should().Be(usuario.ToLowerInvariant());
        notificacao.NumVenda.Should().Be(numVenda);
        notificacao.ProximaAcaoDia.Should().Be(proximaAcaoDia);
        notificacao.Mensagem.Should().Be(mensagem);
        notificacao.Lida.Should().BeFalse();
        notificacao.CriadaEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        notificacao.ExcluidaEm.Should().BeNull();
    }

    [Fact]
    public void Criar_DeveNormalizarUsuarioParaLowercase()
    {
        // Arrange
        var usuario = "TestUser";

        // Act
        var notificacao = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            usuario,
            12345,
            null,
            "Test message");

        // Assert
        notificacao.Usuario.Should().Be(usuario.ToLowerInvariant());
    }

    [Fact]
    public void Criar_DeveCriarNotificacaoSemProximaAcaoDia()
    {
        // Arrange
        var tipo = NotificationType.VendaAtribuida;
        var usuario = "TestUser";
        var numVenda = 12345;
        var mensagem = "Venda atribuída a você";

        // Act
        var notificacao = InadNotificacao.Criar(
            tipo,
            usuario,
            numVenda,
            null,
            mensagem);

        // Assert
        notificacao.Should().NotBeNull();
        notificacao.ProximaAcaoDia.Should().BeNull();
    }

    [Fact]
    public void MarcarComoLida_DeveMarcarNotificacaoComoLida()
    {
        // Arrange
        var notificacao = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            "TestUser",
            12345,
            null,
            "Test message");

        // Act
        notificacao.MarcarComoLida();

        // Assert
        notificacao.Lida.Should().BeTrue();
    }

    [Fact]
    public void Excluir_ComNotificacaoLida_DeveSoftDelete()
    {
        // Arrange
        var notificacao = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            "TestUser",
            12345,
            null,
            "Test message");
        notificacao.MarcarComoLida();

        // Act
        notificacao.Excluir();

        // Assert
        notificacao.ExcluidaEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Excluir_ComNotificacaoNaoLida_DeveLancarExcecao()
    {
        // Arrange
        var notificacao = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            "TestUser",
            12345,
            null,
            "Test message");

        // Act
        var action = () => notificacao.Excluir();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot delete unread notification*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Criar_ComUsuarioVazio_DeveNormalizarParaLowercase(string usuario)
    {
        // Arrange & Act
        var notificacao = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            usuario,
            12345,
            null,
            "Test message");

        // Assert
        notificacao.Usuario.Should().Be(usuario.ToLowerInvariant());
    }

    [Fact]
    public void Criar_DeveGerarIdUnico()
    {
        // Arrange & Act
        var notificacao1 = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            "User1",
            12345,
            null,
            "Message 1");
        var notificacao2 = InadNotificacao.Criar(
            NotificationType.VendaAtribuida,
            "User2",
            67890,
            null,
            "Message 2");

        // Assert
        notificacao1.Id.Should().NotBe(notificacao2.Id);
    }
}

using ApiInadimplencia.Domain.Events;
using ApiInadimplencia.Domain.Responsaveis;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Responsaveis;

public class VendaResponsavelTests
{
    [Fact]
    public void Criar_ComDadosValidos_DeveRetornarResponsavelEEvento()
    {
        // Arrange
        var numVenda = 12345;
        var username = "user123";
        var adminUserCode = "admin001";

        // Act
        var responsavel = VendaResponsavel.Criar(numVenda, username, adminUserCode);

        // Assert
        responsavel.NumVendaFk.Should().Be(numVenda);
        responsavel.Username.Should().Be(username);
        responsavel.AtribuidoPor.Should().Be(adminUserCode);
        responsavel.AtribuidoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        
        responsavel.DomainEvents.Should().HaveCount(1);
        var domainEvent = responsavel.DomainEvents.First();
        domainEvent.Should().BeOfType<ResponsavelAtribuidoEvent>();
        
        var evento = (ResponsavelAtribuidoEvent)domainEvent;
        evento.NumVenda.Should().Be(numVenda);
        evento.PreviousUsername.Should().BeNull();
        evento.CurrentUsername.Should().Be(username);
        evento.AdminUserCode.Should().Be(adminUserCode);
    }

    [Fact]
    public void AtualizarResponsavel_ComNovoUsuario_DeveDispararEvento()
    {
        // Arrange
        var responsavel = VendaResponsavel.Criar(12345, "user123", "admin001");
        responsavel.ClearDomainEvents();
        var novoUsername = "user456";

        // Act
        responsavel.AtualizarResponsavel(novoUsername, "admin001");

        // Assert
        responsavel.Username.Should().Be(novoUsername);
        responsavel.DomainEvents.Should().HaveCount(1);
        
        var domainEvent = responsavel.DomainEvents.First();
        domainEvent.Should().BeOfType<ResponsavelAtribuidoEvent>();
        
        var evento = (ResponsavelAtribuidoEvent)domainEvent;
        evento.PreviousUsername.Should().Be("user123");
        evento.CurrentUsername.Should().Be(novoUsername);
    }

    [Fact]
    public void ClearDomainEvents_DeveRemoverTodosEventos()
    {
        // Arrange
        var responsavel = VendaResponsavel.Criar(12345, "user123", "admin001");

        // Act
        responsavel.ClearDomainEvents();

        // Assert
        responsavel.DomainEvents.Should().BeEmpty();
    }
}

using ApiInadimplencia.Domain.Users;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Usuarios;

public class UsuarioTests
{
    [Fact]
    public void Criar_ComDadosValidos_DeveRetornarUsuario()
    {
        // Arrange
        var userCode = "user123";
        var nome = "Test User";
        var perfil = UserProfile.Operador;
        var corHex = "#FF0000";

        // Act
        var usuario = Usuario.Criar(userCode, nome, perfil, corHex);

        // Assert
        usuario.UserCode.Should().Be(userCode);
        usuario.Nome.Should().Be(nome);
        usuario.Perfil.Should().Be(perfil);
        usuario.CorHex.Should().Be("#FF0000");
    }

    [Fact]
    public void Criar_ComCorHexSemHash_DeveNormalizar()
    {
        // Arrange
        var userCode = "user123";
        var nome = "Test User";
        var perfil = UserProfile.Operador;
        var corHex = "FF0000";

        // Act
        var usuario = Usuario.Criar(userCode, nome, perfil, corHex);

        // Assert
        usuario.CorHex.Should().Be("#FF0000");
    }

    [Fact]
    public void Criar_ComUserCodeVazio_DeveLancarArgumentException()
    {
        // Arrange
        var userCode = "";
        var nome = "Test User";
        var perfil = UserProfile.Operador;
        var corHex = "#FF0000";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Usuario.Criar(userCode, nome, perfil, corHex));
    }

    [Fact]
    public void Criar_ComNomeVazio_DeveLancarArgumentException()
    {
        // Arrange
        var userCode = "user123";
        var nome = "";
        var perfil = UserProfile.Operador;
        var corHex = "#FF0000";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Usuario.Criar(userCode, nome, perfil, corHex));
    }

    [Fact]
    public void Criar_ComPerfilInvalido_DeveLancarArgumentException()
    {
        // Arrange
        var userCode = "user123";
        var nome = "Test User";
        var perfil = (UserProfile)999; // Invalid profile
        var corHex = "#FF0000";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Usuario.Criar(userCode, nome, perfil, corHex));
    }

    [Fact]
    public void Criar_ComCorHexInvalido_DeveLancarArgumentException()
    {
        // Arrange
        var userCode = "user123";
        var nome = "Test User";
        var perfil = UserProfile.Operador;
        var corHex = "INVALID";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Usuario.Criar(userCode, nome, perfil, corHex));
    }

    [Fact]
    public void Atualizar_ComDadosValidos_DeveAtualizarUsuario()
    {
        // Arrange
        var usuario = Usuario.Criar("user123", "Test User", UserProfile.Operador, "#FF0000");

        // Act
        usuario.Atualizar("Updated Name", UserProfile.Admin, "#00FF00");

        // Assert
        usuario.Nome.Should().Be("Updated Name");
        usuario.Perfil.Should().Be(UserProfile.Admin);
        usuario.CorHex.Should().Be("#00FF00");
    }

    [Fact]
    public void Atualizar_ComNomeVazio_DeveLancarArgumentException()
    {
        // Arrange
        var usuario = Usuario.Criar("user123", "Test User", UserProfile.Operador, "#FF0000");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => usuario.Atualizar(""));
    }

    [Fact]
    public void Atualizar_ComPerfilInvalido_DeveLancarArgumentException()
    {
        // Arrange
        var usuario = Usuario.Criar("user123", "Test User", UserProfile.Operador, "#FF0000");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => usuario.Atualizar(perfil: (UserProfile)999));
    }

    [Fact]
    public void Atualizar_ComCorHexInvalido_DeveLancarArgumentException()
    {
        // Arrange
        var usuario = Usuario.Criar("user123", "Test User", UserProfile.Operador, "#FF0000");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => usuario.Atualizar(corHex: "INVALID"));
    }
}

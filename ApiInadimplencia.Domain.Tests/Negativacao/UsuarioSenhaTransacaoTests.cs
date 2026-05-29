using ApiInadimplencia.Domain.Negativacao;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Negativacao;

public sealed class UsuarioSenhaTransacaoTests
{
    [Fact]
    public void Criar_ComUsernameVazio_DeveLancarArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UsuarioSenhaTransacao.Criar("", "hash"));
        Assert.Throws<ArgumentException>(() => UsuarioSenhaTransacao.Criar("  ", "hash"));
        Assert.Throws<ArgumentException>(() => UsuarioSenhaTransacao.Criar(null!, "hash"));
    }

    [Fact]
    public void Criar_ComHashVazio_DeveLancarArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UsuarioSenhaTransacao.Criar("user", ""));
        Assert.Throws<ArgumentException>(() => UsuarioSenhaTransacao.Criar("user", "  "));
        Assert.Throws<ArgumentException>(() => UsuarioSenhaTransacao.Criar("user", null!));
    }

    [Fact]
    public void Criar_ComDadosValidos_DeveCriarEntidade()
    {
        // Arrange
        var username = "testuser";
        var hash = "hashedpassword123";

        // Act
        var result = UsuarioSenhaTransacao.Criar(username, hash);

        // Assert
        Assert.Equal(username, result.Username);
        Assert.Equal(hash, result.Hash);
        Assert.Equal(0, result.TentativasFalhas);
        Assert.Null(result.BloqueadoAte);
        Assert.NotEqual(default, result.CriadaEm);
        Assert.NotEqual(default, result.AtualizadaEm);
    }

    [Fact]
    public void AtualizarHash_ComHashValido_DeveAtualizarEResetarContadores()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "oldhash");
        var utcNow = DateTime.UtcNow;
        var lockoutDuration = TimeSpan.FromMinutes(15);
        
        // Bloqueia a conta com 3 tentativas
        senha.RegistrarTentativaInvalida(3, lockoutDuration, utcNow);
        senha.RegistrarTentativaInvalida(3, lockoutDuration, utcNow);
        senha.RegistrarTentativaInvalida(3, lockoutDuration, utcNow);
        
        Assert.Equal(3, senha.TentativasFalhas);
        Assert.NotNull(senha.BloqueadoAte);

        var novoHash = "newhash456";

        // Act
        senha.AtualizarHash(novoHash);

        // Assert
        Assert.Equal(novoHash, senha.Hash);
        Assert.Equal(0, senha.TentativasFalhas);
        Assert.Null(senha.BloqueadoAte);
    }

    [Fact]
    public void AtualizarHash_ComHashVazio_DeveLancarArgumentException()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => senha.AtualizarHash(""));
        Assert.Throws<ArgumentException>(() => senha.AtualizarHash(null!));
    }

    [Fact]
    public void RegistrarTentativaInvalida_AbaixoDoLimite_DeveIncrementarContador()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        var utcNow = DateTime.UtcNow;

        // Act
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), utcNow);

        // Assert
        Assert.Equal(1, senha.TentativasFalhas);
        Assert.Null(senha.BloqueadoAte);
    }

    [Fact]
    public void RegistrarTentativaInvalida_NoLimite_DeveBloquear()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        var utcNow = DateTime.UtcNow;
        var lockoutDuration = TimeSpan.FromMinutes(15);

        // Act - 3 tentativas (limite)
        senha.RegistrarTentativaInvalida(3, lockoutDuration, utcNow);
        senha.RegistrarTentativaInvalida(3, lockoutDuration, utcNow);
        senha.RegistrarTentativaInvalida(3, lockoutDuration, utcNow);

        // Assert
        Assert.Equal(3, senha.TentativasFalhas);
        Assert.NotNull(senha.BloqueadoAte);
        Assert.True(senha.EstaBloqueado(utcNow));
    }

    [Fact]
    public void RegistrarTentativaInvalida_AposExpiracaoDoBloqueio_DeveResetarContador()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        var pastTime = DateTime.UtcNow.AddMinutes(-20);
        var lockoutDuration = TimeSpan.FromMinutes(15);

        // Act - Bloqueia no passado
        senha.RegistrarTentativaInvalida(3, lockoutDuration, pastTime);
        senha.RegistrarTentativaInvalida(3, lockoutDuration, pastTime);
        senha.RegistrarTentativaInvalida(3, lockoutDuration, pastTime);

        // Act - Nova tentativa após expiração
        var now = DateTime.UtcNow;
        senha.RegistrarTentativaInvalida(3, lockoutDuration, now);

        // Assert
        Assert.Equal(1, senha.TentativasFalhas);
        Assert.False(senha.EstaBloqueado(now));
    }

    [Fact]
    public void RegistrarTentativaValida_DeveResetarContadoresEBloqueio()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        Assert.Equal(2, senha.TentativasFalhas);

        // Act
        senha.RegistrarTentativaValida();

        // Assert
        Assert.Equal(0, senha.TentativasFalhas);
        Assert.Null(senha.BloqueadoAte);
    }

    [Fact]
    public void EstaBloqueado_SemBloqueio_DeveRetornarFalso()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        var utcNow = DateTime.UtcNow;

        // Act
        var result = senha.EstaBloqueado(utcNow);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EstaBloqueado_ComBloqueioAtivo_DeveRetornarVerdadeiro()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        var utcNow = DateTime.UtcNow;
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), utcNow);
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), utcNow);
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), utcNow);

        // Act
        var result = senha.EstaBloqueado(utcNow);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EstaBloqueado_ComBloqueioExpirado_DeveRetornarFalso()
    {
        // Arrange
        var senha = UsuarioSenhaTransacao.Criar("user", "hash");
        var pastTime = DateTime.UtcNow.AddMinutes(-20);
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), pastTime);
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), pastTime);
        senha.RegistrarTentativaInvalida(3, TimeSpan.FromMinutes(15), pastTime);

        // Act
        var result = senha.EstaBloqueado(DateTime.UtcNow);

        // Assert
        Assert.False(result);
    }
}

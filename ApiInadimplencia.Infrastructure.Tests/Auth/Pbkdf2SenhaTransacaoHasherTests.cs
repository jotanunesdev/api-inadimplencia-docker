using ApiInadimplencia.Infrastructure.Auth;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Auth;

public sealed class Pbkdf2SenhaTransacaoHasherTests
{
    private readonly Pbkdf2SenhaTransacaoHasher _hasher;

    public Pbkdf2SenhaTransacaoHasherTests()
    {
        _hasher = new Pbkdf2SenhaTransacaoHasher();
    }

    [Fact]
    public void Hash_ComMesmaSenha_DeveGerarHashesDiferentes()
    {
        // Arrange
        var senha = "MinhaSenha123";

        // Act
        var hash1 = _hasher.Hash(senha);
        var hash2 = _hasher.Hash(senha);

        // Assert - PBKDF2 com salt aleatório deve gerar hashes diferentes
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_ComSenhaVazia_DeveLancarArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _hasher.Hash(""));
        Assert.Throws<ArgumentException>(() => _hasher.Hash("  "));
        Assert.Throws<ArgumentException>(() => _hasher.Hash(null!));
    }

    [Fact]
    public void Verify_ComHashValidoESenhaCorreta_DeveRetornarVerdadeiro()
    {
        // Arrange
        var senha = "MinhaSenha123";
        var hash = _hasher.Hash(senha);

        // Act
        var result = _hasher.Verify(hash, senha);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_ComHashValidoESenhaIncorreta_DeveRetornarFalso()
    {
        // Arrange
        var senha = "MinhaSenha123";
        var hash = _hasher.Hash(senha);
        var senhaIncorreta = "OutraSenha456";

        // Act
        var result = _hasher.Verify(hash, senhaIncorreta);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_ComHashVazio_DeveLancarArgumentException()
    {
        // Arrange
        var senha = "MinhaSenha123";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _hasher.Verify("", senha));
        Assert.Throws<ArgumentException>(() => _hasher.Verify(null!, senha));
    }

    [Fact]
    public void Verify_ComSenhaVazia_DeveRetornarFalso()
    {
        // Arrange
        var senha = "MinhaSenha123";
        var hash = _hasher.Hash(senha);

        // Act
        var result = _hasher.Verify(hash, "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_ComSenhaNula_DeveRetornarFalso()
    {
        // Arrange
        var senha = "MinhaSenha123";
        var hash = _hasher.Hash(senha);

        // Act
        var result = _hasher.Verify(hash, null!);

        // Assert
        Assert.False(result);
    }
}

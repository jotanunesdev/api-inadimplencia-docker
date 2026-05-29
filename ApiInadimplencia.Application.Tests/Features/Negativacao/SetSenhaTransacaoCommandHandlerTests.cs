using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Domain.Negativacao;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class SetSenhaTransacaoCommandHandlerTests
{
    private readonly Mock<ISenhaTransacaoRepository> _repositoryMock;
    private readonly Mock<ISenhaTransacaoHasher> _hasherMock;
    private readonly SetSenhaTransacaoCommandHandler _handler;

    public SetSenhaTransacaoCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISenhaTransacaoRepository>();
        _hasherMock = new Mock<ISenhaTransacaoHasher>();
        _handler = new SetSenhaTransacaoCommandHandler(_repositoryMock.Object, _hasherMock.Object);
    }

    [Fact]
    public async Task HandleAsync_SenhaMenorQue6Caracteres_DeveLancarArgumentException()
    {
        // Arrange
        var command = new SetSenhaTransacaoCommand("user", null, "12345");
        _repositoryMock.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioSenhaTransacao?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_SenhaNulaOuVazia_DeveLancarArgumentException()
    {
        // Arrange
        var command = new SetSenhaTransacaoCommand("user", null, "");
        _repositoryMock.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioSenhaTransacao?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_PrimeiraSenha_SemSenhaAtual_DeveCriarNovaSenha()
    {
        // Arrange
        var command = new SetSenhaTransacaoCommand("user", null, "NovaSenha123");
        var hash = "hashedpassword";
        
        _repositoryMock.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioSenhaTransacao?)null);
        _hasherMock.Setup(h => h.Hash("NovaSenha123")).Returns(hash);
        _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<UsuarioSenhaTransacao>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _hasherMock.Verify(h => h.Hash("NovaSenha123"), Times.Once);
        _repositoryMock.Verify(r => r.UpsertAsync(
            It.Is<UsuarioSenhaTransacao>(s => s.Username == "user" && s.Hash == hash),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Atualizacao_SemSenhaAtual_DeveLancarUnauthorizedAccessException()
    {
        // Arrange
        var command = new SetSenhaTransacaoCommand("user", null, "NovaSenha123");
        var existingSenha = UsuarioSenhaTransacao.Criar("user", "oldhash");
        
        _repositoryMock.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSenha);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Atualizacao_ComSenhaAtualIncorreta_DeveLancarUnauthorizedAccessException()
    {
        // Arrange
        var command = new SetSenhaTransacaoCommand("user", "SenhaErrada", "NovaSenha123");
        var existingSenha = UsuarioSenhaTransacao.Criar("user", "oldhash");
        
        _repositoryMock.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSenha);
        _hasherMock.Setup(h => h.Verify("oldhash", "SenhaErrada")).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Atualizacao_ComSenhaAtualCorreta_DeveAtualizarSenha()
    {
        // Arrange
        var command = new SetSenhaTransacaoCommand("user", "SenhaAtual123", "NovaSenha456");
        var existingSenha = UsuarioSenhaTransacao.Criar("user", "oldhash");
        var novoHash = "newhashedpassword";
        
        _repositoryMock.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSenha);
        _hasherMock.Setup(h => h.Verify("oldhash", "SenhaAtual123")).Returns(true);
        _hasherMock.Setup(h => h.Hash("NovaSenha456")).Returns(novoHash);
        _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<UsuarioSenhaTransacao>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _hasherMock.Verify(h => h.Verify("oldhash", "SenhaAtual123"), Times.Once);
        _hasherMock.Verify(h => h.Hash("NovaSenha456"), Times.Once);
        _repositoryMock.Verify(r => r.UpsertAsync(
            It.Is<UsuarioSenhaTransacao>(s => s.Username == "user" && s.Hash == novoHash),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

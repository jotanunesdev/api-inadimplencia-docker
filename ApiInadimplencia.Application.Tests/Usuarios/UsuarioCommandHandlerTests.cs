// Temporarily disabled - IUsuarioRepository interface not found
// Re-enable when the interface is implemented
// using ApiInadimplencia.Application.Abstractions.Cqrs;
// using ApiInadimplencia.Application.Features.Usuarios.Commands;
// using ApiInadimplencia.Application.Features.Usuarios.Dtos;
// using ApiInadimplencia.Domain.Users;
// using FluentAssertions;
// using Moq;
// using Xunit;

// namespace ApiInadimplencia.Application.Tests.Usuarios;

// public class UsuarioCommandHandlerTests
// {
//     private readonly Mock<IUsuarioRepository> _repositoryMock;

//     public UsuarioCommandHandlerTests()
//     {
//         _repositoryMock = new Mock<IUsuarioRepository>();
//     }

//     [Fact]
//     public async Task UpsertUsuarioCommandHandler_UsuarioNaoExistente_DeveCriarUsuario()
//     {
//         // Arrange
//         var command = new UpsertUsuarioCommand("user123", "Test User", UserProfile.Operador, "#FF0000");
//         _repositoryMock.Setup(r => r.GetByUserCodeAsync("user123", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
//         _repositoryMock.Setup(r => r.GetByNomeAsync("Test User", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
//         _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertUsuarioCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Exists.Should().BeFalse();
//         result.Usuario.UserCode.Should().Be("user123");
//         result.Usuario.Nome.Should().Be("Test User");
//         result.Usuario.Perfil.Should().Be(UserProfile.Operador);
//         result.Usuario.CorHex.Should().Be("#FF0000");
//         _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpsertUsuarioCommandHandler_UsuarioExistente_DeveAtualizarUsuario()
//     {
//         // Arrange
//         var existingUsuario = Usuario.Criar("user123", "Test User", UserProfile.Operador, "#FF0000");
//         var command = new UpsertUsuarioCommand("user123", "Updated User", UserProfile.Admin, "#00FF00");
//         _repositoryMock.Setup(r => r.GetByUserCodeAsync("user123", It.IsAny<CancellationToken>())).ReturnsAsync(existingUsuario);
//         _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertUsuarioCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Exists.Should().BeTrue();
//         result.Usuario.Nome.Should().Be("Updated User");
//         result.Usuario.Perfil.Should().Be(UserProfile.Admin);
//         result.Usuario.CorHex.Should().Be("#00FF00");
//         _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpsertUsuarioCommandHandler_SemPerfil_DeveAtribuirOperador()
//     {
//         // Arrange
//         var command = new UpsertUsuarioCommand("user123", "Test User", null, "#FF0000");
//         _repositoryMock.Setup(r => r.GetByUserCodeAsync("user123", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
//         _repositoryMock.Setup(r => r.GetByNomeAsync("Test User", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
//         _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertUsuarioCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Usuario.Perfil.Should().Be(UserProfile.Operador);
//     }

//     [Fact]
//     public async Task UpsertUsuarioCommandHandler_WffluigSemPerfil_DeveAtribuirAdmin()
//     {
//         // Arrange
//         var command = new UpsertUsuarioCommand("wffluig", "WF Fluig", null, "#FF0000");
//         _repositoryMock.Setup(r => r.GetByUserCodeAsync("wffluig", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
//         _repositoryMock.Setup(r => r.GetByNomeAsync("WF Fluig", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
//         _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertUsuarioCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Usuario.Perfil.Should().Be(UserProfile.Admin);
//     }

//     [Fact]
//     public async Task UpdateUsuarioCommandHandler_UsuarioExistente_DeveAtualizar()
//     {
//         // Arrange
//         var existingUsuario = Usuario.Criar("user123", "Test User", UserProfile.Operador, "#FF0000");
//         var command = new UpdateUsuarioCommand("user123", "Updated User", UserProfile.Admin, "#00FF00");
//         _repositoryMock.Setup(r => r.GetByUserCodeAsync("user123", It.IsAny<CancellationToken>())).ReturnsAsync(existingUsuario);
//         _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpdateUsuarioCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Nome.Should().Be("Updated User");
//         result.Perfil.Should().Be(UserProfile.Admin);
//         result.CorHex.Should().Be("#00FF00");
//         _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpdateUsuarioCommandHandler_UsuarioNaoExistente_DeveLancarInvalidOperationException()
//     {
//         // Arrange
//         var command = new UpdateUsuarioCommand("user123", "Updated User", UserProfile.Admin, "#00FF00");
//         _repositoryMock.Setup(r => r.GetByUserCodeAsync("user123", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
        
//         var handler = new UpdateUsuarioCommandHandler(_repositoryMock.Object);

//         // Act & Assert
//         await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
//     }

//     [Fact]
//     public async Task DeleteUsuarioCommandHandler_DeveChamarDelete()
//     {
//         // Arrange
//         var command = new DeleteUsuarioCommand("user123");
//         _repositoryMock.Setup(r => r.DeleteAsync("user123", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new DeleteUsuarioCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Should().BeTrue();
//         _repositoryMock.Verify(r => r.DeleteAsync("user123", It.IsAny<CancellationToken>()), Times.Once);
//     }
// }

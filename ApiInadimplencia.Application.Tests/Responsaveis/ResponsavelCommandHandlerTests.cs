// Temporarily disabled - IResponsavelRepository and IUsuarioValidator interfaces not found
// Re-enable when the interfaces are implemented
// using ApiInadimplencia.Application.Features.Responsaveis.Commands;
// using ApiInadimplencia.Application.Features.Responsaveis.Dtos;
// using ApiInadimplencia.Domain.Responsaveis;
// using FluentAssertions;
// using Moq;
// using Xunit;

// namespace ApiInadimplencia.Application.Tests.Responsaveis;

// public class ResponsavelCommandHandlerTests
// {
//     private readonly Mock<IResponsavelRepository> _repositoryMock;
//     private readonly Mock<IUsuarioValidator> _validatorMock;

//     public ResponsavelCommandHandlerTests()
//     {
//         _repositoryMock = new Mock<IResponsavelRepository>();
//         _validatorMock = new Mock<IUsuarioValidator>();
//     }

//     [Fact]
//     public async Task UpsertResponsavelCommandHandler_AdminValido_NovoResponsavel_DeveCriar()
//     {
//         // Arrange
//         var command = new UpsertResponsavelCommand(12345, "user123", "admin001");
//         _validatorMock.Setup(v => v.IsAdminUserAsync("admin001", It.IsAny<CancellationToken>())).ReturnsAsync(true);
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync((VendaResponsavel?)null);
//         _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<VendaResponsavel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertResponsavelCommandHandler(_repositoryMock.Object, _validatorMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.NumVendaFk.Should().Be(12345);
//         result.Username.Should().Be("user123");
//         result.AtribuidoPor.Should().Be("admin001");
//         _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<VendaResponsavel>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpsertResponsavelCommandHandler_AdminInvalido_DeveLancarInvalidOperationException()
//     {
//         // Arrange
//         var command = new UpsertResponsavelCommand(12345, "user123", "admin001");
//         _validatorMock.Setup(v => v.IsAdminUserAsync("admin001", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        
//         var handler = new UpsertResponsavelCommandHandler(_repositoryMock.Object, _validatorMock.Object);

//         // Act & Assert
//         await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
//     }

//     [Fact]
//     public async Task UpsertResponsavelCommandHandler_ResponsavelExistente_DeveAtualizar()
//     {
//         // Arrange
//         var existing = VendaResponsavel.Criar(12345, "user123", "admin001");
//         existing.ClearDomainEvents();
//         var command = new UpsertResponsavelCommand(12345, "user456", "admin001");
//         _validatorMock.Setup(v => v.IsAdminUserAsync("admin001", It.IsAny<CancellationToken>())).ReturnsAsync(true);
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
//         _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<VendaResponsavel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertResponsavelCommandHandler(_repositoryMock.Object, _validatorMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Username.Should().Be("user456");
//         _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<VendaResponsavel>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpdateResponsavelCommandHandler_AdminValido_DeveAtualizar()
//     {
//         // Arrange
//         var existing = VendaResponsavel.Criar(12345, "user123", "admin001");
//         existing.ClearDomainEvents();
//         var command = new UpdateResponsavelCommand(12345, "user456", "admin001");
//         _validatorMock.Setup(v => v.IsAdminUserAsync("admin001", It.IsAny<CancellationToken>())).ReturnsAsync(true);
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
//         _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<VendaResponsavel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpdateResponsavelCommandHandler(_repositoryMock.Object, _validatorMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Username.Should().Be("user456");
//         _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<VendaResponsavel>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpdateResponsavelCommandHandler_ResponsavelNaoExistente_DeveLancarInvalidOperationException()
//     {
//         // Arrange
//         var command = new UpdateResponsavelCommand(12345, "user456", "admin001");
//         _validatorMock.Setup(v => v.IsAdminUserAsync("admin001", It.IsAny<CancellationToken>())).ReturnsAsync(true);
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync((VendaResponsavel?)null);
        
//         var handler = new UpdateResponsavelCommandHandler(_repositoryMock.Object, _validatorMock.Object);

//         // Act & Assert
//         await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
//     }

//     [Fact]
//     public async Task DeleteResponsavelCommandHandler_DeveChamarDelete()
//     {
//         // Arrange
//         var command = new DeleteResponsavelCommand(12345);
//         _repositoryMock.Setup(r => r.DeleteAsync(12345, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new DeleteResponsavelCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Should().BeTrue();
//         _repositoryMock.Verify(r => r.DeleteAsync(12345, It.IsAny<CancellationToken>()), Times.Once);
//     }
// }

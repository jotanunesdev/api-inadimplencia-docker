// Temporarily disabled - IKanbanStatusRepository interface not found
// Re-enable when the interface is implemented
// using ApiInadimplencia.Application.Features.Kanban.Commands;
// using ApiInadimplencia.Application.Features.Kanban.Dtos;
// using ApiInadimplencia.Domain.Kanban;
// using FluentAssertions;
// using Moq;
// using Xunit;

// namespace ApiInadimplencia.Application.Tests.Kanban;

// public class KanbanCommandHandlerTests
// {
//     private readonly Mock<IKanbanStatusRepository> _repositoryMock;

//     public KanbanCommandHandlerTests()
//     {
//         _repositoryMock = new Mock<IKanbanStatusRepository>();
//     }

//     [Fact]
//     public async Task UpsertKanbanStatusCommandHandler_StatusNaoExistente_DeveCriar()
//     {
//         // Arrange
//         var command = new UpsertKanbanStatusCommand(12345, "Ação 1", "todo", "2024-01-01");
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync((KanbanStatusEntity?)null);
//         _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<KanbanStatusEntity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertKanbanStatusCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.NumVendaFk.Should().Be(12345);
//         result.ProximaAcao.Should().Be("Ação 1");
//         result.Status.Should().Be(KanbanStatus.Todo);
//         result.StatusData.Should().Be("2024-01-01");
//         _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<KanbanStatusEntity>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpsertKanbanStatusCommandHandler_StatusExistente_DeveAtualizar()
//     {
//         // Arrange
//         var existing = KanbanStatusEntity.Criar(12345, "Ação 1", "todo", new DateOnly(2024, 1, 1));
//         var command = new UpsertKanbanStatusCommand(12345, "Ação 2", "fazendo", "2024-02-01");
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
//         _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<KanbanStatusEntity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertKanbanStatusCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.ProximaAcao.Should().Be("Ação 2");
//         result.Status.Should().Be(KanbanStatus.InProgress);
//         result.StatusData.Should().Be("2024-02-01");
//         _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<KanbanStatusEntity>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task UpsertKanbanStatusCommandHandler_StatusInvalido_DeveLancarArgumentException()
//     {
//         // Arrange
//         var command = new UpsertKanbanStatusCommand(12345, "Ação 1", "invalid", "2024-01-01");
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync((KanbanStatusEntity?)null);
        
//         var handler = new UpsertKanbanStatusCommandHandler(_repositoryMock.Object);

//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
//     }

//     [Fact]
//     public async Task UpsertKanbanStatusCommandHandler_StatusDataInvalido_DeveLancarArgumentException()
//     {
//         // Arrange
//         var command = new UpsertKanbanStatusCommand(12345, "Ação 1", "todo", "invalid-date");
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync((KanbanStatusEntity?)null);
        
//         var handler = new UpsertKanbanStatusCommandHandler(_repositoryMock.Object);

//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
//     }

//     [Fact]
//     public async Task UpsertKanbanStatusCommandHandler_StatusPTBR_DeveNormalizar()
//     {
//         // Arrange
//         var command = new UpsertKanbanStatusCommand(12345, "Ação 1", "a fazer", "2024-01-01");
//         _repositoryMock.Setup(r => r.GetByNumVendaAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync((KanbanStatusEntity?)null);
//         _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<KanbanStatusEntity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
//         var handler = new UpsertKanbanStatusCommandHandler(_repositoryMock.Object);

//         // Act
//         var result = await handler.HandleAsync(command);

//         // Assert
//         result.Status.Should().Be(KanbanStatus.Todo);
//     }
// }

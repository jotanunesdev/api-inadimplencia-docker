using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.Ocorrencias.Commands;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Domain.Ocorrencias;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Ocorrencias.Commands;

public class DeleteOcorrenciaCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_QuandoOcorrenciaExiste_DeveExcluirERetornarTrue()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();
        var ocorrenciaExistente = Ocorrencia.Criar(
            12345,
            "testuser",
            "Test description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocorrenciaExistente);

        mockRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new DeleteOcorrenciaCommandHandler(mockRepository.Object);
        var command = new DeleteOcorrenciaCommand(ocorrenciaExistente.Id);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Should().BeTrue();
        mockRepository.Verify(r => r.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.DeleteAsync(ocorrenciaExistente, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QuandoOcorrenciaNaoExiste_DeveRetornarFalse()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();

        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ocorrencia?)null);

        var handler = new DeleteOcorrenciaCommandHandler(mockRepository.Object);
        var command = new DeleteOcorrenciaCommand(Guid.NewGuid());

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Should().BeFalse();
        mockRepository.Verify(r => r.DeleteAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

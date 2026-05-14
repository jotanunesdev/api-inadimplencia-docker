using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.Ocorrencias.Commands;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Domain.Ocorrencias;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Ocorrencias.Commands;

public class UpdateOcorrenciaCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_QuandoOcorrenciaExiste_DeveAtualizarERetornarTrue()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();
        var ocorrenciaExistente = Ocorrencia.Criar(
            12345,
            "testuser",
            "Original description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocorrenciaExistente);

        mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateOcorrenciaCommandHandler(mockRepository.Object);
        var command = new UpdateOcorrenciaCommand(
            ocorrenciaExistente.Id,
            "Updated description",
            "FECHADO",
            new DateTime(2024, 1, 20),
            "16:45",
            "2024-01-25",
            null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Should().BeTrue();
        mockRepository.Verify(r => r.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QuandoOcorrenciaNaoExiste_DeveRetornarFalse()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();

        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ocorrencia?)null);

        var handler = new UpdateOcorrenciaCommandHandler(mockRepository.Object);
        var command = new UpdateOcorrenciaCommand(
            Guid.NewGuid(),
            "Updated description",
            "FECHADO",
            new DateTime(2024, 1, 20),
            "16:45");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Should().BeFalse();
        mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_QuandoCamposSaoNulos_DeveAtualizarApenasCamposFornecidos()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();
        var ocorrenciaExistente = Ocorrencia.Criar(
            12345,
            "testuser",
            "Original description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30",
            "2024-01-20",
            "2024011500001");

        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocorrenciaExistente);

        mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateOcorrenciaCommandHandler(mockRepository.Object);
        var command = new UpdateOcorrenciaCommand(
            ocorrenciaExistente.Id,
            "Updated description",
            null,
            null,
            null);

        // Act
        await handler.HandleAsync(command);

        // Assert
        mockRepository.Verify(r => r.UpdateAsync(
            It.Is<Ocorrencia>(o => 
                o.Descricao == "Updated description" &&
                o.StatusOcorrencia == "ABERTO" &&
                o.DtOcorrencia == new DateTime(2024, 1, 15)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.Ocorrencias.Commands;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Ocorrencias.Commands;

public class CreateOcorrenciaCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_QuandoVendaNaoExiste_DeveRetornarConflict()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();
        var mockValidator = new Mock<IVendaValidator>();
        
        mockValidator
            .Setup(v => v.VendaExisteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var handler = new CreateOcorrenciaCommandHandler(mockRepository.Object, mockValidator.Object);
        var command = new CreateOcorrenciaCommand(
            12345,
            "testuser",
            "Teste",
            "ABERTO",
            DateTime.Now,
            DateTime.Now.ToString("HH:mm"),
            "Test action",
            null);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_QuandoVendaExiste_DeveCriarOcorrencia()
    {
        // Arrange
        var mockRepository = new Mock<IOcorrenciaRepository>();
        var mockValidator = new Mock<IVendaValidator>();
        
        mockValidator
            .Setup(v => v.VendaExisteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Domain.Ocorrencias.Ocorrencia>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        var handler = new CreateOcorrenciaCommandHandler(mockRepository.Object, mockValidator.Object);
        var command = new CreateOcorrenciaCommand(
            12345,
            "testuser",
            "Teste",
            "ABERTO",
            DateTime.Now,
            DateTime.Now.ToString("HH:mm"),
            "Test action",
            null);
        
        // Act
        var result = await handler.HandleAsync(command);
        
        // Assert
        result.Should().NotBeEmpty();
        mockRepository.Verify(r => r.AddAsync(It.IsAny<Domain.Ocorrencias.Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

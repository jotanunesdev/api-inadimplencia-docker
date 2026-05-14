using System.Text.Json;
using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Commands;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Atendimentos.Commands;

public class CreateAtendimentoCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_DeveGerarProtocoloECriarAtendimento()
    {
        // Arrange
        var mockProtocoloGenerator = new Mock<IProtocoloGenerator>();
        var mockRepository = new Mock<IAtendimentoRepository>();
        
        var protocoloEsperado = "2025011500001";
        mockProtocoloGenerator
            .Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(protocoloEsperado);
        
        mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Domain.Atendimentos.Atendimento>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        var handler = new CreateAtendimentoCommandHandler(mockProtocoloGenerator.Object, mockRepository.Object);
        var vendaData = new Dictionary<string, object?>
        {
            { "NUM_VENDA", 12345 },
            { "NOME_CLIENTE", "Test Client" }
        };
        var command = new CreateAtendimentoCommand(
            "12345678901",
            12345,
            vendaData);
        
        // Act
        var result = await handler.HandleAsync(command);
        
        // Assert
        result.Should().Be(protocoloEsperado);
        mockProtocoloGenerator.Verify(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.AddAsync(It.IsAny<Domain.Atendimentos.Atendimento>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DeveSerializarDadosVendaComoJson()
    {
        // Arrange
        var mockProtocoloGenerator = new Mock<IProtocoloGenerator>();
        var mockRepository = new Mock<IAtendimentoRepository>();
        
        mockProtocoloGenerator
            .Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2025011500001");
        
        Domain.Atendimentos.Atendimento? capturedAtendimento = null;
        mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Domain.Atendimentos.Atendimento>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Atendimentos.Atendimento, CancellationToken>((a, ct) => capturedAtendimento = a)
            .Returns(Task.CompletedTask);
        
        var handler = new CreateAtendimentoCommandHandler(mockProtocoloGenerator.Object, mockRepository.Object);
        var vendaData = new Dictionary<string, object?>
        {
            { "NUM_VENDA", 12345 },
            { "NOME_CLIENTE", "Test Client" },
            { "VALOR", 100000.50 }
        };
        var command = new CreateAtendimentoCommand(
            "12345678901",
            12345,
            vendaData);
        
        // Act
        await handler.HandleAsync(command);
        
        // Assert
        capturedAtendimento.Should().NotBeNull();
        var jsonData = JsonSerializer.Deserialize<Dictionary<string, object?>>(capturedAtendimento!.DadosVendaJson);
        jsonData.Should().NotBeNull();
        jsonData.Should().ContainKey("NUM_VENDA");
    }
}

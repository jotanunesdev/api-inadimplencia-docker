using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.Ocorrencias.Commands;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class OcorrenciaRepositoryIntegrationTests
{
    [Fact]
    public async Task AddAsync_DevePersistirOcorrencia()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ApiInadimplencia.Application.Abstractions.Persistence.ILegacySqlExecutor>();
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null, 0));

        var repository = new OcorrenciaRepository(mockSqlExecutor.Object);
        var ocorrencia = Ocorrencia.Criar(
            12345,
            "testuser",
            "Test occurrence",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        // Act
        await repository.AddAsync(ocorrencia);

        // Assert
        mockSqlExecutor.Verify(e => e.ExecuteAsync(
            "Ocorrencia.Insert",
            It.Is<Dictionary<string, object?>>(d =>
                d["id"] != null && d["id"].ToString() == ocorrencia.Id.ToString() &&
                Convert.ToInt32(d["numVenda"]) == ocorrencia.NumVendaFk &&
                d["nomeUsuario"] != null && d["nomeUsuario"].ToString() == ocorrencia.NomeUsuarioFk),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarOcorrencia()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ApiInadimplencia.Application.Abstractions.Persistence.ILegacySqlExecutor>();
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null, 0));

        var repository = new OcorrenciaRepository(mockSqlExecutor.Object);
        var ocorrencia = Ocorrencia.Criar(
            12345,
            "testuser",
            "Original description",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        ocorrencia.Atualizar(descricao: "Updated description");

        // Act
        await repository.UpdateAsync(ocorrencia);

        // Assert
        mockSqlExecutor.Verify(e => e.ExecuteAsync(
            "Ocorrencia.Update",
            It.Is<Dictionary<string, object?>>(d =>
                d["id"] != null && d["id"].ToString() == ocorrencia.Id.ToString() &&
                d["descricao"] != null && d["descricao"].ToString() == "Updated description"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeveExcluirOcorrencia()
    {
        // Arrange
        var mockSqlExecutor = new Mock<ApiInadimplencia.Application.Abstractions.Persistence.ILegacySqlExecutor>();
        mockSqlExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySqlResult(true, null, 0));

        var repository = new OcorrenciaRepository(mockSqlExecutor.Object);
        var ocorrencia = Ocorrencia.Criar(
            12345,
            "testuser",
            "Test occurrence",
            "ABERTO",
            new DateTime(2024, 1, 15),
            "14:30");

        // Act
        await repository.DeleteAsync(ocorrencia);

        // Assert
        mockSqlExecutor.Verify(e => e.ExecuteAsync(
            "Ocorrencia.Delete",
            It.Is<Dictionary<string, object?>>(d => d["id"] != null && d["id"].ToString() == ocorrencia.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ApiInadimplencia.Domain.Kanban;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.Kanban;

public class KanbanStatusEntityTests
{
    [Fact]
    public void Criar_ComStatusTodo_DeveNormalizarParaTodo()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "todo", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.Todo);
    }

    [Fact]
    public void Criar_ComStatusAFazer_DeveNormalizarParaTodo()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "a fazer", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.Todo);
    }

    [Fact]
    public void Criar_ComStatusInprogress_DeveNormalizarParaInProgress()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "inprogress", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.InProgress);
    }

    [Fact]
    public void Criar_ComStatusInProgress_DeveNormalizarParaInProgress()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "in_progress", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.InProgress);
    }

    [Fact]
    public void Criar_ComStatusFazendo_DeveNormalizarParaInProgress()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "fazendo", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.InProgress);
    }

    [Fact]
    public void Criar_ComStatusDone_DeveNormalizarParaDone()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "done", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.Done);
    }

    [Fact]
    public void Criar_ComStatusPronto_DeveNormalizarParaDone()
    {
        // Act
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "pronto", new DateOnly(2024, 1, 1));

        // Assert
        kanban.Status.Should().Be(KanbanStatus.Done);
    }

    [Fact]
    public void Criar_ComStatusInvalido_DeveLancarArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            KanbanStatusEntity.Criar(12345, "Ação 1", "invalid", new DateOnly(2024, 1, 1)));
    }

    [Fact]
    public void Atualizar_ComStatusValido_DeveNormalizarEAtualizar()
    {
        // Arrange
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "todo", new DateOnly(2024, 1, 1));

        // Act
        kanban.Atualizar(status: "fazendo");

        // Assert
        kanban.Status.Should().Be(KanbanStatus.InProgress);
    }

    [Fact]
    public void Atualizar_ComProximaAcao_DeveAtualizar()
    {
        // Arrange
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "todo", new DateOnly(2024, 1, 1));

        // Act
        kanban.Atualizar(proximaAcao: "Ação 2");

        // Assert
        kanban.ProximaAcao.Should().Be("Ação 2");
    }

    [Fact]
    public void Atualizar_ComStatusData_DeveAtualizar()
    {
        // Arrange
        var kanban = KanbanStatusEntity.Criar(12345, "Ação 1", "todo", new DateOnly(2024, 1, 1));

        // Act
        kanban.Atualizar(statusData: new DateOnly(2024, 2, 1));

        // Assert
        kanban.StatusData.Should().Be(new DateOnly(2024, 2, 1));
    }
}

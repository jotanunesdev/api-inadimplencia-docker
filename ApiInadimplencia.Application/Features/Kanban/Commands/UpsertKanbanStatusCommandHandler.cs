using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Kanban.Dtos;
using ApiInadimplencia.Domain.Kanban;

namespace ApiInadimplencia.Application.Features.Kanban.Commands;

/// <summary>
/// Handles upsert of a kanban status for a sale.
/// </summary>
public class UpsertKanbanStatusCommandHandler : ICommandHandler<UpsertKanbanStatusCommand, KanbanStatusDto>
{
    private readonly IKanbanStatusRepository _kanbanStatusRepository;

    public UpsertKanbanStatusCommandHandler(IKanbanStatusRepository kanbanStatusRepository)
    {
        _kanbanStatusRepository = kanbanStatusRepository;
    }

    /// <inheritdoc />
    public async Task<KanbanStatusDto> HandleAsync(UpsertKanbanStatusCommand command, CancellationToken cancellationToken = default)
    {
        // Parse status date
        if (!DateOnly.TryParse(command.StatusData, out var statusData))
        {
            throw new ArgumentException($"Invalid status date format '{command.StatusData}'. Expected YYYY-MM-DD.", nameof(command.StatusData));
        }

        // Try to find existing status
        var existing = await _kanbanStatusRepository.GetByNumVendaAsync(command.NumVendaFk, cancellationToken);
        KanbanStatusEntity kanbanStatus;

        if (existing != null)
        {
            // Update existing
            existing.Atualizar(command.ProximaAcao, command.Status, statusData);
            kanbanStatus = existing;
        }
        else
        {
            // Create new
            kanbanStatus = KanbanStatusEntity.Criar(
                command.NumVendaFk,
                command.ProximaAcao,
                command.Status,
                statusData);
        }

        await _kanbanStatusRepository.UpsertAsync(kanbanStatus, cancellationToken);

        return new KanbanStatusDto(
            kanbanStatus.NumVendaFk,
            kanbanStatus.ProximaAcao,
            kanbanStatus.Status,
            kanbanStatus.StatusData.ToString("yyyy-MM-dd"));
    }
}

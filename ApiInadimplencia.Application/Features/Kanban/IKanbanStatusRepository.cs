using ApiInadimplencia.Domain.Kanban;

namespace ApiInadimplencia.Application.Features.Kanban;

/// <summary>
/// Repository interface for kanban statuses.
/// </summary>
public interface IKanbanStatusRepository
{
    Task<KanbanStatusEntity?> GetByNumVendaAsync(int numVenda, CancellationToken cancellationToken);
    Task UpsertAsync(KanbanStatusEntity status, CancellationToken cancellationToken);
}

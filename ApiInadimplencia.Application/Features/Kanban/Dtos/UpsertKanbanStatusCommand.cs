using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Kanban.Dtos;

/// <summary>
/// Command to upsert a kanban status for a sale.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="ProximaAcao">Next action description.</param>
/// <param name="Status">Status (todo, inprogress, done, or PT-BR aliases).</param>
/// <param name="StatusData">Status date in YYYY-MM-DD format.</param>
public record UpsertKanbanStatusCommand(
    int NumVendaFk,
    string ProximaAcao,
    string Status,
    string StatusData) : ICommand<KanbanStatusDto>;

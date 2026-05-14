using ApiInadimplencia.Domain.Kanban;

namespace ApiInadimplencia.Application.Features.Kanban.Dtos;

/// <summary>
/// DTO representing a kanban status.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="ProximaAcao">Next action description.</param>
/// <param name="Status">Normalized status.</param>
/// <param name="StatusData">Status date in YYYY-MM-DD format.</param>
public record KanbanStatusDto(
    int NumVendaFk,
    string ProximaAcao,
    KanbanStatus Status,
    string StatusData);

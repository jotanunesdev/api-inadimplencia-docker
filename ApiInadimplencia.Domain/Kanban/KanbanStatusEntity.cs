using ApiInadimplencia.Domain.Common;

namespace ApiInadimplencia.Domain.Kanban;

/// <summary>
/// Represents a kanban status persisted in the database.
/// </summary>
public class KanbanStatusEntity
{
    /// <summary>
    /// Gets the sale number.
    /// </summary>
    public int NumVendaFk { get; private set; }

    /// <summary>
    /// Gets the next action description.
    /// </summary>
    public string ProximaAcao { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized status.
    /// </summary>
    public KanbanStatus Status { get; private set; }

    /// <summary>
    /// Gets the status date in YYYY-MM-DD format.
    /// </summary>
    public DateOnly StatusData { get; private set; }

    /// <summary>
    /// Creates a new kanban status with normalized status.
    /// </summary>
    public static KanbanStatusEntity Criar(
        int numVendaFk,
        string proximaAcao,
        string status,
        DateOnly statusData)
    {
        var normalizedStatus = NormalizeStatus(status);
        return new KanbanStatusEntity
        {
            NumVendaFk = numVendaFk,
            ProximaAcao = proximaAcao,
            Status = normalizedStatus,
            StatusData = statusData
        };
    }

    /// <summary>
    /// Updates the status with normalized status value.
    /// </summary>
    public void Atualizar(
        string? proximaAcao = null,
        string? status = null,
        DateOnly? statusData = null)
    {
        if (proximaAcao != null) ProximaAcao = proximaAcao;
        if (status != null) Status = NormalizeStatus(status);
        if (statusData.HasValue) StatusData = statusData.Value;
    }

    /// <summary>
    /// Normalizes status string to KanbanStatus enum.
    /// </summary>
    private static KanbanStatus NormalizeStatus(string status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            "todo" or "a fazer" or "afazer" => KanbanStatus.Todo,
            "inprogress" or "in_progress" or "fazendo" or "em progresso" => KanbanStatus.InProgress,
            "done" or "pronto" or "concluído" or "concluido" => KanbanStatus.Done,
            _ => throw new ArgumentException($"Invalid status '{status}'. Valid values: todo, inprogress, done (and PT-BR aliases).", nameof(status))
        };
    }
}

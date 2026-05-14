namespace ApiInadimplencia.Domain.Kanban;

/// <summary>
/// Represents the normalized Kanban status used by the inadimplencia module.
/// </summary>
public enum KanbanStatus
{
    /// <summary>
    /// Action is pending.
    /// </summary>
    Todo,

    /// <summary>
    /// Action is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Action is done.
    /// </summary>
    Done,
}


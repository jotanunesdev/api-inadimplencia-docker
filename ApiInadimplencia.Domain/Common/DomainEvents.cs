namespace ApiInadimplencia.Domain.Common;

/// <summary>
/// Represents a domain event emitted by an aggregate after a relevant business change.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the instant when the event was created.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base record for immutable domain events.
/// </summary>
/// <param name="OccurredAt">The instant when the event was created.</param>
public abstract record DomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;


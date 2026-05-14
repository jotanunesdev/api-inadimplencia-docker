using ApiInadimplencia.Domain.Common;

namespace ApiInadimplencia.Domain.Events;

/// <summary>
/// Event emitted when a sale is assigned to a responsible user.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="PreviousUsername">Previous responsible user, when one existed.</param>
/// <param name="CurrentUsername">New responsible user.</param>
/// <param name="AdminUserCode">Admin user code that performed the assignment.</param>
/// <param name="OccurredAt">Event creation instant.</param>
public sealed record ResponsavelAtribuidoEvent(
    int NumVenda,
    string? PreviousUsername,
    string CurrentUsername,
    string AdminUserCode,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

/// <summary>
/// Event emitted when an occurrence is registered for a sale.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="OccurrenceId">Occurrence identifier.</param>
/// <param name="ProximaAcao">Next action date, when present.</param>
/// <param name="OccurredAt">Event creation instant.</param>
public sealed record OcorrenciaRegistradaEvent(
    int NumVenda,
    Guid OccurrenceId,
    DateTimeOffset? ProximaAcao,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

/// <summary>
/// Event emitted when a Serasa PEFIN webhook is received.
/// </summary>
/// <param name="EventType">Webhook event type.</param>
/// <param name="TransactionId">Serasa transaction identifier.</param>
/// <param name="OccurredAt">Event creation instant.</param>
public sealed record SerasaPefinWebhookRecebidoEvent(
    string EventType,
    string TransactionId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);


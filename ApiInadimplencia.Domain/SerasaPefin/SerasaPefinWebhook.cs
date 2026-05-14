namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Represents a Serasa PEFIN webhook received from Serasa.
/// </summary>
public class SerasaPefinWebhook
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the UUID from Serasa for idempotency.
    /// </summary>
    public string Uuid { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the event type (inclusao, avalista, baixa).
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the result (sucesso, erro).
    /// </summary>
    public string Resultado { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    public string TransactionId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the webhook payload (JSON).
    /// </summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime RecebidoEm { get; private set; }

    /// <summary>
    /// Creates a new Serasa PEFIN webhook record.
    /// </summary>
    public static SerasaPefinWebhook Criar(
        string uuid,
        string eventType,
        string resultado,
        string transactionId,
        string payloadJson)
    {
        return new SerasaPefinWebhook
        {
            Id = Guid.NewGuid(),
            Uuid = uuid,
            EventType = eventType,
            Resultado = resultado,
            TransactionId = transactionId,
            PayloadJson = payloadJson,
            RecebidoEm = DateTime.UtcNow
        };
    }
}

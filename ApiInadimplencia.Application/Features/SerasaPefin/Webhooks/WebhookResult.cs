namespace ApiInadimplencia.Application.Features.SerasaPefin.Webhooks;

/// <summary>
/// Result of processing a Serasa webhook.
/// </summary>
public sealed class WebhookResult
{
    /// <summary>
    /// Gets whether the webhook was processed successfully.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets whether the webhook was already processed (idempotency).
    /// </summary>
    public bool WasAlreadyProcessed { get; private set; }

    /// <summary>
    /// Gets whether no matching solicitation was found for the transaction ID.
    /// </summary>
    public bool NoMatchingTransaction { get; private set; }

    /// <summary>
    /// Gets the UUID of the webhook.
    /// </summary>
    public string Uuid { get; private set; } = string.Empty;

    private WebhookResult()
    {
    }

    /// <summary>
    /// Creates a result indicating the webhook was processed successfully.
    /// </summary>
    public static WebhookResult Processed(string uuid) => new()
    {
        IsSuccess = true,
        WasAlreadyProcessed = false,
        NoMatchingTransaction = false,
        Uuid = uuid
    };

    /// <summary>
    /// Creates a result indicating the webhook was already processed (idempotency).
    /// </summary>
    public static WebhookResult AlreadyProcessed(string uuid) => new()
    {
        IsSuccess = true,
        WasAlreadyProcessed = true,
        NoMatchingTransaction = false,
        Uuid = uuid
    };

    /// <summary>
    /// Creates a result indicating no matching solicitation was found.
    /// </summary>
    public static WebhookResult OrphanWebhook => new()
    {
        IsSuccess = true,
        WasAlreadyProcessed = false,
        NoMatchingTransaction = true
    };
}

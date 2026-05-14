using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Webhooks;

/// <summary>
/// Handler for Serasa PEFIN webhooks with idempotency by UUID.
/// Processes all 6 webhook types (inclusao/avalista/baixa × sucesso/erro).
/// </summary>
public sealed class SerasaWebhookHandler
{
    private readonly ISerasaPefinRepository _repository;
    private readonly ILogger<SerasaWebhookHandler> _logger;

    public SerasaWebhookHandler(
        ISerasaPefinRepository repository,
        ILogger<SerasaWebhookHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a Serasa webhook with idempotency check.
    /// </summary>
    /// <param name="eventType">The event type (inclusao, avalista, baixa).</param>
    /// <param name="resultado">The result (sucesso, erro).</param>
    /// <param name="rawJson">The raw JSON payload from Serasa.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WebhookResult indicating the outcome.</returns>
    public async Task<WebhookResult> HandleAsync(
        WebhookEventType eventType,
        WebhookResultado resultado,
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing Serasa webhook. EventType: {EventType}, Resultado: {Resultado}",
            eventType, resultado);

        SerasaWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SerasaWebhookPayload>(rawJson);
            if (payload?.Uuid is null)
            {
                _logger.LogWarning("Webhook payload missing UUID. Persisting as error.");
                await PersistErrorWebhookAsync(eventType, resultado, rawJson, "Missing UUID", cancellationToken);
                return WebhookResult.Processed(string.Empty);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse webhook JSON. Persisting as error.");
            await PersistErrorWebhookAsync(eventType, resultado, rawJson, $"JSON parse error: {ex.Message}", cancellationToken);
            return WebhookResult.Processed(string.Empty);
        }

        // Check idempotency by UUID
        if (await _repository.WebhookExistsByUuidAsync(payload.Uuid, cancellationToken))
        {
            _logger.LogInformation("Webhook with UUID {Uuid} already processed, skipping", payload.Uuid);
            return WebhookResult.AlreadyProcessed(payload.Uuid);
        }

        // Resolve transaction ID from payload
        var transactionId = payload.Uuid;

        // Find matching solicitation
        var solicitacao = await _repository.GetByTransactionIdAsync(transactionId, cancellationToken);
        if (solicitacao is null)
        {
            _logger.LogWarning("No matching solicitation found for TransactionId {TransactionId}. Persisting orphan webhook.", transactionId);
            await PersistOrphanWebhookAsync(eventType, resultado, payload.Uuid, transactionId, rawJson, cancellationToken);
            return WebhookResult.OrphanWebhook;
        }

        // Apply webhook to solicitation
        ApplyToSolicitacao(solicitacao, eventType, resultado, payload, rawJson);

        // Persist webhook and update solicitation atomically
        var webhookRecord = BuildWebhookRecord(eventType, resultado, payload.Uuid, transactionId, rawJson, solicitacao.Id, true, null);
        await _repository.ApplyWebhookTransactionalAsync(solicitacao, webhookRecord, cancellationToken);

        _logger.LogInformation(
            "Webhook processed successfully. UUID: {Uuid}, TransactionId: {TransactionId}, NewStatus: {Status}",
            payload.Uuid, transactionId, solicitacao.Status);

        return WebhookResult.Processed(payload.Uuid);
    }

    /// <summary>
    /// Applies the webhook to the solicitation based on event type and result.
    /// </summary>
    private void ApplyToSolicitacao(
        SerasaPefinSolicitacaoCompleta solicitacao,
        WebhookEventType eventType,
        WebhookResultado resultado,
        SerasaWebhookPayload payload,
        string rawJson)
    {
        if (resultado == WebhookResultado.Erro)
        {
            var errorMessage = payload.Error?.Message ?? "Unknown error";
            var statusCode = payload.Error?.StatusCode;
            solicitacao.AplicarWebhookErro(rawJson, errorMessage, statusCode);
            return;
        }

        // Resultado == Sucesso
        switch (eventType)
        {
            case WebhookEventType.Inclusao:
            case WebhookEventType.Avalista:
                solicitacao.AplicarWebhookSucesso(rawJson, payload.CadusKey, payload.CadusSerie);
                break;

            case WebhookEventType.Baixa:
                // For baixa success, we need to mark as BaixadoSucesso
                // The entity method AplicarWebhookSucesso handles this based on current status
                solicitacao.AplicarWebhookSucesso(rawJson, payload.CadusKey, payload.CadusSerie);
                break;
        }
    }

    /// <summary>
    /// Builds a webhook record for persistence.
    /// </summary>
    private SerasaPefinWebhookRecord BuildWebhookRecord(
        WebhookEventType eventType,
        WebhookResultado resultado,
        string uuid,
        string transactionId,
        string rawJson,
        Guid? matchedSolicitacaoId,
        bool processado,
        string? mensagemErro)
    {
        var eventTypeStr = $"{eventType.ToString().ToUpper()}_{resultado.ToString().ToUpper()}";
        return new SerasaPefinWebhookRecord(
            Id: Guid.NewGuid(),
            EventType: eventTypeStr,
            TransactionId: transactionId,
            Payload: rawJson,
            MatchedSolicitacaoId: matchedSolicitacaoId,
            Processado: processado,
            MensagemErro: mensagemErro,
            DtRecebimento: DateTime.UtcNow);
    }

    /// <summary>
    /// Persists an orphan webhook (no matching solicitation).
    /// </summary>
    private async Task PersistOrphanWebhookAsync(
        WebhookEventType eventType,
        WebhookResultado resultado,
        string uuid,
        string transactionId,
        string rawJson,
        CancellationToken cancellationToken)
    {
        var webhookRecord = BuildWebhookRecord(
            eventType,
            resultado,
            uuid,
            transactionId,
            rawJson,
            null,
            false,
            "NoMatchingTransaction");

        await _repository.AddWebhookAsync(webhookRecord, cancellationToken);
    }

    /// <summary>
    /// Persists a webhook with processing error.
    /// </summary>
    private async Task PersistErrorWebhookAsync(
        WebhookEventType eventType,
        WebhookResultado resultado,
        string rawJson,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var webhookRecord = BuildWebhookRecord(
            eventType,
            resultado,
            Guid.NewGuid().ToString(),
            null,
            rawJson,
            null,
            false,
            errorMessage);

        await _repository.AddWebhookAsync(webhookRecord, cancellationToken);
    }
}

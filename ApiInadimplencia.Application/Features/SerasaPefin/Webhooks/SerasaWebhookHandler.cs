using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Domain.Notifications;
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
    private readonly ISerasaPefinBaixaRepository _baixaRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<SerasaWebhookHandler> _logger;

    public SerasaWebhookHandler(
        ISerasaPefinRepository repository,
        ISerasaPefinBaixaRepository baixaRepository,
        INotificationDispatcher notificationDispatcher,
        ILogger<SerasaWebhookHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _baixaRepository = baixaRepository ?? throw new ArgumentNullException(nameof(baixaRepository));
        _notificationDispatcher = notificationDispatcher ?? throw new ArgumentNullException(nameof(notificationDispatcher));
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

        // Check idempotency by UUID (shared SERASA_PEFIN_WEBHOOKS table)
        if (await _repository.WebhookExistsByUuidAsync(payload.Uuid, cancellationToken))
        {
            _logger.LogInformation("Webhook with UUID {Uuid} already processed, skipping", payload.Uuid);
            return WebhookResult.AlreadyProcessed(payload.Uuid);
        }

        // Resolve transaction ID from payload
        var transactionId = payload.Uuid;

        // Branch: baixa events resolve via the dedicated baixa repository.
        if (eventType == WebhookEventType.Baixa)
        {
            return await HandleBaixaAsync(resultado, transactionId, rawJson, payload, cancellationToken);
        }

        // Negativação (Inclusao / Avalista) — caminho legado preservado.
        var solicitacao = await _repository.GetByTransactionIdAsync(transactionId, cancellationToken);
        if (solicitacao is null)
        {
            _logger.LogWarning("No matching solicitation found for TransactionId {TransactionId}. Persisting orphan webhook.", transactionId);
            await PersistOrphanWebhookAsync(eventType, resultado, payload.Uuid, transactionId, rawJson, cancellationToken);
            return WebhookResult.OrphanWebhook;
        }

        ApplyToSolicitacao(solicitacao, eventType, resultado, payload, rawJson);

        var webhookRecord = BuildWebhookRecord(eventType, resultado, payload.Uuid, transactionId, rawJson, solicitacao.Id, true, null);
        await _repository.ApplyWebhookTransactionalAsync(solicitacao, webhookRecord, cancellationToken);

        _logger.LogInformation(
            "Webhook processed successfully. UUID: {Uuid}, TransactionId: {TransactionId}, NewStatus: {Status}",
            payload.Uuid, transactionId, solicitacao.Status);

        await DispatchNotificationsAsync(eventType, resultado, solicitacao, payload, cancellationToken);

        return WebhookResult.Processed(payload.Uuid);
    }

    /// <summary>
    /// Handles a baixa webhook: resolves via the baixa repository, applies the
    /// state transition on the baixa aggregate, persists transactionally and
    /// notifies the solicitante.
    /// </summary>
    private async Task<WebhookResult> HandleBaixaAsync(
        WebhookResultado resultado,
        string transactionId,
        string rawJson,
        SerasaWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        var baixa = await _baixaRepository.GetByTransactionIdAsync(transactionId, cancellationToken);
        if (baixa is null)
        {
            _logger.LogWarning(
                "No matching baixa found for TransactionId {TransactionId}. Persisting orphan webhook.", transactionId);
            await PersistOrphanWebhookAsync(WebhookEventType.Baixa, resultado, payload.Uuid, transactionId, rawJson, cancellationToken);
            return WebhookResult.OrphanWebhook;
        }

        ApplyToBaixa(baixa, resultado, payload, rawJson);

        // MATCHED_SOLICITACAO_ID tem FK -> SERASA_PEFIN_SOLICITACOES.ID. Como baixa.Id
        // pertence a SERASA_PEFIN_BAIXAS, usamos a solicitação-mãe (IdSolicitacaoNegativacao)
        // para satisfazer a constraint. A baixa específica é identificável pelo
        // transactionId (UUID), que é único.
        var webhookRecord = BuildWebhookRecord(
            WebhookEventType.Baixa, resultado, payload.Uuid, transactionId, rawJson, baixa.IdSolicitacaoNegativacao, true, null);

        await _baixaRepository.ApplyWebhookTransactionalAsync(baixa, webhookRecord, cancellationToken);

        _logger.LogInformation(
            "Baixa webhook processed. UUID: {Uuid}, TransactionId: {TransactionId}, NewStatus: {Status}",
            payload.Uuid, transactionId, baixa.Status);

        await DispatchBaixaNotificationAsync(resultado, baixa, payload, cancellationToken);

        return WebhookResult.Processed(payload.Uuid);
    }

    /// <summary>
    /// Applies the baixa-specific webhook transition. Sucesso → BaixadoSucesso;
    /// Erro → BaixadoErro (com mensagem e statusCode capturados).
    /// </summary>
    private static void ApplyToBaixa(
        SerasaPefinBaixaSolicitacao baixa,
        WebhookResultado resultado,
        SerasaWebhookPayload payload,
        string rawJson)
    {
        if (resultado == WebhookResultado.Erro)
        {
            var errorMessage = payload.Error?.Message ?? "Unknown error";
            var statusCode = payload.Error?.StatusCode;
            baixa.AplicarWebhookErro(rawJson, errorMessage, statusCode);
            return;
        }

        baixa.AplicarWebhookSucesso(rawJson);
    }

    /// <summary>
    /// Notifies the solicitante (in-app) on baixa final state.
    /// </summary>
    private async Task DispatchBaixaNotificationAsync(
        WebhookResultado resultado,
        SerasaPefinBaixaSolicitacao baixa,
        SerasaWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baixa.SolicitanteUsername))
        {
            _logger.LogInformation(
                "No solicitante to notify for baixa {BaixaId} (legacy/orphan record).", baixa.Id);
            return;
        }

        var parcela = baixa.NumeroParcela?.ToString() ?? "-";

        try
        {
            if (resultado == WebhookResultado.Sucesso)
            {
                var mensagem = $"Baixa concluída com sucesso para parcela {parcela} da venda {baixa.NumVendaFk}.";
                var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    mensagem,
                    solicitanteUsername = baixa.SolicitanteUsername,
                    solicitacaoId = baixa.Id,
                    numVenda = baixa.NumVendaFk,
                    numeroParcela = baixa.NumeroParcela,
                    status = baixa.Status.ToDbValue(),
                });

                await _notificationDispatcher.DispatchAsync(
                    NotificationType.RetornoBaixaSucesso,
                    baixa.SolicitanteUsername,
                    baixa.NumVendaFk,
                    payloadJson,
                    null,
                    baixa.Id.ToString(),
                    cancellationToken);
            }
            else
            {
                var serasaErrorMessage = payload.Error?.Message ?? baixa.ErrorMessage ?? "Erro desconhecido";
                var mensagem =
                    $"Baixa rejeitada pela Serasa: {serasaErrorMessage}. Reenvie a solicitação se necessário.";
                var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    mensagem,
                    solicitanteUsername = baixa.SolicitanteUsername,
                    solicitacaoId = baixa.Id,
                    numVenda = baixa.NumVendaFk,
                    numeroParcela = baixa.NumeroParcela,
                    status = baixa.Status.ToDbValue(),
                    errorMessage = serasaErrorMessage,
                    errorStatusCode = payload.Error?.StatusCode ?? baixa.ErrorStatusCode,
                    tentativas = baixa.Tentativas,
                });

                await _notificationDispatcher.DispatchAsync(
                    NotificationType.RetornoBaixaErro,
                    baixa.SolicitanteUsername,
                    baixa.NumVendaFk,
                    payloadJson,
                    null,
                    baixa.Id.ToString(),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Falha na notificação não deve reverter o processamento do webhook.
            _logger.LogWarning(ex,
                "Failed to dispatch baixa notification. BaixaId: {BaixaId}, Solicitante: {Solicitante}",
                baixa.Id, baixa.SolicitanteUsername);
        }
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
                // Baixa events are handled by HandleBaixaAsync via ISerasaPefinBaixaRepository.
                // This branch is kept only for legacy fallback (e.g., orphan webhook persistence)
                // and should not be reached in the active code path.
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

    /// <summary>
    /// Dispatches notifications to solicitante and aprovador after webhook processing.
    /// Only dispatches for inclusão webhooks (sucesso or erro); baixa webhooks are out of scope.
    /// </summary>
    private async Task DispatchNotificationsAsync(
        WebhookEventType eventType,
        WebhookResultado resultado,
        SerasaPefinSolicitacaoCompleta solicitacao,
        SerasaWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        // Baixa webhooks are dispatched by DispatchBaixaNotificationAsync, never here.
        if (eventType == WebhookEventType.Baixa)
        {
            return;
        }

        // Collect usernames to notify (skip if null for legacy records)
        var usernamesToNotify = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(solicitacao.SolicitanteUsername))
        {
            usernamesToNotify.Add(solicitacao.SolicitanteUsername);
        }
        if (!string.IsNullOrWhiteSpace(solicitacao.AprovadorUsername))
        {
            usernamesToNotify.Add(solicitacao.AprovadorUsername);
        }

        // Skip if no usernames to notify (legacy record without approval workflow)
        if (usernamesToNotify.Count == 0)
        {
            _logger.LogInformation("No usernames to notify for solicitation {SolicitacaoId} (legacy record without approval workflow)", solicitacao.Id);
            return;
        }

        try
        {
            if (resultado == WebhookResultado.Sucesso)
            {
                // Success notification
                var mensagem = $"Cliente negativado com sucesso (venda nº {solicitacao.NumVendaFk}).";
                _logger.LogInformation("Dispatching success notification to {Count} users for solicitation {SolicitacaoId}", usernamesToNotify.Count, solicitacao.Id);
                await _notificationDispatcher.DispatchManyAsync(
                    NotificationType.RetornoSerasaSucesso,
                    usernamesToNotify,
                    solicitacao.NumVendaFk,
                    mensagem,
                    null,
                    null,
                    cancellationToken);
            }
            else if (resultado == WebhookResultado.Erro)
            {
                // Error notification
                var errorMessage = payload.Error?.Message ?? solicitacao.ErrorMessage ?? "Erro desconhecido";
                var mensagem = $"Erro ao negativar cliente (venda nº {solicitacao.NumVendaFk}): {errorMessage}";
                _logger.LogInformation("Dispatching error notification to {Count} users for solicitation {SolicitacaoId}", usernamesToNotify.Count, solicitacao.Id);
                await _notificationDispatcher.DispatchManyAsync(
                    NotificationType.RetornoSerasaErro,
                    usernamesToNotify,
                    solicitacao.NumVendaFk,
                    mensagem,
                    null,
                    null,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Dispatch failure should not revert webhook processing
            _logger.LogWarning(ex, "Failed to dispatch notification for solicitation {SolicitacaoId}. Webhook processing succeeded.", solicitacao.Id);
        }
    }
}

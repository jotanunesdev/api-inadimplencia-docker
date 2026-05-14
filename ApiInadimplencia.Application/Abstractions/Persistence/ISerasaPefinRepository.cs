using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for the Serasa PEFIN aggregate (<c>dbo.SERASA_PEFIN_SOLICITACOES</c>
/// and <c>dbo.SERASA_PEFIN_WEBHOOKS</c>). Implementations must guarantee idempotency and
/// serializable writes as specified in the Node reference model.
/// </summary>
public interface ISerasaPefinRepository
{
    /// <summary>
    /// Inserts a new solicitation row atomically. The SQL implementation relies on the
    /// <c>UX_SERASA_PEFIN_SOLICITACOES_ATIVA</c> filtered unique index to reject duplicated
    /// active requests.
    /// </summary>
    /// <exception cref="SerasaPefinDuplicateActiveException">When a duplicated active entry exists.</exception>
    Task<Guid> AddAsync(SerasaPefinSolicitacaoCompleta solicitacao, CancellationToken cancellationToken);

    /// <summary>
    /// Updates every persisted column of an existing solicitation.
    /// </summary>
    Task UpdateAsync(SerasaPefinSolicitacaoCompleta solicitacao, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a solicitation by its primary key.
    /// </summary>
    Task<SerasaPefinSolicitacaoCompleta?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a solicitation by the Serasa transaction id (UUID that also appears in webhook payloads).
    /// </summary>
    Task<SerasaPefinSolicitacaoCompleta?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all solicitations linked to a sale, ordered by creation date (desc).
    /// </summary>
    Task<IReadOnlyList<SerasaPefinSolicitacaoCompleta>> ListByNumVendaAsync(int numVenda, CancellationToken cancellationToken);

    /// <summary>
    /// Returns whether there is already an active solicitation for the given identity tuple.
    /// </summary>
    Task<bool> ExistsActiveAsync(
        int numVenda,
        string contractNumber,
        string documentoDevedor,
        string? documentoGarantidor,
        SerasaPefinRecordType tipoRegistro,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists a webhook row and, when <paramref name="matchedSolicitacaoId"/> is provided,
    /// updates the referenced solicitation atomically inside a single SERIALIZABLE transaction.
    /// </summary>
    Task AddWebhookAsync(SerasaPefinWebhookRecord webhook, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a webhook with the given UUID has already been processed (idempotency).
    /// </summary>
    Task<bool> WebhookExistsByUuidAsync(string uuid, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a webhook row and updates the referenced solicitation atomically
    /// inside a single SERIALIZABLE transaction.
    /// </summary>
    Task ApplyWebhookTransactionalAsync(
        SerasaPefinSolicitacaoCompleta solicitacao,
        SerasaPefinWebhookRecord webhook,
        CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot passed to <see cref="ISerasaPefinRepository.AddWebhookAsync"/>. Keeps the repository
/// signature small while allowing the Application layer to describe what it has received.
/// </summary>
/// <param name="Id">Generated identifier.</param>
/// <param name="EventType">Canonical event type (e.g. <c>INCLUSAO_SUCESSO</c>).</param>
/// <param name="TransactionId">UUID extracted from the webhook payload (null when missing).</param>
/// <param name="Payload">Raw webhook JSON payload.</param>
/// <param name="MatchedSolicitacaoId">Matched solicitation id when the UUID was resolved.</param>
/// <param name="Processado">Whether the webhook has produced a state change in the solicitation.</param>
/// <param name="MensagemErro">Error message captured when <paramref name="Processado"/> is false.</param>
/// <param name="DtRecebimento">Reception timestamp (UTC).</param>
public sealed record SerasaPefinWebhookRecord(
    Guid Id,
    string EventType,
    string? TransactionId,
    string Payload,
    Guid? MatchedSolicitacaoId,
    bool Processado,
    string? MensagemErro,
    DateTime DtRecebimento);

/// <summary>
/// Thrown by <see cref="ISerasaPefinRepository"/> implementations when the filtered unique index
/// <c>UX_SERASA_PEFIN_SOLICITACOES_ATIVA</c> is violated, indicating that an active solicitation
/// already exists for the same (NumVenda, Contract, DocumentoDevedor, DocumentoGarantidor, TipoRegistro).
/// </summary>
public sealed class SerasaPefinDuplicateActiveException : Exception
{
    public SerasaPefinDuplicateActiveException(string message) : base(message)
    {
    }

    public SerasaPefinDuplicateActiveException(string message, Exception inner) : base(message, inner)
    {
    }
}

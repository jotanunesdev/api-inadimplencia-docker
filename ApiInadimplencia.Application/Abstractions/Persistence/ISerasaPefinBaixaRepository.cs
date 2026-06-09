using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for the Serasa PEFIN baixa (write-off) aggregate
/// (<c>dbo.SERASA_PEFIN_BAIXAS</c>). Implementations must guarantee idempotency
/// via the filtered unique index <c>UX_SERASA_PEFIN_BAIXAS_ATIVA</c> and use
/// SERIALIZABLE transactions for write operations involving multiple rows.
/// Webhooks reuse the shared <c>dbo.SERASA_PEFIN_WEBHOOKS</c> table via
/// <see cref="SerasaPefinWebhookRecord"/>.
/// </summary>
public interface ISerasaPefinBaixaRepository
{
    /// <summary>
    /// Inserts a new baixa solicitation atomically.
    /// </summary>
    /// <exception cref="SerasaPefinBaixaDuplicateActiveException">
    /// When an active baixa already exists for the same (NUM_VENDA, CONTRACT, PARCELA) combination.
    /// </exception>
    Task<Guid> AddAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts multiple baixa solicitations atomically within a single transaction.
    /// </summary>
    /// <exception cref="SerasaPefinBaixaDuplicateActiveException">When duplicated active entry exists.</exception>
    Task AddManyAsync(IReadOnlyCollection<SerasaPefinBaixaSolicitacao> baixas, CancellationToken cancellationToken);

    /// <summary>
    /// Updates every persisted column of an existing baixa solicitation.
    /// </summary>
    Task UpdateAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken);

    /// <summary>Retrieves a baixa solicitation by its primary key.</summary>
    Task<SerasaPefinBaixaSolicitacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Retrieves a baixa solicitation by the Serasa transaction id (UUID).</summary>
    Task<SerasaPefinBaixaSolicitacao?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns whether there is already an ACTIVE baixa for the given (NumVenda, ContractNumber, NumeroParcela) tuple.
    /// Active states are AGUARDANDO_APROVACAO, APROVADA, PENDENTE_ENVIO, BAIXA_ENVIADA, BAIXA_AGUARDANDO_RETORNO.
    /// </summary>
    Task<bool> ExistsActiveAsync(
        int numVendaFk,
        string contractNumber,
        int? numeroParcela,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists baixa solicitations filtered by status/venda/solicitante, ordered by creation date desc, paginated.
    /// </summary>
    Task<IReadOnlyList<SerasaPefinBaixaSolicitacao>> ListByStatusAsync(
        SerasaPefinBaixaStatus? status,
        int? numVenda,
        string? solicitanteUsername,
        int take,
        int skip,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists a webhook row and updates the referenced baixa solicitation atomically inside a SERIALIZABLE transaction.
    /// </summary>
    Task ApplyWebhookTransactionalAsync(
        SerasaPefinBaixaSolicitacao baixa,
        SerasaPefinWebhookRecord webhook,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the set of <c>NUMERO_PARCELA</c> values that already have at least
    /// one baixa in <c>BAIXADO_SUCESSO</c> status for the given venda. Used by
    /// the dividas-elegiveis listing to mark parcelas as already settled, so the
    /// UI does not offer them again for negativacao or baixa.
    /// </summary>
    Task<IReadOnlySet<int>> ListParcelasComBaixaConcluidaAsync(
        int numVendaFk,
        CancellationToken cancellationToken);
}

/// <summary>
/// Thrown when the filtered unique index <c>UX_SERASA_PEFIN_BAIXAS_ATIVA</c> is violated,
/// indicating that an active baixa already exists for the same parcela.
/// </summary>
public sealed class SerasaPefinBaixaDuplicateActiveException : Exception
{
    public SerasaPefinBaixaDuplicateActiveException(string message) : base(message)
    {
    }

    public SerasaPefinBaixaDuplicateActiveException(string message, Exception inner) : base(message, inner)
    {
    }
}

using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;

namespace api_inadimplencia.Api.Tests.Infrastructure;

public sealed class InMemorySerasaPefinRepository : ISerasaPefinRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, SerasaPefinSolicitacaoCompleta> _solicitacoes = new();
    private readonly Dictionary<string, Guid> _transactionIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _webhookUuids = new(StringComparer.OrdinalIgnoreCase);

    public void Clear()
    {
        lock (_gate)
        {
            _solicitacoes.Clear();
            _transactionIds.Clear();
            _webhookUuids.Clear();
        }
    }

    public Task<Guid> AddAsync(SerasaPefinSolicitacaoCompleta solicitacao, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitacao);

        lock (_gate)
        {
            EnsureNoDuplicateActive(solicitacao);
            _solicitacoes[solicitacao.Id] = solicitacao;
            IndexTransactionId(solicitacao);
            return Task.FromResult(solicitacao.Id);
        }
    }

    public Task AddManyAsync(IReadOnlyCollection<SerasaPefinSolicitacaoCompleta> solicitacoes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitacoes);

        lock (_gate)
        {
            foreach (var solicitacao in solicitacoes)
            {
                ArgumentNullException.ThrowIfNull(solicitacao);
                EnsureNoDuplicateActive(solicitacao);
            }

            foreach (var solicitacao in solicitacoes)
            {
                _solicitacoes[solicitacao.Id] = solicitacao;
                IndexTransactionId(solicitacao);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(SerasaPefinSolicitacaoCompleta solicitacao, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitacao);

        lock (_gate)
        {
            _solicitacoes[solicitacao.Id] = solicitacao;
            IndexTransactionId(solicitacao);
        }

        return Task.CompletedTask;
    }

    public Task UpdateManyAsync(IReadOnlyCollection<SerasaPefinSolicitacaoCompleta> solicitacoes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitacoes);

        lock (_gate)
        {
            foreach (var solicitacao in solicitacoes)
            {
                ArgumentNullException.ThrowIfNull(solicitacao);
                _solicitacoes[solicitacao.Id] = solicitacao;
                IndexTransactionId(solicitacao);
            }
        }

        return Task.CompletedTask;
    }

    public Task<SerasaPefinSolicitacaoCompleta?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _solicitacoes.TryGetValue(id, out var solicitacao);
            return Task.FromResult(solicitacao);
        }
    }

    public Task<SerasaPefinSolicitacaoCompleta?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return Task.FromResult<SerasaPefinSolicitacaoCompleta?>(null);
        }

        lock (_gate)
        {
            if (_transactionIds.TryGetValue(transactionId, out var id) && _solicitacoes.TryGetValue(id, out var solicitacao))
            {
                return Task.FromResult<SerasaPefinSolicitacaoCompleta?>(solicitacao);
            }
        }

        return Task.FromResult<SerasaPefinSolicitacaoCompleta?>(null);
    }

    public Task<IReadOnlyList<SerasaPefinSolicitacaoCompleta>> ListByNumVendaAsync(int numVenda, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var result = _solicitacoes.Values
                .Where(s => s.NumVendaFk == numVenda)
                .OrderByDescending(s => s.DtCriacao)
                .ToList();

            return Task.FromResult<IReadOnlyList<SerasaPefinSolicitacaoCompleta>>(result);
        }
    }

    public Task<IReadOnlyList<SerasaPefinSolicitacaoCompleta>> ListByIdSolicitacaoPaiAsync(Guid solicitacaoPaiId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var result = _solicitacoes.Values
                .Where(s => s.IdSolicitacaoPai == solicitacaoPaiId && s.NumeroParcela.HasValue)
                .OrderBy(s => s.NumeroParcela)
                .ThenBy(s => s.DtCriacao)
                .ToList();

            return Task.FromResult<IReadOnlyList<SerasaPefinSolicitacaoCompleta>>(result);
        }
    }

    public Task<IReadOnlyList<SerasaPefinSolicitacaoCompleta>> ListByStatusAsync(SerasaPefinStatus? status, int? numVenda, Guid? solicitacaoId, string? solicitanteUsername, int take, int skip, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IEnumerable<SerasaPefinSolicitacaoCompleta> query = _solicitacoes.Values;

            if (status.HasValue)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            if (numVenda.HasValue)
            {
                query = query.Where(s => s.NumVendaFk == numVenda.Value);
            }

            if (solicitacaoId.HasValue)
            {
                query = query.Where(s => s.Id == solicitacaoId.Value);
            }

            if (!string.IsNullOrWhiteSpace(solicitanteUsername))
            {
                query = query.Where(s => string.Equals(s.SolicitanteUsername, solicitanteUsername, StringComparison.OrdinalIgnoreCase));
            }

            var result = query
                .OrderByDescending(s => s.DtCriacao)
                .Skip(skip)
                .Take(take)
                .ToList();

            return Task.FromResult<IReadOnlyList<SerasaPefinSolicitacaoCompleta>>(result);
        }
    }

    public Task<bool> ExistsActiveAsync(int numVenda, string contractNumber, string documentoDevedor, string? documentoGarantidor, SerasaPefinRecordType tipoRegistro, CancellationToken cancellationToken)
    {
        return ExistsActiveAsync(numVenda, contractNumber, documentoDevedor, documentoGarantidor, tipoRegistro, null, cancellationToken);
    }

    public Task<bool> ExistsActiveAsync(int numVenda, string contractNumber, string documentoDevedor, string? documentoGarantidor, SerasaPefinRecordType tipoRegistro, int? numeroParcela, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var exists = _solicitacoes.Values.Any(s =>
                s.NumVendaFk == numVenda &&
                string.Equals(s.ContractNumber, contractNumber, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.DocumentoDevedor, documentoDevedor, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.DocumentoGarantidor, documentoGarantidor, StringComparison.OrdinalIgnoreCase) &&
                s.TipoRegistro == tipoRegistro &&
                s.NumeroParcela == numeroParcela &&
                IsActiveStatus(s.Status));

            return Task.FromResult(exists);
        }
    }

    public Task AddWebhookAsync(SerasaPefinWebhookRecord webhook, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(webhook.TransactionId))
            {
                _webhookUuids.Add(webhook.TransactionId);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> WebhookExistsByUuidAsync(string uuid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(uuid) && _webhookUuids.Contains(uuid));
        }
    }

    public Task ApplyWebhookTransactionalAsync(SerasaPefinSolicitacaoCompleta solicitacao, SerasaPefinWebhookRecord webhook, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitacao);

        lock (_gate)
        {
            _solicitacoes[solicitacao.Id] = solicitacao;
            IndexTransactionId(solicitacao);
            if (!string.IsNullOrWhiteSpace(webhook.TransactionId))
            {
                _webhookUuids.Add(webhook.TransactionId);
            }
        }

        return Task.CompletedTask;
    }

    private void EnsureNoDuplicateActive(SerasaPefinSolicitacaoCompleta solicitacao)
    {
        var duplicateExists = _solicitacoes.Values.Any(existing =>
            existing.Id != solicitacao.Id &&
            existing.NumVendaFk == solicitacao.NumVendaFk &&
            string.Equals(existing.ContractNumber, solicitacao.ContractNumber, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.DocumentoDevedor, solicitacao.DocumentoDevedor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.DocumentoGarantidor, solicitacao.DocumentoGarantidor, StringComparison.OrdinalIgnoreCase) &&
            existing.TipoRegistro == solicitacao.TipoRegistro &&
            existing.NumeroParcela == solicitacao.NumeroParcela &&
            IsActiveStatus(existing.Status));

        if (duplicateExists)
        {
            throw new SerasaPefinDuplicateActiveException("Active Serasa PEFIN solicitation already exists for the same (NUM_VENDA, CONTRACT_NUMBER, DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, TIPO_REGISTRO, NUMERO_PARCELA) combination.");
        }
    }

    private void IndexTransactionId(SerasaPefinSolicitacaoCompleta solicitacao)
    {
        if (!string.IsNullOrWhiteSpace(solicitacao.TransactionId))
        {
            _transactionIds[solicitacao.TransactionId] = solicitacao.Id;
        }
    }

    private static bool IsActiveStatus(SerasaPefinStatus status)
    {
        return status is SerasaPefinStatus.AguardandoAprovacao
            or SerasaPefinStatus.Aprovada
            or SerasaPefinStatus.AprovadaFalhaEnvio
            or SerasaPefinStatus.PendenteEnvio
            or SerasaPefinStatus.EnviadoSerasa
            or SerasaPefinStatus.AguardandoRetorno;
    }
}

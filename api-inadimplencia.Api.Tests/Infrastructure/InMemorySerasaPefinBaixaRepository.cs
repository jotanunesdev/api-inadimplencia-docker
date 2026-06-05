using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;

namespace api_inadimplencia.Api.Tests.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="ISerasaPefinBaixaRepository"/> used by API
/// integration tests. Replicates the active-row uniqueness invariant of the SQL
/// filtered unique index <c>UX_SERASA_PEFIN_BAIXAS_ATIVA</c>.
/// </summary>
public sealed class InMemorySerasaPefinBaixaRepository : ISerasaPefinBaixaRepository
{
    private static readonly HashSet<SerasaPefinBaixaStatus> ActiveStatuses = new()
    {
        SerasaPefinBaixaStatus.AguardandoAprovacao,
        SerasaPefinBaixaStatus.Aprovada,
        SerasaPefinBaixaStatus.PendenteEnvio,
        SerasaPefinBaixaStatus.BaixaEnviada,
        SerasaPefinBaixaStatus.BaixaAguardandoRetorno,
    };

    private readonly object _gate = new();
    private readonly Dictionary<Guid, SerasaPefinBaixaSolicitacao> _baixas = new();

    public void Clear()
    {
        lock (_gate)
        {
            _baixas.Clear();
        }
    }

    public Task<Guid> AddAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixa);
        lock (_gate)
        {
            EnsureNoDuplicateActive(baixa);
            _baixas[baixa.Id] = baixa;
        }
        return Task.FromResult(baixa.Id);
    }

    public Task AddManyAsync(IReadOnlyCollection<SerasaPefinBaixaSolicitacao> baixas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixas);
        lock (_gate)
        {
            foreach (var b in baixas)
            {
                EnsureNoDuplicateActive(b);
            }
            foreach (var b in baixas)
            {
                _baixas[b.Id] = b;
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixa);
        lock (_gate)
        {
            _baixas[baixa.Id] = baixa;
        }
        return Task.CompletedTask;
    }

    public Task<SerasaPefinBaixaSolicitacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _baixas.TryGetValue(id, out var b);
            return Task.FromResult(b);
        }
    }

    public Task<SerasaPefinBaixaSolicitacao?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return Task.FromResult<SerasaPefinBaixaSolicitacao?>(null);
        }
        lock (_gate)
        {
            var match = _baixas.Values.FirstOrDefault(b => string.Equals(b.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(match);
        }
    }

    public Task<bool> ExistsActiveAsync(int numVendaFk, string contractNumber, int? numeroParcela, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var exists = _baixas.Values.Any(b =>
                b.NumVendaFk == numVendaFk
                && string.Equals(b.ContractNumber, contractNumber, StringComparison.OrdinalIgnoreCase)
                && b.NumeroParcela == numeroParcela
                && ActiveStatuses.Contains(b.Status));
            return Task.FromResult(exists);
        }
    }

    public Task<IReadOnlyList<SerasaPefinBaixaSolicitacao>> ListByStatusAsync(
        SerasaPefinBaixaStatus? status,
        int? numVenda,
        string? solicitanteUsername,
        int take,
        int skip,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var query = _baixas.Values.AsEnumerable();
            if (status.HasValue)
            {
                query = query.Where(b => b.Status == status.Value);
            }
            if (numVenda.HasValue)
            {
                query = query.Where(b => b.NumVendaFk == numVenda.Value);
            }
            if (!string.IsNullOrWhiteSpace(solicitanteUsername))
            {
                query = query.Where(b => string.Equals(b.SolicitanteUsername, solicitanteUsername, StringComparison.OrdinalIgnoreCase));
            }

            var result = query
                .OrderByDescending(b => b.DtCriacao)
                .Skip(Math.Max(0, skip))
                .Take(Math.Max(0, take))
                .ToList();

            return Task.FromResult<IReadOnlyList<SerasaPefinBaixaSolicitacao>>(result);
        }
    }

    public Task ApplyWebhookTransactionalAsync(
        SerasaPefinBaixaSolicitacao baixa,
        SerasaPefinWebhookRecord webhook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixa);
        lock (_gate)
        {
            _baixas[baixa.Id] = baixa;
        }
        return Task.CompletedTask;
    }

    private void EnsureNoDuplicateActive(SerasaPefinBaixaSolicitacao baixa)
    {
        var conflict = _baixas.Values.Any(b =>
            b.Id != baixa.Id
            && b.NumVendaFk == baixa.NumVendaFk
            && string.Equals(b.ContractNumber, baixa.ContractNumber, StringComparison.OrdinalIgnoreCase)
            && b.NumeroParcela == baixa.NumeroParcela
            && ActiveStatuses.Contains(b.Status));

        if (conflict)
        {
            throw new SerasaPefinBaixaDuplicateActiveException(
                $"JA_EM_APROVACAO: baixa ativa para venda {baixa.NumVendaFk} parcela {baixa.NumeroParcela}.");
        }
    }
}

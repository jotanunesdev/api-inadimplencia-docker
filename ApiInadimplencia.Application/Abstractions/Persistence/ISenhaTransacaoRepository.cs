using ApiInadimplencia.Domain.Negativacao;

namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Repository for transaction password persistence.
/// </summary>
public interface ISenhaTransacaoRepository
{
    /// <summary>
    /// Gets the transaction password record by username.
    /// </summary>
    Task<UsuarioSenhaTransacao?> GetByUsernameAsync(string username, CancellationToken ct);

    /// <summary>
    /// Creates or updates a transaction password record.
    /// </summary>
    Task UpsertAsync(UsuarioSenhaTransacao senha, CancellationToken ct);
}

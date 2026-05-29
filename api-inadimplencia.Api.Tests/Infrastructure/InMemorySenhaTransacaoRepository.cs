using System.Collections.Concurrent;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.Negativacao;

namespace api_inadimplencia.Api.Tests.Infrastructure;

public sealed class InMemorySenhaTransacaoRepository : ISenhaTransacaoRepository
{
    private readonly ConcurrentDictionary<string, UsuarioSenhaTransacao> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<UsuarioSenhaTransacao?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_store.TryGetValue(username, out var senha))
        {
            return Task.FromResult<UsuarioSenhaTransacao?>(Clone(senha));
        }

        return Task.FromResult<UsuarioSenhaTransacao?>(null);
    }

    public Task UpsertAsync(UsuarioSenhaTransacao senha, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _store[senha.Username] = Clone(senha);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _store.Clear();
    }

    private static UsuarioSenhaTransacao Clone(UsuarioSenhaTransacao senha)
    {
        return UsuarioSenhaTransacao.Reconstruct(
            senha.Username,
            senha.Hash,
            senha.TentativasFalhas,
            senha.BloqueadoAte,
            senha.CriadaEm,
            senha.AtualizadaEm);
    }
}

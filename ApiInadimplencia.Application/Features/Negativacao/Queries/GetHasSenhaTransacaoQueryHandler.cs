using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Handler for GetHasSenhaTransacaoQuery.
/// </summary>
public sealed class GetHasSenhaTransacaoQueryHandler : IQueryHandler<GetHasSenhaTransacaoQuery, bool>
{
    private readonly ISenhaTransacaoRepository _repository;

    public GetHasSenhaTransacaoQueryHandler(ISenhaTransacaoRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(GetHasSenhaTransacaoQuery query, CancellationToken cancellationToken = default)
    {
        var senhaTransacao = await _repository.GetByUsernameAsync(query.Username, cancellationToken);
        return senhaTransacao != null;
    }
}

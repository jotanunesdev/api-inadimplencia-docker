using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Domain.Negativacao;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// Implementation of transaction password validator with lockout policy.
/// </summary>
public sealed class SenhaTransacaoValidator : ISenhaTransacaoValidator
{
    private readonly ISenhaTransacaoRepository _repository;
    private readonly ISenhaTransacaoHasher _hasher;
    private readonly NegativacaoOptions _options;

    public SenhaTransacaoValidator(
        ISenhaTransacaoRepository repository,
        ISenhaTransacaoHasher hasher,
        IOptions<NegativacaoOptions> options)
    {
        _repository = repository;
        _hasher = hasher;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<SenhaTransacaoValidationResult> ValidateAsync(
        string username,
        string senha,
        CancellationToken ct = default)
    {
        var senhaTransacao = await _repository.GetByUsernameAsync(username, ct);

        if (senhaTransacao == null)
        {
            return SenhaTransacaoValidationResult.NotSet;
        }

        var utcNow = DateTime.UtcNow;

        // Check if account is locked out
        if (senhaTransacao.EstaBloqueado(utcNow))
        {
            return SenhaTransacaoValidationResult.LockedOut;
        }

        // Verify password
        var isValid = _hasher.Verify(senhaTransacao.Hash, senha);

        if (isValid)
        {
            senhaTransacao.RegistrarTentativaValida();
            await _repository.UpsertAsync(senhaTransacao, ct);
            return SenhaTransacaoValidationResult.Valid;
        }
        else
        {
            var lockoutDuration = TimeSpan.FromMinutes(_options.LockoutMinutos);
            senhaTransacao.RegistrarTentativaInvalida(_options.MaxTentativasSenha, lockoutDuration, utcNow);
            await _repository.UpsertAsync(senhaTransacao, ct);

            // Check if just got locked out
            if (senhaTransacao.EstaBloqueado(utcNow))
            {
                return SenhaTransacaoValidationResult.LockedOut;
            }

            return SenhaTransacaoValidationResult.Invalid;
        }
    }
}

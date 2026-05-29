using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.Negativacao;

namespace ApiInadimplencia.Application.Features.Negativacao.Commands;

/// <summary>
/// Handler for SetSenhaTransacaoCommand.
/// </summary>
public sealed class SetSenhaTransacaoCommandHandler : ICommandHandler<SetSenhaTransacaoCommand, bool>
{
    private readonly ISenhaTransacaoRepository _repository;
    private readonly ISenhaTransacaoHasher _hasher;

    private const int MinSenhaLength = 6;

    public SetSenhaTransacaoCommandHandler(
        ISenhaTransacaoRepository repository,
        ISenhaTransacaoHasher hasher)
    {
        _repository = repository;
        _hasher = hasher;
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(SetSenhaTransacaoCommand command, CancellationToken cancellationToken = default)
    {
        // Validate new password length
        if (string.IsNullOrWhiteSpace(command.NovaSenha) || command.NovaSenha.Length < MinSenhaLength)
        {
            throw new ArgumentException($"Password must be at least {MinSenhaLength} characters.", nameof(command.NovaSenha));
        }

        // Check if user already has a password
        var existing = await _repository.GetByUsernameAsync(command.Username, cancellationToken);

        if (existing == null)
        {
            // First time setting password - no current password needed
            var hash = _hasher.Hash(command.NovaSenha);
            var novaSenha = UsuarioSenhaTransacao.Criar(command.Username, hash);
            await _repository.UpsertAsync(novaSenha, cancellationToken);
        }
        else
        {
            // Updating existing password - current password required
            if (string.IsNullOrWhiteSpace(command.SenhaAtual))
            {
                throw new UnauthorizedAccessException("Current password is required to update password.");
            }

            // Verify current password
            var isValid = _hasher.Verify(existing.Hash, command.SenhaAtual);
            if (!isValid)
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            // Update with new password
            var novoHash = _hasher.Hash(command.NovaSenha);
            existing.AtualizarHash(novoHash);
            await _repository.UpsertAsync(existing, cancellationToken);
        }

        return true;
    }
}

using ApiInadimplencia.Domain.Users;

namespace ApiInadimplencia.Application.Features.Usuarios;

/// <summary>
/// Repository interface for users.
/// </summary>
public interface IUsuarioRepository
{
    Task<Usuario?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken);
    Task<Usuario?> GetByNomeAsync(string nome, CancellationToken cancellationToken);
    Task AddAsync(Usuario usuario, CancellationToken cancellationToken);
    Task UpdateAsync(Usuario usuario, CancellationToken cancellationToken);
    Task DeleteAsync(string userCode, CancellationToken cancellationToken);
}

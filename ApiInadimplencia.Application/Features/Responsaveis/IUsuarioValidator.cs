namespace ApiInadimplencia.Application.Features.Responsaveis;

/// <summary>
/// Validator for checking if a user exists and has admin profile.
/// </summary>
public interface IUsuarioValidator
{
    Task<bool> IsAdminUserAsync(string userCode, CancellationToken cancellationToken);
}

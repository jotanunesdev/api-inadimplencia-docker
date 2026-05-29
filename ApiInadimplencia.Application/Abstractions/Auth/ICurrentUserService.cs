namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Porta para obter informações do usuário autenticado atual.
/// Implementação em Infrastructure (adapter) usa IHttpContextAccessor.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Nome do usuário autenticado (username/login).
    /// Null se não houver usuário autenticado.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Indica se há um usuário autenticado no contexto atual.
    /// </summary>
    bool IsAuthenticated { get; }
}

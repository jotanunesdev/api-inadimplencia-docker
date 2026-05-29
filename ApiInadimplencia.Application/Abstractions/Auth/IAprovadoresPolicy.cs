namespace ApiInadimplencia.Application.Abstractions.Auth;

/// <summary>
/// Porta para verificar se um usuário é aprovador autorizado e listar aprovadores.
/// Implementação em Infrastructure (adapter) usa IOptions<NegativacaoOptions>.
/// </summary>
public interface IAprovadoresPolicy
{
    /// <summary>
    /// Verifica se o username fornecido é um aprovador autorizado.
    /// A comparação deve ser case-insensitive.
    /// </summary>
    /// <param name="username">Username a verificar (pode ser null ou vazio).</param>
    /// <returns>True se o usuário está na lista de aprovadores; false caso contrário.</returns>
    bool IsAprovador(string? username);

    /// <summary>
    /// Retorna a lista imutável de usernames aprovadores.
    /// </summary>
    /// <returns>Lista readonly de aprovadores.</returns>
    IReadOnlyList<string> ListAprovadores();

    /// <summary>
    /// Verifica se o username é um "super decisor", autorizado a aprovar/rejeitar
    /// inclusive solicitações que ele mesmo criou (bypass anti-conluio).
    /// Implementação padrão retorna false para preservar comportamento existente
    /// quando adapters customizados/legados ou mocks de teste não a sobrepõem.
    /// </summary>
    /// <param name="username">Username a verificar (pode ser null ou vazio).</param>
    /// <returns>True quando o usuário tem permissão especial.</returns>
    bool IsSuperDecisor(string? username) => false;
}

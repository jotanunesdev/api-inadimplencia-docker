namespace ApiInadimplencia.Domain.Users;

/// <summary>
/// Operational user profile accepted by the inadimplencia module.
/// </summary>
public enum UserProfile
{
    /// <summary>User can perform administrative assignments.</summary>
    Admin,

    /// <summary>User can operate assigned sales.</summary>
    Operador,
}


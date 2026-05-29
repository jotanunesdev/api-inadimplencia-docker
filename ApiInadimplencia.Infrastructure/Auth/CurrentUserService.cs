using System.Security.Claims;
using ApiInadimplencia.Application.Abstractions.Auth;
using Microsoft.AspNetCore.Http;

namespace ApiInadimplencia.Infrastructure.Auth;

/// <summary>
/// Adapter que implementa ICurrentUserService usando IHttpContextAccessor.
/// Registrado como Scoped no DI.
///
/// Estratégia de identificação do usuário, em ordem:
///   1. <see cref="HttpContext.User"/> autenticado (caso um middleware real de
///      autenticação seja adicionado no futuro).
///   2. Fallback: header HTTP <c>X-Username</c> — convenção atual do projeto,
///      adotada também pelos endpoints de Configurações.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private const string UsernameHeader = "X-Username";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Username
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            if (httpContext.User?.Identity is { IsAuthenticated: true } identity
                && !string.IsNullOrWhiteSpace(identity.Name))
            {
                return identity.Name.Trim().ToLowerInvariant();
            }

            return GetUsernameFromHeader(httpContext);
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return false;
            }

            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(GetUsernameFromHeader(httpContext));
        }
    }

    private static string? GetUsernameFromHeader(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(UsernameHeader, out var values))
        {
            return null;
        }

        var raw = values.ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant();
    }
}

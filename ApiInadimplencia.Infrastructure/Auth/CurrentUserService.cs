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
///   2. Fallback: headers de identidade enviados pelo Fluig, priorizando
///      <c>X-Username</c>, <c>X-User-Code</c> e <c>X-User-Name</c>.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private static readonly string[] UsernameHeaders =
    [
        "X-Username",
        "X-User-Code",
        "X-User-Name",
    ];

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

            return GetUsernameFromHeaders(httpContext);
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

            return !string.IsNullOrWhiteSpace(GetUsernameFromHeaders(httpContext));
        }
    }

    private static string? GetUsernameFromHeaders(HttpContext httpContext)
    {
        foreach (var headerName in UsernameHeaders)
        {
            var raw = httpContext.Request.Headers[headerName].ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim().ToLowerInvariant();
            }
        }

        return null;
    }
}

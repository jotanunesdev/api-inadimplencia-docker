using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Infrastructure.Auth;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_inadimplencia.Api.Tests.Infrastructure;

public class ApiTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public virtual async Task ResetStateAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InadimplenciaDbContext>();
        var senhaRepository = scope.ServiceProvider.GetRequiredService<InMemorySenhaTransacaoRepository>();
        var serasaRepository = scope.ServiceProvider.GetRequiredService<InMemorySerasaPefinRepository>();
        var baixaRepository = scope.ServiceProvider.GetRequiredService<InMemorySerasaPefinBaixaRepository>();
        var protocoloGenerator = scope.ServiceProvider.GetRequiredService<InMemoryProtocoloGenerator>();
        var entraTokenRepository = scope.ServiceProvider.GetRequiredService<InMemoryEntraTokenRepository>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        senhaRepository.Clear();
        serasaRepository.Clear();
        baixaRepository.Clear();
        protocoloGenerator.Reset();
        entraTokenRepository.Clear();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Negativacao:UsuariosAprovadores:0"] = "aracy.mendoca",
                ["Negativacao:UsuariosAprovadores:1"] = "adriano.oliveira",
                ["Negativacao:UsuariosAprovadores:2"] = "gustavo.trindade",
                ["Negativacao:QuorumAprovacao"] = "1",
                ["Negativacao:DiasAtrasoMinimo"] = "60",
                ["Negativacao:MaxTentativasSenha"] = "3",
                ["Negativacao:LockoutMinutos"] = "15",
                ["Negativacao:JanelaTentativasMinutos"] = "5",
                ["SerasaPefin:Env"] = "uat",
                ["SerasaPefin:AuthUrl"] = "https://example.test/oauth/token",
                ["SerasaPefin:CollectionBaseUrl"] = "https://example.test/collection/debt/",
                ["SerasaPefin:ClientId"] = "test-client",
                ["SerasaPefin:ClientSecret"] = "test-secret",
                ["SerasaPefin:LogonVinculado"] = "test-logon",
                ["SerasaPefin:CnpjContrato"] = "62173620000180",
                ["SerasaPefin:UseUatDefaults"] = "true",
                ["SerasaPefin:CreditorDocument"] = "62173620000180",
                ["SerasaPefin:AreaInformante"] = "0001",
                ["SerasaPefin:TimeoutSeconds"] = "10",
                ["Auth:RequireAuthenticatedInadimplencia"] = "false",
                ["Auth:AuthServerBaseUrl"] = "http://localhost:3013",
                ["Auth:CredentialsEncryptionKey"] = "test-credential-key",
                ["Auth:EntraId:TenantId"] = "test-tenant",
                ["Auth:EntraId:ApiClientId"] = "test-api-client",
                ["Auth:EntraId:Audience"] = "api://test-api-client",
                ["Auth:EntraId:ClientId"] = "test-fluig-client",
                ["Auth:EntraId:Scope"] = "api://test-api-client/API.Access",
                ["Auth:EntraId:RedirectUris"] = "https://fluig.test/callback,https://oauth.pstmn.io/v1/browser-callback,http://localhost:5173,http://localhost:5173/home/configuracao"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInadimplenciaQueryService>();
            services.RemoveAll<ISenhaTransacaoRepository>();
            services.RemoveAll<ISerasaPefinRepository>();
            services.RemoveAll<ISerasaPefinBaixaRepository>();
            services.RemoveAll<IProtocoloGenerator>();
            services.RemoveAll<IAuthServerClient>();
            services.RemoveAll<IEntraIdAuthClient>();
            services.RemoveAll<IEntraTokenRepository>();
            services.RemoveAll<AuthOptions>();
            services.AddSingleton(new AuthOptions
            {
                AuthServerBaseUrl = "http://localhost:3013",
                CredentialsEncryptionKey = "test-credential-key",
                RequireAuthenticatedInadimplencia = false,
                EntraId = new EntraIdOptions
                {
                    TenantId = "test-tenant",
                    ApiClientId = "test-api-client",
                    Audience = "api://test-api-client",
                    ClientId = "test-fluig-client",
                    Scope = "api://test-api-client/API.Access",
                    RedirectUris =
                    [
                        "https://fluig.test/callback",
                        "https://oauth.pstmn.io/v1/browser-callback",
                        "http://localhost:5173",
                        "http://localhost:5173/home/configuracao"
                    ],
                },
            });
            services.AddSingleton<FakeEntraIdAuthClient>();
            services.AddSingleton<IAuthServerClient>(sp => sp.GetRequiredService<FakeEntraIdAuthClient>());
            services.AddSingleton<IEntraIdAuthClient>(sp => sp.GetRequiredService<FakeEntraIdAuthClient>());
            services.AddSingleton<InMemoryEntraTokenRepository>();
            services.AddSingleton<IEntraTokenRepository>(sp => sp.GetRequiredService<InMemoryEntraTokenRepository>());
            services.AddSingleton<IInadimplenciaQueryService, TestInadimplenciaQueryService>();
            services.AddSingleton<InMemorySenhaTransacaoRepository>();
            services.AddSingleton<InMemorySerasaPefinRepository>();
            services.AddSingleton<InMemorySerasaPefinBaixaRepository>();
            services.AddSingleton<InMemoryProtocoloGenerator>();
            services.AddSingleton<ISenhaTransacaoRepository>(sp => sp.GetRequiredService<InMemorySenhaTransacaoRepository>());
            services.AddSingleton<ISerasaPefinRepository>(sp => sp.GetRequiredService<InMemorySerasaPefinRepository>());
            services.AddSingleton<ISerasaPefinBaixaRepository>(sp => sp.GetRequiredService<InMemorySerasaPefinBaixaRepository>());
            services.AddSingleton<IProtocoloGenerator>(sp => sp.GetRequiredService<InMemoryProtocoloGenerator>());
        });
    }

    private sealed class FakeEntraIdAuthClient : IEntraIdAuthClient
    {
        public Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(new LoginResponse
            {
                AccessToken = "test-access-token",
                TokenType = "Bearer",
                ExpiresIn = 3600,
                RefreshToken = "test-refresh-token",
                Scope = "api://test-api-client/API.Access",
                User = new AuthUserDto(
                    username,
                    "Test User",
                    $"{username}@example.test",
                    null,
                    null,
                    null,
                    [],
                    ["API.Access", "inadimplencia:read", "inadimplencia:write"]),
            });

        public Task<AuthIdentity?> IntrospectAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult<AuthIdentity?>(new AuthIdentity(
                "test.user",
                "test.user@example.test",
                ["API.Access", "inadimplencia:read", "inadimplencia:write"],
                "entra-id",
                DateTimeOffset.UtcNow.AddHours(1)));

        public EntraAuthorizationUrl BuildAuthorizationUrl(
            string? redirectUri,
            string? state,
            string? codeChallenge,
            string? codeChallengeMethod,
            string? prompt)
        {
            var resolvedRedirectUri = NormalizeRedirectUri(redirectUri ?? "https://oauth.pstmn.io/v1/browser-callback");
            var authorizationUrl = "https://login.microsoftonline.com/test-tenant/oauth2/v2.0/authorize"
                + "?client_id=test-fluig-client"
                + "&response_type=code"
                + $"&redirect_uri={Uri.EscapeDataString(resolvedRedirectUri)}"
                + "&scope=api%3A%2F%2Ftest-api-client%2FAPI.Access"
                + (string.IsNullOrWhiteSpace(state) ? string.Empty : $"&state={Uri.EscapeDataString(state)}")
                + (string.IsNullOrWhiteSpace(codeChallenge) ? string.Empty : $"&code_challenge={Uri.EscapeDataString(codeChallenge)}");

            return new EntraAuthorizationUrl(
                authorizationUrl,
                "test-tenant",
                "test-fluig-client",
                "api://test-api-client/API.Access",
                resolvedRedirectUri);
        }

        public Task<LoginResponse> ExchangeAuthorizationCodeAsync(
            string code,
            string redirectUri,
            string? codeVerifier,
            CancellationToken cancellationToken = default)
            => LoginAsync("test.user", "from-code", cancellationToken);

        public Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => LoginAsync("test.user", "from-refresh", cancellationToken);

        private static string NormalizeRedirectUri(string redirectUri)
        {
            if (!Uri.TryCreate(redirectUri.Trim(), UriKind.Absolute, out var uri))
            {
                return redirectUri.Trim().TrimEnd('/');
            }

            var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? uri.GetLeftPart(UriPartial.Authority) : normalized;
        }
    }

    public sealed class InMemoryEntraTokenRepository : IEntraTokenRepository
    {
        private readonly ConcurrentDictionary<string, StoredEntraToken> _tokens = new(StringComparer.OrdinalIgnoreCase);
        private int _nextId;

        public bool IsConfigured => true;

        public Task<StoredEntraToken?> FindByOwnerKeyAsync(string ownerKey, CancellationToken cancellationToken = default)
        {
            _tokens.TryGetValue(NormalizeOwnerKey(ownerKey), out var token);
            return Task.FromResult(token);
        }

        public Task<StoredEntraToken> UpsertAsync(
            string ownerKey,
            string? fluigUserName,
            string? fluigUserCode,
            string subject,
            EncryptedSecret encryptedRefreshToken,
            CancellationToken cancellationToken = default)
        {
            var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
            var now = DateTime.UtcNow;
            var token = _tokens.AddOrUpdate(
                normalizedOwnerKey,
                _ => new StoredEntraToken(
                    Interlocked.Increment(ref _nextId),
                    normalizedOwnerKey,
                    fluigUserName,
                    fluigUserCode,
                    subject,
                    encryptedRefreshToken.CipherText,
                    encryptedRefreshToken.Iv,
                    encryptedRefreshToken.Tag,
                    now,
                    now,
                    null),
                (_, current) => current with
                {
                    FluigUserName = fluigUserName,
                    FluigUserCode = fluigUserCode,
                    Subject = subject,
                    RefreshTokenCipher = encryptedRefreshToken.CipherText,
                    RefreshTokenIv = encryptedRefreshToken.Iv,
                    RefreshTokenTag = encryptedRefreshToken.Tag,
                    UpdatedAt = now,
                });

            return Task.FromResult(token);
        }

        public Task MarkLastLoginAsync(string ownerKey, CancellationToken cancellationToken = default)
        {
            var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
            if (_tokens.TryGetValue(normalizedOwnerKey, out var token))
            {
                _tokens[normalizedOwnerKey] = token with
                {
                    UpdatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                };
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string ownerKey, CancellationToken cancellationToken = default)
        {
            _tokens.TryRemove(NormalizeOwnerKey(ownerKey), out _);
            return Task.CompletedTask;
        }

        public void Clear()
        {
            _tokens.Clear();
            _nextId = 0;
        }

        private static string NormalizeOwnerKey(string? value)
            => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}

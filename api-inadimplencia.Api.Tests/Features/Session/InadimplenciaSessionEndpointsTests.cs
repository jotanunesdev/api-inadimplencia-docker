using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.Features.Session;

public class InadimplenciaSessionEndpointsTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public InadimplenciaSessionEndpointsTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CredentialKey_ReturnsTransportPublicKey()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/session/credential-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("RSA-OAEP-256", document.RootElement.GetProperty("algorithm").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("keyId").GetString()));
        Assert.Contains("BEGIN PUBLIC KEY", document.RootElement.GetProperty("publicKey").GetString());
    }

    [Fact]
    public async Task EntraLogin_WithCredentials_ReturnsAccessTokenAndScopes()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/inadimplencia/session/entra/login", new
        {
            username = "test.user",
            password = "password",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.GetProperty("authenticated").GetBoolean());
        Assert.Equal("test-access-token", root.GetProperty("accessToken").GetString());
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Equal("api://test-api-client/API.Access", root.GetProperty("scope").GetString());
        Assert.Contains(root.GetProperty("scopes").EnumerateArray(), scope => scope.GetString() == "inadimplencia:read");
    }

    [Fact]
    public async Task EntraLogin_WithoutCredentials_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/inadimplencia/session/entra/login", new
        {
            username = "",
            password = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("ENTRA_CREDENTIALS_REQUIRED", json);
    }

    [Fact]
    public async Task EntraAuthorizeUrl_ReturnsConfiguredAuthorizationUrl()
    {
        var client = _factory.CreateClient();
        var redirectUri = Uri.EscapeDataString("https://oauth.pstmn.io/v1/browser-callback");

        var response = await client.GetAsync($"/inadimplencia/session/entra/authorize-url?redirectUri={redirectUri}&state=test-state&codeChallenge=test-challenge");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var authorizationUrl = root.GetProperty("authorizationUrl").GetString();

        Assert.Equal("test-tenant", root.GetProperty("tenantId").GetString());
        Assert.Equal("test-fluig-client", root.GetProperty("clientId").GetString());
        Assert.Equal("api://test-api-client/API.Access", root.GetProperty("scope").GetString());
        Assert.Equal("https://oauth.pstmn.io/v1/browser-callback", root.GetProperty("redirectUri").GetString());
        Assert.Contains("https://login.microsoftonline.com/test-tenant/oauth2/v2.0/authorize", authorizationUrl);
        Assert.Contains("client_id=test-fluig-client", authorizationUrl);
        Assert.Contains("code_challenge=test-challenge", authorizationUrl);
    }

    [Fact]
    public async Task EntraAuthorizeUrl_WithLocalhostRootRedirectUri_NormalizesTrailingSlash()
    {
        var client = _factory.CreateClient();
        var redirectUri = Uri.EscapeDataString("http://localhost:5173/");

        var response = await client.GetAsync($"/inadimplencia/session/entra/authorize-url?redirectUri={redirectUri}&state=test-state&codeChallenge=test-challenge");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var authorizationUrl = root.GetProperty("authorizationUrl").GetString();

        Assert.Equal("http://localhost:5173", root.GetProperty("redirectUri").GetString());
        Assert.Contains($"redirect_uri={Uri.EscapeDataString("http://localhost:5173")}", authorizationUrl);
    }

    [Fact]
    public async Task EntraAuthorizeUrl_WithLocalhostRouteRedirectUri_ReturnsConfiguredAuthorizationUrl()
    {
        var client = _factory.CreateClient();
        var redirectUri = Uri.EscapeDataString("http://localhost:5173/home/configuracao");

        var response = await client.GetAsync($"/inadimplencia/session/entra/authorize-url?redirectUri={redirectUri}&state=test-state&codeChallenge=test-challenge");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var authorizationUrl = root.GetProperty("authorizationUrl").GetString();

        Assert.Equal("http://localhost:5173/home/configuracao", root.GetProperty("redirectUri").GetString());
        Assert.Contains($"redirect_uri={Uri.EscapeDataString("http://localhost:5173/home/configuracao")}", authorizationUrl);
    }

    [Fact]
    public async Task EntraToken_WithAuthorizationCode_ReturnsAccessTokenAndScopes()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/inadimplencia/session/entra/token")
        {
            Content = JsonContent.Create(new
            {
                code = "auth-code",
                redirectUri = "https://oauth.pstmn.io/v1/browser-callback",
                codeVerifier = "code-verifier",
            }),
        };
        request.Headers.Add("X-User-Code", "fluig.token.user");

        var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.GetProperty("authenticated").GetBoolean());
        Assert.True(root.GetProperty("credentialsRegistered").GetBoolean());
        Assert.Equal("test-access-token", root.GetProperty("accessToken").GetString());
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Contains(root.GetProperty("scopes").EnumerateArray(), scope => scope.GetString() == "inadimplencia:write");
    }

    [Fact]
    public async Task Bootstrap_WithStoredEntraRefreshToken_ReturnsAccessTokenAndScopes()
    {
        var client = _factory.CreateClient();
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/inadimplencia/session/entra/token")
        {
            Content = JsonContent.Create(new
            {
                code = "auth-code",
                redirectUri = "https://oauth.pstmn.io/v1/browser-callback",
                codeVerifier = "code-verifier",
            }),
        };
        tokenRequest.Headers.Add("X-User-Code", "fluig.bootstrap.user");

        var tokenResponse = await client.SendAsync(tokenRequest);
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, await tokenResponse.Content.ReadAsStringAsync());

        using var bootstrapRequest = new HttpRequestMessage(HttpMethod.Post, "/inadimplencia/session/bootstrap");
        bootstrapRequest.Headers.Add("X-User-Code", "fluig.bootstrap.user");

        var response = await client.SendAsync(bootstrapRequest);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.GetProperty("authenticated").GetBoolean());
        Assert.True(root.GetProperty("credentialsRegistered").GetBoolean());
        Assert.Equal("test-access-token", root.GetProperty("accessToken").GetString());
        Assert.Contains(root.GetProperty("scopes").EnumerateArray(), scope => scope.GetString() == "inadimplencia:read");
    }

    [Fact]
    public async Task Status_WithoutFluigOwner_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/inadimplencia/session/status");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("FLUIG_USER_MISSING", json);
    }

    [Fact]
    public async Task Status_WithFluigOwnerAndNoCredentials_ReturnsAnonymousStatus()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/inadimplencia/session/status");
        request.Headers.Add("X-User-Code", "fluig.user");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("authenticated").GetBoolean());
        Assert.False(document.RootElement.GetProperty("credentialsRegistered").GetBoolean());
    }
}

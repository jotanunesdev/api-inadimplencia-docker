using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;

/// <summary>
/// HttpClient tipado para integração com Serasa PEFIN
/// </summary>
public class SerasaPefinClient
{
    private readonly HttpClient _httpClient;
    private readonly SerasaPefinOptions _options;
    private readonly ILogger<SerasaPefinClient> _logger;

    public SerasaPefinClient(
        HttpClient httpClient,
        IOptions<SerasaPefinOptions> options,
        ILogger<SerasaPefinClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    /// Obtém token de autenticação via Basic Auth
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        // AuthUrl is the full login endpoint URL
        var response = await _httpClient.PostAsync(_options.AuthUrl, null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<SerasaTokenResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return tokenResponse?.AccessToken ?? throw new InvalidOperationException("Failed to obtain access token");
    }

    /// <summary>
    /// POST para inclusão de dívida principal
    /// </summary>
    public async Task<SerasaInclusionResponse> PostMainDebtAsync(object payload, string token, CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = $"{_options.CollectionBaseUrl.TrimEnd('/')}/debt/";
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Serasa PEFIN main debt inclusion failed. Status: {StatusCode}, Body: {Body}", (int)response.StatusCode, body);
            throw new ApiInadimplencia.Application.Abstractions.Integrations.SerasaPefinHttpException((int)response.StatusCode, body, $"Serasa PEFIN main debt inclusion failed with status {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<SerasaInclusionResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result ?? throw new InvalidOperationException("Failed to deserialize inclusion response");
    }

    /// <summary>
    /// POST para inclusão de dívida de avalista
    /// </summary>
    public async Task<SerasaInclusionResponse> PostGuarantorAsync(object payload, string token, CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = $"{_options.CollectionBaseUrl.TrimEnd('/')}/debt/guarantor";
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Serasa PEFIN guarantor inclusion failed. Status: {StatusCode}, Body: {Body}", (int)response.StatusCode, body);
            throw new ApiInadimplencia.Application.Abstractions.Integrations.SerasaPefinHttpException((int)response.StatusCode, body, $"Serasa PEFIN guarantor inclusion failed with status {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<SerasaInclusionResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result ?? throw new InvalidOperationException("Failed to deserialize inclusion response");
    }

    private record SerasaTokenResponse(string AccessToken, string TokenType, string ExpiresIn);
}

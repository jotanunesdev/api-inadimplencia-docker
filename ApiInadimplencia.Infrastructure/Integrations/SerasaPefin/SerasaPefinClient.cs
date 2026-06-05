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
    /// Obtém token de autenticação via Basic Auth.
    /// Cria HttpRequestMessage isolada (sem mutar DefaultRequestHeaders) para evitar
    /// vazamento de Authorization/Accept entre chamadas concorrentes.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.AuthUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Serasa PEFIN login failed. Status: {StatusCode}, Body: {Body}", (int)response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }

        var tokenResponse = JsonSerializer.Deserialize<SerasaTokenResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return tokenResponse?.AccessToken ?? throw new InvalidOperationException("Failed to obtain access token");
    }

    /// <summary>
    /// POST para inclusão de dívida principal.
    /// Usa HttpRequestMessage isolada para garantir que apenas Authorization Bearer e
    /// content-type/Accept previstos na documentação Serasa sejam enviados.
    /// </summary>
    public async Task<SerasaInclusionResponse> PostMainDebtAsync(object payload, string token, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{_options.CollectionBaseUrl.TrimEnd('/')}/debt/";
        return await PostInclusionAsync(endpoint, payload, token, "main debt", cancellationToken);
    }

    /// <summary>
    /// POST para inclusão de dívida de avalista.
    /// </summary>
    public async Task<SerasaInclusionResponse> PostGuarantorAsync(object payload, string token, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{_options.CollectionBaseUrl.TrimEnd('/')}/debt/guarantor";
        return await PostInclusionAsync(endpoint, payload, token, "guarantor", cancellationToken);
    }

    private async Task<SerasaInclusionResponse> PostInclusionAsync(
        string endpoint,
        object payload,
        string token,
        string label,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Serasa.Http.Sending {Label} POST {Endpoint} | TokenLen={TokenLen} | Body={Body}",
            label, endpoint, token?.Length ?? 0, json);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("Serasa.Http.Response {Label} Status={StatusCode} Body={Body}",
            label, (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Serasa PEFIN {Label} inclusion failed. Status: {StatusCode}, Body: {Body}", label, (int)response.StatusCode, responseContent);
            throw new ApiInadimplencia.Application.Abstractions.Integrations.SerasaPefinHttpException((int)response.StatusCode, responseContent, $"Serasa PEFIN {label} inclusion failed with status {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<SerasaInclusionResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result ?? throw new InvalidOperationException("Failed to deserialize inclusion response");
    }

    /// <summary>
    /// DELETE para baixa de dívida via header <c>contract-number</c>.
    /// Endpoint: <c>{CollectionBaseUrl}/debt/contract</c>.
    /// Body vazio; identificação totalmente via headers conforme contrato Serasa v8.
    /// </summary>
    public async Task<SerasaBaixaResponse> DeleteByContractAsync(
        SerasaBaixaRequest request,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CreditorDocument))
        {
            throw new ArgumentException("creditor-document is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DebtorDocument))
        {
            throw new ArgumentException("debtor-document is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ContractNumber))
        {
            throw new ArgumentException("contract-number is required.", nameof(request));
        }

        var endpoint = $"{_options.CollectionBaseUrl.TrimEnd('/')}/debt/contract";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.Add("creditor-document", request.CreditorDocument);
        httpRequest.Headers.Add("debtor-document", request.DebtorDocument);
        httpRequest.Headers.Add("contract-number", request.ContractNumber);
        httpRequest.Headers.Add("reason", request.Reason.ToString(System.Globalization.CultureInfo.InvariantCulture));
        httpRequest.Headers.Add("type", "PEFIN");

        _logger.LogInformation(
            "Serasa.Http.Sending baixa DELETE {Endpoint} | TokenLen={TokenLen} | Contract={Contract} | Reason={Reason}",
            endpoint,
            token?.Length ?? 0,
            request.ContractNumber,
            request.Reason);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "Serasa.Http.Response baixa DELETE Status={StatusCode} Body={Body}",
            (int)response.StatusCode,
            responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Serasa PEFIN baixa DELETE failed. Status: {StatusCode}, Body: {Body}",
                (int)response.StatusCode,
                responseContent);
            throw new ApiInadimplencia.Application.Abstractions.Integrations.SerasaPefinHttpException(
                (int)response.StatusCode,
                responseContent,
                $"Serasa PEFIN baixa DELETE failed with status {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<SerasaBaixaResponse>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result is null || string.IsNullOrWhiteSpace(result.TransactionId))
        {
            throw new InvalidOperationException("Failed to deserialize Serasa baixa response (missing transactionId).");
        }

        return result;
    }

    private record SerasaTokenResponse(string AccessToken, string TokenType, string ExpiresIn);
}

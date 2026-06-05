using ApiInadimplencia.Application.Abstractions.Integrations;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;

/// <summary>
/// Implementação do gateway Serasa PEFIN com cache de token e retry
/// </summary>
public class SerasaPefinGateway : ISerasaPefinGateway
{
    private readonly SerasaPefinClient _client;
    private readonly SerasaPefinTokenCache _tokenCache;
    private readonly ILogger<SerasaPefinGateway> _logger;

    public SerasaPefinGateway(
        SerasaPefinClient client,
        SerasaPefinTokenCache tokenCache,
        ILogger<SerasaPefinGateway> logger)
    {
        _client = client;
        _tokenCache = tokenCache;
        _logger = logger;
    }

    /// <summary>
    /// Obtém token de acesso com cache
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var cachedToken = _tokenCache.GetToken();
        if (cachedToken != null)
        {
            _logger.LogDebug("Using cached Serasa PEFIN token");
            return cachedToken;
        }

        _logger.LogInformation("Obtaining new Serasa PEFIN token");
        var token = await _client.GetTokenAsync(cancellationToken);
        
        // Assume token expires in 1 hour (3600 seconds) as per Serasa PEFIN spec
        _tokenCache.SetToken(token, TimeSpan.FromSeconds(3600));
        
        return token;
    }

    /// <summary>
    /// POST para inclusão de dívida principal com retry em 401
    /// </summary>
    public async Task<SerasaInclusionResponse> PostMainDebtAsync(object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetTokenAsync(cancellationToken);
            
            // Mask sensitive data in logs
            var maskedRequest = new
            {
                payload
            };
            
            _logger.LogInformation("Requesting Serasa PEFIN main debt inclusion: {Request}", JsonSerializer.Serialize(maskedRequest));
            
            var response = await _client.PostMainDebtAsync(payload, token, cancellationToken);
            _logger.LogInformation("Serasa PEFIN main debt inclusion successful. TransactionId: {TransactionId}", response.TransactionId);
            
            return response;
        }
        catch (SerasaPefinHttpException ex) when (ex.StatusCode == 401)
        {
            _logger.LogWarning("Unauthorized request, retrying with new token");
            _tokenCache.Clear();
            var token = await GetTokenAsync(cancellationToken);
            return await _client.PostMainDebtAsync(payload, token, cancellationToken);
        }
    }

    /// <summary>
    /// POST para inclusão de dívida de avalista com retry em 401
    /// </summary>
    public async Task<SerasaInclusionResponse> PostGuarantorAsync(object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetTokenAsync(cancellationToken);
            
            // Mask sensitive data in logs
            var maskedRequest = new
            {
                payload
            };
            
            _logger.LogInformation("Requesting Serasa PEFIN guarantor inclusion: {Request}", JsonSerializer.Serialize(maskedRequest));
            
            var response = await _client.PostGuarantorAsync(payload, token, cancellationToken);
            _logger.LogInformation("Serasa PEFIN guarantor inclusion successful. TransactionId: {TransactionId}", response.TransactionId);
            
            return response;
        }
        catch (SerasaPefinHttpException ex) when (ex.StatusCode == 401)
        {
            _logger.LogWarning("Unauthorized request, retrying with new token");
            _tokenCache.Clear();
            var token = await GetTokenAsync(cancellationToken);
            return await _client.PostGuarantorAsync(payload, token, cancellationToken);
        }
    }

    /// <summary>
    /// DELETE para baixa de dívida por contrato (header), com retry em 401.
    /// </summary>
    public async Task<SerasaBaixaResponse> DeleteByContractAsync(
        SerasaBaixaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var token = await GetTokenAsync(cancellationToken);

            _logger.LogInformation(
                "Requesting Serasa PEFIN baixa DELETE by contract: Contract={Contract}, Reason={Reason}, Creditor={Creditor}, Debtor={Debtor}",
                request.ContractNumber,
                request.Reason,
                MaskDocument(request.CreditorDocument),
                MaskDocument(request.DebtorDocument));

            var response = await _client.DeleteByContractAsync(request, token, cancellationToken);
            _logger.LogInformation(
                "Serasa PEFIN baixa DELETE successful. TransactionId: {TransactionId}",
                response.TransactionId);

            return response;
        }
        catch (SerasaPefinHttpException ex) when (ex.StatusCode == 401)
        {
            _logger.LogWarning("Unauthorized baixa request, retrying with new token");
            _tokenCache.Clear();
            var token = await GetTokenAsync(cancellationToken);
            return await _client.DeleteByContractAsync(request, token, cancellationToken);
        }
    }

    private static string MaskDocument(string? document)
    {
        if (string.IsNullOrEmpty(document))
        {
            return "***";
        }

        if (document.Length <= 4)
        {
            return new string('*', document.Length);
        }

        return $"***{document[^4..]}";
    }
}

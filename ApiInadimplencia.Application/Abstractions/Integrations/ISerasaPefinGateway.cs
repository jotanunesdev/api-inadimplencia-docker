using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Application.Abstractions.Integrations;

/// <summary>
/// Gateway para integração com Serasa Experian PEFIN
/// </summary>
public interface ISerasaPefinGateway
{
    /// <summary>
    /// Obtém token de acesso para API Serasa PEFIN
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Bearer token</returns>
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST para inclusão de dívida principal.
    /// </summary>
    Task<SerasaInclusionResponse> PostMainDebtAsync(object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST para inclusão de dívida de avalista.
    /// </summary>
    Task<SerasaInclusionResponse> PostGuarantorAsync(object payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from Serasa PEFIN inclusion endpoint.
/// </summary>
public sealed record SerasaInclusionResponse(
    string TransactionId,
    string Status);

/// <summary>
/// Exception thrown when Serasa PEFIN HTTP request fails with details for troubleshooting.
/// </summary>
public sealed class SerasaPefinHttpException : Exception
{
    /// <summary>HTTP status code returned by Serasa.</summary>
    public int StatusCode { get; }

    /// <summary>Response body from Serasa (preserved for troubleshooting).</summary>
    public string Body { get; }

    public SerasaPefinHttpException(int statusCode, string body, string message) : base(message)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public SerasaPefinHttpException(int statusCode, string body, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
        Body = body;
    }
}

/// <summary>
/// DTO para solicitação de negativação Serasa PEFIN
/// </summary>
public record SerasaPefinRequest(
    int NumVenda,
    SerasaPefinRecordType TipoRegistro,
    decimal Valor,
    DateTime DataVencimento,
    string NumeroContrato,
    string AreaInformante,
    string DocumentoDevedor,
    string DocumentoCredor,
    string EnderecoDevedor,
    string? DocumentoGarantidor = null,
    string? EnderecoGarantidor = null);


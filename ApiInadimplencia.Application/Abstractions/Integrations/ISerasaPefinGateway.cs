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

    /// <summary>
    /// DELETE para baixa de dívida por contract-number (header).
    /// Endpoint: <c>{CollectionBaseUrl}/debt/contract</c>.
    /// Retorna o <c>transactionId</c> retornado pela Serasa para correlação com webhook.
    /// </summary>
    Task<SerasaBaixaResponse> DeleteByContractAsync(SerasaBaixaRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from Serasa PEFIN inclusion endpoint.
/// </summary>
public sealed record SerasaInclusionResponse(
    string TransactionId,
    string Status);

/// <summary>
/// Request para baixa de dívida via DELETE por <c>contract-number</c>.
/// </summary>
/// <param name="CreditorDocument">CNPJ do credor (somente dígitos).</param>
/// <param name="DebtorDocument">CPF/CNPJ do devedor (somente dígitos).</param>
/// <param name="ContractNumber">Número do contrato a ser baixado.</param>
/// <param name="Reason">Código numérico do motivo da baixa (whitelist Serasa).</param>
public sealed record SerasaBaixaRequest(
    string CreditorDocument,
    string DebtorDocument,
    string ContractNumber,
    int Reason);

/// <summary>
/// Response do DELETE de baixa: a Serasa retorna apenas o <c>transactionId</c>;
/// o resultado final (sucesso/erro) chega via webhook.
/// </summary>
public sealed record SerasaBaixaResponse(string TransactionId);

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


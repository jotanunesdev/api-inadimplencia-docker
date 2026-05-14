using System.Text.Json.Serialization;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Webhooks;

/// <summary>
/// DTO representing the payload received from Serasa PEFIN webhooks.
/// Based on Serasa documentation v8 - Payload de Resposta (Inclusão/Exclusão).
/// </summary>
public sealed class SerasaWebhookPayload
{
    /// <summary>
    /// UUID from Serasa (same as transactionId from initial request).
    /// Used for idempotency checks.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>
    /// Debtor document (CPF/CNPJ).
    /// </summary>
    [JsonPropertyName("debtorDocument")]
    public string? DebtorDocument { get; set; }

    /// <summary>
    /// Creditor document (CNPJ).
    /// </summary>
    [JsonPropertyName("creditorDocument")]
    public string? CreditorDocument { get; set; }

    /// <summary>
    /// Contract number.
    /// </summary>
    [JsonPropertyName("contract")]
    public string? Contract { get; set; }

    /// <summary>
    /// Debt value.
    /// </summary>
    [JsonPropertyName("debtValue")]
    public double? DebtValue { get; set; }

    /// <summary>
    /// Debt date (yyyy-MM-dd).
    /// </summary>
    [JsonPropertyName("debtDate")]
    public string? DebtDate { get; set; }

    /// <summary>
    /// CADUS key returned on success.
    /// </summary>
    [JsonPropertyName("cadusKey")]
    public string? CadusKey { get; set; }

    /// <summary>
    /// CADUS series returned on success.
    /// </summary>
    [JsonPropertyName("cadusSerie")]
    public string? CadusSerie { get; set; }

    /// <summary>
    /// Debt type (PEFIN).
    /// </summary>
    [JsonPropertyName("debtType")]
    public string? DebtType { get; set; }

    /// <summary>
    /// Creditor area.
    /// </summary>
    [JsonPropertyName("creditorArea")]
    public string? CreditorArea { get; set; }

    /// <summary>
    /// Category ID.
    /// </summary>
    [JsonPropertyName("categoryId")]
    public string? CategoryId { get; set; }

    /// <summary>
    /// Error details (present only when operation fails).
    /// </summary>
    [JsonPropertyName("error")]
    public SerasaWebhookError? Error { get; set; }

    /// <summary>
    /// Creditor information (exclusion payload).
    /// </summary>
    [JsonPropertyName("creditor")]
    public SerasaWebhookCreditor? Creditor { get; set; }

    /// <summary>
    /// Debtor information (exclusion payload).
    /// </summary>
    [JsonPropertyName("debtor")]
    public SerasaWebhookDebtor? Debtor { get; set; }

    /// <summary>
    /// Write-off information (exclusion payload).
    /// </summary>
    [JsonPropertyName("writeOff")]
    public SerasaWebhookWriteOff? WriteOff { get; set; }
}

/// <summary>
/// Error details from Serasa webhook.
/// </summary>
public sealed class SerasaWebhookError
{
    /// <summary>
    /// Error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }
}

/// <summary>
/// Creditor information (exclusion payload).
/// </summary>
public sealed class SerasaWebhookCreditor
{
    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }
}

/// <summary>
/// Debtor information (exclusion payload).
/// </summary>
public sealed class SerasaWebhookDebtor
{
    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }
}

/// <summary>
/// Write-off information (exclusion payload).
/// </summary>
public sealed class SerasaWebhookWriteOff
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

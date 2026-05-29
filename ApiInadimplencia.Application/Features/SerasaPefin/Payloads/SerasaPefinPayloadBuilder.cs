using ApiInadimplencia.Domain.SerasaPefin;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Payloads;

/// <summary>
/// Builds and validates the JSON payloads sent to Serasa for inclusion (debt and guarantor),
/// matching the Node reference <c>serasaPefinPayloadBuilder.js</c>.
/// </summary>
public sealed class SerasaPefinPayloadBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null, // use property names as declared (camelCase-like mirroring Node JSON)
    };

    /// <summary>
    /// Options controlling Serasa PEFIN request validation.
    /// </summary>
    public sealed record Options(bool UatEnabled, string AreaInformante, string CategoryId);

    /// <summary>
    /// Validates input and builds the main debt inclusion payload.
    /// </summary>
    /// <exception cref="SerasaPefinValidationException">When input is invalid.</exception>
    public (object Payload, string PayloadJson) BuildMainDebt(MainDebtInput input, Options options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        var missing = ValidateMainDebtInput(input);
        if (missing.Count > 0)
        {
            throw SerasaPefinValidationException.ForMissingFields(missing);
        }

        ValidateUatDocuments(options, input.DebtorDocument, input.CreditorDocument);

        var payload = new Dictionary<string, object?>
        {
            ["value"] = Math.Round(input.Value, SerasaPefinConstants.ValueDecimals, MidpointRounding.AwayFromZero),
            ["areaInformante"] = options.AreaInformante,
            ["dueDate"] = input.DueDate.ToString("yyyy-MM-dd"),
            ["categoryId"] = string.IsNullOrWhiteSpace(input.CategoryId) ? options.CategoryId : input.CategoryId,
            ["debtor"] = new Dictionary<string, object?>
            {
                ["documentNumber"] = SerasaPefinConstants.DigitsOnly(input.DebtorDocument),
                ["name"] = input.DebtorName?.Trim() ?? string.Empty,
                ["address"] = BuildAddress(input.DebtorAddress!),
            },
            ["creditor"] = new Dictionary<string, object?>
            {
                ["documentNumber"] = SerasaPefinConstants.DigitsOnly(input.CreditorDocument),
            },
            ["contractNumber"] = $"{input.ContractNumber.Trim()}-P{input.Parcela.Numero}",
            ["debtType"] = SerasaPefinConstants.DebtType,
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return (payload, json);
    }

    /// <summary>
    /// Validates input and builds the guarantor inclusion payload.
    /// </summary>
    /// <exception cref="SerasaPefinValidationException">When input is invalid.</exception>
    public (object Payload, string PayloadJson) BuildGuarantor(GuarantorInput input, Options options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        var missing = ValidateGuarantorInput(input);
        if (missing.Count > 0)
        {
            throw SerasaPefinValidationException.ForMissingFields(missing);
        }

        ValidateUatDocuments(options, input.DebtorDocument, input.CreditorDocument, input.GuarantorDocument);

        var payload = new Dictionary<string, object?>
        {
            ["categoryId"] = string.IsNullOrWhiteSpace(input.CategoryId) ? options.CategoryId : input.CategoryId,
            ["value"] = Math.Round(input.Value, SerasaPefinConstants.ValueDecimals, MidpointRounding.AwayFromZero),
            ["dueDate"] = input.DueDate.ToString("yyyy-MM-dd"),
            ["debtorDocument"] = SerasaPefinConstants.DigitsOnly(input.DebtorDocument),
            ["contractNumber"] = $"{input.ContractNumber.Trim()}-P{input.Parcela.Numero}",
            ["guarantor"] = new Dictionary<string, object?>
            {
                ["documentNumber"] = SerasaPefinConstants.DigitsOnly(input.GuarantorDocument),
                ["name"] = input.GuarantorName?.Trim() ?? string.Empty,
                ["address"] = BuildAddress(input.GuarantorAddress!),
            },
            ["creditor"] = new Dictionary<string, object?>
            {
                ["documentNumber"] = SerasaPefinConstants.DigitsOnly(input.CreditorDocument),
            },
            ["debtType"] = SerasaPefinConstants.DebtType,
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return (payload, json);
    }

    /// <summary>
    /// Masks a CPF/CNPJ to first 2-3 digits and last 2 digits, preserving length semantics.
    /// </summary>
    public static string MaskDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return string.Empty;
        }

        var digits = SerasaPefinConstants.DigitsOnly(document);
        if (digits.Length <= 3)
        {
            return "***";
        }

        return digits.Length <= 11
            ? $"{digits[..3]}.***.{digits[^2..]}"
            : $"{digits[..2]}.***.{digits[^2..]}";
    }

    /// <summary>
    /// Returns a new payload dictionary with sensitive documents masked, suitable for storage as auditoria.
    /// </summary>
    public Dictionary<string, object?> MaskPayloadForAudit(Dictionary<string, object?> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Shallow-clone + mask known document fields.
        var masked = new Dictionary<string, object?>(payload);

        if (masked.TryGetValue("debtor", out var debtorObj) && debtorObj is Dictionary<string, object?> debtor)
        {
            masked["debtor"] = MaskDocumentInObject(debtor, "documentNumber");
        }

        if (masked.TryGetValue("guarantor", out var guarantorObj) && guarantorObj is Dictionary<string, object?> guarantor)
        {
            masked["guarantor"] = MaskDocumentInObject(guarantor, "documentNumber");
        }

        if (masked.TryGetValue("creditor", out var creditorObj) && creditorObj is Dictionary<string, object?> creditor)
        {
            masked["creditor"] = MaskDocumentInObject(creditor, "documentNumber");
        }

        if (masked.TryGetValue("debtorDocument", out _))
        {
            masked["debtorDocument"] = MaskDocument(masked["debtorDocument"]?.ToString());
        }

        return masked;
    }

    /// <summary>
    /// Serializes the masked payload as compact JSON (for persistence in PAYLOAD_AUDITORIA).
    /// </summary>
    public string SerializeMasked(Dictionary<string, object?> payload)
    {
        var masked = MaskPayloadForAudit(payload);
        return JsonSerializer.Serialize(masked, SerializerOptions);
    }

    private static Dictionary<string, object?> MaskDocumentInObject(Dictionary<string, object?> source, string key)
    {
        var clone = new Dictionary<string, object?>(source);
        if (clone.TryGetValue(key, out var value) && value is string str)
        {
            clone[key] = MaskDocument(str);
        }

        return clone;
    }

    private static Dictionary<string, object?> BuildAddress(SerasaAddress address)
    {
        var normalized = new Dictionary<string, object?>
        {
            ["zipCode"] = SerasaPefinConstants.DigitsOnly(address.ZipCode),
            ["addressLine"] = address.AddressLine.Trim(),
            ["district"] = address.District.Trim(),
            ["city"] = address.City.Trim(),
            ["state"] = address.State.Trim(),
        };

        if (!string.IsNullOrWhiteSpace(address.Complement))
        {
            normalized["complement"] = address.Complement.Trim();
        }

        if (!string.IsNullOrWhiteSpace(address.Number))
        {
            normalized["number"] = address.Number.Trim();
        }

        return normalized;
    }

    private static List<string> ValidateMainDebtInput(MainDebtInput input)
    {
        var missing = new List<string>();

        if (input.Value < SerasaPefinConstants.MinValue)
        {
            missing.Add("value");
        }

        if (input.DueDate == default)
        {
            missing.Add("dueDate");
        }

        if (string.IsNullOrWhiteSpace(input.ContractNumber))
        {
            missing.Add("contractNumber");
        }

        if (string.IsNullOrWhiteSpace(input.DebtorDocument))
        {
            missing.Add("debtor.documentNumber");
        }

        if (string.IsNullOrWhiteSpace(input.CreditorDocument))
        {
            missing.Add("creditor.documentNumber");
        }

        missing.AddRange(ValidateAddressMissing(input.DebtorAddress, "debtor.address."));
        return missing;
    }

    private static List<string> ValidateGuarantorInput(GuarantorInput input)
    {
        var missing = new List<string>();

        if (input.Value < SerasaPefinConstants.MinValue)
        {
            missing.Add("value");
        }

        if (input.DueDate == default)
        {
            missing.Add("dueDate");
        }

        if (string.IsNullOrWhiteSpace(input.ContractNumber))
        {
            missing.Add("contractNumber");
        }

        if (string.IsNullOrWhiteSpace(input.DebtorDocument))
        {
            missing.Add("debtorDocument");
        }

        if (string.IsNullOrWhiteSpace(input.CreditorDocument))
        {
            missing.Add("creditor.documentNumber");
        }

        if (string.IsNullOrWhiteSpace(input.GuarantorDocument))
        {
            missing.Add("guarantor.documentNumber");
        }

        missing.AddRange(ValidateAddressMissing(input.GuarantorAddress, "guarantor.address."));
        return missing;
    }

    private static IEnumerable<string> ValidateAddressMissing(SerasaAddress? address, string prefix)
    {
        if (address is null)
        {
            yield return $"{prefix}address";
            yield break;
        }

        if (string.IsNullOrWhiteSpace(address.ZipCode))
        {
            yield return $"{prefix}zipCode";
        }

        if (string.IsNullOrWhiteSpace(address.AddressLine))
        {
            yield return $"{prefix}addressLine";
        }

        if (string.IsNullOrWhiteSpace(address.District))
        {
            yield return $"{prefix}district";
        }

        if (string.IsNullOrWhiteSpace(address.City))
        {
            yield return $"{prefix}city";
        }

        if (string.IsNullOrWhiteSpace(address.State))
        {
            yield return $"{prefix}state";
        }
    }

    private static void ValidateUatDocuments(Options options, params string?[] documents)
    {
        var blocked = GetBlockedUatDocuments(options, documents);
        if (blocked.Count > 0)
        {
            throw SerasaPefinValidationException.ForBlockedDocuments(blocked);
        }
    }

    /// <summary>
    /// Validates UAT documents without throwing. Returns list of blocked documents.
    /// </summary>
    public static List<string> GetBlockedUatDocuments(Options options, params string?[] documents)
    {
        if (!options.UatEnabled)
        {
            return new List<string>();
        }

        var blocked = new List<string>();
        foreach (var doc in documents)
        {
            if (string.IsNullOrWhiteSpace(doc))
            {
                continue;
            }

            var digits = SerasaPefinConstants.DigitsOnly(doc);
            if (digits.Length > 0 && !SerasaPefinConstants.UatAuthorizedDocuments.Contains(digits))
            {
                blocked.Add(digits);
            }
        }

        return blocked;
    }

    /// <summary>
    /// Validates main debt input without throwing. Returns validation result.
    /// </summary>
    public static SerasaPefinValidationResult ValidateMainDebt(MainDebtInput input, Options options)
    {
        var missing = ValidateMainDebtInput(input);
        var blocked = GetBlockedUatDocuments(options, input.DebtorDocument, input.CreditorDocument);

        return new SerasaPefinValidationResult(
            IsValid: missing.Count == 0 && blocked.Count == 0,
            MissingFields: missing,
            BlockedDocuments: blocked);
    }

    /// <summary>
    /// Validates guarantor input without throwing. Returns validation result.
    /// </summary>
    public static SerasaPefinValidationResult ValidateGuarantor(GuarantorInput input, Options options)
    {
        var missing = ValidateGuarantorInput(input);
        var blocked = GetBlockedUatDocuments(options, input.DebtorDocument, input.CreditorDocument, input.GuarantorDocument);

        return new SerasaPefinValidationResult(
            IsValid: missing.Count == 0 && blocked.Count == 0,
            MissingFields: missing,
            BlockedDocuments: blocked);
    }
}

/// <summary>
/// Generic Serasa address shared by debtor and guarantor payloads.
/// </summary>
public sealed record SerasaAddress(
    string ZipCode,
    string AddressLine,
    string District,
    string City,
    string State,
    string? Complement = null,
    string? Number = null);

/// <summary>
/// Input representing a parcela (installment) with its specific data.
/// </summary>
public sealed record ParcelaInput(
    decimal Valor,
    DateOnly Vencimento,
    int Numero,
    string IdOrigem);

/// <summary>
/// Input used to build the main debt inclusion payload.
/// </summary>
public sealed record MainDebtInput(
    ParcelaInput Parcela,
    string ContractNumber,
    string DebtorDocument,
    string? DebtorName,
    SerasaAddress? DebtorAddress,
    string CreditorDocument,
    string? CategoryId = null)
{
    /// <summary>
    /// Value derived from the parcela for backward compatibility.
    /// </summary>
    public decimal Value => Parcela.Valor;

    /// <summary>
    /// DueDate derived from the parcela for backward compatibility.
    /// </summary>
    public DateOnly DueDate => Parcela.Vencimento;
}

/// <summary>
/// Input used to build a guarantor inclusion payload.
/// </summary>
public sealed record GuarantorInput(
    ParcelaInput Parcela,
    string ContractNumber,
    string DebtorDocument,
    string CreditorDocument,
    string GuarantorDocument,
    string? GuarantorName,
    SerasaAddress? GuarantorAddress,
    string? CategoryId = null)
{
    /// <summary>
    /// Value derived from the parcela for backward compatibility.
    /// </summary>
    public decimal Value => Parcela.Valor;

    /// <summary>
    /// DueDate derived from the parcela for backward compatibility.
    /// </summary>
    public DateOnly DueDate => Parcela.Vencimento;
}

/// <summary>
/// Validation result for Serasa PEFIN payload validation (non-throwing version).
/// </summary>
public sealed record SerasaPefinValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> BlockedDocuments);

/// <summary>
/// Thrown when the Serasa payload builder detects invalid input (missing fields or blocked documents).
/// </summary>
public sealed class SerasaPefinValidationException : Exception
{
    /// <summary>Short code mapping to the Node error codes.</summary>
    public string Code { get; }

    /// <summary>Target HTTP status code (400 for validation errors).</summary>
    public int StatusCode { get; }

    /// <summary>List of missing fields when applicable.</summary>
    public IReadOnlyList<string> MissingFields { get; }

    /// <summary>List of blocked documents (UAT) when applicable.</summary>
    public IReadOnlyList<string> BlockedDocuments { get; }

    private SerasaPefinValidationException(
        string message,
        string code,
        int statusCode,
        IReadOnlyList<string>? missingFields = null,
        IReadOnlyList<string>? blockedDocuments = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        MissingFields = missingFields ?? Array.Empty<string>();
        BlockedDocuments = blockedDocuments ?? Array.Empty<string>();
    }

    /// <summary>Creates an exception representing missing required fields.</summary>
    public static SerasaPefinValidationException ForMissingFields(IReadOnlyList<string> fields) => new(
        "SERASA_PEFIN_CAMPOS_OBRIGATORIOS_FALTANTES",
        "SERASA_PEFIN_MISSING_REQUIRED_FIELDS",
        400,
        missingFields: fields);

    /// <summary>Creates an exception representing blocked (non-UAT) documents.</summary>
    public static SerasaPefinValidationException ForBlockedDocuments(IReadOnlyList<string> blocked) => new(
        "SERASA_PEFIN_DOCUMENTO_NAO_AUTORIZADO_UAT",
        "SERASA_PEFIN_UAT_DOCUMENT_NOT_ALLOWED",
        400,
        blockedDocuments: blocked);
}

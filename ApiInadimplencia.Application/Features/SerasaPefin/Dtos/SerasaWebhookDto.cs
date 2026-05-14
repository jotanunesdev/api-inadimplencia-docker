namespace ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

/// <summary>
/// DTO for Serasa PEFIN webhook payload
/// </summary>
public record SerasaWebhookDto(
    string Uuid,
    string EventType,
    string Resultado,
    string TransactionId,
    Dictionary<string, object> Payload);

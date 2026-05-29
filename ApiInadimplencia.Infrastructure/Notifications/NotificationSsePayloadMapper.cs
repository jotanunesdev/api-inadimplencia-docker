using System.Text.Json;
using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Infrastructure.Notifications;

public static class NotificationSsePayloadMapper
{
    public static Dictionary<string, object?> ToSnapshotPayload(InadNotificacao notification)
        => ToPayload(notification, readAt: null, deletedAt: notification.ExcluidaEm);

    public static Dictionary<string, object?> ToUpdatePayload(
        InadNotificacao notification,
        DateTimeOffset? readAt = null,
        DateTimeOffset? deletedAt = null)
        => ToPayload(notification, readAt, deletedAt);

    public static Dictionary<string, object?> ToReadAllPayload()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "read_all",
        };

    private static Dictionary<string, object?> ToPayload(
        InadNotificacao notification,
        DateTimeOffset? readAt,
        DateTimeOffset? deletedAt)
    {
        var payload = ParsePayload(notification.Mensagem);
        var statusNegativacao = GetValue(payload, "statusNegativacao") ?? GetValue(payload, "status") ?? GetValue(payload, "statusKanban");
        var solicitacaoId = GetValue(payload, "solicitacaoId") ?? GetValue(payload, "solicitacaO_ID") ?? GetValue(payload, "SOLICITACAO_ID");
        var solicitanteUsername = GetValue(payload, "solicitanteUsername") ?? GetValue(payload, "solicitante") ?? GetValue(payload, "solicitanteUsername");
        var aprovador = GetValue(payload, "aprovador") ?? GetValue(payload, "APROVADOR");

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = notification.Id.ToString(),
            ["tipo"] = NotificationTypeToLegacyString(notification.Tipo),
            ["type"] = NotificationTypeToSlug(notification.Tipo),
            ["numVenda"] = notification.NumVenda,
            ["cliente"] = GetValue(payload, "cliente"),
            ["cpfCnpj"] = GetValue(payload, "cpfCnpj") ?? GetValue(payload, "cpf") ?? GetValue(payload, "cpfCnpjMasked"),
            ["empreendimento"] = GetValue(payload, "empreendimento"),
            ["valorInadimplente"] = GetValue(payload, "valorInadimplente") ?? GetValue(payload, "valor"),
            ["score"] = GetValue(payload, "score"),
            ["responsavel"] = GetValue(payload, "responsavel"),
            ["proximaAcao"] = GetValue(payload, "proximaAcao"),
            ["status"] = statusNegativacao,
            ["adminUserCode"] = GetValue(payload, "adminUserCode") ?? solicitanteUsername,
            ["lida"] = notification.Lida,
            ["createdAt"] = notification.CriadaEm.ToString("O"),
            ["readAt"] = readAt?.ToString("O"),
            ["deletedAt"] = deletedAt?.ToString("O") ?? notification.ExcluidaEm?.ToString("O"),
            ["mensagem"] = GetValue(payload, "mensagem") ?? notification.Mensagem,
            ["solicitacaoId"] = solicitacaoId,
            ["solicitante"] = solicitanteUsername,
            ["aprovador"] = aprovador,
            ["statusNegativacao"] = statusNegativacao,
            ["parcelas"] = GetValue(payload, "parcelas"),
        };
    }

    private static object? GetValue(Dictionary<string, object?> payload, string key)
        => payload.TryGetValue(key, out var value) ? value : null;

    private static Dictionary<string, object?> ParsePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return ParseObject(document.RootElement);
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, object?> ParseObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ParseElement(property.Value);
        }

        return result;
    }

    private static object? ParseElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ParseObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ParseElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var dec) => dec,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    private static string NotificationTypeToLegacyString(NotificationType type) => type switch
    {
        NotificationType.VendaAtribuida => "VENDA_ATRIBUIDA",
        NotificationType.VendaAtrasada => "VENDA_ATRASADA",
        NotificationType.SolicitacaoNegativacao => "SOLICITACAO_NEGATIVACAO",
        NotificationType.AprovacaoNegativacao => "APROVACAO_NEGATIVACAO",
        NotificationType.RejeicaoNegativacao => "REJEICAO_NEGATIVACAO",
        NotificationType.RetornoSerasaSucesso => "RETORNO_SERASA_SUCESSO",
        NotificationType.RetornoSerasaErro => "RETORNO_SERASA_ERRO",
        _ => type.ToString().ToUpperInvariant(),
    };

    private static string NotificationTypeToSlug(NotificationType type) => type switch
    {
        NotificationType.VendaAtribuida => "assignment",
        NotificationType.VendaAtrasada => "overdue",
        NotificationType.SolicitacaoNegativacao => "negativacao",
        NotificationType.AprovacaoNegativacao => "negativacao",
        NotificationType.RejeicaoNegativacao => "negativacao",
        NotificationType.RetornoSerasaSucesso => "negativacao",
        NotificationType.RetornoSerasaErro => "negativacao",
        _ => "negativacao",
    };
}

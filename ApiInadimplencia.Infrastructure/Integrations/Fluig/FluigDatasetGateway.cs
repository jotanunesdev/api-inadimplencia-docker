using System.Net;
using System.Text.Json;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Integrations.Fluig;

/// <summary>
/// Fluig dataset gateway. Calls
/// <c>GET {baseUrl}/dataset/api/v2/dataset-handle/search</c> with the cached
/// session cookie. On 401/403 the cookie is refreshed once and the request
/// retried. Faithfully ports the legacy Node.js <c>fluigDataset.js</c>.
/// </summary>
public sealed class FluigDatasetGateway : IFluigDatasetGateway
{
    /// <summary>HTTP client name used for dataset queries (separate from auth client).</summary>
    public const string DatasetHttpClientName = "Fluig.Dataset";

    private static readonly IReadOnlyDictionary<FluigConstraintType, string> ConstraintTypeMap =
        new Dictionary<FluigConstraintType, string>
        {
            [FluigConstraintType.Must] = "MUST",
            [FluigConstraintType.MustNot] = "MUST_NOT",
            [FluigConstraintType.Should] = "SHOULD",
        };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FluigSessionManager _sessionManager;
    private readonly IOptions<FluigOptions> _options;
    private readonly ILogger<FluigDatasetGateway> _logger;

    public FluigDatasetGateway(
        IHttpClientFactory httpClientFactory,
        FluigSessionManager sessionManager,
        IOptions<FluigOptions> options,
        ILogger<FluigDatasetGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _sessionManager = sessionManager;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FluigDatasetResponse> SearchAsync(
        FluigDatasetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DatasetName))
        {
            throw new ArgumentException("Dataset name is required.", nameof(request));
        }

        var baseUrl = (_options.Value.Url ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Fluig:Url não configurado.");
        }

        var requestUri = BuildRequestUri(baseUrl, request);

        var cookie = await _sessionManager.GetCookieAsync(cancellationToken).ConfigureAwait(false);
        var (response, body) = await SendAsync(requestUri, cookie, cancellationToken).ConfigureAwait(false);

        // Retry once after a forced refresh when the session is no longer valid.
        if (response is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogInformation("Fluig session rejected ({Status}); refreshing cookie and retrying dataset {Dataset}", (int)response, request.DatasetName);
            _sessionManager.Invalidate();
            var refreshed = await _sessionManager.RefreshAsync(cancellationToken).ConfigureAwait(false);
            (response, body) = await SendAsync(requestUri, refreshed, cancellationToken).ConfigureAwait(false);
        }

        if (response != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Erro ao consultar dataset {request.DatasetName} (status {(int)response}): {Truncate(body, 500)}");
        }

        return ParseResponse(body);
    }

    private static Uri BuildRequestUri(string baseUrl, FluigDatasetRequest request)
    {
        var builder = new UriBuilder($"{baseUrl}/dataset/api/v2/dataset-handle/search");

        // Compose query manually because we need repeated keys (Uri.EscapeDataString
        // + manual join is simpler than messing with HttpUtility on .NET 8).
        var pairs = new List<string> { $"datasetId={Uri.EscapeDataString(request.DatasetName)}" };

        if (request.Fields is { Count: > 0 })
        {
            foreach (var field in request.Fields)
            {
                if (field is null) continue;
                pairs.Add($"field={Uri.EscapeDataString(field)}");
            }
        }

        if (request.OrderBy is { Count: > 0 })
        {
            foreach (var order in request.OrderBy)
            {
                if (order is null) continue;
                pairs.Add($"orderby={Uri.EscapeDataString(order)}");
            }
        }

        if (request.Constraints is { Count: > 0 })
        {
            // Constraints are sent as 4 parallel arrays preserving order.
            foreach (var c in request.Constraints)
            {
                pairs.Add($"constraintsField={Uri.EscapeDataString(c.Field ?? string.Empty)}");
                pairs.Add($"constraintsInitialValue={Uri.EscapeDataString(c.InitialValue ?? string.Empty)}");
                pairs.Add($"constraintsFinalValue={Uri.EscapeDataString(c.FinalValue ?? c.InitialValue ?? string.Empty)}");
                pairs.Add($"constraintsType={Uri.EscapeDataString(ConstraintTypeMap.GetValueOrDefault(c.Type, "MUST"))}");
            }
        }

        builder.Query = string.Join("&", pairs);
        return builder.Uri;
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        Uri uri,
        string cookie,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient(DatasetHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, body);
    }

    private FluigDatasetResponse ParseResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return FluigDatasetResponse.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("values", out var values)
                || values.ValueKind != JsonValueKind.Array)
            {
                _logger.LogDebug("Fluig dataset response did not contain a 'values' array. Returning empty.");
                return FluigDatasetResponse.Empty;
            }

            var rows = new List<IReadOnlyDictionary<string, string?>>(values.GetArrayLength());
            foreach (var row in values.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in row.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Null => null,
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => prop.Value.GetRawText(),
                    };
                }
                rows.Add(dict);
            }

            return new FluigDatasetResponse(rows);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Resposta inválida do Fluig: {Truncate(body, 500)}", ex);
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max] + "…";
}

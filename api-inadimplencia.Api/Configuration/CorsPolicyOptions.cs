using Microsoft.Extensions.Configuration;

namespace ApiInadimplencia.Api.Configuration;

/// <summary>
/// Configures the explicit CORS policy used by the inadimplencia API.
/// </summary>
public sealed class CorsPolicyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Named CORS policy used by the HTTP pipeline.</summary>
    public const string PolicyName = "InadimplenciaCors";

    /// <summary>Allowed browser origins. Values are normalized to scheme + host + port.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } =
    [
        "https://fluig.jotanunes.com",
        "https://hmfluig.jotanunes.com",
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:3000",
        "http://127.0.0.1:3000",
    ];

    /// <summary>Allowed HTTP methods.</summary>
    public IReadOnlyList<string> AllowedMethods { get; init; } =
    [
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "OPTIONS",
    ];

    /// <summary>Allowed request headers.</summary>
    public IReadOnlyList<string> AllowedHeaders { get; init; } =
    [
        "Accept",
        "Authorization",
        "Cache-Control",
        "Content-Type",
        "Origin",
        "Pragma",
        "X-Requested-With",
        "X-User-Code",
        "X-User-Name",
        "X-Username",
    ];

    /// <summary>Response headers exposed to browser JavaScript.</summary>
    public IReadOnlyList<string> ExposedHeaders { get; init; } =
    [
        "Content-Disposition",
        "X-Correlation-Id",
        "X-Request-Id",
    ];

    /// <summary>Whether browser credentials such as cookies are allowed.</summary>
    public bool AllowCredentials { get; init; } = true;

    /// <summary>Preflight cache duration in seconds.</summary>
    public int PreflightMaxAgeSeconds { get; init; } = 3600;

    /// <summary>Builds CORS options from configuration and environment fallback keys.</summary>
    public static CorsPolicyOptions FromConfiguration(IConfiguration configuration)
        => new()
        {
            AllowedOrigins = OriginValues(
                Value(configuration, "Cors:AllowedOrigins", "INAD_CORS_ALLOWED_ORIGINS", "CORS_ORIGIN", "AUTH_CORS_ORIGIN")),
            AllowedMethods = CsvValues(
                Value(configuration, "Cors:AllowedMethods", "INAD_CORS_ALLOWED_METHODS"),
                ["GET", "POST", "PUT", "DELETE", "OPTIONS"]),
            AllowedHeaders = CsvValues(
                Value(configuration, "Cors:AllowedHeaders", "INAD_CORS_ALLOWED_HEADERS"),
                ["Accept", "Authorization", "Cache-Control", "Content-Type", "Origin", "Pragma", "X-Requested-With", "X-User-Code", "X-User-Name", "X-Username"]),
            ExposedHeaders = CsvValues(
                Value(configuration, "Cors:ExposedHeaders", "INAD_CORS_EXPOSED_HEADERS"),
                ["Content-Disposition", "X-Correlation-Id", "X-Request-Id"]),
            AllowCredentials = ParseBoolean(
                Value(configuration, "Cors:AllowCredentials", "INAD_CORS_ALLOW_CREDENTIALS"),
                true),
            PreflightMaxAgeSeconds = (int)ParsePositiveLong(
                Value(configuration, "Cors:PreflightMaxAgeSeconds", "INAD_CORS_PREFLIGHT_MAX_AGE_SECONDS"),
                3600),
        };

    private static string? Value(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Trim('"');
            }
        }

        return null;
    }

    private static IReadOnlyList<string> OriginValues(string? value)
    {
        var values = CsvValues(value, []);
        if (values.Count == 0)
        {
            return new CorsPolicyOptions().AllowedOrigins;
        }

        return values
            .Select(NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeOrigin(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
    }

    private static IReadOnlyList<string> CsvValues(string? value, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var values = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? fallback : values;
    }

    private static bool ParseBoolean(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "sim" or "yes" or "on" => true,
            "false" or "0" or "nao" or "no" or "off" => false,
            _ => fallback,
        };
    }

    private static long ParsePositiveLong(string? value, long fallback)
        => long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}

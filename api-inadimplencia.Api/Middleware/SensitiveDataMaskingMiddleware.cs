using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Api.Middleware;

/// <summary>
/// Middleware para mascarar dados sensíveis em logs e respostas
/// Mascarar CPF/CNPJ, tokens, secrets, cookies Fluig, payloads Serasa
/// </summary>
public class SensitiveDataMaskingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SensitiveDataMaskingMiddleware> _logger;
    private readonly bool _isDevelopment;

    public SensitiveDataMaskingMiddleware(RequestDelegate next, ILogger<SensitiveDataMaskingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = configuration.GetValue<bool>("IsDevelopment", true);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log request com dados mascarados
        var maskedRequestHeaders = MaskHeaders(context.Request.Headers);
        _logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
        _logger.LogDebug("Request Headers: {Headers}", maskedRequestHeaders);

        // Capturar response original para mascarar dados sensíveis
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            // Só aplicamos o masking no CORPO da response quando o conteúdo é JSON.
            // Aplicar em HTML/JS/CSS (ex.: Swagger UI) corrompe os assets, pois a regex
            // de "tokens genéricos longos" trunca nomes de arquivos e identificadores
            // (ex.: swagger-ui-bundle.js -> swagger-***), causando 404 no navegador.
            var contentType = context.Response.ContentType ?? string.Empty;
            var shouldMaskBody = _isDevelopment
                && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);

            if (shouldMaskBody)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
                var maskedResponseBody = MaskResponseBody(responseBody);

                var maskedBytes = Encoding.UTF8.GetBytes(maskedResponseBody);
                context.Response.ContentLength = maskedBytes.Length;
                context.Response.Body = originalBodyStream;
                await context.Response.Body.WriteAsync(maskedBytes);

                _logger.LogDebug("Response: {StatusCode} Body (first 500 chars): {Body}",
                    context.Response.StatusCode,
                    MaskSensitiveData(responseBody.Substring(0, Math.Min(500, responseBody.Length))));
            }
            else
            {
                // Para qualquer outro tipo (HTML, JS, CSS, binário, etc.) apenas copiamos
                // o stream original sem modificar, preservando os bytes exatamente.
                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(originalBodyStream);
                context.Response.Body = originalBodyStream;

                _logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    /// <summary>
    /// Mascarar headers HTTP
    /// </summary>
    private static string MaskHeaders(IHeaderDictionary headers)
    {
        var masked = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            var key = header.Key;
            var value = header.Value.ToString();

            // Mascarar headers sensíveis
            if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            {
                masked[key] = MaskSensitiveData(value);
            }
            else
            {
                masked[key] = value;
            }
        }

        return string.Join(", ", masked.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }

    /// <summary>
    /// Mascarar dados sensíveis em uma string
    /// </summary>
    public static string MaskSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var masked = input;

        // Mascarar CPF (manter primeiros 3 e últimos 2 dígitos)
        masked = Regex.Replace(masked, @"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", 
            match => MaskCpfCnpj(match.Value));

        // Mascarar CNPJ (manter primeiros 2 e últimos 2 dígitos)
        masked = Regex.Replace(masked, @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", 
            match => MaskCnpj(match.Value));

        // Mascarar tokens Bearer (mostrar primeiros 8 caracteres)
        masked = Regex.Replace(masked, @"Bearer\s+([A-Za-z0-9\-._~+/]{8})[A-Za-z0-9\-._~+/]+", 
            "Bearer $1***");

        // Mascarar tokens genéricos longos
        masked = Regex.Replace(masked, @"\b[A-Za-z0-9\-._~+/]{20,}\b", 
            match => match.Value.Length > 20 ? $"{match.Value.Substring(0, 8)}***" : match.Value);

        // Mascarar cookies Fluig (JSESSIONID, etc.)
        masked = Regex.Replace(masked, @"(JSESSIONID|\.AspNetCore\.Session|cookie)[^=]*=[^;\s]+", 
            match => $"{match.Value.Split('=')[0]}=***");

        // Mascarar secrets/keys em JSON
        masked = Regex.Replace(masked, @"""(password|secret|token|apikey|api_key|authorization)""\s*:\s*""[^""]+""", 
            match => $"{match.Value.Split(':')[0]}: \"*****\"", RegexOptions.IgnoreCase);

        // Mascarar documentos em payloads Serasa (padrões comuns)
        masked = Regex.Replace(masked, @"""(documento|cpf|cnpj)""\s*:\s*""\d+""", 
            match => $"{match.Value.Split(':')[0]}: \"***\"", RegexOptions.IgnoreCase);

        return masked;
    }

    /// <summary>
    /// Mascarar dados sensíveis em corpo de respostas JSON.
    /// Versão conservadora: NÃO aplica o regex genérico de "tokens longos",
    /// que corrompia identificadores legítimos como URLs/paths do OpenAPI,
    /// GUIDs, nomes de tipos qualificados e payloads de domínio
    /// (ex.: "/inadimplencia/..." era convertido para "/inadimpl***").
    /// O mascaramento agressivo continua disponível para logs via
    /// <see cref="MaskSensitiveData"/>.
    /// </summary>
    public static string MaskResponseBody(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var masked = input;

        // Mascarar CPF (manter primeiros 3 dígitos)
        masked = Regex.Replace(masked, @"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b",
            match => MaskCpfCnpj(match.Value));

        // Mascarar CNPJ (manter primeiros 2 dígitos)
        masked = Regex.Replace(masked, @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b",
            match => MaskCnpj(match.Value));

        // Mascarar tokens Bearer
        masked = Regex.Replace(masked, @"Bearer\s+([A-Za-z0-9\-._~+/]{8})[A-Za-z0-9\-._~+/]+",
            "Bearer $1***");

        // Mascarar secrets/keys em JSON
        masked = Regex.Replace(masked, @"""(password|secret|token|apikey|api_key|authorization)""\s*:\s*""[^""]+""",
            match => $"{match.Value.Split(':')[0]}: \"*****\"", RegexOptions.IgnoreCase);

        // Mascarar documentos em payloads (cpf/cnpj/documento como números puros)
        masked = Regex.Replace(masked, @"""(documento|cpf|cnpj)""\s*:\s*""\d+""",
            match => $"{match.Value.Split(':')[0]}: \"***\"", RegexOptions.IgnoreCase);

        return masked;
    }

    /// <summary>
    /// Mascarar CPF mantendo primeiros 3 e últimos 2 dígitos
    /// Ex: 123.456.789-01 → 123***01
    /// </summary>
    private static string MaskCpfCnpj(string cpf)
    {
        var digits = Regex.Replace(cpf, @"\D", "");
        if (digits.Length == 11)
        {
            return $"{digits.Substring(0, 3)}***{digits.Substring(9)}";
        }
        return "***";
    }

    /// <summary>
    /// Mascarar CNPJ mantendo primeiros 2 e últimos 2 dígitos
    /// Ex: 12.345.678/0001-90 → 12***90
    /// </summary>
    private static string MaskCnpj(string cnpj)
    {
        var digits = Regex.Replace(cnpj, @"\D", "");
        if (digits.Length == 14)
        {
            return $"{digits.Substring(0, 2)}***{digits.Substring(12)}";
        }
        return "***";
    }

    /// <summary>
    /// Mascarar valor específico de cookie
    /// </summary>
    public static string MaskCookieValue(string cookieHeader)
    {
        if (string.IsNullOrEmpty(cookieHeader))
            return cookieHeader;

        // Mascarar valores de cookies mantendo nomes
        var cookies = cookieHeader.Split(';');
        var maskedCookies = new List<string>();

        foreach (var cookie in cookies)
        {
            var parts = cookie.Trim().Split('=');
            if (parts.Length >= 2)
            {
                var cookieName = parts[0].Trim();
                var cookieValue = "***";
                maskedCookies.Add($"{cookieName}={cookieValue}");
            }
            else
            {
                maskedCookies.Add(cookie.Trim());
            }
        }

        return string.Join("; ", maskedCookies);
    }

    /// <summary>
    /// Mascarar token mostrando apenas primeiros 8 caracteres
    /// </summary>
    public static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return token;

        return token.Length > 8 ? $"{token.Substring(0, 8)}***" : "***";
    }

    /// <summary>
    /// Mascarar secret completamente
    /// </summary>
    public static string MaskSecret()
    {
        return "*****";
    }
}

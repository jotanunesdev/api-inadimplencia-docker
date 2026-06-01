using System.Text.RegularExpressions;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Relatorios.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Application.Features.Relatorios.Commands;

/// <summary>
/// Generates a financial sheet (PDF) URL by orchestrating Fluig datasets and
/// the TOTVS RM report engine. Port of the legacy Node.js
/// <c>rmReportService.fetchFichaFinanceiraUrl</c> with three-tier fallback:
/// <list type="number">
///   <item>Try <c>ds_paramsRel</c> with the configured (ReportColigada, ReportId).</item>
///   <item>On "Relatório não localizado", look up <c>ds_paiFilho_controleDeAcessoRMreportsFluig</c> for an alternative ReportId / Coligada.</item>
///   <item>Still missing → swap Coligada (0 ↔ 1) and retry.</item>
/// </list>
/// The resolved XML is parameter-substituted (COLIGADA/NUMVENDA placeholders
/// identified by <c>ParamName</c>) and submitted to <c>dsIntegraFacilRM</c>
/// with OPC=6 to produce <c>Report.pdf</c>.
/// </summary>
public sealed partial class GenerateFichaFinanceiraCommandHandler
    : ICommandHandler<GenerateFichaFinanceiraCommand, string>
{
    [GeneratedRegex(@"<RptParameterReportPar>([\s\S]*?)</RptParameterReportPar>", RegexOptions.IgnoreCase)]
    private static partial Regex ParameterBlockRegex();

    [GeneratedRegex(@"<ParamName>([\s\S]*?)</ParamName>", RegexOptions.IgnoreCase)]
    private static partial Regex ParamNameRegex();

    [GeneratedRegex(@"<Value([^>]*)>[\s\S]*?</Value>", RegexOptions.IgnoreCase)]
    private static partial Regex ValueElementRegex();

    private static readonly (string From, string To)[] XmlCaseNormalizations =
    {
        ("arrayofrptparameterreportpar", "ArrayOfRptParameterReportPar"),
        ("rptparameterreportpar", "RptParameterReportPar"),
        ("description", "Description"),
        ("paramname", "ParamName"),
        ("type", "Type"),
        ("data", "Data"),
        ("unityType", "UnityType"),
        ("assemblyname", "AssemblyName"),
        ("value", "Value"),
        ("visible", "Visible"),
        ("i:Type", "i:type"),
        ("z:factoryType=", "z:FactoryType="),
        ("http://schemas.Datacontract.org", "http://schemas.datacontract.org"),
        ("xmlns=\"http://www.w3.org/1999/xhtml\"", ""),
    };

    private readonly IFluigDatasetGateway _fluigGateway;
    private readonly IOptions<RmOptions> _options;
    private readonly ILogger<GenerateFichaFinanceiraCommandHandler> _logger;

    public GenerateFichaFinanceiraCommandHandler(
        IFluigDatasetGateway fluigGateway,
        IOptions<RmOptions> options,
        ILogger<GenerateFichaFinanceiraCommandHandler> logger)
    {
        _fluigGateway = fluigGateway;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> HandleAsync(GenerateFichaFinanceiraCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.NumVenda <= 0)
        {
            throw new ArgumentException("NumVenda é obrigatório.", nameof(command));
        }

        var rm = _options.Value;
        var paramColigada = command.CodColigada ?? rm.ParamColigada;
        var reportId = command.ReportId ?? rm.ReportId;
        var reportColigada = command.ReportColigada ?? rm.ReportColigada;

        // Step 1: try the configured (reportColigada, reportId).
        var attempts = new List<(int Coligada, int ReportId, string? Error)>();
        var paramsXml = await TryFetchParamsXmlAsync(reportColigada, reportId, attempts, cancellationToken).ConfigureAwait(false);

        // Step 2: metadata fallback (when "Relatorio nao localizado").
        if (paramsXml is null && IsReportNotFound(attempts[^1].Error))
        {
            var meta = await ResolveReportMetaAsync(rm, cancellationToken).ConfigureAwait(false);
            if (meta is { } found)
            {
                reportId = found.ReportId;
                reportColigada = found.ReportColigada;
                paramsXml = await TryFetchParamsXmlAsync(reportColigada, reportId, attempts, cancellationToken).ConfigureAwait(false);
            }
        }

        // Step 3: swap coligada (0 ↔ 1) and retry.
        if (paramsXml is null && IsReportNotFound(attempts[^1].Error))
        {
            var altColigada = reportColigada == 0 ? 1 : 0;
            paramsXml = await TryFetchParamsXmlAsync(altColigada, reportId, attempts, cancellationToken).ConfigureAwait(false);
            if (paramsXml is not null)
            {
                reportColigada = altColigada;
            }
        }

        if (paramsXml is null)
        {
            var lastError = attempts[^1].Error;
            if (!string.IsNullOrEmpty(lastError) && !IsReportNotFound(lastError))
            {
                throw new InvalidOperationException(lastError);
            }

            var summary = string.Join(" | ", attempts.Select(a => $"coligada={a.Coligada}, id={a.ReportId}"));
            throw new InvalidOperationException($"Relatório não localizado. Tentativas: {summary}");
        }

        // Substitute COLIGADA / NUMVENDA based on ParamName.
        var resolvedXml = ApplyParamValues(paramsXml, paramColigada, command.NumVenda);
        if (rm.Debug)
        {
            _logger.LogInformation(
                "RM ficha-financeira parâmetros resolvidos: reportId={ReportId}, reportColigada={ReportColigada}, paramColigada={ParamColigada}, numVenda={NumVenda}",
                reportId, reportColigada, paramColigada, command.NumVenda);
        }

        // Call dsIntegraFacilRM (OPC=6) to generate Report.pdf.
        var reportRequest = new FluigDatasetRequest(
            "dsIntegraFacilRM",
            Constraints: new[]
            {
                new FluigConstraint("OPC", "6"),
                new FluigConstraint("REPORT", reportId.ToString()),
                new FluigConstraint("COLIGADA", reportColigada.ToString()),
                new FluigConstraint("PARAMETER", resolvedXml),
                new FluigConstraint("FILE", "Report.pdf"),
                new FluigConstraint("FILTRO", string.Empty),
            });

        var reportResponse = await _fluigGateway.SearchAsync(reportRequest, cancellationToken).ConfigureAwait(false);
        var errorField = ReadDatasetError(reportResponse);
        if (!string.IsNullOrEmpty(errorField))
        {
            throw new InvalidOperationException(errorField);
        }

        var url = reportResponse.Values.FirstOrDefault()?.GetValueOrDefault("RETORNO")
                  ?? reportResponse.Values.FirstOrDefault()?.GetValueOrDefault("retorno");

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Relatório não retornou URL.");
        }

        return url;
    }

    private async Task<string?> TryFetchParamsXmlAsync(
        int reportColigada,
        int reportId,
        List<(int Coligada, int ReportId, string? Error)> attempts,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _fluigGateway.SearchAsync(
                new FluigDatasetRequest(
                    "ds_paramsRel",
                    Fields: new[] { reportColigada.ToString(), reportId.ToString() }),
                cancellationToken).ConfigureAwait(false);

            var error = ReadDatasetError(response);
            if (!string.IsNullOrEmpty(error))
            {
                attempts.Add((reportColigada, reportId, error));
                return null;
            }

            var xml = ExtractParamsXml(response);
            if (string.IsNullOrEmpty(xml))
            {
                attempts.Add((reportColigada, reportId, "Parâmetros do relatório não encontrados."));
                return null;
            }

            attempts.Add((reportColigada, reportId, null));
            return xml;
        }
        catch (Exception ex)
        {
            attempts.Add((reportColigada, reportId, ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Looks up the report metadata in <c>ds_paiFilho_controleDeAcessoRMreportsFluig</c>.
    /// Each row carries a <c>table_relatorio</c> field with multiple entries
    /// separated by the unit-separator char (\u0018); each entry is a
    /// pipe-delimited tuple "reportColigada|codSistema|sistema|reportId|reportCode|descricao".
    /// </summary>
    private async Task<(int ReportColigada, int ReportId)?> ResolveReportMetaAsync(
        RmOptions rm,
        CancellationToken cancellationToken)
    {
        FluigDatasetResponse response;
        try
        {
            response = await _fluigGateway.SearchAsync(
                new FluigDatasetRequest("ds_paiFilho_controleDeAcessoRMreportsFluig"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha consultando metadados de relatório no Fluig; seguindo sem fallback.");
            return null;
        }

        var entries = response.Values
            .SelectMany(row => (row.GetValueOrDefault("table_relatorio") ?? string.Empty).Split('\u0018'))
            .Select(ParseReportEntry)
            .OfType<ReportMetaEntry>()
            .ToList();

        var code = (rm.ReportCode ?? rm.ReportId.ToString()).Trim();
        var nameNormalized = (rm.ReportName ?? string.Empty).Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(code))
        {
            var matchByCode = entries.FirstOrDefault(e => string.Equals(e.ReportCode, code, StringComparison.OrdinalIgnoreCase));
            if (matchByCode is not null)
            {
                return (matchByCode.ReportColigada, matchByCode.ReportId);
            }
        }

        if (!string.IsNullOrEmpty(nameNormalized))
        {
            var matchByName = entries.FirstOrDefault(e =>
                (e.Descricao ?? string.Empty).ToLowerInvariant().Contains(nameNormalized));
            if (matchByName is not null)
            {
                return (matchByName.ReportColigada, matchByName.ReportId);
            }
        }

        return null;
    }

    private sealed record ReportMetaEntry(
        int ReportColigada,
        string CodSistema,
        string Sistema,
        int ReportId,
        string ReportCode,
        string? Descricao);

    private static ReportMetaEntry? ParseReportEntry(string raw)
    {
        var parts = raw.Split('|', StringSplitOptions.None).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        if (parts.Length < 5
            || !int.TryParse(parts[0], out var reportColigada)
            || !int.TryParse(parts[3], out var reportId))
        {
            return null;
        }

        var descricao = parts.Length > 5 ? string.Join(" | ", parts.Skip(5)) : null;
        return new ReportMetaEntry(reportColigada, parts[1], parts[2], reportId, parts[4], descricao);
    }

    private static string? ExtractParamsXml(FluigDatasetResponse response)
    {
        // Find the record that actually contains the parameter XML; fall back to
        // any record with a RESULTADO field, otherwise the first row.
        var withMarker = response.Values.FirstOrDefault(row =>
        {
            var v = row.GetValueOrDefault("RESULTADO") ?? row.GetValueOrDefault("resultado");
            return !string.IsNullOrEmpty(v) && v.Contains("RptParameterReportPar", StringComparison.OrdinalIgnoreCase);
        });

        var record = withMarker
                     ?? response.Values.FirstOrDefault(row => !string.IsNullOrEmpty(row.GetValueOrDefault("RESULTADO") ?? row.GetValueOrDefault("resultado")))
                     ?? response.Values.FirstOrDefault();

        if (record is null)
        {
            return null;
        }

        var xml = record.GetValueOrDefault("RESULTADO") ?? record.GetValueOrDefault("resultado");
        return string.IsNullOrWhiteSpace(xml) ? null : xml;
    }

    private static string? ReadDatasetError(FluigDatasetResponse response)
    {
        var first = response.Values.FirstOrDefault();
        if (first is null) return null;
        return first.GetValueOrDefault("ERRO")
               ?? first.GetValueOrDefault("ERROR")
               ?? first.GetValueOrDefault("error")
               ?? first.GetValueOrDefault("Error");
    }

    private static bool IsReportNotFound(string? message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        var lower = message.ToLowerInvariant();
        return lower.Contains("relat") && lower.Contains("nao localizado");
    }

    /// <summary>
    /// Replaces &lt;Value&gt; contents whose corresponding &lt;ParamName&gt; mentions
    /// "colig" (→ paramColigada) or "venda" (→ numVenda). Other parameters are
    /// left untouched. The XML is first normalized to fix legacy casing issues
    /// observed in the TOTVS RM dataset output.
    /// </summary>
    internal static string ApplyParamValues(string xml, int paramColigada, int numVenda)
    {
        var normalized = NormalizeXml(xml);
        return ParameterBlockRegex().Replace(normalized, match =>
        {
            var inner = match.Groups[1].Value;
            var nameMatch = ParamNameRegex().Match(inner);
            if (!nameMatch.Success) return match.Value;

            var paramName = nameMatch.Groups[1].Value.Trim().ToLowerInvariant();
            string? newValue = paramName switch
            {
                var n when n.Contains("colig") => paramColigada.ToString(),
                var n when n.Contains("venda") => numVenda.ToString(),
                _ => null,
            };
            if (newValue is null) return match.Value;

            var replacedInner = ValueElementRegex().Replace(inner, valueMatch =>
            {
                var attrs = valueMatch.Groups[1].Value;
                return $"<Value{attrs}>{newValue}</Value>";
            }, 1);

            return $"<RptParameterReportPar>{replacedInner}</RptParameterReportPar>";
        });
    }

    private static string NormalizeXml(string xml)
    {
        if (string.IsNullOrEmpty(xml)) return string.Empty;
        var current = xml;
        foreach (var (from, to) in XmlCaseNormalizations)
        {
            // Match the Node.js loop semantics: replace all occurrences (case-sensitive).
            current = current.Replace(from, to, StringComparison.Ordinal);
        }
        return current;
    }
}

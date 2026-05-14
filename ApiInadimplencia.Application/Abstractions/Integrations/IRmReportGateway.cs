namespace ApiInadimplencia.Application.Abstractions.Integrations;

/// <summary>
/// Gateway para integração com relatórios TOTVS RM via Fluig
/// </summary>
public interface IRmReportGateway
{
    /// <summary>
    /// Gera um relatório RM via integração Fluig
    /// </summary>
    /// <param name="reportId">ID do relatório</param>
    /// <param name="parameters">Parâmetros do relatório (COLIGADA, NUMVENDA, etc.)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL do PDF gerado</returns>
    Task<string> GenerateReportAsync(string reportId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

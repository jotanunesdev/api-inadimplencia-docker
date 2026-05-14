namespace ApiInadimplencia.Application.Abstractions.Integrations;

/// <summary>
/// Gateway para integração com datasets Fluig/TOTVS
/// </summary>
public interface IFluigDatasetGateway
{
    /// <summary>
    /// Busca dados de um dataset Fluig
    /// </summary>
    /// <param name="datasetName">Nome do dataset</param>
    /// <param name="parameters">Parâmetros do dataset</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Conteúdo do dataset como string XML/JSON</returns>
    Task<string> GetDatasetAsync(string datasetName, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

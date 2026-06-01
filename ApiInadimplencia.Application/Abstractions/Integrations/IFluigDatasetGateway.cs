namespace ApiInadimplencia.Application.Abstractions.Integrations;

/// <summary>
/// Port for the Fluig dataset-handle/search integration. Implementations
/// authenticate against Fluig once (cookie cache) and dispatch dataset queries
/// preserving the legacy Node.js semantics (fields, order, constraints).
/// </summary>
public interface IFluigDatasetGateway
{
    /// <summary>
    /// Executes a Fluig dataset search and returns the parsed values rows.
    /// </summary>
    /// <param name="request">Dataset request (name + optional fields/order/constraints).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dataset response with rows. Throws on transport or auth failure.</returns>
    Task<FluigDatasetResponse> SearchAsync(FluigDatasetRequest request, CancellationToken cancellationToken = default);
}

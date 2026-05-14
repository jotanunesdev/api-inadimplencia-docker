namespace ApiInadimplencia.Application.Abstractions;

/// <summary>
/// Generates unique attendance protocols in AAAAMMDD##### format.
/// </summary>
public interface IProtocoloGenerator
{
    /// <summary>
    /// Generates a new unique protocol number.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unique protocol string in AAAAMMDD##### format.</returns>
    Task<string> GerarProtocoloAsync(CancellationToken cancellationToken = default);
}

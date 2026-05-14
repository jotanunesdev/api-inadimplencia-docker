using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Persistence;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Generates unique attendance protocols using SERIALIZABLE isolation.
/// </summary>
public class ProtocoloGenerator : IProtocoloGenerator
{
    private readonly ILegacySqlExecutor _sqlExecutor;

    public ProtocoloGenerator(ILegacySqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    /// <inheritdoc />
    public async Task<string> GerarProtocoloAsync(CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "DataAtual", DateTime.Now.ToString("yyyyMMdd") }
        };

        // Execute SQL with SERIALIZABLE isolation to generate unique protocol
        // This uses UPDLOCK and HOLDLOCK to prevent race conditions
        var result = await _sqlExecutor.ExecuteAsync(
            "Protocolo.Gerar",
            parameters,
            cancellationToken: cancellationToken);

        if (result == null || !result.IsConfigured)
        {
            throw new InvalidOperationException("Falha ao gerar protocolo.");
        }

        var protocolo = result.Data?.ToString();
        if (string.IsNullOrEmpty(protocolo))
        {
            throw new InvalidOperationException("Protocolo gerado está vazio.");
        }

        return protocolo;
    }
}

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
            ["DataAtual"] = DateTime.Now.ToString("yyyyMMdd")
        };

        // Execute SQL with SERIALIZABLE isolation to generate unique protocol.
        // The command uses UPDLOCK and HOLDLOCK on dbo.OCORRENCIAS to prevent
        // race conditions and returns a single row with the column PROTOCOLO.
        var result = await _sqlExecutor.ExecuteAsync(
            "Protocolo.Gerar",
            parameters,
            cancellationToken: cancellationToken);

        if (result == null || !result.IsConfigured)
        {
            throw new InvalidOperationException("Falha ao gerar protocolo.");
        }

        if (result.Data is string protocoloDireto)
        {
            if (string.IsNullOrWhiteSpace(protocoloDireto))
            {
                throw new InvalidOperationException("Protocolo gerado está vazio.");
            }

            return protocoloDireto;
        }

        if (result.Data is not IDictionary<string, object?> row
            || !row.TryGetValue("PROTOCOLO", out var protocoloValue)
            || protocoloValue is null)
        {
            throw new InvalidOperationException("Protocolo gerado está vazio.");
        }

        var protocolo = protocoloValue.ToString();
        if (string.IsNullOrWhiteSpace(protocolo))
        {
            throw new InvalidOperationException("Protocolo gerado está vazio.");
        }

        return protocolo;
    }
}

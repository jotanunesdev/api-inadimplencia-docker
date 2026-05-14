using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Ocorrencias;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Validator for checking if a sale exists in the database.
/// </summary>
public class VendaValidator : IVendaValidator
{
    private readonly ILegacySqlExecutor _sqlExecutor;

    public VendaValidator(ILegacySqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    public async Task<bool> VendaExisteAsync(int numVenda, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "NUM_VENDA", numVenda }
        };

        var result = await _sqlExecutor.QueryAsync(
            "Venda.Existe",
            parameters,
            true,
            cancellationToken);

        return result != null && result.IsConfigured && result.Data != null;
    }
}

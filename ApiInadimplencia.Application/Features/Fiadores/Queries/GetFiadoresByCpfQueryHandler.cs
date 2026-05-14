using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;

namespace ApiInadimplencia.Application.Features.Fiadores.Queries;

/// <summary>
/// Handles the query to get guarantors by CPF/CNPJ.
/// </summary>
public sealed class GetFiadoresByCpfQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetFiadoresByCpfQuery, IReadOnlyList<FiadorDto>>
{
    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<IReadOnlyList<FiadorDto>> HandleAsync(
        GetFiadoresByCpfQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await _executor.QueryAsync(
            "Fiadores.ByCpf",
            new Dictionary<string, object?>
            {
                ["cpf"] = query.Cpf
            },
            single: false,
            cancellationToken);

        if (!result.IsConfigured || result.Data is null)
        {
            return [];
        }

        var rows = (IReadOnlyList<Dictionary<string, object?>>)result.Data;
        return rows.Select(MapToDto).ToList();
    }

    private static FiadorDto MapToDto(Dictionary<string, object?> row)
    {
        return new FiadorDto
        {
            ID_ASSOCIADO = GetValue<int?>(row, "ID_ASSOCIADO"),
            ID_RESERVA = GetValue<int?>(row, "ID_RESERVA"),
            ID_PESSOA = GetValue<int?>(row, "ID_PESSOA"),
            NOME = GetValue<string>(row, "NOME"),
            DOCUMENTO = GetValue<string>(row, "DOCUMENTO"),
            DATA_CADASTRO = GetValue<DateTime?>(row, "DATA_CADASTRO"),
            RENDA_FAMILIAR = GetValue<decimal?>(row, "RENDA_FAMILIAR"),
            TIPO_ASSOCIACAO = GetValue<string>(row, "TIPO_ASSOCIACAO"),
            NUM_VENDA = GetValue<int?>(row, "NUM_VENDA"),
            ENDERECO = GetValue<string>(row, "ENDERECO"),
        };
    }

    private static T GetValue<T>(Dictionary<string, object?> row, string key)
        => RowValueConverter.GetValue<T>(row, key);
}

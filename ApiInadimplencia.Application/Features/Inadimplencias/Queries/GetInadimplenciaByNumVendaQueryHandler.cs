using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Handles the query to get a defaulted sale by NUM_VENDA.
/// </summary>
public sealed class GetInadimplenciaByNumVendaQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetInadimplenciaByNumVendaQuery, InadimplenciaDto?>
{
    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<InadimplenciaDto?> HandleAsync(
        GetInadimplenciaByNumVendaQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await _executor.QueryAsync(
            "Inadimplencia.ByNumVenda",
            new Dictionary<string, object?>
            {
                ["numVenda"] = query.NumVenda
            },
            single: true,
            cancellationToken);

        if (!result.IsConfigured || result.Data is null)
        {
            return null;
        }

        var row = (Dictionary<string, object?>)result.Data;
        return MapToDto(row);
    }

    private static InadimplenciaDto MapToDto(Dictionary<string, object?> row)
    {
        return new InadimplenciaDto
        {
            CLIENTE = GetValue<string>(row, "CLIENTE"),
            CPF_CNPJ = GetValue<string>(row, "CPF_CNPJ"),
            NUM_VENDA = GetValue<int>(row, "NUM_VENDA"),
            EMPREENDIMENTO = GetValue<string>(row, "EMPREENDIMENTO"),
            BLOCO = GetValue<string>(row, "BLOCO"),
            UNIDADE = GetValue<string>(row, "UNIDADE"),
            QTD_PARCELAS_INADIMPLENTES = GetValue<int?>(row, "QTD_PARCELAS_INADIMPLENTES"),
            STATUS_REPASSE = GetValue<string>(row, "STATUS_REPASSE"),
            SCORE = GetValue<string>(row, "SCORE"),
            SUGESTAO = GetValue<string>(row, "SUGESTAO"),
            VENCIMENTO_MAIS_ANTIGO = GetValue<DateTime?>(row, "VENCIMENTO_MAIS_ANTIGO"),
            VALOR_TOTAL_EM_ABERTO = GetValue<decimal?>(row, "VALOR_TOTAL_EM_ABERTO"),
            VALOR_INADIMPLENTE = GetValue<decimal?>(row, "VALOR_INADIMPLENTE"),
            VALOR_NAO_CONTRATUAL_INAD = GetValue<decimal?>(row, "VALOR_NAO_CONTRATUAL_INAD"),
            VALOR_POUPANCA_INAD = GetValue<decimal?>(row, "VALOR_POUPANCA_INAD"),
            PROXIMA_ACAO = GetValue<string>(row, "PROXIMA_ACAO"),
        };
    }

    private static T GetValue<T>(Dictionary<string, object?> row, string key)
        => RowValueConverter.GetValue<T>(row, key);
}

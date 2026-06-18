using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Inadimplencias.Dtos;

namespace ApiInadimplencia.Application.Features.Inadimplencias.Queries;

/// <summary>
/// Handles the query to get defaulted sales by CPF/CNPJ.
/// </summary>
public sealed class GetInadimplenciaByCpfQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetInadimplenciaByCpfQuery, PagedInadimplenciaResult>
{
    private const int DefaultPageSize = 5000;
    private const int MaxPageSize = 5000;
    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<PagedInadimplenciaResult> HandleAsync(
        GetInadimplenciaByCpfQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? DefaultPageSize : query.PageSize, 1, MaxPageSize);
        var offset = (page - 1) * pageSize;

        var result = await _executor.QueryAsync(
            "Inadimplencia.ByCpf",
            new Dictionary<string, object?>
            {
                ["cpf"] = query.Cpf,
                ["offset"] = offset,
                ["pageSize"] = pageSize,
            },
            single: false,
            cancellationToken);

        if (!result.IsConfigured || result.Data is null)
        {
            return new PagedInadimplenciaResult([], 0, page, pageSize);
        }

        var rows = (IReadOnlyList<Dictionary<string, object?>>)result.Data;
        var total = rows.Count > 0 ? RowValueConverter.GetValue<int>(rows[0], "TOTAL_COUNT") : 0;
        return new PagedInadimplenciaResult(rows.Select(MapToDto).ToList(), total, page, pageSize);
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

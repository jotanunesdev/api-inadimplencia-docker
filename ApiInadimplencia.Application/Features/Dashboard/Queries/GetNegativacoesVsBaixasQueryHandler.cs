using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Handler para <see cref="GetNegativacoesVsBaixasQuery"/>. Lê
/// <c>vw_serasa_pefin_negativacao_baixa_mensal</c> (sempre 12 meses, ordenado
/// por mês) e trunca em-memória para os <c>Meses</c> mais recentes solicitados.
/// </summary>
public sealed class GetNegativacoesVsBaixasQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetNegativacoesVsBaixasQuery, IReadOnlyList<NegativacaoBaixaMensalDto>>
{
    /// <summary>Janela mínima permitida (em meses).</summary>
    public const int MinMeses = 1;

    /// <summary>Janela máxima permitida (em meses).</summary>
    public const int MaxMeses = 24;

    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<IReadOnlyList<NegativacaoBaixaMensalDto>> HandleAsync(
        GetNegativacoesVsBaixasQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Meses < MinMeses || query.Meses > MaxMeses)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Meses,
                $"MESES_INVALIDO: Meses deve estar entre {MinMeses} e {MaxMeses}.");
        }

        if (!_executor.IsConfigured)
        {
            return Array.Empty<NegativacaoBaixaMensalDto>();
        }

        var result = await _executor.QueryAsync(
            "Dashboard.NegativacaoBaixaMensal",
            new Dictionary<string, object?>(),
            single: false,
            cancellationToken);

        if (!result.IsConfigured || result.Data is not IReadOnlyList<Dictionary<string, object?>> rows)
        {
            return Array.Empty<NegativacaoBaixaMensalDto>();
        }

        var dtos = rows
            .Select(row => new NegativacaoBaixaMensalDto(
                AnoMes: RowValueConverter.GetValue<string>(row, "ANO_MES") ?? string.Empty,
                QtdNegativacoes: RowValueConverter.GetValue<long>(row, "QTD_NEGATIVACOES"),
                QtdBaixas: RowValueConverter.GetValue<long>(row, "QTD_BAIXAS")))
            .OrderBy(d => d.AnoMes, StringComparer.Ordinal)
            .ToList();

        // A view sempre retorna 12 meses; aplicamos trim para janelas menores.
        // Para Meses > 12 a view ainda limita a 12, então mantemos tudo.
        if (query.Meses < dtos.Count)
        {
            return dtos.Skip(dtos.Count - query.Meses).ToList();
        }

        return dtos;
    }
}

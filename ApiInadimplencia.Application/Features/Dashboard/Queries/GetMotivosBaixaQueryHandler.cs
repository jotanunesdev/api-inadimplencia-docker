using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Handler para <see cref="GetMotivosBaixaQuery"/>. Lê <c>vw_serasa_pefin_baixa_motivos</c>
/// (sempre janela de 12 meses) e mapeia para DTOs. Quando o SQL Server não está
/// configurado (ambientes de teste/desenvolvimento sem banco), retorna lista vazia.
/// </summary>
public sealed class GetMotivosBaixaQueryHandler(ILegacySqlExecutor executor)
    : IQueryHandler<GetMotivosBaixaQuery, IReadOnlyList<MotivoBaixaDto>>
{
    /// <summary>Janela mínima permitida (em meses).</summary>
    public const int MinMeses = 1;

    /// <summary>Janela máxima permitida (em meses).</summary>
    public const int MaxMeses = 24;

    private readonly ILegacySqlExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public async Task<IReadOnlyList<MotivoBaixaDto>> HandleAsync(
        GetMotivosBaixaQuery query,
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
            return Array.Empty<MotivoBaixaDto>();
        }

        var result = await _executor.QueryAsync(
            "Dashboard.BaixaMotivos",
            new Dictionary<string, object?>(),
            single: false,
            cancellationToken);

        if (!result.IsConfigured || result.Data is not IReadOnlyList<Dictionary<string, object?>> rows)
        {
            return Array.Empty<MotivoBaixaDto>();
        }

        return rows
            .Select(row => new MotivoBaixaDto(
                Motivo: RowValueConverter.GetValue<byte>(row, "MOTIVO"),
                Descricao: RowValueConverter.GetValue<string>(row, "DESCRICAO") ?? string.Empty,
                Qtd: RowValueConverter.GetValue<long>(row, "QTD"),
                Percentual: RowValueConverter.GetValue<decimal>(row, "PERCENTUAL")))
            .ToList();
    }
}

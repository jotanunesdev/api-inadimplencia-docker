using ApiInadimplencia.Application.Abstractions.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// ADO.NET implementation of <see cref="IInadimplenciaParcelaWriteService"/>.
/// Updates the DW table <c>DW.fat_analise_inadimplencia_parcelas</c> with the
/// current Serasa PEFIN status (SIM/NAO) for a given parcela.
/// </summary>
public sealed class InadimplenciaParcelaWriteService(
    SqlServerConnectionFactory connectionFactory,
    ILogger<InadimplenciaParcelaWriteService> logger) : IInadimplenciaParcelaWriteService
{
    private const string TableParcelas = "DW.fat_analise_inadimplencia_parcelas";

    /// <inheritdoc />
    public async Task<int> SetNegativadoByVendaEVencimentoAsync(
        int numVenda,
        DateOnly dataVencimento,
        bool negativado,
        CancellationToken cancellationToken = default)
    {
        if (numVenda <= 0)
        {
            logger.LogWarning("SetNegativado skipped: NUM_VENDA inválido ({NumVenda}).", numVenda);
            return 0;
        }

        const string query = $"""
            UPDATE {TableParcelas}
            SET NEGATIVADO = @flag
            WHERE NUM_VENDA = @numVenda
              AND CAST(DATAVENCIMENTO AS date) = @dataVenc
            """;

        try
        {
            using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqlCommand(query, connection);
            command.Parameters.Add(new SqlParameter("@flag", System.Data.SqlDbType.VarChar, 3) { Value = negativado ? "SIM" : "NAO" });
            command.Parameters.Add(new SqlParameter("@numVenda", System.Data.SqlDbType.Int) { Value = numVenda });
            command.Parameters.Add(new SqlParameter("@dataVenc", System.Data.SqlDbType.Date)
            {
                Value = dataVencimento.ToDateTime(TimeOnly.MinValue),
            });

            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "DW NEGATIVADO atualizado para '{Flag}'. NumVenda={NumVenda}, DataVenc={DataVenc}, RowsAffected={Rows}",
                negativado ? "SIM" : "NAO", numVenda, dataVencimento, rows);
            return rows;
        }
        catch (Exception ex)
        {
            // Best-effort: nunca deve quebrar o fluxo principal.
            logger.LogWarning(ex,
                "Falha ao atualizar NEGATIVADO no DW. NumVenda={NumVenda}, DataVenc={DataVenc}, Negativado={Negativado}",
                numVenda, dataVencimento, negativado);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<int> SetNegativadoByNumeroDocumentoAsync(
        string numeroDocumento,
        bool negativado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroDocumento))
        {
            logger.LogWarning("SetNegativado skipped: NUMERO_DOCUMENTO vazio.");
            return 0;
        }

        const string query = $"""
            UPDATE {TableParcelas}
            SET NEGATIVADO = @flag
            WHERE NUMERO_DOCUMENTO = @numDoc
            """;

        try
        {
            using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqlCommand(query, connection);
            command.Parameters.Add(new SqlParameter("@flag", System.Data.SqlDbType.VarChar, 3) { Value = negativado ? "SIM" : "NAO" });
            command.Parameters.Add(new SqlParameter("@numDoc", System.Data.SqlDbType.VarChar, 50) { Value = numeroDocumento.Trim() });

            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "DW NEGATIVADO atualizado para '{Flag}'. NumeroDocumento={NumDoc}, RowsAffected={Rows}",
                negativado ? "SIM" : "NAO", numeroDocumento, rows);
            return rows;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Falha ao atualizar NEGATIVADO no DW por NUMERO_DOCUMENTO. NumDoc={NumDoc}, Negativado={Negativado}",
                numeroDocumento, negativado);
            return 0;
        }
    }
}

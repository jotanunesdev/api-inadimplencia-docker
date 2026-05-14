using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Queries;

/// <summary>
/// Handler for ListOcorrenciasByNumVendaQuery.
/// </summary>
public sealed class ListOcorrenciasByNumVendaQueryHandler : IQueryHandler<ListOcorrenciasByNumVendaQuery, IReadOnlyList<OcorrenciaDto>>
{
    private readonly string _connectionString;

    public ListOcorrenciasByNumVendaQueryHandler(IConfiguration configuration)
    {
        _connectionString = configuration["SqlServer:ConnectionString"] 
            ?? throw new InvalidOperationException("SqlServer connection string not configured.");
    }

    public async Task<IReadOnlyList<OcorrenciaDto>> HandleAsync(ListOcorrenciasByNumVendaQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                ID, NUM_VENDA_FK, NOME_USUARIO_FK, DESCRICAO, 
                STATUS_OCORRENCIA, DT_OCORRENCIA, HORA_OCORRENCIA, 
                PROXIMA_ACAO, PROTOCOLO
            FROM dbo.OCORRENCIAS
            WHERE NUM_VENDA_FK = @NumVenda
            ORDER BY DT_OCORRENCIA DESC, HORA_OCORRENCIA DESC";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NumVenda", query.NumVenda);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<OcorrenciaDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OcorrenciaDto
            {
                Id = reader.GetGuid(0),
                NumVendaFk = reader.GetInt32(1),
                NomeUsuarioFk = reader.GetString(2),
                Descricao = reader.GetString(3),
                StatusOcorrencia = reader.GetString(4),
                DtOcorrencia = reader.GetDateTime(5),
                HoraOcorrencia = ToStringValue(reader, 6),
                ProximaAcao = reader.IsDBNull(7) ? null : ToStringValue(reader, 7),
                Protocolo = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return results;
    }

    private static string ToStringValue(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return string.Empty;
        var value = reader.GetValue(index);
        return value switch
        {
            TimeSpan ts => ts.ToString(@"hh\:mm\:ss"),
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
            string s => s,
            _ => value?.ToString() ?? string.Empty
        };
    }
}

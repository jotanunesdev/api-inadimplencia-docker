using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApiInadimplencia.Application.Features.Atendimentos.Queries;

/// <summary>
/// Handler for ListAtendimentosByNumVendaQuery.
/// </summary>
public sealed class ListAtendimentosByNumVendaQueryHandler : IQueryHandler<ListAtendimentosByNumVendaQuery, IReadOnlyList<AtendimentoDto>>
{
    private readonly string _connectionString;

    public ListAtendimentosByNumVendaQueryHandler(IConfiguration configuration)
    {
        _connectionString = configuration["SqlServer:ConnectionString"] 
            ?? throw new InvalidOperationException("SqlServer connection string not configured.");
    }

    public async Task<IReadOnlyList<AtendimentoDto>> HandleAsync(ListAtendimentosByNumVendaQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                ID, PROTOCOLO, CPF_CNPJ, NUM_VENDA_FK, DADOS_VENDA, CRIADO_EM
            FROM dbo.ATENDIMENTOS
            WHERE NUM_VENDA_FK = @NumVenda
            ORDER BY CRIADO_EM DESC";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NumVenda", query.NumVenda);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AtendimentoDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AtendimentoDto
            {
                Id = reader.GetGuid(0),
                Protocolo = reader.GetString(1),
                Cpf = reader.GetString(2),
                NumVendaFk = reader.GetInt32(3),
                DadosVendaJson = reader.GetString(4),
                CriadoEm = reader.GetDateTime(5)
            });
        }

        return results;
    }
}

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApiInadimplencia.Application.Features.Atendimentos.Queries;

/// <summary>
/// Handler for ListAtendimentosByClienteQuery.
/// </summary>
public sealed class ListAtendimentosByClienteQueryHandler : IQueryHandler<ListAtendimentosByClienteQuery, IReadOnlyList<AtendimentoDto>>
{
    private readonly string _connectionString;

    public ListAtendimentosByClienteQueryHandler(IConfiguration configuration)
    {
        _connectionString = configuration["SqlServer:ConnectionString"] 
            ?? throw new InvalidOperationException("SqlServer connection string not configured.");
    }

    public async Task<IReadOnlyList<AtendimentoDto>> HandleAsync(ListAtendimentosByClienteQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                a.ID, a.PROTOCOLO, a.CPF_CNPJ, a.NUM_VENDA_FK, a.DADOS_VENDA, a.CRIADO_EM
            FROM dbo.ATENDIMENTOS a
            INNER JOIN DW.fat_analise_inadimplencia_v4 v ON a.NUM_VENDA_FK = v.NUM_VENDA
            WHERE v.NOME_CLIENTE LIKE @NomeCliente
            ORDER BY a.CRIADO_EM DESC";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NomeCliente", $"%{query.NomeCliente}%");
        
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

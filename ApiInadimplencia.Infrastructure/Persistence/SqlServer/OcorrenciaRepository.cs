using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Domain.Ocorrencias;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// SQL Server implementation of the occurrence repository.
/// </summary>
public class OcorrenciaRepository : IOcorrenciaRepository
{
    private readonly ILegacySqlExecutor _sqlExecutor;

    public OcorrenciaRepository(ILegacySqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    public async Task AddAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default)
    {
        // Parameter names must match the placeholders declared in the SQL
        // command "Ocorrencia.Insert" (camelCase). Keep this list in sync
        // with the SQL definition in LegacySqlExecutor.
        var parameters = new Dictionary<string, object?>
        {
            { "id", ocorrencia.Id },
            { "numVenda", ocorrencia.NumVendaFk },
            { "nomeUsuario", ocorrencia.NomeUsuarioFk },
            { "descricao", ocorrencia.Descricao },
            { "statusOcorrencia", ocorrencia.StatusOcorrencia },
            { "dtOcorrencia", ocorrencia.DtOcorrencia },
            { "horaOcorrencia", ocorrencia.HoraOcorrencia },
            { "proximaAcao", ocorrencia.ProximaAcao },
            { "protocolo", ocorrencia.Protocolo }
        };

        await _sqlExecutor.ExecuteAsync(
            "Ocorrencia.Insert",
            parameters,
            cancellationToken: cancellationToken);
    }

    public async Task<Ocorrencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "id", id }
        };

        var result = await _sqlExecutor.QueryAsync(
            "Ocorrencia.GetById",
            parameters,
            true,
            cancellationToken: cancellationToken);

        if (result == null || !result.IsConfigured || result.Data == null)
        {
            return null;
        }

        return MapToOcorrencia(result.Data!);
    }

    public async Task UpdateAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default)
    {
        // Parameter names must match the SQL command "Ocorrencia.Update" (camelCase).
        var parameters = new Dictionary<string, object?>
        {
            { "id", ocorrencia.Id },
            { "numVenda", ocorrencia.NumVendaFk },
            { "nomeUsuario", ocorrencia.NomeUsuarioFk },
            { "descricao", ocorrencia.Descricao },
            { "statusOcorrencia", ocorrencia.StatusOcorrencia },
            { "dtOcorrencia", ocorrencia.DtOcorrencia },
            { "horaOcorrencia", ocorrencia.HoraOcorrencia },
            { "proximaAcao", ocorrencia.ProximaAcao },
            { "protocolo", ocorrencia.Protocolo }
        };

        await _sqlExecutor.ExecuteAsync(
            "Ocorrencia.Update",
            parameters,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "id", ocorrencia.Id }
        };

        await _sqlExecutor.ExecuteAsync(
            "Ocorrencia.Delete",
            parameters,
            cancellationToken: cancellationToken);
    }

    private static Ocorrencia MapToOcorrencia(object data)
    {
        // This is a simplified mapper - in production, use proper ORM or Dapper mapping
        var dict = data as IDictionary<string, object?>;
        if (dict == null)
        {
            throw new InvalidOperationException("Unable to map data to Ocorrencia.");
        }

        return Ocorrencia.Criar(
            Convert.ToInt32(dict["NUM_VENDA_FK"]),
            Convert.ToString(dict["NOME_USUARIO_FK"]) ?? string.Empty,
            Convert.ToString(dict["DESCRICAO"]) ?? string.Empty,
            Convert.ToString(dict["STATUS_OCORRENCIA"]) ?? string.Empty,
            Convert.ToDateTime(dict["DT_OCORRENCIA"]),
            Convert.ToString(dict["HORA_OCORRENCIA"]) ?? string.Empty,
            dict["PROXIMA_ACAO"]?.ToString(),
            dict["PROTOCOLO"]?.ToString());
    }
}

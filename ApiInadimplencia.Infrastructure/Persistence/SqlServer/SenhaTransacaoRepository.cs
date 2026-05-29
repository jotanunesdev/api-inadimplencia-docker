using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.Negativacao;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// SQL Server implementation of transaction password repository using Dapper.
/// </summary>
public sealed class SenhaTransacaoRepository : ISenhaTransacaoRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly ILogger<SenhaTransacaoRepository> _logger;

    public SenhaTransacaoRepository(
        SqlServerConnectionFactory connectionFactory,
        ILogger<SenhaTransacaoRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<UsuarioSenhaTransacao?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        const string sql = @"
            SELECT Username, Hash, TentativasFalhas, BloqueadoAte, CriadaEm, AtualizadaEm
            FROM UsuarioSenhaTransacao
            WHERE Username = @Username";

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);
        var result = await connection.QueryFirstOrDefaultAsync(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: ct));

        if (result == null)
        {
            return null;
        }

        return MapToEntity(result);
    }

    public async Task UpsertAsync(UsuarioSenhaTransacao senha, CancellationToken ct)
    {
        const string sql = @"
            MERGE INTO UsuarioSenhaTransacao AS target
            USING (SELECT @Username AS Username) AS source
            ON target.Username = source.Username
            WHEN MATCHED THEN
                UPDATE SET
                    Hash = @Hash,
                    TentativasFalhas = @TentativasFalhas,
                    BloqueadoAte = @BloqueadoAte,
                    AtualizadaEm = @AtualizadaEm
            WHEN NOT MATCHED THEN
                INSERT (Username, Hash, TentativasFalhas, BloqueadoAte, CriadaEm, AtualizadaEm)
                VALUES (@Username, @Hash, @TentativasFalhas, @BloqueadoAte, @CriadaEm, @AtualizadaEm);";

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Username = senha.Username,
                    Hash = senha.Hash,
                    TentativasFalhas = senha.TentativasFalhas,
                    BloqueadoAte = senha.BloqueadoAte,
                    CriadaEm = senha.CriadaEm,
                    AtualizadaEm = senha.AtualizadaEm
                },
                cancellationToken: ct));

        _logger.LogInformation("Transaction password upserted for user: {Username}", senha.Username);
    }

    private static UsuarioSenhaTransacao MapToEntity(dynamic result)
    {
        return UsuarioSenhaTransacao.Reconstruct(
            result.Username,
            result.Hash,
            result.TentativasFalhas,
            result.BloqueadoAte,
            result.CriadaEm,
            result.AtualizadaEm);
    }

    private record SenhaTransacaoDto(
        string Username,
        string Hash,
        int TentativasFalhas,
        DateTime? BloqueadoAte,
        DateTime CriadaEm,
        DateTime AtualizadaEm);
}

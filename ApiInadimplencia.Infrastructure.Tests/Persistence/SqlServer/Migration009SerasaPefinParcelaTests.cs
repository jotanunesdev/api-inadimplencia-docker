using FluentAssertions;
using FactAttribute = ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer.RequiresSqlFactAttribute;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class Migration009SerasaPefinParcelaTests : IDisposable
{
    private readonly string _connectionString;

    public Migration009SerasaPefinParcelaTests()
    {
        _connectionString = SqlIntegrationTestGuard.RequireAvailableConnectionString(nameof(Migration009SerasaPefinParcelaTests));
    }

    public void Dispose()
    {
        // No cleanup needed for migration validation tests
    }

    [RequiresSqlFact]
    public async Task Migration009_SerasaPefinParcela_ColumnsExist()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'SERASA_PEFIN_SOLICITACOES' AND TABLE_SCHEMA = 'dbo'
                AND COLUMN_NAME IN ('NUMERO_PARCELA', 'PARCELA_ID_ORIGEM', 'ID_SOLICITACAO_PAI')
              ORDER BY ORDINAL_POSITION",
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<(string Name, string Type, int? MaxLength, string Nullable)>();

        while (await reader.ReadAsync())
        {
            columns.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2),
                reader.GetString(3)
            ));
        }

        // Verify required columns exist
        columns.Should().Contain(c => c.Name == "NUMERO_PARCELA" && c.Type == "int" && c.Nullable == "YES");
        columns.Should().Contain(c => c.Name == "PARCELA_ID_ORIGEM" && c.Type == "nvarchar" && c.MaxLength == 64 && c.Nullable == "YES");
        columns.Should().Contain(c => c.Name == "ID_SOLICITACAO_PAI" && c.Type == "uniqueidentifier" && c.Nullable == "YES");
    }

    [RequiresSqlFact]
    public async Task Migration009_SerasaPefinParcela_IndexAtivaExists()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT i.name, i.type_desc, i.is_unique, i.filter_definition
              FROM sys.indexes i
              WHERE i.object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
                AND i.name = 'UX_SERASA_PEFIN_SOLICITACOES_ATIVA'",
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        var hasIndex = await reader.ReadAsync();

        hasIndex.Should().BeTrue();
        reader.GetString(0).Should().Be("UX_SERASA_PEFIN_SOLICITACOES_ATIVA");
        reader.GetString(1).Should().Be("NONCLUSTERED");
        reader.GetBoolean(2).Should().BeTrue();
        
        var filter = reader.IsDBNull(3) ? null : reader.GetString(3);
        filter.Should().Contain("NUMERO_PARCELA IS NOT NULL");
    }

    [Fact]
    public async Task Migration009_SerasaPefinParcela_IndexAtivaLegadaExists()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT i.name, i.type_desc, i.is_unique, i.filter_definition
              FROM sys.indexes i
              WHERE i.object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
                AND i.name = 'UX_SERASA_PEFIN_SOLICITACOES_ATIVA_LEGADA'",
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        var hasIndex = await reader.ReadAsync();

        hasIndex.Should().BeTrue();
        reader.GetString(0).Should().Be("UX_SERASA_PEFIN_SOLICITACOES_ATIVA_LEGADA");
        reader.GetString(1).Should().Be("NONCLUSTERED");
        reader.GetBoolean(2).Should().BeTrue();
        
        var filter = reader.IsDBNull(3) ? null : reader.GetString(3);
        filter.Should().Contain("NUMERO_PARCELA IS NULL");
    }

    [Fact]
    public async Task Migration009_SerasaPefinParcela_IndexAtivaColumns()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT c.name
              FROM sys.index_columns ic
              INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
              WHERE ic.object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
                AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE name = 'UX_SERASA_PEFIN_SOLICITACOES_ATIVA')
              ORDER BY ic.key_ordinal",
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        // Verify index includes NUMERO_PARCELA
        columns.Should().Contain("NUMERO_PARCELA");
        columns.Should().Contain("NUM_VENDA_FK");
        columns.Should().Contain("CONTRACT_NUMBER");
        columns.Should().Contain("DOCUMENTO_DEVEDOR");
        columns.Should().Contain("DOCUMENTO_GARANTIDOR");
        columns.Should().Contain("TIPO_REGISTRO");
    }

    [Fact]
    public async Task Migration009_SerasaPefinParcela_ScriptIsIdempotent()
    {
        // Act & Assert - Run the script twice should not fail
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "db", "009_serasa_pefin_parcela.sql");
        var script = await File.ReadAllTextAsync(scriptPath);
        
        // First execution
        await using var command1 = new SqlCommand(script, connection);
        await command1.ExecuteNonQueryAsync();

        // Second execution (should not fail)
        await using var command2 = new SqlCommand(script, connection);
        var exception = await Record.ExceptionAsync(async () => await command2.ExecuteNonQueryAsync());

        exception.Should().BeNull();
    }
}

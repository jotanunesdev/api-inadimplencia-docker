using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class SerasaPefinRepositoryIntegrationTests : IDisposable
{
    private readonly SerasaPefinRepository _repository;
    private readonly string _connectionString;

    public SerasaPefinRepositoryIntegrationTests()
    {
        // Connection string from .env using dwbi user
        _connectionString = "Server=192.168.79.240\\bi,10433;Database=dwjnc;User Id=dwbi;Password=4bi@2023;Encrypt=True;TrustServerCertificate=True;";
        
        var options = Options.Create(new SqlServerOptions
        {
            ConnectionString = _connectionString,
            CommandTimeoutSeconds = 30
        });

        var connectionFactory = new SqlServerConnectionFactory(options);
        _repository = new SerasaPefinRepository(connectionFactory);

        // Cleanup before tests
        CleanupTestData().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        // Cleanup after tests
        CleanupTestData().GetAwaiter().GetResult();
    }

    private async Task CleanupTestData()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(
            "DELETE FROM dbo.SERASA_PEFIN_WEBHOOKS WHERE TRANSACTION_ID LIKE 'TEST-%';" +
            "DELETE FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE OPERADOR = 'test-runner';",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AddAsync_NewSolicitacao_PersistsRow()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-001",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        // Act
        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Assert
        id.Should().NotBeEmpty();
        
        // Verify persistence
        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.NumVendaFk.Should().Be(12345);
        persisted.TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        persisted.DocumentoDevedor.Should().Be("12345678901");
        persisted.Status.Should().Be(SerasaPefinStatus.PendenteEnvio);
    }

    [Fact]
    public async Task AddAsync_DuplicateActive_ThrowsSerasaPefinDuplicateActiveException()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12346,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-002",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12346,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-002",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        // Act
        await _repository.AddAsync(solicitacao1, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<SerasaPefinDuplicateActiveException>(
            () => _repository.AddAsync(solicitacao2, CancellationToken.None));
        
        exception.Message.Should().Contain("Active Serasa PEFIN solicitation already exists");
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusChange()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12347,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-003",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        
        // Act
        solicitacao.MarcarAguardandoRetorno("TEST-TRANS-001");
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Assert
        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SerasaPefinStatus.AguardandoRetorno);
        updated.TransactionId.Should().Be("TEST-TRANS-001");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsHydratedAggregate()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12348,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-004",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Act
        var retrieved = await _repository.GetByIdAsync(id, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
        retrieved.NumVendaFk.Should().Be(12348);
        retrieved.TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        retrieved.DocumentoDevedor.Should().Be("12345678901");
        retrieved.DocumentoCredor.Should().Be("16202491000193");
        retrieved.ContractNumber.Should().Be("TEST-004");
        retrieved.AreaInformante.Should().Be("0001");
        retrieved.Valor.Should().Be(100.50m);
        retrieved.DataVencimento.Should().Be(new DateOnly(2026, 12, 31));
        retrieved.Status.Should().Be(SerasaPefinStatus.PendenteEnvio);
        retrieved.PayloadAuditoria.Should().Be("{}");
        retrieved.Operador.Should().Be("test-runner");
    }

    [Fact]
    public async Task GetByTransactionIdAsync_FindsRow()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12349,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-005",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao, CancellationToken.None);
        solicitacao.MarcarAguardandoRetorno("TEST-TRANS-002");
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Act
        var retrieved = await _repository.GetByTransactionIdAsync("TEST-TRANS-002", CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.TransactionId.Should().Be("TEST-TRANS-002");
        retrieved.NumVendaFk.Should().Be(12349);
    }

    [Fact]
    public async Task ListByNumVendaAsync_ReturnsRowsOrderedDesc()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12350,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-006",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12350,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-007",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await Task.Delay(200); // Ensure different timestamps
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var results = await _repository.ListByNumVendaAsync(12350, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        // Verify descending order by checking that the second record is not after the first
        results[1].DtCriacao.Should().NotBeAfter(results[0].DtCriacao); // Descending order
    }

    [Fact]
    public async Task ExistsActiveAsync_DetectsActiveDuplicate()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12351,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-008",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Act
        var exists = await _repository.ExistsActiveAsync(
            numVenda: 12351,
            contractNumber: "TEST-008",
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            tipoRegistro: SerasaPefinRecordType.Principal,
            CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AddWebhookAsync_PersistsWebhookRow()
    {
        // Arrange
        var webhook = new SerasaPefinWebhookRecord(
            Id: Guid.NewGuid(),
            EventType: "INCLUSAO_SUCESSO",
            TransactionId: "TEST-TRANS-003",
            Payload: "{\"test\":\"payload\"}",
            MatchedSolicitacaoId: null,
            Processado: true,
            MensagemErro: null,
            DtRecebimento: DateTime.UtcNow);

        // Act
        await _repository.AddWebhookAsync(webhook, CancellationToken.None);

        // Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.SERASA_PEFIN_WEBHOOKS WHERE TRANSACTION_ID = 'TEST-TRANS-003'",
            connection);
        var count = await command.ExecuteScalarAsync();
        
        count.Should().NotBeNull();
        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    public async Task WebhookExistsByUuidAsync_ReturnsTrueWhenWebhookExists()
    {
        // Arrange
        var webhook = new SerasaPefinWebhookRecord(
            Id: Guid.NewGuid(),
            EventType: "INCLUSAO_SUCESSO",
            TransactionId: "TEST-TRANS-004",
            Payload: "{\"test\":\"payload\"}",
            MatchedSolicitacaoId: null,
            Processado: true,
            MensagemErro: null,
            DtRecebimento: DateTime.UtcNow);

        await _repository.AddWebhookAsync(webhook, CancellationToken.None);

        // Act
        var exists = await _repository.WebhookExistsByUuidAsync("TEST-TRANS-004", CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task WebhookExistsByUuidAsync_ReturnsFalseWhenWebhookDoesNotExist()
    {
        // Act
        var exists = await _repository.WebhookExistsByUuidAsync("TEST-TRANS-NONEXISTENT", CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyWebhookTransactionalAsync_UpdatesSolicitacaoAndInsertsWebhook()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12352,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-009",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        solicitacao.MarcarAguardandoRetorno("TEST-TRANS-005");
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        solicitacao.AplicarWebhookSucesso("{\"test\":\"webhook\"}", "CADUS-001", "SERIE-001");

        var webhook = new SerasaPefinWebhookRecord(
            Id: Guid.NewGuid(),
            EventType: "INCLUSAO_SUCESSO",
            TransactionId: "TEST-TRANS-005",
            Payload: "{\"test\":\"webhook\"}",
            MatchedSolicitacaoId: id,
            Processado: true,
            MensagemErro: null,
            DtRecebimento: DateTime.UtcNow);

        // Act
        await _repository.ApplyWebhookTransactionalAsync(solicitacao, webhook, CancellationToken.None);

        // Assert
        var updatedSolicitacao = await _repository.GetByIdAsync(id, CancellationToken.None);
        updatedSolicitacao.Should().NotBeNull();
        updatedSolicitacao!.Status.Should().Be(SerasaPefinStatus.NegativadoSucesso);
        updatedSolicitacao.CadusKey.Should().Be("CADUS-001");
        updatedSolicitacao.CadusSerie.Should().Be("SERIE-001");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.SERASA_PEFIN_WEBHOOKS WHERE TRANSACTION_ID = 'TEST-TRANS-005' AND PROCESSADO = 1",
            connection);
        var webhookCount = await command.ExecuteScalarAsync();
        
        webhookCount.Should().NotBeNull();
        Convert.ToInt32(webhookCount).Should().Be(1);
    }

    [Fact]
    public async Task ApplyWebhookTransactionalAsync_RollsBackOnFailure()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12353,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-010",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        solicitacao.MarcarAguardandoRetorno("TEST-TRANS-006");
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Create webhook with invalid solicitacao ID to simulate failure
        var invalidSolicitacaoId = Guid.NewGuid();
        var webhook = new SerasaPefinWebhookRecord(
            Id: Guid.NewGuid(),
            EventType: "INCLUSAO_SUCESSO",
            TransactionId: "TEST-TRANS-006",
            Payload: "{\"test\":\"webhook\"}",
            MatchedSolicitacaoId: invalidSolicitacaoId,
            Processado: true,
            MensagemErro: null,
            DtRecebimento: DateTime.UtcNow);

        // Act & Assert - Should throw exception and rollback
        await Assert.ThrowsAsync<SqlException>(() => 
            _repository.ApplyWebhookTransactionalAsync(solicitacao, webhook, CancellationToken.None));

        // Verify rollback - solicitacao status should not have changed
        var unchangedSolicitacao = await _repository.GetByIdAsync(id, CancellationToken.None);
        unchangedSolicitacao.Should().NotBeNull();
        unchangedSolicitacao!.Status.Should().Be(SerasaPefinStatus.AguardandoRetorno);
        unchangedSolicitacao.CadusKey.Should().BeNull();
    }
}

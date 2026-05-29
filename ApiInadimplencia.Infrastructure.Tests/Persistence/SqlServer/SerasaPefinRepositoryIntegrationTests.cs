using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FactAttribute = ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer.RequiresSqlFactAttribute;
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
        _connectionString = SqlIntegrationTestGuard.RequireAvailableConnectionString(nameof(SerasaPefinRepositoryIntegrationTests));
        
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
            "DELETE FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE CONTRACT_NUMBER LIKE 'TEST-%' OR OPERADOR LIKE '%user%' OR OPERADOR LIKE '%test%';",
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
    public async Task ListByIdSolicitacaoPaiAsync_ReturnsOnlyChildRowsForRequestedParentOrderedByNumeroParcela()
    {
        // Arrange
        var solicitacaoPai = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12354,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-005A",
            areaInformante: "0001",
            valor: 300.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "user.parent");

        var outraSolicitacaoPai = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12355,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-005B",
            areaInformante: "0001",
            valor: 400.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "user.other");

        var filhaParcelaDois = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12354,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-005A",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 11, 30),
            solicitanteUsername: "user.parent",
            numeroParcela: 2,
            parcelaIdOrigem: "2",
            idSolicitacaoPai: solicitacaoPai.Id);

        var filhaParcelaUm = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12354,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-005A",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 10, 31),
            solicitanteUsername: "user.parent",
            numeroParcela: 1,
            parcelaIdOrigem: "1",
            idSolicitacaoPai: solicitacaoPai.Id);

        var filhaDeOutroPai = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12355,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-005B",
            areaInformante: "0001",
            valor: 150.50m,
            dataVencimento: new DateOnly(2026, 9, 30),
            solicitanteUsername: "user.other",
            numeroParcela: 3,
            parcelaIdOrigem: "3",
            idSolicitacaoPai: outraSolicitacaoPai.Id);

        await _repository.AddManyAsync(
            new[] { solicitacaoPai, outraSolicitacaoPai, filhaParcelaDois, filhaParcelaUm, filhaDeOutroPai },
            CancellationToken.None);

        // Act
        var results = await _repository.ListByIdSolicitacaoPaiAsync(solicitacaoPai.Id, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results[0].IdSolicitacaoPai.Should().Be(solicitacaoPai.Id);
        results[0].NumeroParcela.Should().Be(1);
        results[0].ParcelaIdOrigem.Should().Be("1");
        results[1].IdSolicitacaoPai.Should().Be(solicitacaoPai.Id);
        results[1].NumeroParcela.Should().Be(2);
        results[1].ParcelaIdOrigem.Should().Be("2");
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
    public async Task ListByStatusAsync_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12360,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-010",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "user1");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12361,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-011",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var results = await _repository.ListByStatusAsync(
            status: SerasaPefinStatus.AguardandoAprovacao,
            numVenda: null,
            solicitacaoId: null,
            solicitanteUsername: null,
            take: 50,
            skip: 0,
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].Status.Should().Be(SerasaPefinStatus.AguardandoAprovacao);
        results[0].SolicitanteUsername.Should().Be("user1");
    }

    [Fact]
    public async Task ListByStatusAsync_WithNumVendaFilter_ReturnsFilteredResults()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12362,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-012",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12363,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-013",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var results = await _repository.ListByStatusAsync(
            status: null,
            numVenda: 12362,
            solicitacaoId: null,
            solicitanteUsername: null,
            take: 50,
            skip: 0,
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].NumVendaFk.Should().Be(12362);
    }

    [Fact]
    public async Task ListByStatusAsync_WithSolicitacaoIdFilter_ReturnsSingleResult()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12364,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-014",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Act
        var results = await _repository.ListByStatusAsync(
            status: null,
            numVenda: null,
            solicitacaoId: id,
            solicitanteUsername: null,
            take: 50,
            skip: 0,
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(id);
    }

    [Fact]
    public async Task ListByStatusAsync_WithSolicitanteUsernameFilter_ReturnsFilteredResults()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12365,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-015",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "user.test");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12366,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-016",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "other.user");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var results = await _repository.ListByStatusAsync(
            status: null,
            numVenda: null,
            solicitacaoId: null,
            solicitanteUsername: "user.test",
            take: 50,
            skip: 0,
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].SolicitanteUsername.Should().Be("user.test");
    }

    [Fact]
    public async Task ListByStatusAsync_WithMultipleFilters_ReturnsFilteredResults()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12367,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-017",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "user.multi");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12367,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-018",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "other.multi");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var results = await _repository.ListByStatusAsync(
            status: SerasaPefinStatus.AguardandoAprovacao,
            numVenda: 12367,
            solicitacaoId: null,
            solicitanteUsername: "user.multi",
            take: 50,
            skip: 0,
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].NumVendaFk.Should().Be(12367);
        results[0].SolicitanteUsername.Should().Be("user.multi");
        results[0].Status.Should().Be(SerasaPefinStatus.AguardandoAprovacao);
    }

    [Fact]
    public async Task ListByStatusAsync_WithPagination_RespectsTakeAndSkip()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12368,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-019",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12369,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-020",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var solicitacao3 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12370,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "11122233344",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-021",
            areaInformante: "0001",
            valor: 300.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await Task.Delay(200);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);
        await Task.Delay(200);
        await _repository.AddAsync(solicitacao3, CancellationToken.None);

        // Act - Get first page with take=2
        var page1 = await _repository.ListByStatusAsync(
            status: null,
            numVenda: null,
            solicitacaoId: null,
            solicitanteUsername: null,
            take: 2,
            skip: 0,
            CancellationToken.None);

        // Act - Get second page with skip=2
        var page2 = await _repository.ListByStatusAsync(
            status: null,
            numVenda: null,
            solicitacaoId: null,
            solicitanteUsername: null,
            take: 2,
            skip: 2,
            CancellationToken.None);

        // Assert
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(1);
        // Verify no overlap between pages
        page1.Select(p => p.Id).Should().NotIntersectWith(page2.Select(p => p.Id));
    }

    [Fact]
    public async Task ListByStatusAsync_NoFilters_ReturnsAllResultsPaginated()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12371,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-022",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 12372,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-023",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            operador: "test-runner",
            payloadAuditoria: "{}");

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var results = await _repository.ListByStatusAsync(
            status: null,
            numVenda: null,
            solicitacaoId: null,
            solicitanteUsername: null,
            take: 50,
            skip: 0,
            CancellationToken.None);

        // Assert
        results.Count.Should().BeGreaterThanOrEqualTo(2);
        // Verify descending order
        for (int i = 1; i < results.Count; i++)
        {
            results[i].DtCriacao.Should().NotBeAfter(results[i - 1].DtCriacao);
        }
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

    [Fact]
    public async Task Migration005_UsuarioSenhaTransacao_TableExists()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'USUARIO_SENHA_TRANSACAO' AND TABLE_SCHEMA = 'dbo'",
            connection);
        var count = await command.ExecuteScalarAsync();

        count.Should().NotBeNull();
        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    public async Task Migration005_UsuarioSenhaTransacao_HasCorrectColumns()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'USUARIO_SENHA_TRANSACAO' AND TABLE_SCHEMA = 'dbo'
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

        // Verify required columns
        columns.Should().Contain(c => c.Name == "USERNAME" && c.Type == "varchar" && c.MaxLength == 100 && c.Nullable == "NO");
        columns.Should().Contain(c => c.Name == "HASH" && c.Type == "nvarchar" && c.MaxLength == 500 && c.Nullable == "NO");
        columns.Should().Contain(c => c.Name == "TENTATIVAS_FALHAS" && c.Type == "int" && c.Nullable == "NO");
        columns.Should().Contain(c => c.Name == "BLOQUEADO_ATE" && c.Type == "datetime2" && c.Nullable == "YES");
        columns.Should().Contain(c => c.Name == "CRIADA_EM" && c.Type == "datetime2" && c.Nullable == "NO");
        columns.Should().Contain(c => c.Name == "ATUALIZADA_EM" && c.Type == "datetime2" && c.Nullable == "NO");
    }

    [Fact]
    public async Task Migration006_SerasaPefinStatus_ContainsNewStatuses()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT definition FROM sys.check_constraints
              WHERE parent_object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
              AND name = 'CK_SERASA_PEFIN_SOLICITACOES_STATUS'",
            connection);

        var definition = await command.ExecuteScalarAsync() as string;

        definition.Should().NotBeNull();
        definition.Should().Contain("AGUARDANDO_APROVACAO");
        definition.Should().Contain("APROVADA");
        definition.Should().Contain("REJEITADA");
        definition.Should().Contain("APROVADA_FALHA_ENVIO");
    }

    [Fact]
    public async Task Migration006_SerasaPefin_HasNewColumns()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT COLUMN_NAME
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'SERASA_PEFIN_SOLICITACOES' AND TABLE_SCHEMA = 'dbo'
              AND COLUMN_NAME IN ('SOLICITANTE_USERNAME', 'APROVADOR_USERNAME', 'DT_APROVACAO', 'JUSTIFICATIVA')",
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        // Verify all new columns exist
        columns.Should().Contain("SOLICITANTE_USERNAME");
        columns.Should().Contain("APROVADOR_USERNAME");
        columns.Should().Contain("DT_APROVACAO");
        columns.Should().Contain("JUSTIFICATIVA");
    }

    [Fact]
    public async Task Migration006_SerasaPefin_IndexAtiva_IncludesNewStatuses()
    {
        // Act & Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT filter_definition FROM sys.indexes
              WHERE name = 'UX_SERASA_PEFIN_SOLICITACOES_ATIVA'
              AND object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')",
            connection);

        var filterDefinition = await command.ExecuteScalarAsync() as string;

        filterDefinition.Should().NotBeNull();
        filterDefinition.Should().Contain("AGUARDANDO_APROVACAO");
        filterDefinition.Should().Contain("APROVADA");
    }

    [Fact]
    public async Task Migration006_InsertAguardandoAprovacao_Accepted()
    {
        // Act & Assert - Test that AGUARDANDO_APROVACAO status is accepted by constraint
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var id = Guid.NewGuid();
        await using var command = new SqlCommand(
            @"INSERT INTO dbo.SERASA_PEFIN_SOLICITACOES
              (ID, NUM_VENDA_FK, TIPO_REGISTRO, DOCUMENTO_DEVEDOR, DOCUMENTO_CREDOR,
               CONTRACT_NUMBER, CATEGORY_ID, AREA_INFORMANTE, VALOR, DATA_VENCIMENTO,
               STATUS, PAYLOAD_AUDITORIA, OPERADOR, SOLICITANTE_USERNAME)
              VALUES (@Id, 99999, 'PRINCIPAL', '12345678901', '16202491000193',
               'TEST-APROVACAO', 'FI', '0001', 100.50, '2026-12-31',
               'AGUARDANDO_APROVACAO', '{}', 'test-runner', 'test.user')",
            connection);
        command.Parameters.AddWithValue("@Id", id);

        var exception = await Record.ExceptionAsync(() => command.ExecuteNonQueryAsync());

        // Should not throw - status should be accepted
        exception.Should().BeNull();

        // Cleanup
        await using var cleanupCommand = new SqlCommand(
            "DELETE FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE ID = @Id",
            connection);
        cleanupCommand.Parameters.AddWithValue("@Id", id);
        await cleanupCommand.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migration006_InsertInvalidStatus_Rejected()
    {
        // Act & Assert - Test that invalid status is rejected by constraint
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var id = Guid.NewGuid();
        await using var command = new SqlCommand(
            @"INSERT INTO dbo.SERASA_PEFIN_SOLICITACOES
              (ID, NUM_VENDA_FK, TIPO_REGISTRO, DOCUMENTO_DEVEDOR, DOCUMENTO_CREDOR,
               CONTRACT_NUMBER, CATEGORY_ID, AREA_INFORMANTE, VALOR, DATA_VENCIMENTO,
               STATUS, PAYLOAD_AUDITORIA, OPERADOR)
              VALUES (@Id, 99998, 'PRINCIPAL', '12345678901', '16202491000193',
               'TEST-INVALID', 'FI', '0001', 100.50, '2026-12-31',
               'XPTO', '{}', 'test-runner')",
            connection);
        command.Parameters.AddWithValue("@Id", id);

        var exception = await Record.ExceptionAsync(() => command.ExecuteNonQueryAsync());

        // Should throw SqlException due to CHECK constraint violation
        exception.Should().NotBeNull();
        exception.Should().BeOfType<SqlException>();
    }

    [Fact]
    public async Task Migration006_DuplicateAguardandoAprovacao_BlockedByIndex()
    {
        // Act & Assert - Test that duplicate AGUARDANDO_APROVACAO is blocked by filtered unique index
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        try
        {
            // Insert first record with AGUARDANDO_APROVACAO
            await using var command1 = new SqlCommand(
                @"INSERT INTO dbo.SERASA_PEFIN_SOLICITACOES
                  (ID, NUM_VENDA_FK, TIPO_REGISTRO, DOCUMENTO_DEVEDOR, DOCUMENTO_CREDOR,
                   CONTRACT_NUMBER, CATEGORY_ID, AREA_INFORMANTE, VALOR, DATA_VENCIMENTO,
                   STATUS, PAYLOAD_AUDITORIA, OPERADOR, SOLICITANTE_USERNAME)
                  VALUES (@Id, 99997, 'PRINCIPAL', '12345678901', '16202491000193',
                   'TEST-DUPLICATE', 'FI', '0001', 100.50, '2026-12-31',
                   'AGUARDANDO_APROVACAO', '{}', 'test-runner', 'test.user')",
                connection);
            command1.Parameters.AddWithValue("@Id", id1);
            await command1.ExecuteNonQueryAsync();

            // Try to insert duplicate with same key and AGUARDANDO_APROVACAO
            await using var command2 = new SqlCommand(
                @"INSERT INTO dbo.SERASA_PEFIN_SOLICITACOES
                  (ID, NUM_VENDA_FK, TIPO_REGISTRO, DOCUMENTO_DEVEDOR, DOCUMENTO_CREDOR,
                   CONTRACT_NUMBER, CATEGORY_ID, AREA_INFORMANTE, VALOR, DATA_VENCIMENTO,
                   STATUS, PAYLOAD_AUDITORIA, OPERADOR, SOLICITANTE_USERNAME)
                  VALUES (@Id, 99997, 'PRINCIPAL', '12345678901', '16202491000193',
                   'TEST-DUPLICATE', 'FI', '0001', 100.50, '2026-12-31',
                   'AGUARDANDO_APROVACAO', '{}', 'test-runner', 'test.user2')",
                connection);
            command2.Parameters.AddWithValue("@Id", id2);

            var exception = await Record.ExceptionAsync(() => command2.ExecuteNonQueryAsync());

            // Should throw SqlException due to unique index violation (2601 or 2627)
            exception.Should().NotBeNull();
            exception.Should().BeOfType<SqlException>();
            var sqlEx = (SqlException)exception;
            sqlEx.Number.Should().BeOneOf(2601, 2627);
        }
        finally
        {
            // Cleanup
            await using var cleanupCommand = new SqlCommand(
                "DELETE FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE NUM_VENDA_FK = 99997",
                connection);
            await cleanupCommand.ExecuteNonQueryAsync();
        }
    }
}

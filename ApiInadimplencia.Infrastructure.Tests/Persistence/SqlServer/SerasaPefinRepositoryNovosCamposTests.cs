using ApiInadimplencia.Domain.SerasaPefin;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FactAttribute = ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer.RequiresSqlFactAttribute;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class SerasaPefinRepositoryNovosCamposTests : IDisposable
{
    private readonly SerasaPefinRepository _repository;
    private readonly string _connectionString;

    public SerasaPefinRepositoryNovosCamposTests()
    {
        _connectionString = SqlIntegrationTestGuard.RequireAvailableConnectionString(nameof(SerasaPefinRepositoryNovosCamposTests));
        
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
            "DELETE FROM dbo.SERASA_PEFIN_WEBHOOKS WHERE TRANSACTION_ID LIKE 'TEST-NOVOS-CAMPOS-%';" +
            "DELETE FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE OPERADOR = 'test-runner-novos-campos';",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task InsertComAguardandoAprovacaoESolicitanteUsername_RoundtripPreservado()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54321,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-001",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test");

        // Act
        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Assert
        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(SerasaPefinStatus.AguardandoAprovacao);
        persisted.SolicitanteUsername.Should().Be("solicitante.test");
        persisted.AprovadorUsername.Should().BeNull();
        persisted.DtAprovacao.Should().BeNull();
        persisted.Justificativa.Should().BeNull();
        persisted.TransactionId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateParaAprovada_RegistraAprovadorUsernameEDtAprovacao()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54322,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-002",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        var aprovadorUsername = "aprovador.test";
        var utcNow = DateTime.UtcNow;

        // Act
        solicitacao.MarcarAprovada(aprovadorUsername, utcNow);
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Assert
        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SerasaPefinStatus.Aprovada);
        updated.AprovadorUsername.Should().Be(aprovadorUsername);
        updated.DtAprovacao.Should().BeCloseTo(utcNow, TimeSpan.FromSeconds(1));
        updated.SolicitanteUsername.Should().Be("solicitante.test");
    }

    [Fact]
    public async Task UpdateParaRejeitada_RegistraJustificativa()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54323,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-003",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        var aprovadorUsername = "aprovador.test";
        var justificativa = "Dados inconsistentes na documentação";
        var utcNow = DateTime.UtcNow;

        // Act
        solicitacao.MarcarRejeitada(aprovadorUsername, justificativa, utcNow);
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Assert
        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SerasaPefinStatus.Rejeitada);
        updated.AprovadorUsername.Should().Be(aprovadorUsername);
        updated.DtAprovacao.Should().BeCloseTo(utcNow, TimeSpan.FromSeconds(1));
        updated.Justificativa.Should().Be(justificativa);
        updated.SolicitanteUsername.Should().Be("solicitante.test");
    }

    [Fact]
    public async Task UpdateParaAprovadaFalhaEnvio_RegistraErro()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54324,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-004",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        solicitacao.MarcarAprovada("aprovador.test", DateTime.UtcNow);
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        var errorMessage = "Erro de conexão com Serasa";
        var statusCode = 500;

        // Act
        solicitacao.MarcarAprovadaFalhaEnvio(errorMessage, statusCode);
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Assert
        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SerasaPefinStatus.AprovadaFalhaEnvio);
        updated.ErrorMessage.Should().Be(errorMessage);
        updated.ErrorStatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task UpdateParaPendenteEnvio_RegistraPayloadAuditoria()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54325,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-005",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test");

        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);
        solicitacao.MarcarAprovada("aprovador.test", DateTime.UtcNow);
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        var payloadAuditoria = "{\"masked\": true, \"documentos\": \"***\"}";

        // Act
        solicitacao.MarcarPreparadoParaEnvio(payloadAuditoria);
        await _repository.UpdateAsync(solicitacao, CancellationToken.None);

        // Assert
        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SerasaPefinStatus.PendenteEnvio);
        updated.PayloadAuditoria.Should().Be(payloadAuditoria);
    }

    [Fact]
    public async Task InsertComCamposDeParcela_RoundtripPreservado()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54326,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-006",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test",
            numeroParcela: 2,
            parcelaIdOrigem: "TITULO-12345",
            idSolicitacaoPai: Guid.NewGuid());

        // Act
        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Assert
        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.NumeroParcela.Should().Be(2);
        persisted.ParcelaIdOrigem.Should().Be("TITULO-12345");
        persisted.IdSolicitacaoPai.Should().NotBeNull();
        persisted.IdSolicitacaoPai.Should().Be(solicitacao.IdSolicitacaoPai);
    }

    [Fact]
    public async Task InsertSemCamposDeParcela_CompatibilidadeRetroativa()
    {
        // Arrange - solicitação sem parcela (legada)
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54327,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-007",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test");

        // Act
        var id = await _repository.AddAsync(solicitacao, CancellationToken.None);

        // Assert
        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.NumeroParcela.Should().BeNull();
        persisted.ParcelaIdOrigem.Should().BeNull();
        persisted.IdSolicitacaoPai.Should().BeNull();
    }

    [Fact]
    public async Task ListByNumVendaAsync_RetornaCamposDeParcela()
    {
        // Arrange
        var solicitacao1 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54328,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-008",
            areaInformante: "0001",
            valor: 100.50m,
            dataVencimento: new DateOnly(2026, 12, 31),
            solicitanteUsername: "solicitante.test",
            numeroParcela: 1,
            parcelaIdOrigem: "TITULO-001");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 54328,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "16202491000193",
            contractNumber: "TEST-NOVOS-009",
            areaInformante: "0001",
            valor: 200.50m,
            dataVencimento: new DateOnly(2027, 01, 31),
            solicitanteUsername: "solicitante.test",
            numeroParcela: 2,
            parcelaIdOrigem: "TITULO-002",
            idSolicitacaoPai: solicitacao1.Id);

        await _repository.AddAsync(solicitacao1, CancellationToken.None);
        await _repository.AddAsync(solicitacao2, CancellationToken.None);

        // Act
        var solicitacoes = await _repository.ListByNumVendaAsync(54328, CancellationToken.None);

        // Assert
        solicitacoes.Should().HaveCount(2);
        solicitacoes.Should().ContainSingle(s => s.NumeroParcela == 1 && s.ParcelaIdOrigem == "TITULO-001");
        solicitacoes.Should().ContainSingle(s => s.NumeroParcela == 2 && s.ParcelaIdOrigem == "TITULO-002");
    }
}

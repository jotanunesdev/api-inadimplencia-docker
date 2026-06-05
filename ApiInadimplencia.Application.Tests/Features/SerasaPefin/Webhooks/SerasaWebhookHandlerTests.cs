using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.SerasaPefin.Webhooks;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Webhooks;

public class SerasaWebhookHandlerTests
{
    private readonly Mock<ISerasaPefinRepository> _repositoryMock;
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepositoryMock;
    private readonly Mock<INotificationDispatcher> _notificationDispatcherMock;
    private readonly Mock<ILogger<SerasaWebhookHandler>> _loggerMock;
    private readonly SerasaWebhookHandler _handler;

    public SerasaWebhookHandlerTests()
    {
        _repositoryMock = new Mock<ISerasaPefinRepository>();
        _baixaRepositoryMock = new Mock<ISerasaPefinBaixaRepository>();
        _notificationDispatcherMock = new Mock<INotificationDispatcher>();
        _loggerMock = new Mock<ILogger<SerasaWebhookHandler>>();
        _handler = new SerasaWebhookHandler(
            _repositoryMock.Object,
            _baixaRepositoryMock.Object,
            _notificationDispatcherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_InclusaoSucesso_UpdatesStatusToNegativadoSucesso()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            operador: "test",
            payloadAuditoria: "{}");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.WasAlreadyProcessed);
        Assert.False(result.NoMatchingTransaction);
        Assert.Equal(uuid, result.Uuid);
        Assert.Equal(SerasaPefinStatus.NegativadoSucesso, solicitacao.Status);
        Assert.Equal("008080948A", solicitacao.CadusKey);
        Assert.Equal("001", solicitacao.CadusSerie);
        _repositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.Is<SerasaPefinSolicitacaoCompleta>(s => s.Status == SerasaPefinStatus.NegativadoSucesso),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InclusaoErro_UpdatesStatusToNegativadoErro_CapturesErrorMessage()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI",
            error = new
            {
                message = "Invalid document",
                statusCode = 400
            }
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            operador: "test",
            payloadAuditoria: "{}");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Erro,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SerasaPefinStatus.NegativadoErro, solicitacao.Status);
        Assert.Equal("Invalid document", solicitacao.ErrorMessage);
        Assert.Equal(400, solicitacao.ErrorStatusCode);
        _repositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.Is<SerasaPefinSolicitacaoCompleta>(s => s.Status == SerasaPefinStatus.NegativadoErro),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AvalistaSucesso_AppliesToGuarantorRecord()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "98765432100",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948B",
            cadusSerie = "002",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Garantidor,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            operador: "test",
            payloadAuditoria: "{}",
            documentoGarantidor: "98765432100");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Avalista,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SerasaPefinStatus.NegativadoSucesso, solicitacao.Status);
        Assert.Equal("008080948B", solicitacao.CadusKey);
        Assert.Equal("002", solicitacao.CadusSerie);
    }

    // -------------------------------------------------------------------
    // Baixa webhook tests (Task 6.0) - resolved via ISerasaPefinBaixaRepository.
    // -------------------------------------------------------------------

    private static SerasaPefinBaixaSolicitacao BuildBaixaAguardandoRetorno(string uuid, string solicitante = "solicitante.user")
    {
        var baixa = SerasaPefinBaixaSolicitacao.Hydrate(
            id: Guid.NewGuid(),
            idSolicitacaoNegativacao: Guid.NewGuid(),
            numVendaFk: 123,
            numeroParcela: 1,
            contractNumber: "123456/00",
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            motivo: SerasaPefinBaixaMotivo.From(3),
            status: SerasaPefinBaixaStatus.BaixaAguardandoRetorno,
            solicitanteUsername: solicitante,
            aprovadorUsername: "aprovador.user",
            dtAprovacao: DateTime.UtcNow,
            justificativa: null,
            transactionId: uuid,
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            tentativas: 1,
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow);
        return baixa;
    }

    [Fact]
    public async Task Handle_BaixaSucesso_ResolvesViaBaixaRepository_AndDispatchesNotification()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            creditor = new { documentNumber = "12345678000190", documentType = "CNPJ" },
            debtor = new { documentNumber = "12345678900", documentType = "CPF" },
            writeOff = new { reason = "1" }
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var baixa = BuildBaixaAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _baixaRepositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baixa);
        _baixaRepositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinBaixaSolicitacao>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Baixa,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.WasAlreadyProcessed);
        Assert.False(result.NoMatchingTransaction);
        Assert.Equal(SerasaPefinBaixaStatus.BaixadoSucesso, baixa.Status);

        // Persistência via baixa repo (atomic).
        _baixaRepositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.Is<SerasaPefinBaixaSolicitacao>(b => b.Id == baixa.Id && b.Status == SerasaPefinBaixaStatus.BaixadoSucesso),
            It.Is<SerasaPefinWebhookRecord>(w => w.MatchedSolicitacaoId == baixa.Id && w.Processado),
            It.IsAny<CancellationToken>()), Times.Once);

        // Não consulta o repositório de negativação para baixa.
        _repositoryMock.Verify(r => r.GetByTransactionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Notificação ao solicitante (RetornoBaixaSucesso) com payload contendo solicitacaoId/status/solicitante.
        _notificationDispatcherMock.Verify(d => d.DispatchAsync(
            NotificationType.RetornoBaixaSucesso,
            "solicitante.user",
            123,
            It.Is<string>(m =>
                m.Contains("solicitanteUsername") &&
                m.Contains("solicitacaoId") &&
                m.Contains("BAIXADO_SUCESSO") &&
                m.Contains("sucesso")),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BaixaErro_ResolvesViaBaixaRepository_AndDispatchesErrorNotification()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            error = new
            {
                message = "Baixa not allowed",
                statusCode = 403
            }
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var baixa = BuildBaixaAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _baixaRepositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baixa);
        _baixaRepositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinBaixaSolicitacao>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Baixa,
            WebhookResultado.Erro,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SerasaPefinBaixaStatus.BaixadoErro, baixa.Status);
        Assert.Equal("Baixa not allowed", baixa.ErrorMessage);
        Assert.Equal(403, baixa.ErrorStatusCode);

        _baixaRepositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.Is<SerasaPefinBaixaSolicitacao>(b => b.Id == baixa.Id && b.Status == SerasaPefinBaixaStatus.BaixadoErro),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(d => d.DispatchAsync(
            NotificationType.RetornoBaixaErro,
            "solicitante.user",
            123,
            It.Is<string>(m =>
                m.Contains("Baixa not allowed") &&
                m.Contains("BAIXADO_ERRO") &&
                m.Contains("Reenvie")),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BaixaWebhook_DuplicateUuid_ReturnsAlreadyProcessed_NoSecondUpdate()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new { uuid = uuid };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Baixa,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.WasAlreadyProcessed);
        Assert.Equal(uuid, result.Uuid);
        _baixaRepositoryMock.Verify(r => r.GetByTransactionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _baixaRepositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinBaixaSolicitacao>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _notificationDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BaixaWebhook_NoMatchingBaixa_PersistsOrphanWebhook()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new { uuid = uuid };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _baixaRepositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinBaixaSolicitacao?)null);
        _repositoryMock.Setup(r => r.AddWebhookAsync(It.IsAny<SerasaPefinWebhookRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Baixa,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.NoMatchingTransaction);
        _repositoryMock.Verify(r => r.AddWebhookAsync(
            It.Is<SerasaPefinWebhookRecord>(w => !w.Processado && w.MensagemErro == "NoMatchingTransaction"),
            It.IsAny<CancellationToken>()), Times.Once);
        _baixaRepositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinBaixaSolicitacao>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateUuid_ReturnsAlreadyProcessed_NoSecondUpdate()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.WasAlreadyProcessed);
        Assert.False(result.NoMatchingTransaction);
        Assert.Equal(uuid, result.Uuid);
        _repositoryMock.Verify(r => r.GetByTransactionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoMatchingTransaction_PersistsOrphanWebhook()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinSolicitacaoCompleta?)null);
        _repositoryMock.Setup(r => r.AddWebhookAsync(
            It.Is<SerasaPefinWebhookRecord>(w => w.TransactionId == uuid && !w.Processado),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.WasAlreadyProcessed);
        Assert.True(result.NoMatchingTransaction);
        _repositoryMock.Verify(r => r.AddWebhookAsync(
            It.Is<SerasaPefinWebhookRecord>(w => 
                w.TransactionId == uuid && 
                !w.Processado && 
                w.MensagemErro == "NoMatchingTransaction"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MalformedJson_PersistsErrorRow_Returns200()
    {
        // Arrange
        var rawJson = "{ invalid json }";

        _repositoryMock.Setup(r => r.AddWebhookAsync(
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        _repositoryMock.Verify(r => r.AddWebhookAsync(
            It.Is<SerasaPefinWebhookRecord>(w => 
                !w.Processado && 
                w.MensagemErro != null && 
                w.MensagemErro.Contains("JSON parse error")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Task 9.0 Tests - Notification Dispatch

    [Fact]
    public async Task Handle_InclusaoSucesso_WithUsernames_DispatchesNotificationToSolicitanteAndAprovador()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            solicitanteUsername: "solicitante.user");
        solicitacao.MarcarAprovada("aprovador.user", DateTime.UtcNow);
        solicitacao.MarcarPreparadoParaEnvio("{}");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationDispatcherMock.Setup(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante.user", Guid.NewGuid() },
                { "aprovador.user", Guid.NewGuid() }
            });

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        _notificationDispatcherMock.Verify(d => d.DispatchManyAsync(
            NotificationType.RetornoSerasaSucesso,
            It.Is<IReadOnlyList<string>>(list => 
                list.Count == 2 && 
                list.Contains("solicitante.user") && 
                list.Contains("aprovador.user")),
            123,
            It.Is<string>(msg => msg.Contains("Cliente negativado com sucesso") && msg.Contains("venda nº 123")),
            null,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InclusaoErro_WithUsernames_DispatchesErrorNotificationWithErrorMessage()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI",
            error = new
            {
                message = "Invalid document format",
                statusCode = 400
            }
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            solicitanteUsername: "solicitante.user");
        solicitacao.MarcarAprovada("aprovador.user", DateTime.UtcNow);
        solicitacao.MarcarPreparadoParaEnvio("{}");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationDispatcherMock.Setup(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante.user", Guid.NewGuid() },
                { "aprovador.user", Guid.NewGuid() }
            });

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Erro,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        _notificationDispatcherMock.Verify(d => d.DispatchManyAsync(
            NotificationType.RetornoSerasaErro,
            It.Is<IReadOnlyList<string>>(list => list.Count == 2),
            123,
            It.Is<string>(msg => msg.Contains("Erro ao negativar cliente") && msg.Contains("venda nº 123") && msg.Contains("Invalid document format")),
            null,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateUuid_DoesNotDispatchNotification()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.WasAlreadyProcessed);
        _notificationDispatcherMock.Verify(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutUsernames_DoesNotDispatchNotification_StillProcessesWebhook()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            operador: "test",
            payloadAuditoria: "{}");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SerasaPefinStatus.NegativadoSucesso, solicitacao.Status);
        _notificationDispatcherMock.Verify(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DispatcherFailure_WebhookStillCompletes_LogsWarning()
    {
        // Arrange
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new
        {
            uuid = uuid,
            debtorDocument = "12345678900",
            creditorDocument = "12345678000190",
            contract = "123456/00",
            debtValue = 1000.00,
            debtDate = "2024-01-01",
            cadusKey = "008080948A",
            cadusSerie = "001",
            debtType = "PEFIN",
            creditorArea = "0001",
            categoryId = "FI"
        };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 123,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678900",
            documentoCredor: "12345678000190",
            contractNumber: "123456/00",
            areaInformante: "0001",
            valor: 1000.00m,
            dataVencimento: new DateOnly(2024, 1, 1),
            solicitanteUsername: "solicitante.user");
        solicitacao.MarcarAprovada("aprovador.user", DateTime.UtcNow);
        solicitacao.MarcarPreparadoParaEnvio("{}");
        solicitacao.MarcarAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _repositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinSolicitacaoCompleta>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationDispatcherMock.Setup(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification service unavailable"));

        // Act
        var result = await _handler.HandleAsync(
            WebhookEventType.Inclusao,
            WebhookResultado.Sucesso,
            rawJson);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SerasaPefinStatus.NegativadoSucesso, solicitacao.Status);
        _notificationDispatcherMock.Verify(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _loggerMock.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, t) => true),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BaixaWebhook_DoesNotDispatchManyNotification_UsesDispatchAsyncOnly()
    {
        // Garante que o caminho de baixa NUNCA usa DispatchManyAsync (reservado para negativação).
        var uuid = "f1d11b18-b459-4f11-97a8-8143a6c392e4";
        var payload = new { uuid = uuid };
        var rawJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var baixa = BuildBaixaAguardandoRetorno(uuid);

        _repositoryMock.Setup(r => r.WebhookExistsByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _baixaRepositoryMock.Setup(r => r.GetByTransactionIdAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baixa);
        _baixaRepositoryMock.Setup(r => r.ApplyWebhookTransactionalAsync(
            It.IsAny<SerasaPefinBaixaSolicitacao>(),
            It.IsAny<SerasaPefinWebhookRecord>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(
            WebhookEventType.Baixa,
            WebhookResultado.Sucesso,
            rawJson);

        Assert.True(result.IsSuccess);
        _notificationDispatcherMock.Verify(d => d.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _notificationDispatcherMock.Verify(d => d.DispatchAsync(
            NotificationType.RetornoBaixaSucesso,
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

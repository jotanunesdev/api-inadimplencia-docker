using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.SerasaPefin.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class DecideNegativacaoCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IAprovadoresPolicy> _aprovadoresPolicyMock;
    private readonly Mock<ISenhaTransacaoValidator> _senhaValidatorMock;
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock;
    private readonly Mock<ISerasaPefinRepository> _serasaRepositoryMock;
    private readonly Mock<IOcorrenciaRepository> _ocorrenciaRepositoryMock;
    private readonly Mock<IProtocoloGenerator> _protocoloGeneratorMock;
    private readonly Mock<INotificationDispatcher> _notificationDispatcherMock;
    private readonly Mock<ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>> _requestNegativacaoHandlerMock;
    private readonly Mock<ILogger<DecideNegativacaoCommandHandler>> _loggerMock;
    private readonly DecideNegativacaoCommandHandler _handler;

    public DecideNegativacaoCommandHandlerTests()
    {
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _aprovadoresPolicyMock = new Mock<IAprovadoresPolicy>();
        _senhaValidatorMock = new Mock<ISenhaTransacaoValidator>();
        _queryServiceMock = new Mock<IInadimplenciaQueryService>();
        _serasaRepositoryMock = new Mock<ISerasaPefinRepository>();
        _ocorrenciaRepositoryMock = new Mock<IOcorrenciaRepository>();
        _protocoloGeneratorMock = new Mock<IProtocoloGenerator>();
        _notificationDispatcherMock = new Mock<INotificationDispatcher>();
        _requestNegativacaoHandlerMock = new Mock<ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>>();
        _loggerMock = new Mock<ILogger<DecideNegativacaoCommandHandler>>();

        _handler = new DecideNegativacaoCommandHandler(
            _currentUserServiceMock.Object,
            _aprovadoresPolicyMock.Object,
            _senhaValidatorMock.Object,
            _queryServiceMock.Object,
            _serasaRepositoryMock.Object,
            _ocorrenciaRepositoryMock.Object,
            _protocoloGeneratorMock.Object,
            _notificationDispatcherMock.Object,
            _requestNegativacaoHandlerMock.Object,
            _loggerMock.Object);

        _queryServiceMock.Setup(q => q.GetVendaAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CriarVenda());
    }

    [Fact]
    public async Task HandleAsync_UsuarioNaoAprovador_DeveLancarUnauthorizedAccessException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("operador")).Returns(false);

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: Guid.NewGuid(),
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("NAO_AUTORIZADO", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_SolicitacaoInexistente_DeveLancarKeyNotFoundException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinSolicitacaoCompleta?)null);

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("NAO_ENCONTRADA", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_SolicitacaoNaoEmAguardandoAprovacao_DeveLancarInvalidOperationException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = SerasaPefinSolicitacaoCompleta.Hydrate(
            id: solicitacaoId,
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            documentoCredor: "12345678000190",
            contractNumber: "12345",
            categoryId: "FI",
            areaInformante: "AREA",
            valor: 100m,
            dataVencimento: new DateOnly(2024, 1, 1),
            status: SerasaPefinStatus.Rejeitada, // Not AguardandoAprovacao
            transactionId: null,
            cadusKey: null,
            cadusSerie: null,
            payloadAuditoria: "{}",
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            operador: "solicitante",
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow,
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("JA_DECIDIDA", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_AprovadorIgualSolicitante_DeveLancarInvalidOperationException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("solicitante");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("solicitante")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("solicitante", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = SerasaPefinSolicitacaoCompleta.Hydrate(
            id: solicitacaoId,
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            documentoCredor: "12345678000190",
            contractNumber: "12345",
            categoryId: "FI",
            areaInformante: "AREA",
            valor: 100m,
            dataVencimento: new DateOnly(2024, 1, 1),
            status: SerasaPefinStatus.AguardandoAprovacao,
            transactionId: null,
            cadusKey: null,
            cadusSerie: null,
            payloadAuditoria: "{}",
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            operador: "solicitante",
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow,
            solicitanteUsername: "solicitante"); // Same as current user

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("SOLICITANTE_NAO_PODE_APROVAR", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_SenhaInvalida_DeveLancarUnauthorizedAccessException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha_errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Invalid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = SerasaPefinSolicitacaoCompleta.Hydrate(
            id: solicitacaoId,
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            documentoCredor: "12345678000190",
            contractNumber: "12345",
            categoryId: "FI",
            areaInformante: "AREA",
            valor: 100m,
            dataVencimento: new DateOnly(2024, 1, 1),
            status: SerasaPefinStatus.AguardandoAprovacao,
            transactionId: null,
            cadusKey: null,
            cadusSerie: null,
            payloadAuditoria: "{}",
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            operador: "solicitante",
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow,
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha_errada");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("SENHA_INVALIDA", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_AprovarHappyPath_DeveAtualizarStatusCriarOcorrenciaENotificarComParcelasReais()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);
        var filhas = new[]
        {
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 1, idSolicitacaoPai: solicitacaoId),
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 2, idSolicitacaoPai: solicitacaoId),
        };

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filhas);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                new List<SerasaSolicitacaoResult>
                {
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-123", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 1),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-124", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 2),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-125", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 3)
                },
                SerasaPefinStatus.AguardandoRetorno));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _serasaRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<SerasaPefinSolicitacaoCompleta>(s =>
                s.Id == solicitacaoId &&
                s.Status == SerasaPefinStatus.Aprovada &&
                s.AprovadorUsername == "aracy.mendoca"),
            It.IsAny<CancellationToken>()), Times.Once);

        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.NomeUsuarioFk == "aracy.mendoca" &&
                o.StatusOcorrencia == "Aprovacao de negativacao" &&
                o.Protocolo == "2026051400001" &&
                o.Descricao.StartsWith("Eu, Aracy Mendoca, aprovo a solicitacao de negativacao do cliente Cliente Teste, com a venda Nº 12345, no endereço Rua Teste, Centro, Goiania/GO, CEP 12345-678, para as parcelas ")
                && o.Descricao.Contains("01/01/2024 R$ 100,00")),
            It.IsAny<CancellationToken>()), Times.Once);

        _requestNegativacaoHandlerMock.Verify(h => h.HandleAsync(
            It.Is<RequestNegativacaoCommand>(c =>
                c.NumVenda == 12345 &&
                c.SolicitacaoIdExistente == solicitacaoId),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.Is<IReadOnlyList<string>>(u => u.Contains("solicitante") && u.Contains("aracy.mendoca")),
            12345,
            It.Is<string>(msg => msg.Contains("3 de 3 parcelas enviadas")), // Should contain parcel count
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AprovarComTresFilhas_DevePropagarStatusPendenteEnvioParaFilhas()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);

        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);
        var filhas = new[]
        {
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 1, idSolicitacaoPai: solicitacaoId),
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 2, idSolicitacaoPai: solicitacaoId),
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 3, idSolicitacaoPai: solicitacaoId),
        };

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filhas);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                new List<SerasaSolicitacaoResult>
                {
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-123", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 1),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-124", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 2),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-125", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 3)
                },
                SerasaPefinStatus.AguardandoRetorno));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _serasaRepositoryMock.Verify(r => r.UpdateManyAsync(
            It.Is<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(items =>
                items.Count == 4
                && items.Any(s => s.Id == solicitacaoId && s.Status == SerasaPefinStatus.Aprovada && s.AprovadorUsername == "aracy.mendoca")
                && items.Count(s => s.IdSolicitacaoPai == solicitacaoId && s.Status == SerasaPefinStatus.PendenteEnvio && s.AprovadorUsername == "aracy.mendoca") == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AprovarSolicitacaoLegadaSemFilhas_NaoDeveQuebrar()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);

        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SerasaPefinSolicitacaoCompleta>());

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                new List<SerasaSolicitacaoResult>
                {
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-123", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 1)
                },
                SerasaPefinStatus.AguardandoRetorno));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        _serasaRepositoryMock.Verify(r => r.UpdateManyAsync(
            It.Is<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(items =>
                items.Count == 1
                && items.Single().Id == solicitacaoId
                && items.Single().Status == SerasaPefinStatus.Aprovada),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AprovarComFalhaParcial_DeveTransitarParaAprovadaFalhaEnvioENotificarComErro()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);
        var filhas = new[]
        {
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 1, idSolicitacaoPai: solicitacaoId),
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 2, idSolicitacaoPai: solicitacaoId),
        };

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filhas);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                new List<SerasaSolicitacaoResult>
                {
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-123", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 1),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, null, SerasaPefinStatus.NegativadoErro, "Serasa error", NumeroParcela: 2),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-125", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 3)
                },
                SerasaPefinStatus.NegativadoErro));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _serasaRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<SerasaPefinSolicitacaoCompleta>(s =>
                s.Id == solicitacaoId &&
                s.Status == SerasaPefinStatus.AprovadaFalhaEnvio &&
                s.ErrorMessage != null),
            It.IsAny<CancellationToken>()), Times.Once);

        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.NomeUsuarioFk == "aracy.mendoca" &&
                o.StatusOcorrencia == "Aprovacao de negativacao" &&
                o.Descricao.StartsWith("Eu, Aracy Mendoca, aprovo a solicitacao de negativacao do cliente Cliente Teste, com a venda Nº 12345, no endereço Rua Teste, Centro, Goiania/GO, CEP 12345-678, para as parcelas ")
                && o.Descricao.Contains("01/01/2024 R$ 100,00")),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.Is<IReadOnlyList<string>>(u => u.Contains("solicitante") && u.Contains("aracy.mendoca")),
            12345,
            It.Is<string>(msg => msg.Contains("2 de 3 parcelas enviadas") && msg.Contains("1 com erro")), // Should show partial success
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AprovarComTodasParcelasFalha_DeveTransitarParaAprovadaFalhaEnvioENotificarComTodasFalhas()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);
        var filhas = new[]
        {
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 1, idSolicitacaoPai: solicitacaoId),
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 2, idSolicitacaoPai: solicitacaoId),
        };

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filhas);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                new List<SerasaSolicitacaoResult>
                {
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, null, SerasaPefinStatus.NegativadoErro, "Serasa error 1", NumeroParcela: 1),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, null, SerasaPefinStatus.NegativadoErro, "Serasa error 2", NumeroParcela: 2),
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, null, SerasaPefinStatus.NegativadoErro, "Serasa error 3", NumeroParcela: 3)
                },
                SerasaPefinStatus.NegativadoErro));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _serasaRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<SerasaPefinSolicitacaoCompleta>(s =>
                s.Id == solicitacaoId &&
                s.Status == SerasaPefinStatus.AprovadaFalhaEnvio &&
                s.ErrorMessage != null),
            It.IsAny<CancellationToken>()), Times.Once);

        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.NomeUsuarioFk == "aracy.mendoca" &&
                o.StatusOcorrencia == "Aprovacao de negativacao" &&
                o.Descricao.StartsWith("Eu, Aracy Mendoca, aprovo a solicitacao de negativacao do cliente Cliente Teste, com a venda Nº 12345, no endereço Rua Teste, Centro, Goiania/GO, CEP 12345-678, para as parcelas ")
                && o.Descricao.Contains("01/01/2024 R$ 100,00")),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.Is<IReadOnlyList<string>>(u => u.Contains("solicitante") && u.Contains("aracy.mendoca")),
            12345,
            It.Is<string>(msg => msg.Contains("0 de 3 parcelas enviadas") && msg.Contains("3 com erro")), // Should show all failed
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AprovarComFalhaSincronaSerasa_DeveTransitarParaAprovadaFalhaEnvio()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = SerasaPefinSolicitacaoCompleta.Hydrate(
            id: solicitacaoId,
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            documentoCredor: "12345678000190",
            contractNumber: "12345",
            categoryId: "FI",
            areaInformante: "AREA",
            valor: 100m,
            dataVencimento: new DateOnly(2024, 1, 1),
            status: SerasaPefinStatus.AguardandoAprovacao,
            transactionId: null,
            cadusKey: null,
            cadusSerie: null,
            payloadAuditoria: "{}",
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            operador: "solicitante",
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow,
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Serasa unavailable"));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _serasaRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<SerasaPefinSolicitacaoCompleta>(s =>
                s.Id == solicitacaoId &&
                s.Status == SerasaPefinStatus.AprovadaFalhaEnvio &&
                s.ErrorMessage != null),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.Is<IReadOnlyList<string>>(u => u.Contains("solicitante") && u.Contains("aracy.mendoca")),
            12345,
            It.Is<string>(msg => msg.Contains("falha") || msg.Contains("erro")),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RejeitarHappyPath_DeveAtualizarStatusCriarOcorrecaoENotificarSolicitante()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);
        var filhas = new[]
        {
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 1, idSolicitacaoPai: solicitacaoId),
            CriarSolicitacao(Guid.NewGuid(), SerasaPefinStatus.AguardandoAprovacao, numeroParcela: 2, idSolicitacaoPai: solicitacaoId),
        };

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filhas);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _notificationDispatcherMock.Setup(n => n.DispatchAsync(
            It.IsAny<NotificationType>(),
            "solicitante",
            12345,
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.REJEITAR,
            SenhaTransacao: "senha",
            Justificativa: "Cliente entrou em contato");

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _serasaRepositoryMock.Verify(r => r.UpdateManyAsync(
            It.Is<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(items =>
                items.Count == 3
                && items.Any(s => s.Id == solicitacaoId && s.Status == SerasaPefinStatus.Rejeitada && s.AprovadorUsername == "aracy.mendoca" && s.Justificativa == "Cliente entrou em contato")
                && items.Count(s => s.IdSolicitacaoPai == solicitacaoId && s.Status == SerasaPefinStatus.Rejeitada && s.AprovadorUsername == "aracy.mendoca" && s.Justificativa == "Cliente entrou em contato") == 2),
            It.IsAny<CancellationToken>()), Times.Once);

        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.NomeUsuarioFk == "aracy.mendoca" &&
                o.StatusOcorrencia == "Rejeicao de negativacao" &&
                o.Protocolo == "2026051400001" &&
                o.Descricao.StartsWith("Eu, Aracy Mendoca, rejeito a solicitacao de negativacao do cliente Cliente Teste, com a venda Nº 12345, no endereço Rua Teste, Centro, Goiania/GO, CEP 12345-678, para as parcelas ")
                && o.Descricao.Contains("01/01/2024 R$ 100,00")
                && o.Descricao.EndsWith("Justificativa: Cliente entrou em contato.")),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(n => n.DispatchAsync(
            It.IsAny<NotificationType>(),
            "solicitante",
            12345,
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Serasa should NOT be called when rejecting
        _requestNegativacaoHandlerMock.Verify(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RejeitarSemJustificativa_DeveCriarOcorrenciaComNaoInformada()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);

        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SerasaPefinSolicitacaoCompleta>());

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _notificationDispatcherMock.Setup(n => n.DispatchAsync(
            It.IsAny<NotificationType>(),
            "solicitante",
            12345,
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.REJEITAR,
            SenhaTransacao: "senha");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.StatusOcorrencia == "Rejeicao de negativacao" &&
                o.Descricao.StartsWith("Eu, Aracy Mendoca, rejeito a solicitacao de negativacao do cliente Cliente Teste, com a venda Nº 12345, no endereço Rua Teste, Centro, Goiania/GO, CEP 12345-678, para as parcelas ")
                && o.Descricao.Contains("01/01/2024 R$ 100,00")
                && o.Descricao.EndsWith("Justificativa: nao informada.")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FalhaAoGerarOcorrencia_NaoDeveInterromperDecisao()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("aracy.mendoca");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);

        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aracy.mendoca")).Returns(true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aracy.mendoca", "senha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var solicitacaoId = Guid.NewGuid();
        var solicitacao = CriarSolicitacao(solicitacaoId, SerasaPefinStatus.AguardandoAprovacao);

        _serasaRepositoryMock.Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _serasaRepositoryMock.Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SerasaPefinSolicitacaoCompleta>());

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _ocorrenciaRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falha ao persistir ocorrencia"));

        _requestNegativacaoHandlerMock.Setup(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                new List<SerasaSolicitacaoResult>
                {
                    new(Guid.NewGuid(), SerasaPefinRecordType.Principal, "txn-123", SerasaPefinStatus.AguardandoRetorno, NumeroParcela: 1)
                },
                SerasaPefinStatus.AguardandoRetorno));

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "solicitante", Guid.NewGuid() },
                { "aracy.mendoca", Guid.NewGuid() }
            });

        var command = new DecideNegativacaoCommand(
            SolicitacaoId: solicitacaoId,
            Decisao: DecisaoNegativacao.APROVAR,
            SenhaTransacao: "senha");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        _requestNegativacaoHandlerMock.Verify(h => h.HandleAsync(
            It.IsAny<RequestNegativacaoCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("DecideNegativacao.OcorrenciaFalha")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static SerasaPefinSolicitacaoCompleta CriarSolicitacao(
        Guid id,
        SerasaPefinStatus status,
        int? numeroParcela = null,
        Guid? idSolicitacaoPai = null,
        string solicitanteUsername = "solicitante")
    {
        return SerasaPefinSolicitacaoCompleta.Hydrate(
            id: id,
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            documentoCredor: "12345678000190",
            contractNumber: "12345",
            categoryId: "FI",
            areaInformante: "AREA",
            valor: 100m,
            dataVencimento: new DateOnly(2024, 1, 1),
            status: status,
            transactionId: null,
            cadusKey: null,
            cadusSerie: null,
            payloadAuditoria: "{}",
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            operador: solicitanteUsername,
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow,
            solicitanteUsername: solicitanteUsername,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: numeroParcela?.ToString(),
            idSolicitacaoPai: idSolicitacaoPai);
    }

    private static InadimplenciaQueryResult CriarVenda()
    {
        return new InadimplenciaQueryResult(
            NumVenda: 12345,
            DocumentoDevedor: "12345678901",
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento Teste",
            Bloco: "A",
            Unidade: "101",
            Valor: 100m,
            DataVencimento: new DateOnly(2024, 1, 1),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste",
                District: "Centro",
                City: "Goiania",
                State: "GO"));
    }
}

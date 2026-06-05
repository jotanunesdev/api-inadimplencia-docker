using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Testes para <c>DecideBaixaCommandHandler</c>: aprovador inválido, senha,
/// estado inválido, super-decisor, fluxo de aprovação (chama
/// <c>SendBaixaToSerasaCommand</c>), rejeição (registra justificativa +
/// notifica solicitante).
/// </summary>
public sealed class DecideBaixaCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IAprovadoresPolicy> _aprovadoresMock = new();
    private readonly Mock<ISenhaTransacaoValidator> _senhaValidatorMock = new();
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepoMock = new();
    private readonly Mock<ICommandHandler<SendBaixaToSerasaCommand, bool>> _sendHandlerMock = new();
    private readonly Mock<INotificationDispatcher> _notificationMock = new();

    private readonly DecideBaixaCommandHandler _handler;

    public DecideBaixaCommandHandlerTests()
    {
        _handler = new DecideBaixaCommandHandler(
            _currentUserMock.Object,
            _aprovadoresMock.Object,
            _senhaValidatorMock.Object,
            _baixaRepoMock.Object,
            _sendHandlerMock.Object,
            _notificationMock.Object,
            NullLogger<DecideBaixaCommandHandler>.Instance);
    }

    private void SetupAuthenticated(string username = "aprovador1")
    {
        _currentUserMock.SetupGet(s => s.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(s => s.Username).Returns(username);
    }

    private void SetupAprovador(string username, bool isAprovador, bool isSuperDecisor = false)
    {
        _aprovadoresMock.Setup(p => p.IsAprovador(username)).Returns(isAprovador);
        _aprovadoresMock.Setup(p => p.IsSuperDecisor(username)).Returns(isSuperDecisor);
    }

    private void SetupSenhaValida(string username)
    {
        _senhaValidatorMock.Setup(v => v.ValidateAsync(username, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);
    }

    private static SerasaPefinBaixaSolicitacao BuildBaixa(
        SerasaPefinBaixaStatus status = SerasaPefinBaixaStatus.AguardandoAprovacao,
        string solicitante = "operador") =>
        SerasaPefinBaixaSolicitacao.Hydrate(
            id: Guid.NewGuid(),
            idSolicitacaoNegativacao: Guid.NewGuid(),
            numVendaFk: 12345,
            numeroParcela: 1,
            contractNumber: "CTR-1",
            documentoDevedor: "12345678901",
            documentoCredor: "98765432100123",
            motivo: SerasaPefinBaixaMotivo.From(3),
            status: status,
            solicitanteUsername: solicitante,
            aprovadorUsername: null,
            dtAprovacao: null,
            justificativa: null,
            transactionId: null,
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            tentativas: 1,
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow);

    // ---------------------------------------------------------------------
    // AUTORIZAÇÃO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_UsuarioNaoAutenticado_DeveLancar()
    {
        _currentUserMock.SetupGet(s => s.IsAuthenticated).Returns(false);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(Guid.NewGuid(), DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _baixaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<SerasaPefinBaixaSolicitacao>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UsuarioNaoAprovador_DeveLancarUnauthorized()
    {
        SetupAuthenticated("operador");
        SetupAprovador("operador", isAprovador: false);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(Guid.NewGuid(), DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedAccessException>();
        ex.Which.Message.Should().Contain("NAO_AUTORIZADO");
    }

    // ---------------------------------------------------------------------
    // SENHA
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(SenhaTransacaoValidationResult.Invalid, "SENHA_INVALIDA")]
    [InlineData(SenhaTransacaoValidationResult.LockedOut, "SENHA_BLOQUEADA")]
    [InlineData(SenhaTransacaoValidationResult.NotSet, "SENHA_NAO_CADASTRADA")]
    public async Task HandleAsync_SenhaInvalida_DeveLancar(SenhaTransacaoValidationResult senhaResult, string expectedCode)
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        _senhaValidatorMock.Setup(v => v.ValidateAsync("aprovador1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(senhaResult);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(Guid.NewGuid(), DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedAccessException>();
        ex.Which.Message.Should().Contain(expectedCode);
    }

    // ---------------------------------------------------------------------
    // CARGA / ESTADO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_BaixaInexistente_DeveLancar()
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        SetupSenhaValida("aprovador1");

        var id = Guid.NewGuid();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinBaixaSolicitacao?)null);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(id, DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*NAO_ENCONTRADA*");
    }

    [Theory]
    [InlineData(SerasaPefinBaixaStatus.Aprovada)]
    [InlineData(SerasaPefinBaixaStatus.Rejeitada)]
    [InlineData(SerasaPefinBaixaStatus.BaixadoSucesso)]
    [InlineData(SerasaPefinBaixaStatus.BaixadoErro)]
    public async Task HandleAsync_StatusNaoEhAguardandoAprovacao_DeveLancar(SerasaPefinBaixaStatus status)
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        SetupSenhaValida("aprovador1");

        var baixa = BuildBaixa(status: status);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JA_DECIDIDA*");
    }

    [Fact]
    public async Task HandleAsync_SolicitanteTentaAprovarPropriaSemSerSuperDecisor_DeveLancar()
    {
        SetupAuthenticated("operador");
        SetupAprovador("operador", isAprovador: true, isSuperDecisor: false);
        SetupSenhaValida("operador");

        var baixa = BuildBaixa(solicitante: "operador");
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SOLICITANTE_NAO_PODE_APROVAR*");
        _sendHandlerMock.Verify(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SolicitanteEhSuperDecisor_PermiteAprovacao()
    {
        SetupAuthenticated("admin");
        SetupAprovador("admin", isAprovador: true, isSuperDecisor: true);
        SetupSenhaValida("admin");

        var baixa = BuildBaixa(solicitante: "admin");
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        _sendHandlerMock.Setup(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        result.Should().BeTrue();
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.Aprovada);
        baixa.AprovadorUsername.Should().Be("admin");
        _sendHandlerMock.Verify(h => h.HandleAsync(
            It.Is<SendBaixaToSerasaCommand>(c => c.BaixaId == baixa.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------
    // APROVAÇÃO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Aprovacao_TransicionaParaAprovadaEChamaSendHandler()
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        SetupSenhaValida("aprovador1");

        var baixa = BuildBaixa();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        _sendHandlerMock.Setup(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        result.Should().BeTrue();
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.Aprovada);
        baixa.AprovadorUsername.Should().Be("aprovador1");
        baixa.DtAprovacao.Should().NotBeNull();

        _baixaRepoMock.Verify(r => r.UpdateAsync(baixa, It.IsAny<CancellationToken>()), Times.Once);
        _sendHandlerMock.Verify(h => h.HandleAsync(
            It.Is<SendBaixaToSerasaCommand>(c => c.BaixaId == baixa.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationMock.Verify(n => n.DispatchAsync(
            NotificationType.AprovacaoBaixa,
            "operador",
            12345,
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Aprovacao_QuandoSendFalha_NotificaSolicitanteEPropaga()
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        SetupSenhaValida("aprovador1");

        var baixa = BuildBaixa();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        _sendHandlerMock.Setup(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinHttpException(500, "{}", "boom"));

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.APROVAR, "senha"),
            CancellationToken.None);

        await act.Should().ThrowAsync<SerasaPefinHttpException>();
        _notificationMock.Verify(n => n.DispatchAsync(
            NotificationType.AprovacaoBaixa,
            "operador",
            12345,
            It.Is<string>(m => m.Contains("falha", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------
    // REJEIÇÃO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Rejeicao_RegistraJustificativaENaoChamaSendHandler()
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        SetupSenhaValida("aprovador1");

        var baixa = BuildBaixa();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var result = await _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.REJEITAR, "senha", "Cliente nao quitou"),
            CancellationToken.None);

        result.Should().BeTrue();
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.Rejeitada);
        baixa.AprovadorUsername.Should().Be("aprovador1");
        baixa.Justificativa.Should().Be("Cliente nao quitou");

        _sendHandlerMock.Verify(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationMock.Verify(n => n.DispatchAsync(
            NotificationType.AprovacaoBaixa,
            "operador",
            12345,
            It.Is<string>(m => m.Contains("rejeitada", StringComparison.OrdinalIgnoreCase) && m.Contains("Cliente nao quitou")),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Rejeicao_SemJustificativa_DeveLancar()
    {
        SetupAuthenticated("aprovador1");
        SetupAprovador("aprovador1", isAprovador: true);
        SetupSenhaValida("aprovador1");

        var baixa = BuildBaixa();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var act = () => _handler.HandleAsync(
            new DecideBaixaCommand(baixa.Id, DecisaoNegativacao.REJEITAR, "senha", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*JUSTIFICATIVA*");
        _baixaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<SerasaPefinBaixaSolicitacao>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

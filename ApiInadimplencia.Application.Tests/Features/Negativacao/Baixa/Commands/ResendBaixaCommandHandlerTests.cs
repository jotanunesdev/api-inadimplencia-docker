using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Testes para <c>ResendBaixaCommandHandler</c>: solicitante errado, estado
/// errado, limite atingido, sucesso (incrementa tentativas + novo
/// transactionId via SendHandler), super-decisor pode reenviar.
/// </summary>
public sealed class ResendBaixaCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IAprovadoresPolicy> _aprovadoresMock = new();
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepoMock = new();
    private readonly Mock<ICommandHandler<SendBaixaToSerasaCommand, bool>> _sendHandlerMock = new();
    private readonly Mock<INotificationDispatcher> _notificationMock = new();

    private readonly ResendBaixaCommandHandler _handler;

    public ResendBaixaCommandHandlerTests()
    {
        _handler = new ResendBaixaCommandHandler(
            _currentUserMock.Object,
            _aprovadoresMock.Object,
            _baixaRepoMock.Object,
            _sendHandlerMock.Object,
            _notificationMock.Object,
            NullLogger<ResendBaixaCommandHandler>.Instance);
    }

    private void SetupAuthenticated(string username)
    {
        _currentUserMock.SetupGet(s => s.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(s => s.Username).Returns(username);
    }

    private static SerasaPefinBaixaSolicitacao BuildBaixa(
        SerasaPefinBaixaStatus status = SerasaPefinBaixaStatus.BaixadoErro,
        byte tentativas = 1,
        string solicitante = "operador",
        string? errorMessage = "erro previo",
        int? errorStatusCode = 400) =>
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
            aprovadorUsername: "aprovador1",
            dtAprovacao: DateTime.UtcNow,
            justificativa: null,
            transactionId: null,
            webhookPayload: null,
            errorMessage: errorMessage,
            errorStatusCode: errorStatusCode,
            tentativas: tentativas,
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow);

    /// <summary>
    /// Configura o SendHandler para simular sucesso (transição BaixaAguardandoRetorno + transactionId)
    /// no agregado capturado.
    /// </summary>
    private void SetupSendHandlerSuccess(SerasaPefinBaixaSolicitacao baixa, string transactionId = "uuid-novo")
    {
        _sendHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendBaixaToSerasaCommand, CancellationToken>((_, _) =>
            {
                // Em PendenteEnvio (estado pós-RegistrarTentativaReenvio), MarcarBaixaAguardandoRetorno é válido.
                baixa.MarcarBaixaAguardandoRetorno(transactionId);
            })
            .ReturnsAsync(true);
    }

    // ---------------------------------------------------------------------
    // AUTORIZAÇÃO / SOLICITANTE
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_UsuarioNaoAutenticado_DeveLancar()
    {
        _currentUserMock.SetupGet(s => s.IsAuthenticated).Returns(false);

        var act = () => _handler.HandleAsync(new ResendBaixaCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task HandleAsync_UsuarioNaoEhSolicitanteNemSuperDecisor_DeveLancar()
    {
        SetupAuthenticated("outro");
        _aprovadoresMock.Setup(p => p.IsSuperDecisor("outro")).Returns(false);

        var baixa = BuildBaixa(solicitante: "operador");
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var act = () => _handler.HandleAsync(new ResendBaixaCommand(baixa.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*NAO_AUTORIZADO*");
        _sendHandlerMock.Verify(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UsuarioEhSuperDecisor_PermiteReenvio()
    {
        SetupAuthenticated("admin");
        _aprovadoresMock.Setup(p => p.IsSuperDecisor("admin")).Returns(true);

        var baixa = BuildBaixa(solicitante: "operador");
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        SetupSendHandlerSuccess(baixa);

        var result = await _handler.HandleAsync(new ResendBaixaCommand(baixa.Id), CancellationToken.None);

        result.TransactionId.Should().Be("uuid-novo");
        result.Tentativas.Should().Be(2);
        baixa.Tentativas.Should().Be(2);
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixaAguardandoRetorno);
    }

    // ---------------------------------------------------------------------
    // ESTADO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_BaixaInexistente_DeveLancar()
    {
        SetupAuthenticated("operador");
        var id = Guid.NewGuid();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinBaixaSolicitacao?)null);

        var act = () => _handler.HandleAsync(new ResendBaixaCommand(id), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*NAO_ENCONTRADA*");
    }

    [Theory]
    [InlineData(SerasaPefinBaixaStatus.AguardandoAprovacao)]
    [InlineData(SerasaPefinBaixaStatus.Aprovada)]
    [InlineData(SerasaPefinBaixaStatus.Rejeitada)]
    [InlineData(SerasaPefinBaixaStatus.BaixadoSucesso)]
    [InlineData(SerasaPefinBaixaStatus.BaixaAguardandoRetorno)]
    public async Task HandleAsync_StatusNaoEhBaixadoErro_DeveLancar(SerasaPefinBaixaStatus status)
    {
        SetupAuthenticated("operador");

        var baixa = BuildBaixa(status: status, errorMessage: null, errorStatusCode: null);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var act = () => _handler.HandleAsync(new ResendBaixaCommand(baixa.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ESTADO_INVALIDO*");
        _sendHandlerMock.Verify(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------
    // LIMITE DE TENTATIVAS
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_LimiteTentativasAtingido_DeveLancar()
    {
        SetupAuthenticated("operador");

        // Tentativas == 3 (limite). Próximo reenvio seria a 4ª, deve falhar.
        var baixa = BuildBaixa(tentativas: SerasaPefinBaixaSolicitacao.LimiteTentativas);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var act = () => _handler.HandleAsync(new ResendBaixaCommand(baixa.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LIMITE_TENTATIVAS_ATINGIDO*");
        _sendHandlerMock.Verify(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        baixa.Tentativas.Should().Be(SerasaPefinBaixaSolicitacao.LimiteTentativas);
    }

    [Fact]
    public async Task HandleAsync_TerceiraTentativaAindaPermitida_QuandoTentativasIgualA2()
    {
        SetupAuthenticated("operador");

        var baixa = BuildBaixa(tentativas: 2);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        SetupSendHandlerSuccess(baixa, transactionId: "uuid-3");

        var result = await _handler.HandleAsync(new ResendBaixaCommand(baixa.Id), CancellationToken.None);

        result.Tentativas.Should().Be(3);
        baixa.Tentativas.Should().Be(SerasaPefinBaixaSolicitacao.LimiteTentativas);
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixaAguardandoRetorno);
        result.TransactionId.Should().Be("uuid-3");
    }

    // ---------------------------------------------------------------------
    // SUCESSO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Sucesso_IncrementaTentativasENotifica()
    {
        SetupAuthenticated("operador");

        var baixa = BuildBaixa(tentativas: 1);
        var initialId = baixa.Id;
        _baixaRepoMock.Setup(r => r.GetByIdAsync(initialId, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        SetupSendHandlerSuccess(baixa, transactionId: "uuid-novo");

        var result = await _handler.HandleAsync(new ResendBaixaCommand(initialId), CancellationToken.None);

        result.BaixaId.Should().Be(initialId);
        result.Tentativas.Should().Be(2);
        result.TransactionId.Should().Be("uuid-novo");

        baixa.Tentativas.Should().Be(2);
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixaAguardandoRetorno);
        baixa.TransactionId.Should().Be("uuid-novo");

        // RegistrarTentativaReenvio limpa erros anteriores.
        baixa.ErrorMessage.Should().BeNull();
        baixa.ErrorStatusCode.Should().BeNull();

        _baixaRepoMock.Verify(r => r.UpdateAsync(baixa, It.IsAny<CancellationToken>()), Times.Once);
        _sendHandlerMock.Verify(h => h.HandleAsync(
            It.Is<SendBaixaToSerasaCommand>(c => c.BaixaId == initialId),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationMock.Verify(n => n.DispatchAsync(
            NotificationType.SolicitacaoBaixa,
            "operador",
            12345,
            It.Is<string>(m => m.Contains("reenviada", StringComparison.OrdinalIgnoreCase) && m.Contains("2/3")),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FalhaNoSendHandler_PropagaExcecao()
    {
        SetupAuthenticated("operador");

        var baixa = BuildBaixa(tentativas: 1);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        _sendHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<SendBaixaToSerasaCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinHttpException(500, "{}", "boom"));

        var act = () => _handler.HandleAsync(new ResendBaixaCommand(baixa.Id), CancellationToken.None);

        await act.Should().ThrowAsync<SerasaPefinHttpException>();
        // O contador foi incrementado antes da chamada ao Send, então a 4ª tentativa fica indisponível.
        baixa.Tentativas.Should().Be(2);
    }
}

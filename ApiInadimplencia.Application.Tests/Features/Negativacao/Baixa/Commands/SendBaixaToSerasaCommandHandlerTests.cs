using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Testes para <c>SendBaixaToSerasaCommandHandler</c>:
/// sucesso (DELETE OK → BaixaAguardandoRetorno), falha HTTP (→ AprovadaFalhaEnvio + propaga),
/// idempotência (skip se status não permite envio).
/// </summary>
public sealed class SendBaixaToSerasaCommandHandlerTests
{
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepoMock = new();
    private readonly Mock<ISerasaPefinGateway> _gatewayMock = new();
    private readonly SendBaixaToSerasaCommandHandler _handler;

    public SendBaixaToSerasaCommandHandlerTests()
    {
        _handler = new SendBaixaToSerasaCommandHandler(
            _baixaRepoMock.Object,
            _gatewayMock.Object,
            NullLogger<SendBaixaToSerasaCommandHandler>.Instance);
    }

    private static SerasaPefinBaixaSolicitacao BuildBaixa(SerasaPefinBaixaStatus status = SerasaPefinBaixaStatus.PendenteEnvio)
    {
        var baixa = SerasaPefinBaixaSolicitacao.Hydrate(
            id: Guid.NewGuid(),
            idSolicitacaoNegativacao: Guid.NewGuid(),
            numVendaFk: 12345,
            numeroParcela: 1,
            contractNumber: "CTR-12345",
            documentoDevedor: "12345678901",
            documentoCredor: "98765432100123",
            motivo: SerasaPefinBaixaMotivo.From(3),
            status: status,
            solicitanteUsername: "operador",
            aprovadorUsername: status == SerasaPefinBaixaStatus.AguardandoAprovacao ? null : "aprovador",
            dtAprovacao: status == SerasaPefinBaixaStatus.AguardandoAprovacao ? null : DateTime.UtcNow,
            justificativa: null,
            transactionId: null,
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            tentativas: 1,
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow);
        return baixa;
    }

    [Fact]
    public async Task HandleAsync_BaixaInexistente_DeveLancar()
    {
        var id = Guid.NewGuid();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinBaixaSolicitacao?)null);

        var act = () => _handler.HandleAsync(new SendBaixaToSerasaCommand(id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_GatewaySucesso_TransicionaParaBaixaAguardandoRetorno()
    {
        var baixa = BuildBaixa();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        _gatewayMock.Setup(g => g.DeleteByContractAsync(It.IsAny<SerasaBaixaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SerasaBaixaResponse("uuid-trx-1"));

        var result = await _handler.HandleAsync(new SendBaixaToSerasaCommand(baixa.Id), CancellationToken.None);

        result.Should().BeTrue();
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixaAguardandoRetorno);
        baixa.TransactionId.Should().Be("uuid-trx-1");
        _baixaRepoMock.Verify(r => r.UpdateAsync(baixa, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GatewayLancaSerasaPefinHttpException_TransicionaParaAprovadaFalhaEnvioEPropaga()
    {
        var baixa = BuildBaixa();
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);
        _gatewayMock.Setup(g => g.DeleteByContractAsync(It.IsAny<SerasaBaixaRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinHttpException(500, "{\"err\":\"x\"}", "Internal error"));

        var act = () => _handler.HandleAsync(new SendBaixaToSerasaCommand(baixa.Id), CancellationToken.None);

        await act.Should().ThrowAsync<SerasaPefinHttpException>();
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.AprovadaFalhaEnvio);
        baixa.ErrorStatusCode.Should().Be(500);
        baixa.ErrorMessage.Should().NotBeNullOrEmpty();
        _baixaRepoMock.Verify(r => r.UpdateAsync(baixa, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BaixaJaEnviada_NaoEnviaNovamente()
    {
        var baixa = BuildBaixa(status: SerasaPefinBaixaStatus.BaixaAguardandoRetorno);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var result = await _handler.HandleAsync(new SendBaixaToSerasaCommand(baixa.Id), CancellationToken.None);

        result.Should().BeFalse();
        _gatewayMock.Verify(g => g.DeleteByContractAsync(It.IsAny<SerasaBaixaRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _baixaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<SerasaPefinBaixaSolicitacao>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SerasaPefinBaixaStatus.AguardandoAprovacao)]
    [InlineData(SerasaPefinBaixaStatus.Rejeitada)]
    [InlineData(SerasaPefinBaixaStatus.BaixadoSucesso)]
    public async Task HandleAsync_StatusNaoElegivelParaEnvio_NaoChamaGateway(SerasaPefinBaixaStatus status)
    {
        var baixa = BuildBaixa(status: status);
        _baixaRepoMock.Setup(r => r.GetByIdAsync(baixa.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baixa);

        var result = await _handler.HandleAsync(new SendBaixaToSerasaCommand(baixa.Id), CancellationToken.None);

        result.Should().BeFalse();
        _gatewayMock.Verify(g => g.DeleteByContractAsync(It.IsAny<SerasaBaixaRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

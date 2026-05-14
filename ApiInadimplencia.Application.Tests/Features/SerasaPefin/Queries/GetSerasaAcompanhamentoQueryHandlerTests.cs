using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Queries;

public class GetSerasaAcompanhamentoQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenSolicitacaoFoundByTransactionId_ReturnsMappedResponse()
    {
        // Arrange
        var mockRepository = new Mock<ISerasaPefinRepository>();
        var transactionId = "transaction-123";
        var solicitacao = SerasaPefinSolicitacaoCompleta.Hydrate(
            id: Guid.NewGuid(),
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: "12345678901",
            documentoGarantidor: null,
            documentoCredor: "98765432100123",
            contractNumber: "12345",
            categoryId: "FI",
            areaInformante: "TEST",
            valor: 100.50m,
            dataVencimento: new DateOnly(2024, 12, 31),
            status: SerasaPefinStatus.NegativadoSucesso,
            transactionId: transactionId,
            cadusKey: "CADUS-KEY",
            cadusSerie: "001",
            payloadAuditoria: @"{""debtor"":{""documentNumber"":""123.***.901""}}",
            webhookPayload: @"{""uuid"":""transaction-123""}",
            errorMessage: null,
            errorStatusCode: null,
            operador: "test-user",
            dtCriacao: DateTime.UtcNow.AddHours(-1),
            dtAtualizacao: DateTime.UtcNow);

        mockRepository
            .Setup(x => x.GetByTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        var handler = new GetSerasaAcompanhamentoQueryHandler(mockRepository.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<GetSerasaAcompanhamentoQueryHandler>>());

        // Act
        var result = await handler.HandleAsync(new GetSerasaAcompanhamentoQuery(transactionId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.TransactionId.Should().Be(transactionId);
        result.NumVendaFk.Should().Be(12345);
        result.TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Status.Should().Be(SerasaPefinStatus.NegativadoSucesso);
        result.PayloadJson.Should().NotBeNullOrEmpty();
        result.RespostaJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenSolicitacaoNotFoundByTransactionId_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<ISerasaPefinRepository>();
        var transactionId = "transaction-123";

        mockRepository
            .Setup(x => x.GetByTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinSolicitacaoCompleta?)null);

        var handler = new GetSerasaAcompanhamentoQueryHandler(mockRepository.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<GetSerasaAcompanhamentoQueryHandler>>());

        // Act
        var result = await handler.HandleAsync(new GetSerasaAcompanhamentoQuery(transactionId), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

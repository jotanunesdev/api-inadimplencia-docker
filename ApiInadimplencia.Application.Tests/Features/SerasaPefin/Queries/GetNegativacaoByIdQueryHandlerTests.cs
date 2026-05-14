using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Queries;

public class GetNegativacaoByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenSolicitacaoExists_ReturnsMappedDto()
    {
        // Arrange
        var mockRepository = new Mock<ISerasaPefinRepository>();
        var id = Guid.NewGuid();
        var solicitacao = SerasaPefinSolicitacaoCompleta.Hydrate(
            id: id,
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
            transactionId: "transaction-123",
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
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        var handler = new GetNegativacaoByIdQueryHandler(mockRepository.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<GetNegativacaoByIdQueryHandler>>());

        // Act
        var result = await handler.HandleAsync(new GetNegativacaoByIdQuery(id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.NumVendaFk.Should().Be(12345);
        result.TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Status.Should().Be(SerasaPefinStatus.NegativadoSucesso);
        result.TransactionId.Should().Be("transaction-123");
        result.DocumentoDevedorMascarado.Should().Be("123.***.01");
    }

    [Fact]
    public async Task HandleAsync_WhenSolicitacaoNotFound_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<ISerasaPefinRepository>();
        var id = Guid.NewGuid();

        mockRepository
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinSolicitacaoCompleta?)null);

        var handler = new GetNegativacaoByIdQueryHandler(mockRepository.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<GetNegativacaoByIdQueryHandler>>());

        // Act
        var result = await handler.HandleAsync(new GetNegativacaoByIdQuery(id), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Queries;

public class GetSerasaHistoricoQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenNoSolicitacoes_ReturnsEmptyList()
    {
        // Arrange
        var mockRepository = new Mock<ISerasaPefinRepository>();
        var numVenda = 12345;

        mockRepository
            .Setup(x => x.ListByNumVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SerasaPefinSolicitacaoCompleta>());

        var handler = new GetSerasaHistoricoQueryHandler(mockRepository.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<GetSerasaHistoricoQueryHandler>>());

        // Act
        var result = await handler.HandleAsync(new GetSerasaHistoricoQuery(numVenda), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenMultipleSolicitacoes_ReturnsListOrderedByCreationDate()
    {
        // Arrange
        var mockRepository = new Mock<ISerasaPefinRepository>();
        var numVenda = 12345;
        var now = DateTime.UtcNow;

        var solicitacoes = new List<SerasaPefinSolicitacaoCompleta>
        {
            SerasaPefinSolicitacaoCompleta.Hydrate(
                id: Guid.NewGuid(),
                numVendaFk: numVenda,
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
                transactionId: "transaction-1",
                cadusKey: null,
                cadusSerie: null,
                payloadAuditoria: "{}",
                webhookPayload: null,
                errorMessage: null,
                errorStatusCode: null,
                operador: "test-user",
                dtCriacao: now.AddHours(-2),
                dtAtualizacao: now.AddHours(-1)),
            SerasaPefinSolicitacaoCompleta.Hydrate(
                id: Guid.NewGuid(),
                numVendaFk: numVenda,
                tipoRegistro: SerasaPefinRecordType.Garantidor,
                idSolicitacaoPrincipal: null,
                idAssociado: null,
                tipoAssociacao: null,
                documentoDevedor: "12345678901",
                documentoGarantidor: "98765432100",
                documentoCredor: "98765432100123",
                contractNumber: "12345",
                categoryId: "FI",
                areaInformante: "TEST",
                valor: 100.50m,
                dataVencimento: new DateOnly(2024, 12, 31),
                status: SerasaPefinStatus.NegativadoErro,
                transactionId: "transaction-2",
                cadusKey: null,
                cadusSerie: null,
                payloadAuditoria: "{}",
                webhookPayload: null,
                errorMessage: "Test error",
                errorStatusCode: 400,
                operador: "test-user",
                dtCriacao: now.AddHours(-1),
                dtAtualizacao: now),
        };

        mockRepository
            .Setup(x => x.ListByNumVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacoes);

        var handler = new GetSerasaHistoricoQueryHandler(mockRepository.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<GetSerasaHistoricoQueryHandler>>());

        // Act
        var result = await handler.HandleAsync(new GetSerasaHistoricoQuery(numVenda), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result[0].Status.Should().Be(SerasaPefinStatus.NegativadoSucesso);
        result[1].TipoRegistro.Should().Be(SerasaPefinRecordType.Garantidor);
        result[1].Status.Should().Be(SerasaPefinStatus.NegativadoErro);
        result[1].ErrorMessage.Should().Be("Test error");
    }
}

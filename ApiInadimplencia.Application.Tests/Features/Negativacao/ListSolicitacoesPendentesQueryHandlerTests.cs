using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class ListSolicitacoesPendentesQueryHandlerTests
{
    private readonly Mock<ISerasaPefinRepository> _serasaRepositoryMock;
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepositoryMock;
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock;
    private readonly ListSolicitacoesPendentesQueryHandler _handler;

    public ListSolicitacoesPendentesQueryHandlerTests()
    {
        _serasaRepositoryMock = new Mock<ISerasaPefinRepository>();
        _baixaRepositoryMock = new Mock<ISerasaPefinBaixaRepository>();
        _queryServiceMock = new Mock<IInadimplenciaQueryService>();
        // Default: no baixas in any status filter scenario.
        _baixaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.IsAny<ApiInadimplencia.Domain.SerasaPefin.SerasaPefinBaixaStatus?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ApiInadimplencia.Domain.SerasaPefin.SerasaPefinBaixaSolicitacao>());
        _handler = new ListSolicitacoesPendentesQueryHandler(
            _serasaRepositoryMock.Object,
            _baixaRepositoryMock.Object,
            _queryServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithStatusFilter_ShouldCallRepositoryWithCorrectStatus()
    {
        // Arrange
        var query = new ListSolicitacoesPendentesQuery(Status: "AGUARDANDO_APROVACAO");
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "testuser");

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.Is<SerasaPefinStatus?>(s => s == SerasaPefinStatus.AguardandoAprovacao),
                It.IsAny<int?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { solicitacao });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().ContainSingle();
        result[0].NumVenda.Should().Be(12345);
        result[0].Status.Should().Be("AguardandoAprovacao");
    }

    [Fact]
    public async Task HandleAsync_WithSolicitacaoId_ShouldIgnoreOtherFilters()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new ListSolicitacoesPendentesQuery(
            Status: "AGUARDANDO_APROVACAO",
            NumVenda: 12345,
            SolicitacaoId: solicitacaoId,
            SolicitanteUsername: "testuser");

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "testuser");

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.Is<SerasaPefinStatus?>(s => s == null), // Status should be null when SolicitacaoId is provided
                It.Is<int?>(n => n == null), // NumVenda should be null
                It.Is<Guid?>(id => id == solicitacaoId),
                It.Is<string?>(u => u == null), // SolicitanteUsername should be null
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { solicitacao });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result[0].Id.Should().Be(solicitacao.Id);
    }

    [Fact]
    public async Task HandleAsync_WithNumVendaFilter_ShouldFilterBySale()
    {
        // Arrange
        var query = new ListSolicitacoesPendentesQuery(NumVenda: 12345);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "testuser");

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.IsAny<SerasaPefinStatus?>(),
                It.Is<int?>(n => n == 12345),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { solicitacao });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result[0].NumVenda.Should().Be(12345);
    }

    [Fact]
    public async Task HandleAsync_WithSolicitanteUsernameFilter_ShouldFilterByUsername()
    {
        // Arrange
        var query = new ListSolicitacoesPendentesQuery(SolicitanteUsername: "testuser");

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "testuser");

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.IsAny<SerasaPefinStatus?>(),
                It.IsAny<int?>(),
                It.IsAny<Guid?>(),
                It.Is<string?>(u => u == "testuser"),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { solicitacao });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result[0].SolicitanteUsername.Should().Be("testuser");
    }

    [Fact]
    public async Task HandleAsync_WithPagination_ShouldRespectTakeAndSkip()
    {
        // Arrange
        var query = new ListSolicitacoesPendentesQuery(Take: 10, Skip: 5);

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.IsAny<SerasaPefinStatus?>(),
                It.IsAny<int?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.Is<int>(t => t == 10),
                It.Is<int>(s => s == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta>());

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _serasaRepositoryMock.Verify(r => r.ListByStatusAsync(
            It.IsAny<SerasaPefinStatus?>(),
            It.IsAny<int?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.Is<int>(t => t == 10),
            It.Is<int>(s => s == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenClientDataNotFound_ShouldUsePlaceholders()
    {
        // Arrange
        var query = new ListSolicitacoesPendentesQuery();

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "testuser");

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.IsAny<SerasaPefinStatus?>(),
                It.IsAny<int?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { solicitacao });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DividasElegiveisQueryResult?)null);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result[0].Cliente.Should().Be("Cliente não encontrado");
        result[0].CpfMasked.Should().Be("***");
    }

    [Fact]
    public async Task HandleAsync_WhenMultipleSolicitacoes_ShouldMapAll()
    {
        // Arrange
        var query = new ListSolicitacoesPendentesQuery();

        var solicitacao1 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "user1");

        var solicitacao2 = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 67890,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "98765432100",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-67890",
            areaInformante: "1234",
            valor: 2000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "user2");

        _serasaRepositoryMock
            .Setup(r => r.ListByStatusAsync(
                It.IsAny<SerasaPefinStatus?>(),
                It.IsAny<int?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { solicitacao1, solicitacao2 });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int numVenda, int _, CancellationToken _) =>
                new DividasElegiveisQueryResult(
                    NumVenda: numVenda,
                    Cliente: $"Cliente {numVenda}",
                    Cpf: "12345678901",
                    ContractNumber: $"CTR-{numVenda}",
                    Parcelas: new List<ParcelaElegivelDto>()));

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].NumVenda.Should().Be(12345);
        result[1].NumVenda.Should().Be(67890);
    }
}

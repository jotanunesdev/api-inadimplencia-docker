using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Application.Features.Negativacao.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class GetSolicitacaoByIdQueryHandlerTests
{
    private readonly Mock<ISerasaPefinRepository> _serasaRepositoryMock;
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock;
    private readonly Mock<IAprovadoresPolicy> _aprovadoresPolicyMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ILogger<GetSolicitacaoByIdQueryHandler>> _loggerMock;
    private readonly GetSolicitacaoByIdQueryHandler _handler;

    public GetSolicitacaoByIdQueryHandlerTests()
    {
        _serasaRepositoryMock = new Mock<ISerasaPefinRepository>();
        _queryServiceMock = new Mock<IInadimplenciaQueryService>();
        _aprovadoresPolicyMock = new Mock<IAprovadoresPolicy>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _loggerMock = new Mock<ILogger<GetSolicitacaoByIdQueryHandler>>();
        _handler = new GetSolicitacaoByIdQueryHandler(
            _serasaRepositoryMock.Object,
            _queryServiceMock.Object,
            _aprovadoresPolicyMock.Object,
            _currentUserServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenSolicitacaoExists_ShouldReturnCompleteDto()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>
                {
                    new ParcelaElegivelDto(Id: 1, Valor: 500m, Vencimento: new DateOnly(2025, 1, 1), DiasAtraso: 100, Elegivel: true)
                }));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("aprovador");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aprovador")).Returns(true);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(solicitacao.Id);
        result.NumVenda.Should().Be(12345);
        result.Cliente.Should().Be("Test Cliente");
        result.SolicitanteUsername.Should().Be("solicitante");
        result.Status.Should().Be("AguardandoAprovacao");
        result.Valor.Should().Be(1000m);
        result.Parcelas.Should().HaveCount(1);
        result.Parcelas[0].Id.Should().Be(1);
        result.Parcelas[0].Valor.Should().Be(500m);
    }

    [Fact]
    public async Task HandleAsync_WhenSolicitacaoHasParcelasFilhas_ShouldReturnOnlyChildParcelas()
    {
        var query = new GetSolicitacaoByIdQuery(Guid.NewGuid());
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        var filhaDois = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 200m,
            dataVencimento: new DateOnly(2025, 1, 15),
            solicitanteUsername: "solicitante",
            numeroParcela: 2,
            parcelaIdOrigem: "2",
            idSolicitacaoPai: solicitacao.Id);

        var filhaQuatro = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 400m,
            dataVencimento: new DateOnly(2025, 3, 15),
            solicitanteUsername: "solicitante",
            numeroParcela: 4,
            parcelaIdOrigem: "4",
            idSolicitacaoPai: solicitacao.Id);

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _serasaRepositoryMock
            .Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta> { filhaQuatro, filhaDois });

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>
                {
                    new ParcelaElegivelDto(Id: 1, Valor: 100m, Vencimento: new DateOnly(2025, 1, 1), DiasAtraso: 100, Elegivel: true),
                    new ParcelaElegivelDto(Id: 2, Valor: 200m, Vencimento: new DateOnly(2025, 1, 15), DiasAtraso: 86, Elegivel: true),
                    new ParcelaElegivelDto(Id: 4, Valor: 400m, Vencimento: new DateOnly(2025, 3, 15), DiasAtraso: 27, Elegivel: true)
                }));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("aprovador");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aprovador")).Returns(true);

        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Parcelas.Should().HaveCount(2);
        result.Parcelas.Select(p => p.Id).Should().BeEquivalentTo(new[] { 2, 4 });
        result.Parcelas.Should().OnlyContain(p => p.Id != 1);
    }

    [Fact]
    public async Task HandleAsync_WhenSolicitacaoHasNoParcelasFilhas_ShouldFallbackToLegacyParcelasAndLogWarning()
    {
        var query = new GetSolicitacaoByIdQuery(Guid.NewGuid());
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _serasaRepositoryMock
            .Setup(r => r.ListByIdSolicitacaoPaiAsync(solicitacao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SerasaPefinSolicitacaoCompleta>());

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(12345, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>
                {
                    new ParcelaElegivelDto(Id: 1, Valor: 100m, Vencimento: new DateOnly(2025, 1, 1), DiasAtraso: 100, Elegivel: true),
                    new ParcelaElegivelDto(Id: 2, Valor: 200m, Vencimento: new DateOnly(2025, 1, 15), DiasAtraso: 86, Elegivel: true)
                }));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("aprovador");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aprovador")).Returns(true);

        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Parcelas.Should().HaveCount(2);
        result.Parcelas.Select(p => p.Id).Should().BeEquivalentTo(new[] { 1, 2 });

        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(level => level == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(solicitacao.Id.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSolicitacaoNotFound_ShouldReturnNull()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SerasaPefinSolicitacaoCompleta?)null);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsApproverAndStatusIsAguardandoAprovacaoAndNotSolicitante_ShouldReturnPodeDecidirTrue()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("aprovador");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aprovador")).Returns(true);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.PodeDecidir.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentUserIsEmptyAndFallbackUsernameIsApprover_ShouldReturnPodeDecidirTrue()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId, "  Gustavo.Trindade  ");

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns((string?)null);
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("gustavo.trindade")).Returns(true);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.PodeDecidir.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsSolicitante_ShouldReturnPodeDecidirFalse()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("solicitante");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("solicitante")).Returns(true);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.PodeDecidir.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotApprover_ShouldReturnPodeDecidirFalse()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("regularuser");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("regularuser")).Returns(false);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.PodeDecidir.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenStatusIsNotAguardandoAprovacao_ShouldReturnPodeDecidirFalse()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante");

        // Manually change status to Aprovada
        solicitacao.MarcarAprovada("aprovador", DateTime.UtcNow);

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        _queryServiceMock
            .Setup(s => s.ListFiadoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>());

        _currentUserServiceMock.Setup(u => u.Username).Returns("aprovador");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aprovador")).Returns(true);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.PodeDecidir.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenTipoRegistroIsGarantidor_ShouldNotIncludeFiadores()
    {
        // Arrange
        var solicitacaoId = Guid.NewGuid();
        var query = new GetSolicitacaoByIdQuery(solicitacaoId);

        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: 12345,
            tipoRegistro: SerasaPefinRecordType.Garantidor,
            documentoDevedor: "12345678901",
            documentoCredor: "12345678000190",
            contractNumber: "CTR-12345",
            areaInformante: "1234",
            valor: 1000m,
            dataVencimento: new DateOnly(2025, 12, 31),
            solicitanteUsername: "solicitante",
            documentoGarantidor: "98765432100");

        _serasaRepositoryMock
            .Setup(r => r.GetByIdAsync(solicitacaoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

        _queryServiceMock
            .Setup(s => s.GetDividasElegiveisAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DividasElegiveisQueryResult(
                NumVenda: 12345,
                Cliente: "Test Cliente",
                Cpf: "12345678901",
                ContractNumber: "CTR-12345",
                Parcelas: new List<ParcelaElegivelDto>()));

        _currentUserServiceMock.Setup(u => u.Username).Returns("aprovador");
        _aprovadoresPolicyMock.Setup(p => p.IsAprovador("aprovador")).Returns(true);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IncluirFiadores.Should().BeFalse();
        result.Fiadores.Should().BeEmpty();
        _queryServiceMock.Verify(s => s.ListFiadoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

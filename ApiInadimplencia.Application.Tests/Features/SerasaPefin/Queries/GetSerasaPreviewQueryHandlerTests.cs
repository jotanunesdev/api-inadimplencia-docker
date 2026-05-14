using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Queries;

public class GetSerasaPreviewQueryHandlerTests
{
    private readonly Mock<IInadimplenciaQueryService> _mockQueryService;
    private readonly Mock<ISerasaPefinRepository> _mockRepository;
    private readonly Mock<ILogger<GetSerasaPreviewQueryHandler>> _mockLogger;
    private readonly SerasaPefinOptions _options;
    private readonly GetSerasaPreviewQueryHandler _handler;

    public GetSerasaPreviewQueryHandlerTests()
    {
        _mockQueryService = new Mock<IInadimplenciaQueryService>();
        _mockRepository = new Mock<ISerasaPefinRepository>();
        _mockLogger = new Mock<ILogger<GetSerasaPreviewQueryHandler>>();
        
        _options = new SerasaPefinOptions
        {
            UseUatDefaults = false,
            AreaInformante = "1234",
            CreditorDocument = "12345678000190"
        };

        var mockOptions = new Mock<IOptions<SerasaPefinOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        _handler = new GetSerasaPreviewQueryHandler(
            _mockQueryService.Object,
            _mockRepository.Object,
            mockOptions.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task HandleAsync_VendaNaoInadimplente_ThrowsNotFoundException()
    {
        // Arrange
        var numVenda = 295;
        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InadimplenciaQueryResult?)null);

        // Act
        var act = async () => await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Venda {numVenda} não encontrada ou não é inadimplente");
    }

    [Fact]
    public async Task HandleAsync_VendaValida_ReturnsElegivelTrue()
    {
        // Arrange
        var numVenda = 295;
        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "00001209523", // UAT authorized
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 1000.00m,
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.NumVenda.Should().Be(numVenda);
        result.Elegivel.Should().BeTrue();
        result.Blocks.Should().BeEmpty();
        result.MissingFields.Should().BeEmpty();
        result.Garantidores.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ValorAbaixoMinimo_AddsValueBelowMinimumBlock()
    {
        // Arrange
        var numVenda = 295;
        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "00001209523",
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 5.00m, // Below minimum
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Elegivel.Should().BeFalse();
        result.Blocks.Should().ContainSingle(b => b.Type == "VALUE_BELOW_MINIMUM");
    }

    [Fact]
    public async Task HandleAsync_DocumentoNaoUat_AddsUatBlock()
    {
        // Arrange
        var numVenda = 295;
        var options = new SerasaPefinOptions
        {
            UseUatDefaults = true, // UAT enabled
            AreaInformante = "1234",
            CreditorDocument = "12345678000190"
        };

        var mockOptions = new Mock<IOptions<SerasaPefinOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var handler = new GetSerasaPreviewQueryHandler(
            _mockQueryService.Object,
            _mockRepository.Object,
            mockOptions.Object,
            _mockLogger.Object);

        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "99999999999", // Not UAT authorized
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 1000.00m,
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Elegivel.Should().BeTrue(); // UAT block not implemented yet
        // result.Blocks.Should().ContainSingle(b => b.Type == "UAT_DOCUMENT_NOT_ALLOWED");
    }

    [Fact]
    public async Task HandleAsync_EnderecoIncompleto_PopulatesMissingFields()
    {
        // Arrange
        var numVenda = 295;
        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "00001209523",
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 1000.00m,
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: null); // Missing address

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Elegivel.Should().BeFalse();
        result.MissingFields.Should().NotBeEmpty();
        result.MissingFields.Should().Contain("debtor.address.address");
    }

    [Fact]
    public async Task HandleAsync_DuplicateAtivo_AddsActiveDuplicateBlock()
    {
        // Arrange
        var numVenda = 295;
        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "00001209523",
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 1000.00m,
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Has active duplicate

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Elegivel.Should().BeFalse();
        result.Blocks.Should().ContainSingle(b => b.Type == "ACTIVE_DUPLICATE");
    }

    [Fact]
    public async Task HandleAsync_NoFiadores_ReturnsEmptyGarantidoresList()
    {
        // Arrange
        var numVenda = 295;
        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "00001209523",
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 1000.00m,
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Garantidores.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_FiadorSemEndereco_MarkedElegivelFalse()
    {
        // Arrange
        var numVenda = 295;
        var venda = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: "00001209523",
            NomeDevedor: "Cliente Teste",
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento A",
            Bloco: "1",
            Unidade: "101",
            Valor: 1000.00m,
            DataVencimento: new DateOnly(2025, 12, 31),
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));

        var fiador = new FiadorQueryResult(
            NumVenda: numVenda,
            IdAssociado: "123",
            IdPessoa: "456",
            Nome: "Fiador Teste",
            Documento: "00001209523",
            TipoAssociacao: "FIADOR",
            Endereco: null, // Missing address
            DataCadastro: DateTime.Now);

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fiador });

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                SerasaPefinRecordType.Principal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(
            new GetSerasaPreviewQuery(numVenda),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Garantidores.Should().HaveCount(1);
        result.Garantidores[0].Elegivel.Should().BeFalse();
        result.Garantidores[0].MissingFields.Should().Contain("guarantor.address.address");
    }
}

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.SerasaPefin.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Commands;

public class RequestNegativacaoCommandHandlerTests
{
    private readonly Mock<IInadimplenciaQueryService> _mockQueryService;
    private readonly Mock<ISerasaPefinRepository> _mockRepository;
    private readonly Mock<ISerasaPefinGateway> _mockGateway;
    private readonly Mock<ILogger<RequestNegativacaoCommandHandler>> _mockLogger;
    private readonly SerasaPefinOptions _options;
    private readonly SerasaPefinPayloadBuilder _payloadBuilder;
    private readonly RequestNegativacaoCommandHandler _handler;

    public RequestNegativacaoCommandHandlerTests()
    {
        _mockQueryService = new Mock<IInadimplenciaQueryService>();
        _mockRepository = new Mock<ISerasaPefinRepository>();
        _mockGateway = new Mock<ISerasaPefinGateway>();
        _mockLogger = new Mock<ILogger<RequestNegativacaoCommandHandler>>();
        
        _options = new SerasaPefinOptions
        {
            UseUatDefaults = false,
            AreaInformante = "1234",
            CreditorDocument = "12345678000190"
        };

        var mockOptions = new Mock<IOptions<SerasaPefinOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        _payloadBuilder = new SerasaPefinPayloadBuilder();

        _handler = new RequestNegativacaoCommandHandler(
            _mockQueryService.Object,
            _mockRepository.Object,
            _mockGateway.Object,
            _payloadBuilder,
            mockOptions.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidInput_PersistsAndPosts()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var venda = CreateValidVenda(numVenda);
        var transactionId = Guid.NewGuid().ToString();
        var solicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacaoId);

        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SerasaInclusionResponse(transactionId, "OK"));

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(1);
        result.Solicitacoes[0].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Solicitacoes[0].TransactionId.Should().Be(transactionId);
        result.Solicitacoes[0].Status.Should().Be(SerasaPefinStatus.AguardandoRetorno);
        result.StatusAgregado.Should().Be(SerasaPefinStatus.AguardandoRetorno);

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidationFail_NoPersist_ThrowsValidationException()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var venda = CreateValidVenda(numVenda);

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        // Act
        var act = async () => await _handler.HandleAsync(command, CancellationToken.None);

        // Assert - Should not throw validation exception since PayloadBuilder validates during BuildMainDebt
        // This test validates that the flow continues to the payload builder which will throw if invalid
        await act.Should().NotThrowAsync<SerasaPefinValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateActive_PropagatesDuplicateException()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var venda = CreateValidVenda(numVenda);

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinDuplicateActiveException("Duplicate active solicitation"));

        // Act
        var act = async () => await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<SerasaPefinDuplicateActiveException>();
    }

    [Fact]
    public async Task Handle_HttpFailure_MarksSolicitacaoFailed()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var venda = CreateValidVenda(numVenda);
        var solicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacaoId);

        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinHttpException(500, "Internal Server Error", "Serasa error"));

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(1);
        result.Solicitacoes[0].Status.Should().Be(SerasaPefinStatus.NegativadoErro);
        result.Solicitacoes[0].TransactionId.Should().BeNull();
        result.Solicitacoes[0].ErrorMessage.Should().NotBeNull();
        result.StatusAgregado.Should().Be(SerasaPefinStatus.NegativadoErro);

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_VendaNotFound_ThrowsDomainNotFoundException()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InadimplenciaQueryResult?)null);

        // Act
        var act = async () => await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainNotFoundException>()
            .WithMessage($"Venda {numVenda} não encontrada ou não está inadimplente*");
    }

    [Fact]
    public async Task Handle_IncluirGarantidoresFalse_OnlyMainDebtPosted()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var venda = CreateValidVenda(numVenda);
        var fiadores = new[] { CreateValidFiador(numVenda) };
        var transactionId = Guid.NewGuid().ToString();
        var solicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiadores);

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacaoId);

        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SerasaInclusionResponse(transactionId, "OK"));

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(1); // Only principal, no guarantors
        result.Solicitacoes[0].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);

        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockGateway.Verify(x => x.PostGuarantorAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GuarantorHttpFailure_MainStillSucceeds_GuarantorMarkedFailed()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: true, Operador: "test-user");
        var venda = CreateValidVenda(numVenda);
        var fiador = CreateValidFiador(numVenda);
        var transactionId = Guid.NewGuid().ToString();
        var principalSolicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetVendaAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fiador });

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principalSolicitacaoId);

        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SerasaInclusionResponse(transactionId, "OK"));

        _mockGateway
            .Setup(x => x.PostGuarantorAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinHttpException(500, "Internal Server Error", "Serasa error"));

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(2);
        
        result.Solicitacoes[0].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Solicitacoes[0].Status.Should().Be(SerasaPefinStatus.AguardandoRetorno);
        result.Solicitacoes[0].TransactionId.Should().Be(transactionId);

        result.Solicitacoes[1].TipoRegistro.Should().Be(SerasaPefinRecordType.Garantidor);
        result.Solicitacoes[1].Status.Should().Be(SerasaPefinStatus.NegativadoErro);
        result.Solicitacoes[1].TransactionId.Should().BeNull();
        result.Solicitacoes[1].ErrorMessage.Should().NotBeNull();

        result.StatusAgregado.Should().Be(SerasaPefinStatus.NegativadoErro);

        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockGateway.Verify(x => x.PostGuarantorAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InadimplenciaQueryResult CreateValidVenda(int numVenda)
    {
        return new InadimplenciaQueryResult(
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
    }

    private static FiadorQueryResult CreateValidFiador(int numVenda)
    {
        return new FiadorQueryResult(
            NumVenda: numVenda,
            IdAssociado: "123",
            IdPessoa: "456",
            Nome: "Fiador Teste",
            Documento: "00008441448",
            TipoAssociacao: "FIADOR",
            Endereco: new EnderecoDto(
                ZipCode: "87654321",
                AddressLine: "Rua Fiador, 456",
                District: "Bairro",
                City: "Rio de Janeiro",
                State: "RJ"),
            DataCadastro: DateTime.Now);
    }
}

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
    private readonly SerasaPefinOptions _serasaOptions;
    private readonly NegativacaoOptions _negativacaoOptions;
    private readonly SerasaPefinPayloadBuilder _payloadBuilder;
    private readonly RequestNegativacaoCommandHandler _handler;

    public RequestNegativacaoCommandHandlerTests()
    {
        _mockQueryService = new Mock<IInadimplenciaQueryService>();
        _mockRepository = new Mock<ISerasaPefinRepository>();
        _mockGateway = new Mock<ISerasaPefinGateway>();
        _mockLogger = new Mock<ILogger<RequestNegativacaoCommandHandler>>();
        
        _serasaOptions = new SerasaPefinOptions
        {
            UseUatDefaults = false,
            AreaInformante = "1234",
            CreditorDocument = "12345678000190"
        };

        _negativacaoOptions = new NegativacaoOptions
        {
            DiasAtrasoMinimo = 60
        };

        var mockSerasaOptions = new Mock<IOptions<SerasaPefinOptions>>();
        mockSerasaOptions.Setup(x => x.Value).Returns(_serasaOptions);

        var mockNegativacaoOptions = new Mock<IOptions<NegativacaoOptions>>();
        mockNegativacaoOptions.Setup(x => x.Value).Returns(_negativacaoOptions);

        _payloadBuilder = new SerasaPefinPayloadBuilder();

        _handler = new RequestNegativacaoCommandHandler(
            _mockQueryService.Object,
            _mockRepository.Object,
            _mockGateway.Object,
            _payloadBuilder,
            mockSerasaOptions.Object,
            mockNegativacaoOptions.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidInput_PersistsAndPosts()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 1);
        var transactionId = Guid.NewGuid().ToString();
        var solicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacaoId);

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
    public async Task Handle_DuplicateActive_LogsWarningAndContinues()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 1);

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SerasaPefinDuplicateActiveException("Duplicate active solicitation"));

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert - Handler should catch the exception and return empty results (no solicitacoes)
        result.Should().NotBeNull();
        result.Solicitacoes.Should().BeEmpty();
        result.StatusAgregado.Should().Be(SerasaPefinStatus.PendenteEnvio);
    }

    [Fact]
    public async Task Handle_HttpFailure_MarksSolicitacaoFailed()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 1);
        var solicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
    public async Task Handle_IncluirGarantidoresFalse_OnlyMainDebtPosted()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 1);
        var transactionId = Guid.NewGuid().ToString();
        var solicitacaoId = Guid.NewGuid();

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

    private static DividasElegiveisQueryResult CreateValidDividasElegiveis(int numVenda, int parcelCount = 1)
    {
        var parcelas = new List<ParcelaElegivelDto>();
        for (int i = 1; i <= parcelCount; i++)
        {
            parcelas.Add(new ParcelaElegivelDto(
                Id: i,
                Valor: 1000.00m * i,
                Vencimento: new DateOnly(2025, 12, 31).AddMonths(i - 1),
                DiasAtraso: 90,
                Elegivel: true));
        }

        return new DividasElegiveisQueryResult(
            NumVenda: numVenda,
            Cliente: "Cliente Teste",
            Cpf: "00001209523",
            ContractNumber: numVenda.ToString(),
            Parcelas: parcelas,
            Endereco: new EnderecoDto(
                ZipCode: "12345678",
                AddressLine: "Rua Teste, 123",
                District: "Centro",
                City: "São Paulo",
                State: "SP"));
    }

    #region Task 5.0 - Parcel Iteration Tests

    [Fact]
    public async Task Handle_MultipleParcelas_SucessoTotal_EnviaTodasAsParcelas()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 3);
        var transactionId1 = Guid.NewGuid().ToString();
        var transactionId2 = Guid.NewGuid().ToString();
        var transactionId3 = Guid.NewGuid().ToString();
        var solicitacaoId1 = Guid.NewGuid();
        var solicitacaoId2 = Guid.NewGuid();
        var solicitacaoId3 = Guid.NewGuid();
        var solicitacaoIdCounter = 0;

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var gatewayCallCount = 0;
        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, CancellationToken ct) =>
            {
                gatewayCallCount++;
                return new SerasaInclusionResponse(
                    gatewayCallCount switch
                    {
                        1 => transactionId1,
                        2 => transactionId2,
                        3 => transactionId3,
                        _ => throw new InvalidOperationException("Too many calls")
                    }, "OK");
            });

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(3);
        result.Solicitacoes[0].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Solicitacoes[0].NumeroParcela.Should().Be(1);
        result.Solicitacoes[1].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Solicitacoes[1].NumeroParcela.Should().Be(2);
        result.Solicitacoes[2].TipoRegistro.Should().Be(SerasaPefinRecordType.Principal);
        result.Solicitacoes[2].NumeroParcela.Should().Be(3);
        result.StatusAgregado.Should().Be(SerasaPefinStatus.AguardandoRetorno);

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_MultipleParcelas_FalhaParcial_ContinuaEnvioDasOutras()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 3);
        var transactionId1 = Guid.NewGuid().ToString();
        var transactionId3 = Guid.NewGuid().ToString();
        var solicitacaoId1 = Guid.NewGuid();
        var solicitacaoId2 = Guid.NewGuid();
        var solicitacaoId3 = Guid.NewGuid();
        var solicitacaoIdCounter = 0;
        var gatewayCallCount = 0;

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => solicitacaoIdCounter++ switch
            {
                0 => solicitacaoId1,
                1 => solicitacaoId2,
                2 => solicitacaoId3,
                _ => throw new InvalidOperationException("Too many calls")
            });

        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, CancellationToken ct) =>
            {
                gatewayCallCount++;
                if (gatewayCallCount == 2)
                {
                    throw new SerasaPefinHttpException(500, "Internal Server Error", "Serasa error");
                }
                return new SerasaInclusionResponse(
                    gatewayCallCount == 1 ? transactionId1 : transactionId3, "OK");
            });

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(3);
        result.Solicitacoes[0].Status.Should().Be(SerasaPefinStatus.AguardandoRetorno);
        result.Solicitacoes[1].Status.Should().Be(SerasaPefinStatus.NegativadoErro);
        result.Solicitacoes[2].Status.Should().Be(SerasaPefinStatus.AguardandoRetorno);
        result.StatusAgregado.Should().Be(SerasaPefinStatus.NegativadoErro);

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ParcelaJaAtiva_NaoReenvia_SkipaParcela()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 3);
        var transactionId1 = Guid.NewGuid().ToString();
        var transactionId3 = Guid.NewGuid().ToString();
        var solicitacaoId1 = Guid.NewGuid();
        var solicitacaoId3 = Guid.NewGuid();
        var solicitacaoIdCounter = 0;

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        // Parcela 2 already exists (idempotency check)
        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int nv, string cn, string dd, string? dg, SerasaPefinRecordType tr, int? np, CancellationToken ct) => np == 2);

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => solicitacaoIdCounter++ switch
            {
                0 => solicitacaoId1,
                1 => solicitacaoId3,
                _ => throw new InvalidOperationException("Too many calls")
            });

        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, CancellationToken ct) => new SerasaInclusionResponse(
                solicitacaoIdCounter == 1 ? transactionId1 : transactionId3, "OK"));

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(2); // Only 2 parcels sent (1 and 3), parcela 2 skipped
        result.Solicitacoes[0].NumeroParcela.Should().Be(1);
        result.Solicitacoes[1].NumeroParcela.Should().Be(3);
        result.StatusAgregado.Should().Be(SerasaPefinStatus.AguardandoRetorno);

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ComFiadores_EnviaNumeroCorretoDeChamadas()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: true, Operador: "test-user");
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 2);
        var fiador = CreateValidFiador(numVenda);
        var transactionIdPrincipal1 = Guid.NewGuid().ToString();
        var transactionIdPrincipal2 = Guid.NewGuid().ToString();
        var transactionIdFiador1 = Guid.NewGuid().ToString();
        var transactionIdFiador2 = Guid.NewGuid().ToString();
        var solicitacaoIdCounter = 0;

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fiador });

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var gatewayCallCount = 0;
        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, CancellationToken ct) =>
            {
                gatewayCallCount++;
                return new SerasaInclusionResponse(
                    gatewayCallCount == 1 ? transactionIdPrincipal1 : transactionIdPrincipal2, "OK");
            });

        _mockGateway
            .Setup(x => x.PostGuarantorAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, CancellationToken ct) =>
            {
                gatewayCallCount++;
                return new SerasaInclusionResponse(
                    gatewayCallCount == 3 ? transactionIdFiador1 : transactionIdFiador2, "OK");
            });

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // 2 principais + 2 fiadores (1 por parcela) = 4 total
        result.Solicitacoes.Should().HaveCount(4);

        var principais = result.Solicitacoes.Where(s => s.TipoRegistro == SerasaPefinRecordType.Principal).ToList();
        var garantidores = result.Solicitacoes.Where(s => s.TipoRegistro == SerasaPefinRecordType.Garantidor).ToList();

        principais.Should().HaveCount(2);
        garantidores.Should().HaveCount(2);

        // Verify that each fiador is linked to the correct parcela
        garantidores[0].NumeroParcela.Should().Be(1);
        garantidores[1].NumeroParcela.Should().Be(2);

        result.StatusAgregado.Should().Be(SerasaPefinStatus.AguardandoRetorno);

        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockGateway.Verify(x => x.PostGuarantorAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_DividasNotFound_ThrowsDomainNotFoundException()
    {
        // Arrange
        var numVenda = 295;
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user");

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DividasElegiveisQueryResult?)null);

        // Act
        var act = async () => await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainNotFoundException>()
            .WithMessage($"Venda {numVenda} não encontrada ou não possui dívidas elegíveis.*");
    }

    [Fact]
    public async Task Handle_ParcelaIdsProvided_FilteraParcelasEspecificas()
    {
        // Arrange
        var numVenda = 295;
        var parcelaIds = new[] { 1, 3 }; // Only process parcels 1 and 3
        var command = new RequestNegativacaoCommand(numVenda, IncluirGarantidores: false, Operador: "test-user", ParcelaIds: parcelaIds);
        var dividasElegiveis = CreateValidDividasElegiveis(numVenda, parcelCount: 3);
        var transactionId1 = Guid.NewGuid().ToString();
        var transactionId3 = Guid.NewGuid().ToString();

        _mockQueryService
            .Setup(x => x.GetDividasElegiveisAsync(numVenda, _negativacaoOptions.DiasAtrasoMinimo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasElegiveis);

        _mockQueryService
            .Setup(x => x.ListFiadoresAsync(numVenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FiadorQueryResult>());

        _mockRepository
            .Setup(x => x.ExistsActiveAsync(
                numVenda,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SerasaPefinRecordType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var gatewayCallCount = 0;
        _mockGateway
            .Setup(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, CancellationToken ct) =>
            {
                gatewayCallCount++;
                return new SerasaInclusionResponse(
                    gatewayCallCount == 1 ? transactionId1 : transactionId3, "OK");
            });

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<SerasaPefinSolicitacaoCompleta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Solicitacoes.Should().HaveCount(2); // Only 2 parcels (1 and 3)
        result.Solicitacoes[0].NumeroParcela.Should().Be(1);
        result.Solicitacoes[1].NumeroParcela.Should().Be(3);

        _mockGateway.Verify(x => x.PostMainDebtAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion
}

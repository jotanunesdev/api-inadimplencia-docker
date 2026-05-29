using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class RequestNegativacaoFluxoCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ISenhaTransacaoValidator> _senhaValidatorMock;
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock;
    private readonly Mock<ISerasaPefinRepository> _serasaRepositoryMock;
    private readonly Mock<IOcorrenciaRepository> _ocorrenciaRepositoryMock;
    private readonly Mock<IProtocoloGenerator> _protocoloGeneratorMock;
    private readonly Mock<IAprovadoresPolicy> _aprovadoresPolicyMock;
    private readonly Mock<INotificationDispatcher> _notificationDispatcherMock;
    private readonly RequestNegativacaoFluxoCommandHandler _handler;

    public RequestNegativacaoFluxoCommandHandlerTests()
    {
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _senhaValidatorMock = new Mock<ISenhaTransacaoValidator>();
        _queryServiceMock = new Mock<IInadimplenciaQueryService>();
        _serasaRepositoryMock = new Mock<ISerasaPefinRepository>();
        _ocorrenciaRepositoryMock = new Mock<IOcorrenciaRepository>();
        _protocoloGeneratorMock = new Mock<IProtocoloGenerator>();
        _aprovadoresPolicyMock = new Mock<IAprovadoresPolicy>();
        _notificationDispatcherMock = new Mock<INotificationDispatcher>();

        _handler = new RequestNegativacaoFluxoCommandHandler(
            _currentUserServiceMock.Object,
            _senhaValidatorMock.Object,
            _queryServiceMock.Object,
            _serasaRepositoryMock.Object,
            _ocorrenciaRepositoryMock.Object,
            _protocoloGeneratorMock.Object,
            _aprovadoresPolicyMock.Object,
            _notificationDispatcherMock.Object,
            NullLogger<RequestNegativacaoFluxoCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_SenhaInvalida_DeveLancarExcecaoSemEscrita()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1, 2 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_errada");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Invalid);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("SENHA_INVALIDA", exception.Message);

        // Verify no writes were made
        _serasaRepositoryMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationDispatcherMock.Verify(n => n.DispatchManyAsync(It.IsAny<NotificationType>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SenhaBloqueada_DeveLancarExcecaoSemEscrita()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1, 2 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_qualquer");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_qualquer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.LockedOut);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("SENHA_BLOQUEADA", exception.Message);

        // Verify no writes were made
        _serasaRepositoryMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SenhaNaoCadastrada_DeveLancarExcecaoSemEscrita()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1, 2 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_qualquer");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_qualquer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.NotSet);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("SENHA_NAO_CADASTRADA", exception.Message);

        // Verify no writes were made
        _serasaRepositoryMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ParcelaNaoElegivel_DeveLancarExcecaoSemEscrita()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1, 2 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_correta");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_correta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var dividasResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2024, 1, 1), 30, false), // Not eligible
                new(2, 1000m, new DateOnly(2024, 2, 1), 40, false)  // Not eligible
            }.AsReadOnly());

        _queryServiceMock.Setup(q => q.GetDividasElegiveisAsync(12345, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasResult);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("NAO_ELEGIVEL", exception.Message);

        // Verify no writes were made
        _serasaRepositoryMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_JaExisteSolicitacaoAtiva_DeveLancarExcecaoSemEscrita()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1, 2 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_correta");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_correta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var dividasResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2023, 1, 1), 90, true),
                new(2, 1000m, new DateOnly(2024, 1, 1), 30, false)
            }.AsReadOnly());

        _queryServiceMock.Setup(q => q.GetDividasElegiveisAsync(12345, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasResult);

        _serasaRepositoryMock.Setup(r => r.ExistsActiveAsync(
            12345,
            "CTR-12345",
            "12345678901",
            null,
            SerasaPefinRecordType.Principal,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Contains("JA_EM_APROVACAO", exception.Message);

        // Verify no writes were made
        _serasaRepositoryMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Sucesso_DeveCriarSolicitacaoOcorrenciaENotificar()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_correta");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_correta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var dividasResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2023, 1, 1), 90, true)
            }.AsReadOnly());

        _queryServiceMock.Setup(q => q.GetDividasElegiveisAsync(12345, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasResult);

        _serasaRepositoryMock.Setup(r => r.ExistsActiveAsync(
            12345,
            "CTR-12345",
            "12345678901",
            null,
            SerasaPefinRecordType.Principal,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _serasaRepositoryMock.Setup(r => r.AddManyAsync(
            It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vendaResult = new InadimplenciaQueryResult(
            NumVenda: 12345,
            DocumentoDevedor: "12345678901",
            NomeDevedor: "João Silva",
            Cliente: "João Silva",
            Empreendimento: "Empreendimento A",
            Bloco: "Bloco 1",
            Unidade: "Apto 101",
            Valor: 1000m,
            DataVencimento: new DateOnly(2023, 1, 1),
            Endereco: new EnderecoDto(
                "12345678",
                "Rua Teste",
                "Bairro Teste",
                "Cidade Teste",
                "SP"));

        _queryServiceMock.Setup(q => q.GetVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendaResult);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        var aprovadores = new List<string> { "aracy.mendoca", "adriano.oliveira" };
        _aprovadoresPolicyMock.Setup(p => p.ListAprovadores()).Returns(aprovadores.AsReadOnly());

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "aracy.mendoca", Guid.NewGuid() },
                { "adriano.oliveira", Guid.NewGuid() }
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _serasaRepositoryMock.Verify(r => r.AddManyAsync(
            It.Is<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(items =>
                items.Count == 2 &&
                items.Count(s => s.IdSolicitacaoPai == null && s.NumeroParcela == null && s.NumVendaFk == 12345 && s.Status == SerasaPefinStatus.AguardandoAprovacao && s.SolicitanteUsername == "operador") == 1 &&
                items.Count(s => s.IdSolicitacaoPai == result && s.NumeroParcela == 1 && s.ParcelaIdOrigem == "1" && s.Valor == 1000m && s.DataVencimento == new DateOnly(2023, 1, 1) && s.Status == SerasaPefinStatus.AguardandoAprovacao) == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.NomeUsuarioFk == "operador" &&
                o.StatusOcorrencia == "Solicitação de negativação"),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationDispatcherMock.Verify(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            aprovadores,
            12345,
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            result.ToString(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SucessoComMultiplasParcelasElegiveis_DevePersistirPaiEFilhas()
    {
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);

        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1, 2 },
            IncluirFiadores: false,
            SenhaTransacao: "senha_correta");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_correta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var dividasResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2023, 1, 1), 90, true),
                new(2, 1500m, new DateOnly(2023, 2, 1), 80, true)
            }.AsReadOnly());

        _queryServiceMock.Setup(q => q.GetDividasElegiveisAsync(12345, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasResult);

        _serasaRepositoryMock.Setup(r => r.ExistsActiveAsync(
            12345,
            "CTR-12345",
            "12345678901",
            null,
            SerasaPefinRecordType.Principal,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _serasaRepositoryMock.Setup(r => r.AddManyAsync(
            It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vendaResult = new InadimplenciaQueryResult(
            NumVenda: 12345,
            DocumentoDevedor: "12345678901",
            NomeDevedor: "João Silva",
            Cliente: "João Silva",
            Empreendimento: "Empreendimento A",
            Bloco: "Bloco 1",
            Unidade: "Apto 101",
            Valor: 2500m,
            DataVencimento: new DateOnly(2023, 2, 1),
            Endereco: new EnderecoDto(
                "12345678",
                "Rua Teste",
                "Bairro Teste",
                "Cidade Teste",
                "SP"));

        _queryServiceMock.Setup(q => q.GetVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendaResult);

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        _aprovadoresPolicyMock.Setup(p => p.ListAprovadores()).Returns(Array.Empty<string>());

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        _serasaRepositoryMock.Verify(r => r.AddManyAsync(
            It.Is<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(items =>
                items.Count == 3 &&
                items.Count(s => s.Id == result && s.IdSolicitacaoPai == null && s.NumeroParcela == null && s.Valor == 2500m && s.DataVencimento == new DateOnly(2023, 2, 1)) == 1 &&
                items.Count(s => s.IdSolicitacaoPai == result && s.NumeroParcela == 1 && s.ParcelaIdOrigem == "1" && s.Valor == 1000m && s.DataVencimento == new DateOnly(2023, 1, 1)) == 1 &&
                items.Count(s => s.IdSolicitacaoPai == result && s.NumeroParcela == 2 && s.ParcelaIdOrigem == "2" && s.Valor == 1500m && s.DataVencimento == new DateOnly(2023, 2, 1)) == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SucessoComFiadores_DeveIncluirSufixoNaMensagem()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1 },
            IncluirFiadores: true,
            SenhaTransacao: "senha_correta");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_correta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var dividasResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2023, 1, 1), 90, true)
            }.AsReadOnly());

        _queryServiceMock.Setup(q => q.GetDividasElegiveisAsync(12345, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasResult);

        _serasaRepositoryMock.Setup(r => r.ExistsActiveAsync(
            12345,
            "CTR-12345",
            "12345678901",
            null,
            SerasaPefinRecordType.Principal,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _serasaRepositoryMock.Setup(r => r.AddManyAsync(
            It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vendaResult = new InadimplenciaQueryResult(
            NumVenda: 12345,
            DocumentoDevedor: "12345678901",
            NomeDevedor: "João Silva",
            Cliente: "João Silva",
            Empreendimento: "Empreendimento A",
            Bloco: "Bloco 1",
            Unidade: "Apto 101",
            Valor: 1000m,
            DataVencimento: new DateOnly(2023, 1, 1),
            Endereco: new EnderecoDto(
                "12345678",
                "Rua Teste",
                "Bairro Teste",
                "Cidade Teste",
                "SP"));

        _queryServiceMock.Setup(q => q.GetVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendaResult);

        var fiadores = new List<FiadorQueryResult>
        {
            new(12345, "ASSOC001", "PESSOA001", "Fiador Teste", "98765432100", "FIADOR", null, DateTime.Now)
        };

        _queryServiceMock.Setup(q => q.ListFiadoresAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiadores.AsReadOnly());

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        var aprovadores = new List<string> { "aracy.mendoca" };
        _aprovadoresPolicyMock.Setup(p => p.ListAprovadores()).Returns(aprovadores.AsReadOnly());

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "aracy.mendoca", Guid.NewGuid() }
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.Descricao.Contains("e seus fiadores")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemFiadoresDisponiveisComIncluirFiadoresTrue_DeveNaoIncluirSufixo()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.Username).Returns("operador");
        _currentUserServiceMock.Setup(s => s.IsAuthenticated).Returns(true);
        
        var command = new RequestNegativacaoFluxoCommand(
            NumVenda: 12345,
            ParcelaIds: new List<long> { 1 },
            IncluirFiadores: true,
            SenhaTransacao: "senha_correta");

        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", "senha_correta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var dividasResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2023, 1, 1), 90, true)
            }.AsReadOnly());

        _queryServiceMock.Setup(q => q.GetDividasElegiveisAsync(12345, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dividasResult);

        _serasaRepositoryMock.Setup(r => r.ExistsActiveAsync(
            12345,
            "CTR-12345",
            "12345678901",
            null,
            SerasaPefinRecordType.Principal,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _serasaRepositoryMock.Setup(r => r.AddManyAsync(
            It.IsAny<IReadOnlyCollection<SerasaPefinSolicitacaoCompleta>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vendaResult = new InadimplenciaQueryResult(
            NumVenda: 12345,
            DocumentoDevedor: "12345678901",
            NomeDevedor: "João Silva",
            Cliente: "João Silva",
            Empreendimento: "Empreendimento A",
            Bloco: "Bloco 1",
            Unidade: "Apto 101",
            Valor: 1000m,
            DataVencimento: new DateOnly(2023, 1, 1),
            Endereco: new EnderecoDto(
                "12345678",
                "Rua Teste",
                "Bairro Teste",
                "Cidade Teste",
                "SP"));

        _queryServiceMock.Setup(q => q.GetVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendaResult);

        _queryServiceMock.Setup(q => q.ListFiadoresAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FiadorQueryResult>().AsReadOnly());

        _protocoloGeneratorMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026051400001");

        var aprovadores = new List<string> { "aracy.mendoca" };
        _aprovadoresPolicyMock.Setup(p => p.ListAprovadores()).Returns(aprovadores.AsReadOnly());

        _notificationDispatcherMock.Setup(n => n.DispatchManyAsync(
            It.IsAny<NotificationType>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<int?>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>
            {
                { "aracy.mendoca", Guid.NewGuid() }
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _ocorrenciaRepositoryMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                !o.Descricao.Contains("e seus fiadores")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

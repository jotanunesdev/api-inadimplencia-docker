using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Application.Features.Negativacao.Queries;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class GetDividasElegiveisQueryHandlerTests
{
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock;
    private readonly Mock<ISerasaPefinRepository> _serasaRepositoryMock;
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepositoryMock;
    private readonly Mock<IOptions<NegativacaoOptions>> _optionsMock;
    private readonly GetDividasElegiveisQueryHandler _handler;

    public GetDividasElegiveisQueryHandlerTests()
    {
        _queryServiceMock = new Mock<IInadimplenciaQueryService>();
        _serasaRepositoryMock = new Mock<ISerasaPefinRepository>();
        _serasaRepositoryMock
            .Setup(r => r.ListByNumVendaAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SerasaPefinSolicitacaoCompleta>());
        _baixaRepositoryMock = new Mock<ISerasaPefinBaixaRepository>();
        _baixaRepositoryMock
            .Setup(r => r.ListParcelasComBaixaConcluidaAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<int>)new HashSet<int>());
        _optionsMock = new Mock<IOptions<NegativacaoOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(new NegativacaoOptions { DiasAtrasoMinimo = 60 });
        _handler = new GetDividasElegiveisQueryHandler(
            _queryServiceMock.Object,
            _serasaRepositoryMock.Object,
            _baixaRepositoryMock.Object,
            _optionsMock.Object);
    }

    [Fact]
    public async Task HandleAsync_VendaNaoEncontrada_DeveRetornarRespostaVazia()
    {
        // Arrange
        var query = new GetDividasElegiveisQuery(12345);
        _queryServiceMock.Setup(s => s.GetDividasElegiveisAsync(12345, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DividasElegiveisQueryResult?)null);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12345, result.NumVenda);
        Assert.Null(result.Cliente);
        Assert.Null(result.CpfMasked);
        Assert.Null(result.ContractNumber);
        Assert.False(result.ClientePodeNegativar);
        Assert.Empty(result.Parcelas);
    }

    [Fact]
    public async Task HandleAsync_VendaEncontradaSemParcelasElegiveis_DeveRetornarClientePodeNegativarFalse()
    {
        // Arrange
        var query = new GetDividasElegiveisQuery(12345);
        var queryResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "João Silva",
            Cpf: "12345678901",
            ContractNumber: "CTR-12345",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2024, 1, 1), 30, false),
                new(2, 1000m, new DateOnly(2024, 2, 1), 40, false)
            }.AsReadOnly());

        _queryServiceMock.Setup(s => s.GetDividasElegiveisAsync(12345, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12345, result.NumVenda);
        Assert.Equal("João Silva", result.Cliente);
        Assert.NotNull(result.CpfMasked);
        Assert.Equal("CTR-12345", result.ContractNumber);
        Assert.False(result.ClientePodeNegativar);
        Assert.Equal(2, result.Parcelas.Count);
        Assert.All(result.Parcelas, p => Assert.False(p.Elegivel));
    }

    [Fact]
    public async Task HandleAsync_VendaEncontradaComParcelasElegiveis_DeveRetornarClientePodeNegativarTrue()
    {
        // Arrange
        var query = new GetDividasElegiveisQuery(12345);
        var queryResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "Maria Santos",
            Cpf: "98765432100",
            ContractNumber: "CTR-54321",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 1000m, new DateOnly(2023, 1, 1), 90, true),
                new(2, 1000m, new DateOnly(2024, 1, 1), 30, false)
            }.AsReadOnly());

        _queryServiceMock.Setup(s => s.GetDividasElegiveisAsync(12345, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12345, result.NumVenda);
        Assert.Equal("Maria Santos", result.Cliente);
        Assert.NotNull(result.CpfMasked);
        Assert.Equal("CTR-54321", result.ContractNumber);
        Assert.True(result.ClientePodeNegativar);
        Assert.Equal(2, result.Parcelas.Count);
        Assert.True(result.Parcelas[0].Elegivel);
        Assert.False(result.Parcelas[1].Elegivel);
    }

    [Fact]
    public async Task HandleAsync_ParcelaComBaixadoSucesso_DevePermanecerElegivelParaRenegativacao()
    {
        // Regress\u00e3o: ap\u00f3s uma baixa concluida (parcela j\u00e1 retirada do Serasa),
        // a parcela deve continuar elegivel para uma NOVA negativa\u00e7\u00e3o (caso o operador
        // tenha errado dados na primeira), mas n\u00e3o pode mais aparecer no modal
        // de baixa (StatusSerasa=BAIXADO_SUCESSO sinaliza isso para a UI).
        var query = new GetDividasElegiveisQuery(18592);
        var queryResult = new DividasElegiveisQueryResult(
            NumVenda: 18592,
            Cliente: "Raiza",
            Cpf: "05440404570",
            ContractNumber: "18592",
            Parcelas: new List<ParcelaElegivelDto>
            {
                new(1, 31.01m, new DateOnly(2023, 3, 20), 1177, true),
            }.AsReadOnly());

        _queryServiceMock.Setup(s => s.GetDividasElegiveisAsync(18592, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);
        _baixaRepositoryMock
            .Setup(r => r.ListParcelasComBaixaConcluidaAsync(18592, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<int>)new HashSet<int> { 1 });

        var result = await _handler.HandleAsync(query, CancellationToken.None);

        Assert.True(result.ClientePodeNegativar);
        Assert.Single(result.Parcelas);
        Assert.True(result.Parcelas[0].Elegivel);
        Assert.Equal("BAIXADO_SUCESSO", result.Parcelas[0].StatusSerasa);
    }

    [Fact]
    public async Task HandleAsync_DeveMascararCpfNaResposta()
    {
        // Arrange
        var query = new GetDividasElegiveisQuery(12345);
        var queryResult = new DividasElegiveisQueryResult(
            NumVenda: 12345,
            Cliente: "Teste Cliente",
            Cpf: "12345678901",
            ContractNumber: "CTR-99999",
            Parcelas: new List<ParcelaElegivelDto>().AsReadOnly());

        _queryServiceMock.Setup(s => s.GetDividasElegiveisAsync(12345, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result.CpfMasked);
        Assert.NotEqual("12345678901", result.CpfMasked);
        Assert.Contains("***", result.CpfMasked);
    }
}

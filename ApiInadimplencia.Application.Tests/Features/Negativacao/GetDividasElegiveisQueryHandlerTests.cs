using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Application.Features.Negativacao.Queries;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao;

public sealed class GetDividasElegiveisQueryHandlerTests
{
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock;
    private readonly Mock<IOptions<NegativacaoOptions>> _optionsMock;
    private readonly GetDividasElegiveisQueryHandler _handler;

    public GetDividasElegiveisQueryHandlerTests()
    {
        _queryServiceMock = new Mock<IInadimplenciaQueryService>();
        _optionsMock = new Mock<IOptions<NegativacaoOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(new NegativacaoOptions { DiasAtrasoMinimo = 60 });
        _handler = new GetDividasElegiveisQueryHandler(_queryServiceMock.Object, _optionsMock.Object);
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

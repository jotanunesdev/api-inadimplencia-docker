using ApiInadimplencia.Application.Abstractions.Persistence;

namespace api_inadimplencia.Api.Tests.Infrastructure;

public sealed class TestInadimplenciaQueryService : IInadimplenciaQueryService
{
    private static readonly EnderecoDto Endereco = new(
        ZipCode: "30110000",
        AddressLine: "Rua Exemplo, 123",
        District: "Centro",
        City: "Belo Horizonte",
        State: "MG",
        Complement: "Apto 101",
        Number: "123");

    private static readonly InadimplenciaQueryResult Venda295 = new(
        NumVenda: 295,
        DocumentoDevedor: "12345678900",
        NomeDevedor: "Cliente Teste",
        Cliente: "Cliente Teste",
        Empreendimento: "Residencial Teste",
        Bloco: "A",
        Unidade: "101",
        Valor: 1000.00m,
        DataVencimento: new DateOnly(2024, 1, 1),
        Endereco: Endereco);

    private static readonly IReadOnlyList<ParcelaElegivelDto> Parcelas295 =
    [
        new ParcelaElegivelDto(
            Id: 1,
            Valor: 1000.00m,
            Vencimento: new DateOnly(2024, 1, 1),
            DiasAtraso: 120,
            Elegivel: true)
    ];

    public Task<InadimplenciaQueryResult?> GetVendaAsync(int numVenda, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<InadimplenciaQueryResult?>(numVenda == 295 ? Venda295 : null);
    }

    public Task<IReadOnlyList<FiadorQueryResult>> ListFiadoresAsync(int numVenda, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FiadorQueryResult> result = Array.Empty<FiadorQueryResult>();
        return Task.FromResult(result);
    }

    public Task<DividasElegiveisQueryResult?> GetDividasElegiveisAsync(int numVenda, int diasAtrasoMinimo, CancellationToken cancellationToken = default)
    {
        if (numVenda != 295)
        {
            return Task.FromResult<DividasElegiveisQueryResult?>(null);
        }

        var parcelas = Parcelas295
            .Select(parcela => parcela with { Elegivel = parcela.DiasAtraso > diasAtrasoMinimo })
            .ToList()
            .AsReadOnly();

        return Task.FromResult<DividasElegiveisQueryResult?>(new DividasElegiveisQueryResult(
            NumVenda: 295,
            Cliente: Venda295.Cliente,
            Cpf: Venda295.DocumentoDevedor,
            ContractNumber: "295/00",
            Parcelas: parcelas,
            Endereco: Endereco));
    }

    public Task<ParcelaPorIdLanQueryResult?> GetParcelaByIdLanAsync(long idLan, CancellationToken cancellationToken = default)
    {
        if (idLan != 12345)
        {
            return Task.FromResult<ParcelaPorIdLanQueryResult?>(null);
        }

        return Task.FromResult<ParcelaPorIdLanQueryResult?>(new ParcelaPorIdLanQueryResult(
            IdLan: 12345,
            NumVenda: 295,
            NumeroDocumento: "029501X",
            DataVencimento: new DateOnly(2024, 1, 1),
            Valor: 1000.00m,
            Inadimplente: "SIM",
            Negativado: null,
            DiasAtraso: 120));
    }
}

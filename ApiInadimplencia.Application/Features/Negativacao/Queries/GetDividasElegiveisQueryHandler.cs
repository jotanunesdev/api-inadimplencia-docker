using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Handler for GetDividasElegiveisQuery.
/// </summary>
public sealed class GetDividasElegiveisQueryHandler : IQueryHandler<GetDividasElegiveisQuery, DividasElegiveisResponse>
{
    private readonly IInadimplenciaQueryService _queryService;
    private readonly IOptions<NegativacaoOptions> _options;

    public GetDividasElegiveisQueryHandler(
        IInadimplenciaQueryService queryService,
        IOptions<NegativacaoOptions> options)
    {
        _queryService = queryService;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<DividasElegiveisResponse> HandleAsync(GetDividasElegiveisQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _queryService.GetDividasElegiveisAsync(
            query.NumVenda,
            _options.Value.DiasAtrasoMinimo,
            cancellationToken);

        if (result is null)
        {
            return new DividasElegiveisResponse(
                NumVenda: query.NumVenda,
                Cliente: null,
                CpfMasked: null,
                ContractNumber: null,
                ClientePodeNegativar: false,
                Parcelas: []);
        }

        var cpfMasked = SerasaPefinPayloadBuilder.MaskDocument(result.Cpf);
        var clientePodeNegativar = result.Parcelas.Any(p => p.Elegivel);

        return new DividasElegiveisResponse(
            NumVenda: result.NumVenda,
            Cliente: result.Cliente,
            CpfMasked: cpfMasked,
            ContractNumber: result.ContractNumber,
            ClientePodeNegativar: clientePodeNegativar,
            Parcelas: result.Parcelas,
            Endereco: result.Endereco);
    }
}

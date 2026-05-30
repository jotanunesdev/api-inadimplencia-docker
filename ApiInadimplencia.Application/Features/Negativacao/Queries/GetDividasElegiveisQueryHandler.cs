using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Handler for GetDividasElegiveisQuery.
/// </summary>
public sealed class GetDividasElegiveisQueryHandler : IQueryHandler<GetDividasElegiveisQuery, DividasElegiveisResponse>
{
    private readonly IInadimplenciaQueryService _queryService;
    private readonly ISerasaPefinRepository _serasaRepository;
    private readonly IOptions<NegativacaoOptions> _options;

    public GetDividasElegiveisQueryHandler(
        IInadimplenciaQueryService queryService,
        ISerasaPefinRepository serasaRepository,
        IOptions<NegativacaoOptions> options)
    {
        _queryService = queryService;
        _serasaRepository = serasaRepository;
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

        // Enriquece cada parcela com o status Serasa (quando houver solicitacao ativa ou negativada).
        // Status terminais que NAO bloqueiam nova selecao: REJEITADA, NEGATIVADO_ERRO, BAIXADO_SUCESSO, BAIXADO_ERRO.
        var solicitacoesSerasa = await _serasaRepository.ListByNumVendaAsync(query.NumVenda, cancellationToken);
        var statusPorParcela = BuildStatusMap(solicitacoesSerasa);

        var parcelasEnriquecidas = result.Parcelas
            .Select(parcela =>
            {
                if (statusPorParcela.TryGetValue(parcela.Id, out var status))
                {
                    return parcela with
                    {
                        Elegivel = false,
                        StatusSerasa = status,
                    };
                }
                return parcela;
            })
            .ToList();

        var cpfMasked = SerasaPefinPayloadBuilder.MaskDocument(result.Cpf);
        var clientePodeNegativar = parcelasEnriquecidas.Any(p => p.Elegivel);

        return new DividasElegiveisResponse(
            NumVenda: result.NumVenda,
            Cliente: result.Cliente,
            CpfMasked: cpfMasked,
            ContractNumber: result.ContractNumber,
            ClientePodeNegativar: clientePodeNegativar,
            Parcelas: parcelasEnriquecidas,
            Endereco: result.Endereco);
    }

    /// <summary>
    /// Constroi mapa NumeroParcela -> Status, considerando apenas solicitacoes Serasa
    /// que devem bloquear nova selecao (status nao-terminal-falha). Quando ha multiplas
    /// solicitacoes para a mesma parcela, prioriza o status mais "ativo" segundo a ordem
    /// definida em <see cref="GetStatusPriority"/>.
    /// </summary>
    private static Dictionary<int, string> BuildStatusMap(
        IReadOnlyList<SerasaPefinSolicitacaoCompleta> solicitacoes)
    {
        var map = new Dictionary<int, string>();

        foreach (var solicitacao in solicitacoes)
        {
            if (solicitacao.NumeroParcela is null)
            {
                continue; // ignora pais (sem numero de parcela)
            }

            var statusKey = solicitacao.Status.ToDbValue();
            if (!IsBlockingStatus(statusKey))
            {
                continue;
            }

            var parcela = solicitacao.NumeroParcela.Value;
            if (!map.TryGetValue(parcela, out var existing) ||
                GetStatusPriority(statusKey) > GetStatusPriority(existing))
            {
                map[parcela] = statusKey;
            }
        }

        return map;
    }

    private static bool IsBlockingStatus(string status) => status switch
    {
        "AGUARDANDO_APROVACAO" => true,
        "APROVADA" => true,
        "PENDENTE_ENVIO" => true,
        "ENVIADO_SERASA" => true,
        "AGUARDANDO_RETORNO" => true,
        "NEGATIVADO_SUCESSO" => true,
        "BAIXA_ENVIADA" => true,
        "BAIXA_AGUARDANDO_RETORNO" => true,
        _ => false, // REJEITADA, NEGATIVADO_ERRO, BAIXADO_SUCESSO, BAIXADO_ERRO, APROVADA_FALHA_ENVIO -> nao bloqueia
    };

    private static int GetStatusPriority(string status) => status switch
    {
        "NEGATIVADO_SUCESSO" => 100,
        "BAIXA_AGUARDANDO_RETORNO" => 90,
        "BAIXA_ENVIADA" => 80,
        "AGUARDANDO_RETORNO" => 70,
        "ENVIADO_SERASA" => 60,
        "PENDENTE_ENVIO" => 50,
        "APROVADA" => 40,
        "AGUARDANDO_APROVACAO" => 30,
        _ => 0,
    };
}

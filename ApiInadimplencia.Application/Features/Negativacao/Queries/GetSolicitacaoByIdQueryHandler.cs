using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Fiadores.Dtos;
using ApiInadimplencia.Application.Features.Negativacao.Dtos;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.Negativacao.Queries;

/// <summary>
/// Handler for GetSolicitacaoByIdQuery.
/// Returns a complete solicitation with parcelas, fiadores, and decision permission.
/// </summary>
public sealed class GetSolicitacaoByIdQueryHandler : IQueryHandler<GetSolicitacaoByIdQuery, SolicitacaoDetalheDto?>
{
    private readonly ISerasaPefinRepository _serasaRepository;
    private readonly IInadimplenciaQueryService _queryService;
    private readonly IAprovadoresPolicy _aprovadoresPolicy;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetSolicitacaoByIdQueryHandler> _logger;

    public GetSolicitacaoByIdQueryHandler(
        ISerasaPefinRepository serasaRepository,
        IInadimplenciaQueryService queryService,
        IAprovadoresPolicy aprovadoresPolicy,
        ICurrentUserService currentUserService,
        ILogger<GetSolicitacaoByIdQueryHandler> logger)
    {
        _serasaRepository = serasaRepository;
        _queryService = queryService;
        _aprovadoresPolicy = aprovadoresPolicy;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SolicitacaoDetalheDto?> HandleAsync(GetSolicitacaoByIdQuery query, CancellationToken cancellationToken = default)
    {
        // 1. Get the solicitation from repository
        var solicitacao = await _serasaRepository.GetByIdAsync(query.Id, cancellationToken);
        if (solicitacao is null)
        {
            return null;
        }

        // 2. Load child parcel rows and DW data once
        var parcelasFilhas = await _serasaRepository.ListByIdSolicitacaoPaiAsync(solicitacao.Id, cancellationToken)
            ?? Array.Empty<SerasaPefinSolicitacaoCompleta>();

        var dividasElegiveis = await _queryService.GetDividasElegiveisAsync(solicitacao.NumVendaFk, 0, cancellationToken);
        var parcelas = ResolverParcelas(solicitacao.Id, solicitacao.NumVendaFk, parcelasFilhas, dividasElegiveis);

        // 3. Get fiadores from DW (only for PRINCIPAL records)
        var fiadores = solicitacao.TipoRegistro == SerasaPefinRecordType.Principal
            ? await GetFiadoresAsync(solicitacao.NumVendaFk, cancellationToken)
            : new List<FiadorDto>();

        // 4. Get client data from DW
        string cliente = dividasElegiveis?.Cliente ?? "Cliente não encontrado";
        string cpf = dividasElegiveis?.Cpf ?? solicitacao.DocumentoDevedor;
        string cpfMasked = NegativacaoOcorrenciaScripts.MaskDocument(cpf);

        // 5. Resolve PodeDecidir
        bool podeDecidir = ResolverPodeDecidir(solicitacao, query.Username);

        // 6. Build DTO
        return new SolicitacaoDetalheDto(
            Id: solicitacao.Id,
            NumVenda: solicitacao.NumVendaFk,
            Cliente: cliente,
            CpfMasked: cpfMasked,
            Cpf: cpf,
            SolicitanteUsername: solicitacao.SolicitanteUsername ?? string.Empty,
            DtSolicitacao: solicitacao.DtCriacao,
            Status: solicitacao.Status.ToString(),
            Valor: solicitacao.Valor,
            IncluirFiadores: solicitacao.TipoRegistro == SerasaPefinRecordType.Principal,
            PodeDecidir: podeDecidir,
            Parcelas: parcelas,
            Fiadores: fiadores);
    }

    private bool ResolverPodeDecidir(SerasaPefinSolicitacaoCompleta solicitacao, string? fallbackUsername)
    {
        var currentUser = NormalizeUsername(_currentUserService.Username)
            ?? NormalizeUsername(fallbackUsername);

        // User must be authenticated
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return false;
        }

        // User must be an approved approver
        if (!_aprovadoresPolicy.IsAprovador(currentUser))
        {
            return false;
        }

        // Status must be AguardandoAprovacao
        if (solicitacao.Status != SerasaPefinStatus.AguardandoAprovacao)
        {
            return false;
        }

        // SOLICITANTE_NAO_PODE_APROVAR: requester cannot approve their own request,
        // exceto para super-decisores configurados explicitamente.
        if (solicitacao.SolicitanteUsername?.Equals(currentUser, StringComparison.OrdinalIgnoreCase) == true
            && !_aprovadoresPolicy.IsSuperDecisor(currentUser))
        {
            return false;
        }

        return true;
    }

    private static string? NormalizeUsername(string? username)
    {
        return string.IsNullOrWhiteSpace(username)
            ? null
            : username.Trim().ToLowerInvariant();
    }

    private IReadOnlyList<ParcelaDto> ResolverParcelas(
        Guid solicitacaoId,
        int numVenda,
        IReadOnlyList<SerasaPefinSolicitacaoCompleta> parcelasFilhas,
        DividasElegiveisQueryResult? dividasElegiveis)
    {
        if (parcelasFilhas.Count > 0)
        {
            var parcelasDwPorId = dividasElegiveis?.Parcelas.ToDictionary(p => p.Id)
                ?? new Dictionary<int, ParcelaElegivelDto>();

            return parcelasFilhas
                .Select(filha => MapParcelaFilha(filha, parcelasDwPorId))
                .ToList();
        }

        _logger.LogWarning(
            "Solicitacao {SolicitacaoId} da venda {NumVenda} nao possui parcelas filhas persistidas. Aplicando fallback legado com todas as parcelas elegiveis.",
            solicitacaoId,
            numVenda);

        return dividasElegiveis?.Parcelas
            .Select(p => new ParcelaDto(
                Id: p.Id,
                Valor: p.Valor,
                Vencimento: p.Vencimento,
                DiasAtraso: p.DiasAtraso))
            .ToList() ?? new List<ParcelaDto>();
    }

    private static ParcelaDto MapParcelaFilha(
        SerasaPefinSolicitacaoCompleta filha,
        IReadOnlyDictionary<int, ParcelaElegivelDto> parcelasDwPorId)
    {
        var parcelaId = filha.NumeroParcela
            ?? throw new InvalidOperationException($"Solicitacao filha {filha.Id} nao possui NumeroParcela.");

        if (parcelasDwPorId.TryGetValue(parcelaId, out var parcelaDw))
        {
            return new ParcelaDto(
                Id: parcelaId,
                Valor: parcelaDw.Valor,
                Vencimento: parcelaDw.Vencimento,
                DiasAtraso: parcelaDw.DiasAtraso);
        }

        var diasAtraso = Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - filha.DataVencimento.DayNumber);
        return new ParcelaDto(
            Id: parcelaId,
            Valor: filha.Valor,
            Vencimento: filha.DataVencimento,
            DiasAtraso: diasAtraso);
    }

    private async Task<IReadOnlyList<FiadorDto>> GetFiadoresAsync(int numVenda, CancellationToken cancellationToken)
    {
        var fiadoresQueryResult = await _queryService.ListFiadoresAsync(numVenda, cancellationToken);
        return fiadoresQueryResult
            .Select(f => new FiadorDto
            {
                ID_ASSOCIADO = int.TryParse(f.IdAssociado, out var idAssociado) ? idAssociado : null,
                ID_RESERVA = null,
                ID_PESSOA = int.TryParse(f.IdPessoa, out var idPessoa) ? idPessoa : null,
                NOME = f.Nome,
                DOCUMENTO = f.Documento,
                DATA_CADASTRO = f.DataCadastro,
                RENDA_FAMILIAR = null,
                TIPO_ASSOCIACAO = f.TipoAssociacao,
                NUM_VENDA = f.NumVenda,
                ENDERECO = null // Address can be added later if needed
            })
            .ToList();
    }
}

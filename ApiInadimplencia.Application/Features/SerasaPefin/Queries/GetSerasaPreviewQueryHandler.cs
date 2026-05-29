using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Queries;

/// <summary>
/// Handler for GetSerasaPreviewQuery - queries DW without calling Serasa API
/// </summary>
public class GetSerasaPreviewQueryHandler : IQueryHandler<GetSerasaPreviewQuery, SerasaPefinPreviewResponse>
{
    private readonly IInadimplenciaQueryService _inadimplenciaQueryService;
    private readonly ISerasaPefinRepository _serasaPefinRepository;
    private readonly SerasaPefinOptions _options;
    private readonly ILogger<GetSerasaPreviewQueryHandler> _logger;

    public GetSerasaPreviewQueryHandler(
        IInadimplenciaQueryService inadimplenciaQueryService,
        ISerasaPefinRepository serasaPefinRepository,
        IOptions<SerasaPefinOptions> options,
        ILogger<GetSerasaPreviewQueryHandler> logger)
    {
        _inadimplenciaQueryService = inadimplenciaQueryService;
        _serasaPefinRepository = serasaPefinRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SerasaPefinPreviewResponse> HandleAsync(GetSerasaPreviewQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Serasa PEFIN preview for sale {NumVenda}", query.NumVenda);

        // 1. Buscar venda no DW
        var venda = await _inadimplenciaQueryService.GetVendaAsync(query.NumVenda, cancellationToken);
        if (venda is null)
        {
            throw new NotFoundException($"Venda {query.NumVenda} não encontrada ou não é inadimplente");
        }

        // 2. Buscar fiadores
        var fiadores = await _inadimplenciaQueryService.ListFiadoresAsync(query.NumVenda, cancellationToken);

        // 3. Preparar opções do PayloadBuilder
        var payloadOptions = new SerasaPefinPayloadBuilder.Options(
            UatEnabled: _options.UseUatDefaults,
            AreaInformante: _options.AreaInformante ?? string.Empty,
            CategoryId: SerasaPefinConstants.CategoryId);

        // 4. Construir inputs para validação
        var contractNumber = venda.NumVenda.ToString();
        var creditorDocument = _options.CreditorDocument ?? string.Empty;

        // TODO: Task 5.0 will implement real parcela iteration. Using placeholder for now.
        var parcelaPlaceholder = new ParcelaInput(
            Valor: venda.Valor,
            Vencimento: venda.DataVencimento,
            Numero: 1,
            IdOrigem: venda.NumVenda.ToString());

        var mainDebtInput = new MainDebtInput(
            Parcela: parcelaPlaceholder,
            ContractNumber: contractNumber,
            DebtorDocument: venda.DocumentoDevedor,
            DebtorName: venda.NomeDevedor,
            DebtorAddress: ConvertToSerasaAddress(venda.Endereco),
            CreditorDocument: creditorDocument);

        // 5. Validar dados principais
        var mainValidation = SerasaPefinPayloadBuilder.ValidateMainDebt(mainDebtInput, payloadOptions);

        // 6. Verificar valor mínimo
        var blocks = new List<SerasaPefinBlockDto>();
        if (venda.Valor < SerasaPefinConstants.MinValue)
        {
            blocks.Add(new SerasaPefinBlockDto(
                Type: "VALUE_BELOW_MINIMUM",
                Message: $"Valor deve ser maior ou igual a R$ {SerasaPefinConstants.MinValue:F2}",
                Details: new Dictionary<string, object?>
                {
                    ["valor"] = venda.Valor,
                    ["minValue"] = SerasaPefinConstants.MinValue
                }));
        }

        // 7. Validar documentos UAT
        var blockedMainDocs = SerasaPefinPayloadBuilder.GetBlockedUatDocuments(
            payloadOptions, venda.DocumentoDevedor, creditorDocument);
        if (blockedMainDocs.Count > 0)
        {
            blocks.Add(new SerasaPefinBlockDto(
                Type: "UAT_DOCUMENT_NOT_ALLOWED",
                Message: "Documento não autorizado para ambiente UAT",
                Details: new Dictionary<string, object?>
                {
                    ["blockedDocuments"] = blockedMainDocs
                }));
        }

        // 8. Verificar duplicidade ativa (best-effort)
        try
        {
            var hasActiveDuplicate = await _serasaPefinRepository.ExistsActiveAsync(
                venda.NumVenda,
                contractNumber,
                venda.DocumentoDevedor,
                null, // documentoGarantidor (null para principal)
                SerasaPefinRecordType.Principal,
                cancellationToken);

            if (hasActiveDuplicate)
            {
                blocks.Add(new SerasaPefinBlockDto(
                    Type: "ACTIVE_DUPLICATE",
                    Message: "Já existe negativação ativa para esta venda",
                    Details: new Dictionary<string, object?>
                    {
                        ["numVenda"] = venda.NumVenda,
                        ["contractNumber"] = contractNumber
                    }));
            }
        }
        catch (Exception ex)
        {
            // Best-effort: log but don't block preview
            _logger.LogWarning(ex, "Failed to check for active duplicate for sale {NumVenda}", venda.NumVenda);
        }

        // 9. Validar cada fiador
        var garantidoresPreview = new List<SerasaPefinGarantidorDto>();
        foreach (var fiador in fiadores)
        {
            var guarantorInput = new GuarantorInput(
                Parcela: parcelaPlaceholder,
                ContractNumber: contractNumber,
                DebtorDocument: venda.DocumentoDevedor,
                CreditorDocument: creditorDocument,
                GuarantorDocument: fiador.Documento,
                GuarantorName: fiador.Nome,
                GuarantorAddress: ConvertToSerasaAddress(fiador.Endereco));

            var guarantorValidation = SerasaPefinPayloadBuilder.ValidateGuarantor(guarantorInput, payloadOptions);
            var blockedGuarantorDocs = SerasaPefinPayloadBuilder.GetBlockedUatDocuments(
                payloadOptions, fiador.Documento);

            var isElegivel = guarantorValidation.IsValid && blockedGuarantorDocs.Count == 0;

            garantidoresPreview.Add(new SerasaPefinGarantidorDto(
                Nome: fiador.Nome,
                Documento: SerasaPefinPayloadBuilder.MaskDocument(fiador.Documento),
                TipoAssociacao: fiador.TipoAssociacao,
                Endereco: FormatEndereco(fiador.Endereco),
                Elegivel: isElegivel,
                MissingFields: guarantorValidation.MissingFields.ToList(),
                BlockedDocuments: blockedGuarantorDocs));
        }

        // 10. Determinar elegibilidade geral
        var isPrincipalValid = mainValidation.IsValid && blocks.Count == 0;
        var elegivel = isPrincipalValid && garantidoresPreview.All(g => g.Elegivel);

        // 11. Montar DTO de resposta
        return new SerasaPefinPreviewResponse(
            NumVenda: venda.NumVenda,
            Cliente: venda.Cliente,
            Empreendimento: venda.Empreendimento,
            Bloco: venda.Bloco,
            Unidade: venda.Unidade,
            DocumentoDevedor: SerasaPefinPayloadBuilder.MaskDocument(venda.DocumentoDevedor),
            DocumentoCredor: SerasaPefinPayloadBuilder.MaskDocument(creditorDocument),
            ContractNumber: contractNumber,
            CategoryId: SerasaPefinConstants.CategoryId,
            AreaInformante: payloadOptions.AreaInformante,
            Valor: venda.Valor,
            DataVencimento: venda.DataVencimento.ToString("yyyy-MM-dd"),
            Endereco: FormatEndereco(venda.Endereco),
            Garantidores: garantidoresPreview,
            MissingFields: mainValidation.MissingFields.ToList(),
            Blocks: blocks,
            Elegivel: elegivel);
    }

    private static SerasaAddress? ConvertToSerasaAddress(EnderecoDto? endereco)
    {
        if (endereco is null)
            return null;

        return new SerasaAddress(
            ZipCode: endereco.ZipCode,
            AddressLine: endereco.AddressLine,
            District: endereco.District,
            City: endereco.City,
            State: endereco.State,
            Complement: endereco.Complement,
            Number: endereco.Number);
    }

    private static string FormatEndereco(EnderecoDto? endereco)
    {
        if (endereco is null)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(endereco.AddressLine))
            parts.Add(endereco.AddressLine);
        if (!string.IsNullOrWhiteSpace(endereco.District))
            parts.Add(endereco.District);
        if (!string.IsNullOrWhiteSpace(endereco.City))
            parts.Add(endereco.City);
        if (!string.IsNullOrWhiteSpace(endereco.State))
            parts.Add(endereco.State);

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Exception thrown when a resource is not found
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

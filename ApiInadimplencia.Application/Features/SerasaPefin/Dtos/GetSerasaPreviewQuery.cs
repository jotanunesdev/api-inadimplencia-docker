using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Dtos;

/// <summary>
/// Query to get Serasa PEFIN preview for a sale
/// </summary>
public record GetSerasaPreviewQuery(int NumVenda) : IQuery<SerasaPefinPreviewResponse>;

/// <summary>
/// Response for Serasa PEFIN preview - matches Node.js contract
/// </summary>
public record SerasaPefinPreviewResponse(
    int NumVenda,
    string Cliente,
    string Empreendimento,
    string Bloco,
    string Unidade,
    string DocumentoDevedor,
    string DocumentoCredor,
    string ContractNumber,
    string CategoryId,
    string AreaInformante,
    decimal Valor,
    string DataVencimento,
    string Endereco,
    List<SerasaPefinGarantidorDto> Garantidores,
    List<string> MissingFields,
    List<SerasaPefinBlockDto> Blocks,
    bool Elegivel);

/// <summary>
/// DTO for guarantor in preview
/// </summary>
public record SerasaPefinGarantidorDto(
    string Nome,
    string Documento,
    string TipoAssociacao,
    string Endereco,
    bool Elegivel,
    List<string> MissingFields,
    List<string> BlockedDocuments);

/// <summary>
/// DTO for validation block in preview
/// </summary>
public record SerasaPefinBlockDto(
    string Type,
    string Message,
    Dictionary<string, object?> Details);

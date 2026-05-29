using ApiInadimplencia.Application.Abstractions.Persistence;

namespace ApiInadimplencia.Application.Features.Negativacao.Dtos;

/// <summary>
/// Response DTO for eligible debts query endpoint.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="Cliente">Client name.</param>
/// <param name="CpfMasked">Client CPF masked (e.g., "123.***.***-09").</param>
/// <param name="ContractNumber">Contract number.</param>
/// <param name="ClientePodeNegativar">Whether the client has at least one eligible parcela.</param>
/// <param name="Parcelas">List of parcelas with eligibility information.</param>
/// <param name="Endereco">Client address (nullable when no matching client record).</param>
public sealed record DividasElegiveisResponse(
    int NumVenda,
    string? Cliente,
    string? CpfMasked,
    string? ContractNumber,
    bool ClientePodeNegativar,
    IReadOnlyList<ParcelaElegivelDto> Parcelas,
    EnderecoDto? Endereco = null);

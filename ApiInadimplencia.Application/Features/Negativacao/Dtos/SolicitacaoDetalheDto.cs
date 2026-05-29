namespace ApiInadimplencia.Application.Features.Negativacao.Dtos;

/// <summary>
/// DTO for a complete negativacao solicitation with parcelas and fiadores for the frontend.
/// </summary>
/// <param name="Id">Solicitation ID.</param>
/// <param name="NumVenda">Sale number.</param>
/// <param name="Cliente">Client name.</param>
/// <param name="CpfMasked">Masked CPF of the debtor.</param>
/// <param name="Cpf">Client CPF (digits-only, optional).</param>
/// <param name="SolicitanteUsername">Username of the requester.</param>
/// <param name="DtSolicitacao">Request timestamp (UTC).</param>
/// <param name="Status">Current status.</param>
/// <param name="Valor">Total debt value.</param>
/// <param name="IncluirFiadores">Whether fiadores should be included.</param>
/// <param name="PodeDecidir">Whether the current user can decide on this solicitation.</param>
/// <param name="Parcelas">List of parcelas.</param>
/// <param name="Fiadores">List of fiadores.</param>
public sealed record SolicitacaoDetalheDto(
    Guid Id,
    int NumVenda,
    string Cliente,
    string CpfMasked,
    string? Cpf,
    string SolicitanteUsername,
    DateTime DtSolicitacao,
    string Status,
    decimal Valor,
    bool IncluirFiadores,
    bool PodeDecidir,
    IReadOnlyList<ParcelaDto> Parcelas,
    IReadOnlyList<Fiadores.Dtos.FiadorDto> Fiadores);

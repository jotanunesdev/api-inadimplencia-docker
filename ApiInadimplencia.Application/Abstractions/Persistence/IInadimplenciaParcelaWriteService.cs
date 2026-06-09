namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Port for write-back operations against
/// <c>DW.fat_analise_inadimplencia_parcelas</c>. Used to keep the DW
/// <c>NEGATIVADO</c> column in sync with the Serasa PEFIN flow:
/// <list type="bullet">
///   <item>SIM — when a negativacao webhook returns success.</item>
///   <item>NAO — when a baixa concludes successfully (webhook or RM mode).</item>
/// </list>
/// All operations are best-effort: failures must NOT break the calling
/// transaction. Implementations should log and return rows affected.
/// </summary>
public interface IInadimplenciaParcelaWriteService
{
    /// <summary>
    /// Updates the <c>NEGATIVADO</c> flag for parcelas matching
    /// <paramref name="numVenda"/> + <paramref name="dataVencimento"/>.
    /// </summary>
    /// <param name="numVenda">Sale number.</param>
    /// <param name="dataVencimento">Parcela due date (yyyy-MM-dd).</param>
    /// <param name="negativado">true → 'SIM'; false → 'NAO'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected (0 if none found).</returns>
    Task<int> SetNegativadoByVendaEVencimentoAsync(
        int numVenda,
        DateOnly dataVencimento,
        bool negativado,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the <c>NEGATIVADO</c> flag for the parcela matching
    /// <paramref name="numeroDocumento"/> (exact match on
    /// <c>NUMERO_DOCUMENTO</c>). Used by the RM bypass flow where the
    /// document number is the only identifier known.
    /// </summary>
    /// <param name="numeroDocumento">Document number from RM (NUMERO_DOCUMENTO).</param>
    /// <param name="negativado">true → 'SIM'; false → 'NAO'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected (0 if none found).</returns>
    Task<int> SetNegativadoByNumeroDocumentoAsync(
        string numeroDocumento,
        bool negativado,
        CancellationToken cancellationToken = default);
}

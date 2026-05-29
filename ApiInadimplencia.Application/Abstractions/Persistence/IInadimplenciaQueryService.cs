namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Query service for retrieving inadimplencia data from the Data Warehouse.
/// This service queries DW.fat_analise_inadimplencia_v4 and DW.vw_fiadores_por_venda
/// to provide data needed for Serasa PEFIN preview and negativation operations.
/// </summary>
public interface IInadimplenciaQueryService
{
    /// <summary>
    /// Retrieves inadimplencia sale data from DW.fat_analise_inadimplencia_v4.
    /// Only returns data when INADIMPLENTE='SIM' (case-insensitive, trimmed).
    /// </summary>
    /// <param name="numVenda">Sale number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sale data with address and guarantor information, or null if not found or not inadimplente.</returns>
    Task<InadimplenciaQueryResult?> GetVendaAsync(int numVenda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves guarantor list from DW.vw_fiadores_por_venda for a given sale.
    /// Filters by TIPO_ASSOCIACAO in {FIADOR, CONJUGE, CESSIONARIO, COOBRIGADO, CO-OBRIGADO, CO OBRIGADO}
    /// (Latin1_General_CI_AI collation).
    /// </summary>
    /// <param name="numVenda">Sale number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of eligible guarantors, ordered by DATA_CADASTRO DESC, NOME ASC.</returns>
    Task<IReadOnlyList<FiadorQueryResult>> ListFiadoresAsync(int numVenda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves eligible debts (parcelas) for a sale from DW.fat_analise_inadimplencia_parcelas.
    /// Calculates days overdue from DATAVENCIMENTO to current date and marks each parcela as elegivel
    /// if DIAS_ATRASO > diasAtrasoMinimo.
    /// </summary>
    /// <param name="numVenda">Sale number.</param>
    /// <param name="diasAtrasoMinimo">Minimum days overdue for eligibility (default 60).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Eligible debts data, or null if sale not found.</returns>
    Task<DividasElegiveisQueryResult?> GetDividasElegiveisAsync(int numVenda, int diasAtrasoMinimo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result DTO for inadimplencia sale query from DW.fat_analise_inadimplencia_v4.
/// All documents are digits-only (no punctuation/masking).
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="DocumentoDevedor">Debtor document (CPF/CNPJ), digits-only.</param>
/// <param name="NomeDevedor">Debtor name.</param>
/// <param name="Cliente">Client name (normalized).</param>
/// <param name="Empreendimento">Enterprise/Project name (normalized).</param>
/// <param name="Bloco">Block name (normalized).</param>
/// <param name="Unidade">Unit name (normalized).</param>
/// <param name="Valor">Debt value (decimal).</param>
/// <param name="DataVencimento">Due date (yyyy-MM-dd format as DateOnly).</param>
/// <param name="Endereco">Debtor address (nullable if address data incomplete).</param>
public sealed record InadimplenciaQueryResult(
    int NumVenda,
    string DocumentoDevedor,
    string NomeDevedor,
    string Cliente,
    string Empreendimento,
    string Bloco,
    string Unidade,
    decimal Valor,
    DateOnly DataVencimento,
    EnderecoDto? Endereco);

/// <summary>
/// Address DTO with all fields from the DW.
/// Optional fields (complement, number) may be null.
/// </summary>
/// <param name="ZipCode">CEP (8 digits).</param>
/// <param name="AddressLine">Street name with number combined.</param>
/// <param name="District">Bairro.</param>
/// <param name="City">Cidade/Município.</param>
/// <param name="State">UF/Estado (2 letters).</param>
/// <param name="Complement">Complemento (optional).</param>
/// <param name="Number">Número (optional).</param>
public sealed record EnderecoDto(
    string ZipCode,
    string AddressLine,
    string District,
    string City,
    string State,
    string? Complement = null,
    string? Number = null);

/// <summary>
/// Result DTO for guarantor query from DW.vw_fiadores_por_venda.
/// All documents are digits-only (no punctuation/masking).
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="IdAssociado">Associate ID.</param>
/// <param name="IdPessoa">Person ID.</param>
/// <param name="Nome">Guarantor name.</param>
/// <param name="Documento">Guarantor document (CPF/CNPJ), digits-only.</param>
/// <param name="TipoAssociacao">Association type (FIADOR, CONJUGE, CESSIONARIO, COOBRIGADO, etc.).</param>
/// <param name="Endereco">Guarantor address (nullable if address data incomplete).</param>
/// <param name="DataCadastro">Registration date.</param>
public sealed record FiadorQueryResult(
    int NumVenda,
    string IdAssociado,
    string IdPessoa,
    string Nome,
    string Documento,
    string TipoAssociacao,
    EnderecoDto? Endereco,
    DateTime DataCadastro);

/// <summary>
/// Result DTO for eligible debts query from DW.fat_analise_inadimplencia_parcelas.
/// </summary>
/// <param name="NumVenda">Sale number.</param>
/// <param name="Cliente">Client name.</param>
/// <param name="Cpf">Client CPF (digits-only).</param>
/// <param name="ContractNumber">Contract number.</param>
/// <param name="Parcelas">List of parcelas with eligibility flag.</param>
/// <param name="Endereco">Client address (nullable when no matching client record).</param>
public sealed record DividasElegiveisQueryResult(
    int NumVenda,
    string Cliente,
    string Cpf,
    string ContractNumber,
    IReadOnlyList<ParcelaElegivelDto> Parcelas,
    EnderecoDto? Endereco = null);

/// <summary>
/// DTO for a single parcela (installment) with eligibility information.
/// </summary>
/// <param name="Id">Parcela ID.</param>
/// <param name="Valor">Parcela value.</param>
/// <param name="Vencimento">Due date.</param>
/// <param name="DiasAtraso">Days overdue (calculated from due date to current date).</param>
/// <param name="Elegivel">Whether this parcela is eligible for negativacao (DiasAtraso > threshold).</param>
public sealed record ParcelaElegivelDto(
    int Id,
    decimal Valor,
    DateOnly Vencimento,
    int DiasAtraso,
    bool Elegivel);

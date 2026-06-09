using ApiInadimplencia.Application.Abstractions.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// ADO.NET implementation of <see cref="IInadimplenciaQueryService"/> that queries
/// the Data Warehouse for inadimplencia sale and guarantor data.
/// </summary>
public sealed class InadimplenciaQueryService(
    SqlServerConnectionFactory connectionFactory,
    ILogger<InadimplenciaQueryService> logger) : IInadimplenciaQueryService
{
    private const string TableInadimplencia = "DW.fat_analise_inadimplencia_v4";
    private const string TableCliente = "DW.fat_comercial_cliente_cv";
    private const string ViewGuarantors = "DW.vw_fiadores_por_venda";
    private const string TableParcelas = "DW.fat_analise_inadimplencia_parcelas";

    /// <inheritdoc />
    public async Task<InadimplenciaQueryResult?> GetVendaAsync(int numVenda, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        const string query = $"""
            SELECT TOP 1
                f.NUM_VENDA,
                f.CPF_CNPJ,
                c.NOME,
                f.CLIENTE,
                f.EMPREENDIMENTO,
                f.BLOCO,
                f.UNIDADE,
                NULLIF(LTRIM(RTRIM(f.INADIMPLENTE)), '') AS INADIMPLENTE,
                CONVERT(varchar(10), f.VENCIMENTO_MAIS_ANTIGO, 23) AS DATA_VENCIMENTO,
                CAST(f.VALOR_TOTAL_EM_ABERTO AS decimal(18,2)) AS VALOR_TOTAL,
                CAST(f.VALOR_INADIMPLENTE AS decimal(18,2)) AS VALOR_SOMENTE_INADIMPLENTE,
                c.CEP,
                c.LOGRADOURO,
                c.ENDERECO,
                c.NUMERO,
                c.COMPLEMENTO,
                c.BAIRRO,
                c.CIDADE,
                c.ESTADO
            FROM {TableInadimplencia} f
            LEFT JOIN {TableCliente} c ON REPLACE(REPLACE(REPLACE(f.CPF_CNPJ, '.', ''), '-', ''), '/', '') = c.documento
            WHERE f.NUM_VENDA = @numVenda
              AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            """;

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@numVenda", numVenda);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return MapInadimplenciaRow(reader);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FiadorQueryResult>> ListFiadoresAsync(int numVenda, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        // LEFT JOIN com a tabela de clientes pelo DOCUMENTO (digitos) para obter endereco
        // completo do fiador (CEP, LOGRADOURO, NUMERO, BAIRRO, CIDADE, ESTADO). A coluna
        // ENDERECO da view de fiadores guarda apenas logradouro/bairro como string solta
        // e nao e suficiente para o payload Serasa (que exige zipCode, addressLine, district,
        // city, state). Aliases CLI_* evitam colisao com a coluna ENDERECO da view.
        const string query = $"""
            SELECT
                f.NUM_VENDA,
                f.ID_ASSOCIADO,
                f.ID_RESERVA,
                f.ID_PESSOA,
                f.NOME,
                f.DOCUMENTO,
                f.DATA_CADASTRO,
                f.RENDA_FAMILIAR,
                f.TIPO_ASSOCIACAO,
                f.ENDERECO,
                c.CEP        AS CLI_CEP,
                c.LOGRADOURO AS CLI_LOGRADOURO,
                c.ENDERECO   AS CLI_ENDERECO,
                c.NUMERO     AS CLI_NUMERO,
                c.COMPLEMENTO AS CLI_COMPLEMENTO,
                c.BAIRRO     AS CLI_BAIRRO,
                c.CIDADE     AS CLI_CIDADE,
                c.ESTADO     AS CLI_ESTADO
            FROM {ViewGuarantors} f
            LEFT JOIN {TableCliente} c
                ON REPLACE(REPLACE(REPLACE(f.DOCUMENTO, '.', ''), '-', ''), '/', '') = c.documento
            WHERE f.NUM_VENDA = @numVenda
              AND UPPER(LTRIM(RTRIM(COALESCE(f.TIPO_ASSOCIACAO, '')))) COLLATE Latin1_General_CI_AI
                IN ('FIADOR', 'CONJUGE', 'CESSIONARIO', 'COOBRIGADO', 'CO-OBRIGADO', 'CO OBRIGADO')
            ORDER BY f.DATA_CADASTRO DESC, f.NOME ASC
            """;

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@numVenda", numVenda);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        
        var results = new List<FiadorQueryResult>();
        
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapFiadorRow(reader));
        }

        return results.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<DividasElegiveisQueryResult?> GetDividasElegiveisAsync(int numVenda, int diasAtrasoMinimo, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        // First, get the sale summary (cliente, CPF, address) from the main inadimplencia
        // table joined with the client master. contractNumber is not persisted in
        // DW.fat_analise_inadimplencia_v4, so we fall back to numVenda.ToString() (same
        // convention used elsewhere in the module). LEFT JOIN keeps the sale visible even
        // when the client record is missing — in that case Endereco will be null.
        const string summaryQuery = $"""
            SELECT TOP 1
                f.NUM_VENDA,
                f.CPF_CNPJ,
                c.NOME,
                c.CEP,
                c.LOGRADOURO,
                c.ENDERECO,
                c.NUMERO,
                c.COMPLEMENTO,
                c.BAIRRO,
                c.CIDADE,
                c.ESTADO
            FROM {TableInadimplencia} f
            LEFT JOIN {TableCliente} c ON REPLACE(REPLACE(REPLACE(f.CPF_CNPJ, '.', ''), '-', ''), '/', '') = c.documento
            WHERE f.NUM_VENDA = @numVenda
              AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            """;

        string cliente = string.Empty;
        string cpf = string.Empty;
        string contractNumber = numVenda.ToString();
        EnderecoDto? endereco = null;

        using (var summaryCommand = new SqlCommand(summaryQuery, connection))
        {
            summaryCommand.Parameters.AddWithValue("@numVenda", numVenda);
            using var summaryReader = await summaryCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            
            if (!await summaryReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null; // Sale not found or not inadimplente
            }

            cliente = NormalizeString(GetNullableString(summaryReader, "NOME")) ?? "Cliente";
            cpf = DigitsOnly(GetNullableString(summaryReader, "CPF_CNPJ") ?? "");
            endereco = MapEndereco(summaryReader);
        }

        // Now query the parcelas.
        // Schema of DW.fat_analise_inadimplencia_parcelas: NUM_VENDA, DATAVENCIMENTO, VALOR,
        // INADIMPLENTE ('SIM'/'NAO'), NEGATIVADO ('SIM'/'NAO'/NULL). There is no native
        // primary key column, so we synthesize PARCELA_ID with ROW_NUMBER() ordered by
        // DATAVENCIMENTO ASC (stable for the lifetime of the response).
        const string parcelasQuery = $"""
            SELECT
                ROW_NUMBER() OVER (ORDER BY DATAVENCIMENTO ASC) AS PARCELA_ID,
                CONVERT(varchar(10), DATAVENCIMENTO, 23) AS DATAVENCIMENTO,
                CAST(VALOR AS decimal(18,2)) AS VALOR,
                INADIMPLENTE,
                NEGATIVADO,
                DATEDIFF(day, DATAVENCIMENTO, GETDATE()) AS DIAS_ATRASO
            FROM {TableParcelas}
            WHERE NUM_VENDA = @numVenda
              AND UPPER(LTRIM(RTRIM(COALESCE(INADIMPLENTE, '')))) = 'SIM'
            ORDER BY DATAVENCIMENTO ASC
            """;

        using var parcelasCommand = new SqlCommand(parcelasQuery, connection);
        parcelasCommand.Parameters.AddWithValue("@numVenda", numVenda);

        using var parcelasReader = await parcelasCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        
        var parcelas = new List<ParcelaElegivelDto>();
        
        while (await parcelasReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = (int)parcelasReader.GetInt64(parcelasReader.GetOrdinal("PARCELA_ID"));
            var valor = parcelasReader.GetDecimal(parcelasReader.GetOrdinal("VALOR"));
            var dataVencimentoStr = parcelasReader.GetString(parcelasReader.GetOrdinal("DATAVENCIMENTO"));
            var diasAtraso = parcelasReader.GetInt32(parcelasReader.GetOrdinal("DIAS_ATRASO"));
            var negativado = GetNullableString(parcelasReader, "NEGATIVADO");
            var jaNegativada = string.Equals(
                (negativado ?? string.Empty).Trim(),
                "SIM",
                StringComparison.OrdinalIgnoreCase);
            
            DateOnly vencimento;
            try
            {
                vencimento = DateOnly.ParseExact(dataVencimentoStr, "yyyy-MM-dd", null);
            }
            catch
            {
                // If date parsing fails, skip this parcela
                logger.LogWarning("Failed to parse date DATAVENCIMENTO '{DataVencimento}' for parcela {ParcelaId}", dataVencimentoStr, id);
                continue;
            }

            var elegivel = diasAtraso > diasAtrasoMinimo && !jaNegativada;
            
            parcelas.Add(new ParcelaElegivelDto(
                Id: id,
                Valor: valor,
                Vencimento: vencimento,
                DiasAtraso: diasAtraso,
                Elegivel: elegivel));
        }

        return new DividasElegiveisQueryResult(
            NumVenda: numVenda,
            Cliente: cliente,
            Cpf: cpf,
            ContractNumber: contractNumber,
            Parcelas: parcelas.AsReadOnly(),
            Endereco: endereco);
    }

    private static InadimplenciaQueryResult MapInadimplenciaRow(SqlDataReader reader)
    {
        var numVenda = reader.GetInt32(reader.GetOrdinal("NUM_VENDA"));
        var documentoDevedor = DigitsOnly(reader.GetString(reader.GetOrdinal("CPF_CNPJ")));
        var nomeDevedor = GetNullableString(reader, "NOME") ?? "Cliente";
        
        // Normalize optional string fields
        var cliente = NormalizeString(GetNullableString(reader, "CLIENTE"));
        var empreendimento = NormalizeString(GetNullableString(reader, "EMPREENDIMENTO"));
        var bloco = NormalizeString(GetNullableString(reader, "BLOCO"));
        var unidade = NormalizeString(GetNullableString(reader, "UNIDADE"));
        
        // Valor: prefer VALOR_INADIMPLENTE, fallback to VALOR_TOTAL
        var valor = reader.IsDBNull(reader.GetOrdinal("VALOR_SOMENTE_INADIMPLENTE"))
            ? reader.GetDecimal(reader.GetOrdinal("VALOR_TOTAL"))
            : reader.GetDecimal(reader.GetOrdinal("VALOR_SOMENTE_INADIMPLENTE"));
        
        var dataVencimentoStr = reader.GetString(reader.GetOrdinal("DATA_VENCIMENTO"));
        var dataVencimento = DateOnly.ParseExact(dataVencimentoStr, "yyyy-MM-dd", null);
        
        var endereco = MapEndereco(reader);
        
        return new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: documentoDevedor,
            NomeDevedor: nomeDevedor,
            Cliente: cliente,
            Empreendimento: empreendimento,
            Bloco: bloco,
            Unidade: unidade,
            Valor: valor,
            DataVencimento: dataVencimento,
            Endereco: endereco);
    }

    private static FiadorQueryResult MapFiadorRow(SqlDataReader reader)
    {
        var numVenda = reader.GetInt32(reader.GetOrdinal("NUM_VENDA"));
        var idAssociado = GetIdAsString(reader, "ID_ASSOCIADO");
        var idPessoa = GetIdAsString(reader, "ID_PESSOA");
        var nome = reader.GetString(reader.GetOrdinal("NOME"));
        var documento = DigitsOnly(reader.GetString(reader.GetOrdinal("DOCUMENTO")));
        var tipoAssociacao = reader.GetString(reader.GetOrdinal("TIPO_ASSOCIACAO")).Trim().ToUpperInvariant();
        var dataCadastro = reader.GetDateTime(reader.GetOrdinal("DATA_CADASTRO"));
        
        var endereco = MapEnderecoFiador(reader);
        
        return new FiadorQueryResult(
            NumVenda: numVenda,
            IdAssociado: idAssociado,
            IdPessoa: idPessoa,
            Nome: nome,
            Documento: documento,
            TipoAssociacao: tipoAssociacao,
            Endereco: endereco,
            DataCadastro: dataCadastro);
    }

    private static EnderecoDto? MapEndereco(SqlDataReader reader)
    {
        var cep = GetNullableString(reader, "CEP");
        var logradouro = GetNullableString(reader, "LOGRADOURO");
        var endereco = GetNullableString(reader, "ENDERECO");
        var numero = GetNullableString(reader, "NUMERO");
        var complemento = GetNullableString(reader, "COMPLEMENTO");
        var bairro = GetNullableString(reader, "BAIRRO");
        var cidade = GetNullableString(reader, "CIDADE");
        var estado = GetNullableString(reader, "ESTADO");

        var zipCode = DigitsOnly(cep ?? "");
        var addressLine = BuildAddressLine(logradouro, endereco, numero);
        var district = bairro ?? "";
        var city = cidade ?? "";
        var state = estado ?? "";

        // Return null if no address data is available
        if (string.IsNullOrWhiteSpace(zipCode) && 
            string.IsNullOrWhiteSpace(addressLine) && 
            string.IsNullOrWhiteSpace(district) && 
            string.IsNullOrWhiteSpace(city) && 
            string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        return new EnderecoDto(
            ZipCode: zipCode,
            AddressLine: addressLine,
            District: district,
            City: city,
            State: state,
            Complement: complemento,
            Number: numero);
    }

    /// <summary>
    /// Monta o endereco do fiador a partir das colunas CLI_* trazidas pelo JOIN com
    /// DW.fat_comercial_cliente_cv. Retorna null se o fiador nao tiver registro de
    /// cliente correspondente (todas as colunas CLI_* sao DBNull).
    /// </summary>
    private static EnderecoDto? MapEnderecoFiador(SqlDataReader reader)
    {
        var cep = GetNullableString(reader, "CLI_CEP");
        var logradouro = GetNullableString(reader, "CLI_LOGRADOURO");
        var enderecoFallback = GetNullableString(reader, "CLI_ENDERECO");
        var numero = GetNullableString(reader, "CLI_NUMERO");
        var complemento = GetNullableString(reader, "CLI_COMPLEMENTO");
        var bairro = GetNullableString(reader, "CLI_BAIRRO");
        var cidade = GetNullableString(reader, "CLI_CIDADE");
        var estado = GetNullableString(reader, "CLI_ESTADO");

        // Se todos os campos do cliente sao nulos, o fiador nao foi encontrado em
        // fat_comercial_cliente_cv (provavelmente cadastro pendente). Retorna null
        // para o handler poder reportar Validacao Falhou - guarantor.address.address.
        if (string.IsNullOrWhiteSpace(cep)
            && string.IsNullOrWhiteSpace(logradouro)
            && string.IsNullOrWhiteSpace(enderecoFallback)
            && string.IsNullOrWhiteSpace(numero)
            && string.IsNullOrWhiteSpace(bairro)
            && string.IsNullOrWhiteSpace(cidade)
            && string.IsNullOrWhiteSpace(estado))
        {
            return null;
        }

        var zipCode = DigitsOnly(cep ?? "");
        var addressLine = BuildAddressLine(logradouro, enderecoFallback, numero);

        return new EnderecoDto(
            ZipCode: zipCode,
            AddressLine: addressLine,
            District: bairro ?? "",
            City: cidade ?? "",
            State: estado ?? "",
            Number: numero,
            Complement: complemento);
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Reads an ID column that may be stored as INT, BIGINT, DECIMAL or NVARCHAR
    /// in the DW and returns it as a string. Returns empty string for NULL.
    /// </summary>
    private static string GetIdAsString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            string s => s,
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
            short sh => sh.ToString(System.Globalization.CultureInfo.InvariantCulture),
            byte b => b.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal d => ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static string NormalizeString(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string DigitsOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string BuildAddressLine(string? logradouro, string? endereco, string? numero)
    {
        var street = logradouro?.Trim() ?? string.Empty;
        var fallbackAddress = endereco?.Trim() ?? string.Empty;
        var number = numero?.Trim() ?? string.Empty;

        // Prefer LOGRADOURO; fall back to ENDERECO when LOGRADOURO is empty/null.
        var baseAddress = !string.IsNullOrWhiteSpace(street) ? street : fallbackAddress;

        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(number)
            ? baseAddress
            : $"{baseAddress}, {number}";
    }

    /// <inheritdoc />
    public async Task<ParcelaPorIdLanQueryResult?> GetParcelaByIdLanAsync(long idLan, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string query = $"""
            SELECT TOP 1
                IDLAN,
                NUM_VENDA,
                NUMERO_DOCUMENTO,
                CONVERT(varchar(10), DATAVENCIMENTO, 23) AS DATAVENCIMENTO,
                CAST(VALOR AS decimal(18,2)) AS VALOR,
                INADIMPLENTE,
                NEGATIVADO,
                DATEDIFF(day, DATAVENCIMENTO, GETDATE()) AS DIAS_ATRASO
            FROM {TableParcelas}
            WHERE IDLAN = @idLan
            """;

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@idLan", idLan);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var idLanValue = reader.GetInt64(reader.GetOrdinal("IDLAN"));
        var numVenda = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("NUM_VENDA")));
        var numeroDocumento = GetNullableString(reader, "NUMERO_DOCUMENTO");
        var dataVencimentoStr = reader.GetString(reader.GetOrdinal("DATAVENCIMENTO"));
        var valor = reader.GetDecimal(reader.GetOrdinal("VALOR"));
        var inadimplente = GetNullableString(reader, "INADIMPLENTE");
        var negativado = GetNullableString(reader, "NEGATIVADO");
        var diasAtraso = reader.GetInt32(reader.GetOrdinal("DIAS_ATRASO"));

        if (!DateOnly.TryParseExact(dataVencimentoStr, "yyyy-MM-dd", out var dataVencimento))
        {
            logger.LogWarning(
                "Failed to parse DATAVENCIMENTO '{DataVencimento}' for IDLAN {IdLan}",
                dataVencimentoStr, idLanValue);
            return null;
        }

        return new ParcelaPorIdLanQueryResult(
            IdLan: idLanValue,
            NumVenda: numVenda,
            NumeroDocumento: numeroDocumento,
            DataVencimento: dataVencimento,
            Valor: valor,
            Inadimplente: inadimplente,
            Negativado: negativado,
            DiasAtraso: diasAtraso);
    }
}

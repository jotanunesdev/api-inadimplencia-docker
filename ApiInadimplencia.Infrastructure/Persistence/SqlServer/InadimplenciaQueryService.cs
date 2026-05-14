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
        
        const string query = $"""
            SELECT
                NUM_VENDA,
                ID_ASSOCIADO,
                ID_RESERVA,
                ID_PESSOA,
                NOME,
                DOCUMENTO,
                DATA_CADASTRO,
                RENDA_FAMILIAR,
                TIPO_ASSOCIACAO,
                ENDERECO
            FROM {ViewGuarantors}
            WHERE NUM_VENDA = @numVenda
              AND UPPER(LTRIM(RTRIM(COALESCE(TIPO_ASSOCIACAO, '')))) COLLATE Latin1_General_CI_AI
                IN ('FIADOR', 'CONJUGE', 'CESSIONARIO', 'COOBRIGADO', 'CO-OBRIGADO', 'CO OBRIGADO')
            ORDER BY DATA_CADASTRO DESC, NOME ASC
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
        var idAssociado = reader.GetString(reader.GetOrdinal("ID_ASSOCIADO"));
        var idPessoa = reader.GetString(reader.GetOrdinal("ID_PESSOA"));
        var nome = reader.GetString(reader.GetOrdinal("NOME"));
        var documento = DigitsOnly(reader.GetString(reader.GetOrdinal("DOCUMENTO")));
        var tipoAssociacao = reader.GetString(reader.GetOrdinal("TIPO_ASSOCIACAO")).Trim().ToUpperInvariant();
        var dataCadastro = reader.GetDateTime(reader.GetOrdinal("DATA_CADASTRO"));
        
        var endereco = MapEnderecoFromJson(reader);
        
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

    private static EnderecoDto? MapEnderecoFromJson(SqlDataReader reader)
    {
        // The view has an ENDERECO column which may be JSON or structured data
        // For now, we'll treat it as a string and try to parse if it's JSON
        var enderecoJson = GetNullableString(reader, "ENDERECO");
        
        if (string.IsNullOrWhiteSpace(enderecoJson))
        {
            return null;
        }

        // If it's JSON, we'd need to parse it. For now, return null
        // This will need to be enhanced based on the actual data structure in the view
        // TODO: Parse JSON address when the view structure is known
        return null;
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
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
        var street = logradouro?.Trim() ?? "";
        var fallbackAddress = endereco?.Trim() ?? "";
        var number = numero?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(number))
        {
            return $"{street}, {number}";
        }

        return street ?? fallbackAddress;
    }
}

namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Static constants used by the Serasa PEFIN integration (source of truth: Node reference module).
/// </summary>
public static class SerasaPefinConstants
{
    /// <summary>Default natureza da dívida (FI = Financiamento).</summary>
    public const string CategoryId = "FI";

    /// <summary>Debt type sent to Serasa.</summary>
    public const string DebtType = "PEFIN";

    /// <summary>Minimum debt value accepted for negativação (R$ 10,00).</summary>
    public const decimal MinValue = 10.0m;

    /// <summary>Number of decimals used when rounding monetary values.</summary>
    public const int ValueDecimals = 2;

    /// <summary>
    /// UAT authorized documents (only digits). When <c>INAD_SERASA_USE_UAT_DEFAULTS=true</c>,
    /// only documents from this mass can be sent to Serasa homologação.
    /// </summary>
    public static readonly IReadOnlySet<string> UatAuthorizedDocuments = new HashSet<string>
    {
        // CPFs
        "00001209523", // CLIENTE TESTE ABCB
        "00008441448", // BJRNRNSD OIOIE
        "07420565899", // TESTE CPF SEM POSITIVO
        "04236798484", // NCUH KLCOHKKHH ECAJAE NCGMLU
        "16881670052", // TST PEFIN
        "11572467886", // TST FLEX

        // CNPJs
        "43557445000180", // ESFERA ARENA E NEGOCIOS SPE LTDA
        "00079854000105", // U F NXALWPULN ZK EWCQIXG
        "16202491000193", // CNPJ CONTRATO SERASA (creditor)
    };

    /// <summary>
    /// Statuses that block a new request for the same (NUM_VENDA, CONTRACT_NUMBER,
    /// DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, TIPO_REGISTRO) combination.
    /// </summary>
    public static readonly IReadOnlySet<SerasaPefinStatus> ActiveStatuses = new HashSet<SerasaPefinStatus>
    {
        SerasaPefinStatus.AguardandoAprovacao,
        SerasaPefinStatus.Aprovada,
        SerasaPefinStatus.PendenteEnvio,
        SerasaPefinStatus.EnviadoSerasa,
        SerasaPefinStatus.AguardandoRetorno,
    };

    /// <summary>
    /// Statuses that indicate the solicitation reached a final state.
    /// </summary>
    public static readonly IReadOnlySet<SerasaPefinStatus> FinalStatuses = new HashSet<SerasaPefinStatus>
    {
        SerasaPefinStatus.NegativadoSucesso,
        SerasaPefinStatus.NegativadoErro,
        SerasaPefinStatus.BaixadoSucesso,
        SerasaPefinStatus.BaixadoErro,
    };

    /// <summary>
    /// Maps the persistent enum value to the canonical UPPER_SNAKE_CASE string used by the legacy Node API
    /// (and stored in the SQL <c>STATUS</c> column).
    /// </summary>
    public static string ToDbValue(this SerasaPefinStatus status) => status switch
    {
        SerasaPefinStatus.AguardandoAprovacao => "AGUARDANDO_APROVACAO",
        SerasaPefinStatus.Aprovada => "APROVADA",
        SerasaPefinStatus.Rejeitada => "REJEITADA",
        SerasaPefinStatus.AprovadaFalhaEnvio => "APROVADA_FALHA_ENVIO",
        SerasaPefinStatus.PendenteEnvio => "PENDENTE_ENVIO",
        SerasaPefinStatus.EnviadoSerasa => "ENVIADO_SERASA",
        SerasaPefinStatus.AguardandoRetorno => "AGUARDANDO_RETORNO",
        SerasaPefinStatus.NegativadoSucesso => "NEGATIVADO_SUCESSO",
        SerasaPefinStatus.NegativadoErro => "NEGATIVADO_ERRO",
        SerasaPefinStatus.BaixaEnviada => "BAIXA_ENVIADA",
        SerasaPefinStatus.BaixaAguardandoRetorno => "BAIXA_AGUARDANDO_RETORNO",
        SerasaPefinStatus.BaixadoSucesso => "BAIXADO_SUCESSO",
        SerasaPefinStatus.BaixadoErro => "BAIXADO_ERRO",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>
    /// Parses a canonical UPPER_SNAKE_CASE string (from SQL) back to <see cref="SerasaPefinStatus"/>.
    /// </summary>
    public static SerasaPefinStatus ParseStatus(string value) => value switch
    {
        "AGUARDANDO_APROVACAO" => SerasaPefinStatus.AguardandoAprovacao,
        "APROVADA" => SerasaPefinStatus.Aprovada,
        "REJEITADA" => SerasaPefinStatus.Rejeitada,
        "APROVADA_FALHA_ENVIO" => SerasaPefinStatus.AprovadaFalhaEnvio,
        "PENDENTE_ENVIO" => SerasaPefinStatus.PendenteEnvio,
        "ENVIADO_SERASA" => SerasaPefinStatus.EnviadoSerasa,
        "AGUARDANDO_RETORNO" => SerasaPefinStatus.AguardandoRetorno,
        "NEGATIVADO_SUCESSO" => SerasaPefinStatus.NegativadoSucesso,
        "NEGATIVADO_ERRO" => SerasaPefinStatus.NegativadoErro,
        "BAIXA_ENVIADA" => SerasaPefinStatus.BaixaEnviada,
        "BAIXA_AGUARDANDO_RETORNO" => SerasaPefinStatus.BaixaAguardandoRetorno,
        "BAIXADO_SUCESSO" => SerasaPefinStatus.BaixadoSucesso,
        "BAIXADO_ERRO" => SerasaPefinStatus.BaixadoErro,
        _ => throw new ArgumentException($"Unknown Serasa PEFIN status: '{value}'.", nameof(value)),
    };

    /// <summary>
    /// Maps the record type enum to the canonical UPPER_SNAKE_CASE string stored in SQL.
    /// </summary>
    public static string ToDbValue(this SerasaPefinRecordType tipo) => tipo switch
    {
        SerasaPefinRecordType.Principal => "PRINCIPAL",
        SerasaPefinRecordType.Garantidor => "GARANTIDOR",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>
    /// Parses the canonical UPPER_SNAKE_CASE string back to <see cref="SerasaPefinRecordType"/>.
    /// </summary>
    public static SerasaPefinRecordType ParseRecordType(string value) => value switch
    {
        "PRINCIPAL" => SerasaPefinRecordType.Principal,
        "GARANTIDOR" => SerasaPefinRecordType.Garantidor,
        _ => throw new ArgumentException($"Unknown TIPO_REGISTRO: '{value}'.", nameof(value)),
    };

    /// <summary>
    /// Returns <paramref name="value"/> containing only digit characters; empty string for null.
    /// </summary>
    public static string DigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var ch in value)
        {
            if (ch >= '0' && ch <= '9')
            {
                buffer[length++] = ch;
            }
        }

        return length == 0 ? string.Empty : new string(buffer[..length]);
    }
}

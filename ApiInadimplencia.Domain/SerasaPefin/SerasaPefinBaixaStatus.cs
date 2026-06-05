namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Status de ciclo de vida de uma solicitação de baixa (write-off) de dívida no Serasa PEFIN.
/// Persistido como string canônica em <c>dbo.SERASA_PEFIN_BAIXAS.STATUS</c>.
/// </summary>
public enum SerasaPefinBaixaStatus
{
    /// <summary>Solicitação criada e aguardando aprovação por um aprovador.</summary>
    AguardandoAprovacao,

    /// <summary>Solicitação aprovada, pronta para ser preparada para envio ao Serasa.</summary>
    Aprovada,

    /// <summary>Solicitação rejeitada por um aprovador (estado terminal).</summary>
    Rejeitada,

    /// <summary>Solicitação aprovada porém falhou no envio HTTP antes de receber transactionId.</summary>
    AprovadaFalhaEnvio,

    /// <summary>Solicitação preparada para envio ao Serasa.</summary>
    PendenteEnvio,

    /// <summary>Requisição DELETE enviada ao Serasa (estado transitório).</summary>
    BaixaEnviada,

    /// <summary>Serasa aceitou a baixa e retornou transactionId; aguardando webhook.</summary>
    BaixaAguardandoRetorno,

    /// <summary>Webhook confirmou conclusão da baixa com sucesso (estado terminal de sucesso).</summary>
    BaixadoSucesso,

    /// <summary>Webhook reportou erro na baixa; permite reenvio até atingir o limite de tentativas.</summary>
    BaixadoErro,
}

/// <summary>
/// Mapeamento canônico de <see cref="SerasaPefinBaixaStatus"/> para os valores
/// armazenados na coluna <c>STATUS</c> da tabela <c>SERASA_PEFIN_BAIXAS</c>.
/// </summary>
public static class SerasaPefinBaixaStatusExtensions
{
    /// <summary>Converte o enum para o valor canônico UPPER_SNAKE_CASE persistido em SQL.</summary>
    public static string ToDbValue(this SerasaPefinBaixaStatus status) => status switch
    {
        SerasaPefinBaixaStatus.AguardandoAprovacao => "AGUARDANDO_APROVACAO",
        SerasaPefinBaixaStatus.Aprovada => "APROVADA",
        SerasaPefinBaixaStatus.Rejeitada => "REJEITADA",
        SerasaPefinBaixaStatus.AprovadaFalhaEnvio => "APROVADA_FALHA_ENVIO",
        SerasaPefinBaixaStatus.PendenteEnvio => "PENDENTE_ENVIO",
        SerasaPefinBaixaStatus.BaixaEnviada => "BAIXA_ENVIADA",
        SerasaPefinBaixaStatus.BaixaAguardandoRetorno => "BAIXA_AGUARDANDO_RETORNO",
        SerasaPefinBaixaStatus.BaixadoSucesso => "BAIXADO_SUCESSO",
        SerasaPefinBaixaStatus.BaixadoErro => "BAIXADO_ERRO",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Converte o valor canônico SQL de volta para o enum.</summary>
    public static SerasaPefinBaixaStatus ParseBaixaStatus(string value) => value switch
    {
        "AGUARDANDO_APROVACAO" => SerasaPefinBaixaStatus.AguardandoAprovacao,
        "APROVADA" => SerasaPefinBaixaStatus.Aprovada,
        "REJEITADA" => SerasaPefinBaixaStatus.Rejeitada,
        "APROVADA_FALHA_ENVIO" => SerasaPefinBaixaStatus.AprovadaFalhaEnvio,
        "PENDENTE_ENVIO" => SerasaPefinBaixaStatus.PendenteEnvio,
        "BAIXA_ENVIADA" => SerasaPefinBaixaStatus.BaixaEnviada,
        "BAIXA_AGUARDANDO_RETORNO" => SerasaPefinBaixaStatus.BaixaAguardandoRetorno,
        "BAIXADO_SUCESSO" => SerasaPefinBaixaStatus.BaixadoSucesso,
        "BAIXADO_ERRO" => SerasaPefinBaixaStatus.BaixadoErro,
        _ => throw new ArgumentException($"Unknown SerasaPefinBaixaStatus: '{value}'.", nameof(value)),
    };
}

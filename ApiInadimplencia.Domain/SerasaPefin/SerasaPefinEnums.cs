namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Persistent status values for Serasa PEFIN solicitations.
/// </summary>
public enum SerasaPefinStatus
{
    /// <summary>Solicitation was persisted and is waiting to be sent.</summary>
    PendenteEnvio,

    /// <summary>Payload was sent to Serasa.</summary>
    EnviadoSerasa,

    /// <summary>Serasa accepted the request and the API waits for webhook callback.</summary>
    AguardandoRetorno,

    /// <summary>Inclusion finished successfully.</summary>
    NegativadoSucesso,

    /// <summary>Inclusion finished with error.</summary>
    NegativadoErro,

    /// <summary>Removal request was sent.</summary>
    BaixaEnviada,

    /// <summary>Removal was accepted and waits for callback.</summary>
    BaixaAguardandoRetorno,

    /// <summary>Removal finished successfully.</summary>
    BaixadoSucesso,

    /// <summary>Removal finished with error.</summary>
    BaixadoErro,
}

/// <summary>
/// Identifies whether a Serasa PEFIN solicitation represents the main debtor or a guarantor.
/// </summary>
public enum SerasaPefinRecordType
{
    /// <summary>Main debt record.</summary>
    Principal,

    /// <summary>Guarantor record.</summary>
    Garantidor,
}

/// <summary>
/// Event type for Serasa PEFIN webhooks.
/// </summary>
public enum WebhookEventType
{
    /// <summary>Inclusion of main debt record.</summary>
    Inclusao,

    /// <summary>Inclusion of guarantor record.</summary>
    Avalista,

    /// <summary>Removal (baixa) of a record.</summary>
    Baixa,
}

/// <summary>
/// Result type for Serasa PEFIN webhooks.
/// </summary>
public enum WebhookResultado
{
    /// <summary>Operation completed successfully.</summary>
    Sucesso,

    /// <summary>Operation completed with error.</summary>
    Erro,
}


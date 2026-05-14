namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Complete aggregate root representing a row in <c>dbo.SERASA_PEFIN_SOLICITACOES</c>.
/// Carries every column needed by the integration (principal or garantidor), along with
/// the lifecycle transitions expected by the Node reference service.
/// </summary>
public sealed class SerasaPefinSolicitacaoCompleta
{
    /// <summary>Gets the unique identifier (SQL <c>ID</c>).</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the related sale number (SQL <c>NUM_VENDA_FK</c>).</summary>
    public int NumVendaFk { get; private set; }

    /// <summary>Gets the record type (PRINCIPAL or GARANTIDOR).</summary>
    public SerasaPefinRecordType TipoRegistro { get; private set; }

    /// <summary>For garantidores, identifies the principal solicitation id.</summary>
    public Guid? IdSolicitacaoPrincipal { get; private set; }

    /// <summary>Associate identifier (e.g. Fluig ID or DW associate key) used for guarantors.</summary>
    public string? IdAssociado { get; private set; }

    /// <summary>Association type (FIADOR, CONJUGE, CESSINARIO, COOBRIGADO).</summary>
    public string? TipoAssociacao { get; private set; }

    /// <summary>Debtor document (CPF/CNPJ, digits-only).</summary>
    public string DocumentoDevedor { get; private set; } = string.Empty;

    /// <summary>Guarantor document (digits-only). Null for PRINCIPAL records.</summary>
    public string? DocumentoGarantidor { get; private set; }

    /// <summary>Creditor CNPJ (digits-only) used in the Serasa payload.</summary>
    public string DocumentoCredor { get; private set; } = string.Empty;

    /// <summary>Contract number sent to Serasa (usually NUM_VENDA as string).</summary>
    public string ContractNumber { get; private set; } = string.Empty;

    /// <summary>Natureza da dívida (default FI).</summary>
    public string CategoryId { get; private set; } = SerasaPefinConstants.CategoryId;

    /// <summary>Área informante (4 characters defined per creditor).</summary>
    public string AreaInformante { get; private set; } = string.Empty;

    /// <summary>Debt amount in reais, always &gt;= <see cref="SerasaPefinConstants.MinValue"/>.</summary>
    public decimal Valor { get; private set; }

    /// <summary>Debt due date (YYYY-MM-DD).</summary>
    public DateOnly DataVencimento { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public SerasaPefinStatus Status { get; private set; }

    /// <summary>Serasa transaction id returned by the initial POST.</summary>
    public string? TransactionId { get; private set; }

    /// <summary>CADUS key returned via webhook on success.</summary>
    public string? CadusKey { get; private set; }

    /// <summary>CADUS série returned via webhook on success.</summary>
    public string? CadusSerie { get; private set; }

    /// <summary>JSON payload actually sent to Serasa (with sensitive data masked for auditoria).</summary>
    public string PayloadAuditoria { get; private set; } = "{}";

    /// <summary>JSON payload received from the webhook (raw).</summary>
    public string? WebhookPayload { get; private set; }

    /// <summary>Error message when the request failed (set by webhook erro or HTTP failure).</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>HTTP/Serasa error code, when applicable.</summary>
    public int? ErrorStatusCode { get; private set; }

    /// <summary>Usuario solicitante (from authenticated context).</summary>
    public string Operador { get; private set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime DtCriacao { get; private set; }

    /// <summary>Last update timestamp (UTC).</summary>
    public DateTime DtAtualizacao { get; private set; }

    private SerasaPefinSolicitacaoCompleta()
    {
    }

    /// <summary>
    /// Factory used when creating a new solicitation before it is sent to Serasa.
    /// </summary>
    public static SerasaPefinSolicitacaoCompleta Criar(
        int numVendaFk,
        SerasaPefinRecordType tipoRegistro,
        string documentoDevedor,
        string documentoCredor,
        string contractNumber,
        string areaInformante,
        decimal valor,
        DateOnly dataVencimento,
        string operador,
        string payloadAuditoria,
        Guid? idSolicitacaoPrincipal = null,
        string? documentoGarantidor = null,
        string? idAssociado = null,
        string? tipoAssociacao = null,
        string categoryId = SerasaPefinConstants.CategoryId)
    {
        if (numVendaFk <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numVendaFk), "NUM_VENDA_FK must be positive.");
        }

        if (string.IsNullOrWhiteSpace(documentoDevedor))
        {
            throw new ArgumentException("DOCUMENTO_DEVEDOR is required.", nameof(documentoDevedor));
        }

        if (string.IsNullOrWhiteSpace(documentoCredor))
        {
            throw new ArgumentException("DOCUMENTO_CREDOR is required.", nameof(documentoCredor));
        }

        if (string.IsNullOrWhiteSpace(contractNumber))
        {
            throw new ArgumentException("CONTRACT_NUMBER is required.", nameof(contractNumber));
        }

        if (string.IsNullOrWhiteSpace(areaInformante))
        {
            throw new ArgumentException("AREA_INFORMANTE is required.", nameof(areaInformante));
        }

        if (string.IsNullOrWhiteSpace(operador))
        {
            throw new ArgumentException("OPERADOR is required.", nameof(operador));
        }

        if (valor < SerasaPefinConstants.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), $"Valor must be >= {SerasaPefinConstants.MinValue:F2}.");
        }

        if (tipoRegistro == SerasaPefinRecordType.Garantidor
            && string.IsNullOrWhiteSpace(documentoGarantidor))
        {
            throw new ArgumentException("DOCUMENTO_GARANTIDOR is required for GARANTIDOR records.", nameof(documentoGarantidor));
        }

        var now = DateTime.UtcNow;
        return new SerasaPefinSolicitacaoCompleta
        {
            Id = Guid.NewGuid(),
            NumVendaFk = numVendaFk,
            TipoRegistro = tipoRegistro,
            IdSolicitacaoPrincipal = idSolicitacaoPrincipal,
            IdAssociado = idAssociado,
            TipoAssociacao = tipoAssociacao,
            DocumentoDevedor = SerasaPefinConstants.DigitsOnly(documentoDevedor),
            DocumentoGarantidor = documentoGarantidor is null ? null : SerasaPefinConstants.DigitsOnly(documentoGarantidor),
            DocumentoCredor = SerasaPefinConstants.DigitsOnly(documentoCredor),
            ContractNumber = contractNumber.Trim(),
            CategoryId = categoryId,
            AreaInformante = areaInformante.Trim(),
            Valor = Math.Round(valor, SerasaPefinConstants.ValueDecimals, MidpointRounding.AwayFromZero),
            DataVencimento = dataVencimento,
            Status = SerasaPefinStatus.PendenteEnvio,
            PayloadAuditoria = string.IsNullOrEmpty(payloadAuditoria) ? "{}" : payloadAuditoria,
            Operador = operador,
            DtCriacao = now,
            DtAtualizacao = now,
        };
    }

    /// <summary>
    /// Re-hydrates an instance from persistence without running factory invariants.
    /// Prefer <see cref="Criar"/> for brand-new entities.
    /// </summary>
    public static SerasaPefinSolicitacaoCompleta Hydrate(
        Guid id,
        int numVendaFk,
        SerasaPefinRecordType tipoRegistro,
        Guid? idSolicitacaoPrincipal,
        string? idAssociado,
        string? tipoAssociacao,
        string documentoDevedor,
        string? documentoGarantidor,
        string documentoCredor,
        string contractNumber,
        string categoryId,
        string areaInformante,
        decimal valor,
        DateOnly dataVencimento,
        SerasaPefinStatus status,
        string? transactionId,
        string? cadusKey,
        string? cadusSerie,
        string payloadAuditoria,
        string? webhookPayload,
        string? errorMessage,
        int? errorStatusCode,
        string operador,
        DateTime dtCriacao,
        DateTime dtAtualizacao) => new()
    {
        Id = id,
        NumVendaFk = numVendaFk,
        TipoRegistro = tipoRegistro,
        IdSolicitacaoPrincipal = idSolicitacaoPrincipal,
        IdAssociado = idAssociado,
        TipoAssociacao = tipoAssociacao,
        DocumentoDevedor = documentoDevedor,
        DocumentoGarantidor = documentoGarantidor,
        DocumentoCredor = documentoCredor,
        ContractNumber = contractNumber,
        CategoryId = categoryId,
        AreaInformante = areaInformante,
        Valor = valor,
        DataVencimento = dataVencimento,
        Status = status,
        TransactionId = transactionId,
        CadusKey = cadusKey,
        CadusSerie = cadusSerie,
        PayloadAuditoria = payloadAuditoria,
        WebhookPayload = webhookPayload,
        ErrorMessage = errorMessage,
        ErrorStatusCode = errorStatusCode,
        Operador = operador,
        DtCriacao = dtCriacao,
        DtAtualizacao = dtAtualizacao,
    };

    /// <summary>Marks as successfully sent to Serasa with the returned transaction id.</summary>
    public void MarcarAguardandoRetorno(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new ArgumentException("Transaction id is required.", nameof(transactionId));
        }

        TransactionId = transactionId;
        Status = SerasaPefinStatus.AguardandoRetorno;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Marks the solicitation as finished with success based on a webhook payload.</summary>
    public void AplicarWebhookSucesso(string webhookPayload, string? cadusKey, string? cadusSerie)
    {
        Status = TipoRegistro == SerasaPefinRecordType.Principal || Status == SerasaPefinStatus.AguardandoRetorno
            ? SerasaPefinStatus.NegativadoSucesso
            : SerasaPefinStatus.BaixadoSucesso;

        WebhookPayload = webhookPayload;
        CadusKey = cadusKey;
        CadusSerie = cadusSerie;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Marks the solicitation as finished with error based on a webhook payload.</summary>
    public void AplicarWebhookErro(string webhookPayload, string errorMessage, int? errorStatusCode)
    {
        Status = SerasaPefinStatus.NegativadoErro;
        WebhookPayload = webhookPayload;
        ErrorMessage = errorMessage;
        ErrorStatusCode = errorStatusCode;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Marks the solicitation as failed due to an HTTP/auth error before Serasa returned a transaction id.</summary>
    public void MarcarFalhaEnvio(string errorMessage, int? errorStatusCode)
    {
        Status = SerasaPefinStatus.NegativadoErro;
        ErrorMessage = errorMessage;
        ErrorStatusCode = errorStatusCode;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Marks that the removal request (baixa) was dispatched.</summary>
    public void MarcarBaixaAguardandoRetorno(string transactionId)
    {
        TransactionId = transactionId;
        Status = SerasaPefinStatus.BaixaAguardandoRetorno;
        DtAtualizacao = DateTime.UtcNow;
    }
}

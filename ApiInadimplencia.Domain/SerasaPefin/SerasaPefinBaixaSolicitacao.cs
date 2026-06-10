namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Aggregate root representando uma solicitação de baixa (write-off) de dívida no Serasa PEFIN
/// (linha em <c>dbo.SERASA_PEFIN_BAIXAS</c>). Encapsula as transições de ciclo de vida e as
/// invariantes do fluxo de aprovação + envio + webhook + reenvio (limite de 3 tentativas).
/// </summary>
public sealed class SerasaPefinBaixaSolicitacao
{
    /// <summary>Limite máximo de tentativas (incluindo a primeira) por solicitação.</summary>
    public const byte LimiteTentativas = 3;

    /// <summary>Identificador único da solicitação de baixa.</summary>
    public Guid Id { get; private set; }

    /// <summary>FK para a solicitação de negativação que originou a baixa (<c>SERASA_PEFIN_SOLICITACOES.ID</c>).</summary>
    public Guid IdSolicitacaoNegativacao { get; private set; }

    /// <summary>Número da venda (FK).</summary>
    public int NumVendaFk { get; private set; }

    /// <summary>Número da parcela; null para solicitações legadas sem parcelamento.</summary>
    public int? NumeroParcela { get; private set; }

    /// <summary>Número do contrato enviado ao Serasa.</summary>
    public string ContractNumber { get; private set; } = string.Empty;

    /// <summary>CPF/CNPJ do devedor (somente dígitos).</summary>
    public string DocumentoDevedor { get; private set; } = string.Empty;

    /// <summary>CNPJ do credor (somente dígitos).</summary>
    public string DocumentoCredor { get; private set; } = string.Empty;

    /// <summary>Motivo da baixa (whitelist Serasa).</summary>
    public SerasaPefinBaixaMotivo Motivo { get; private set; } = default!;

    /// <summary>Status atual da solicitação.</summary>
    public SerasaPefinBaixaStatus Status { get; private set; }

    /// <summary>Username do solicitante (analista que abriu a solicitação).</summary>
    public string SolicitanteUsername { get; private set; } = string.Empty;

    /// <summary>Username do aprovador que decidiu (aprovou ou rejeitou).</summary>
    public string? AprovadorUsername { get; private set; }

    /// <summary>Data/hora UTC da decisão (aprovação ou rejeição).</summary>
    public DateTime? DtAprovacao { get; private set; }

    /// <summary>Justificativa fornecida na rejeição.</summary>
    public string? Justificativa { get; private set; }

    /// <summary>UUID da transação retornada pelo Serasa após o envio bem-sucedido.</summary>
    public string? TransactionId { get; private set; }

    /// <summary>Payload bruto do webhook recebido da Serasa.</summary>
    public string? WebhookPayload { get; private set; }

    /// <summary>Mensagem de erro capturada (em falha de envio HTTP ou em webhook de erro).</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Status HTTP/Serasa do erro, quando disponível.</summary>
    public int? ErrorStatusCode { get; private set; }

    /// <summary>Contador de tentativas (1..3).</summary>
    public byte Tentativas { get; private set; }

    /// <summary>Timestamp UTC de criação.</summary>
    public DateTime DtCriacao { get; private set; }

    /// <summary>Timestamp UTC da última atualização.</summary>
    public DateTime DtAtualizacao { get; private set; }

    private SerasaPefinBaixaSolicitacao()
    {
    }

    /// <summary>
    /// Factory para criação de uma nova solicitação de baixa, em estado
    /// <see cref="SerasaPefinBaixaStatus.AguardandoAprovacao"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Quando algum campo obrigatório é inválido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Quando algum valor numérico é inválido.</exception>
    public static SerasaPefinBaixaSolicitacao CriarParaAprovacao(
        Guid idSolicitacaoNegativacao,
        int numVendaFk,
        int? numeroParcela,
        string contractNumber,
        string documentoDevedor,
        string documentoCredor,
        SerasaPefinBaixaMotivo motivo,
        string solicitanteUsername)
    {
        if (idSolicitacaoNegativacao == Guid.Empty)
        {
            throw new ArgumentException("ID_SOLICITACAO_NEGATIVACAO is required.", nameof(idSolicitacaoNegativacao));
        }

        if (numVendaFk <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numVendaFk), "NUM_VENDA_FK must be positive.");
        }

        if (numeroParcela.HasValue && numeroParcela.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numeroParcela), "NUMERO_PARCELA must be positive when provided.");
        }

        if (string.IsNullOrWhiteSpace(contractNumber))
        {
            throw new ArgumentException("CONTRACT_NUMBER is required.", nameof(contractNumber));
        }

        if (string.IsNullOrWhiteSpace(documentoDevedor))
        {
            throw new ArgumentException("DOCUMENTO_DEVEDOR is required.", nameof(documentoDevedor));
        }

        if (string.IsNullOrWhiteSpace(documentoCredor))
        {
            throw new ArgumentException("DOCUMENTO_CREDOR is required.", nameof(documentoCredor));
        }

        if (motivo is null)
        {
            throw new ArgumentNullException(nameof(motivo), "MOTIVO is required.");
        }

        if (string.IsNullOrWhiteSpace(solicitanteUsername))
        {
            throw new ArgumentException("SOLICITANTE_USERNAME is required.", nameof(solicitanteUsername));
        }

        var now = DateTime.UtcNow;

        return new SerasaPefinBaixaSolicitacao
        {
            Id = Guid.NewGuid(),
            IdSolicitacaoNegativacao = idSolicitacaoNegativacao,
            NumVendaFk = numVendaFk,
            NumeroParcela = numeroParcela,
            ContractNumber = contractNumber.Trim(),
            DocumentoDevedor = SerasaPefinConstants.DigitsOnly(documentoDevedor),
            DocumentoCredor = SerasaPefinConstants.DigitsOnly(documentoCredor),
            Motivo = motivo,
            Status = SerasaPefinBaixaStatus.AguardandoAprovacao,
            SolicitanteUsername = solicitanteUsername.Trim(),
            Tentativas = 1,
            DtCriacao = now,
            DtAtualizacao = now,
        };
    }

    /// <summary>
    /// Factory para a integração TOTVS RM (Fórmula Visual): cria uma baixa já
    /// CONCLUÍDA (<see cref="SerasaPefinBaixaStatus.BaixadoSucesso"/>) após o DELETE
    /// ter sido aceito pela Serasa, sem passar pelo fluxo de aprovação/webhook.
    /// Usada apenas no modo RM, onde não há aprovador humano nem retorno via webhook.
    /// </summary>
    /// <param name="idSolicitacaoNegativacao">FK obrigatória para a negativação de origem.</param>
    /// <param name="transactionId">UUID retornado pela Serasa (pode ser null/vazio se a Serasa não retornar).</param>
    /// <exception cref="ArgumentException">Quando algum campo obrigatório é inválido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Quando algum valor numérico é inválido.</exception>
    public static SerasaPefinBaixaSolicitacao CriarRmConcluida(
        Guid idSolicitacaoNegativacao,
        int numVendaFk,
        int? numeroParcela,
        string contractNumber,
        string documentoDevedor,
        string documentoCredor,
        SerasaPefinBaixaMotivo motivo,
        string? transactionId,
        string solicitanteUsername = "TOTVS_RM")
    {
        if (idSolicitacaoNegativacao == Guid.Empty)
        {
            throw new ArgumentException("ID_SOLICITACAO_NEGATIVACAO is required.", nameof(idSolicitacaoNegativacao));
        }

        if (numVendaFk <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numVendaFk), "NUM_VENDA_FK must be positive.");
        }

        if (numeroParcela.HasValue && numeroParcela.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numeroParcela), "NUMERO_PARCELA must be positive when provided.");
        }

        if (string.IsNullOrWhiteSpace(contractNumber))
        {
            throw new ArgumentException("CONTRACT_NUMBER is required.", nameof(contractNumber));
        }

        if (string.IsNullOrWhiteSpace(documentoDevedor))
        {
            throw new ArgumentException("DOCUMENTO_DEVEDOR is required.", nameof(documentoDevedor));
        }

        if (string.IsNullOrWhiteSpace(documentoCredor))
        {
            throw new ArgumentException("DOCUMENTO_CREDOR is required.", nameof(documentoCredor));
        }

        if (motivo is null)
        {
            throw new ArgumentNullException(nameof(motivo), "MOTIVO is required.");
        }

        if (string.IsNullOrWhiteSpace(solicitanteUsername))
        {
            throw new ArgumentException("SOLICITANTE_USERNAME is required.", nameof(solicitanteUsername));
        }

        var now = DateTime.UtcNow;

        return new SerasaPefinBaixaSolicitacao
        {
            Id = Guid.NewGuid(),
            IdSolicitacaoNegativacao = idSolicitacaoNegativacao,
            NumVendaFk = numVendaFk,
            NumeroParcela = numeroParcela,
            ContractNumber = contractNumber.Trim(),
            DocumentoDevedor = SerasaPefinConstants.DigitsOnly(documentoDevedor),
            DocumentoCredor = SerasaPefinConstants.DigitsOnly(documentoCredor),
            Motivo = motivo,
            Status = SerasaPefinBaixaStatus.BaixadoSucesso,
            SolicitanteUsername = solicitanteUsername.Trim(),
            AprovadorUsername = solicitanteUsername.Trim(),
            DtAprovacao = now,
            TransactionId = string.IsNullOrWhiteSpace(transactionId) ? null : transactionId.Trim(),
            Tentativas = 1,
            DtCriacao = now,
            DtAtualizacao = now,
        };
    }

    /// <summary>
    /// Reidrata uma instância a partir da persistência, sem validar invariantes de criação.
    /// </summary>
    public static SerasaPefinBaixaSolicitacao Hydrate(
        Guid id,
        Guid idSolicitacaoNegativacao,
        int numVendaFk,
        int? numeroParcela,
        string contractNumber,
        string documentoDevedor,
        string documentoCredor,
        SerasaPefinBaixaMotivo motivo,
        SerasaPefinBaixaStatus status,
        string solicitanteUsername,
        string? aprovadorUsername,
        DateTime? dtAprovacao,
        string? justificativa,
        string? transactionId,
        string? webhookPayload,
        string? errorMessage,
        int? errorStatusCode,
        byte tentativas,
        DateTime dtCriacao,
        DateTime dtAtualizacao) => new()
        {
            Id = id,
            IdSolicitacaoNegativacao = idSolicitacaoNegativacao,
            NumVendaFk = numVendaFk,
            NumeroParcela = numeroParcela,
            ContractNumber = contractNumber,
            DocumentoDevedor = documentoDevedor,
            DocumentoCredor = documentoCredor,
            Motivo = motivo,
            Status = status,
            SolicitanteUsername = solicitanteUsername,
            AprovadorUsername = aprovadorUsername,
            DtAprovacao = dtAprovacao,
            Justificativa = justificativa,
            TransactionId = transactionId,
            WebhookPayload = webhookPayload,
            ErrorMessage = errorMessage,
            ErrorStatusCode = errorStatusCode,
            Tentativas = tentativas,
            DtCriacao = dtCriacao,
            DtAtualizacao = dtAtualizacao,
        };

    /// <summary>Aprovação por um aprovador autorizado.</summary>
    /// <exception cref="InvalidOperationException">Se o estado atual não é <see cref="SerasaPefinBaixaStatus.AguardandoAprovacao"/>.</exception>
    public void MarcarAprovada(string aprovadorUsername, DateTime utcNow)
    {
        if (Status != SerasaPefinBaixaStatus.AguardandoAprovacao)
        {
            throw new InvalidOperationException(
                $"Cannot mark as APROVADA: current status is {Status}. Only AGUARDANDO_APROVACAO can transition to APROVADA.");
        }

        if (string.IsNullOrWhiteSpace(aprovadorUsername))
        {
            throw new ArgumentException("APROVADOR_USERNAME is required.", nameof(aprovadorUsername));
        }

        AprovadorUsername = aprovadorUsername.Trim();
        DtAprovacao = utcNow;
        Status = SerasaPefinBaixaStatus.Aprovada;
        DtAtualizacao = utcNow;
    }

    /// <summary>Rejeição por um aprovador, com justificativa obrigatória.</summary>
    public void MarcarRejeitada(string aprovadorUsername, string justificativa, DateTime utcNow)
    {
        if (Status != SerasaPefinBaixaStatus.AguardandoAprovacao)
        {
            throw new InvalidOperationException(
                $"Cannot mark as REJEITADA: current status is {Status}. Only AGUARDANDO_APROVACAO can transition to REJEITADA.");
        }

        if (string.IsNullOrWhiteSpace(aprovadorUsername))
        {
            throw new ArgumentException("APROVADOR_USERNAME is required.", nameof(aprovadorUsername));
        }

        if (string.IsNullOrWhiteSpace(justificativa))
        {
            throw new ArgumentException("JUSTIFICATIVA is required when rejecting.", nameof(justificativa));
        }

        AprovadorUsername = aprovadorUsername.Trim();
        Justificativa = justificativa.Trim();
        DtAprovacao = utcNow;
        Status = SerasaPefinBaixaStatus.Rejeitada;
        DtAtualizacao = utcNow;
    }

    /// <summary>Transição <see cref="SerasaPefinBaixaStatus.Aprovada"/> → <see cref="SerasaPefinBaixaStatus.PendenteEnvio"/>.</summary>
    public void MarcarPendenteEnvio()
    {
        if (Status != SerasaPefinBaixaStatus.Aprovada)
        {
            throw new InvalidOperationException(
                $"Cannot mark as PENDENTE_ENVIO: current status is {Status}. Only APROVADA can transition to PENDENTE_ENVIO.");
        }

        Status = SerasaPefinBaixaStatus.PendenteEnvio;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Marca como aguardando o webhook após o DELETE bem-sucedido na Serasa.</summary>
    public void MarcarBaixaAguardandoRetorno(string transactionId)
    {
        if (Status != SerasaPefinBaixaStatus.PendenteEnvio)
        {
            throw new InvalidOperationException(
                $"Cannot mark as BAIXA_AGUARDANDO_RETORNO: current status is {Status}. Only PENDENTE_ENVIO can transition to BAIXA_AGUARDANDO_RETORNO.");
        }

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new ArgumentException("TRANSACTION_ID is required.", nameof(transactionId));
        }

        TransactionId = transactionId.Trim();
        Status = SerasaPefinBaixaStatus.BaixaAguardandoRetorno;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Aplica webhook de sucesso, transicionando para <see cref="SerasaPefinBaixaStatus.BaixadoSucesso"/>.</summary>
    public void AplicarWebhookSucesso(string webhookPayload)
    {
        if (Status != SerasaPefinBaixaStatus.BaixaAguardandoRetorno)
        {
            throw new InvalidOperationException(
                $"Cannot apply webhook SUCCESS: current status is {Status}. Only BAIXA_AGUARDANDO_RETORNO can transition to BAIXADO_SUCESSO.");
        }

        WebhookPayload = webhookPayload;
        Status = SerasaPefinBaixaStatus.BaixadoSucesso;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>Aplica webhook de erro, transicionando para <see cref="SerasaPefinBaixaStatus.BaixadoErro"/>.</summary>
    public void AplicarWebhookErro(string webhookPayload, string errorMessage, int? errorStatusCode)
    {
        if (Status != SerasaPefinBaixaStatus.BaixaAguardandoRetorno)
        {
            throw new InvalidOperationException(
                $"Cannot apply webhook ERROR: current status is {Status}. Only BAIXA_AGUARDANDO_RETORNO can transition to BAIXADO_ERRO.");
        }

        WebhookPayload = webhookPayload;
        ErrorMessage = errorMessage;
        ErrorStatusCode = errorStatusCode;
        Status = SerasaPefinBaixaStatus.BaixadoErro;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>
    /// Marca falha de envio HTTP ocorrida ANTES de obter o transactionId
    /// (ex.: 401, 5xx, timeout). Estado terminal "AprovadaFalhaEnvio" para retentativa manual.
    /// </summary>
    public void MarcarFalhaEnvio(string errorMessage, int? errorStatusCode)
    {
        if (Status is not (SerasaPefinBaixaStatus.Aprovada or SerasaPefinBaixaStatus.PendenteEnvio))
        {
            throw new InvalidOperationException(
                $"Cannot mark as APROVADA_FALHA_ENVIO: current status is {Status}. Only APROVADA or PENDENTE_ENVIO can transition to APROVADA_FALHA_ENVIO.");
        }

        ErrorMessage = errorMessage;
        ErrorStatusCode = errorStatusCode;
        Status = SerasaPefinBaixaStatus.AprovadaFalhaEnvio;
        DtAtualizacao = DateTime.UtcNow;
    }

    /// <summary>
    /// Registra uma nova tentativa de envio (reenvio após erro).
    /// Apenas válida em <see cref="SerasaPefinBaixaStatus.BaixadoErro"/> e enquanto
    /// <see cref="Tentativas"/> &lt; <see cref="LimiteTentativas"/>.
    /// Limpa erros e transactionId anteriores para a nova tentativa.
    /// </summary>
    public void RegistrarTentativaReenvio()
    {
        if (Status != SerasaPefinBaixaStatus.BaixadoErro)
        {
            throw new InvalidOperationException(
                $"Cannot register retry: current status is {Status}. Only BAIXADO_ERRO can be re-sent.");
        }

        if (Tentativas >= LimiteTentativas)
        {
            throw new InvalidOperationException(
                $"Cannot register retry: limite de {LimiteTentativas} tentativas atingido (atual: {Tentativas}).");
        }

        Tentativas = (byte)(Tentativas + 1);
        ErrorMessage = null;
        ErrorStatusCode = null;
        TransactionId = null;
        WebhookPayload = null;
        Status = SerasaPefinBaixaStatus.PendenteEnvio;
        DtAtualizacao = DateTime.UtcNow;
    }
}

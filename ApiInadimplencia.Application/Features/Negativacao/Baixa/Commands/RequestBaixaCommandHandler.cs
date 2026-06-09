using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Handler para <see cref="RequestBaixaCommand"/>. Orquestra a criação de N
/// <c>SerasaPefinBaixaSolicitacao</c> (uma por parcela) em status
/// <see cref="SerasaPefinBaixaStatus.AguardandoAprovacao"/>, registra ocorrência
/// e dispara notificações para os aprovadores configurados. Sem efeito colateral
/// se qualquer validação falhar (senha, motivo, elegibilidade, duplicidade).
/// </summary>
public sealed class RequestBaixaCommandHandler : ICommandHandler<RequestBaixaCommand, Guid>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISenhaTransacaoValidator _senhaValidator;
    private readonly IInadimplenciaQueryService _queryService;
    private readonly ISerasaPefinRepository _serasaRepository;
    private readonly ISerasaPefinBaixaRepository _baixaRepository;
    private readonly IOcorrenciaRepository _ocorrenciaRepository;
    private readonly IProtocoloGenerator _protocoloGenerator;
    private readonly IAprovadoresPolicy _aprovadoresPolicy;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ISerasaPefinGateway _serasaGateway;
    private readonly SerasaPefinOptions _serasaOptions;
    private readonly ILogger<RequestBaixaCommandHandler> _logger;

    public RequestBaixaCommandHandler(
        ICurrentUserService currentUserService,
        ISenhaTransacaoValidator senhaValidator,
        IInadimplenciaQueryService queryService,
        ISerasaPefinRepository serasaRepository,
        ISerasaPefinBaixaRepository baixaRepository,
        IOcorrenciaRepository ocorrenciaRepository,
        IProtocoloGenerator protocoloGenerator,
        IAprovadoresPolicy aprovadoresPolicy,
        INotificationDispatcher notificationDispatcher,
        ISerasaPefinGateway serasaGateway,
        IOptions<SerasaPefinOptions> serasaOptions,
        ILogger<RequestBaixaCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _senhaValidator = senhaValidator;
        _queryService = queryService;
        _serasaRepository = serasaRepository;
        _baixaRepository = baixaRepository;
        _ocorrenciaRepository = ocorrenciaRepository;
        _protocoloGenerator = protocoloGenerator;
        _aprovadoresPolicy = aprovadoresPolicy;
        _notificationDispatcher = notificationDispatcher;
        _serasaGateway = serasaGateway;
        _serasaOptions = serasaOptions?.Value ?? throw new ArgumentNullException(nameof(serasaOptions));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> HandleAsync(RequestBaixaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Modo RM: integração TOTVS RM Fórmula Visual. Fire-and-forget direto ao
        // Serasa via DELETE por contract-number; sem persistência nem auth.
        // A rastreabilidade fica a cargo do próprio RM (transactionId no retorno).
        if (command.Rm)
        {
            return await HandleRmAsync(command, cancellationToken).ConfigureAwait(false);
        }

        // Modo padrão: exige usuário autenticado.
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.Username))
        {
            throw new UnauthorizedAccessException("User must be authenticated to request baixa.");
        }

        var username = _currentUserService.Username!;

        // 1. Senha de transação (apenas no modo padrão)
        var senhaResult = await _senhaValidator.ValidateAsync(username, command.SenhaTransacao, cancellationToken);
        switch (senhaResult)
        {
            case SenhaTransacaoValidationResult.Invalid:
                throw new UnauthorizedAccessException("SENHA_INVALIDA: Transaction password is incorrect.");
            case SenhaTransacaoValidationResult.LockedOut:
                throw new UnauthorizedAccessException("SENHA_BLOQUEADA: Account is locked due to too many failed attempts.");
            case SenhaTransacaoValidationResult.NotSet:
                throw new UnauthorizedAccessException("SENHA_NAO_CADASTRADA: Transaction password not set for this user.");
        }

        // 2. Motivo (whitelist Serasa)
        var motivo = SerasaPefinBaixaMotivo.From(command.MotivoBaixa);

        // 3. Parcelas selecionadas precisam existir e estar em NEGATIVADO_SUCESSO
        if (command.ParcelaIds is null || command.ParcelaIds.Count == 0)
        {
            throw new ArgumentException("Pelo menos uma parcela deve ser selecionada.", nameof(command));
        }

        var solicitacoesVenda = await _serasaRepository.ListByNumVendaAsync(command.NumVenda, cancellationToken);
        var childrenByParcela = solicitacoesVenda
            .Where(s => s.NumeroParcela.HasValue)
            .GroupBy(s => s.NumeroParcela!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DtCriacao).First());

        var resolvidas = new List<SerasaPefinSolicitacaoCompleta>(command.ParcelaIds.Count);
        foreach (var parcelaId in command.ParcelaIds)
        {
            if (!childrenByParcela.TryGetValue(parcelaId, out var child))
            {
                throw new ArgumentException(
                    $"NAO_ELEGIVEL: Parcela {parcelaId} nao encontrada para venda {command.NumVenda}.",
                    nameof(command));
            }

            if (child.Status != SerasaPefinStatus.NegativadoSucesso)
            {
                throw new ArgumentException(
                    $"NAO_ELEGIVEL: Parcela {parcelaId} nao esta em NEGATIVADO_SUCESSO (status atual: {child.Status}).",
                    nameof(command));
            }

            resolvidas.Add(child);
        }

        // 4. Duplicidade ativa por parcela (consulta antes de qualquer escrita)
        foreach (var child in resolvidas)
        {
            var existsActive = await _baixaRepository.ExistsActiveAsync(
                child.NumVendaFk,
                child.ContractNumber,
                child.NumeroParcela,
                cancellationToken);

            if (existsActive)
            {
                throw new InvalidOperationException(
                    $"JA_EM_APROVACAO: Ja existe baixa ativa para parcela {child.NumeroParcela} da venda {child.NumVendaFk}.");
            }
        }

        // 5. Buscar dados de venda (para mensagens de ocorrência/notificação)
        var venda = await _queryService.GetVendaAsync(command.NumVenda, cancellationToken);

        // 6. Criar agregados em AGUARDANDO_APROVACAO
        var baixas = resolvidas
            .Select(child => SerasaPefinBaixaSolicitacao.CriarParaAprovacao(
                idSolicitacaoNegativacao: child.Id,
                numVendaFk: child.NumVendaFk,
                numeroParcela: child.NumeroParcela,
                contractNumber: child.ContractNumber,
                documentoDevedor: child.DocumentoDevedor,
                documentoCredor: child.DocumentoCredor,
                motivo: motivo,
                solicitanteUsername: username))
            .ToList();

        await _baixaRepository.AddManyAsync(baixas, cancellationToken);

        // Pega a primeira (parcela mais baixa) como retorno canônico para o frontend.
        var primeiraBaixa = baixas
            .OrderBy(b => b.NumeroParcela ?? int.MaxValue)
            .First();

        // 7. Ocorrência
        var clienteNome = venda?.NomeDevedor ?? "Cliente";
        var mensagemOcorrencia = BaixaOcorrenciaScripts.MontarMensagemSolicitacao(
            usuario: username,
            cliente: clienteNome,
            numVenda: command.NumVenda,
            parcelas: command.ParcelaIds,
            motivo: motivo,
            justificativa: command.Justificativa);

        var protocolo = await _protocoloGenerator.GerarProtocoloAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var ocorrencia = Ocorrencia.Criar(
            numVendaFk: command.NumVenda,
            nomeUsuarioFk: username,
            descricao: mensagemOcorrencia,
            statusOcorrencia: "Solicitação de baixa",
            dtOcorrencia: now,
            horaOcorrencia: now.ToString("HH:mm"),
            protocolo: protocolo);

        await _ocorrenciaRepository.AddAsync(ocorrencia, cancellationToken);

        // 8. Notificação para aprovadores
        var aprovadores = _aprovadoresPolicy.ListAprovadores();
        _logger.LogInformation(
            "Baixa solicitada {BaixaId} venda {NumVenda} parcelas [{Parcelas}] motivo {Motivo}. Notificando {Count} aprovador(es).",
            primeiraBaixa.Id,
            command.NumVenda,
            string.Join(",", command.ParcelaIds),
            motivo.Codigo,
            aprovadores.Count);

        if (aprovadores.Count > 0)
        {
            var valorTotal = resolvidas.Sum(c => c.Valor);
            var mensagemTexto =
                $"Nova solicitacao de baixa para venda {command.NumVenda} ({clienteNome}). " +
                $"Motivo: {motivo.Descricao}. Aguardando sua aprovacao.";

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                mensagem = mensagemTexto,
                cliente = clienteNome,
                cpfCnpj = venda?.DocumentoDevedor ?? resolvidas[0].DocumentoDevedor,
                empreendimento = venda?.Empreendimento,
                bloco = venda?.Bloco,
                unidade = venda?.Unidade,
                valorInadimplente = valorTotal,
                solicitanteUsername = username,
                solicitacaoId = primeiraBaixa.Id,
                baixaIds = baixas.Select(b => b.Id).ToArray(),
                parcelas = command.ParcelaIds,
                motivoBaixa = motivo.Codigo,
                motivoBaixaDescricao = motivo.Descricao,
                protocolo,
                status = SerasaPefinBaixaStatus.AguardandoAprovacao.ToDbValue(),
            });

            await _notificationDispatcher.DispatchManyAsync(
                tipo: NotificationType.SolicitacaoBaixa,
                usernames: aprovadores,
                numVenda: command.NumVenda,
                mensagem: payload,
                dedupeKey: primeiraBaixa.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        return primeiraBaixa.Id;
    }

    /// <summary>
    /// Fluxo da integração TOTVS RM (Fórmula Visual). Recebe o
    /// <c>NumeroDocumento</c> e envia DELETE direto à Serasa por contract-number,
    /// resolvendo o CPF do devedor a partir do <c>NumVenda</c> via
    /// <c>fat_analise_inadimplencia_v4</c>. Sem persistência local — o RM
    /// é responsável pela rastreabilidade usando o <c>transactionId</c> retornado.
    /// </summary>
    private async Task<Guid> HandleRmAsync(RequestBaixaCommand command, CancellationToken cancellationToken)
    {
        // 1. Validações de entrada específicas do modo RM.
        if (string.IsNullOrWhiteSpace(command.NumeroDocumento))
        {
            throw new ArgumentException(
                "NUMERO_DOCUMENTO_OBRIGATORIO: campo 'numeroDocumento' é obrigatório quando rm=true.",
                nameof(command));
        }

        if (command.NumVenda <= 0)
        {
            throw new ArgumentException(
                "NUM_VENDA_OBRIGATORIO: campo 'numVenda' é obrigatório quando rm=true.",
                nameof(command));
        }

        // 2. Motivo (whitelist Serasa: 1, 2, 3, 4, 19, 43, 45).
        var motivo = SerasaPefinBaixaMotivo.From(command.MotivoBaixa);

        // 3. Resolve CPF/CNPJ do devedor pela venda (fat_analise_inadimplencia_v4).
        var venda = await _queryService.GetVendaAsync(command.NumVenda, cancellationToken).ConfigureAwait(false);
        if (venda is null || string.IsNullOrWhiteSpace(venda.DocumentoDevedor))
        {
            throw new ArgumentException(
                $"VENDA_NAO_ENCONTRADA: não foi possível resolver o devedor para a venda {command.NumVenda}.",
                nameof(command));
        }

        // 4. Credor: fixo via configuração SerasaPefinOptions.CreditorDocument.
        if (string.IsNullOrWhiteSpace(_serasaOptions.CreditorDocument))
        {
            throw new InvalidOperationException(
                "SERASA_CREDITOR_DOCUMENT_NAO_CONFIGURADO: defina SerasaPefin:CreditorDocument no appsettings.");
        }

        var contractNumber = command.NumeroDocumento!.Trim();
        var debtorDoc = SerasaPefinConstants.DigitsOnly(venda.DocumentoDevedor);
        var creditorDoc = SerasaPefinConstants.DigitsOnly(_serasaOptions.CreditorDocument);

        _logger.LogInformation(
            "Baixa RM: enviando DELETE para Serasa. Venda={NumVenda} Contract={Contract} Reason={Reason} Debtor=***{DebtorLast4}",
            command.NumVenda,
            contractNumber,
            motivo.Codigo,
            debtorDoc.Length >= 4 ? debtorDoc[^4..] : debtorDoc);

        // 5. Chamada direta ao gateway. Erros HTTP propagam SerasaPefinHttpException,
        // tratada pelo endpoint para retornar 502 ao cliente.
        var response = await _serasaGateway.DeleteByContractAsync(
            new SerasaBaixaRequest(
                CreditorDocument: creditorDoc,
                DebtorDocument: debtorDoc,
                ContractNumber: contractNumber,
                Reason: motivo.Codigo),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Baixa RM enviada. Venda={NumVenda} Contract={Contract} TransactionId={TransactionId}",
            command.NumVenda, contractNumber, response.TransactionId);

        // 6. Retorna o transactionId da Serasa como Guid (Serasa devolve UUID).
        // O endpoint mapeia esse valor como 'solicitacaoId' no JSON de resposta.
        return Guid.TryParse(response.TransactionId, out var txGuid) ? txGuid : Guid.Empty;
    }
}

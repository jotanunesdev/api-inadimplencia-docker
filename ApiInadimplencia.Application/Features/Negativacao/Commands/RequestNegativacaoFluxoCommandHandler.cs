using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.Negativacao.Commands;

/// <summary>
/// Handler for RequestNegativacaoFluxoCommand.
/// Orchestrates the creation of a negativacao solicitation in the approval workflow.
/// </summary>
public sealed class RequestNegativacaoFluxoCommandHandler : ICommandHandler<RequestNegativacaoFluxoCommand, Guid>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISenhaTransacaoValidator _senhaValidator;
    private readonly IInadimplenciaQueryService _queryService;
    private readonly ISerasaPefinRepository _serasaRepository;
    private readonly IOcorrenciaRepository _ocorrenciaRepository;
    private readonly IProtocoloGenerator _protocoloGenerator;
    private readonly IAprovadoresPolicy _aprovadoresPolicy;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<RequestNegativacaoFluxoCommandHandler> _logger;

    public RequestNegativacaoFluxoCommandHandler(
        ICurrentUserService currentUserService,
        ISenhaTransacaoValidator senhaValidator,
        IInadimplenciaQueryService queryService,
        ISerasaPefinRepository serasaRepository,
        IOcorrenciaRepository ocorrenciaRepository,
        IProtocoloGenerator protocoloGenerator,
        IAprovadoresPolicy aprovadoresPolicy,
        INotificationDispatcher notificationDispatcher,
        ILogger<RequestNegativacaoFluxoCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _senhaValidator = senhaValidator;
        _queryService = queryService;
        _serasaRepository = serasaRepository;
        _ocorrenciaRepository = ocorrenciaRepository;
        _protocoloGenerator = protocoloGenerator;
        _aprovadoresPolicy = aprovadoresPolicy;
        _notificationDispatcher = notificationDispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> HandleAsync(RequestNegativacaoFluxoCommand command, CancellationToken cancellationToken = default)
    {
        // Validate authentication
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.Username))
        {
            throw new UnauthorizedAccessException("User must be authenticated to request negativacao.");
        }

        var username = _currentUserService.Username;

        // 1. Validate transaction password BEFORE any writes
        var senhaValidation = await _senhaValidator.ValidateAsync(username, command.SenhaTransacao, cancellationToken);
        if (senhaValidation == SenhaTransacaoValidationResult.Invalid)
        {
            throw new UnauthorizedAccessException("SENHA_INVALIDA: Transaction password is incorrect.");
        }
        if (senhaValidation == SenhaTransacaoValidationResult.LockedOut)
        {
            throw new UnauthorizedAccessException("SENHA_BLOQUEADA: Account is locked due to too many failed attempts.");
        }
        if (senhaValidation == SenhaTransacaoValidationResult.NotSet)
        {
            throw new UnauthorizedAccessException("SENHA_NAO_CADASTRADA: Transaction password not set for this user.");
        }

        // 2. Re-validate server-side eligibility of selected parcels
        var dividasResult = await _queryService.GetDividasElegiveisAsync(command.NumVenda, 60, cancellationToken);
        if (dividasResult == null)
        {
            throw new ArgumentException($"NAO_ELEGIVEL: Sale {command.NumVenda} not found or has no eligible debts.");
        }

        // Check if all selected parcelas are eligible
        var selectedParcelas = dividasResult.Parcelas
            .Where(p => command.ParcelaIds.Contains(p.Id))
            .ToList();

        if (selectedParcelas.Count != command.ParcelaIds.Count)
        {
            throw new ArgumentException("NAO_ELEGIVEL: One or more selected parcelas do not exist for this sale.");
        }

        var parcelasElegiveisSelecionadas = selectedParcelas
            .Where(p => p.Elegivel)
            .ToList();

        if (parcelasElegiveisSelecionadas.Count == 0)
        {
            throw new ArgumentException("NAO_ELEGIVEL: None of the selected parcelas are eligible for negativacao (must have >60 days overdue).");
        }

        // 3. Check for duplicate active solicitation
        var existsActive = await _serasaRepository.ExistsActiveAsync(
            command.NumVenda,
            dividasResult.ContractNumber,
            dividasResult.Cpf,
            null, // documentoGarantidor (null for principal)
            SerasaPefinRecordType.Principal,
            cancellationToken);

        if (existsActive)
        {
            throw new InvalidOperationException($"JA_EM_APROVACAO: An active solicitation already exists for sale {command.NumVenda}.");
        }

        // 4. Get venda data for occurrence message
        var vendaResult = await _queryService.GetVendaAsync(command.NumVenda, cancellationToken);
        if (vendaResult == null)
        {
            throw new ArgumentException($"Sale {command.NumVenda} not found.");
        }

        // 5. Get fiadores if incluirFiadores is true
        IReadOnlyList<string> fiadoresNomes = Array.Empty<string>();
        if (command.IncluirFiadores)
        {
            var fiadores = await _queryService.ListFiadoresAsync(command.NumVenda, cancellationToken);
            if (fiadores != null && fiadores.Count > 0)
            {
                fiadoresNomes = fiadores.Select(f => f.Nome).ToList();
            }
        }

        // 6. Create SerasaPefinSolicitacaoCompleta in AGUARDANDO_APROVACAO status
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk: command.NumVenda,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: vendaResult.DocumentoDevedor,
            documentoCredor: dividasResult.ContractNumber, // Using contract number as creditor document placeholder
            contractNumber: dividasResult.ContractNumber,
            areaInformante: "0001", // TODO: Get from configuration
            valor: parcelasElegiveisSelecionadas.Sum(p => p.Valor),
            dataVencimento: parcelasElegiveisSelecionadas.Max(p => p.Vencimento),
            solicitanteUsername: username);

        var solicitacoesParaPersistir = new List<SerasaPefinSolicitacaoCompleta> { solicitacao };

        solicitacoesParaPersistir.AddRange(parcelasElegiveisSelecionadas.Select(parcela =>
            SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
                numVendaFk: command.NumVenda,
                tipoRegistro: SerasaPefinRecordType.Principal,
                documentoDevedor: vendaResult.DocumentoDevedor,
                documentoCredor: dividasResult.ContractNumber,
                contractNumber: dividasResult.ContractNumber,
                areaInformante: "0001",
                valor: parcela.Valor,
                dataVencimento: parcela.Vencimento,
                solicitanteUsername: username,
                numeroParcela: parcela.Id,
                parcelaIdOrigem: parcela.Id.ToString(),
                idSolicitacaoPai: solicitacao.Id)));

        await _serasaRepository.AddManyAsync(solicitacoesParaPersistir, cancellationToken);
        var solicitacaoId = solicitacao.Id;

        // 7. Create Ocorrencia with standardized message
        var endereco = vendaResult.Endereco != null
            ? $"{vendaResult.Endereco.AddressLine}, {vendaResult.Endereco.District}, {vendaResult.Endereco.City}-{vendaResult.Endereco.State}, CEP {vendaResult.Endereco.ZipCode}"
            : "Endereço não informado";

        var mensagemOcorrencia = NegativacaoOcorrenciaScripts.MontarMensagemSolicitacao(
            usuario: username,
            cliente: vendaResult.NomeDevedor,
            numVenda: command.NumVenda,
            endereco: endereco,
            parcelas: command.ParcelaIds,
            fiadores: fiadoresNomes.Count > 0 ? fiadoresNomes : null);

        var protocolo = await _protocoloGenerator.GerarProtocoloAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var horaOcorrencia = now.ToString("HH:mm");

        var ocorrencia = Ocorrencia.Criar(
            numVendaFk: command.NumVenda,
            nomeUsuarioFk: username,
            descricao: mensagemOcorrencia,
            statusOcorrencia: "Solicitação de negativação",
            dtOcorrencia: now,
            horaOcorrencia: horaOcorrencia,
            protocolo: protocolo);

        await _ocorrenciaRepository.AddAsync(ocorrencia, cancellationToken);

        // 8. Dispatch notifications to all approvers
        var aprovadores = _aprovadoresPolicy.ListAprovadores();
        _logger.LogInformation(
            "Solicitacao {SolicitacaoId} created for venda {NumVenda}. Notifying {Count} aprovador(es): [{Aprovadores}]",
            solicitacaoId, command.NumVenda, aprovadores.Count, string.Join(",", aprovadores));

        if (aprovadores.Count > 0)
        {
            // Persist a JSON payload so the frontend (which reads PAYLOAD as JSON
            // via MapNotificationRow) can render cliente/cpf/empreendimento/valor
            // directly on the notification card.
            var valorTotal = parcelasElegiveisSelecionadas.Sum(p => p.Valor);
            var mensagemTexto = $"Nova solicitação de negativação para venda {command.NumVenda} ({vendaResult.NomeDevedor}). Aguardando sua aprovação.";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                mensagem = mensagemTexto,
                cliente = vendaResult.NomeDevedor,
                cpfCnpj = vendaResult.DocumentoDevedor,
                empreendimento = vendaResult.Empreendimento,
                bloco = vendaResult.Bloco,
                unidade = vendaResult.Unidade,
                valorInadimplente = valorTotal,
                solicitanteUsername = username,
                solicitacaoId,
                protocolo,
                status = "AGUARDANDO_APROVACAO",
            });

            await _notificationDispatcher.DispatchManyAsync(
                tipo: NotificationType.SolicitacaoNegativacao,
                usernames: aprovadores,
                numVenda: command.NumVenda,
                mensagem: payload,
                dedupeKey: solicitacaoId.ToString(),
                cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogWarning("No aprovadores configured. Notifications will not be dispatched for solicitacao {SolicitacaoId}.", solicitacaoId);
        }

        return solicitacaoId;
    }
}

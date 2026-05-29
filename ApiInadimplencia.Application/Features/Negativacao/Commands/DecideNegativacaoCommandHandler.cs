using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Application.Features.SerasaPefin.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Domain.Negativacao;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.Negativacao.Commands;

/// <summary>
/// Handler for DecideNegativacaoCommand implementing the approval/rejection workflow.
/// Validates approver permissions, transaction password, and transitions the solicitation status.
/// If approved, reuses RequestNegativacaoCommandHandler to send to Serasa.
/// </summary>
public class DecideNegativacaoCommandHandler : ICommandHandler<DecideNegativacaoCommand, bool>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAprovadoresPolicy _aprovadoresPolicy;
    private readonly ISenhaTransacaoValidator _senhaValidator;
    private readonly IInadimplenciaQueryService _queryService;
    private readonly ISerasaPefinRepository _serasaRepository;
    private readonly IOcorrenciaRepository _ocorrenciaRepository;
    private readonly IProtocoloGenerator _protocoloGenerator;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse> _requestNegativacaoHandler;
    private readonly ILogger<DecideNegativacaoCommandHandler> _logger;

    public DecideNegativacaoCommandHandler(
        ICurrentUserService currentUserService,
        IAprovadoresPolicy aprovadoresPolicy,
        ISenhaTransacaoValidator senhaValidator,
        IInadimplenciaQueryService queryService,
        ISerasaPefinRepository serasaRepository,
        IOcorrenciaRepository ocorrenciaRepository,
        IProtocoloGenerator protocoloGenerator,
        INotificationDispatcher notificationDispatcher,
        ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse> requestNegativacaoHandler,
        ILogger<DecideNegativacaoCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _aprovadoresPolicy = aprovadoresPolicy;
        _senhaValidator = senhaValidator;
        _queryService = queryService;
        _serasaRepository = serasaRepository;
        _ocorrenciaRepository = ocorrenciaRepository;
        _protocoloGenerator = protocoloGenerator;
        _notificationDispatcher = notificationDispatcher;
        _requestNegativacaoHandler = requestNegativacaoHandler;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DecideNegativacaoCommand command, CancellationToken cancellationToken = default)
    {
        var username = _currentUserService.Username;
        var solicitacaoId = command.SolicitacaoId;
        var decisao = command.Decisao;

        _logger.LogInformation("DecideNegativacao.Start - SolicitacaoId: {SolicitacaoId}, Decisao: {Decisao}, Aprovador: {Aprovador}",
            solicitacaoId, decisao, username);

        // 1. Validate approver
        if (!_aprovadoresPolicy.IsAprovador(username))
        {
            var aprovadoresConfigurados = _aprovadoresPolicy.ListAprovadores() ?? Array.Empty<string>();
            _logger.LogWarning(
                "DecideNegativacao.Unauthorized - SolicitacaoId: {SolicitacaoId}, Username: '{Username}', AprovadoresConfigurados: [{Aprovadores}]",
                solicitacaoId,
                username,
                string.Join(", ", aprovadoresConfigurados));
            throw new UnauthorizedAccessException("NAO_AUTORIZADO: Usuário não é aprovador autorizado.");
        }

        // 2. Validate transaction password
        var senhaResult = await _senhaValidator.ValidateAsync(username, command.SenhaTransacao, cancellationToken);
        if (senhaResult != SenhaTransacaoValidationResult.Valid)
        {
            var error = senhaResult switch
            {
                SenhaTransacaoValidationResult.Invalid => "SENHA_INVALIDA",
                SenhaTransacaoValidationResult.LockedOut => "SENHA_BLOQUEADA",
                SenhaTransacaoValidationResult.NotSet => "SENHA_NAO_CADASTRADA",
                _ => "ERRO_SENHA"
            };
            _logger.LogWarning("DecideNegativacao.SenhaInvalid - SolicitacaoId: {SolicitacaoId}, Username: {Username}, Error: {Error}",
                solicitacaoId, username, error);
            throw new UnauthorizedAccessException($"{error}: Senha de transação inválida.");
        }

        // 3. Load solicitation
        var solicitacao = await _serasaRepository.GetByIdAsync(solicitacaoId, cancellationToken);
        if (solicitacao is null)
        {
            _logger.LogWarning("DecideNegativacao.NotFound - SolicitacaoId: {SolicitacaoId}", solicitacaoId);
            throw new KeyNotFoundException("NAO_ENCONTRADA: Solicitação não encontrada.");
        }

        // 4. Validate status
        if (solicitacao.Status != SerasaPefinStatus.AguardandoAprovacao)
        {
            _logger.LogWarning("DecideNegativacao.AlreadyDecided - SolicitacaoId: {SolicitacaoId}, Status: {Status}",
                solicitacaoId, solicitacao.Status);
            throw new InvalidOperationException($"JA_DECIDIDA: Solicitação já está em status {solicitacao.Status}.");
        }

        // 5. Validate requester cannot approve their own request (bypass para super-decisores)
        if (solicitacao.SolicitanteUsername?.Equals(username, StringComparison.OrdinalIgnoreCase) == true)
        {
            if (_aprovadoresPolicy.IsSuperDecisor(username))
            {
                _logger.LogInformation(
                    "DecideNegativacao.SuperDecisorBypass - SolicitacaoId: {SolicitacaoId}, Username: {Username} aprovando/rejeitando a propria solicitacao.",
                    solicitacaoId,
                    username);
            }
            else
            {
                _logger.LogWarning("DecideNegativacao.RequesterCannotApprove - SolicitacaoId: {SolicitacaoId}, Username: {Username}",
                    solicitacaoId, username);
                throw new InvalidOperationException("SOLICITANTE_NAO_PODE_APROVAR: O solicitante não pode aprovar sua própria solicitação.");
            }
        }

        var filhas = await _serasaRepository.ListByIdSolicitacaoPaiAsync(solicitacao.Id, cancellationToken)
            ?? Array.Empty<SerasaPefinSolicitacaoCompleta>();

        _logger.LogInformation(
            "DecideNegativacao.ChildrenLoaded - SolicitacaoId: {SolicitacaoId}, Decisao: {Decisao}, FilhasAtualizadas: {FilhasAtualizadas}",
            solicitacaoId,
            decisao,
            filhas.Count);

        // 6. Execute decision based on type
        if (decisao == DecisaoNegativacao.APROVAR)
        {
            await HandleAprovacaoAsync(solicitacao, filhas, username, cancellationToken);
        }
        else if (decisao == DecisaoNegativacao.REJEITAR)
        {
            await HandleRejeicaoAsync(solicitacao, filhas, username, command.Justificativa, cancellationToken);
        }

        _logger.LogInformation("DecideNegativacao.Complete - SolicitacaoId: {SolicitacaoId}, Decisao: {Decisao}",
            solicitacaoId, decisao);

        return true;
    }

    private async Task HandleAprovacaoAsync(
        SerasaPefinSolicitacaoCompleta solicitacao,
        IReadOnlyList<SerasaPefinSolicitacaoCompleta> filhas,
        string aprovadorUsername,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        // Mark as approved
        solicitacao.MarcarAprovada(aprovadorUsername, utcNow);
        foreach (var filha in filhas)
        {
            filha.MarcarAprovada(aprovadorUsername, utcNow);
            filha.MarcarPreparadoParaEnvio(filha.PayloadAuditoria);
        }

        var solicitacoesAtualizadas = new List<SerasaPefinSolicitacaoCompleta>(filhas.Count + 1)
        {
            solicitacao
        };
        solicitacoesAtualizadas.AddRange(filhas);

        await _serasaRepository.UpdateManyAsync(solicitacoesAtualizadas, cancellationToken);

        _logger.LogInformation("DecideNegativacao.Aprovada - SolicitacaoId: {SolicitacaoId}, Aprovador: {Aprovador}",
            solicitacao.Id, aprovadorUsername);

        _logger.LogInformation(
            "DecideNegativacao.ChildrenUpdated - SolicitacaoId: {SolicitacaoId}, Decisao: {Decisao}, FilhasAtualizadas: {FilhasAtualizadas}",
            solicitacao.Id,
            DecisaoNegativacao.APROVAR,
            filhas.Count);

        await TryRegistrarOcorrenciaDecisaoAsync(
            solicitacao,
            filhas,
            DecisaoNegativacao.APROVAR,
            aprovadorUsername,
            null,
            cancellationToken);

        // Send to Serasa via RequestNegativacaoCommand (reuse mode)
        RequestNegativacaoResponse? response = null;
        try
        {
            var requestCommand = new RequestNegativacaoCommand(
                solicitacao.NumVendaFk,
                IncluirGarantidores: solicitacao.TipoRegistro == SerasaPefinRecordType.Principal,
                Operador: aprovadorUsername,
                SolicitacaoIdExistente: solicitacao.Id);

            response = await _requestNegativacaoHandler.HandleAsync(requestCommand, cancellationToken);

            // Aggregate results to determine parent status
            var aggregatedStatus = CalculateParentStatus(response.Solicitacoes);
            
            // Update parent solicitation status based on aggregation
            if (aggregatedStatus == SerasaPefinStatus.AprovadaFalhaEnvio)
            {
                solicitacao.MarcarAprovadaFalhaEnvio("Algumas parcelas falharam ao enviar para Serasa", null);
            }
            // For AguardandoRetorno or mixed, keep as Aprovada (the aggregation is logical, not persisted on parent)

            // Update repository after send
            await _serasaRepository.UpdateAsync(solicitacao, cancellationToken);

            _logger.LogInformation("DecideNegativacao.EnviadoSerasa - SolicitacaoId: {SolicitacaoId}, StatusAgregado: {StatusAgregado}",
                solicitacao.Id, response.StatusAgregado);
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            || ex is ApiInadimplencia.Application.Features.SerasaPefin.Payloads.SerasaPefinValidationException
            || ex is InvalidOperationException
            || ex is TimeoutException)
        {
            // Handle synchronous Serasa failure (HTTP, validation/whitelist UAT, gateway timeout, etc.)
            // The parent solicitation transitions to AprovadaFalhaEnvio so the user can see the failure.
            var errorMessage = $"Falha ao enviar para Serasa: {ex.Message}";
            try
            {
                solicitacao.MarcarAprovadaFalhaEnvio(errorMessage, null);
                await _serasaRepository.UpdateAsync(solicitacao, cancellationToken);
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx,
                    "DecideNegativacao.AprovadaFalhaEnvio.PersistError - SolicitacaoId: {SolicitacaoId}",
                    solicitacao.Id);
            }

            // Notify both about the failure
            var failureMessage = $"Solicitação aprovada, mas houve falha ao enviar para Serasa. Erro: {ex.Message}";
            try
            {
                await NotifyBothAsync(solicitacao.SolicitanteUsername, aprovadorUsername, solicitacao.NumVendaFk, failureMessage, NotificationType.AprovacaoNegativacao, cancellationToken);
            }
            catch (Exception notifyEx)
            {
                _logger.LogWarning(notifyEx,
                    "DecideNegativacao.AprovadaFalhaEnvio.NotifyError - SolicitacaoId: {SolicitacaoId}",
                    solicitacao.Id);
            }

            _logger.LogError(ex,
                "DecideNegativacao.SerasaFailure - SolicitacaoId: {SolicitacaoId}, ExceptionType: {ExceptionType}",
                solicitacao.Id, ex.GetType().FullName);
            return;
        }

        // Build notification message with parcel count
        var parcelaIds = response?.Solicitacoes
            .Where(r => r.TipoRegistro == SerasaPefinRecordType.Principal && r.NumeroParcela.HasValue)
            .Select(r => (long)r.NumeroParcela!.Value)
            .Distinct()
            .OrderBy(p => p)
            .ToList() ?? new List<long> { 1 };
        var parcelasEnviadas = parcelaIds.Count;
        var notificationMessage = $"Solicitação de negativação aprovada para venda {solicitacao.NumVendaFk}. {parcelasEnviadas} de {parcelasEnviadas} parcelas enviadas ao Serasa.";
        
        // Adjust message for partial failures
        if (response != null && response.Solicitacoes.Any(r => r.Status == SerasaPefinStatus.NegativadoErro))
        {
            var parcelasComErro = response.Solicitacoes
                .Where(r => r.TipoRegistro == SerasaPefinRecordType.Principal && r.Status == SerasaPefinStatus.NegativadoErro)
                .Select(r => r.NumeroParcela)
                .Distinct()
                .Count();
            var parcelasSucesso = parcelasEnviadas - parcelasComErro;
            notificationMessage = $"Solicitação de negativação aprovada para venda {solicitacao.NumVendaFk}. {parcelasSucesso} de {parcelasEnviadas} parcelas enviadas ao Serasa ({parcelasComErro} com erro).";
        }

        await NotifyBothAsync(solicitacao.SolicitanteUsername, aprovadorUsername, solicitacao.NumVendaFk, notificationMessage, NotificationType.AprovacaoNegativacao, cancellationToken);
    }

    private static SerasaPefinStatus CalculateParentStatus(IReadOnlyList<SerasaSolicitacaoResult> solicitacoes)
    {
        if (solicitacoes.Count == 0)
            return SerasaPefinStatus.AprovadaFalhaEnvio;

        // Consider only Principal records for aggregation (not Guarantidores)
        var principals = solicitacoes.Where(r => r.TipoRegistro == SerasaPefinRecordType.Principal).ToList();
        
        if (principals.Count == 0)
            return SerasaPefinStatus.AprovadaFalhaEnvio;

        var allSuccess = principals.All(r => r.Status == SerasaPefinStatus.AguardandoRetorno);
        var anyError = principals.Any(r => r.Status == SerasaPefinStatus.NegativadoErro);

        if (allSuccess)
            return SerasaPefinStatus.Aprovada; // All sent successfully
        if (anyError)
            return SerasaPefinStatus.AprovadaFalhaEnvio; // At least one failed

        return SerasaPefinStatus.Aprovada;
    }

    private async Task HandleRejeicaoAsync(
        SerasaPefinSolicitacaoCompleta solicitacao,
        IReadOnlyList<SerasaPefinSolicitacaoCompleta> filhas,
        string aprovadorUsername,
        string? justificativa,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var justificativaFinal = justificativa ?? string.Empty;

        // Mark as rejected
        solicitacao.MarcarRejeitada(aprovadorUsername, justificativaFinal, utcNow);
        foreach (var filha in filhas)
        {
            filha.MarcarRejeitada(aprovadorUsername, justificativaFinal, utcNow);
        }

        var solicitacoesAtualizadas = new List<SerasaPefinSolicitacaoCompleta>(filhas.Count + 1)
        {
            solicitacao
        };
        solicitacoesAtualizadas.AddRange(filhas);

        await _serasaRepository.UpdateManyAsync(solicitacoesAtualizadas, cancellationToken);

        _logger.LogInformation("DecideNegativacao.Rejeitada - SolicitacaoId: {SolicitacaoId}, Aprovador: {Aprovador}, Justificativa: {Justificativa}",
            solicitacao.Id, aprovadorUsername, justificativa);

        _logger.LogInformation(
            "DecideNegativacao.ChildrenUpdated - SolicitacaoId: {SolicitacaoId}, Decisao: {Decisao}, FilhasAtualizadas: {FilhasAtualizadas}",
            solicitacao.Id,
            DecisaoNegativacao.REJEITAR,
            filhas.Count);

        await TryRegistrarOcorrenciaDecisaoAsync(
            solicitacao,
            filhas,
            DecisaoNegativacao.REJEITAR,
            aprovadorUsername,
            justificativa,
            cancellationToken);

        // Notify requester only (not Serasa)
        var notificationMessage = $"Solicitação de negativação rejeitada para venda {solicitacao.NumVendaFk}. Motivo: {justificativa ?? "Sem justificativa"}";
        await _notificationDispatcher.DispatchAsync(
            NotificationType.RejeicaoNegativacao,
            solicitacao.SolicitanteUsername ?? string.Empty,
            solicitacao.NumVendaFk,
            notificationMessage,
            null,
            null,
            cancellationToken);

        _logger.LogInformation("DecideNegativacao.NotifiedRequester - SolicitacaoId: {SolicitacaoId}", solicitacao.Id);
    }

    private async Task TryRegistrarOcorrenciaDecisaoAsync(
        SerasaPefinSolicitacaoCompleta solicitacao,
        IReadOnlyList<SerasaPefinSolicitacaoCompleta> filhas,
        DecisaoNegativacao decisao,
        string aprovadorUsername,
        string? justificativa,
        CancellationToken cancellationToken)
    {
        var solicitante = string.IsNullOrWhiteSpace(solicitacao.SolicitanteUsername)
            ? "nao informado"
            : solicitacao.SolicitanteUsername;

        string? protocolo = null;

        try
        {
            var venda = await _queryService.GetVendaAsync(solicitacao.NumVendaFk, cancellationToken)
                ?? throw new InvalidOperationException($"Venda {solicitacao.NumVendaFk} não encontrada para gerar ocorrência da decisão.");

            protocolo = await _protocoloGenerator.GerarProtocoloAsync(cancellationToken);

            var enderecoCompleto = FormatarEnderecoCompleto(venda.Endereco);
            var parcelasResumo = ColetarParcelasResumo(solicitacao, filhas);

            var now = DateTime.UtcNow;
            var descricao = decisao == DecisaoNegativacao.APROVAR
                ? NegativacaoOcorrenciaScripts.MontarMensagemAprovacao(
                    aprovadorUsername,
                    venda.NomeDevedor,
                    solicitacao.NumVendaFk,
                    enderecoCompleto,
                    parcelasResumo)
                : NegativacaoOcorrenciaScripts.MontarMensagemRejeicao(
                    aprovadorUsername,
                    venda.NomeDevedor,
                    solicitacao.NumVendaFk,
                    enderecoCompleto,
                    parcelasResumo,
                    justificativa);

            var statusOcorrencia = decisao == DecisaoNegativacao.APROVAR
                ? "Aprovacao de negativacao"
                : "Rejeicao de negativacao";

            var ocorrencia = Ocorrencia.Criar(
                numVendaFk: solicitacao.NumVendaFk,
                nomeUsuarioFk: aprovadorUsername,
                descricao: descricao,
                statusOcorrencia: statusOcorrencia,
                dtOcorrencia: now,
                horaOcorrencia: now.ToString("HH:mm"),
                protocolo: protocolo);

            await _ocorrenciaRepository.AddAsync(ocorrencia, cancellationToken);

            _logger.LogInformation(
                "DecideNegativacao.OcorrenciaRegistrada - SolicitacaoId: {SolicitacaoId}, NumVenda: {NumVenda}, Decisao: {Decisao}, Aprovador: {Aprovador}, Solicitante: {Solicitante}, Protocolo: {Protocolo}",
                solicitacao.Id,
                solicitacao.NumVendaFk,
                decisao,
                aprovadorUsername,
                solicitante,
                protocolo);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "DecideNegativacao.OcorrenciaFalha - SolicitacaoId: {SolicitacaoId}, NumVenda: {NumVenda}, Decisao: {Decisao}, Aprovador: {Aprovador}, Solicitante: {Solicitante}, Protocolo: {Protocolo}",
                solicitacao.Id,
                solicitacao.NumVendaFk,
                decisao,
                aprovadorUsername,
                solicitante,
                protocolo);
        }
    }

    private static string FormatarEnderecoCompleto(EnderecoDto? endereco)
    {
        if (endereco is null)
        {
            return "endereço não informado";
        }

        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(endereco.AddressLine))
        {
            partes.Add(endereco.AddressLine.Trim());
        }
        if (!string.IsNullOrWhiteSpace(endereco.Number))
        {
            partes.Add(endereco.Number.Trim());
        }
        if (!string.IsNullOrWhiteSpace(endereco.Complement))
        {
            partes.Add(endereco.Complement.Trim());
        }
        if (!string.IsNullOrWhiteSpace(endereco.District))
        {
            partes.Add(endereco.District.Trim());
        }

        var cidadeUf = new List<string>();
        if (!string.IsNullOrWhiteSpace(endereco.City))
        {
            cidadeUf.Add(endereco.City.Trim());
        }
        if (!string.IsNullOrWhiteSpace(endereco.State))
        {
            cidadeUf.Add(endereco.State.Trim());
        }
        if (cidadeUf.Count > 0)
        {
            partes.Add(string.Join("/", cidadeUf));
        }

        if (!string.IsNullOrWhiteSpace(endereco.ZipCode))
        {
            partes.Add($"CEP {FormatarCep(endereco.ZipCode)}");
        }

        return partes.Count > 0 ? string.Join(", ", partes) : "endereço não informado";
    }

    private static string FormatarCep(string cep)
    {
        var digitos = new string(cep.Where(char.IsDigit).ToArray());
        if (digitos.Length == 8)
        {
            return $"{digitos[..5]}-{digitos[5..]}";
        }
        return cep.Trim();
    }

    private static IReadOnlyList<ParcelaOcorrenciaInfo> ColetarParcelasResumo(
        SerasaPefinSolicitacaoCompleta solicitacao,
        IReadOnlyList<SerasaPefinSolicitacaoCompleta> filhas)
    {
        if (filhas is { Count: > 0 })
        {
            return filhas
                .OrderBy(f => f.NumeroParcela ?? int.MaxValue)
                .ThenBy(f => f.DataVencimento)
                .Select(f => new ParcelaOcorrenciaInfo(f.DataVencimento, f.Valor))
                .ToList();
        }

        // Solicitacao legada (sem filhas): usa os dados da propria solicitacao pai.
        return new[]
        {
            new ParcelaOcorrenciaInfo(solicitacao.DataVencimento, solicitacao.Valor),
        };
    }

    private async Task NotifyBothAsync(
        string? solicitanteUsername,
        string aprovadorUsername,
        int numVenda,
        string mensagem,
        NotificationType notificationType,
        CancellationToken cancellationToken)
    {
        var usernames = new List<string>();
        if (!string.IsNullOrWhiteSpace(solicitanteUsername))
        {
            usernames.Add(solicitanteUsername);
        }
        usernames.Add(aprovadorUsername);

        await _notificationDispatcher.DispatchManyAsync(
            notificationType,
            usernames.AsReadOnly(),
            numVenda,
            mensagem,
            null,
            null,
            cancellationToken);
    }
}

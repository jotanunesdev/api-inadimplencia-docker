using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Handler para <see cref="DecideBaixaCommand"/>. Valida que o usuário atual
/// é um aprovador autorizado, valida a senha de transação, valida estado
/// <see cref="SerasaPefinBaixaStatus.AguardandoAprovacao"/> e aplica a transição
/// correspondente (aprovação dispara <see cref="SendBaixaToSerasaCommand"/>;
/// rejeição grava justificativa). Em ambos os casos notifica o solicitante.
/// Solicitante não pode aprovar a própria solicitação salvo se for super-decisor.
/// </summary>
public sealed class DecideBaixaCommandHandler : ICommandHandler<DecideBaixaCommand, bool>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAprovadoresPolicy _aprovadoresPolicy;
    private readonly ISenhaTransacaoValidator _senhaValidator;
    private readonly ISerasaPefinBaixaRepository _baixaRepository;
    private readonly ICommandHandler<SendBaixaToSerasaCommand, bool> _sendBaixaHandler;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<DecideBaixaCommandHandler> _logger;

    public DecideBaixaCommandHandler(
        ICurrentUserService currentUserService,
        IAprovadoresPolicy aprovadoresPolicy,
        ISenhaTransacaoValidator senhaValidator,
        ISerasaPefinBaixaRepository baixaRepository,
        ICommandHandler<SendBaixaToSerasaCommand, bool> sendBaixaHandler,
        INotificationDispatcher notificationDispatcher,
        ILogger<DecideBaixaCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _aprovadoresPolicy = aprovadoresPolicy;
        _senhaValidator = senhaValidator;
        _baixaRepository = baixaRepository;
        _sendBaixaHandler = sendBaixaHandler;
        _notificationDispatcher = notificationDispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(DecideBaixaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.Username))
        {
            throw new UnauthorizedAccessException("User must be authenticated to decide baixa.");
        }

        var username = _currentUserService.Username!;

        // 1. Aprovador
        if (!_aprovadoresPolicy.IsAprovador(username))
        {
            _logger.LogWarning(
                "DecideBaixa.Unauthorized - SolicitacaoId: {SolicitacaoId}, Username: {Username}",
                command.SolicitacaoId, username);
            throw new UnauthorizedAccessException("NAO_AUTORIZADO: Usuário não é aprovador autorizado.");
        }

        // 2. Senha de transação
        var senhaResult = await _senhaValidator.ValidateAsync(username, command.SenhaTransacao, cancellationToken);
        if (senhaResult != SenhaTransacaoValidationResult.Valid)
        {
            var error = senhaResult switch
            {
                SenhaTransacaoValidationResult.Invalid => "SENHA_INVALIDA",
                SenhaTransacaoValidationResult.LockedOut => "SENHA_BLOQUEADA",
                SenhaTransacaoValidationResult.NotSet => "SENHA_NAO_CADASTRADA",
                _ => "ERRO_SENHA",
            };
            throw new UnauthorizedAccessException($"{error}: Senha de transação inválida.");
        }

        // 3. Carrega agregado
        var baixa = await _baixaRepository.GetByIdAsync(command.SolicitacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"NAO_ENCONTRADA: Baixa {command.SolicitacaoId} não encontrada.");

        // 4. Estado
        if (baixa.Status != SerasaPefinBaixaStatus.AguardandoAprovacao)
        {
            throw new InvalidOperationException(
                $"JA_DECIDIDA: Baixa {baixa.Id} já está em status {baixa.Status.ToDbValue()}.");
        }

        // 5. Solicitante não pode aprovar a própria solicitação (exceto super-decisor)
        if (string.Equals(baixa.SolicitanteUsername, username, StringComparison.OrdinalIgnoreCase)
            && !_aprovadoresPolicy.IsSuperDecisor(username))
        {
            throw new InvalidOperationException(
                "SOLICITANTE_NAO_PODE_APROVAR: O solicitante não pode aprovar sua própria solicitação de baixa.");
        }

        if (command.Decisao == DecisaoNegativacao.APROVAR)
        {
            await HandleAprovacaoAsync(baixa, username, cancellationToken);
        }
        else
        {
            await HandleRejeicaoAsync(baixa, username, command.Justificativa, cancellationToken);
        }

        return true;
    }

    private async Task HandleAprovacaoAsync(
        SerasaPefinBaixaSolicitacao baixa,
        string aprovadorUsername,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        baixa.MarcarAprovada(aprovadorUsername, utcNow);
        await _baixaRepository.UpdateAsync(baixa, cancellationToken);

        _logger.LogInformation(
            "DecideBaixa.Aprovada - BaixaId: {BaixaId}, Aprovador: {Aprovador}",
            baixa.Id, aprovadorUsername);

        // Envia ao Serasa. SendHandler é responsável por:
        //  - sucesso: BAIXA_AGUARDANDO_RETORNO + transactionId persistido.
        //  - falha HTTP: APROVADA_FALHA_ENVIO persistido + propaga exceção.
        try
        {
            await _sendBaixaHandler.HandleAsync(new SendBaixaToSerasaCommand(baixa.Id), cancellationToken);

            var mensagem =
                $"Solicitação de baixa aprovada para venda {baixa.NumVendaFk}, parcela " +
                $"{baixa.NumeroParcela?.ToString() ?? "-"}. Enviada ao Serasa, aguardando retorno.";
            await NotifySolicitanteAsync(baixa, mensagem, NotificationType.AprovacaoBaixa, cancellationToken);
        }
        catch (SerasaPefinHttpException ex)
        {
            var mensagem =
                $"Solicitação de baixa aprovada para venda {baixa.NumVendaFk}, parcela " +
                $"{baixa.NumeroParcela?.ToString() ?? "-"}, mas houve falha ao enviar ao Serasa: {ex.Message}.";
            await NotifySolicitanteAsync(baixa, mensagem, NotificationType.AprovacaoBaixa, cancellationToken);
            throw;
        }
    }

    private async Task HandleRejeicaoAsync(
        SerasaPefinBaixaSolicitacao baixa,
        string aprovadorUsername,
        string? justificativa,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(justificativa))
        {
            throw new ArgumentException("JUSTIFICATIVA_OBRIGATORIA: Justificativa é obrigatória para rejeição.", nameof(justificativa));
        }

        var utcNow = DateTime.UtcNow;
        baixa.MarcarRejeitada(aprovadorUsername, justificativa, utcNow);
        await _baixaRepository.UpdateAsync(baixa, cancellationToken);

        _logger.LogInformation(
            "DecideBaixa.Rejeitada - BaixaId: {BaixaId}, Aprovador: {Aprovador}",
            baixa.Id, aprovadorUsername);

        var mensagem =
            $"Solicitação de baixa rejeitada para venda {baixa.NumVendaFk}, parcela " +
            $"{baixa.NumeroParcela?.ToString() ?? "-"}. Motivo: {justificativa.Trim()}";
        await NotifySolicitanteAsync(baixa, mensagem, NotificationType.AprovacaoBaixa, cancellationToken);
    }

    private async Task NotifySolicitanteAsync(
        SerasaPefinBaixaSolicitacao baixa,
        string mensagem,
        NotificationType tipo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baixa.SolicitanteUsername))
        {
            return;
        }

        try
        {
            await _notificationDispatcher.DispatchAsync(
                tipo,
                baixa.SolicitanteUsername,
                baixa.NumVendaFk,
                mensagem,
                null,
                baixa.Id.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "DecideBaixa.NotifyError - BaixaId: {BaixaId}, Solicitante: {Solicitante}",
                baixa.Id, baixa.SolicitanteUsername);
        }
    }
}

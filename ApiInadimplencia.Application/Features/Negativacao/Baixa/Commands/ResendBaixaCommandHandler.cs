using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Handler para <see cref="ResendBaixaCommand"/>. Permite ao solicitante (ou
/// super-decisor) reenviar uma baixa que falhou via webhook
/// (<see cref="SerasaPefinBaixaStatus.BaixadoErro"/>) sem passar por nova
/// aprovação. Aplica <c>RegistrarTentativaReenvio</c> (incrementa contador
/// e transita para <see cref="SerasaPefinBaixaStatus.PendenteEnvio"/>),
/// invoca <see cref="SendBaixaToSerasaCommandHandler"/> e notifica o
/// solicitante. Limite estrito de 3 tentativas: a 4ª falha com mensagem clara.
/// </summary>
public sealed class ResendBaixaCommandHandler : ICommandHandler<ResendBaixaCommand, ResendBaixaResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAprovadoresPolicy _aprovadoresPolicy;
    private readonly ISerasaPefinBaixaRepository _baixaRepository;
    private readonly ICommandHandler<SendBaixaToSerasaCommand, bool> _sendBaixaHandler;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<ResendBaixaCommandHandler> _logger;

    public ResendBaixaCommandHandler(
        ICurrentUserService currentUserService,
        IAprovadoresPolicy aprovadoresPolicy,
        ISerasaPefinBaixaRepository baixaRepository,
        ICommandHandler<SendBaixaToSerasaCommand, bool> sendBaixaHandler,
        INotificationDispatcher notificationDispatcher,
        ILogger<ResendBaixaCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _aprovadoresPolicy = aprovadoresPolicy;
        _baixaRepository = baixaRepository;
        _sendBaixaHandler = sendBaixaHandler;
        _notificationDispatcher = notificationDispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ResendBaixaResult> HandleAsync(ResendBaixaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.Username))
        {
            throw new UnauthorizedAccessException("User must be authenticated to resend baixa.");
        }

        var username = _currentUserService.Username!;

        var baixa = await _baixaRepository.GetByIdAsync(command.SolicitacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"NAO_ENCONTRADA: Baixa {command.SolicitacaoId} não encontrada.");

        // Só o solicitante original (ou super-decisor) pode reenviar.
        var isSolicitante = string.Equals(baixa.SolicitanteUsername, username, StringComparison.OrdinalIgnoreCase);
        if (!isSolicitante && !_aprovadoresPolicy.IsSuperDecisor(username))
        {
            _logger.LogWarning(
                "ResendBaixa.Unauthorized - BaixaId: {BaixaId}, Username: {Username}, Solicitante: {Solicitante}",
                baixa.Id, username, baixa.SolicitanteUsername);
            throw new UnauthorizedAccessException(
                "NAO_AUTORIZADO: Apenas o solicitante original pode reenviar esta baixa.");
        }

        if (baixa.Status != SerasaPefinBaixaStatus.BaixadoErro)
        {
            throw new InvalidOperationException(
                $"ESTADO_INVALIDO: Reenvio só é permitido em BAIXADO_ERRO (status atual: {baixa.Status.ToDbValue()}).");
        }

        if (baixa.Tentativas >= SerasaPefinBaixaSolicitacao.LimiteTentativas)
        {
            throw new InvalidOperationException(
                $"LIMITE_TENTATIVAS_ATINGIDO: Limite de {SerasaPefinBaixaSolicitacao.LimiteTentativas} tentativas atingido para a baixa {baixa.Id}.");
        }

        baixa.RegistrarTentativaReenvio();
        await _baixaRepository.UpdateAsync(baixa, cancellationToken);

        _logger.LogInformation(
            "ResendBaixa.Iniciado - BaixaId: {BaixaId}, Tentativa: {Tentativa}/{Limite}",
            baixa.Id, baixa.Tentativas, SerasaPefinBaixaSolicitacao.LimiteTentativas);

        // SendBaixaToSerasaCommandHandler:
        //  - sucesso: agg em BAIXA_AGUARDANDO_RETORNO com novo transactionId persistido.
        //  - falha HTTP: agg em APROVADA_FALHA_ENVIO persistido + exceção propagada.
        await _sendBaixaHandler.HandleAsync(new SendBaixaToSerasaCommand(baixa.Id), cancellationToken);

        var transactionId = baixa.TransactionId
            ?? throw new InvalidOperationException(
                $"ESTADO_INESPERADO: Reenvio retornou sucesso porém TransactionId nulo para baixa {baixa.Id}.");

        await NotifySolicitanteAsync(baixa, cancellationToken);

        return new ResendBaixaResult(baixa.Id, transactionId, baixa.Tentativas);
    }

    private async Task NotifySolicitanteAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baixa.SolicitanteUsername))
        {
            return;
        }

        var mensagem =
            $"Baixa reenviada (tentativa {baixa.Tentativas}/{SerasaPefinBaixaSolicitacao.LimiteTentativas}) " +
            $"para venda {baixa.NumVendaFk}, parcela {baixa.NumeroParcela?.ToString() ?? "-"}. Aguardando retorno do Serasa.";

        try
        {
            await _notificationDispatcher.DispatchAsync(
                NotificationType.SolicitacaoBaixa,
                baixa.SolicitanteUsername,
                baixa.NumVendaFk,
                mensagem,
                null,
                $"resend:{baixa.Id}:{baixa.Tentativas}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ResendBaixa.NotifyError - BaixaId: {BaixaId}, Solicitante: {Solicitante}",
                baixa.Id, baixa.SolicitanteUsername);
        }
    }
}

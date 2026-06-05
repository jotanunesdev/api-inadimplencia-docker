using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;

namespace ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Handler para <see cref="SendBaixaToSerasaCommand"/>. Envia a solicitação de baixa
/// ao Serasa via DELETE por contrato. Em sucesso, transiciona o agregado para
/// <see cref="SerasaPefinBaixaStatus.BaixaAguardandoRetorno"/> com o transactionId
/// retornado. Em falha HTTP (<see cref="SerasaPefinHttpException"/>), transiciona
/// para <see cref="SerasaPefinBaixaStatus.AprovadaFalhaEnvio"/>, persiste o erro e
/// propaga a exceção. É idempotente: se o status já não permite envio (já enviado,
/// rejeitado, baixado etc.), simplesmente retorna <c>false</c> sem efeitos colaterais.
/// </summary>
public sealed class SendBaixaToSerasaCommandHandler : ICommandHandler<SendBaixaToSerasaCommand, bool>
{
    private readonly ISerasaPefinBaixaRepository _baixaRepository;
    private readonly ISerasaPefinGateway _gateway;
    private readonly ILogger<SendBaixaToSerasaCommandHandler> _logger;

    public SendBaixaToSerasaCommandHandler(
        ISerasaPefinBaixaRepository baixaRepository,
        ISerasaPefinGateway gateway,
        ILogger<SendBaixaToSerasaCommandHandler> logger)
    {
        _baixaRepository = baixaRepository;
        _gateway = gateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(SendBaixaToSerasaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var baixa = await _baixaRepository.GetByIdAsync(command.BaixaId, cancellationToken)
            ?? throw new InvalidOperationException($"Baixa nao encontrada: {command.BaixaId}.");

        // Idempotência: só envia se está em Aprovada ou PendenteEnvio.
        // Qualquer outro status (já enviado, rejeitado, baixado, falha) é no-op.
        if (baixa.Status is not (SerasaPefinBaixaStatus.Aprovada or SerasaPefinBaixaStatus.PendenteEnvio))
        {
            _logger.LogInformation(
                "Baixa {BaixaId} em status {Status} nao requer envio ao Serasa. Skip.",
                baixa.Id, baixa.Status);
            return false;
        }

        // Garantir transição APROVADA → PENDENTE_ENVIO antes de chamar o gateway.
        if (baixa.Status == SerasaPefinBaixaStatus.Aprovada)
        {
            baixa.MarcarPendenteEnvio();
        }

        var request = new SerasaBaixaRequest(
            CreditorDocument: baixa.DocumentoCredor,
            DebtorDocument: baixa.DocumentoDevedor,
            ContractNumber: baixa.ContractNumber,
            Reason: baixa.Motivo.Codigo);

        try
        {
            var response = await _gateway.DeleteByContractAsync(request, cancellationToken);
            baixa.MarcarBaixaAguardandoRetorno(response.TransactionId);
            await _baixaRepository.UpdateAsync(baixa, cancellationToken);

            _logger.LogInformation(
                "Baixa {BaixaId} enviada ao Serasa. TransactionId={TransactionId}",
                baixa.Id, response.TransactionId);

            return true;
        }
        catch (SerasaPefinHttpException ex)
        {
            _logger.LogError(ex,
                "Falha HTTP ao enviar baixa {BaixaId} ao Serasa. Status={Status}",
                baixa.Id, ex.StatusCode);

            baixa.MarcarFalhaEnvio(ex.Message, ex.StatusCode);
            await _baixaRepository.UpdateAsync(baixa, cancellationToken);
            throw;
        }
    }
}

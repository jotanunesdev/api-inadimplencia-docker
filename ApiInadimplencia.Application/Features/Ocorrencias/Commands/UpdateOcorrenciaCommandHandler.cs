using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Domain.Ocorrencias;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Commands;

/// <summary>
/// Handles the update of an existing occurrence.
/// </summary>
public class UpdateOcorrenciaCommandHandler : ICommandHandler<UpdateOcorrenciaCommand, bool>
{
    private readonly IOcorrenciaRepository _ocorrenciaRepository;

    public UpdateOcorrenciaCommandHandler(IOcorrenciaRepository ocorrenciaRepository)
    {
        _ocorrenciaRepository = ocorrenciaRepository;
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(UpdateOcorrenciaCommand command, CancellationToken cancellationToken = default)
    {
        var ocorrencia = await _ocorrenciaRepository.GetByIdAsync(command.Id, cancellationToken);
        if (ocorrencia == null)
        {
            return false;
        }

        ocorrencia.Atualizar(
            command.Descricao,
            command.StatusOcorrencia,
            command.DtOcorrencia,
            command.HoraOcorrencia,
            command.ProximaAcao,
            command.Protocolo);

        await _ocorrenciaRepository.UpdateAsync(ocorrencia, cancellationToken);

        return true;
    }
}

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Domain.Ocorrencias;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Commands;

/// <summary>
/// Handles the deletion of an occurrence.
/// </summary>
public class DeleteOcorrenciaCommandHandler : ICommandHandler<DeleteOcorrenciaCommand, bool>
{
    private readonly IOcorrenciaRepository _ocorrenciaRepository;

    public DeleteOcorrenciaCommandHandler(IOcorrenciaRepository ocorrenciaRepository)
    {
        _ocorrenciaRepository = ocorrenciaRepository;
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(DeleteOcorrenciaCommand command, CancellationToken cancellationToken = default)
    {
        var ocorrencia = await _ocorrenciaRepository.GetByIdAsync(command.Id, cancellationToken);
        if (ocorrencia == null)
        {
            return false;
        }

        await _ocorrenciaRepository.DeleteAsync(ocorrencia, cancellationToken);

        return true;
    }
}

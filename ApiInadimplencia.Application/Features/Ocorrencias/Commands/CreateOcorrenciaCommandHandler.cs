using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Ocorrencias.Dtos;
using ApiInadimplencia.Domain.Ocorrencias;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Commands;

/// <summary>
/// Handles the creation of a new occurrence.
/// </summary>
public class CreateOcorrenciaCommandHandler : ICommandHandler<CreateOcorrenciaCommand, Guid>
{
    private readonly IOcorrenciaRepository _ocorrenciaRepository;
    private readonly IVendaValidator _vendaValidator;

    public CreateOcorrenciaCommandHandler(
        IOcorrenciaRepository ocorrenciaRepository,
        IVendaValidator vendaValidator)
    {
        _ocorrenciaRepository = ocorrenciaRepository;
        _vendaValidator = vendaValidator;
    }

    /// <inheritdoc />
    public async Task<Guid> HandleAsync(CreateOcorrenciaCommand command, CancellationToken cancellationToken = default)
    {
        // Validate that the sale exists
        var vendaExiste = await _vendaValidator.VendaExisteAsync(command.NumVendaFk, cancellationToken);
        if (!vendaExiste)
        {
            throw new InvalidOperationException($"Venda com NUM_VENDA {command.NumVendaFk} não existe.");
        }

        // Create the occurrence domain entity
        var ocorrencia = Ocorrencia.Criar(
            command.NumVendaFk,
            command.NomeUsuarioFk,
            command.Descricao,
            command.StatusOcorrencia,
            command.DtOcorrencia,
            command.HoraOcorrencia,
            command.ProximaAcao,
            command.Protocolo);

        // Persist the occurrence
        await _ocorrenciaRepository.AddAsync(ocorrencia, cancellationToken);

        return ocorrencia.Id;
    }
}

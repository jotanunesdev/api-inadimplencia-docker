using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

/// <summary>
/// Command to update an existing occurrence.
/// </summary>
/// <param name="Id">Occurrence identifier.</param>
/// <param name="Descricao">Description, when updating.</param>
/// <param name="StatusOcorrencia">Status, when updating.</param>
/// <param name="DtOcorrencia">Date, when updating.</param>
/// <param name="HoraOcorrencia">Time, when updating.</param>
/// <param name="ProximaAcao">Next action date, when updating.</param>
/// <param name="Protocolo">Protocol number, when updating.</param>
public record UpdateOcorrenciaCommand(
    Guid Id,
    string? Descricao = null,
    string? StatusOcorrencia = null,
    DateTime? DtOcorrencia = null,
    string? HoraOcorrencia = null,
    string? ProximaAcao = null,
    string? Protocolo = null) : ICommand<bool>;

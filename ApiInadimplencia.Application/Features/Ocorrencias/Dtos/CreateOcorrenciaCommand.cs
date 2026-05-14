using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

/// <summary>
/// Command to create a new occurrence.
/// </summary>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="NomeUsuarioFk">Username of the user registering the occurrence.</param>
/// <param name="Descricao">Description of the occurrence.</param>
/// <param name="StatusOcorrencia">Status of the occurrence.</param>
/// <param name="DtOcorrencia">Date of the occurrence.</param>
/// <param name="HoraOcorrencia">Time of the occurrence.</param>
/// <param name="ProximaAcao">Next action date, when present.</param>
/// <param name="Protocolo">Protocol number, when present.</param>
public record CreateOcorrenciaCommand(
    int NumVendaFk,
    string NomeUsuarioFk,
    string Descricao,
    string StatusOcorrencia,
    DateTime DtOcorrencia,
    string HoraOcorrencia,
    string? ProximaAcao = null,
    string? Protocolo = null) : ICommand<Guid>;

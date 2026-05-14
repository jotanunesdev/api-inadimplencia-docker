using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Ocorrencias.Dtos;

/// <summary>
/// Command to delete an occurrence.
/// </summary>
/// <param name="Id">Occurrence identifier.</param>
public record DeleteOcorrenciaCommand(Guid Id) : ICommand<bool>;

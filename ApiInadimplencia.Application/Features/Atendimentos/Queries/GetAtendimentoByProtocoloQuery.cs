using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;

namespace ApiInadimplencia.Application.Features.Atendimentos.Queries;

/// <summary>
/// Query to get an attendance by protocol.
/// </summary>
/// <param name="Protocolo">Protocol number.</param>
public sealed record GetAtendimentoByProtocoloQuery(string Protocolo) : IQuery<AtendimentoDto?>;

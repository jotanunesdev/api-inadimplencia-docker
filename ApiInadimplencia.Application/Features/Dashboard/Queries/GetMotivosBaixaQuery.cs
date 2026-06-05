using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Dashboard.Dtos;

namespace ApiInadimplencia.Application.Features.Dashboard.Queries;

/// <summary>
/// Query para o card "Motivos de Baixa" do dashboard.
/// </summary>
/// <param name="Meses">Janela rolante em meses (1..24, default 12).</param>
public sealed record GetMotivosBaixaQuery(int Meses = 12) : IQuery<IReadOnlyList<MotivoBaixaDto>>;

namespace ApiInadimplencia.Application.Features.Relatorios.Dtos;

/// <summary>
/// DTO para resposta de relatório
/// </summary>
public record RelatorioDto(
    string Url,
    string ReportId,
    DateTime GeradoEm);

using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Relatorios.Dtos;

/// <summary>
/// Command para gerar relatório ficha financeira
/// </summary>
public record GenerateFichaFinanceiraCommand(
    int NumVenda,
    string CodColigada,
    string ReportColigada,
    string ReportId) : ICommand<string>;

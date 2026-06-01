using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Relatorios.Dtos;

/// <summary>
/// Command para gerar relatório ficha financeira (URL do PDF gerado pelo TOTVS RM via Fluig).
/// Quando os parâmetros opcionais não são informados, o handler usa os defaults do <c>RmOptions</c>.
/// </summary>
/// <param name="NumVenda">Número da venda (obrigatório).</param>
/// <param name="CodColigada">Coligada usada no PARAMETER do relatório. Default: RmOptions.ParamColigada.</param>
/// <param name="ReportColigada">Coligada usada para localizar o relatório. Default: RmOptions.ReportColigada.</param>
/// <param name="ReportId">Identificador do relatório no RM. Default: RmOptions.ReportId.</param>
public record GenerateFichaFinanceiraCommand(
    int NumVenda,
    int? CodColigada = null,
    int? ReportColigada = null,
    int? ReportId = null) : ICommand<string>;

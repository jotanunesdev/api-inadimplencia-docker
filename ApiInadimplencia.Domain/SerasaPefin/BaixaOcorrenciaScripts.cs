using System.Globalization;

namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Mensagens padronizadas de ocorrência para o fluxo de baixa Serasa PEFIN
/// (write-off). Espelha <c>NegativacaoOcorrenciaScripts</c> mas com vocabulário
/// específico do contexto de baixa.
/// </summary>
public static class BaixaOcorrenciaScripts
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Monta a mensagem de ocorrência para a solicitação de baixa.
    /// Template: "Eu, {usuario} solicito a baixa do cliente {cliente}, venda nº {numVenda}, parcelas: {parcelas}, motivo: {motivo} ({motivoCodigo})[. Justificativa: {just}] via Serasa".
    /// </summary>
    public static string MontarMensagemSolicitacao(
        string usuario,
        string cliente,
        int numVenda,
        IReadOnlyList<int> parcelas,
        SerasaPefinBaixaMotivo motivo,
        string? justificativa = null)
    {
        var parcelasStr = string.Join(", ", parcelas);
        var nomeCliente = string.IsNullOrWhiteSpace(cliente) ? "nao informado" : cliente.Trim();

        var mensagem =
            $"Eu, {usuario} solicito a baixa do cliente {nomeCliente}, venda nº {numVenda.ToString(CultureInfo.InvariantCulture)}, " +
            $"parcelas: {parcelasStr}, motivo: {motivo.Descricao} ({motivo.Codigo})";

        if (!string.IsNullOrWhiteSpace(justificativa))
        {
            mensagem += $". Justificativa: {justificativa.Trim()}";
        }

        mensagem += " via Serasa";
        return mensagem;
    }
}

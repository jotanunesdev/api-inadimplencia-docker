using System.Globalization;

namespace ApiInadimplencia.Domain.Negativacao;

/// <summary>
/// Dados resumidos de uma parcela usados nas mensagens de ocorrencia.
/// </summary>
/// <param name="Vencimento">Data de vencimento da parcela.</param>
/// <param name="Valor">Valor da parcela (em reais).</param>
public readonly record struct ParcelaOcorrenciaInfo(DateOnly Vencimento, decimal Valor);

/// <summary>
/// Static service for generating standardized occurrence messages for the negativacao workflow.
/// </summary>
public static class NegativacaoOcorrenciaScripts
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Generates the occurrence message for a negativacao request.
    /// Template: "Eu, {usuario} solicito a negativação do cliente {nome}, venda nº {numVenda}, endereço {end}, para a(as) parcelas: {parcelas} [e seus fiadores {fiadores}] via Serasa"
    /// </summary>
    /// <param name="usuario">Username of the requester.</param>
    /// <param name="cliente">Client name.</param>
    /// <param name="numVenda">Sale number.</param>
    /// <param name="endereco">Debtor address.</param>
    /// <param name="parcelas">List of parcela IDs.</param>
    /// <param name="fiadores">Optional list of guarantor names.</param>
    /// <returns>The formatted occurrence message with masked documents.</returns>
    public static string MontarMensagemSolicitacao(
        string usuario,
        string cliente,
        int numVenda,
        string endereco,
        IReadOnlyList<long> parcelas,
        IReadOnlyList<string>? fiadores = null)
    {
        var parcelasStr = string.Join(", ", parcelas);
        
        var mensagem = $"Eu, {usuario} solicito a negativação do cliente {cliente}, venda nº {numVenda}, endereço {endereco}, para a(as) parcelas: {parcelasStr}";
        
        if (fiadores != null && fiadores.Count > 0)
        {
            var fiadoresStr = string.Join(", ", fiadores);
            mensagem += $" e seus fiadores {fiadoresStr}";
        }
        
        mensagem += " via Serasa";
        
        return mensagem;
    }

    /// <summary>
    /// Generates the occurrence message for an approval, in the format requested by
    /// stakeholders: "Eu, &lt;nome usuario&gt;, aprovo a solicitacao de negativacao do
    /// cliente &lt;cliente&gt;, com a venda Nº &lt;numVenda&gt;, no endereço &lt;endereco&gt;,
    /// para as parcelas &lt;vencimento1&gt; R$ &lt;valor1&gt;, ...".
    /// </summary>
    /// <param name="aprovador">Nome (ou username) do aprovador.</param>
    /// <param name="cliente">Nome do cliente.</param>
    /// <param name="numVenda">Numero da venda.</param>
    /// <param name="endereco">Endereco completo formatado.</param>
    /// <param name="parcelas">Parcelas decididas (vencimento + valor).</param>
    /// <returns>Mensagem padronizada para registrar a ocorrencia de aprovacao.</returns>
    public static string MontarMensagemAprovacao(
        string aprovador,
        string cliente,
        int numVenda,
        string endereco,
        IReadOnlyList<ParcelaOcorrenciaInfo> parcelas)
    {
        return MontarMensagemDecisao(
            verbo: "aprovo",
            usuario: aprovador,
            cliente: cliente,
            numVenda: numVenda,
            endereco: endereco,
            parcelas: parcelas,
            justificativa: null,
            incluirJustificativa: false);
    }

    /// <summary>
    /// Generates the occurrence message for a rejection, in the format requested by
    /// stakeholders: "Eu, &lt;nome usuario&gt;, rejeito a solicitacao de negativacao do
    /// cliente &lt;cliente&gt;, com a venda Nº &lt;numVenda&gt;, no endereço &lt;endereco&gt;,
    /// para as parcelas &lt;vencimento1&gt; R$ &lt;valor1&gt;, ... Justificativa: ...".
    /// </summary>
    /// <param name="aprovador">Nome (ou username) do aprovador.</param>
    /// <param name="cliente">Nome do cliente.</param>
    /// <param name="numVenda">Numero da venda.</param>
    /// <param name="endereco">Endereco completo formatado.</param>
    /// <param name="parcelas">Parcelas decididas (vencimento + valor).</param>
    /// <param name="justificativa">Justificativa fornecida.</param>
    /// <returns>Mensagem padronizada para registrar a ocorrencia de rejeicao.</returns>
    public static string MontarMensagemRejeicao(
        string aprovador,
        string cliente,
        int numVenda,
        string endereco,
        IReadOnlyList<ParcelaOcorrenciaInfo> parcelas,
        string? justificativa)
    {
        return MontarMensagemDecisao(
            verbo: "rejeito",
            usuario: aprovador,
            cliente: cliente,
            numVenda: numVenda,
            endereco: endereco,
            parcelas: parcelas,
            justificativa: justificativa,
            incluirJustificativa: true);
    }

    private static string MontarMensagemDecisao(
        string verbo,
        string usuario,
        string cliente,
        int numVenda,
        string endereco,
        IReadOnlyList<ParcelaOcorrenciaInfo> parcelas,
        string? justificativa,
        bool incluirJustificativa)
    {
        var nomeUsuario = CapitalizarNome(usuario);
        var nomeCliente = string.IsNullOrWhiteSpace(cliente) ? "nao informado" : cliente.Trim();
        var enderecoTexto = string.IsNullOrWhiteSpace(endereco) ? "nao informado" : endereco.Trim();
        var parcelasTexto = parcelas is { Count: > 0 }
            ? string.Join(", ", parcelas.Select(FormatarParcela))
            : "nao informadas";

        var mensagem = $"Eu, {nomeUsuario}, {verbo} a solicitacao de negativacao do cliente {nomeCliente}, com a venda Nº {numVenda.ToString(CultureInfo.InvariantCulture)}, no endereço {enderecoTexto}, para as parcelas {parcelasTexto}.";

        if (incluirJustificativa)
        {
            var justificativaTexto = string.IsNullOrWhiteSpace(justificativa)
                ? "nao informada"
                : justificativa.Trim();
            mensagem += $" Justificativa: {justificativaTexto}.";
        }

        return mensagem;
    }

    private static string FormatarParcela(ParcelaOcorrenciaInfo parcela)
    {
        var vencimento = parcela.Vencimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var valor = parcela.Valor.ToString("N2", PtBr);
        return $"{vencimento} R$ {valor}";
    }

    /// <summary>
    /// Capitaliza um nome ou username para exibicao ("gustavo trindade" -> "Gustavo Trindade",
    /// "gustavo.trindade" -> "Gustavo Trindade"). Username vazio retorna placeholder.
    /// </summary>
    public static string CapitalizarNome(string? usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario))
        {
            return "nao informado";
        }

        var normalizado = usuario
            .Trim()
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .ToLower(PtBr);

        return PtBr.TextInfo.ToTitleCase(normalizado);
    }

    /// <summary>
    /// Masks a CPF/CNPJ document for display in logs and occurrences.
    /// CPF: Shows first 3 digits, masks middle, shows last 2.
    /// CNPJ: Shows first 2 digits, masks middle, shows last 2.
    /// </summary>
    /// <param name="documento">Document string (digits only).</param>
    /// <returns>The masked document string.</returns>
    public static string MaskDocument(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return "***";
        }

        var digitsOnly = new string(documento.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length == 11) // CPF
        {
            return $"{digitsOnly.Substring(0, 3)}***{digitsOnly.Substring(9)}";
        }
        else if (digitsOnly.Length == 14) // CNPJ
        {
            return $"{digitsOnly.Substring(0, 2)}***{digitsOnly.Substring(12)}";
        }
        else
        {
            // Unknown format, mask most of it
            var visibleLength = Math.Min(3, digitsOnly.Length);
            return $"{digitsOnly.Substring(0, visibleLength)}***";
        }
    }
}

namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Value Object que representa o motivo de uma solicitação de baixa Serasa PEFIN.
/// Aceita somente a whitelist oficial Serasa: 1, 2, 3, 4, 19, 43, 45.
/// Imutável, com igualdade estrutural baseada em <see cref="Codigo"/>.
/// </summary>
public sealed record SerasaPefinBaixaMotivo
{
    /// <summary>Códigos válidos conforme contrato Serasa Experian.</summary>
    public static readonly IReadOnlySet<byte> CodigosValidos = new HashSet<byte> { 1, 2, 3, 4, 19, 43, 45 };

    private static readonly IReadOnlyDictionary<byte, string> Descricoes = new Dictionary<byte, string>
    {
        [1] = "PAGAMENTO DA DIVIDA",
        [2] = "RENEGOCIACAO DA DIVIDA",
        [3] = "POR SOLICITACAO DO CLIENTE",
        [4] = "ORDEM JUDICIAL",
        [19] = "RENEGOCIACAO DA DIVIDA POR ACORDO",
        [43] = "BAIXA POR NEGOCIACAO",
        [45] = "CONTESTACAO",
    };

    /// <summary>Código numérico do motivo conforme tabela Serasa.</summary>
    public byte Codigo { get; }

    /// <summary>Descrição canônica do motivo (ASCII upper-case, sem acentos).</summary>
    public string Descricao { get; }

    private SerasaPefinBaixaMotivo(byte codigo, string descricao)
    {
        Codigo = codigo;
        Descricao = descricao;
    }

    /// <summary>
    /// Cria uma instância a partir do <paramref name="codigo"/> Serasa.
    /// </summary>
    /// <exception cref="ArgumentException">Se o motivo não está na whitelist.</exception>
    public static SerasaPefinBaixaMotivo From(byte codigo)
    {
        if (!Descricoes.TryGetValue(codigo, out var descricao))
        {
            throw new ArgumentException(
                $"Codigo de motivo invalido: {codigo}. Valores aceitos: {string.Join(", ", CodigosValidos)}.",
                nameof(codigo));
        }

        return new SerasaPefinBaixaMotivo(codigo, descricao);
    }
}

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

// Contrato validado pelo Financeiro: valor da fonte inclui descontos e reflete
// ajustes/estornos. Não é caixa puro nem histórico imutável de cada baixa parcial.
public static class ValorRecuperadoSql
{
    public const string Mensal = """
        WITH Periodo AS (
            SELECT COALESCE(@dataInicio, DATEADD(month, -11, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))) AS INICIO,
                   COALESCE(@dataFim, CONVERT(date, GETDATE())) AS FIM
        ), Meses AS (
            SELECT DATEFROMPARTS(YEAR(INICIO), MONTH(INICIO), 1) AS MES FROM Periodo
            UNION ALL
            SELECT DATEADD(month, 1, MES) FROM Meses CROSS JOIN Periodo
            WHERE MES < DATEFROMPARTS(YEAR(FIM), MONTH(FIM), 1)
        ), Valores AS (
            SELECT DATEFROMPARTS(YEAR(f.DATA_PAGAMENTO), MONTH(f.DATA_PAGAMENTO), 1) AS MES,
                SUM(f.VALOR_RECUPERADO) AS VALOR_RECUPERADO,
                COUNT_BIG(*) AS PARCELAS,
                SUM(CASE WHEN f.VALOR_RECUPERADO IS NULL THEN 1 ELSE 0 END) AS VALORES_AUSENTES,
                MAX(f.DATA_CARGA) AS DATA_CARGA
            FROM dw.ficha_financeira_valor_recuperado f CROSS JOIN Periodo p
            WHERE f.DATA_PAGAMENTO >= p.INICIO AND f.DATA_PAGAMENTO < DATEADD(day, 1, p.FIM)
              AND f.DIAS_ATRASO > 30
              AND (@semResponsavel=0 OR NOT EXISTS (SELECT 1 FROM dbo.VENDA_RESPONSAVEL sr
                  WHERE sr.NUM_VENDA_FK=f.NUM_VENDA AND NULLIF(LTRIM(RTRIM(sr.NOME_USUARIO_FK)),'') IS NOT NULL))
            GROUP BY DATEFROMPARTS(YEAR(f.DATA_PAGAMENTO), MONTH(f.DATA_PAGAMENTO), 1)
        )
        SELECT CONVERT(varchar(10), m.MES, 23) AS MES,
            CASE WHEN v.VALORES_AUSENTES > 0 THEN NULL ELSE COALESCE(v.VALOR_RECUPERADO, 0) END AS VALOR_RECUPERADO,
            COALESCE(v.PARCELAS, 0) AS PARCELAS, COALESCE(v.VALORES_AUSENTES, 0) AS VALORES_AUSENTES,
            v.DATA_CARGA,
            CONVERT(varchar(10), p.INICIO, 23) AS DATA_INICIO,
            CONVERT(varchar(10), p.FIM, 23) AS DATA_FIM
        FROM Meses m CROSS JOIN Periodo p LEFT JOIN Valores v ON v.MES = m.MES
        ORDER BY m.MES OPTION (MAXRECURSION 61)
        """;

    // A PK de IDLAN garante uma linha por parcela. Não juntar parcelas abertas
    // nem filtrar pelo estado atual da venda: ambas as opções perderiam recuperações.
    public const string Detalhes = """
        SELECT f.IDLAN, f.NUM_VENDA, f.CLIENTE, f.CPF_CNPJ, f.EMPREENDIMENTO,
            f.NUM_DOCUMENTO, f.STATUSLAN, f.DATA_VENCIMENTO, f.DATA_PAGAMENTO, f.DIAS_ATRASO,
            f.VALOR_RECUPERADO, f.SALDO_RECEBER, f.DATA_CARGA,
            r.NOME_USUARIO_FK AS RESPONSAVEL, v.INADIMPLENTE AS INADIMPLENTE_ATUAL,
            COUNT_BIG(*) OVER() AS TOTAL_COUNT,
            SUM(f.VALOR_RECUPERADO) OVER() AS TOTAL_VALOR_RECUPERADO,
            SUM(CASE WHEN f.VALOR_RECUPERADO IS NULL THEN 1 ELSE 0 END) OVER() AS VALORES_AUSENTES,
            MAX(f.DATA_CARGA) OVER() AS DATA_CARGA_PERIODO
        FROM dw.ficha_financeira_valor_recuperado f
        OUTER APPLY (
            SELECT TOP (1) r.NOME_USUARIO_FK FROM dbo.VENDA_RESPONSAVEL r
            WHERE r.NUM_VENDA_FK = f.NUM_VENDA
            ORDER BY r.DT_ATRIBUICAO DESC, r.NOME_USUARIO_FK
        ) r
        LEFT JOIN DW.fat_analise_inadimplencia_v4 v ON v.NUM_VENDA = f.NUM_VENDA
        WHERE f.DATA_PAGAMENTO >= @dataInicio AND f.DATA_PAGAMENTO < DATEADD(day, 1, @dataFim)
          AND f.DIAS_ATRASO > 30
          AND (@semResponsavel=0 OR NOT EXISTS (SELECT 1 FROM dbo.VENDA_RESPONSAVEL sr
              WHERE sr.NUM_VENDA_FK=f.NUM_VENDA AND NULLIF(LTRIM(RTRIM(sr.NOME_USUARIO_FK)),'') IS NOT NULL))
        ORDER BY f.DATA_PAGAMENTO DESC, f.IDLAN
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;
}

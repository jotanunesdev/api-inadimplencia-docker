namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>Somente leitura das observações prospectivas. Nenhum GET cria capturas.</summary>
public static class InadimplenciaObservadaSql
{
    public const string Disponibilidade = """
        SELECT CASE WHEN OBJECT_ID('dbo.INAD_CAPTURA','U') IS NOT NULL
            AND OBJECT_ID('dbo.INAD_VENDA_OBSERVADA','U') IS NOT NULL
            AND OBJECT_ID('dbo.INAD_PARCELA_OBSERVADA','U') IS NOT NULL
            THEN 1 ELSE 0 END DISPONIVEL;
        """;

    // Compara com a captura global imediatamente anterior, nunca atravessa desaparecimentos.
    public const string Base = """
        WITH Chaves AS (
            SELECT c.ID CAPTURA_ID,v.NUM_VENDA FROM dbo.INAD_CAPTURA c
                JOIN dbo.INAD_VENDA_OBSERVADA v ON v.CAPTURA_ID=c.ID
            UNION
            SELECT c.ID,v.NUM_VENDA FROM dbo.INAD_CAPTURA c
                JOIN dbo.INAD_VENDA_OBSERVADA v ON v.CAPTURA_ID=c.ANTERIOR_ID
        ), Comparacao AS (
            SELECT c.ID,c.OBSERVADO_EM,k.NUM_VENDA,c.ANTERIOR_ID,
                a.SITUACAO SITUACAO_ANTERIOR,v.SITUACAO,v.INAD_FONTE,
                CASE WHEN c.ANTERIOR_ID IS NULL THEN 'BASE_INICIAL'
                    WHEN a.SITUACAO IS NULL OR v.SITUACAO IS NULL THEN 'SEM_COMPARACAO'
                    WHEN a.SITUACAO=0 AND v.SITUACAO=1 THEN 'ENTROU_INADIMPLENCIA'
                    WHEN a.SITUACAO=1 AND v.SITUACAO=0 AND v.QUITACAO_ANTERIOR=1 THEN 'REGULARIZADO'
                    WHEN a.SITUACAO=1 AND v.SITUACAO=0 THEN 'SEM_COMPARACAO'
                    ELSE 'SEM_MUDANCA' END EVENTO
            FROM Chaves k JOIN dbo.INAD_CAPTURA c ON c.ID=k.CAPTURA_ID
            LEFT JOIN dbo.INAD_VENDA_OBSERVADA a ON a.CAPTURA_ID=c.ANTERIOR_ID AND a.NUM_VENDA=k.NUM_VENDA
            LEFT JOIN dbo.INAD_VENDA_OBSERVADA v ON v.CAPTURA_ID=c.ID AND v.NUM_VENDA=k.NUM_VENDA
        ), Eventos AS (
            SELECT c.*,CASE WHEN c.EVENTO='ENTROU_INADIMPLENCIA' AND
                MAX(CASE WHEN EVENTO='REGULARIZADO' THEN 1 ELSE 0 END) OVER (
                    PARTITION BY NUM_VENDA ORDER BY ID ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)=1
                THEN 1 ELSE 0 END REINCIDENTE
            FROM Comparacao c
        )
        """;

    public const string Mensais = Base + """
        , Cobertura AS (
            SELECT MIN(OBSERVADO_EM) PRIMEIRA_CAPTURA,MAX(OBSERVADO_EM) ULTIMA_CAPTURA,MIN(ID) PRIMEIRO_ID
            FROM dbo.INAD_CAPTURA
        ), Intervalo AS (
            SELECT COALESCE(@dataInicio,DATEADD(month,-11,DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))) INICIO,
                COALESCE(@dataFim,CAST(GETDATE() AS date)) FIM
        ), Meses AS (
            SELECT DATEFROMPARTS(YEAR(INICIO),MONTH(INICIO),1) MES FROM Intervalo
            UNION ALL
            SELECT DATEADD(month,1,MES) FROM Meses CROSS JOIN Intervalo
            WHERE MES<DATEFROMPARTS(YEAR(FIM),MONTH(FIM),1)
        ), CapturasPeriodo AS (
            SELECT c.* FROM dbo.INAD_CAPTURA c CROSS JOIN Intervalo i
            WHERE CAST(c.OBSERVADO_EM AS date) BETWEEN i.INICIO AND i.FIM
        ), Mensal AS (
            SELECT DATEFROMPARTS(YEAR(e.OBSERVADO_EM),MONTH(e.OBSERVADO_EM),1) MES,
                SUM(CASE WHEN EVENTO='ENTROU_INADIMPLENCIA' THEN 1 ELSE 0 END) ENTRADAS,
                SUM(CASE WHEN EVENTO='REGULARIZADO' THEN 1 ELSE 0 END) SAIDAS,
                SUM(REINCIDENTE) REINCIDENTES,
                SUM(CASE WHEN EVENTO='SEM_COMPARACAO' THEN 1 ELSE 0 END) SEM_COMPARACAO
            FROM Eventos e JOIN CapturasPeriodo c ON c.ID=e.ID
            GROUP BY YEAR(e.OBSERVADO_EM),MONTH(e.OBSERVADO_EM)
        )
        SELECT 1 DISPONIVEL,m.MES,cob.PRIMEIRA_CAPTURA,cob.ULTIMA_CAPTURA,
            (SELECT COUNT(*) FROM dbo.INAD_VENDA_OBSERVADA v WHERE v.CAPTURA_ID=cob.PRIMEIRO_ID AND v.INAD_FONTE=1) BASE_INADIMPLENTES,
            (SELECT COUNT(*) FROM CapturasPeriodo c WHERE YEAR(c.OBSERVADO_EM)=YEAR(m.MES) AND MONTH(c.OBSERVADO_EM)=MONTH(m.MES)) CAPTURAS,
            (SELECT COUNT(*) FROM CapturasPeriodo c WHERE c.ANTERIOR_ID IS NOT NULL AND YEAR(c.OBSERVADO_EM)=YEAR(m.MES) AND MONTH(c.OBSERVADO_EM)=MONTH(m.MES)) COMPARACOES,
            a.ENTRADAS,a.SAIDAS,a.ENTRADAS-a.SAIDAS SALDO,a.REINCIDENTES,a.SEM_COMPARACAO,
            CASE WHEN ultima.ID IS NOT NULL THEN (SELECT COUNT(*) FROM dbo.INAD_VENDA_OBSERVADA v WHERE v.CAPTURA_ID=ultima.ID AND v.INAD_FONTE=1) END TOTAL_INADIMPLENTES,
            ultima.OBSERVADO_EM DATA_POSICAO
        FROM Meses m CROSS JOIN Cobertura cob LEFT JOIN Mensal a ON a.MES=m.MES
        OUTER APPLY (SELECT TOP(1) c.ID,c.OBSERVADO_EM FROM CapturasPeriodo c
            WHERE YEAR(c.OBSERVADO_EM)=YEAR(m.MES) AND MONTH(c.OBSERVADO_EM)=MONTH(m.MES) ORDER BY c.ID DESC) ultima
        ORDER BY m.MES OPTION(MAXRECURSION 61);
        """;

    public const string Detalhes = Base + """
        SELECT e.ID CAPTURA_ID,e.OBSERVADO_EM DATA_REFERENCIA,e.NUM_VENDA,
            e.SITUACAO_ANTERIOR,e.SITUACAO,e.EVENTO,e.REINCIDENTE,
            v.CLIENTE,v.CPF_CNPJ,r.NOME_USUARIO_FK RESPONSAVEL,COUNT_BIG(*) OVER() TOTAL_COUNT
        FROM Eventos e
        LEFT JOIN DW.fat_analise_inadimplencia_v4 v ON v.NUM_VENDA=e.NUM_VENDA
        LEFT JOIN dbo.VENDA_RESPONSAVEL r ON r.NUM_VENDA_FK=e.NUM_VENDA
        WHERE CAST(e.OBSERVADO_EM AS date) BETWEEN @dataInicio AND @dataFim
            AND e.EVENTO IN ('ENTROU_INADIMPLENCIA','REGULARIZADO','SEM_COMPARACAO')
            AND (@situacao IS NULL OR e.EVENTO=@situacao)
        ORDER BY e.ID DESC,e.NUM_VENDA OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;
        """;
}

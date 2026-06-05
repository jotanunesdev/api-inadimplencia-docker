-- =============================================================================
-- 2026-06-03 - Serasa PEFIN: Views Agregadas para Dashboard de Baixa
-- Cria duas views consumidas pelos cards do dashboard:
--   1) vw_serasa_pefin_baixa_motivos
--      Distribuicao dos motivos das baixas concluidas com sucesso nos
--      ultimos 12 meses (rolling window).
--   2) vw_serasa_pefin_negativacao_baixa_mensal
--      Serie mensal (ultimos 12 meses) com totais de negativacoes
--      concluidas (NEGATIVADO_SUCESSO em SERASA_PEFIN_SOLICITACOES) e
--      baixas concluidas (BAIXADO_SUCESSO em SERASA_PEFIN_BAIXAS).
-- Script idempotente via CREATE OR ALTER. Requer SQL Server 2016+.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
GO

-- 1) Motivos de baixa (ultimos 12 meses)
CREATE OR ALTER VIEW dbo.vw_serasa_pefin_baixa_motivos
AS
WITH baixas AS (
    SELECT
        b.MOTIVO,
        b.DT_ATUALIZACAO
    FROM dbo.SERASA_PEFIN_BAIXAS AS b
    WHERE b.STATUS = 'BAIXADO_SUCESSO'
      AND b.DT_ATUALIZACAO >= DATEADD(MONTH, -12, CAST(SYSUTCDATETIME() AS date))
),
total AS (
    SELECT COUNT_BIG(*) AS QTD_TOTAL FROM baixas
)
SELECT
    m.CODIGO                                              AS MOTIVO,
    m.DESCRICAO                                           AS DESCRICAO,
    COUNT_BIG(b.MOTIVO)                                   AS QTD,
    CAST(
        CASE
            WHEN (SELECT QTD_TOTAL FROM total) = 0 THEN 0
            ELSE 100.0 * COUNT_BIG(b.MOTIVO) / (SELECT QTD_TOTAL FROM total)
        END AS decimal(6,2)
    )                                                     AS PERCENTUAL
FROM (
    VALUES
        ( 1, 'PAGAMENTO DA DIVIDA'),
        ( 2, 'RENEGOCIACAO DA DIVIDA'),
        ( 3, 'POR SOLICITACAO DO CLIENTE'),
        ( 4, 'ORDEM JUDICIAL'),
        (19, 'RENEGOCIACAO DA DIVIDA POR ACORDO'),
        (43, 'BAIXA POR NEGOCIACAO'),
        (45, 'CONTESTACAO')
) AS m (CODIGO, DESCRICAO)
LEFT JOIN baixas AS b
       ON b.MOTIVO = m.CODIGO
GROUP BY m.CODIGO, m.DESCRICAO;
GO

-- 2) Comparativo mensal (ultimos 12 meses): negativacoes vs baixas concluidas
CREATE OR ALTER VIEW dbo.vw_serasa_pefin_negativacao_baixa_mensal
AS
WITH meses AS (
    -- Gera os ultimos 12 meses (incluindo o mes atual) como ancoras.
    -- Usa systable de pequena seletividade para evitar recursao/CTE pesada.
    SELECT TOP (12)
        DATEFROMPARTS(
            YEAR(DATEADD(MONTH, -ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) + 1, SYSUTCDATETIME())),
            MONTH(DATEADD(MONTH, -ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) + 1, SYSUTCDATETIME())),
            1
        ) AS PRIMEIRO_DIA
    FROM sys.all_objects
),
negativacoes AS (
    SELECT
        DATEFROMPARTS(YEAR(s.DT_ATUALIZACAO), MONTH(s.DT_ATUALIZACAO), 1) AS PRIMEIRO_DIA,
        COUNT_BIG(*)                                                     AS QTD
    FROM dbo.SERASA_PEFIN_SOLICITACOES AS s
    WHERE s.STATUS = 'NEGATIVADO_SUCESSO'
      AND s.TIPO_REGISTRO = 'PRINCIPAL'
      AND s.DT_ATUALIZACAO >= DATEADD(MONTH, -12, CAST(SYSUTCDATETIME() AS date))
    GROUP BY DATEFROMPARTS(YEAR(s.DT_ATUALIZACAO), MONTH(s.DT_ATUALIZACAO), 1)
),
baixas AS (
    SELECT
        DATEFROMPARTS(YEAR(b.DT_ATUALIZACAO), MONTH(b.DT_ATUALIZACAO), 1) AS PRIMEIRO_DIA,
        COUNT_BIG(*)                                                     AS QTD
    FROM dbo.SERASA_PEFIN_BAIXAS AS b
    WHERE b.STATUS = 'BAIXADO_SUCESSO'
      AND b.DT_ATUALIZACAO >= DATEADD(MONTH, -12, CAST(SYSUTCDATETIME() AS date))
    GROUP BY DATEFROMPARTS(YEAR(b.DT_ATUALIZACAO), MONTH(b.DT_ATUALIZACAO), 1)
)
SELECT
    CONVERT(char(7), m.PRIMEIRO_DIA, 126)        AS ANO_MES,           -- formato YYYY-MM
    ISNULL(n.QTD, 0)                             AS QTD_NEGATIVACOES,
    ISNULL(b.QTD, 0)                             AS QTD_BAIXAS
FROM meses AS m
LEFT JOIN negativacoes AS n
       ON n.PRIMEIRO_DIA = m.PRIMEIRO_DIA
LEFT JOIN baixas AS b
       ON b.PRIMEIRO_DIA = m.PRIMEIRO_DIA;
GO

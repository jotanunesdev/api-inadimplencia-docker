-- =============================================================================
-- 013_sync_negativado_retroativo.sql
--
-- Sincronização retroativa da coluna DW.fat_analise_inadimplencia_parcelas.NEGATIVADO
-- a partir do estado atual de dbo.SERASA_PEFIN_SOLICITACOES e dbo.SERASA_PEFIN_BAIXAS.
--
-- Regra:
--   - NEGATIVADO = 'SIM' quando existir SOLICITACAO (TIPO_REGISTRO = 'PRINCIPAL')
--     com STATUS = 'NEGATIVADO_SUCESSO' SEM baixa correspondente concluída
--     (BAIXADO_SUCESSO).
--   - NEGATIVADO = 'NAO' quando existir baixa BAIXADO_SUCESSO ou quando nunca
--     houve negativação concluída.
--
-- Chave de junção: NUM_VENDA + DATA_VENCIMENTO (decisão arquitetural; ver
-- documentos/techspec-codebase.md).
--
-- IDEMPOTENTE — pode ser executado quantas vezes quiser. Faz UPDATE somente onde
-- o valor atual diverge do esperado.
--
-- USO RECOMENDADO:
--   1. BACKUP da tabela DW.fat_analise_inadimplencia_parcelas antes (snapshot).
--   2. Executar o SELECT de pré-visualização (seção 1) e conferir os números.
--   3. Executar o UPDATE (seção 2).
--   4. Conferir o relatório final (seção 3).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- -----------------------------------------------------------------------------
-- 0) Validações de schema (falha cedo se algo estiver fora do esperado)
-- -----------------------------------------------------------------------------
IF OBJECT_ID('DW.fat_analise_inadimplencia_parcelas', 'U') IS NULL
BEGIN
    RAISERROR('Tabela DW.fat_analise_inadimplencia_parcelas não encontrada.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NULL
BEGIN
    RAISERROR('Tabela dbo.SERASA_PEFIN_SOLICITACOES não encontrada.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.SERASA_PEFIN_BAIXAS', 'U') IS NULL
BEGIN
    RAISERROR('Tabela dbo.SERASA_PEFIN_BAIXAS não encontrada.', 16, 1);
    RETURN;
END;
GO

-- -----------------------------------------------------------------------------
-- 1) PRÉ-VISUALIZAÇÃO — execute este bloco isoladamente para conferir
-- -----------------------------------------------------------------------------
PRINT '--- PRÉ-VISUALIZAÇÃO (nenhuma alteração ainda) ---';

;WITH NegativacoesAtivas AS (
    -- Última solicitação PRINCIPAL por (NUM_VENDA_FK, DATA_VENCIMENTO).
    SELECT
        s.NUM_VENDA_FK,
        s.DATA_VENCIMENTO,
        s.STATUS,
        s.DT_ATUALIZACAO,
        ROW_NUMBER() OVER (
            PARTITION BY s.NUM_VENDA_FK, s.DATA_VENCIMENTO
            ORDER BY s.DT_ATUALIZACAO DESC, s.DT_CRIACAO DESC
        ) AS rn
    FROM dbo.SERASA_PEFIN_SOLICITACOES s
    WHERE s.TIPO_REGISTRO = 'PRINCIPAL'
),
NegativadasFinal AS (
    -- Considera "negativada" apenas se a última solicitação PRINCIPAL é
    -- NEGATIVADO_SUCESSO E não há baixa concluída posterior na mesma chave.
    SELECT n.NUM_VENDA_FK, n.DATA_VENCIMENTO
    FROM NegativacoesAtivas n
    WHERE n.rn = 1
      AND n.STATUS = 'NEGATIVADO_SUCESSO'
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.SERASA_PEFIN_BAIXAS b
          INNER JOIN dbo.SERASA_PEFIN_SOLICITACOES sp
              ON sp.ID = b.ID_SOLICITACAO_NEGATIVACAO
          WHERE b.STATUS = 'BAIXADO_SUCESSO'
            AND sp.NUM_VENDA_FK = n.NUM_VENDA_FK
            AND sp.DATA_VENCIMENTO = n.DATA_VENCIMENTO
      )
)
SELECT
    [Status]               = CASE WHEN nf.NUM_VENDA_FK IS NOT NULL THEN 'SIM' ELSE 'NAO' END,
    [Atual_NEGATIVADO]     = COALESCE(LTRIM(RTRIM(p.NEGATIVADO)), 'NULL'),
    [Diverge]              = CASE
        WHEN nf.NUM_VENDA_FK IS NOT NULL
             AND UPPER(LTRIM(RTRIM(COALESCE(p.NEGATIVADO, '')))) <> 'SIM' THEN 'X'
        WHEN nf.NUM_VENDA_FK IS NULL
             AND UPPER(LTRIM(RTRIM(COALESCE(p.NEGATIVADO, '')))) = 'SIM' THEN 'X'
        ELSE ''
    END,
    [Qtd]                  = COUNT_BIG(*)
FROM DW.fat_analise_inadimplencia_parcelas p
LEFT JOIN NegativadasFinal nf
       ON nf.NUM_VENDA_FK = p.NUM_VENDA
      AND nf.DATA_VENCIMENTO = CAST(p.DATAVENCIMENTO AS date)
GROUP BY
    CASE WHEN nf.NUM_VENDA_FK IS NOT NULL THEN 'SIM' ELSE 'NAO' END,
    COALESCE(LTRIM(RTRIM(p.NEGATIVADO)), 'NULL'),
    CASE
        WHEN nf.NUM_VENDA_FK IS NOT NULL
             AND UPPER(LTRIM(RTRIM(COALESCE(p.NEGATIVADO, '')))) <> 'SIM' THEN 'X'
        WHEN nf.NUM_VENDA_FK IS NULL
             AND UPPER(LTRIM(RTRIM(COALESCE(p.NEGATIVADO, '')))) = 'SIM' THEN 'X'
        ELSE ''
    END
ORDER BY 1, 2;
GO

-- -----------------------------------------------------------------------------
-- 2) UPDATE — execute em transação. Faz UPDATE somente onde diverge.
-- -----------------------------------------------------------------------------
PRINT '--- APLICANDO UPDATES ---';

BEGIN TRANSACTION;

;WITH NegativacoesAtivas AS (
    SELECT
        s.NUM_VENDA_FK,
        s.DATA_VENCIMENTO,
        s.STATUS,
        ROW_NUMBER() OVER (
            PARTITION BY s.NUM_VENDA_FK, s.DATA_VENCIMENTO
            ORDER BY s.DT_ATUALIZACAO DESC, s.DT_CRIACAO DESC
        ) AS rn
    FROM dbo.SERASA_PEFIN_SOLICITACOES s
    WHERE s.TIPO_REGISTRO = 'PRINCIPAL'
),
NegativadasFinal AS (
    SELECT n.NUM_VENDA_FK, n.DATA_VENCIMENTO
    FROM NegativacoesAtivas n
    WHERE n.rn = 1
      AND n.STATUS = 'NEGATIVADO_SUCESSO'
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.SERASA_PEFIN_BAIXAS b
          INNER JOIN dbo.SERASA_PEFIN_SOLICITACOES sp
              ON sp.ID = b.ID_SOLICITACAO_NEGATIVACAO
          WHERE b.STATUS = 'BAIXADO_SUCESSO'
            AND sp.NUM_VENDA_FK = n.NUM_VENDA_FK
            AND sp.DATA_VENCIMENTO = n.DATA_VENCIMENTO
      )
)
UPDATE p
SET p.NEGATIVADO = CASE
                       WHEN nf.NUM_VENDA_FK IS NOT NULL THEN 'SIM'
                       ELSE 'NAO'
                   END
FROM DW.fat_analise_inadimplencia_parcelas p
LEFT JOIN NegativadasFinal nf
       ON nf.NUM_VENDA_FK = p.NUM_VENDA
      AND nf.DATA_VENCIMENTO = CAST(p.DATAVENCIMENTO AS date)
WHERE
    -- Só atualiza linhas que divergem do estado correto.
    (
        nf.NUM_VENDA_FK IS NOT NULL
        AND UPPER(LTRIM(RTRIM(COALESCE(p.NEGATIVADO, '')))) <> 'SIM'
    )
    OR
    (
        nf.NUM_VENDA_FK IS NULL
        AND UPPER(LTRIM(RTRIM(COALESCE(p.NEGATIVADO, '')))) <> 'NAO'
    );

DECLARE @rows_atualizadas int = @@ROWCOUNT;
PRINT CONCAT('Linhas atualizadas: ', @rows_atualizadas);

COMMIT TRANSACTION;
GO

-- -----------------------------------------------------------------------------
-- 3) RELATÓRIO FINAL — distribuição pós-update
-- -----------------------------------------------------------------------------
PRINT '--- DISTRIBUIÇÃO PÓS-UPDATE ---';

SELECT
    NEGATIVADO     = COALESCE(LTRIM(RTRIM(NEGATIVADO)), 'NULL'),
    Qtd            = COUNT_BIG(*)
FROM DW.fat_analise_inadimplencia_parcelas
GROUP BY COALESCE(LTRIM(RTRIM(NEGATIVADO)), 'NULL')
ORDER BY 1;
GO

-- =============================================================================
-- 2026-05-20 - INAD_NOTIFICACOES - Adiciona coluna DEDUPE_KEY
--
-- A entidade EF InadNotificacao mapeia a propriedade DedupeKey para a coluna
-- DEDUPE_KEY (snake_case maiusculo, padrao da tabela legada). A tabela legada
-- nao possuia essa coluna e qualquer SELECT/INSERT envolvendo InadNotificacao
-- falhava com "Invalid column name 'DEDUPE_KEY'", inclusive o snapshot inicial
-- do endpoint SSE /inadimplencia/notifications/stream.
--
-- Este script eh idempotente: pode ser reexecutado sem efeitos colaterais.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE name = 'DEDUPE_KEY'
      AND object_id = OBJECT_ID('dbo.INAD_NOTIFICACOES')
)
BEGIN
    ALTER TABLE dbo.INAD_NOTIFICACOES
        ADD DEDUPE_KEY NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_INAD_NOTIFICACOES_DedupeKey'
      AND object_id = OBJECT_ID('dbo.INAD_NOTIFICACOES')
)
BEGIN
    CREATE INDEX IX_INAD_NOTIFICACOES_DedupeKey
        ON dbo.INAD_NOTIFICACOES (DEDUPE_KEY)
        WHERE DEDUPE_KEY IS NOT NULL;
END
GO

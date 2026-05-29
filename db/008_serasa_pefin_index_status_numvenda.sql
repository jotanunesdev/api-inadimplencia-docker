-- =============================================================================
-- 2026-05-19 - Serasa PEFIN - Index para ListByStatusAsync
-- Script idempotente para criar índice de suporte a filtros por status e numVenda.
-- Nao executar diretamente em producao; aplicar de forma controlada em dev/UAT.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_SerasaPefinSolicitacoes_Status_NumVenda'
      AND object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SerasaPefinSolicitacoes_Status_NumVenda
        ON dbo.SERASA_PEFIN_SOLICITACOES (STATUS, NUM_VENDA_FK)
        INCLUDE (DT_CRIACAO, SOLICITANTE_USERNAME, CONTRACT_NUMBER, VALOR);
END
GO

-- =============================================================================
-- 2026-05-19 - Envio Individual por Parcela ao Serasa
-- Script idempotente para adicionar colunas de parcela e atualizar index unico
-- para suportar 1 registro por parcela.
-- Nao executar diretamente em producao; aplicar de forma controlada em dev/UAT.
-- =============================================================================

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Adicionar coluna NumeroParcela se não existir
    IF COL_LENGTH('dbo.SERASA_PEFIN_SOLICITACOES', 'NUMERO_PARCELA') IS NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD NUMERO_PARCELA INT NULL;
    END

    -- Adicionar coluna ParcelaIdOrigem se não existir
    IF COL_LENGTH('dbo.SERASA_PEFIN_SOLICITACOES', 'PARCELA_ID_ORIGEM') IS NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD PARCELA_ID_ORIGEM NVARCHAR(64) NULL;
    END

    -- Adicionar coluna IdSolicitacaoPai se não existir
    IF COL_LENGTH('dbo.SERASA_PEFIN_SOLICITACOES', 'ID_SOLICITACAO_PAI') IS NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD ID_SOLICITACAO_PAI UNIQUEIDENTIFIER NULL;
    END
END
GO

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Drop index existente para recriar com suporte a parcela
    IF OBJECT_ID('dbo.UX_SERASA_PEFIN_SOLICITACOES_ATIVA', 'U') IS NOT NULL
    BEGIN
        DROP INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA ON dbo.SERASA_PEFIN_SOLICITACOES;
    END
END
GO

SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Criar index unico filtrado para registros com parcela (novos registros)
    -- Este index previne duplicacao por (numVenda, contractNumber, documentoDevedor, 
    -- documentoGarantidor, tipoRegistro, numeroParcela) para registros ativos
    CREATE UNIQUE NONCLUSTERED INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA
        ON dbo.SERASA_PEFIN_SOLICITACOES (
            NUM_VENDA_FK,
            CONTRACT_NUMBER,
            DOCUMENTO_DEVEDOR,
            DOCUMENTO_GARANTIDOR,
            TIPO_REGISTRO,
            NUMERO_PARCELA
        )
        WHERE NUMERO_PARCELA IS NOT NULL
          AND STATUS IN (
              'AGUARDANDO_APROVACAO',
              'APROVADA',
              'PENDENTE_ENVIO',
              'ENVIADO_SERASA',
              'AGUARDANDO_RETORNO'
          );
END
GO

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Criar index legado para registros antigos (sem parcela)
    -- Mantem a semantica original para compatibilidade retroativa
    -- Inclui ID como tiebreaker para lidar com duplicados existentes nos dados
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UX_SERASA_PEFIN_SOLICITACOES_ATIVA_LEGADA'
          AND object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
    )
    BEGIN
        SET QUOTED_IDENTIFIER ON;
        CREATE UNIQUE NONCLUSTERED INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA_LEGADA
            ON dbo.SERASA_PEFIN_SOLICITACOES (
                NUM_VENDA_FK,
                CONTRACT_NUMBER,
                DOCUMENTO_DEVEDOR,
                DOCUMENTO_GARANTIDOR,
                TIPO_REGISTRO,
                ID
            )
            WHERE NUMERO_PARCELA IS NULL
              AND STATUS IN (
                  'AGUARDANDO_APROVACAO',
                  'APROVADA',
                  'PENDENTE_ENVIO',
                  'ENVIADO_SERASA',
                  'AGUARDANDO_RETORNO'
              );
    END
END
GO

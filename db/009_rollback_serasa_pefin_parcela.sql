-- =============================================================================
-- ROLLBACK - 2026-05-19 - Envio Individual por Parcela ao Serasa
-- Script para reverter migration 009_serasa_pefin_parcela.sql
-- ATENÇÃO: Este script deve ser executado apenas em caso de rollback planejado.
-- =============================================================================
 
IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Remover index legado se existir
    IF OBJECT_ID('dbo.UX_SERASA_PEFIN_SOLICITACOES_ATIVA_LEGADA', 'U') IS NOT NULL
    BEGIN
        DROP INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA_LEGADA ON dbo.SERASA_PEFIN_SOLICITACOES;
    END
END
GO

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Remover index atualizado se existir
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
    -- Recriar index original (sem NumeroParcela)
    CREATE UNIQUE NONCLUSTERED INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA
        ON dbo.SERASA_PEFIN_SOLICITACOES (
            NUM_VENDA_FK,
            CONTRACT_NUMBER,
            DOCUMENTO_DEVEDOR,
            DOCUMENTO_GARANTIDOR,
            TIPO_REGISTRO
        )
        WHERE STATUS IN (
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
    -- Remover coluna IdSolicitacaoPai se existir
    IF COL_LENGTH('dbo.SERASA_PEFIN_SOLICITACOES', 'ID_SOLICITACAO_PAI') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        DROP COLUMN ID_SOLICITACAO_PAI;
    END

    -- Remover coluna ParcelaIdOrigem se existir
    IF COL_LENGTH('dbo.SERASA_PEFIN_SOLICITACOES', 'PARCELA_ID_ORIGEM') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        DROP COLUMN PARCELA_ID_ORIGEM;
    END

    -- Remover coluna NumeroParcela se existir
    IF COL_LENGTH('dbo.SERASA_PEFIN_SOLICITACOES', 'NUMERO_PARCELA') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        DROP COLUMN NUMERO_PARCELA;
    END
END
GO

-- =============================================================================
-- NOTAS DE ROLLBACK:
-- 1. Este script remove as colunas adicionadas e restaura o index original
-- 2. Dados existentes nas colunas removidas serão PERDIDOS
-- 3. Certifique-se de que não há registros com NumeroParcela preenchido antes do rollback
-- 4. Execute backup completo do banco antes do rollback
-- =============================================================================

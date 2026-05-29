-- =============================================================================
-- 2026-05-14 - Fluxo de Negativação Serasa
-- Script idempotente para estender SERASA_PEFIN_SOLICITACOES com novos status
-- e campos de fluxo de aprovação.
-- Nao executar diretamente em producao; aplicar de forma controlada em dev/UAT.
-- =============================================================================

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Drop constraint de status existente para recriar com novos valores
    IF OBJECT_ID('dbo.CK_SERASA_PEFIN_SOLICITACOES_STATUS', 'C') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        DROP CONSTRAINT CK_SERASA_PEFIN_SOLICITACOES_STATUS;
    END

    -- Recriar constraint com novos status de fluxo de aprovação
    ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES WITH CHECK
    ADD CONSTRAINT CK_SERASA_PEFIN_SOLICITACOES_STATUS
        CHECK (STATUS IN (
            'AGUARDANDO_APROVACAO',
            'APROVADA',
            'REJEITADA',
            'APROVADA_FALHA_ENVIO',
            'PENDENTE_ENVIO',
            'ENVIADO_SERASA',
            'AGUARDANDO_RETORNO',
            'NEGATIVADO_SUCESSO',
            'NEGATIVADO_ERRO',
            'BAIXA_ENVIADA',
            'BAIXA_AGUARDANDO_RETORNO',
            'BAIXADO_SUCESSO',
            'BAIXADO_ERRO'
        ));
END
GO

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Adicionar colunas de fluxo de aprovação se não existirem
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
          AND name = 'SOLICITANTE_USERNAME'
    )
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD SOLICITANTE_USERNAME varchar(100) NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
          AND name = 'APROVADOR_USERNAME'
    )
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD APROVADOR_USERNAME varchar(100) NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
          AND name = 'DT_APROVACAO'
    )
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD DT_APROVACAO datetime2 NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES')
          AND name = 'JUSTIFICATIVA'
    )
    BEGIN
        ALTER TABLE dbo.SERASA_PEFIN_SOLICITACOES
        ADD JUSTIFICATIVA nvarchar(500) NULL;
    END
END
GO

IF OBJECT_ID('dbo.SERASA_PEFIN_SOLICITACOES', 'U') IS NOT NULL
BEGIN
    -- Drop e recriar índice único filtrado para incluir AGUARDANDO_APROVACAO e APROVADA
    IF OBJECT_ID('dbo.UX_SERASA_PEFIN_SOLICITACOES_ATIVA', 'U') IS NOT NULL
    BEGIN
        DROP INDEX UX_SERASA_PEFIN_SOLICITACOES_ATIVA ON dbo.SERASA_PEFIN_SOLICITACOES;
    END

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

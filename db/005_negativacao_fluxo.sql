-- =============================================================================
-- 2026-05-14 - Fluxo de Negativação Serasa
-- Script idempotente para criar tabela USUARIO_SENHA_TRANSACAO para
-- armazenamento de hash de senha de transação por usuário.
-- Nao executar diretamente em producao; aplicar de forma controlada em dev/UAT.
-- =============================================================================

IF OBJECT_ID('dbo.USUARIO_SENHA_TRANSACAO', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.USUARIO_SENHA_TRANSACAO (
        USERNAME varchar(100) NOT NULL PRIMARY KEY,
        HASH nvarchar(500) NOT NULL,
        TENTATIVAS_FALHAS int NOT NULL
            CONSTRAINT DF_USUARIO_SENHA_TRANSACAO_TENTATIVAS_FALHAS DEFAULT (0),
        BLOQUEADO_ATE datetime2 NULL,
        CRIADA_EM datetime2 NOT NULL
            CONSTRAINT DF_USUARIO_SENHA_TRANSACAO_CRIADA_EM DEFAULT (SYSUTCDATETIME()),
        ATUALIZADA_EM datetime2 NOT NULL
            CONSTRAINT DF_USUARIO_SENHA_TRANSACAO_ATUALIZADA_EM DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID('dbo.USUARIO_SENHA_TRANSACAO', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_USUARIO_SENHA_TRANSACAO_BLOQUEADO_ATE'
          AND object_id = OBJECT_ID('dbo.USUARIO_SENHA_TRANSACAO')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_USUARIO_SENHA_TRANSACAO_BLOQUEADO_ATE
            ON dbo.USUARIO_SENHA_TRANSACAO (BLOQUEADO_ATE)
            WHERE BLOQUEADO_ATE IS NOT NULL;
    END
END
GO

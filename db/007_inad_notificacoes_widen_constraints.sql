/* -----------------------------------------------------------------------------
   007 - Widen CHECK constraints on dbo.INAD_NOTIFICACOES to accept the new
         notification types emitted by the negativacao approval workflow.

   Original constraints only allowed 'VENDA_ATRIBUIDA' and 'VENDA_ATRASADA',
   blocking SOLICITACAO_NEGATIVACAO / APROVACAO_NEGATIVACAO / REJEICAO_NEGATIVACAO
   / RETORNO_SERASA_SUCESSO / RETORNO_SERASA_ERRO and silently breaking
   notification dispatch via the EF-backed NotificationDispatcher.

   The ORIGEM_USUARIO presence rule is preserved for the legacy types and
   relaxed (no requirement) for the new negativacao types, since they do not
   carry a separate "origin user".
   ----------------------------------------------------------------------------- */

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_NOTIFICACOES_TIPO')
    ALTER TABLE dbo.INAD_NOTIFICACOES DROP CONSTRAINT CK_NOTIFICACOES_TIPO;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_NOTIFICACOES_ORIGEM')
    ALTER TABLE dbo.INAD_NOTIFICACOES DROP CONSTRAINT CK_NOTIFICACOES_ORIGEM;
GO

ALTER TABLE dbo.INAD_NOTIFICACOES
    ADD CONSTRAINT CK_NOTIFICACOES_TIPO CHECK (
        TIPO IN (
            'VENDA_ATRIBUIDA',
            'VENDA_ATRASADA',
            'SOLICITACAO_NEGATIVACAO',
            'APROVACAO_NEGATIVACAO',
            'REJEICAO_NEGATIVACAO',
            'RETORNO_SERASA_SUCESSO',
            'RETORNO_SERASA_ERRO'
        )
    );
GO

ALTER TABLE dbo.INAD_NOTIFICACOES
    ADD CONSTRAINT CK_NOTIFICACOES_ORIGEM CHECK (
        (TIPO = 'VENDA_ATRIBUIDA' AND ORIGEM_USUARIO IS NOT NULL)
        OR (TIPO = 'VENDA_ATRASADA' AND ORIGEM_USUARIO IS NULL)
        OR TIPO IN (
            'SOLICITACAO_NEGATIVACAO',
            'APROVACAO_NEGATIVACAO',
            'REJEICAO_NEGATIVACAO',
            'RETORNO_SERASA_SUCESSO',
            'RETORNO_SERASA_ERRO'
        )
    );
GO

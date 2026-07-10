/* =============================================================================
   016 - Corrige notificacoes legadas destinadas ao username digitado sem "n".

   A configuracao antiga gravou "aracy.mendoca". O username oficial do Fluig e
   "aracy.mendonca", portanto essas notificacoes nao aparecem na consulta da
   usuaria. O script e idempotente e evita conflito quando a notificacao correta
   para a mesma chave de deduplicacao ja existe.
   ============================================================================= */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE notificacao
SET USUARIO_DESTINATARIO = 'aracy.mendonca'
FROM dbo.INAD_NOTIFICACOES AS notificacao
WHERE LOWER(LTRIM(RTRIM(notificacao.USUARIO_DESTINATARIO))) = 'aracy.mendoca'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.INAD_NOTIFICACOES AS correta
      WHERE LOWER(LTRIM(RTRIM(correta.USUARIO_DESTINATARIO))) = 'aracy.mendonca'
        AND correta.TIPO = notificacao.TIPO
        AND correta.NUM_VENDA = notificacao.NUM_VENDA
        AND (
            correta.DEDUPE_KEY = notificacao.DEDUPE_KEY
            OR (correta.DEDUPE_KEY IS NULL AND notificacao.DEDUPE_KEY IS NULL)
        )
  );

COMMIT TRANSACTION;
GO

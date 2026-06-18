/*
  Índices sugeridos com base nos gargalos observados nas rotas de leitura e nos
  testes de carga. Revise em homologação antes de aplicar em produção.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_OCORRENCIAS_NumVenda_ProximaAcao_DataHora'
      AND object_id = OBJECT_ID('dbo.OCORRENCIAS')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_OCORRENCIAS_NumVenda_ProximaAcao_DataHora
        ON dbo.OCORRENCIAS (NUM_VENDA_FK, PROXIMA_ACAO DESC, DT_OCORRENCIA DESC, HORA_OCORRENCIA DESC)
        INCLUDE (NOME_USUARIO_FK);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_VENDA_RESPONSAVEL_NomeUsuario_NumVenda'
      AND object_id = OBJECT_ID('dbo.VENDA_RESPONSAVEL')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_VENDA_RESPONSAVEL_NomeUsuario_NumVenda
        ON dbo.VENDA_RESPONSAVEL (NOME_USUARIO_FK, NUM_VENDA_FK)
        INCLUDE (DT_ATRIBUICAO);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_VENDA_RESPONSAVEL_NumVenda'
      AND object_id = OBJECT_ID('dbo.VENDA_RESPONSAVEL')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_VENDA_RESPONSAVEL_NumVenda
        ON dbo.VENDA_RESPONSAVEL (NUM_VENDA_FK)
        INCLUDE (NOME_USUARIO_FK, DT_ATRIBUICAO);
END
GO

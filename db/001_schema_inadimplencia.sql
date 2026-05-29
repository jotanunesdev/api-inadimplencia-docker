/* =============================================================================
   api-inadimplencia - Schema SQL Server (dbo)
   Banco alvo (vide .env): dwjnc em 192.168.79.240\bi,10433

   Execução sugerida:
     sqlcmd -S "192.168.79.240\bi,10433" -d dwjnc -U fluig -P "fluig@2019" \
            -C -i db\001_schema_inadimplencia.sql

   Todas as criações são idempotentes (IF NOT EXISTS).
   Tipos baseados em ApiInadimplencia.Infrastructure.Persistence.SqlServer.InadimplenciaDbContext.
   ============================================================================= */

USE [dwjnc];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   1) dbo.OCORRENCIAS
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.OCORRENCIAS'))
BEGIN
    CREATE TABLE dbo.OCORRENCIAS
    (
        Id                UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_OCORRENCIAS_Id DEFAULT (NEWID()),
        NumVendaFk        INT               NOT NULL,
        NomeUsuarioFk     NVARCHAR(100)     NOT NULL,
        Descricao         NVARCHAR(1000)    NOT NULL,
        StatusOcorrencia  NVARCHAR(50)      NOT NULL,
        DtOcorrencia      DATETIME2(7)      NOT NULL,
        HoraOcorrencia    NVARCHAR(10)      NOT NULL,
        ProximaAcao       NVARCHAR(500)     NULL,
        Protocolo         NVARCHAR(50)      NULL,
        CONSTRAINT PK_OCORRENCIAS PRIMARY KEY CLUSTERED (Id)
    );
    CREATE INDEX IX_OCORRENCIAS_NumVendaFk   ON dbo.OCORRENCIAS (NumVendaFk);
    CREATE INDEX IX_OCORRENCIAS_Protocolo    ON dbo.OCORRENCIAS (Protocolo);
END
GO

/* -----------------------------------------------------------------------------
   2) dbo.ATENDIMENTOS
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.ATENDIMENTOS'))
BEGIN
    CREATE TABLE dbo.ATENDIMENTOS
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ATENDIMENTOS_Id DEFAULT (NEWID()),
        Protocolo       NVARCHAR(13)     NOT NULL,
        Cpf             NVARCHAR(14)     NOT NULL,
        NumVendaFk      INT              NOT NULL,
        DadosVendaJson  NVARCHAR(MAX)    NOT NULL,
        CriadoEm        DATETIME2(7)     NOT NULL,
        CONSTRAINT PK_ATENDIMENTOS PRIMARY KEY CLUSTERED (Id)
    );
    CREATE UNIQUE INDEX UX_ATENDIMENTOS_Protocolo ON dbo.ATENDIMENTOS (Protocolo);
    CREATE        INDEX IX_ATENDIMENTOS_Cpf        ON dbo.ATENDIMENTOS (Cpf);
    CREATE        INDEX IX_ATENDIMENTOS_NumVendaFk ON dbo.ATENDIMENTOS (NumVendaFk);
END
GO

/* -----------------------------------------------------------------------------
   3) dbo.USUARIO
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.USUARIO'))
BEGIN
    CREATE TABLE dbo.USUARIO
    (
        Id        INT              IDENTITY(1,1) NOT NULL,
        UserCode  NVARCHAR(50)     NOT NULL,
        Nome      NVARCHAR(200)    NOT NULL,
        Perfil    INT              NOT NULL,           -- enum UserProfile (0,1,...)
        CorHex    NVARCHAR(7)      NOT NULL,           -- ex.: #A1B2C3
        CriadoEm  DATETIME2(7)     NOT NULL,
        CONSTRAINT PK_USUARIO PRIMARY KEY CLUSTERED (Id)
    );
    CREATE UNIQUE INDEX UX_USUARIO_UserCode ON dbo.USUARIO (UserCode);
    CREATE        INDEX IX_USUARIO_Nome     ON dbo.USUARIO (Nome);
END
GO

/* -----------------------------------------------------------------------------
   4) dbo.VENDA_RESPONSAVEL
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.VENDA_RESPONSAVEL'))
BEGIN
    CREATE TABLE dbo.VENDA_RESPONSAVEL
    (
        NumVendaFk    INT              NOT NULL,
        Username      NVARCHAR(100)    NOT NULL,
        AtribuidoEm   DATETIME2(7)     NOT NULL,
        AtribuidoPor  NVARCHAR(100)    NOT NULL,
        CONSTRAINT PK_VENDA_RESPONSAVEL PRIMARY KEY CLUSTERED (NumVendaFk)
    );
    CREATE INDEX IX_VENDA_RESPONSAVEL_Username ON dbo.VENDA_RESPONSAVEL (Username);
END
GO

/* -----------------------------------------------------------------------------
   5) dbo.KANBAN_STATUS
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.KANBAN_STATUS'))
BEGIN
    CREATE TABLE dbo.KANBAN_STATUS
    (
        NumVendaFk   INT            NOT NULL,
        ProximaAcao  NVARCHAR(500)  NOT NULL,
        Status       INT            NOT NULL,          -- enum KanbanStatus (Todo/InProgress/Done)
        StatusData   DATE           NOT NULL,
        CONSTRAINT PK_KANBAN_STATUS PRIMARY KEY CLUSTERED (NumVendaFk, ProximaAcao)
    );
END
GO

/* -----------------------------------------------------------------------------
   6) dbo.INAD_NOTIFICACOES
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.INAD_NOTIFICACOES'))
BEGIN
    CREATE TABLE dbo.INAD_NOTIFICACOES
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INAD_NOTIFICACOES_Id DEFAULT (NEWID()),
        Tipo            INT              NOT NULL,     -- enum NotificationType
        Usuario         NVARCHAR(100)    NOT NULL,
        NumVenda        INT              NOT NULL,
        ProximaAcaoDia  DATE             NULL,
        Mensagem        NVARCHAR(500)    NOT NULL,
        DedupeKey       NVARCHAR(100)    NULL,
        Lida            BIT              NOT NULL CONSTRAINT DF_INAD_NOTIFICACOES_Lida DEFAULT (0),
        CriadaEm        DATETIME2(7)     NOT NULL,
        ExcluidaEm      DATETIME2(7)     NULL,
        CONSTRAINT PK_INAD_NOTIFICACOES PRIMARY KEY CLUSTERED (Id)
    );

    /* Índice único respeitando NULL em ProximaAcaoDia (filtro evita violação por múltiplos NULL). */
    CREATE UNIQUE INDEX UX_INAD_NOTIFICACOES_Dedup
        ON dbo.INAD_NOTIFICACOES (Tipo, Usuario, NumVenda, ProximaAcaoDia)
        WHERE ProximaAcaoDia IS NOT NULL;

    CREATE INDEX IX_INAD_NOTIFICACOES_DedupeKey
        ON dbo.INAD_NOTIFICACOES (DedupeKey)
        WHERE DedupeKey IS NOT NULL;

    CREATE INDEX IX_INAD_NOTIFICACOES_Usuario ON dbo.INAD_NOTIFICACOES (Usuario, Lida, ExcluidaEm);
END
GO

/* -----------------------------------------------------------------------------
   7) dbo.SERASA_PEFIN_SOLICITACOES
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.SERASA_PEFIN_SOLICITACOES'))
BEGIN
    CREATE TABLE dbo.SERASA_PEFIN_SOLICITACOES
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SERASA_PEFIN_SOLICITACOES_Id DEFAULT (NEWID()),
        NumVendaFk     INT              NOT NULL,
        TipoRegistro   INT              NOT NULL,      -- enum SerasaPefinRecordType
        TransactionId  NVARCHAR(100)    NULL,
        Status         INT              NOT NULL,      -- enum SerasaPefinStatus
        PayloadJson    NVARCHAR(MAX)    NOT NULL,
        RespostaJson   NVARCHAR(MAX)    NULL,
        ErrorMessage   NVARCHAR(1000)   NULL,
        CriadoEm       DATETIME2(7)     NOT NULL,
        EnviadoEm      DATETIME2(7)     NULL,
        CompletadoEm   DATETIME2(7)     NULL,
        CONSTRAINT PK_SERASA_PEFIN_SOLICITACOES PRIMARY KEY CLUSTERED (Id)
    );
    CREATE INDEX IX_SERASA_PEFIN_SOLICITACOES_TransactionId ON dbo.SERASA_PEFIN_SOLICITACOES (TransactionId);
    CREATE INDEX IX_SERASA_PEFIN_SOLICITACOES_NumVendaFk    ON dbo.SERASA_PEFIN_SOLICITACOES (NumVendaFk);
END
GO

/* -----------------------------------------------------------------------------
   8) dbo.SERASA_PEFIN_WEBHOOKS
   ----------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.SERASA_PEFIN_WEBHOOKS'))
BEGIN
    CREATE TABLE dbo.SERASA_PEFIN_WEBHOOKS
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SERASA_PEFIN_WEBHOOKS_Id DEFAULT (NEWID()),
        Uuid           NVARCHAR(100)    NOT NULL,
        EventType      NVARCHAR(50)     NOT NULL,
        Resultado      NVARCHAR(50)     NOT NULL,
        TransactionId  NVARCHAR(100)    NOT NULL,
        PayloadJson    NVARCHAR(MAX)    NOT NULL,
        RecebidoEm     DATETIME2(7)     NOT NULL,
        CONSTRAINT PK_SERASA_PEFIN_WEBHOOKS PRIMARY KEY CLUSTERED (Id)
    );
    CREATE UNIQUE INDEX UX_SERASA_PEFIN_WEBHOOKS_Uuid          ON dbo.SERASA_PEFIN_WEBHOOKS (Uuid);
    CREATE        INDEX IX_SERASA_PEFIN_WEBHOOKS_TransactionId ON dbo.SERASA_PEFIN_WEBHOOKS (TransactionId);
END
GO

PRINT '>>> Schema do modulo inadimplencia aplicado com sucesso.';
GO

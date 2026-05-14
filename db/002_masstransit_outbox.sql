/* =============================================================================
   api-inadimplencia - Tabelas opcionais do MassTransit EF Core Outbox/Inbox
   Só execute se for reativar AddEntityFrameworkOutbox<InadimplenciaDbContext>
   em ApiInadimplencia.Infrastructure.DependencyInjection.

   Baseado em MassTransit.EntityFrameworkCoreIntegration (v8+) - schema dbo.
   Tipos validados contra as entidades InboxState / OutboxMessage / OutboxState.
   ============================================================================= */

USE [dwjnc];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -------------------- InboxState -------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.InboxState'))
BEGIN
    CREATE TABLE dbo.InboxState
    (
        Id                     BIGINT           IDENTITY(1,1) NOT NULL,
        MessageId              UNIQUEIDENTIFIER NOT NULL,
        ConsumerId             UNIQUEIDENTIFIER NOT NULL,
        LockId                 UNIQUEIDENTIFIER NOT NULL,
        RowVersion             ROWVERSION       NOT NULL,
        Received               DATETIME2(7)     NOT NULL,
        ReceiveCount           INT              NOT NULL,
        ExpirationTime         DATETIME2(7)     NULL,
        Consumed               DATETIME2(7)     NULL,
        Delivered              DATETIME2(7)     NULL,
        LastSequenceNumber     BIGINT           NULL,
        CONSTRAINT PK_InboxState PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT AK_InboxState_MessageId_ConsumerId UNIQUE (MessageId, ConsumerId)
    );
    CREATE INDEX IX_InboxState_Delivered ON dbo.InboxState (Delivered);
END
GO

/* -------------------- OutboxMessage -------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.OutboxMessage'))
BEGIN
    CREATE TABLE dbo.OutboxMessage
    (
        SequenceNumber       BIGINT           IDENTITY(1,1) NOT NULL,
        EnqueueTime          DATETIME2(7)     NULL,
        SentTime             DATETIME2(7)     NOT NULL,
        Headers              NVARCHAR(MAX)    NULL,
        Properties           NVARCHAR(MAX)    NULL,
        InboxMessageId       UNIQUEIDENTIFIER NULL,
        InboxConsumerId      UNIQUEIDENTIFIER NULL,
        OutboxId             UNIQUEIDENTIFIER NULL,
        MessageId            UNIQUEIDENTIFIER NOT NULL,
        ContentType          NVARCHAR(256)    NOT NULL,
        MessageType          NVARCHAR(MAX)    NOT NULL,
        Body                 NVARCHAR(MAX)    NOT NULL,
        ConversationId       UNIQUEIDENTIFIER NULL,
        CorrelationId        UNIQUEIDENTIFIER NULL,
        InitiatorId          UNIQUEIDENTIFIER NULL,
        RequestId            UNIQUEIDENTIFIER NULL,
        SourceAddress        NVARCHAR(256)    NULL,
        DestinationAddress   NVARCHAR(256)    NULL,
        ResponseAddress      NVARCHAR(256)    NULL,
        FaultAddress         NVARCHAR(256)    NULL,
        ExpirationTime       DATETIME2(7)     NULL,
        CONSTRAINT PK_OutboxMessage PRIMARY KEY CLUSTERED (SequenceNumber)
    );
    CREATE INDEX IX_OutboxMessage_EnqueueTime                         ON dbo.OutboxMessage (EnqueueTime);
    CREATE INDEX IX_OutboxMessage_ExpirationTime                      ON dbo.OutboxMessage (ExpirationTime);
    CREATE INDEX IX_OutboxMessage_OutboxId_SequenceNumber             ON dbo.OutboxMessage (OutboxId, SequenceNumber);
    CREATE INDEX IX_OutboxMessage_Inbox_SequenceNumber                ON dbo.OutboxMessage (InboxMessageId, InboxConsumerId, SequenceNumber);
END
GO

/* -------------------- OutboxState -------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.OutboxState'))
BEGIN
    CREATE TABLE dbo.OutboxState
    (
        OutboxId             UNIQUEIDENTIFIER NOT NULL,
        LockId               UNIQUEIDENTIFIER NOT NULL,
        RowVersion           ROWVERSION       NOT NULL,
        Created              DATETIME2(7)     NOT NULL,
        Delivered            DATETIME2(7)     NULL,
        LastSequenceNumber   BIGINT           NULL,
        CONSTRAINT PK_OutboxState PRIMARY KEY CLUSTERED (OutboxId)
    );
    CREATE INDEX IX_OutboxState_Created ON dbo.OutboxState (Created);
END
GO

PRINT '>>> Tabelas InboxState/OutboxMessage/OutboxState criadas. Reative AddEntityFrameworkOutbox.';
GO

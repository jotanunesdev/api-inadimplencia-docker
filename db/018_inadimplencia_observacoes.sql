-- RJ-019: acompanhamento prospectivo. Revisar/aplicar SOMENTE em ambiente aprovado.
-- Este script não inicia captura, não agenda job e não altera as fontes DW.
-- Ausência no DW não é quitação. Não reconstruir datas anteriores à primeira captura.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.INAD_CAPTURA', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.INAD_CAPTURA (
        ID bigint IDENTITY PRIMARY KEY,
        ANTERIOR_ID bigint NULL REFERENCES dbo.INAD_CAPTURA(ID),
        OBSERVADO_EM datetime2(3) NOT NULL,
        CAPTURADO_POR nvarchar(128) NOT NULL
    );
    CREATE INDEX IX_INAD_CAPTURA_DATA ON dbo.INAD_CAPTURA(OBSERVADO_EM, ID);
    CREATE TABLE dbo.INAD_VENDA_OBSERVADA (
        CAPTURA_ID bigint NOT NULL REFERENCES dbo.INAD_CAPTURA(ID),
        NUM_VENDA int NOT NULL,
        INAD_FONTE bit NULL,
        SITUACAO bit NULL, -- 1 INAD; 0 ADP; NULL não comparável (não é novo status operacional)
        DIVIDAS_IDENTIFICADAS bit NOT NULL,
        QUITACAO_ANTERIOR bit NOT NULL,
        CONSTRAINT PK_INAD_VENDA_OBSERVADA PRIMARY KEY (CAPTURA_ID, NUM_VENDA)
    );
    CREATE TABLE dbo.INAD_PARCELA_OBSERVADA (
        CAPTURA_ID bigint NOT NULL,
        NUM_VENDA int NOT NULL,
        IDLAN int NOT NULL,
        CONSTRAINT PK_INAD_PARCELA_OBSERVADA PRIMARY KEY (CAPTURA_ID, NUM_VENDA, IDLAN),
        CONSTRAINT FK_INAD_PARCELA_VENDA FOREIGN KEY (CAPTURA_ID, NUM_VENDA)
            REFERENCES dbo.INAD_VENDA_OBSERVADA(CAPTURA_ID, NUM_VENDA)
    );
END;
COMMIT;
GO
CREATE OR ALTER PROCEDURE dbo.CapturarInadimplenciaObservada
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- Captura coerente, atômica e serializada. Pode reter locks nas fontes:
    -- executar após ETL concluído, fora do pico, avaliar em homologação antes de ativar.
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @lock int;
        EXEC @lock = sys.sp_getapplock @Resource='RJ_INAD_CAPTURA',
            @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0;
        IF @lock < 0 THROW 51000, 'Já existe uma captura de inadimplência em execução.', 1;
        DECLARE @anterior bigint = (SELECT MAX(ID) FROM dbo.INAD_CAPTURA);
        DECLARE @agora datetime2(3) = SYSDATETIME(); -- horário do servidor, não data do pagamento

        SELECT NUM_VENDA, UPPER(LTRIM(RTRIM(INADIMPLENTE))) FLAG,
            UPPER(LTRIM(RTRIM(STATUS_REPASSE))) REPASSE
        INTO #Vendas FROM DW.fat_analise_inadimplencia_v4;
        IF NOT EXISTS (SELECT 1 FROM #Vendas)
            THROW 51001, 'Fonte de vendas vazia: captura recusada; verificar ETL.', 1;
        IF EXISTS (SELECT NUM_VENDA FROM #Vendas GROUP BY NUM_VENDA HAVING COUNT(*) > 1)
            OR EXISTS (SELECT 1 FROM #Vendas WHERE NUM_VENDA IS NULL)
            THROW 51002, 'Chave de venda ausente ou repetida: captura recusada.', 1;
        CREATE UNIQUE CLUSTERED INDEX IX_Vendas ON #Vendas(NUM_VENDA);

        -- Agregar por venda/IDLAN, sem escolher silenciosamente uma das linhas conflitantes.
        SELECT p.NUM_VENDA, TRY_CONVERT(int,p.IDLAN) IDLAN,
            MAX(CASE WHEN UPPER(LTRIM(RTRIM(p.INADIMPLENTE)))='SIM' THEN 1 ELSE 0 END) ATRASADA,
            CASE WHEN TRY_CONVERT(int,p.IDLAN) IS NOT NULL
                AND COUNT(DISTINCT UPPER(LTRIM(RTRIM(p.INADIMPLENTE))))=1
                AND MIN(UPPER(LTRIM(RTRIM(p.INADIMPLENTE)))) IN ('SIM','NAO')
                AND COUNT(p.INADIMPLENTE)=COUNT(*)
                AND COUNT(p.DATAVENCIMENTO)=COUNT(*) AND MIN(p.DATAVENCIMENTO)=MAX(p.DATAVENCIMENTO)
                AND COUNT(p.VALOR)=COUNT(*) AND MIN(p.VALOR)=MAX(p.VALOR)
                THEN 1 ELSE 0 END CONSISTENTE
        INTO #Parcelas
        FROM DW.fat_analise_inadimplencia_parcelas p JOIN #Vendas v ON v.NUM_VENDA=p.NUM_VENDA
        GROUP BY p.NUM_VENDA, TRY_CONVERT(int,p.IDLAN);
        CREATE INDEX IX_Parcelas ON #Parcelas(NUM_VENDA,IDLAN);

        -- A ficha é usada apenas como evidência atual de quitação, nunca como diário de pagamentos.
        SELECT f.NUM_VENDA,f.IDLAN,
            CASE WHEN COUNT(*)=1 AND MIN(UPPER(LTRIM(RTRIM(f.STATUSLAN))))='BAIXADO'
                AND MIN(f.DATA_BAIXA) IS NOT NULL AND MAX(f.DATA_BAIXA)<=@agora
                AND MAX(f.SALDO_RECEBER)<=0 AND MIN(f.RECEBIDO)>0
                AND COUNT(f.DATA_CANCELAMENTO)=0 AND COUNT(f.DATA_CANCELAMENTO_LAN)=0
                AND MIN(UPPER(LTRIM(RTRIM(f.SITUACAO_VENDA))))='EFETIVADA'
                THEN 1 ELSE 0 END QUITADA,
            CASE WHEN COUNT(*)=1 AND MIN(f.DATA_VENCIMENTO)<CAST(@agora AS date)
                AND MIN(UPPER(LTRIM(RTRIM(f.STATUSLAN)))) IN ('EM ABERTO','BAIXA PARCIAL')
                AND MIN(f.SALDO_RECEBER)>0 AND COUNT(f.DATA_CANCELAMENTO)=0
                AND COUNT(f.DATA_CANCELAMENTO_LAN)=0
                AND MIN(UPPER(LTRIM(RTRIM(f.SITUACAO_VENDA))))='EFETIVADA'
                THEN 1 ELSE 0 END ATRASADA_CONFIRMADA,
            CASE WHEN COUNT(*)=1 AND MIN(f.DATA_VENCIMENTO) IS NOT NULL
                AND COUNT(f.DATA_CANCELAMENTO)=0 AND COUNT(f.DATA_CANCELAMENTO_LAN)=0
                AND MIN(UPPER(LTRIM(RTRIM(f.SITUACAO_VENDA))))='EFETIVADA'
                AND (MIN(f.DATA_VENCIMENTO)>=CAST(@agora AS date)
                    OR (MIN(UPPER(LTRIM(RTRIM(f.STATUSLAN))))='BAIXADO'
                        AND MIN(f.DATA_BAIXA) IS NOT NULL AND MAX(f.DATA_BAIXA)<=@agora
                        AND MAX(f.SALDO_RECEBER)<=0 AND MIN(f.RECEBIDO)>0))
                THEN 1 ELSE 0 END SEM_PENDENCIA
        INTO #Ficha
        FROM DW.fat_ficha_financeira_cliente f JOIN #Vendas v ON v.NUM_VENDA=f.NUM_VENDA
        GROUP BY f.NUM_VENDA,f.IDLAN;
        CREATE INDEX IX_Ficha ON #Ficha(NUM_VENDA,IDLAN);

        INSERT dbo.INAD_CAPTURA(ANTERIOR_ID,OBSERVADO_EM,CAPTURADO_POR)
        VALUES(@anterior,@agora,ORIGINAL_LOGIN());
        DECLARE @id bigint = SCOPE_IDENTITY();
        INSERT dbo.INAD_VENDA_OBSERVADA
            (CAPTURA_ID,NUM_VENDA,INAD_FONTE,SITUACAO,DIVIDAS_IDENTIFICADAS,QUITACAO_ANTERIOR)
        SELECT @id,v.NUM_VENDA,CASE v.FLAG WHEN 'SIM' THEN 1 WHEN 'NAO' THEN 0 END,
            CASE WHEN v.REPASSE='DISTRATO' THEN NULL
                -- O resumo sozinho não comprova atraso. Exigir ao menos uma
                -- parcela identificável, consistente, vencida e ainda em aberto.
                WHEN v.FLAG='SIM' AND EXISTS (
                    SELECT 1 FROM #Parcelas p JOIN #Ficha f ON f.NUM_VENDA=p.NUM_VENDA AND f.IDLAN=p.IDLAN
                    WHERE p.NUM_VENDA=v.NUM_VENDA AND p.ATRASADA=1 AND p.CONSISTENTE=1
                        AND f.ATRASADA_CONFIRMADA=1
                ) THEN 1
                WHEN v.FLAG='NAO'
                    AND EXISTS (SELECT 1 FROM #Ficha f WHERE f.NUM_VENDA=v.NUM_VENDA)
                    AND NOT EXISTS (SELECT 1 FROM #Ficha f WHERE f.NUM_VENDA=v.NUM_VENDA AND f.SEM_PENDENCIA=0)
                    AND NOT EXISTS (SELECT 1 FROM #Parcelas p WHERE p.NUM_VENDA=v.NUM_VENDA AND (p.ATRASADA=1 OR p.CONSISTENTE=0))
                    THEN 0 END,
            CASE WHEN EXISTS (SELECT 1 FROM #Parcelas p WHERE p.NUM_VENDA=v.NUM_VENDA AND p.ATRASADA=1)
                AND NOT EXISTS (SELECT 1 FROM #Parcelas p WHERE p.NUM_VENDA=v.NUM_VENDA AND p.ATRASADA=1 AND p.CONSISTENTE=0)
                AND NOT EXISTS (SELECT 1 FROM #Parcelas p WHERE p.NUM_VENDA=v.NUM_VENDA AND p.ATRASADA=1
                    AND NOT EXISTS (SELECT 1 FROM #Ficha f WHERE f.NUM_VENDA=p.NUM_VENDA AND f.IDLAN=p.IDLAN AND f.ATRASADA_CONFIRMADA=1))
                THEN 1 ELSE 0 END,
            CASE WHEN EXISTS (SELECT 1 FROM dbo.INAD_VENDA_OBSERVADA a
                    WHERE a.CAPTURA_ID=@anterior AND a.NUM_VENDA=v.NUM_VENDA AND a.DIVIDAS_IDENTIFICADAS=1)
                AND EXISTS (SELECT 1 FROM dbo.INAD_PARCELA_OBSERVADA p WHERE p.CAPTURA_ID=@anterior AND p.NUM_VENDA=v.NUM_VENDA)
                AND NOT EXISTS (SELECT 1 FROM dbo.INAD_PARCELA_OBSERVADA p
                    WHERE p.CAPTURA_ID=@anterior AND p.NUM_VENDA=v.NUM_VENDA
                    AND NOT EXISTS (SELECT 1 FROM #Ficha f WHERE f.NUM_VENDA=p.NUM_VENDA AND f.IDLAN=p.IDLAN AND f.QUITADA=1))
                THEN 1 ELSE 0 END
        FROM #Vendas v;
        INSERT dbo.INAD_PARCELA_OBSERVADA(CAPTURA_ID,NUM_VENDA,IDLAN)
        SELECT @id,NUM_VENDA,IDLAN FROM #Parcelas WHERE ATRASADA=1 AND CONSISTENTE=1;
        COMMIT;
        SELECT @id CAPTURA_ID,@agora OBSERVADO_EM;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
-- Sem GRANT para o usuário da API. Somente operação controlada pode executar a captura.
-- Rollback não destrutivo: parar capturas e voltar a versão da aplicação; preservar histórico.
-- Não fazer DROP/TRUNCATE: estas observações não podem ser reconstruídas pelo estado atual.

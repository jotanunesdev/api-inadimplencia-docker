namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>Vínculo jurídico no nível do cliente; nunca altera a situação operacional de suas vendas.</summary>
public static class CarteiraJuridicaSql
{
    // RJ-033: lista central, baseada nas etapas existentes e autorização da área.
    // Ausência desta lista não equivale a encerramento. Não usar ativo='S' da ponte.
    public const string Classificacao = """
        (N'Avaliação Jurídico','ATIVO'),(N'Enviado ao Escritório','ATIVO'),
        (N'Acordo Extra Judicial','ATIVO'),(N'Novo Processo','ATIVO'),
        (N'Aguardando Citação','ATIVO'),(N'Conciliação','ATIVO'),(N'Instrução','ATIVO'),
        (N'Perícia','ATIVO'),(N'Sentenciado','ATIVO'),(N'Recurso','ATIVO'),
        (N'Cumprimento de Sentença','ATIVO'),(N'Acordo Judicial','ATIVO'),
        (N'Arquivado','ENCERRADO'),(N'Extinto','ENCERRADO'),(N'Cancelado','ENCERRADO'),
        (N'Desistencia','ENCERRADO'),(N'Desistência','ENCERRADO'),(N'Desistência Comercial','ENCERRADO')
        """;

    public const string Base = """
        WITH Regras AS (SELECT * FROM (VALUES
        """ + Classificacao + """
        ) x(SITUACAO,CLASSE)), Fonte AS (
            SELECT TRY_CONVERT(int,referencia) AS IDPROCESSO,
                TRY_CONVERT(datetime2,referencia_data) AS DATA_REFERENCIA,
                NULLIF(LTRIM(RTRIM(situacao)),'') AS SITUACAO,
                NULLIF(LTRIM(RTRIM(numero)),'') AS NUMERO
            FROM dw.fat_processos_juridicos_cv
        ), Referencias AS (
            SELECT IDPROCESSO, MAX(DATA_REFERENCIA) AS DATA_REFERENCIA,
                CASE WHEN COUNT(*)=COUNT(DATA_REFERENCIA) THEN 1 ELSE 0 END AS DATAS_VALIDAS
            FROM Fonte WHERE IDPROCESSO IS NOT NULL GROUP BY IDPROCESSO
        ), Ultimas AS (
            SELECT r.IDPROCESSO, r.DATA_REFERENCIA,
                CASE WHEN MIN(r.DATAS_VALIDAS)=1 AND COUNT(*)=COUNT(f.SITUACAO)
                          AND MIN(f.SITUACAO)=MAX(f.SITUACAO) THEN MAX(f.SITUACAO) END AS SITUACAO,
                CASE WHEN MIN(f.NUMERO)=MAX(f.NUMERO) THEN MAX(f.NUMERO) END AS NUMERO
            FROM Referencias r LEFT JOIN Fonte f ON f.IDPROCESSO=r.IDPROCESSO AND f.DATA_REFERENCIA=r.DATA_REFERENCIA
            GROUP BY r.IDPROCESSO,r.DATA_REFERENCIA
        ), Identidades AS (
            SELECT idprocesso AS IDPROCESSO,idcliente AS ID_CLIENTE,
                NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(documento_cliente)),'.',''),'-',''),'/',''),' ',''),'') AS DOCUMENTO,
                TRY_CONVERT(datetime2,referencia_data) AS DATA_PONTE
            FROM dw.fat_processojur_cv
        ), Ponte AS (
            SELECT IDPROCESSO,
                CASE WHEN COUNT(*)=COUNT(ID_CLIENTE) AND MIN(ID_CLIENTE)=MAX(ID_CLIENTE)
                          AND COUNT(*)=COUNT(DOCUMENTO) AND MIN(DOCUMENTO)=MAX(DOCUMENTO)
                     THEN MAX(ID_CLIENTE) END AS ID_CLIENTE,
                CASE WHEN COUNT(*)=COUNT(ID_CLIENTE) AND MIN(ID_CLIENTE)=MAX(ID_CLIENTE)
                          AND COUNT(*)=COUNT(DOCUMENTO) AND MIN(DOCUMENTO)=MAX(DOCUMENTO)
                     THEN MAX(DOCUMENTO) END AS DOCUMENTO,
                MAX(DATA_PONTE) AS DATA_PONTE
            FROM Identidades GROUP BY IDPROCESSO
        ), Processos AS (
            SELECT u.*,p.ID_CLIENTE,p.DOCUMENTO,p.DATA_PONTE,
                COALESCE((SELECT MAX(r.CLASSE) FROM Regras r WHERE r.SITUACAO=u.SITUACAO),'NAO_CLASSIFICADO') AS CLASSE
            FROM Ultimas u LEFT JOIN Ponte p ON p.IDPROCESSO=u.IDPROCESSO
        ), Responsaveis AS (
            SELECT NUM_VENDA_FK,CASE WHEN COUNT(*)=1 THEN MAX(NOME_USUARIO_FK) END AS RESPONSAVEL
            FROM dbo.VENDA_RESPONSAVEL GROUP BY NUM_VENDA_FK
        ), Vendas AS (
            SELECT f.NUM_VENDA,f.ID_CLIENTE,f.CLIENTE,f.CPF_CNPJ,f.EMPREENDIMENTO,
                f.SCORE,f.SUGESTAO,f.VALOR_INADIMPLENTE,r.RESPONSAVEL,
                NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(f.CPF_CNPJ)),'.',''),'-',''),'/',''),' ',''),'') AS DOCUMENTO
            FROM DW.fat_analise_inadimplencia_v4 f
            LEFT JOIN Responsaveis r ON r.NUM_VENDA_FK=f.NUM_VENDA
            WHERE UPPER(LTRIM(RTRIM(f.INADIMPLENTE)))='SIM'
                AND (@cliente IS NULL OR f.CLIENTE LIKE @cliente ESCAPE '~')
                AND (@nomeUsuario IS NULL OR LOWER(LTRIM(RTRIM(r.RESPONSAVEL)))=@nomeUsuario)
        ), Vinculos AS (
            SELECT v.NUM_VENDA,p.* FROM Vendas v JOIN Processos p
                ON p.ID_CLIENTE=v.ID_CLIENTE AND p.DOCUMENTO=v.DOCUMENTO
        ), VendasAtivas AS (
            SELECT v.* FROM Vendas v WHERE EXISTS (
                SELECT 1 FROM Processos p WHERE p.ID_CLIENTE=v.ID_CLIENTE
                    AND p.DOCUMENTO=v.DOCUMENTO AND p.CLASSE='ATIVO'
            )
        ), ClientesAtivos AS (
            SELECT ID_CLIENTE,DOCUMENTO,COUNT_BIG(*) AS VENDAS FROM VendasAtivas GROUP BY ID_CLIENTE,DOCUMENTO
        )
        """;

    public const string Resumo = Base + """
        SELECT a.CLIENTES_ATIVOS,a.VENDAS_DOS_CLIENTES,b.*,
            (SELECT COUNT_BIG(*) FROM Vendas) AS BASE_VENDAS,
            (SELECT COUNT_BIG(*) FROM Processos WHERE ID_CLIENTE IS NULL OR DOCUMENTO IS NULL) AS GLOBAL_PROCESSOS_SEM_PONTE,
            (SELECT COUNT_BIG(*) FROM Fonte WHERE IDPROCESSO IS NULL) AS GLOBAL_LINHAS_SEM_REFERENCIA,
            (SELECT MAX(DATA_REFERENCIA) FROM Referencias) AS ULTIMA_REFERENCIA,
            (SELECT MAX(DATA_PONTE) FROM Ponte) AS ULTIMA_REFERENCIA_PONTE,
            CONVERT(varchar(19),GETDATE(),126) AS CONSULTADO_EM
        FROM (SELECT COUNT_BIG(*) AS CLIENTES_ATIVOS,COALESCE(SUM(VENDAS),0) AS VENDAS_DOS_CLIENTES FROM ClientesAtivos) a
        CROSS JOIN (SELECT COUNT(DISTINCT CASE WHEN CLASSE='ATIVO' THEN IDPROCESSO END) AS PROCESSOS_ATIVOS,
            COUNT(DISTINCT CASE WHEN CLASSE='ENCERRADO' THEN IDPROCESSO END) AS PROCESSOS_ENCERRADOS,
            COUNT(DISTINCT CASE WHEN CLASSE='NAO_CLASSIFICADO' THEN IDPROCESSO END) AS PROCESSOS_NAO_CLASSIFICADOS
            FROM Vinculos) b
        OPTION(RECOMPILE)
        """;

    public const string Detalhes = Base + """
        SELECT v.*,COUNT_BIG(*) OVER() AS TOTAL_COUNT,
            (SELECT COUNT_BIG(*) FROM Vinculos p WHERE p.NUM_VENDA=v.NUM_VENDA AND p.CLASSE='ATIVO') AS PROCESSOS_ATIVOS
        FROM VendasAtivas v ORDER BY v.NUM_VENDA
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY OPTION(RECOMPILE)
        """;

    public const string ProcessosDetalhes = Base + """
        SELECT IDPROCESSO,NUMERO,SITUACAO,CLASSE,DATA_REFERENCIA,DATA_PONTE,
            COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM Vinculos WHERE NUM_VENDA=@numVenda AND CLASSE='ATIVO'
        ORDER BY IDPROCESSO
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY OPTION(RECOMPILE)
        """;
}

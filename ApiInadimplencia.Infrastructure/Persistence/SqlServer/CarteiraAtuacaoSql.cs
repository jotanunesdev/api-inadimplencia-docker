namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>Posição atual por venda. Categorias analíticas, não novos status operacionais.</summary>
public static class CarteiraAtuacaoSql
{
    // Lista fechada dos cinco contatos confirmados, com os nomes já usados na tela.
    // Não adicionar ocorrências administrativas nem usar descrição livre como evidência.
    public const string TiposContato = """
        (N'Cobrança ativa (telefonema)'), (N'Cobrança via WhatsApp'),
        (N'Negociação em andamento'), (N'Promessa de pagamento'),
        (N'Retorno agendado com o cliente')
        """;

    // Juridico é consultado separadamente por CarteiraJuridicaSql; não altera atuação.
    // SCORE é usado diretamente. Datas de ocorrência são evidências para revisão,
    // não um início de ciclo inferido a partir do vencimento ou da atribuição.
    public const string Base = """
        WITH TiposContato(TIPO) AS (SELECT TIPO FROM (VALUES
        """ + TiposContato + """
        ) t(TIPO)), Responsaveis AS (
            SELECT NUM_VENDA_FK,
                CASE WHEN COUNT(*) = 1 THEN MAX(NULLIF(LTRIM(RTRIM(NOME_USUARIO_FK)), '')) END AS RESPONSAVEL,
                CASE WHEN COUNT(*) = 1 THEN MAX(DT_ATRIBUICAO) END AS DT_ATRIBUICAO,
                COUNT_BIG(*) AS VINCULOS
            FROM dbo.VENDA_RESPONSAVEL GROUP BY NUM_VENDA_FK
        ), Vendas AS (
            SELECT f.NUM_VENDA, f.ID_CLIENTE, f.CLIENTE, f.CPF_CNPJ, f.EMPREENDIMENTO,
                f.SCORE, f.SUGESTAO, f.VENCIMENTO_MAIS_ANTIGO, f.VALOR_INADIMPLENTE,
                r.RESPONSAVEL, r.DT_ATRIBUICAO,
                agenda.PROXIMA_ACAO, agenda.NOME_USUARIO_FK AS AUTOR_AGENDAMENTO,
                CASE WHEN agenda.PROXIMA_ACAO IS NOT NULL THEN 1 ELSE 0 END AS TEM_AGENDAMENTO,
                CASE WHEN COALESCE(r.VINCULOS,0) > 1 THEN 1 ELSE 0 END AS RESPONSAVEL_AMBIGUO,
                CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.OCORRENCIAS o
                    WHERE o.NUM_VENDA_FK = f.NUM_VENDA
                      AND o.DT_OCORRENCIA < DATEADD(day,1,CONVERT(date,GETDATE()))
                      AND EXISTS (SELECT 1 FROM TiposContato t WHERE t.TIPO = o.STATUS_OCORRENCIA)
                ) THEN 1 ELSE 0 END AS TEM_CONTATO_HISTORICO
            FROM DW.fat_analise_inadimplencia_v4 f
            LEFT JOIN Responsaveis r ON r.NUM_VENDA_FK = f.NUM_VENDA
            OUTER APPLY (
                -- Mesmo critério de Dashboard.AcoesDefinidas: último registro com próxima ação.
                SELECT TOP (1) o.PROXIMA_ACAO, o.NOME_USUARIO_FK
                FROM dbo.OCORRENCIAS o
                WHERE o.NUM_VENDA_FK=f.NUM_VENDA AND o.PROXIMA_ACAO IS NOT NULL
                ORDER BY o.DT_OCORRENCIA DESC,o.HORA_OCORRENCIA DESC,o.PROXIMA_ACAO DESC,o.ID DESC
            ) agenda
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE,'')))) = 'SIM'
              AND (@cliente IS NULL OR f.CLIENTE LIKE @cliente ESCAPE '~')
              AND (@nomeUsuario IS NULL OR LOWER(r.RESPONSAVEL) = @nomeUsuario)
              AND (@semResponsavel=0 OR NOT EXISTS (SELECT 1 FROM dbo.VENDA_RESPONSAVEL sr
                  WHERE sr.NUM_VENDA_FK=f.NUM_VENDA AND NULLIF(LTRIM(RTRIM(sr.NOME_USUARIO_FK)),'') IS NOT NULL))
        ), Classificadas AS (
            SELECT *, CASE
                WHEN SCORE IS NULL THEN 'SEM_SCORE'
                WHEN SCORE <= 5 THEN 'NAO_APTO'
                WHEN RESPONSAVEL_AMBIGUO = 1 THEN 'RESPONSAVEL_AMBIGUO'
                WHEN CONVERT(date,PROXIMA_ACAO) > CONVERT(date,GETDATE()) THEN 'APTO_ACAO_FUTURA'
                WHEN PROXIMA_ACAO IS NOT NULL THEN 'APTO_ACAO_VENCIDA'
                WHEN TEM_CONTATO_HISTORICO = 1 THEN 'CICLO_PENDENTE'
                ELSE 'APTO_SEM_REGISTRO' END AS CATEGORIA
            FROM Vendas
        )
        """;

    public const string Resumo = Base + """
        , Categorias(CATEGORIA) AS (
            SELECT CATEGORIA FROM (VALUES ('NAO_APTO'),('APTO_SEM_REGISTRO'),
                ('CICLO_PENDENTE'),('APTO_ACAO_FUTURA'),('APTO_ACAO_VENCIDA'),('SEM_SCORE'),('RESPONSAVEL_AMBIGUO')) c(CATEGORIA)
        ), Totais AS (
            SELECT c.CATEGORIA, COUNT_BIG(v.NUM_VENDA) AS QUANTIDADE
            FROM Categorias c LEFT JOIN Classificadas v ON v.CATEGORIA=c.CATEGORIA
            GROUP BY c.CATEGORIA
        )
        SELECT CATEGORIA, QUANTIDADE, SUM(QUANTIDADE) OVER() AS TOTAL_VENDAS,
            COALESCE(CAST(100.0 * QUANTIDADE / NULLIF(SUM(QUANTIDADE) OVER(),0) AS decimal(10,2)),0) AS PERCENTUAL,
            CONVERT(varchar(19), GETDATE(), 126) AS CONSULTADO_EM,
            'VENDA' AS GRANULARIDADE, CAST(0 AS int) AS CICLO_VALIDADO,
            CAST(1 AS int) AS JURIDICO_DISPONIVEL,
            COALESCE((SELECT v.RESPONSAVEL, COUNT_BIG(*) AS QUANTIDADE
             FROM Classificadas v
             WHERE v.CATEGORIA = t.CATEGORIA
               AND v.CATEGORIA IN ('APTO_SEM_REGISTRO', 'CICLO_PENDENTE','APTO_ACAO_FUTURA','APTO_ACAO_VENCIDA')
             GROUP BY v.RESPONSAVEL
             ORDER BY QUANTIDADE DESC, v.RESPONSAVEL
             FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]') AS RESPONSAVEIS_JSON
        FROM Totais t ORDER BY CATEGORIA
        """;

    public const string Detalhes = Base + """
        SELECT v.*, COUNT_BIG(*) OVER() AS TOTAL_COUNT,
            CONVERT(varchar(19),GETDATE(),126) AS CONSULTADO_EM
        FROM Classificadas v WHERE v.CATEGORIA = @situacao
        ORDER BY v.NUM_VENDA
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;

    // Somente sob demanda. Mostra evidências históricas sem afirmar vínculo ao ciclo atual.
    public const string Contatos = Base + """
        SELECT o.ID, o.NUM_VENDA_FK, o.NOME_USUARIO_FK, o.DT_OCORRENCIA,
            o.HORA_OCORRENCIA, o.STATUS_OCORRENCIA, o.DESCRICAO, o.PROXIMA_ACAO, o.PROTOCOLO,
            COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM dbo.OCORRENCIAS o
        WHERE o.NUM_VENDA_FK = @numVenda
          AND EXISTS (SELECT 1 FROM Classificadas v WHERE v.NUM_VENDA=o.NUM_VENDA_FK
              AND v.CATEGORIA=@situacao)
          AND (o.PROXIMA_ACAO IS NOT NULL OR EXISTS (SELECT 1 FROM TiposContato t WHERE t.TIPO=o.STATUS_OCORRENCIA))
        ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.ID DESC
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;
}

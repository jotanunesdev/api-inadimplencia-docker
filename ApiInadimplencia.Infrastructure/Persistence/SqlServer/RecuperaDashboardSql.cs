namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>Agregações com granularidade explícita; nenhuma lista financeira é agregada na API.</summary>
public static class RecuperaDashboardSql
{
    // Mesmo universo dos KPIs existentes. Não relacionar parcelas aqui: o 1:N
    // multiplicaria as vendas e os valores. NUM_VENDA é único na fonte confirmada.
    public const string CarteiraInadimplenteDetalhes = """
        SELECT f.NUM_VENDA, f.CLIENTE, f.CPF_CNPJ, f.EMPREENDIMENTO,
            f.INADIMPLENTE, f.VALOR_INADIMPLENTE, f.VALOR_TOTAL_EM_ABERTO,
            f.QTD_PARCELAS_INADIMPLENTES, f.VENCIMENTO_MAIS_ANTIGO,
            f.STATUS_REPASSE, r.NOME_USUARIO_FK AS RESPONSAVEL,
            CONVERT(varchar(10), GETDATE(), 23) AS DATA_REFERENCIA,
            COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM DW.fat_analise_inadimplencia_v4 f
        LEFT JOIN dbo.VENDA_RESPONSAVEL r ON r.NUM_VENDA_FK = f.NUM_VENDA
        WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
          AND (@cliente IS NULL OR f.CLIENTE LIKE @cliente ESCAPE '~')
          AND (@semResponsavel=0 OR NOT EXISTS (SELECT 1 FROM dbo.VENDA_RESPONSAVEL sr
              WHERE sr.NUM_VENDA_FK=f.NUM_VENDA AND NULLIF(LTRIM(RTRIM(sr.NOME_USUARIO_FK)),'') IS NOT NULL))
        ORDER BY f.VALOR_INADIMPLENTE DESC, f.NUM_VENDA
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;

    // Exibe registros da fonte, sem deduplicar IDLAN nulo ou inferir pagamento
    // pelo indicador NAO. A quantidade da paginação não é quantidade de vendas.
    public const string CarteiraInadimplenteParcelas = """
        SELECT p.NUM_VENDA, p.IDLAN, p.NUMERO_DOCUMENTO, p.DATAVENCIMENTO,
            p.VALOR, p.INADIMPLENTE, p.NEGATIVADO,
            CONVERT(varchar(10), GETDATE(), 23) AS DATA_REFERENCIA,
            COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM DW.fat_analise_inadimplencia_parcelas p
        WHERE p.NUM_VENDA = @numVenda
          AND EXISTS (
            SELECT 1 FROM DW.fat_analise_inadimplencia_v4 f
            WHERE f.NUM_VENDA = p.NUM_VENDA
              AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
          )
        ORDER BY p.DATAVENCIMENTO, p.IDLAN, p.NUMERO_DOCUMENTO,
            p.VALOR, p.INADIMPLENTE, p.NEGATIVADO
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;

    // Snapshot: dates of occurrences never stand in for financial history.
    // STATUSLAN/baixa parcial follow the existing financial source and DW rules.
    public const string SituacaoAtualBase = """
        WITH Fonte AS (
            SELECT *, UPPER(LTRIM(RTRIM(STATUSLAN))) AS STATUS_NORMALIZADO,
                NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(CPF_CNPJ)), '.', ''), '-', ''), '/', ''), ' ', ''), '') AS DOCUMENTO_NORMALIZADO,
                TRY_CONVERT(decimal(19,2), VALOR_VENDA) AS VALOR_NORMALIZADO
            FROM DW.fat_ficha_financeira_cliente
            WHERE UPPER(LTRIM(RTRIM(SITUACAO_VENDA))) = 'EFETIVADA' AND NUM_VENDA IS NOT NULL
        ), Parcelas AS (
            SELECT NUM_VENDA, IDLAN, MAX(CLIENTE) AS CLIENTE,
                MIN(DATA_VENCIMENTO) AS DATA_VENCIMENTO, MAX(DATA_BAIXA) AS DATA_BAIXA,
                MAX(STATUS_NORMALIZADO) AS STATUSLAN, MAX(SALDO_RECEBER) AS SALDO_RECEBER,
                MAX(RECEBIDO) AS RECEBIDO,
                CASE WHEN IDLAN IS NOT NULL AND MIN(STATUS_NORMALIZADO) = MAX(STATUS_NORMALIZADO)
                      AND COUNT(*) = COUNT(STATUS_NORMALIZADO)
                      AND MIN(DATA_VENCIMENTO) = MAX(DATA_VENCIMENTO) AND COUNT(*) = COUNT(DATA_VENCIMENTO)
                      AND MIN(CASE WHEN STATUS_NORMALIZADO IN ('EM ABERTO','BAIXA PARCIAL') THEN 1
                                   WHEN STATUS_NORMALIZADO = 'BAIXADO' AND DATA_BAIXA IS NOT NULL
                                        AND SALDO_RECEBER <= 0 THEN 1 ELSE 0 END) = 1
                     THEN 1 ELSE 0 END AS CONHECIDA,
                MAX(CASE WHEN STATUS_NORMALIZADO IN ('EM ABERTO','BAIXA PARCIAL')
                              AND DATA_VENCIMENTO < CONVERT(date, GETDATE()) THEN 1 ELSE 0 END) AS ATRASADA
            FROM Fonte
            WHERE DATA_CANCELAMENTO_LAN IS NULL AND COALESCE(STATUS_NORMALIZADO, '') <> 'CANCELADO'
                AND (TIPO_PARCELA IS NULL OR TIPO_PARCELA NOT LIKE '60-Devolu%')
            GROUP BY NUM_VENDA, IDLAN
        ), Situacoes AS (
            SELECT NUM_VENDA, MAX(CASE WHEN CONHECIDA = 1 THEN ATRASADA ELSE 0 END) AS INAD,
                MIN(CONHECIDA) AS CONHECIDA, COUNT_BIG(*) AS PARCELAS,
                SUM(CASE WHEN CONHECIDA = 1 AND ATRASADA = 1 THEN 1 ELSE 0 END) AS PARCELAS_ATRASADAS
            FROM Parcelas GROUP BY NUM_VENDA
        ), Vendas AS (
            SELECT ff.NUM_VENDA,
                MAX(ff.CLIENTE) AS CLIENTE,
                CASE WHEN MIN(ff.DOCUMENTO_NORMALIZADO) = MAX(ff.DOCUMENTO_NORMALIZADO)
                     AND COUNT(*) = COUNT(ff.DOCUMENTO_NORMALIZADO)
                     THEN MIN(ff.DOCUMENTO_NORMALIZADO) END AS DOCUMENTO,
                MAX(ff.VALOR_NORMALIZADO) AS VALOR,
                CASE WHEN MIN(ff.DOCUMENTO_NORMALIZADO) = MAX(ff.DOCUMENTO_NORMALIZADO)
                     AND MIN(ff.VALOR_NORMALIZADO) = MAX(ff.VALOR_NORMALIZADO)
                     AND COUNT(*) = COUNT(ff.VALOR_NORMALIZADO) THEN 1 ELSE 0 END AS CONSISTENTE
            FROM Fonte ff
            GROUP BY ff.NUM_VENDA
        ), Clientes AS (
            SELECT v.DOCUMENTO, SUM(v.VALOR) AS VALOR,
                CASE WHEN MAX(s.INAD) = 1 THEN 'INADIMPLENTE'
                     WHEN MIN(COALESCE(s.CONHECIDA, 0)) = 1 THEN 'ADIMPLENTE'
                     ELSE 'SEM_SITUACAO' END AS SITUACAO,
                MIN(v.CONSISTENTE) AS CONSISTENTE
            FROM Vendas v
            LEFT JOIN Situacoes s ON s.NUM_VENDA = v.NUM_VENDA
            WHERE v.DOCUMENTO IS NOT NULL
            GROUP BY v.DOCUMENTO
        )
        """;

    public const string Convertidos = SituacaoAtualBase + """
        , Totais AS (
            SELECT categorias.SITUACAO, COUNT_BIG(c.DOCUMENTO) AS QUANTIDADE,
                SUM(CASE WHEN c.CONSISTENTE = 1 THEN c.VALOR ELSE 0 END) AS VALOR,
                SUM(CASE WHEN c.CONSISTENTE = 0 THEN 1 ELSE 0 END) AS CLIENTES_VALOR_INCONSISTENTE
            FROM (VALUES ('ADIMPLENTE'), ('INADIMPLENTE'), ('SEM_SITUACAO')) categorias(SITUACAO)
            LEFT JOIN Clientes c ON c.SITUACAO = categorias.SITUACAO
            GROUP BY categorias.SITUACAO
        )
        SELECT SITUACAO, QUANTIDADE, VALOR, CLIENTES_VALOR_INCONSISTENTE,
            CONVERT(varchar(10), GETDATE(), 23) AS DATA_REFERENCIA,
            SUM(QUANTIDADE) OVER() AS TOTAL_CLIENTES,
            SUM(VALOR) OVER() AS VALOR_TOTAL,
            COALESCE(CAST(100.0 * QUANTIDADE / NULLIF(SUM(QUANTIDADE) OVER(), 0) AS decimal(10,2)), 0) AS PERCENTUAL_CLIENTES,
            COALESCE(CAST(100.0 * VALOR / NULLIF(SUM(VALOR) OVER(), 0) AS decimal(10,2)), 0) AS PERCENTUAL_VALOR,
            (SELECT COUNT_BIG(*) FROM Vendas WHERE DOCUMENTO IS NULL) AS VENDAS_SEM_DOCUMENTO
        FROM Totais ORDER BY SITUACAO
        """;

    public const string SituacaoClientesDetalhes = SituacaoAtualBase + """
        SELECT v.NUM_VENDA, v.CLIENTE, v.DOCUMENTO AS CPF_CNPJ, c.SITUACAO,
            CASE WHEN v.CONSISTENTE = 1 THEN v.VALOR END AS VALOR_VENDA,
            r.NOME_USUARIO_FK AS RESPONSAVEL, s.PARCELAS, s.PARCELAS_ATRASADAS,
            CONVERT(varchar(10), GETDATE(), 23) AS DATA_REFERENCIA, COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM Vendas v JOIN Clientes c ON c.DOCUMENTO = v.DOCUMENTO
        LEFT JOIN Situacoes s ON s.NUM_VENDA = v.NUM_VENDA
        LEFT JOIN dbo.VENDA_RESPONSAVEL r ON r.NUM_VENDA_FK = v.NUM_VENDA
        WHERE (@situacao IS NULL OR c.SITUACAO = @situacao)
        ORDER BY v.DOCUMENTO, v.NUM_VENDA
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;

    public static readonly string SituacaoParcelasDetalhes = SituacaoAtualBase.Replace(
        "AND NUM_VENDA IS NOT NULL", "AND NUM_VENDA = @numVenda") + """
        SELECT IDLAN, NUM_VENDA, STATUSLAN, DATA_VENCIMENTO, DATA_BAIXA, SALDO_RECEBER, RECEBIDO,
            CASE WHEN CONHECIDA = 0 THEN 'SEM_SITUACAO' WHEN ATRASADA = 1 THEN 'INADIMPLENTE'
                 ELSE 'ADIMPLENTE' END AS SITUACAO,
            CONVERT(varchar(10), GETDATE(), 23) AS DATA_REFERENCIA, COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM Parcelas WHERE NUM_VENDA = @numVenda
        ORDER BY DATA_VENCIMENTO, IDLAN
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;

    // Existing DW snapshot, not a newly invented event log. See the inspected
    // usp_gera_carteira_analitica: dias_atraso derives from live overdue parcels.
    // Fully paid sales disappear from this source; never infer recovery from absence.
    public const string HistoricoBase = """
        WITH VendasMes AS (
            SELECT data_base, venda AS NUM_VENDA, MAX(comprador) AS CLIENTE,
                CASE WHEN MIN(NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(documento)),'.',''),'-',''),'/',''),' ',''),''))
                        = MAX(NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(documento)),'.',''),'-',''),'/',''),' ',''),''))
                          AND COUNT(*) = COUNT(NULLIF(LTRIM(RTRIM(documento)),''))
                     THEN MIN(NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(documento)),'.',''),'-',''),'/',''),' ',''),'')) END AS DOCUMENTO,
                MAX(venda_valor) AS VALOR,
                CASE WHEN MIN(venda_valor) = MAX(venda_valor) AND COUNT(*) = COUNT(venda_valor)
                          AND MIN(venda_valor) >= 0 THEN 1 ELSE 0 END AS VALOR_CONSISTENTE,
                CASE WHEN MIN(dias_atraso) = MAX(dias_atraso) AND COUNT(*) = COUNT(dias_atraso)
                          AND MIN(dias_atraso) >= 0 THEN MAX(CASE WHEN dias_atraso > 0 THEN 1 ELSE 0 END) END AS INAD
            FROM dw.carteira_analitica_hist
            WHERE data_base <= COALESCE(@dataFim, CONVERT(date, GETDATE())) AND venda IS NOT NULL
            GROUP BY data_base, venda
        ), ClientesMes AS (
            SELECT data_base, DOCUMENTO,
                CASE WHEN MAX(INAD) = 1 THEN 1 WHEN COUNT(*) = COUNT(INAD) THEN 0 END AS INAD,
                CASE WHEN MIN(VALOR_CONSISTENTE) = 1 THEN SUM(VALOR) END AS VALOR,
                STRING_AGG(CONVERT(varchar(max), NUM_VENDA), ',') WITHIN GROUP (ORDER BY NUM_VENDA) AS VENDAS
            FROM VendasMes WHERE DOCUMENTO IS NOT NULL GROUP BY data_base, DOCUMENTO
        ), Anteriores AS (
            SELECT *, LAG(data_base) OVER(PARTITION BY DOCUMENTO ORDER BY data_base) AS BASE_ANTERIOR,
                LAG(INAD) OVER(PARTITION BY DOCUMENTO ORDER BY data_base) AS INAD_ANTERIOR,
                LAG(VENDAS) OVER(PARTITION BY DOCUMENTO ORDER BY data_base) AS VENDAS_ANTERIORES
            FROM ClientesMes
        ), Comparaveis AS (
            SELECT *, CASE WHEN DATEDIFF(month, BASE_ANTERIOR, data_base) = 1
                                AND VENDAS = VENDAS_ANTERIORES AND INAD IS NOT NULL AND INAD_ANTERIOR IS NOT NULL
                           THEN 1 ELSE 0 END AS COMPARAVEL
            FROM Anteriores
        ), Movimentos AS (
            SELECT *, CASE WHEN COMPARAVEL = 1 AND INAD_ANTERIOR = 1 AND INAD = 0 THEN 1 ELSE 0 END AS REGULARIZOU
            FROM Comparaveis
        ), Reincidencias AS (
            SELECT *, MAX(REGULARIZOU) OVER(PARTITION BY DOCUMENTO ORDER BY data_base
                ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS JA_REGULARIZOU
            FROM Movimentos
        ), Eventos AS (
            SELECT *, CASE WHEN COMPARAVEL = 0 THEN 'SEM_COMPARACAO'
                WHEN REGULARIZOU = 1 THEN 'REGULARIZADO'
                WHEN INAD_ANTERIOR = 0 AND INAD = 1 AND JA_REGULARIZOU = 1 THEN 'REINCIDENTE'
                WHEN INAD_ANTERIOR = 0 AND INAD = 1 THEN 'ENTROU_INADIMPLENCIA'
                ELSE 'SEM_MUDANCA' END AS EVENTO
            FROM Reincidencias
        )
        """;

    public const string ConvertidosMensais = HistoricoBase + """
        , Cobertura AS (
            SELECT data_base, COUNT_BIG(*) AS VENDAS,
                SUM(CASE WHEN DOCUMENTO IS NULL THEN 1 ELSE 0 END) AS VENDAS_SEM_DOCUMENTO
            FROM VendasMes GROUP BY data_base
        ), Limites AS (
            SELECT MIN(data_base) AS PRIMEIRA_BASE, MAX(data_base) AS ULTIMA_BASE FROM dw.carteira_analitica_hist
        ), Meses AS (
            SELECT EOMONTH(COALESCE(@dataInicio, DATEADD(month,-11,ULTIMA_BASE), GETDATE())) AS MES FROM Limites
            UNION ALL SELECT EOMONTH(DATEADD(month,1,MES)) FROM Meses CROSS JOIN Limites
            WHERE EOMONTH(DATEADD(month,1,MES)) <= COALESCE(@dataFim,ULTIMA_BASE,CONVERT(date,GETDATE()))
        ), Totais AS (
            SELECT data_base, COUNT_BIG(*) AS TOTAL_CLIENTES,
                SUM(CASE WHEN INAD = 0 THEN 1 ELSE 0 END) AS ADIMPLENTES,
                SUM(CASE WHEN INAD = 1 THEN 1 ELSE 0 END) AS INADIMPLENTES,
                SUM(CASE WHEN INAD IS NULL THEN 1 ELSE 0 END) AS SEM_SITUACAO,
                SUM(CASE WHEN EVENTO = 'ENTROU_INADIMPLENCIA' THEN 1 ELSE 0 END) AS NOVOS_INADIMPLENTES,
                SUM(CASE WHEN EVENTO = 'REGULARIZADO' THEN 1 ELSE 0 END) AS REGULARIZADOS,
                SUM(CASE WHEN EVENTO = 'REINCIDENTE' THEN 1 ELSE 0 END) AS REINCIDENTES,
                SUM(CASE WHEN COMPARAVEL = 0 THEN 1 ELSE 0 END) AS SEM_COMPARACAO,
                SUM(CASE WHEN EVENTO IN ('ENTROU_INADIMPLENCIA','REGULARIZADO','REINCIDENTE') THEN 1 ELSE 0 END) AS CONVERTIDOS,
                SUM(CASE WHEN EVENTO = 'ENTROU_INADIMPLENCIA' THEN VALOR ELSE 0 END) AS VALOR_NOVOS,
                SUM(CASE WHEN EVENTO = 'REGULARIZADO' THEN VALOR ELSE 0 END) AS VALOR_REGULARIZADOS,
                SUM(CASE WHEN EVENTO = 'REINCIDENTE' THEN VALOR ELSE 0 END) AS VALOR_REINCIDENTES,
                SUM(CASE WHEN EVENTO IN ('ENTROU_INADIMPLENCIA','REGULARIZADO','REINCIDENTE') AND VALOR IS NULL THEN 1 ELSE 0 END) AS VALORES_INCONSISTENTES
            FROM Eventos GROUP BY data_base
        )
        SELECT CONVERT(varchar(10), m.MES, 23) AS DATA_BASE,
            CONVERT(varchar(10), l.PRIMEIRA_BASE, 23) AS PRIMEIRA_BASE,
            CONVERT(varchar(10), l.ULTIMA_BASE, 23) AS ULTIMA_BASE,
            CASE WHEN cv.VENDAS > 0 THEN 1 ELSE 0 END AS TEM_BASE,
            t.TOTAL_CLIENTES, t.ADIMPLENTES, t.INADIMPLENTES, t.SEM_SITUACAO,
            t.NOVOS_INADIMPLENTES, t.REGULARIZADOS, t.REINCIDENTES, t.CONVERTIDOS, t.SEM_COMPARACAO,
            t.VALOR_NOVOS, t.VALOR_REGULARIZADOS, t.VALOR_REINCIDENTES, t.VALORES_INCONSISTENTES,
            t.VALOR_NOVOS + t.VALOR_REGULARIZADOS + t.VALOR_REINCIDENTES AS VALOR_CONVERTIDOS,
            COALESCE(cv.VENDAS_SEM_DOCUMENTO, 0) AS VENDAS_SEM_DOCUMENTO
        FROM Meses m CROSS JOIN Limites l LEFT JOIN Totais t ON t.data_base=m.MES
        LEFT JOIN Cobertura cv ON cv.data_base=m.MES
        WHERE m.MES >= COALESCE(@dataInicio,m.MES) AND m.MES <= COALESCE(@dataFim,l.ULTIMA_BASE,m.MES)
        ORDER BY m.MES OPTION(MAXRECURSION 1200, RECOMPILE)
        """;

    public const string ConvertidosMensaisDetalhes = HistoricoBase + """
        SELECT CONVERT(varchar(10), e.data_base, 23) AS DATA_REFERENCIA,
            v.NUM_VENDA, v.CLIENTE, e.DOCUMENTO AS CPF_CNPJ, e.EVENTO,
            CASE WHEN e.INAD = 1 THEN 'INADIMPLENTE' WHEN e.INAD = 0 THEN 'ADIMPLENTE' ELSE 'SEM_SITUACAO' END AS SITUACAO,
            CASE WHEN v.VALOR_CONSISTENTE = 1 THEN v.VALOR END AS VALOR_VENDA,
            r.NOME_USUARIO_FK AS RESPONSAVEL, COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM Eventos e JOIN VendasMes v ON v.data_base=e.data_base AND v.DOCUMENTO=e.DOCUMENTO
        LEFT JOIN dbo.VENDA_RESPONSAVEL r ON r.NUM_VENDA_FK=v.NUM_VENDA
        WHERE e.data_base = @dataInicio AND (@situacao IS NULL OR e.EVENTO = @situacao)
        ORDER BY e.DOCUMENTO, v.NUM_VENDA
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;

    // Parcelas PRINCIPAL apenas: exclui o resumo pai quando possui filhas, e garantidores.
    // A baixa concluída pertence à solicitação específica, não a todas as tentativas da parcela.
    public const string Negativadas = """
        WITH Registros AS (
            SELECT s.ID, s.NUM_VENDA_FK, s.NUMERO_PARCELA, s.CONTRACT_NUMBER, s.DOCUMENTO_DEVEDOR,
                s.VALOR, s.DT_CRIACAO,
                CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.SERASA_PEFIN_BAIXAS b
                    WHERE b.ID_SOLICITACAO_NEGATIVACAO = s.ID AND b.STATUS = 'BAIXADO_SUCESSO'
                ) THEN 'BAIXADO_SUCESSO' ELSE s.STATUS END AS STATUS
            FROM dbo.SERASA_PEFIN_SOLICITACOES s
            WHERE s.TIPO_REGISTRO = 'PRINCIPAL'
              AND (s.NUMERO_PARCELA IS NOT NULL OR NOT EXISTS (
                  SELECT 1 FROM dbo.SERASA_PEFIN_SOLICITACOES filha
                  WHERE filha.ID_SOLICITACAO_PAI = s.ID AND filha.TIPO_REGISTRO = 'PRINCIPAL'
              ))
        ), Ordenados AS (
            SELECT *, ROW_NUMBER() OVER (
                PARTITION BY NUM_VENDA_FK, CONTRACT_NUMBER, DOCUMENTO_DEVEDOR,
                    COALESCE(CONVERT(varchar(40), NUMERO_PARCELA), CONVERT(varchar(40), ID))
                ORDER BY CASE STATUS WHEN 'NEGATIVADO_SUCESSO' THEN 100 WHEN 'BAIXA_AGUARDANDO_RETORNO' THEN 90
                    WHEN 'BAIXA_ENVIADA' THEN 80 WHEN 'AGUARDANDO_RETORNO' THEN 70 WHEN 'ENVIADO_SERASA' THEN 60
                    WHEN 'PENDENTE_ENVIO' THEN 50 WHEN 'APROVADA' THEN 40 WHEN 'AGUARDANDO_APROVACAO' THEN 30 ELSE 0 END DESC,
                    DT_CRIACAO DESC, ID DESC
            ) AS RN
            FROM Registros
        ), Vigentes AS (
            SELECT * FROM Ordenados WHERE RN = 1
                AND (@dataInicio IS NULL OR DT_CRIACAO >= @dataInicio)
                AND (@dataFim IS NULL OR DT_CRIACAO < DATEADD(day, 1, @dataFim))
        )
        SELECT STATUS, COUNT(DISTINCT NUM_VENDA_FK) AS VENDAS, COUNT_BIG(*) AS REGISTROS,
            SUM(CAST(VALOR AS decimal(19,2))) AS VALOR,
            (SELECT COUNT(DISTINCT NUM_VENDA_FK) FROM Vigentes) AS TOTAL_VENDAS,
            (SELECT COUNT(DISTINCT NUM_VENDA_FK) FROM Vigentes WHERE STATUS = 'NEGATIVADO_SUCESSO') AS VENDAS_NEGATIVADAS,
            (SELECT COALESCE(SUM(CAST(VALOR AS decimal(19,2))), 0) FROM Vigentes) AS VALOR_TOTAL
        FROM Vigentes GROUP BY STATUS ORDER BY STATUS
        """;

    public const string OcorrenciasDiaDetalhes = """
        SELECT o.ID, o.NUM_VENDA_FK, o.NOME_USUARIO_FK, o.DESCRICAO, o.STATUS_OCORRENCIA,
            o.DT_OCORRENCIA, o.HORA_OCORRENCIA, o.PROXIMA_ACAO, o.PROTOCOLO,
            COUNT_BIG(*) OVER() AS TOTAL_COUNT
        FROM dbo.OCORRENCIAS o
        WHERE o.DT_OCORRENCIA >= @dataInicio AND o.DT_OCORRENCIA < DATEADD(day, 1, @dataInicio)
            AND EXISTS (SELECT 1 FROM DW.fat_analise_inadimplencia_v4 f
                WHERE f.NUM_VENDA = o.NUM_VENDA_FK
                    AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM')
        ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.ID DESC
        OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
        """;
}

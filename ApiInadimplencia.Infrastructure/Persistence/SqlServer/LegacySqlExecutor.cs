using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Infrastructure.Configuration;
using Dapper;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Executes selected SQL Server operations from the source Node module using parameterized SQL.
/// </summary>
/// <param name="connectionFactory">SQL connection factory.</param>
/// <param name="options">SQL Server options.</param>
public sealed class LegacySqlExecutor(
    SqlServerConnectionFactory connectionFactory,
    IOptions<SqlServerOptions> options) : ILegacySqlExecutor
{
    private static readonly IReadOnlyDictionary<string, SqlDefinition> Queries = BuildQueries();
    private static readonly IReadOnlyDictionary<string, SqlDefinition> Commands = new Dictionary<string, SqlDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly SqlServerConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly SqlServerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public bool IsConfigured => _connectionFactory.IsConfigured;

    /// <inheritdoc />
    public async Task<LegacySqlResult> QueryAsync(
        string queryKey,
        IReadOnlyDictionary<string, object?> parameters,
        bool single,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new LegacySqlResult(false, null);
        }

        if (!Queries.TryGetValue(queryKey, out var definition))
        {
            throw new NotImplementedException($"SQL query '{queryKey}' has not been migrated yet.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            definition.Sql,
            ToDynamicParameters(parameters),
            commandTimeout: _options.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync(command).ConfigureAwait(false);
        var materialized = rows.Select(ToDictionary).ToList();
        object? data = single ? materialized.FirstOrDefault() : materialized;

        return new LegacySqlResult(true, data);
    }

    /// <inheritdoc />
    public async Task<LegacySqlResult> ExecuteAsync(
        string commandKey,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new LegacySqlResult(false, null);
        }

        if (!Commands.TryGetValue(commandKey, out var definition))
        {
            throw new NotImplementedException($"SQL command '{commandKey}' has not been migrated yet.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            definition.Sql,
            ToDynamicParameters(parameters),
            commandTimeout: _options.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
        return new LegacySqlResult(true, null, rowsAffected);
    }

    private static DynamicParameters ToDynamicParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var dynamicParameters = new DynamicParameters();
        foreach (var (key, value) in parameters)
        {
            dynamicParameters.Add(key, value);
        }

        return dynamicParameters;
    }

    private static Dictionary<string, object?> ToDictionary(dynamic row)
    {
        var source = (IDictionary<string, object?>)row;
        return new Dictionary<string, object?>(source, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, SqlDefinition> BuildQueries()
    {
        const string latestAcaoApply = """
            OUTER APPLY (
                SELECT TOP 1 o.PROXIMA_ACAO
                FROM dbo.OCORRENCIAS o
                WHERE o.NUM_VENDA_FK = f.NUM_VENDA
                  AND o.PROXIMA_ACAO IS NOT NULL
                ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
            ) ultima_acao
            """;

        const string selectInadimplencia = """
            f.CLIENTE,
            f.CPF_CNPJ,
            f.NUM_VENDA,
            f.EMPREENDIMENTO,
            f.BLOCO,
            f.UNIDADE,
            f.QTD_PARCELAS_INADIMPLENTES,
            f.STATUS_REPASSE,
            f.SCORE,
            f.SUGESTAO,
            f.VENCIMENTO_MAIS_ANTIGO,
            f.VALOR_TOTAL_EM_ABERTO,
            f.VALOR_INADIMPLENTE,
            f.VALOR_NAO_CONTRATUAL_INAD,
            f.VALOR_POUPANCA_INAD,
            ultima_acao.PROXIMA_ACAO
            """;

        var queries = new Dictionary<string, SqlDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inadimplencia.List"] = new($"""
                SELECT {selectInadimplencia}
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                """),

            ["Inadimplencia.ByCpf"] = new($"""
                SELECT {selectInadimplencia}
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE REPLACE(REPLACE(REPLACE(f.CPF_CNPJ, '.', ''), '-', ''), '/', '') = @cpf
                """),

            ["Inadimplencia.ByNumVenda"] = new($"""
                SELECT TOP 1 {selectInadimplencia}
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE f.NUM_VENDA = @numVenda
                """),

            ["Inadimplencia.ByResponsavel"] = new($"""
                SELECT {selectInadimplencia},
                       vr.NOME_USUARIO_FK AS RESPONSAVEL,
                       u.COR_HEX AS RESPONSAVEL_COR_HEX
                FROM DW.fat_analise_inadimplencia_v4 f
                INNER JOIN dbo.VENDA_RESPONSAVEL vr ON vr.NUM_VENDA_FK = f.NUM_VENDA
                LEFT JOIN dbo.USUARIO u ON u.NOME = vr.NOME_USUARIO_FK
                {latestAcaoApply}
                WHERE vr.NOME_USUARIO_FK = @nome
                """),

            ["Inadimplencia.ByCliente"] = new($"""
                SELECT {selectInadimplencia}
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE f.CLIENTE LIKE '%' + @nomeCliente + '%'
                """),

            ["ProximasAcoes.List"] = new("""
                WITH Ranked AS (
                    SELECT
                        o.NUM_VENDA_FK,
                        o.PROXIMA_ACAO,
                        ROW_NUMBER() OVER (
                            PARTITION BY o.NUM_VENDA_FK
                            ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
                        ) AS RN
                    FROM dbo.OCORRENCIAS o
                    WHERE o.PROXIMA_ACAO IS NOT NULL
                )
                SELECT ranked.NUM_VENDA_FK AS NUM_VENDA, ranked.PROXIMA_ACAO
                FROM Ranked ranked
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = ranked.NUM_VENDA_FK
                WHERE ranked.RN = 1
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                ORDER BY ranked.PROXIMA_ACAO ASC
                """),

            ["ProximasAcoes.ByNumVenda"] = new("""
                SELECT TOP 1 o.NUM_VENDA_FK AS NUM_VENDA, o.PROXIMA_ACAO
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE o.NUM_VENDA_FK = @numVenda
                  AND o.PROXIMA_ACAO IS NOT NULL
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
                """),

            ["Usuarios.List"] = new("SELECT * FROM dbo.USUARIO ORDER BY NOME"),
            ["Usuarios.ByNome"] = new("SELECT TOP 1 * FROM dbo.USUARIO WHERE NOME = @nome"),

            ["Responsaveis.List"] = new("""
                SELECT vr.NUM_VENDA_FK,
                       vr.NOME_USUARIO_FK,
                       vr.DT_ATRIBUICAO,
                       f.CLIENTE,
                       f.CPF_CNPJ,
                       f.EMPREENDIMENTO,
                       f.VALOR_INADIMPLENTE,
                       f.SCORE,
                       u.COR_HEX
                FROM dbo.VENDA_RESPONSAVEL vr
                LEFT JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = vr.NUM_VENDA_FK
                LEFT JOIN dbo.USUARIO u ON u.NOME = vr.NOME_USUARIO_FK
                ORDER BY vr.DT_ATRIBUICAO DESC
                """),

            ["Responsaveis.ByNumVenda"] = new("""
                SELECT TOP 1 vr.NUM_VENDA_FK,
                       vr.NOME_USUARIO_FK,
                       vr.DT_ATRIBUICAO,
                       f.CLIENTE,
                       f.CPF_CNPJ,
                       f.EMPREENDIMENTO,
                       f.VALOR_INADIMPLENTE,
                       f.SCORE,
                       u.COR_HEX
                FROM dbo.VENDA_RESPONSAVEL vr
                LEFT JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = vr.NUM_VENDA_FK
                LEFT JOIN dbo.USUARIO u ON u.NOME = vr.NOME_USUARIO_FK
                WHERE vr.NUM_VENDA_FK = @numVenda
                """),

            ["KanbanStatus.List"] = new("""
                WITH UltimoKanban AS (
                    SELECT
                        ks.NUM_VENDA_FK,
                        ks.PROXIMA_ACAO,
                        ks.STATUS,
                        ks.STATUS_DATA,
                        ks.NOME_USUARIO_FK,
                        ks.DT_ATUALIZACAO,
                        ROW_NUMBER() OVER (
                            PARTITION BY ks.NUM_VENDA_FK, ks.PROXIMA_ACAO
                            ORDER BY ks.DT_ATUALIZACAO DESC
                        ) AS RN
                    FROM dbo.KANBAN_STATUS ks
                )
                SELECT NUM_VENDA_FK, PROXIMA_ACAO, STATUS, STATUS_DATA, NOME_USUARIO_FK, DT_ATUALIZACAO
                FROM UltimoKanban
                WHERE RN = 1
                ORDER BY DT_ATUALIZACAO DESC
                """),

            ["Fiadores.ByNumVenda"] = new("""
                SELECT ID_ASSOCIADO,
                       ID_RESERVA,
                       ID_PESSOA,
                       NOME,
                       DOCUMENTO,
                       DATA_CADASTRO,
                       RENDA_FAMILIAR,
                       TIPO_ASSOCIACAO,
                       NUM_VENDA,
                       ENDERECO
                FROM DW.vw_fiadores_por_venda
                WHERE NUM_VENDA = @numVenda
                ORDER BY DATA_CADASTRO DESC, NOME ASC
                """),

            ["Fiadores.ByCpf"] = new("""
                SELECT ID_ASSOCIADO,
                       ID_RESERVA,
                       ID_PESSOA,
                       NOME,
                       DOCUMENTO,
                       DATA_CADASTRO,
                       RENDA_FAMILIAR,
                       TIPO_ASSOCIACAO,
                       NUM_VENDA,
                       ENDERECO
                FROM DW.vw_fiadores_por_venda
                WHERE REPLACE(REPLACE(REPLACE(DOCUMENTO, '.', ''), '-', ''), '/', '') = @cpf
                ORDER BY DATA_CADASTRO DESC, NOME ASC
                """),

            ["Dashboard.Kpis"] = new("""
                SELECT
                    COUNT(DISTINCT f.NUM_VENDA) AS TOTAL_VENDAS,
                    COUNT(DISTINCT f.CPF_CNPJ) AS TOTAL_CLIENTES,
                    SUM(COALESCE(f.VALOR_TOTAL_EM_ABERTO, 0)) AS SALDO_TOTAL,
                    SUM(COALESCE(f.VALOR_INADIMPLENTE, 0)) AS VALOR_INADIMPLENTE,
                    CASE
                        WHEN SUM(COALESCE(f.VALOR_TOTAL_EM_ABERTO, 0)) = 0 THEN 0
                        ELSE SUM(COALESCE(f.VALOR_INADIMPLENTE, 0)) * 100.0 / SUM(COALESCE(f.VALOR_TOTAL_EM_ABERTO, 0))
                    END AS PERCENTUAL_INADIMPLENCIA
                FROM DW.fat_analise_inadimplencia_v4 f
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                """),

            ["Dashboard.OcorrenciasPorUsuario"] = new("""
                SELECT
                    o.NOME_USUARIO_FK AS USUARIO,
                    COUNT(*) AS QTD_OCORRENCIAS
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                GROUP BY o.NOME_USUARIO_FK
                ORDER BY QTD_OCORRENCIAS DESC
                """),

            ["Dashboard.OcorrenciasPorVenda"] = new("""
                SELECT
                    o.NUM_VENDA_FK AS NUM_VENDA,
                    f.CLIENTE,
                    COUNT(*) AS QTD_OCORRENCIAS
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                GROUP BY o.NUM_VENDA_FK, f.CLIENTE
                ORDER BY QTD_OCORRENCIAS DESC
                """),

            ["Dashboard.OcorrenciasPorDia"] = new("""
                SELECT
                    CONVERT(date, o.DT_OCORRENCIA) AS DIA,
                    COUNT(*) AS QTD_OCORRENCIAS
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND (@dataInicio IS NULL OR o.DT_OCORRENCIA >= @dataInicio)
                  AND (@dataFim IS NULL OR o.DT_OCORRENCIA <= @dataFim)
                GROUP BY CONVERT(date, o.DT_OCORRENCIA)
                ORDER BY DIA DESC
                """),

            ["Dashboard.OcorrenciasPorHora"] = new("""
                SELECT
                    DATEPART(HOUR, o.HORA_OCORRENCIA) AS HORA,
                    COUNT(*) AS QTD_OCORRENCIAS
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND (@dataInicio IS NULL OR o.DT_OCORRENCIA >= @dataInicio)
                  AND (@dataFim IS NULL OR o.DT_OCORRENCIA <= @dataFim)
                GROUP BY DATEPART(HOUR, o.HORA_OCORRENCIA)
                ORDER BY HORA
                """),

            ["Dashboard.OcorrenciasPorDiaHora"] = new("""
                SELECT
                    CONVERT(date, o.DT_OCORRENCIA) AS DIA,
                    DATEPART(HOUR, o.HORA_OCORRENCIA) AS HORA,
                    COUNT(*) AS QTD_OCORRENCIAS
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND (@dataInicio IS NULL OR o.DT_OCORRENCIA >= @dataInicio)
                  AND (@dataFim IS NULL OR o.DT_OCORRENCIA <= @dataFim)
                GROUP BY CONVERT(date, o.DT_OCORRENCIA), DATEPART(HOUR, o.HORA_OCORRENCIA)
                ORDER BY DIA DESC, HORA
                """),

            ["Dashboard.ProximasAcoesPorDia"] = new("""
                WITH Ranked AS (
                    SELECT
                        o.NUM_VENDA_FK,
                        o.PROXIMA_ACAO,
                        o.DT_OCORRENCIA,
                        ROW_NUMBER() OVER (
                            PARTITION BY o.NUM_VENDA_FK
                            ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
                        ) AS RN
                    FROM dbo.OCORRENCIAS o
                    WHERE o.PROXIMA_ACAO IS NOT NULL
                )
                SELECT
                    CONVERT(r.DT_OCORRENCIA, date) AS DIA,
                    r.PROXIMA_ACAO,
                    COUNT(DISTINCT r.NUM_VENDA_FK) AS QTD_VENDAS
                FROM Ranked r
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = r.NUM_VENDA_FK
                WHERE r.RN = 1
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND (@dataInicio IS NULL OR r.DT_OCORRENCIA >= @dataInicio)
                  AND (@dataFim IS NULL OR r.DT_OCORRENCIA <= @dataFim)
                GROUP BY CONVERT(r.DT_OCORRENCIA, date), r.PROXIMA_ACAO
                ORDER BY DIA DESC, r.PROXIMA_ACAO
                """),

            ["Dashboard.AcoesDefinidas"] = new("""
                SELECT DISTINCT
                    o.PROXIMA_ACAO,
                    COUNT(*) OVER (PARTITION BY o.PROXIMA_ACAO) AS QTD
                FROM dbo.OCORRENCIAS o
                WHERE o.PROXIMA_ACAO IS NOT NULL
                ORDER BY PROXIMA_ACAO
                """),

            ["Dashboard.AtendentesPorProximaAcao"] = new("""
                WITH Ranked AS (
                    SELECT
                        o.NUM_VENDA_FK,
                        o.PROXIMA_ACAO,
                        o.NOME_USUARIO_FK,
                        ROW_NUMBER() OVER (
                            PARTITION BY o.NUM_VENDA_FK
                            ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
                        ) AS RN
                    FROM dbo.OCORRENCIAS o
                    WHERE o.PROXIMA_ACAO IS NOT NULL
                )
                SELECT
                    r.PROXIMA_ACAO,
                    r.NOME_USUARIO_FK AS ATENDENTE,
                    COUNT(DISTINCT r.NUM_VENDA_FK) AS QTD_VENDAS
                FROM Ranked r
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = r.NUM_VENDA_FK
                WHERE r.RN = 1
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                GROUP BY r.PROXIMA_ACAO, r.NOME_USUARIO_FK
                ORDER BY r.PROXIMA_ACAO, QTD_VENDAS DESC
                """),

            ["Dashboard.Aging"] = new("""
                SELECT
                    CASE
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 0 AND 30 THEN '0-30'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 31 AND 60 THEN '31-60'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 61 AND 90 THEN '61-90'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 91 AND 120 THEN '91-120'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 121 AND 180 THEN '121-180'
                        ELSE '181+'
                    END AS FAIXA,
                    COUNT(DISTINCT f.NUM_VENDA) AS QTD_VENDAS,
                    SUM(COALESCE(f.VALOR_INADIMPLENTE, 0)) AS VALOR_TOTAL
                FROM DW.fat_analise_inadimplencia_v4 f
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL
                GROUP BY
                    CASE
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 0 AND 30 THEN '0-30'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 31 AND 60 THEN '31-60'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 61 AND 90 THEN '61-90'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 91 AND 120 THEN '91-120'
                        WHEN DATEDIFF(DAY, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 121 AND 180 THEN '121-180'
                        ELSE '181+'
                    END
                ORDER BY
                    CASE FAIXA
                        WHEN '0-30' THEN 1
                        WHEN '31-60' THEN 2
                        WHEN '61-90' THEN 3
                        WHEN '91-120' THEN 4
                        WHEN '121-180' THEN 5
                        ELSE 6
                    END
                """),

            ["Dashboard.ParcelasInadimplentes"] = new("""
                SELECT
                    f.NUM_VENDA,
                    f.CLIENTE,
                    f.CPF_CNPJ,
                    f.EMPREENDIMENTO,
                    f.BLOCO,
                    f.UNIDADE,
                    f.QTD_PARCELAS_INADIMPLENTES,
                    f.VALOR_INADIMPLENTE,
                    f.SCORE,
                    f.VENCIMENTO_MAIS_ANTIGO
                FROM DW.fat_analise_inadimplencia_v4 f
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND f.QTD_PARCELAS_INADIMPLENTES > 0
                ORDER BY f.QTD_PARCELAS_INADIMPLENTES DESC, f.VALOR_INADIMPLENTE DESC
                OFFSET 0 ROWS
                FETCH NEXT @limit ROWS ONLY
                """),

            ["Dashboard.ScoreSaldo"] = new("""
                SELECT
                    f.SCORE,
                    COUNT(DISTINCT f.NUM_VENDA) AS QTD_VENDAS,
                    SUM(COALESCE(f.VALOR_INADIMPLENTE, 0)) AS VALOR_TOTAL,
                    AVG(COALESCE(f.VALOR_INADIMPLENTE, 0)) AS VALOR_MEDIO
                FROM DW.fat_analise_inadimplencia_v4 f
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND f.SCORE IS NOT NULL
                GROUP BY f.SCORE
                ORDER BY f.SCORE
                """),

            ["Dashboard.SaldoPorMesVencimento"] = new("""
                SELECT
                    FORMAT(f.VENCIMENTO_MAIS_ANTIGO, 'yyyy-MM') AS MES_VENCIMENTO,
                    COUNT(DISTINCT f.NUM_VENDA) AS QTD_VENDAS,
                    SUM(COALESCE(f.VALOR_INADIMPLENTE, 0)) AS VALOR_TOTAL
                FROM DW.fat_analise_inadimplencia_v4 f
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL
                GROUP BY FORMAT(f.VENCIMENTO_MAIS_ANTIGO, 'yyyy-MM')
                ORDER BY MES_VENCIMENTO DESC
                """),

            ["Dashboard.PerfilRiscoEmpreendimento"] = new("""
                SELECT
                    f.EMPREENDIMENTO,
                    f.SCORE,
                    COUNT(DISTINCT f.NUM_VENDA) AS QTD_VENDAS,
                    SUM(COALESCE(f.VALOR_INADIMPLENTE, 0)) AS VALOR_TOTAL
                FROM DW.fat_analise_inadimplencia_v4 f
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND f.EMPREENDIMENTO IS NOT NULL
                  AND f.SCORE IS NOT NULL
                GROUP BY f.EMPREENDIMENTO, f.SCORE
                ORDER BY f.EMPREENDIMENTO, f.SCORE
                """),

            ["Notifications.List"] = new("""
                ;WITH CountCTE AS (
                    SELECT COUNT(*) AS Total
                    FROM dbo.INAD_NOTIFICACOES n
                    WHERE n.USUARIO_DESTINATARIO = @username
                      AND n.DT_EXCLUSAO IS NULL
                      AND (@lida IS NULL OR n.LIDA = @lida)
                ),
                UnreadCTE AS (
                    SELECT COUNT(*) AS UnreadCount
                    FROM dbo.INAD_NOTIFICACOES n
                    WHERE n.USUARIO_DESTINATARIO = @username
                      AND n.LIDA = 0
                      AND n.DT_EXCLUSAO IS NULL
                )
                SELECT n.ID,
                       n.TIPO,
                       n.USUARIO_DESTINATARIO,
                       n.ORIGEM_USUARIO,
                       n.NUM_VENDA,
                       f.SCORE,
                       n.PROXIMA_ACAO,
                       n.PAYLOAD,
                       n.LIDA,
                       n.DT_CRIACAO,
                       n.DT_LEITURA,
                       n.DT_EXCLUSAO,
                       c.Total,
                       u.UnreadCount
                FROM dbo.INAD_NOTIFICACOES n
                LEFT JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = n.NUM_VENDA
                CROSS JOIN CountCTE c
                CROSS JOIN UnreadCTE u
                WHERE n.USUARIO_DESTINATARIO = @username
                  AND n.DT_EXCLUSAO IS NULL
                  AND (@lida IS NULL OR n.LIDA = @lida)
                ORDER BY n.LIDA ASC, n.DT_CRIACAO DESC
                OFFSET @offset ROWS
                FETCH NEXT @pageSize ROWS ONLY
                """),

            ["SerasaPefin.HistoryByNumVenda"] = new("""
                SELECT *
                FROM dbo.SERASA_PEFIN_SOLICITACOES
                WHERE NUM_VENDA_FK = @numVenda
                ORDER BY DT_CRIACAO DESC
                """),

            ["SerasaPefin.ById"] = new("""
                SELECT TOP 1 *
                FROM dbo.SERASA_PEFIN_SOLICITACOES
                WHERE ID = @id
                """),

            ["SerasaPefin.ByTransactionId"] = new("""
                SELECT TOP 1 *
                FROM dbo.SERASA_PEFIN_SOLICITACOES
                WHERE TRANSACTION_ID = @transactionId
                """),
        };

        return queries;
    }

    private sealed record SqlDefinition(string Sql);
}


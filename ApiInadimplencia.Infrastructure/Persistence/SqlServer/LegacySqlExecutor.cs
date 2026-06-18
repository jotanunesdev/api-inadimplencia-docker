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
    private static readonly IReadOnlyDictionary<string, SqlDefinition> Commands = BuildCommands();
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

        if (definition.ReturnsRows)
        {
            var rows = await connection.QueryAsync(command).ConfigureAwait(false);
            var materialized = rows.Select(ToDictionary).ToList();
            return new LegacySqlResult(true, materialized.FirstOrDefault(), materialized.Count);
        }

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
                SELECT {selectInadimplencia},
                       COUNT_BIG(1) OVER() AS TOTAL_COUNT
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                ORDER BY TRY_CAST(f.VALOR_TOTAL_EM_ABERTO AS float) DESC, f.NUM_VENDA DESC
                OFFSET @offset ROWS
                FETCH NEXT @pageSize ROWS ONLY
                """),

            ["Inadimplencia.ByCpf"] = new($"""
                SELECT {selectInadimplencia},
                       COUNT_BIG(1) OVER() AS TOTAL_COUNT
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE REPLACE(REPLACE(REPLACE(f.CPF_CNPJ, '.', ''), '-', ''), '/', '') = @cpf
                ORDER BY TRY_CAST(f.VALOR_TOTAL_EM_ABERTO AS float) DESC, f.NUM_VENDA DESC
                OFFSET @offset ROWS
                FETCH NEXT @pageSize ROWS ONLY
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
                       u.COR_HEX AS RESPONSAVEL_COR_HEX,
                       COUNT_BIG(1) OVER() AS TOTAL_COUNT
                FROM DW.fat_analise_inadimplencia_v4 f
                INNER JOIN dbo.VENDA_RESPONSAVEL vr ON vr.NUM_VENDA_FK = f.NUM_VENDA
                LEFT JOIN dbo.USUARIO u ON u.NOME = vr.NOME_USUARIO_FK
                {latestAcaoApply}
                WHERE vr.NOME_USUARIO_FK = @nome
                ORDER BY TRY_CAST(f.VALOR_TOTAL_EM_ABERTO AS float) DESC, f.NUM_VENDA DESC
                OFFSET @offset ROWS
                FETCH NEXT @pageSize ROWS ONLY
                """),

            ["Inadimplencia.ByCliente"] = new($"""
                SELECT {selectInadimplencia},
                       COUNT_BIG(1) OVER() AS TOTAL_COUNT
                FROM DW.fat_analise_inadimplencia_v4 f
                {latestAcaoApply}
                WHERE f.CLIENTE LIKE '%' + @nomeCliente + '%'
                ORDER BY TRY_CAST(f.VALOR_TOTAL_EM_ABERTO AS float) DESC, f.NUM_VENDA DESC
                OFFSET @offset ROWS
                FETCH NEXT @pageSize ROWS ONLY
                """),

            // Calendar view rule: per cliente, exibir apenas a PROXIMA_ACAO mais distante
            // (MAX(PROXIMA_ACAO)). Ex.: cliente com acoes em 27/05, 29/05 e 02/06 -> 02/06.
            // DT_OCORRENCIA/HORA_OCORRENCIA sao tiebreakers quando duas ocorrencias
            // agendam a mesma data de proxima acao.
            ["ProximasAcoes.List"] = new("""
                WITH Ranked AS (
                    SELECT
                        o.NUM_VENDA_FK,
                        o.PROXIMA_ACAO,
                        ROW_NUMBER() OVER (
                            PARTITION BY o.NUM_VENDA_FK
                            ORDER BY o.PROXIMA_ACAO DESC, o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC
                        ) AS RN
                    FROM dbo.OCORRENCIAS o
                    WHERE o.PROXIMA_ACAO IS NOT NULL
                )
                SELECT ranked.NUM_VENDA_FK AS NUM_VENDA,
                       ranked.PROXIMA_ACAO,
                       COUNT_BIG(1) OVER() AS TOTAL_COUNT
                FROM Ranked ranked
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = ranked.NUM_VENDA_FK
                WHERE ranked.RN = 1
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                ORDER BY ranked.PROXIMA_ACAO ASC
                OFFSET @offset ROWS
                FETCH NEXT @pageSize ROWS ONLY
                """),

            // Mesma regra de selecao da Lista: prioriza a PROXIMA_ACAO mais distante.
            ["ProximasAcoes.ByNumVenda"] = new("""
                SELECT TOP 1 o.NUM_VENDA_FK AS NUM_VENDA, o.PROXIMA_ACAO
                FROM dbo.OCORRENCIAS o
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = o.NUM_VENDA_FK
                WHERE o.NUM_VENDA_FK = @numVenda
                  AND o.PROXIMA_ACAO IS NOT NULL
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                ORDER BY o.PROXIMA_ACAO DESC, o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC
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
                        o.NOME_USUARIO_FK,
                        o.DT_OCORRENCIA,
                        o.HORA_OCORRENCIA,
                        ROW_NUMBER() OVER (
                            PARTITION BY o.NUM_VENDA_FK
                            ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
                        ) AS RN
                    FROM dbo.OCORRENCIAS o
                    WHERE o.PROXIMA_ACAO IS NOT NULL
                )
                SELECT
                    TRY_CONVERT(date, r.PROXIMA_ACAO) AS DATA,
                    COUNT(DISTINCT r.NUM_VENDA_FK) AS TOTAL
                FROM Ranked r
                INNER JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = r.NUM_VENDA_FK
                WHERE r.RN = 1
                  AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
                  AND (@dataInicio IS NULL OR r.DT_OCORRENCIA >= @dataInicio)
                  AND (@dataFim IS NULL OR r.DT_OCORRENCIA <= @dataFim)
                  AND (@nomeUsuario IS NULL OR LOWER(LTRIM(RTRIM(COALESCE(r.NOME_USUARIO_FK, '')))) = @nomeUsuario)
                GROUP BY TRY_CONVERT(date, r.PROXIMA_ACAO)
                ORDER BY DATA
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

            ["Ocorrencia.List"] = new("""
                SELECT ID,
                       NUM_VENDA_FK,
                       NOME_USUARIO_FK,
                       DESCRICAO,
                       STATUS_OCORRENCIA,
                       DT_OCORRENCIA,
                       HORA_OCORRENCIA,
                       PROXIMA_ACAO,
                       PROTOCOLO
                FROM dbo.OCORRENCIAS
                ORDER BY DT_OCORRENCIA DESC, HORA_OCORRENCIA DESC
                """),

            ["Ocorrencia.GetById"] = new("""
                SELECT TOP 1 ID,
                       NUM_VENDA_FK,
                       NOME_USUARIO_FK,
                       DESCRICAO,
                       STATUS_OCORRENCIA,
                       DT_OCORRENCIA,
                       HORA_OCORRENCIA,
                       PROXIMA_ACAO,
                       PROTOCOLO
                FROM dbo.OCORRENCIAS
                WHERE ID = @id
                """),

            ["Ocorrencia.ByNumVenda"] = new("""
                SELECT ID,
                       NUM_VENDA_FK,
                       NOME_USUARIO_FK,
                       DESCRICAO,
                       STATUS_OCORRENCIA,
                       DT_OCORRENCIA,
                       HORA_OCORRENCIA,
                       PROXIMA_ACAO,
                       PROTOCOLO
                FROM dbo.OCORRENCIAS
                WHERE NUM_VENDA_FK = @numVenda
                ORDER BY DT_OCORRENCIA DESC, HORA_OCORRENCIA DESC
                """),

            ["Ocorrencia.ByProtocolo"] = new("""
                SELECT ID,
                       NUM_VENDA_FK,
                       NOME_USUARIO_FK,
                       DESCRICAO,
                       STATUS_OCORRENCIA,
                       DT_OCORRENCIA,
                       HORA_OCORRENCIA,
                       PROXIMA_ACAO,
                       PROTOCOLO
                FROM dbo.OCORRENCIAS
                WHERE PROTOCOLO = @protocolo
                ORDER BY DT_OCORRENCIA DESC, HORA_OCORRENCIA DESC
                """),

            ["Atendimento.ByProtocolo"] = new("""
                SELECT TOP 1 *
                FROM dbo.ATENDIMENTOS
                WHERE PROTOCOLO = @protocolo
                """),

            ["Atendimento.ByCpf"] = new("""
                SELECT *
                FROM dbo.ATENDIMENTOS
                WHERE REPLACE(REPLACE(REPLACE(CPF_CNPJ, '.', ''), '-', ''), '/', '') = @cpf
                ORDER BY CRIADO_EM DESC
                """),

            ["Atendimento.ByNumVenda"] = new("""
                SELECT *
                FROM dbo.ATENDIMENTOS
                WHERE NUM_VENDA_FK = @numVenda
                ORDER BY CRIADO_EM DESC
                """),

            ["Atendimento.ByCliente"] = new("""
                SELECT *
                FROM dbo.ATENDIMENTOS
                WHERE CLIENTE LIKE '%' + @nomeCliente + '%'
                ORDER BY CRIADO_EM DESC
                """),
        };

        ApplyDashboardQueries(queries);

        return queries;
    }

    private static void ApplyDashboardQueries(Dictionary<string, SqlDefinition> queries)
    {
        queries["Dashboard.Kpis"] = new("""
            SELECT
                COUNT(*) AS TOTAL_VENDAS,
                COUNT(DISTINCT f.CPF_CNPJ) AS TOTAL_CLIENTES,
                SUM(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)) AS TOTAL_SALDO,
                SUM(CAST(f.VALOR_INADIMPLENTE AS float)) AS TOTAL_INADIMPLENTE,
                CAST(
                    CASE WHEN SUM(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)) = 0 THEN 0
                    ELSE (100.0 * SUM(CAST(f.VALOR_INADIMPLENTE AS float)) / SUM(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)))
                    END AS decimal(10,2)
                ) AS PERC_INADIMPLENTE
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            """);

        queries["Dashboard.VendasPorResponsavel"] = new("""
            SELECT
                u.NOME AS RESPONSAVEL,
                COUNT(vr_inad.NUM_VENDA_FK) AS TOTAL_VENDAS,
                u.COR_HEX
            FROM dbo.USUARIO u
            LEFT JOIN (
                SELECT DISTINCT vr.NUM_VENDA_FK, vr.NOME_USUARIO_FK
                FROM dbo.VENDA_RESPONSAVEL vr
                INNER JOIN DW.fat_analise_inadimplencia_v4 fat ON fat.NUM_VENDA = vr.NUM_VENDA_FK
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
                  AND (@dataInicio IS NULL OR @dataFim IS NULL OR (
                      fat.VENCIMENTO_MAIS_ANTIGO IS NOT NULL
                      AND fat.VENCIMENTO_MAIS_ANTIGO BETWEEN @dataInicio AND @dataFim
                  ))
            ) vr_inad ON vr_inad.NOME_USUARIO_FK = u.NOME
            GROUP BY u.NOME, u.COR_HEX
            ORDER BY TOTAL_VENDAS DESC
            """);

        queries["Dashboard.Responsaveis"] = new("SELECT * FROM dbo.VENDA_RESPONSAVEL");

        queries["Dashboard.InadimplenciaPorEmpreendimento"] = new("""
            SELECT
                COALESCE(f.EMPREENDIMENTO, 'Nao informado') AS EMPREENDIMENTO,
                COUNT(*) AS TOTAL_VENDAS,
                SUM(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)) AS TOTAL_SALDO,
                SUM(CAST(f.VALOR_INADIMPLENTE AS float)) AS TOTAL_INADIMPLENTE
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY COALESCE(f.EMPREENDIMENTO, 'Nao informado')
            ORDER BY TOTAL_SALDO DESC
            """);

        queries["Dashboard.ClientesPorEmpreendimento"] = new("""
            SELECT
                COALESCE(f.EMPREENDIMENTO, 'Nao informado') AS EMPREENDIMENTO,
                COUNT(DISTINCT f.CPF_CNPJ) AS TOTAL_CLIENTES
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY COALESCE(f.EMPREENDIMENTO, 'Nao informado')
            ORDER BY TOTAL_CLIENTES DESC
            """);

        queries["Dashboard.StatusRepasse"] = new("""
            SELECT
                COALESCE(f.STATUS_REPASSE, 'Nao informado') AS STATUS_REPASSE,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY COALESCE(f.STATUS_REPASSE, 'Nao informado')
            ORDER BY TOTAL DESC
            """);

        queries["Dashboard.Blocos"] = new("""
            SELECT
                COALESCE(f.EMPREENDIMENTO, 'Nao informado') AS EMPREENDIMENTO,
                COALESCE(f.BLOCO, 'Nao informado') AS BLOCO,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY COALESCE(f.EMPREENDIMENTO, 'Nao informado'), COALESCE(f.BLOCO, 'Nao informado')
            ORDER BY TOTAL DESC
            """);

        queries["Dashboard.Unidades"] = new("""
            SELECT
                COALESCE(f.EMPREENDIMENTO, 'Nao informado') AS EMPREENDIMENTO,
                COALESCE(f.UNIDADE, 'Nao informado') AS UNIDADE,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY COALESCE(f.EMPREENDIMENTO, 'Nao informado'), COALESCE(f.UNIDADE, 'Nao informado')
            ORDER BY TOTAL DESC
            """);

        queries["Dashboard.BaixaMotivos"] = new("""
            SELECT MOTIVO, DESCRICAO, QTD, PERCENTUAL
            FROM dbo.vw_serasa_pefin_baixa_motivos
            ORDER BY MOTIVO
            """);

        queries["Dashboard.NegativacaoBaixaMensal"] = new("""
            SELECT ANO_MES, QTD_NEGATIVACOES, QTD_BAIXAS
            FROM dbo.vw_serasa_pefin_negativacao_baixa_mensal
            ORDER BY ANO_MES
            """);

        queries["Dashboard.UsuariosAtivos"] = new("""
            SELECT
                CASE WHEN ATIVO = 1 THEN 'Ativo' ELSE 'Inativo' END AS STATUS,
                COUNT(*) AS TOTAL
            FROM dbo.USUARIO
            GROUP BY CASE WHEN ATIVO = 1 THEN 'Ativo' ELSE 'Inativo' END
            """);

        queries["Dashboard.Aging"] = new("""
            SELECT
                CASE
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 0 AND 30 THEN '0-30'
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 31 AND 90 THEN '31-90'
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 91 AND 180 THEN '91-180'
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) > 180 THEN '180+'
                    ELSE '0-30'
                END AS FAIXA,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL
              AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
              AND DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) >= 0
            GROUP BY
                CASE
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 0 AND 30 THEN '0-30'
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 31 AND 90 THEN '31-90'
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 91 AND 180 THEN '91-180'
                    WHEN DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) > 180 THEN '180+'
                    ELSE '0-30'
                END
            ORDER BY FAIXA
            """);

        queries["Dashboard.ParcelasInadimplentes"] = new("""
            SELECT
                COALESCE(CAST(f.QTD_PARCELAS_INADIMPLENTES AS varchar(20)), 'Nao informado') AS QTD_PARCELAS,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY f.QTD_PARCELAS_INADIMPLENTES
            ORDER BY
                CASE WHEN f.QTD_PARCELAS_INADIMPLENTES IS NULL THEN 1 ELSE 0 END,
                f.QTD_PARCELAS_INADIMPLENTES
            """);

        queries["Dashboard.ParcelasDetalhes"] = new("""
            SELECT TOP (@limit)
                f.CLIENTE,
                f.CPF_CNPJ,
                f.NUM_VENDA,
                f.EMPREENDIMENTO,
                f.BLOCO,
                f.UNIDADE,
                f.SCORE,
                CAST(f.VALOR_TOTAL_EM_ABERTO AS float) AS SALDO,
                CAST(f.VALOR_INADIMPLENTE AS float) AS VALOR_SOMENTE_INADIMPLENTE,
                f.QTD_PARCELAS_INADIMPLENTES,
                f.VENCIMENTO_MAIS_ANTIGO,
                f.STATUS_REPASSE,
                ultima_acao.PROXIMA_ACAO AS PROXIMA_ACAO,
                f.SUGESTAO
            FROM DW.fat_analise_inadimplencia_v4 f
            OUTER APPLY (
                SELECT TOP 1 o.PROXIMA_ACAO
                FROM dbo.OCORRENCIAS o
                WHERE o.NUM_VENDA_FK = f.NUM_VENDA
                  AND o.PROXIMA_ACAO IS NOT NULL
                ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
            ) AS ultima_acao
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
              AND ((@qtd IS NULL AND f.QTD_PARCELAS_INADIMPLENTES IS NULL) OR f.QTD_PARCELAS_INADIMPLENTES = @qtd)
            ORDER BY CAST(f.VALOR_TOTAL_EM_ABERTO AS float) DESC
            """);

        queries["Dashboard.ScoreSaldo"] = new("""
            SELECT
                f.SCORE,
                AVG(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)) AS MEDIA_SALDO,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
              AND f.SCORE IS NOT NULL
            GROUP BY f.SCORE
            ORDER BY f.SCORE
            """);

        queries["Dashboard.ScoreSaldoDetalhes"] = new("""
            SELECT TOP (@limit)
                f.CLIENTE,
                f.CPF_CNPJ,
                f.NUM_VENDA,
                f.EMPREENDIMENTO,
                f.BLOCO,
                f.UNIDADE,
                f.SCORE,
                CAST(f.VALOR_TOTAL_EM_ABERTO AS float) AS SALDO,
                CAST(f.VALOR_INADIMPLENTE AS float) AS VALOR_SOMENTE_INADIMPLENTE,
                f.QTD_PARCELAS_INADIMPLENTES,
                f.VENCIMENTO_MAIS_ANTIGO,
                f.STATUS_REPASSE,
                ultima_acao.PROXIMA_ACAO AS PROXIMA_ACAO,
                f.SUGESTAO
            FROM DW.fat_analise_inadimplencia_v4 f
            OUTER APPLY (
                SELECT TOP 1 o.PROXIMA_ACAO
                FROM dbo.OCORRENCIAS o
                WHERE o.NUM_VENDA_FK = f.NUM_VENDA
                  AND o.PROXIMA_ACAO IS NOT NULL
                ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
            ) AS ultima_acao
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
              AND TRY_CAST(f.SCORE AS float) = TRY_CAST(@score AS float)
            ORDER BY CAST(f.VALOR_TOTAL_EM_ABERTO AS float) DESC
            """);

        queries["Dashboard.SaldoPorMesVencimento"] = new("""
            SELECT
                DATEFROMPARTS(YEAR(f.VENCIMENTO_MAIS_ANTIGO), MONTH(f.VENCIMENTO_MAIS_ANTIGO), 1) AS MES,
                SUM(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)) AS TOTAL_SALDO
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL
              AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY YEAR(f.VENCIMENTO_MAIS_ANTIGO), MONTH(f.VENCIMENTO_MAIS_ANTIGO)
            ORDER BY MES
            """);

        queries["Dashboard.PerfilRiscoEmpreendimento"] = new("""
            SELECT
                COALESCE(f.EMPREENDIMENTO, 'Nao informado') AS EMPREENDIMENTO,
                COUNT(*) AS TOTAL_VENDAS,
                COUNT(DISTINCT f.CPF_CNPJ) AS TOTAL_CLIENTES,
                AVG(CAST(f.SCORE AS float)) AS MEDIA_SCORE,
                AVG(CAST(f.QTD_PARCELAS_INADIMPLENTES AS float)) AS MEDIA_PARCELAS,
                SUM(CAST(f.VALOR_TOTAL_EM_ABERTO AS float)) AS TOTAL_SALDO,
                SUM(CAST(f.VALOR_INADIMPLENTE AS float)) AS TOTAL_INADIMPLENTE
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            GROUP BY COALESCE(f.EMPREENDIMENTO, 'Nao informado')
            ORDER BY EMPREENDIMENTO
            """);

        queries["Dashboard.AgingDetalhes"] = new("""
            SELECT TOP (@limit)
                f.CLIENTE,
                f.CPF_CNPJ,
                f.NUM_VENDA,
                f.EMPREENDIMENTO,
                f.BLOCO,
                f.UNIDADE,
                f.VENCIMENTO_MAIS_ANTIGO,
                DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) AS DIAS_ATRASO,
                CAST(f.VALOR_TOTAL_EM_ABERTO AS float) AS SALDO,
                CAST(f.VALOR_INADIMPLENTE AS float) AS VALOR_SOMENTE_INADIMPLENTE
            FROM DW.fat_analise_inadimplencia_v4 f
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
              AND (
                  (@faixa = '0-30' AND f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL AND DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 0 AND 30)
                  OR (@faixa = '31-90' AND f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL AND DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 31 AND 90)
                  OR (@faixa = '91-180' AND f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL AND DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) BETWEEN 91 AND 180)
                  OR (@faixa = '180+' AND f.VENCIMENTO_MAIS_ANTIGO IS NOT NULL AND DATEDIFF(day, f.VENCIMENTO_MAIS_ANTIGO, GETDATE()) > 180)
              )
            ORDER BY DIAS_ATRASO DESC, f.VENCIMENTO_MAIS_ANTIGO ASC
            """);

        queries["Dashboard.Ocorrencias"] = new("""
            SELECT *
            FROM dbo.OCORRENCIAS
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            """);

        queries["Dashboard.OcorrenciasPorUsuario"] = new("""
            SELECT
                COALESCE(o.NOME_USUARIO_FK, 'Nao informado') AS USUARIO,
                COUNT(*) AS TOTAL,
                MAX(u.COR_HEX) AS COR_HEX
            FROM dbo.OCORRENCIAS o
            INNER JOIN (
                SELECT DISTINCT fat.NUM_VENDA
                FROM DW.fat_analise_inadimplencia_v4 fat
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
            ) vendas_inad ON vendas_inad.NUM_VENDA = o.NUM_VENDA_FK
            LEFT JOIN dbo.USUARIO u ON u.NOME = o.NOME_USUARIO_FK
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            GROUP BY COALESCE(o.NOME_USUARIO_FK, 'Nao informado')
            ORDER BY TOTAL DESC
            """);

        queries["Dashboard.OcorrenciasPorVenda"] = new("""
            SELECT TOP (@limit)
                o.NUM_VENDA_FK,
                COUNT(*) AS TOTAL
            FROM dbo.OCORRENCIAS o
            INNER JOIN (
                SELECT DISTINCT fat.NUM_VENDA
                FROM DW.fat_analise_inadimplencia_v4 fat
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
            ) vendas_inad ON vendas_inad.NUM_VENDA = o.NUM_VENDA_FK
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            GROUP BY o.NUM_VENDA_FK
            ORDER BY TOTAL DESC
            """);

        queries["Dashboard.OcorrenciasPorDia"] = new("""
            SELECT
                o.DT_OCORRENCIA AS DATA,
                COUNT(*) AS TOTAL
            FROM dbo.OCORRENCIAS o
            INNER JOIN (
                SELECT DISTINCT fat.NUM_VENDA
                FROM DW.fat_analise_inadimplencia_v4 fat
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
            ) vendas_inad ON vendas_inad.NUM_VENDA = o.NUM_VENDA_FK
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            GROUP BY o.DT_OCORRENCIA
            ORDER BY DATA
            """);

        queries["Dashboard.OcorrenciasPorHora"] = new("""
            SELECT
                DATEPART(HOUR, o.HORA_OCORRENCIA) AS HORA,
                COUNT(*) AS TOTAL
            FROM dbo.OCORRENCIAS o
            INNER JOIN (
                SELECT DISTINCT fat.NUM_VENDA
                FROM DW.fat_analise_inadimplencia_v4 fat
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
            ) vendas_inad ON vendas_inad.NUM_VENDA = o.NUM_VENDA_FK
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            GROUP BY DATEPART(HOUR, o.HORA_OCORRENCIA)
            ORDER BY HORA
            """);

        queries["Dashboard.OcorrenciasPorDiaHora"] = new("""
            SELECT
                o.DT_OCORRENCIA AS DATA,
                DATEPART(HOUR, o.HORA_OCORRENCIA) AS HORA,
                COUNT(*) AS TOTAL
            FROM dbo.OCORRENCIAS o
            INNER JOIN (
                SELECT DISTINCT fat.NUM_VENDA
                FROM DW.fat_analise_inadimplencia_v4 fat
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
            ) vendas_inad ON vendas_inad.NUM_VENDA = o.NUM_VENDA_FK
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            GROUP BY o.DT_OCORRENCIA, DATEPART(HOUR, o.HORA_OCORRENCIA)
            ORDER BY DATA, HORA
            """);

        queries["Dashboard.ProximasAcoesPorDia"] = new("""
            SELECT
                TRY_CONVERT(date, ultima_acao.PROXIMA_ACAO) AS DATA,
                COUNT(*) AS TOTAL
            FROM DW.fat_analise_inadimplencia_v4 f
            OUTER APPLY (
                SELECT TOP 1 o.PROXIMA_ACAO,
                       o.NOME_USUARIO_FK
                FROM dbo.OCORRENCIAS o
                WHERE o.NUM_VENDA_FK = f.NUM_VENDA
                  AND o.PROXIMA_ACAO IS NOT NULL
                  AND (@dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim)
                ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
            ) AS ultima_acao
            WHERE ultima_acao.PROXIMA_ACAO IS NOT NULL
              AND UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
              AND (@nomeUsuario IS NULL OR LOWER(LTRIM(RTRIM(COALESCE(ultima_acao.NOME_USUARIO_FK, '')))) = @nomeUsuario)
            GROUP BY TRY_CONVERT(date, ultima_acao.PROXIMA_ACAO)
            ORDER BY DATA
            """);

        queries["Dashboard.AcoesDefinidas"] = new("""
            SELECT
                COUNT(*) AS TOTAL_VENDAS,
                SUM(CASE WHEN ultima_acao.PROXIMA_ACAO IS NOT NULL THEN 1 ELSE 0 END) AS COM_ACAO,
                SUM(CASE WHEN ultima_acao.PROXIMA_ACAO IS NULL THEN 1 ELSE 0 END) AS SEM_ACAO,
                CAST(
                    CASE WHEN COUNT(*) = 0 THEN 0
                    ELSE (100.0 * SUM(CASE WHEN ultima_acao.PROXIMA_ACAO IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*))
                    END AS decimal(10,2)
                ) AS PERC_COM_ACAO
            FROM DW.fat_analise_inadimplencia_v4 f
            OUTER APPLY (
                SELECT TOP 1 o.PROXIMA_ACAO
                FROM dbo.OCORRENCIAS o
                WHERE o.NUM_VENDA_FK = f.NUM_VENDA
                  AND o.PROXIMA_ACAO IS NOT NULL
                  AND (@dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim)
                ORDER BY o.DT_OCORRENCIA DESC, o.HORA_OCORRENCIA DESC, o.PROXIMA_ACAO DESC
            ) AS ultima_acao
            WHERE UPPER(LTRIM(RTRIM(COALESCE(f.INADIMPLENTE, '')))) = 'SIM'
            """);

        queries["Dashboard.AtendentesPorProximaAcao"] = new("""
            SELECT
                COALESCE(o.NOME_USUARIO_FK, 'Nao informado') AS USUARIO,
                MAX(u.COR_HEX) AS COR_HEX,
                SUM(CASE WHEN o.PROXIMA_ACAO IS NOT NULL AND o.DT_OCORRENCIA >= DATEADD(day, -7, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS ULT_7_DIAS,
                SUM(CASE WHEN o.PROXIMA_ACAO IS NOT NULL AND o.DT_OCORRENCIA >= DATEADD(day, -15, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS ULT_15_DIAS,
                SUM(CASE WHEN o.PROXIMA_ACAO IS NOT NULL AND o.DT_OCORRENCIA >= DATEADD(day, -30, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS ULT_30_DIAS,
                SUM(CASE WHEN o.PROXIMA_ACAO IS NOT NULL AND o.DT_OCORRENCIA >= DATEADD(month, -6, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS ULT_6_MESES,
                SUM(CASE WHEN o.PROXIMA_ACAO IS NOT NULL AND o.DT_OCORRENCIA >= DATEADD(year, -1, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS ULT_1_ANO
            FROM dbo.OCORRENCIAS o
            INNER JOIN (
                SELECT DISTINCT fat.NUM_VENDA
                FROM DW.fat_analise_inadimplencia_v4 fat
                WHERE UPPER(LTRIM(RTRIM(COALESCE(fat.INADIMPLENTE, '')))) = 'SIM'
            ) vendas_inad ON vendas_inad.NUM_VENDA = o.NUM_VENDA_FK
            LEFT JOIN dbo.USUARIO u ON u.NOME = o.NOME_USUARIO_FK
            WHERE @dataInicio IS NULL OR @dataFim IS NULL OR o.DT_OCORRENCIA BETWEEN @dataInicio AND @dataFim
            GROUP BY COALESCE(o.NOME_USUARIO_FK, 'Nao informado')
            ORDER BY ULT_7_DIAS DESC, ULT_15_DIAS DESC, ULT_30_DIAS DESC
            """);
    }

    private static IReadOnlyDictionary<string, SqlDefinition> BuildCommands()
        => new Dictionary<string, SqlDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Notifications.MarkRead"] = new("""
                UPDATE dbo.INAD_NOTIFICACOES
                SET LIDA = 1,
                    DT_LEITURA = COALESCE(DT_LEITURA, GETDATE())
                WHERE ID = @id
                  AND USUARIO_DESTINATARIO = @username
                  AND DT_EXCLUSAO IS NULL
                """),

            ["Notifications.MarkAllRead"] = new("""
                UPDATE dbo.INAD_NOTIFICACOES
                SET LIDA = 1,
                    DT_LEITURA = COALESCE(DT_LEITURA, GETDATE())
                WHERE USUARIO_DESTINATARIO = @username
                  AND LIDA = 0
                  AND DT_EXCLUSAO IS NULL
                """),

            ["Notifications.Delete"] = new("""
                UPDATE dbo.INAD_NOTIFICACOES
                SET DT_EXCLUSAO = COALESCE(DT_EXCLUSAO, GETDATE())
                WHERE ID = @id
                  AND USUARIO_DESTINATARIO = @username
                  AND DT_EXCLUSAO IS NULL
                """),

            ["Usuarios.Upsert"] = new("""
                DECLARE @existingNome varchar(255);

                SELECT TOP 1 @existingNome = NOME
                FROM dbo.USUARIO
                WHERE (@userCode IS NOT NULL AND USER_CODE = @userCode)
                   OR NOME = @nome;

                IF @existingNome IS NULL
                BEGIN
                    INSERT INTO dbo.USUARIO (NOME, USER_CODE, PERFIL, CPF_USUARIO, SETOR, CARGO, ATIVO, COR_HEX)
                    VALUES (@nome, @userCode, @perfil, @cpfUsuario, @setor, @cargo, @ativo, @corHex);
                    SET @existingNome = @nome;
                END
                ELSE
                BEGIN
                    UPDATE dbo.USUARIO
                    SET NOME = COALESCE(@nome, NOME),
                        CPF_USUARIO = COALESCE(@cpfUsuario, CPF_USUARIO),
                        SETOR = COALESCE(@setor, SETOR),
                        CARGO = COALESCE(@cargo, CARGO),
                        ATIVO = COALESCE(@ativo, ATIVO),
                        COR_HEX = COALESCE(@corHex, COR_HEX),
                        USER_CODE = COALESCE(@userCode, USER_CODE),
                        PERFIL = COALESCE(@perfil, PERFIL)
                    WHERE NOME = @existingNome;
                    SET @existingNome = @nome;
                END

                SELECT TOP 1 *
                FROM dbo.USUARIO
                WHERE NOME = @existingNome;
                """, ReturnsRows: true),

            ["Usuarios.Update"] = new("""
                UPDATE dbo.USUARIO
                SET CPF_USUARIO = COALESCE(@cpfUsuario, CPF_USUARIO),
                    SETOR = COALESCE(@setor, SETOR),
                    CARGO = COALESCE(@cargo, CARGO),
                    ATIVO = COALESCE(@ativo, ATIVO),
                    COR_HEX = COALESCE(@corHex, COR_HEX),
                    USER_CODE = COALESCE(@userCode, USER_CODE),
                    PERFIL = COALESCE(@perfil, PERFIL)
                WHERE NOME = @nome;

                SELECT TOP 1 *
                FROM dbo.USUARIO
                WHERE NOME = @nome;
                """, ReturnsRows: true),

            ["Usuarios.Delete"] = new("DELETE FROM dbo.USUARIO WHERE NOME = @nome"),

            ["Responsaveis.Upsert"] = new("""
                DECLARE @responsavelAnterior varchar(255);

                SELECT TOP 1 @responsavelAnterior = NOME_USUARIO_FK
                FROM dbo.VENDA_RESPONSAVEL
                WHERE NUM_VENDA_FK = @numVenda;

                MERGE dbo.VENDA_RESPONSAVEL AS target
                USING (SELECT @numVenda AS NUM_VENDA_FK, @nomeUsuario AS NOME_USUARIO_FK) AS source
                   ON target.NUM_VENDA_FK = source.NUM_VENDA_FK
                WHEN MATCHED THEN
                    UPDATE SET NOME_USUARIO_FK = source.NOME_USUARIO_FK,
                               DT_ATRIBUICAO = GETDATE()
                WHEN NOT MATCHED THEN
                    INSERT (NUM_VENDA_FK, NOME_USUARIO_FK)
                    VALUES (source.NUM_VENDA_FK, source.NOME_USUARIO_FK);

                UPDATE dbo.KANBAN_STATUS
                SET NOME_USUARIO_FK = @nomeUsuario
                WHERE NUM_VENDA_FK = @numVenda;

                IF @responsavelAnterior IS NOT NULL
                   AND LOWER(LTRIM(RTRIM(@responsavelAnterior))) <> LOWER(LTRIM(RTRIM(@nomeUsuario)))
                BEGIN
                    UPDATE dbo.INAD_NOTIFICACOES
                    SET DT_EXCLUSAO = COALESCE(DT_EXCLUSAO, GETDATE())
                    WHERE TIPO = 'VENDA_ATRIBUIDA'
                      AND NUM_VENDA = @numVenda
                      AND USUARIO_DESTINATARIO = @responsavelAnterior
                      AND DT_EXCLUSAO IS NULL;
                END;

                IF @nomeUsuario IS NOT NULL
                   AND (@responsavelAnterior IS NULL OR LOWER(LTRIM(RTRIM(@responsavelAnterior))) <> LOWER(LTRIM(RTRIM(@nomeUsuario))))
                BEGIN
                    INSERT INTO dbo.INAD_NOTIFICACOES (
                        ID,
                        TIPO,
                        USUARIO_DESTINATARIO,
                        ORIGEM_USUARIO,
                        NUM_VENDA,
                        PROXIMA_ACAO,
                        PAYLOAD,
                        LIDA,
                        DT_CRIACAO
                    )
                    SELECT NEWID(),
                           'VENDA_ATRIBUIDA',
                           @nomeUsuario,
                           @adminUserCode,
                           @numVenda,
                           NULL,
                           (
                               SELECT @numVenda AS numVenda,
                                      f.CLIENTE AS cliente,
                                      f.CPF_CNPJ AS cpfCnpj,
                                      f.EMPREENDIMENTO AS empreendimento,
                                      f.VALOR_INADIMPLENTE AS valorInadimplente,
                                      f.SCORE AS score,
                                      @nomeUsuario AS responsavel,
                                      GETDATE() AS dtAtribuicao
                               FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                           ),
                           0,
                           GETDATE()
                    FROM DW.fat_analise_inadimplencia_v4 f
                    WHERE f.NUM_VENDA = @numVenda;
                END;

                SELECT vr.NUM_VENDA_FK,
                       vr.NOME_USUARIO_FK,
                       vr.DT_ATRIBUICAO,
                       f.SCORE,
                       u.COR_HEX
                FROM dbo.VENDA_RESPONSAVEL vr
                LEFT JOIN DW.fat_analise_inadimplencia_v4 f ON f.NUM_VENDA = vr.NUM_VENDA_FK
                LEFT JOIN dbo.USUARIO u ON u.NOME = vr.NOME_USUARIO_FK
                WHERE vr.NUM_VENDA_FK = @numVenda;
                """, ReturnsRows: true),

            ["Responsaveis.Delete"] = new("DELETE FROM dbo.VENDA_RESPONSAVEL WHERE NUM_VENDA_FK = @numVenda"),

            ["KanbanStatus.Upsert"] = new("""
                MERGE dbo.KANBAN_STATUS AS target
                USING (
                    SELECT @numVenda AS NUM_VENDA_FK, @proximaAcao AS PROXIMA_ACAO
                ) AS source
                   ON target.NUM_VENDA_FK = source.NUM_VENDA_FK
                  AND target.PROXIMA_ACAO = source.PROXIMA_ACAO
                WHEN MATCHED THEN
                    UPDATE SET STATUS = @status,
                               STATUS_DATA = @statusData,
                               NOME_USUARIO_FK = @nomeUsuario,
                               DT_ATUALIZACAO = CASE
                                   WHEN target.STATUS = 'inProgress' AND @status = 'inProgress'
                                       THEN target.DT_ATUALIZACAO
                                   ELSE GETDATE()
                               END
                WHEN NOT MATCHED THEN
                    INSERT (NUM_VENDA_FK, PROXIMA_ACAO, STATUS, STATUS_DATA, NOME_USUARIO_FK, DT_ATUALIZACAO)
                    VALUES (@numVenda, @proximaAcao, @status, @statusData, @nomeUsuario, GETDATE());

                SELECT NUM_VENDA_FK,
                       PROXIMA_ACAO,
                       STATUS,
                       STATUS_DATA,
                       NOME_USUARIO_FK,
                       DT_ATUALIZACAO
                FROM dbo.KANBAN_STATUS
                WHERE NUM_VENDA_FK = @numVenda
                  AND PROXIMA_ACAO = @proximaAcao;
                """, ReturnsRows: true),

            ["Ocorrencia.Insert"] = new("""
                INSERT INTO dbo.OCORRENCIAS (
                    ID,
                    NUM_VENDA_FK,
                    NOME_USUARIO_FK,
                    DESCRICAO,
                    STATUS_OCORRENCIA,
                    DT_OCORRENCIA,
                    HORA_OCORRENCIA,
                    PROXIMA_ACAO,
                    PROTOCOLO
                )
                OUTPUT inserted.ID,
                       inserted.NUM_VENDA_FK,
                       inserted.NOME_USUARIO_FK,
                       inserted.DESCRICAO,
                       inserted.STATUS_OCORRENCIA,
                       inserted.DT_OCORRENCIA,
                       inserted.HORA_OCORRENCIA,
                       inserted.PROXIMA_ACAO,
                       inserted.PROTOCOLO
                VALUES (
                    @id,
                    @numVenda,
                    @nomeUsuario,
                    @descricao,
                    @statusOcorrencia,
                    @dtOcorrencia,
                    @horaOcorrencia,
                    @proximaAcao,
                    @protocolo
                );
                """, ReturnsRows: true),

            ["Ocorrencia.Update"] = new("""
                UPDATE dbo.OCORRENCIAS
                SET NUM_VENDA_FK = @numVenda,
                    NOME_USUARIO_FK = @nomeUsuario,
                    DESCRICAO = @descricao,
                    STATUS_OCORRENCIA = @statusOcorrencia,
                    DT_OCORRENCIA = @dtOcorrencia,
                    HORA_OCORRENCIA = @horaOcorrencia,
                    PROXIMA_ACAO = @proximaAcao,
                    PROTOCOLO = @protocolo
                WHERE ID = @id;

                SELECT TOP 1 ID,
                       NUM_VENDA_FK,
                       NOME_USUARIO_FK,
                       DESCRICAO,
                       STATUS_OCORRENCIA,
                       DT_OCORRENCIA,
                       HORA_OCORRENCIA,
                       PROXIMA_ACAO,
                       PROTOCOLO
                FROM dbo.OCORRENCIAS
                WHERE ID = @id;
                """, ReturnsRows: true),

            ["Ocorrencia.Delete"] = new("DELETE FROM dbo.OCORRENCIAS WHERE ID = @id"),

            ["Atendimento.CreateFromVenda"] = new("""
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                UPDATE a
                SET a.STATUS_PROTOCOLO = 0
                FROM dbo.ATENDIMENTOS a
                WHERE a.NUM_VENDA_FK = @numVenda
                  AND ISNULL(a.STATUS_PROTOCOLO, 0) = 1
                  AND DATEDIFF(SECOND, a.CRIADO_EM, GETDATE()) >= 720
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.OCORRENCIAS oc
                      WHERE LTRIM(RTRIM(oc.PROTOCOLO)) = LTRIM(RTRIM(a.PROTOCOLO))
                  );

                IF EXISTS (
                    SELECT 1
                    FROM dbo.ATENDIMENTOS a WITH (UPDLOCK, HOLDLOCK)
                    WHERE a.NUM_VENDA_FK = @numVenda
                      AND ISNULL(a.STATUS_PROTOCOLO, 0) = 1
                      AND DATEDIFF(SECOND, a.CRIADO_EM, GETDATE()) < 720
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.OCORRENCIAS oc
                          WHERE LTRIM(RTRIM(oc.PROTOCOLO)) = LTRIM(RTRIM(a.PROTOCOLO))
                      )
                )
                BEGIN
                    SELECT TOP 1 CAST(1 AS bit) AS ATENDIMENTO_ATIVO,
                           a.*
                    FROM dbo.ATENDIMENTOS a
                    WHERE a.NUM_VENDA_FK = @numVenda
                      AND ISNULL(a.STATUS_PROTOCOLO, 0) = 1
                      AND DATEDIFF(SECOND, a.CRIADO_EM, GETDATE()) < 720
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.OCORRENCIAS oc
                          WHERE LTRIM(RTRIM(oc.PROTOCOLO)) = LTRIM(RTRIM(a.PROTOCOLO))
                      )
                    ORDER BY a.CRIADO_EM DESC;
                    COMMIT TRANSACTION;
                    RETURN;
                END;

                DECLARE @prefix varchar(8) = CONVERT(char(8), GETDATE(), 112);
                DECLARE @maxProtocolo varchar(20);
                SELECT @maxProtocolo = MAX(PROTOCOLO)
                FROM dbo.ATENDIMENTOS WITH (UPDLOCK, HOLDLOCK)
                WHERE PROTOCOLO LIKE @prefix + '%';

                DECLARE @nextSequence int = 1;
                IF @maxProtocolo IS NOT NULL AND LEN(@maxProtocolo) >= 13
                BEGIN
                    SET @nextSequence = COALESCE(TRY_CAST(RIGHT(@maxProtocolo, 5) AS int), 0) + 1;
                END;

                DECLARE @protocolo varchar(20) = @prefix + RIGHT('00000' + CAST(@nextSequence AS varchar(5)), 5);

                INSERT INTO dbo.ATENDIMENTOS (
                    PROTOCOLO,
                    NUM_VENDA_FK,
                    CPF_CNPJ,
                    CLIENTE,
                    EMPREENDIMENTO,
                    DADOS_VENDA,
                    STATUS_PROTOCOLO
                )
                OUTPUT CAST(0 AS bit) AS ATENDIMENTO_ATIVO,
                       inserted.*
                VALUES (
                    @protocolo,
                    @numVenda,
                    @cpfCnpj,
                    @cliente,
                    @empreendimento,
                    @dadosVenda,
                    1
                );

                COMMIT TRANSACTION;
                """, ReturnsRows: true),

            // Generates a unique sequential protocolo (format: YYYYMMDDXXXXX) using
            // SERIALIZABLE isolation + UPDLOCK/HOLDLOCK to prevent races. The sequence
            // is derived from the highest existing protocolo of the day in dbo.OCORRENCIAS.
            ["Protocolo.Gerar"] = new("""
                SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                BEGIN TRANSACTION;

                DECLARE @prefix varchar(8) = CONVERT(char(8), GETDATE(), 112);
                DECLARE @maxProtocolo varchar(20);
                SELECT @maxProtocolo = MAX(PROTOCOLO)
                FROM dbo.OCORRENCIAS WITH (UPDLOCK, HOLDLOCK)
                WHERE PROTOCOLO LIKE @prefix + '%';

                DECLARE @nextSequence int = 1;
                IF @maxProtocolo IS NOT NULL AND LEN(@maxProtocolo) >= 13
                BEGIN
                    SET @nextSequence = COALESCE(TRY_CAST(RIGHT(@maxProtocolo, 5) AS int), 0) + 1;
                END;

                SELECT @prefix + RIGHT('00000' + CAST(@nextSequence AS varchar(5)), 5) AS PROTOCOLO;

                COMMIT TRANSACTION;
                """, ReturnsRows: true),
        };

    private sealed record SqlDefinition(string Sql, bool ReturnsRows = false);
}

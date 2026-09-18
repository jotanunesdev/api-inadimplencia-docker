using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

// SELECTs sintéticos: nunca grava, nem consulta registros financeiros reais.
public sealed class ValorRecuperadoReadOnlyTests
{
    private const string Sources = """
        WITH Recuperados AS (
            SELECT f.*, 'Teste' CLIENTE, '00000000000' CPF_CNPJ, 'Obra' EMPREENDIMENTO,
                'Documento' NUM_DOCUMENTO, CAST('2025-01-01' AS date) DATA_VENCIMENTO,
                CAST('2026-03-01' AS datetime2) DATA_CARGA, 0.00 SALDO_RECEBER
            FROM (VALUES
                (11,1,CAST('2026-01-15' AS date),CAST(2000.00 AS decimal(18,4)),'BAIXA PARCIAL'),
                (12,1,CAST('2026-02-15' AS date),CAST(2000.00 AS decimal(18,4)),'BAIXADO'),
                (21,2,CAST('2026-01-31' AS date),CAST(1500.00 AS decimal(18,4)),'BAIXADO'),
                (31,3,CAST('2026-02-01' AS date),CAST(500.00 AS decimal(18,4)),'BAIXADO')
            ) f(IDLAN,NUM_VENDA,DATA_PAGAMENTO,VALOR_RECUPERADO,STATUSLAN)
        ), Responsaveis AS (
            SELECT 1 NUM_VENDA_FK, 'Atual' NOME_USUARIO_FK, CAST('2026-02-01' AS date) DT_ATRIBUICAO
            UNION ALL SELECT 1, 'Anterior', CAST('2026-01-01' AS date)
        ), Vendas AS (
            SELECT 1 NUM_VENDA, 'SIM' INADIMPLENTE UNION ALL SELECT 2, 'NAO'
        )
        """;

    private static string Sql(string sql) => Sources + (sql.StartsWith("WITH ") ? ", " + sql[5..] : sql)
        .Replace("dw.ficha_financeira_valor_recuperado", "Recuperados")
        .Replace("dbo.VENDA_RESPONSAVEL", "Responsaveis")
        .Replace("DW.fat_analise_inadimplencia_v4", "Vendas");
    private static SqlConnection Connection() => new(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));

    [RecuperaReadOnlyFact]
    public async Task Mensal_SomaParcelasIncluindoParcialSemContarVendasOuDuplicarValores()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(Sql(ValorRecuperadoSql.Mensal), new { dataInicio = new DateTime(2026,1,1), dataFim = new DateTime(2026,3,31) })).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { 3500m, 2500m, 0m }, rows.Select(x => (decimal)x.VALOR_RECUPERADO));
        Assert.Equal(2L, (long)rows[0].PARCELAS);
        Assert.All(rows, row => Assert.False(((IDictionary<string, object>)row).ContainsKey("VENDAS_RECUPERADAS")));
    }

    [RecuperaReadOnlyFact]
    public async Task Detalhes_PreservamVendaAindaInadimplenteEHistoricaSemMultiplicarResponsaveis()
    {
        await using var connection = Connection();
        var args = new { dataInicio = new DateTime(2026,1,1), dataFim = new DateTime(2026,2,28), offset = 0, limit = 100 };
        var rows = (await connection.QueryAsync(Sql(ValorRecuperadoSql.Detalhes), args)).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(6000m, rows.Sum(x => (decimal)x.VALOR_RECUPERADO));
        Assert.All(rows, row => Assert.Equal(4L, (long)row.TOTAL_COUNT));
        var partial = rows.Single(x => (int)x.IDLAN == 11);
        Assert.Equal("SIM", (string)partial.INADIMPLENTE_ATUAL);
        Assert.Equal("BAIXA PARCIAL", (string)partial.STATUSLAN);
        Assert.Equal("Atual", (string)partial.RESPONSAVEL);
        Assert.Null(rows.Single(x => (int)x.NUM_VENDA == 3).INADIMPLENTE_ATUAL);
        var page = (await connection.QueryAsync(Sql(ValorRecuperadoSql.Detalhes), new { args.dataInicio, args.dataFim, offset = 1, limit = 1 })).ToList();
        Assert.Equal(31, (int)Assert.Single(page).IDLAN);
        Assert.Equal(6000m, (decimal)page[0].TOTAL_VALOR_RECUPERADO);
        Assert.Equal(0, (int)page[0].VALORES_AUSENTES);
        Assert.Equal(new DateTime(2026,3,1), (DateTime)page[0].DATA_CARGA_PERIODO);
    }

    [RecuperaReadOnlyFact]
    public async Task PeriodoFiltraPelaBaixaEIncluiODiaFinal()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(Sql(ValorRecuperadoSql.Mensal), new { dataInicio = new DateTime(2026,1,16), dataFim = new DateTime(2026,1,31) })).ToList();
        Assert.Equal(1500m, (decimal)Assert.Single(rows).VALOR_RECUPERADO);
        var empty = (await connection.QueryAsync(Sql(ValorRecuperadoSql.Detalhes), new { dataInicio = new DateTime(2026,3,1), dataFim = new DateTime(2026,3,31), offset = 0, limit = 20 })).ToList();
        Assert.Empty(empty);
    }

    [RecuperaReadOnlyFact]
    public async Task AjustesRefletemValorAtualSemSubtrairOuAdicionarEstornoEDescontoNovamente()
    {
        await using var connection = Connection();
        var sql = Sql(ValorRecuperadoSql.Mensal).Replace("CAST(2000.00 AS decimal(18,4)),'BAIXA PARCIAL'", "CAST(800.00 AS decimal(18,4)),'BAIXA PARCIAL'");
        var rows = (await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026,1,1), dataFim = new DateTime(2026,1,31) })).ToList();
        Assert.Equal(2300m, (decimal)Assert.Single(rows).VALOR_RECUPERADO);
    }

    [RecuperaReadOnlyFact]
    public async Task ValorAusenteNaoViraZeroConfirmado()
    {
        await using var connection = Connection();
        var sql = Sql(ValorRecuperadoSql.Mensal).Replace("CAST(2000.00 AS decimal(18,4)),'BAIXA PARCIAL'", "CAST(NULL AS decimal(18,4)),'BAIXA PARCIAL'");
        var row = Assert.Single(await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026,1,1), dataFim = new DateTime(2026,1,31) }));
        Assert.Null(row.VALOR_RECUPERADO);
        Assert.Equal(1, (int)row.VALORES_AUSENTES);
    }

    [RecuperaReadOnlyFact]
    public async Task SomaDeTodasAsPaginasConfereComColunaDoMesmoPeriodoParcial()
    {
        await using var connection = Connection();
        var start = new DateTime(2026,1,16);
        var end = new DateTime(2026,2,15);
        var months = (await connection.QueryAsync(Sql(ValorRecuperadoSql.Mensal), new { dataInicio = start, dataFim = end })).ToList();
        foreach (var month in months)
        {
            var monthStart = DateTime.Parse((string)month.MES);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var begin = monthStart > start ? monthStart : start;
            var finish = monthEnd < end ? monthEnd : end;
            decimal sum = 0;
            for (var offset = 0; offset < (long)month.PARCELAS; offset++)
            {
                var row = Assert.Single(await connection.QueryAsync(Sql(ValorRecuperadoSql.Detalhes), new { dataInicio = begin, dataFim = finish, offset, limit = 1 }));
                sum += (decimal)row.VALOR_RECUPERADO;
                Assert.Equal((decimal)month.VALOR_RECUPERADO, (decimal)row.TOTAL_VALOR_RECUPERADO);
                Assert.Equal((DateTime)month.DATA_CARGA, (DateTime)row.DATA_CARGA_PERIODO);
            }
            Assert.Equal((decimal)month.VALOR_RECUPERADO, sum);
        }
    }
}

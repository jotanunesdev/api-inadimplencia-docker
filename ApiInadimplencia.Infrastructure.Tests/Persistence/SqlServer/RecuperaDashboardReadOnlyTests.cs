using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

// Apenas SELECT sobre VALUES em CTEs: não cria tabelas, não modifica dados e não usa TEST_CONNECTION_STRING.
public sealed class RecuperaReadOnlyFactAttribute : FactAttribute
{
    public RecuperaReadOnlyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY")))
            Skip = "Configure RECUPERA_SQL_READONLY para executar as consultas SELECT no SQL Server.";
    }
}

public sealed class RecuperaDashboardReadOnlyTests
{
    private static SqlConnection Connection() => new(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));

    private const string CarteiraSources = """
        WITH Carteira AS (
            SELECT f.*, 'Cliente sintético' CLIENTE, '00000000000' CPF_CNPJ,
                'Obra teste' EMPREENDIMENTO, 9000.00 VALOR_TOTAL_EM_ABERTO,
                2 QTD_PARCELAS_INADIMPLENTES, CAST('2026-01-01' AS date) VENCIMENTO_MAIS_ANTIGO,
                'Desbloqueado' STATUS_REPASSE
            FROM (VALUES (1,'SIM',4000.00),(2,' sim ',1000.00),(3,'NAO',5000.00)) f(NUM_VENDA,INADIMPLENTE,VALOR_INADIMPLENTE)
        ), Responsaveis AS (
            SELECT 1 NUM_VENDA_FK, 'Operador teste' NOME_USUARIO_FK
        ), Parcelas AS (
            SELECT p.*, CAST('2026-01-01' AS date) DATAVENCIMENTO,
                CAST(NULL AS varchar(3)) NEGATIVADO
            FROM (VALUES
                (1,'11','A',2000.00,'SIM'), (1,'11','A',2000.00,'SIM'),
                (1,NULL,'B',2000.00,'NAO'), (2,'21','C',1000.00,'SIM'),
                (3,'31','D',5000.00,'SIM')
            ) p(NUM_VENDA,IDLAN,NUMERO_DOCUMENTO,VALOR,INADIMPLENTE)
        )
        """;

    private static string CarteiraSql(string sql) => CarteiraSources + sql
        .Replace("DW.fat_analise_inadimplencia_v4", "Carteira")
        .Replace("DW.fat_analise_inadimplencia_parcelas", "Parcelas")
        .Replace("dbo.VENDA_RESPONSAVEL", "Responsaveis");

    [RecuperaReadOnlyFact]
    public async Task Carteira_ListaVendasDosKpisSemMultiplicarValoresPorParcelas()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(CarteiraSql(RecuperaDashboardSql.CarteiraInadimplenteDetalhes), new { offset = 0, limit = 20, cliente = (string?)null })).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(5000m, rows.Sum(x => (decimal)x.VALOR_INADIMPLENTE));
        Assert.All(rows, row => Assert.Equal(2L, (long)row.TOTAL_COUNT));
        Assert.Null(rows[1].RESPONSAVEL);
        var page = (await connection.QueryAsync(CarteiraSql(RecuperaDashboardSql.CarteiraInadimplenteDetalhes), new { offset = 1, limit = 1, cliente = (string?)null })).ToList();
        Assert.Equal(2, (int)Assert.Single(page).NUM_VENDA);
    }

    [RecuperaReadOnlyFact]
    public async Task Carteira_ParcelasPreservamRegistrosRepetidosENulosSemInferirPagamento()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(CarteiraSql(RecuperaDashboardSql.CarteiraInadimplenteParcelas), new { offset = 0, limit = 20, numVenda = 1 })).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(2, rows.Count(row => row.IDLAN == "11"));
        Assert.Contains(rows, row => row.IDLAN == null && row.INADIMPLENTE == "NAO");
        Assert.All(rows, row => Assert.Equal(3L, (long)row.TOTAL_COUNT));
    }

    [RecuperaReadOnlyFact]
    public async Task Carteira_ParcelaInadimplenteNaoAmpliaUniversoDoResumo()
    {
        await using var connection = Connection();
        var rows = await connection.QueryAsync(CarteiraSql(RecuperaDashboardSql.CarteiraInadimplenteParcelas), new { offset = 0, limit = 20, numVenda = 3 });
        Assert.Empty(rows);
        var emptyPage = await connection.QueryAsync(CarteiraSql(RecuperaDashboardSql.CarteiraInadimplenteDetalhes), new { offset = 20, limit = 20, cliente = (string?)null });
        Assert.Empty(emptyPage);
    }

    [RecuperaReadOnlyFact]
    public async Task Carteira_ClienteFiltraAntesDaPaginacaoSemAmpliarUniverso()
    {
        await using var connection = Connection();
        var sql = CarteiraSql(RecuperaDashboardSql.CarteiraInadimplenteDetalhes)
            .Replace("'Cliente sintético' CLIENTE", "CASE WHEN f.NUM_VENDA=1 THEN 'Ana' ELSE 'Maria 100%' END CLIENTE");
        var rows = (await connection.QueryAsync(sql, new { offset = 0, limit = 1, cliente = "%Maria%" })).ToList();
        var row = Assert.Single(rows);
        Assert.Equal(2, (int)row.NUM_VENDA);
        Assert.Equal(1L, (long)row.TOTAL_COUNT);
        Assert.Empty(await connection.QueryAsync(sql, new { offset = 1, limit = 1, cliente = "%Maria%" }));
        Assert.Empty(await connection.QueryAsync(sql, new { offset = 0, limit = 20, cliente = "%Inexistente%" }));
        Assert.Single(await connection.QueryAsync(sql, new { offset = 0, limit = 20, cliente = "%100~%%" }));
        Assert.Empty(await connection.QueryAsync(sql, new { offset = 0, limit = 20, cliente = "%~_%" }));
    }

    private const string Sources = """
        WITH Financeira AS (
            SELECT f.*, 'Teste' AS CLIENTE, CAST('2020-01-01' AS date) AS DATA_VENCIMENTO,
                CASE WHEN STATUSLAN = 'BAIXADO' THEN CAST('2020-01-01' AS date) END AS DATA_BAIXA,
                CASE WHEN STATUSLAN = 'BAIXADO' THEN 0.00 ELSE 10.00 END AS SALDO_RECEBER,
                0.00 AS RECEBIDO, CAST(NULL AS date) AS DATA_CANCELAMENTO_LAN, '2-Mensal' AS TIPO_PARCELA
            FROM (VALUES
                (1, '111.111.111-11', 100.00, 'Efetivada', 101, 'EM ABERTO'),
                (1, '111.111.111-11', 100.00, 'Efetivada', 101, 'EM ABERTO'),
                (2, '11111111111', 200.00, 'Efetivada', 201, 'BAIXADO'),
                (3, '22222222222', 100.00, 'Efetivada', 301, 'BAIXADO'),
                (3, '22222222222', 100.00, 'Efetivada', 302, 'BAIXADO'),
                (4, '33333333333', 999.00, 'Distratada', 401, 'EM ABERTO'),
                (5, '44444444444', 50.00, 'Efetivada', 501, NULL)
            ) f(NUM_VENDA, CPF_CNPJ, VALOR_VENDA, SITUACAO_VENDA, IDLAN, STATUSLAN)
        ),
        """;

    [RecuperaReadOnlyFact]
    public async Task Convertidos_ClienteUnicoValorNaoDuplicadoEInadimplenciaPrevalente()
    {
        await using var connection = Connection();
        var sql = Sources + RecuperaDashboardSql.Convertidos.Replace("WITH Fonte", "Fonte")
            .Replace("DW.fat_ficha_financeira_cliente", "Financeira").Replace("DW.fat_analise_inadimplencia_v4", "SituacaoFonte");
        var rows = (await connection.QueryAsync(sql)).ToList();
        var inad = rows.Single(x => x.SITUACAO == "INADIMPLENTE");
        var ad = rows.Single(x => x.SITUACAO == "ADIMPLENTE");
        var unknown = rows.Single(x => x.SITUACAO == "SEM_SITUACAO");
        Assert.Equal(1L, (long)inad.QUANTIDADE);
        Assert.Equal(300m, (decimal)inad.VALOR);
        Assert.Equal(100m, (decimal)ad.VALOR);
        Assert.Equal(50m, (decimal)unknown.VALOR);
        Assert.Equal(3L, (long)inad.TOTAL_CLIENTES);
        Assert.Equal(450m, (decimal)inad.VALOR_TOTAL);
        Assert.Equal(33.33m, (decimal)inad.PERCENTUAL_CLIENTES);
        Assert.Equal(66.67m, (decimal)inad.PERCENTUAL_VALOR);
    }

    [RecuperaReadOnlyFact]
    public async Task Convertidos_BaseVaziaNaoDividePorZero()
    {
        await using var connection = Connection();
        var sql = Sources + RecuperaDashboardSql.Convertidos.Replace("WITH Fonte", "Fonte")
            .Replace("DW.fat_ficha_financeira_cliente", "Financeira").Replace("DW.fat_analise_inadimplencia_v4", "SituacaoFonte")
            .Replace("= 'EFETIVADA'", "= 'INEXISTENTE'");
        var rows = (await connection.QueryAsync(sql)).ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => { Assert.Equal(0L, (long)row.TOTAL_CLIENTES); Assert.Equal(0m, (decimal)row.PERCENTUAL_VALOR); Assert.Equal(0m, (decimal)row.PERCENTUAL_CLIENTES); });
    }

    [RecuperaReadOnlyFact]
    public async Task Parcelas_BaixaParcialVencidaNaoEQuitacaoMesmoComSaldoZero()
    {
        await using var connection = Connection();
        var sql = Sources.Replace("'EM ABERTO'", "'BAIXA PARCIAL'").Replace("ELSE 10.00", "ELSE 0.00")
            + RecuperaDashboardSql.Convertidos.Replace("WITH Fonte", "Fonte").Replace("DW.fat_ficha_financeira_cliente", "Financeira");
        var rows = (await connection.QueryAsync(sql)).ToList();
        Assert.Equal(1L, (long)rows.Single(x => x.SITUACAO == "INADIMPLENTE").QUANTIDADE);
    }

    [RecuperaReadOnlyFact]
    public async Task Parcelas_FuturasNaoSaoAtrasadas()
    {
        await using var connection = Connection();
        var sql = Sources.Replace("CAST('2020-01-01' AS date) AS DATA_VENCIMENTO", "DATEADD(day,1,CONVERT(date,GETDATE())) AS DATA_VENCIMENTO")
            + RecuperaDashboardSql.Convertidos.Replace("WITH Fonte", "Fonte").Replace("DW.fat_ficha_financeira_cliente", "Financeira");
        var rows = (await connection.QueryAsync(sql)).ToList();
        Assert.Equal(0L, (long)rows.Single(x => x.SITUACAO == "INADIMPLENTE").QUANTIDADE);
        Assert.Equal(2L, (long)rows.Single(x => x.SITUACAO == "ADIMPLENTE").QUANTIDADE);
    }

    private const string Historico = """
        WITH Historico AS (
            SELECT CAST(mes AS date) AS data_base, venda, doc AS documento, valor AS venda_valor,
                atraso AS dias_atraso, 'Teste' AS comprador
            FROM (VALUES
                ('2026-01-31',1,'111',100.00,0), ('2026-01-31',1,'111',100.00,0),
                ('2026-02-28',1,'111',100.00,5), ('2026-03-31',1,'111',100.00,0), ('2026-04-30',1,'111',100.00,7),
                ('2026-01-31',2,'111',200.00,0), ('2026-02-28',2,'111',200.00,0),
                ('2026-03-31',2,'111',200.00,0), ('2026-04-30',2,'111',200.00,0),
                ('2026-01-31',3,'222',100.00,8), ('2026-03-31',3,'222',100.00,0),
                ('2026-01-31',4,'333',50.00,5), ('2026-01-31',5,'333',50.00,0),
                ('2026-02-28',5,'333',50.00,0)
            ) s(mes,venda,doc,valor,atraso)
        ),
        """;

    [RecuperaReadOnlyFact]
    public async Task Historico_CiclosSemDuplicidadeSemFalsaRegularizacaoEComFiltroAposJanela()
    {
        await using var connection = Connection();
        var sql = Historico + RecuperaDashboardSql.ConvertidosMensais.Replace("WITH VendasMes", "VendasMes")
            .Replace("dw.carteira_analitica_hist", "Historico");
        var rows = (await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026,2,1), dataFim = new DateTime(2026,5,31) })).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(1, (int)rows[0].NOVOS_INADIMPLENTES);
        Assert.Equal(300m, (decimal)rows[0].VALOR_NOVOS);
        Assert.Equal(0, (int)rows[0].REGULARIZADOS); // sale disappeared for client 333
        Assert.Equal(1, (int)rows[1].REGULARIZADOS); // client 222 has a missing month, not recovery
        Assert.Equal(300m, (decimal)rows[1].VALOR_REGULARIZADOS);
        Assert.Equal(1, (int)rows[2].REINCIDENTES);
        Assert.Equal(0, (int)rows[2].NOVOS_INADIMPLENTES);
        Assert.Equal(300m, (decimal)rows[2].VALOR_REINCIDENTES);
        Assert.Equal(0, (int)rows[3].TEM_BASE);
        Assert.Null((object?)rows[3].TOTAL_CLIENTES); // absence is not zero
    }

    [RecuperaReadOnlyFact]
    public async Task Detalhamento_VendaResponsavelEPaginacaoSemDuplicarParcelas()
    {
        await using var connection = Connection();
        const string responsaveis = " Responsaveis AS (SELECT 1 AS NUM_VENDA_FK, 'operador' AS NOME_USUARIO_FK), ";
        var sql = Sources + responsaveis + RecuperaDashboardSql.SituacaoClientesDetalhes.Replace("WITH Fonte", "Fonte")
            .Replace("DW.fat_ficha_financeira_cliente", "Financeira").Replace("dbo.VENDA_RESPONSAVEL", "Responsaveis");
        var row = (await connection.QueryAsync(sql, new { situacao = "INADIMPLENTE", limit = 1, offset = 0 })).Single();
        Assert.Equal(1, (int)row.NUM_VENDA);
        Assert.Equal("operador", (string)row.RESPONSAVEL);
        Assert.Equal(2L, (long)row.TOTAL_COUNT);
        Assert.Equal(1L, (long)row.PARCELAS);
        sql = Sources + RecuperaDashboardSql.SituacaoParcelasDetalhes.Replace("WITH Fonte", "Fonte")
            .Replace("DW.fat_ficha_financeira_cliente", "Financeira");
        var parcelas = (await connection.QueryAsync(sql, new { numVenda = 1, limit = 20, offset = 0 })).ToList();
        Assert.Single(parcelas);
        Assert.Equal(101, (int)parcelas[0].IDLAN);
        Assert.Equal("INADIMPLENTE", (string)parcelas[0].SITUACAO);
    }

    [RecuperaReadOnlyFact]
    public async Task Historico_DetalhamentoUsaEventoDoClienteEValorUmaVezPorVenda()
    {
        await using var connection = Connection();
        const string responsaveis = " Responsaveis AS (SELECT 1 AS NUM_VENDA_FK, 'operador' AS NOME_USUARIO_FK), ";
        var sql = Historico + responsaveis + RecuperaDashboardSql.ConvertidosMensaisDetalhes.Replace("WITH VendasMes", "VendasMes")
            .Replace("dw.carteira_analitica_hist", "Historico").Replace("dbo.VENDA_RESPONSAVEL", "Responsaveis");
        var rows = (await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026,4,30), dataFim = new DateTime(2026,4,30), situacao = "REINCIDENTE", limit = 20, offset = 0 })).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(300m, rows.Sum(r => (decimal)r.VALOR_VENDA));
        Assert.All(rows, r => Assert.Equal("111", (string)r.CPF_CNPJ));
    }

    [RecuperaReadOnlyFact]
    public async Task Ocorrencias_DiaPaginacaoOrdemEJoinSemDuplicidade()
    {
        const string source = """
            WITH Ocorrencias AS (
                SELECT * FROM (VALUES
                    (1, 10, 'ana', 'primeira', 'Contato', CAST('2026-09-10' AS datetime), CAST('08:00' AS time), NULL, NULL),
                    (2, 10, 'ana', 'segunda', 'Contato', CAST('2026-09-10' AS datetime), CAST('09:00' AS time), NULL, NULL),
                    (3, 10, 'ana', 'outro dia', 'Contato', CAST('2026-09-11' AS datetime), CAST('10:00' AS time), NULL, NULL),
                    (4, 20, 'ana', 'adimplente', 'Contato', CAST('2026-09-10' AS datetime), CAST('10:00' AS time), NULL, NULL)
                ) o(ID, NUM_VENDA_FK, NOME_USUARIO_FK, DESCRICAO, STATUS_OCORRENCIA, DT_OCORRENCIA, HORA_OCORRENCIA, PROXIMA_ACAO, PROTOCOLO)
            ), Situacoes AS (
                SELECT * FROM (VALUES (10, 'SIM'), (10, 'SIM'), (20, 'NAO')) f(NUM_VENDA, INADIMPLENTE)
            )
            """;
        await using var connection = Connection();
        var sql = source + RecuperaDashboardSql.OcorrenciasDiaDetalhes.Replace("dbo.OCORRENCIAS", "Ocorrencias").Replace("DW.fat_analise_inadimplencia_v4", "Situacoes");
        var rows = (await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026, 9, 10), offset = 0, limit = 1 })).ToList();
        Assert.Single(rows);
        Assert.Equal(2, (int)rows[0].ID);
        Assert.Equal(2L, (long)rows[0].TOTAL_COUNT);
        var next = (await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026, 9, 10), offset = 1, limit = 1 })).Single();
        Assert.Equal(1, (int)next.ID);
        Assert.Empty(await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026, 9, 12), offset = 0, limit = 1 }));
    }

    [RecuperaReadOnlyFact]
    public async Task Negativadas_StatusReaisSemSomarPaiGarantidorOuTentativaAnterior()
    {
        const string source = """
            WITH Solicitacoes AS (
                SELECT id AS ID, venda AS NUM_VENDA_FK, parcela AS NUMERO_PARCELA,
                    'contrato' AS CONTRACT_NUMBER, '11111111111' AS DOCUMENTO_DEVEDOR,
                    valor AS VALOR, CAST(data AS datetime) AS DT_CRIACAO, status AS STATUS,
                    tipo AS TIPO_REGISTRO, pai AS ID_SOLICITACAO_PAI
                FROM (VALUES
                    (1,10,NULL,300.00,'2026-09-10','APROVADA','PRINCIPAL',NULL),
                    (2,10,1,100.00,'2026-09-10','NEGATIVADO_SUCESSO','PRINCIPAL',1),
                    (3,10,2,200.00,'2026-09-10','AGUARDANDO_RETORNO','PRINCIPAL',1),
                    (4,10,1,100.00,'2026-09-10','NEGATIVADO_SUCESSO','GARANTIDOR',1),
                    (5,10,2,200.00,'2025-01-01','REJEITADA','PRINCIPAL',1),
                    (6,20,1,50.00,'2026-09-10','REJEITADA','PRINCIPAL',NULL),
                    (7,30,1,60.00,'2026-09-10','APROVADA','PRINCIPAL',NULL),
                    (8,40,1,70.00,'2026-09-10','AGUARDANDO_APROVACAO','PRINCIPAL',NULL),
                    (9,50,1,80.00,'2026-09-10','NEGATIVADO_ERRO','PRINCIPAL',NULL),
                    (10,60,1,90.00,'2026-09-10','ENVIADO_SERASA','PRINCIPAL',NULL),
                    (11,70,1,110.00,'2026-09-10','NEGATIVADO_SUCESSO','PRINCIPAL',NULL),
                    (12,80,1,120.00,'2026-09-10','PENDENTE_ENVIO','PRINCIPAL',NULL),
                    (13,90,1,130.00,'2026-09-10','APROVADA_FALHA_ENVIO','PRINCIPAL',NULL)
                ) s(id,venda,parcela,valor,data,status,tipo,pai)
            ), Baixas AS (
                SELECT 11 AS ID_SOLICITACAO_NEGATIVACAO, 'BAIXADO_SUCESSO' AS STATUS
            ),
            """;
        await using var connection = Connection();
        var sql = source + RecuperaDashboardSql.Negativadas.Replace("WITH Registros", "Registros")
            .Replace("dbo.SERASA_PEFIN_SOLICITACOES", "Solicitacoes").Replace("dbo.SERASA_PEFIN_BAIXAS", "Baixas");
        var rows = (await connection.QueryAsync(sql, new { dataInicio = new DateTime(2026, 9, 10), dataFim = new DateTime(2026, 9, 10) })).ToList();
        Assert.Equal(9, (int)rows[0].TOTAL_VENDAS);
        Assert.Equal(1, (int)rows[0].VENDAS_NEGATIVADAS);
        Assert.Equal(1010m, (decimal)rows[0].VALOR_TOTAL);
        Assert.Equal(50m, (decimal)rows.Single(r => r.STATUS == "REJEITADA").VALOR);
        Assert.Equal(100m, (decimal)rows.Single(r => r.STATUS == "NEGATIVADO_SUCESSO").VALOR);
        Assert.Equal(110m, (decimal)rows.Single(r => r.STATUS == "BAIXADO_SUCESSO").VALOR);
        Assert.All(rows, row => Assert.Equal(1, (int)row.VENDAS));
        Assert.Empty(await connection.QueryAsync(sql, new { dataInicio = new DateTime(2025, 1, 1), dataFim = new DateTime(2025, 1, 1) }));
    }
}

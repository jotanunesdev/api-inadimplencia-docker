using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

// Somente SELECT com dados sintéticos. Nunca executa migration/procedure no banco configurado.
public sealed class InadimplenciaObservadaReadOnlyTests
{
    private const string Sources = """
        WITH Capturas AS (
            SELECT ID,ANTERIOR_ID,CAST(DT AS datetime2) OBSERVADO_EM FROM (VALUES
                (1,CAST(NULL AS int),'2026-01-01'),(2,1,'2026-01-10'),(3,2,'2026-01-20'),(4,3,'2026-02-01')
            ) c(ID,ANTERIOR_ID,DT)
        ), Vendas AS (
            SELECT CAPTURA_ID,NUM_VENDA,SITUACAO,SITUACAO INAD_FONTE,QUITACAO_ANTERIOR FROM (VALUES
                (1,1,1,0),(1,2,0,0),(1,3,1,0),(1,4,1,0),
                (2,1,1,0),(2,2,1,0),(2,3,0,0),
                (3,1,0,1),(3,2,0,1),(3,3,0,0),(3,4,0,0),
                (4,1,1,0),(4,2,0,0),(4,3,0,0),(4,4,CAST(NULL AS int),0)
            ) v(CAPTURA_ID,NUM_VENDA,SITUACAO,QUITACAO_ANTERIOR)
        ), Cadastro AS (
            SELECT 1 NUM_VENDA,'Cliente sintético' CLIENTE,'00000000000' CPF_CNPJ
        ), Responsaveis AS (SELECT 1 NUM_VENDA_FK,'Operador sintético' NOME_USUARIO_FK)
        """;

    private static string Sql(string query) => Sources + "," + query.TrimStart().Substring(5)
        .Replace("dbo.INAD_CAPTURA", "Capturas")
        .Replace("dbo.INAD_VENDA_OBSERVADA", "Vendas")
        .Replace("DW.fat_analise_inadimplencia_v4", "Cadastro")
        .Replace("dbo.VENDA_RESPONSAVEL", "Responsaveis");
    private static SqlConnection Connection() => new(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));
    private static object Parameters(string inicio = "2026-01-01", string fim = "2026-03-31", int offset = 0, int limit = 100, string? situacao = null) =>
        new { dataInicio = DateTime.Parse(inicio), dataFim = DateTime.Parse(fim), offset, limit, situacao };

    [RecuperaReadOnlyFact]
    public async Task BaseInicialNaoEConversao_PagamentoParcialNaoRecuperaVenda_EReentradaEObservada()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(Sql(InadimplenciaObservadaSql.Mensais), Parameters())).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(3, (int)rows[0].BASE_INADIMPLENTES);
        Assert.Equal(1, (int)rows[0].ENTRADAS); // venda 2, não as três inadimplentes iniciais
        Assert.Equal(2, (int)rows[0].SAIDAS); // 1 e 2 somente na terceira captura
        Assert.Equal(-1, (int)rows[0].SALDO);
        Assert.Equal(3, (int)rows[0].SEM_COMPARACAO); // saída sem quitação, desaparecimento e reaparição
        Assert.Equal(1, (int)rows[1].ENTRADAS);
        Assert.Equal(1, (int)rows[1].REINCIDENTES); // já incluída nas entradas
        Assert.Equal(1, (int)rows[1].TOTAL_INADIMPLENTES);
        Assert.Null(rows[2].ENTRADAS);
        Assert.Null(rows[2].TOTAL_INADIMPLENTES);
    }

    [RecuperaReadOnlyFact]
    public async Task FiltroMantemComparacaoAnteriorAoPeriodoESaidasNaoConfirmadasFicamSeparadas()
    {
        await using var connection = Connection();
        var month = (await connection.QueryAsync(Sql(InadimplenciaObservadaSql.Mensais), Parameters("2026-01-20", "2026-01-20"))).Single();
        Assert.Equal(2, (int)month.SAIDAS);
        Assert.Equal(0, (int)month.ENTRADAS);
        Assert.Equal(1, (int)month.CAPTURAS);
        var details = (await connection.QueryAsync(Sql(InadimplenciaObservadaSql.Detalhes), Parameters("2026-01-10", "2026-01-10"))).ToList();
        Assert.DoesNotContain(details, d => (int)d.NUM_VENDA == 1); // pagou só uma: permanece INAD
        Assert.Equal("SEM_COMPARACAO", (string)details.Single(d => (int)d.NUM_VENDA == 3).EVENTO);
        Assert.Equal("SEM_COMPARACAO", (string)details.Single(d => (int)d.NUM_VENDA == 4).EVENTO);
    }

    [RecuperaReadOnlyFact]
    public async Task DetalhesPaginamMovimentosSemDuplicarVendaEContamRecorrenciaNoPeriodo()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(Sql(InadimplenciaObservadaSql.Detalhes), Parameters(situacao: "REGULARIZADO", limit: 1, offset: 1))).ToList();
        Assert.Single(rows);
        Assert.Equal(2L, (long)rows[0].TOTAL_COUNT);
        Assert.Equal(2, (int)rows[0].NUM_VENDA);
        var feb = (await connection.QueryAsync(Sql(InadimplenciaObservadaSql.Mensais), Parameters("2026-02-01", "2026-02-28"))).Single();
        Assert.Equal(1, (int)feb.REINCIDENTES);
    }

    [RecuperaReadOnlyFact]
    public async Task PrimeiraCapturaSoDefineBase()
    {
        await using var connection = Connection();
        var row = (await connection.QueryAsync(Sql(InadimplenciaObservadaSql.Mensais), Parameters("2026-01-01", "2026-01-01"))).Single();
        Assert.Equal(0, (int)row.COMPARACOES);
        Assert.Equal(0, (int)row.ENTRADAS);
        Assert.Equal(0, (int)row.SAIDAS);
        Assert.Equal(3, (int)row.TOTAL_INADIMPLENTES);
    }

    [RecuperaReadOnlyFact]
    public async Task SelectDaCapturaExigeTodasAsParcelasQuitadasENaoAceitaCancelamentoOuIdAmbiguo()
    {
        // Extrai SOMENTE os SELECTs da procedure, substituindo fontes por VALUES.
        // Não envia CREATE, INSERT, EXEC, transação ou tabelas temporárias ao servidor.
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "018_inadimplencia_observacoes.sql"));
        static string Between(string source, string start, string end)
        {
            var begin = source.IndexOf(start, StringComparison.Ordinal);
            var finish = source.IndexOf(end, begin, StringComparison.Ordinal);
            Assert.True(begin >= 0 && finish > begin);
            return source[begin..finish].Trim().TrimEnd(';');
        }
        var parcelas = Between(script, "SELECT p.NUM_VENDA", "CREATE INDEX IX_Parcelas").Replace("INTO #Parcelas", "");
        var ficha = Between(script, "SELECT f.NUM_VENDA,f.IDLAN", "CREATE INDEX IX_Ficha").Replace("INTO #Ficha", "");
        var result = Between(script, "SELECT @id,v.NUM_VENDA", "INSERT dbo.INAD_PARCELA_OBSERVADA");
        var fixture = """
            WITH Vendas AS (
                SELECT v.NUM_VENDA,v.FLAG,v.REPASSE FROM (VALUES
                    (1,'SIM','Desbloqueado'),(2,'NAO','Desbloqueado'),(3,'NAO','Desbloqueado'),
                    (4,'NAO','DISTRATO'),(5,'SIM','Desbloqueado'),(6,'NAO','Desbloqueado'),(7,'NAO','Desbloqueado'),
                    (8,'SIM','Desbloqueado'),(9,'SIM','Desbloqueado'),(10,'SIM','Desbloqueado'),
                    (11,'SIM','Desbloqueado'),(12,'NAO','Desbloqueado'),(13,'SIM','Desbloqueado'),
                    (14,'SIM','Desbloqueado'),(15,'SIM','Desbloqueado'),(16,'SIM','Desbloqueado'),(17,'SIM','Desbloqueado')
                ) v(NUM_VENDA,FLAG,REPASSE)
            ), FonteParcelas AS (
                SELECT p.*,CAST('2026-01-01' AS date) DATAVENCIMENTO,2000.00 VALOR
                FROM (VALUES (1,'11','NAO'),(1,'12','SIM'),(2,'21','NAO'),(2,'22','NAO'),
                    (3,'31','NAO'),(4,'41','NAO'),(5,'51','SIM'),(5,'51','NAO'),(6,'61','NAO'),(7,'71','NAO'),
                    (8,'81','NAO'),(9,'91','SIM'),(10,'101','SIM'),(11,'111','SIM'),(12,'121','SIM'),
                    (13,'131','SIM'),(14,'141','SIM'),(15,'151','SIM'),(16,'161','SIM'),(16,'161','SIM'),
                    (17,NULL,'SIM')) p(NUM_VENDA,IDLAN,INADIMPLENTE)
            ), FonteFicha AS (
                SELECT f.*,CAST(CASE WHEN NUM_VENDA=9 THEN '2026-04-01' ELSE '2026-01-01' END AS datetime) DATA_VENCIMENTO,'Efetivada' SITUACAO_VENDA,
                    CAST(NULL AS datetime) DATA_CANCELAMENTO,
                    CASE WHEN NUM_VENDA IN (3,10) THEN CAST('2026-02-01' AS datetime) END DATA_CANCELAMENTO_LAN,
                    CASE WHEN STATUSLAN='BAIXADO' THEN CAST('2026-02-01' AS datetime) END DATA_BAIXA
                FROM (VALUES (1,11,'BAIXADO',0.00,2000.00),(1,12,'EM ABERTO',2000.00,0.00),
                    (2,21,'BAIXADO',0.00,2000.00),(2,22,'BAIXADO',0.00,2000.00),
                    (3,31,'BAIXADO',0.00,2000.00),(4,41,'BAIXADO',0.00,2000.00),
                    (5,51,'EM ABERTO',2000.00,0.00),(6,61,'BAIXADO',0.00,2000.00),
                    (7,71,'BAIXADO',0.00,2000.00),(7,71,'BAIXADO',0.00,2000.00),
                    (8,81,'BAIXADO',0.00,2000.00),(9,91,'EM ABERTO',2000.00,0.00),
                    (10,101,'EM ABERTO',2000.00,0.00),(12,121,'EM ABERTO',2000.00,0.00),
                    (13,131,'BAIXA PARCIAL',1000.00,1000.00),(14,141,'BAIXA PARCIAL',0.00,1000.00),
                    (15,151,'BAIXADO',0.00,2000.00),(16,161,'EM ABERTO',2000.00,0.00),
                    (17,171,'EM ABERTO',2000.00,0.00)) f(NUM_VENDA,IDLAN,STATUSLAN,SALDO_RECEBER,RECEBIDO)
            ), Anteriores AS (
                SELECT 1 CAPTURA_ID,NUM_VENDA,CASE WHEN NUM_VENDA=6 THEN 0 ELSE 1 END DIVIDAS_IDENTIFICADAS FROM Vendas
            ), Dividas AS (
                SELECT 1 CAPTURA_ID,NUM_VENDA,IDLAN FROM (VALUES (1,11),(1,12),(2,21),(2,22),(3,31),(4,41),(5,51),(6,61),(7,71)) d(NUM_VENDA,IDLAN)
            )
            """;
        var sql = (fixture + ",Parcelas AS (" + parcelas + "),Ficha AS (" + ficha
            + "),Resultado(CAPTURA_ID,NUM_VENDA,INAD_FONTE,SITUACAO,DIVIDAS_IDENTIFICADAS,QUITACAO_ANTERIOR) AS ("
            + result + ") SELECT * FROM Resultado ORDER BY NUM_VENDA;")
            .Replace("#Vendas", "Vendas").Replace("#Parcelas", "Parcelas").Replace("#Ficha", "Ficha")
            .Replace("DW.fat_analise_inadimplencia_parcelas", "FonteParcelas")
            .Replace("DW.fat_ficha_financeira_cliente", "FonteFicha")
            .Replace("dbo.INAD_VENDA_OBSERVADA", "Anteriores").Replace("dbo.INAD_PARCELA_OBSERVADA", "Dividas");
        Assert.DoesNotContain("INSERT ", sql);
        Assert.DoesNotContain("EXEC ", sql);
        Assert.DoesNotContain("CREATE ", sql);
        await using var connection = Connection();
        var rows = (await connection.QueryAsync(sql, new { id = 2, anterior = 1, agora = new DateTime(2026, 3, 1) })).ToList();
        Assert.Equal(1, (int)rows[0].SITUACAO); // pagamento de apenas uma parcela
        Assert.Equal(0, (int)rows[0].QUITACAO_ANTERIOR);
        Assert.Equal(0, (int)rows[1].SITUACAO); // duas quitadas
        Assert.Equal(1, (int)rows[1].QUITACAO_ANTERIOR);
        Assert.Null(rows[2].SITUACAO); // cancelamento não é recuperação
        Assert.Null(rows[3].SITUACAO); // distrato
        Assert.Equal(0, (int)rows[4].DIVIDAS_IDENTIFICADAS); // IDLAN com flags conflitantes
        Assert.Equal(0, (int)rows[5].QUITACAO_ANTERIOR); // pendências anteriores incompletas
        Assert.Null(rows[6].SITUACAO); // ficha com duplicidade
        Assert.Null(rows[4].SITUACAO); // indicador de parcela conflitante não confirma atraso
        Assert.Null(rows[7].SITUACAO); // SIM no resumo, mas parcela paga
        Assert.Equal(1, (int)rows[7].INAD_FONTE); // KPI oficial preservado, sem forçar conversão
        Assert.Null(rows[8].SITUACAO); // parcela futura
        Assert.Null(rows[9].SITUACAO); // parcela cancelada
        Assert.Null(rows[10].SITUACAO); // sem ficha
        Assert.Null(rows[11].SITUACAO); // NAO no resumo, mas parcela atrasada
        Assert.Equal(1, (int)rows[12].SITUACAO); // parcial ainda com saldo pendente
        Assert.Null(rows[13].SITUACAO); // parcial com saldo inconsistente
        Assert.Null(rows[14].SITUACAO); // análise ainda SIM, ficha já quitada
        Assert.Equal(1, (int)rows[15].SITUACAO); // linhas idênticas consolidadas sem duplicar venda
        Assert.Null(rows[16].SITUACAO); // sem identificação da parcela
    }
}

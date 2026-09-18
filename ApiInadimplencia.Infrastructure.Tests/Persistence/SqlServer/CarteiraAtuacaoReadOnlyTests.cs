using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public sealed class CarteiraAtuacaoReadOnlyTests
{
    // Apenas SELECT sintético. Nenhum DDL/DML, inclusive quando executado no servidor remoto.
    private const string Sources = """
        WITH Fonte AS (
            SELECT NUM_VENDA, SCORE, INADIMPLENTE, ID_CLIENTE, CLIENTE,
                CAST(NULL AS varchar(20)) CPF_CNPJ, 'Obra' EMPREENDIMENTO, 'Ligação' SUGESTAO,
                CAST('2026-09-01' AS date) VENCIMENTO_MAIS_ANTIGO, 100.0 VALOR_INADIMPLENTE
            FROM (VALUES (1,5,'SIM',1,'Maria'),(2,6,'SIM',1,'Maria'),
                (3,6,'SIM',2,'Ana'),(4,10,'SIM',3,'Jose'),(5,10,'SIM',4,'Joao'),
                (6,NULL,'SIM',NULL,'Sem score'),(7,2,'SIM',NULL,'Sem documento'),
                (8,10,'NAO',8,'Adimplente'),(9,10,'SIM',9,'Sem responsavel'),
                (10,10,'SIM',10,'Ambiguo'),(11,10,'SIM',11,'Contato recente')) v(NUM_VENDA,SCORE,INADIMPLENTE,ID_CLIENTE,CLIENTE)
        ), Atribuicoes AS (
            SELECT NUM_VENDA_FK,NOME_USUARIO_FK,CAST('2026-09-01' AS datetime) DT_ATRIBUICAO
            FROM (VALUES (1,'operador'),(2,'operador'),(3,'operador'),(4,'operador'),
                (5,'operador'),(6,'operador'),(7,'operador'),(10,'a'),(10,'b'),(11,'operador')) r(NUM_VENDA_FK,NOME_USUARIO_FK)
        ), Ocorrencias AS (
            SELECT ID,NUM_VENDA_FK,NOME_USUARIO_FK,STATUS_OCORRENCIA,
                CAST(DATA AS date) DT_OCORRENCIA,CAST('10:00' AS time) HORA_OCORRENCIA,
                'Contato teste' DESCRICAO,CAST(NULL AS datetime) PROXIMA_ACAO, 'Teste' PROTOCOLO
            FROM (VALUES (1,3,'operador',N'Cobrança via WhatsApp','2020-01-01'),
                (2,3,'operador',N'Promessa de pagamento','2020-01-02'),
                (3,4,'outro',N'Cobrança via WhatsApp','2026-09-10'),
                (4,5,'operador',N'Aprovacao de negativacao','2026-09-10'),
                (5,11,'operador',N'Negociação em andamento','2026-09-10')) o(ID,NUM_VENDA_FK,NOME_USUARIO_FK,STATUS_OCORRENCIA,DATA)
        ),
        """;
    private static string Sql(string query) => Sources + query.Replace("WITH TiposContato", "TiposContato")
        .Replace("DW.fat_analise_inadimplencia_v4", "Fonte").Replace("dbo.VENDA_RESPONSAVEL", "Atribuicoes")
        .Replace("dbo.OCORRENCIAS", "Ocorrencias");
    private static object Parameters(string? category = null, int offset = 0, string? client = null, string? user = null, int? sale = null) =>
        new { situacao = category, offset, limit = 1, cliente = client, nomeUsuario = user, numVenda = sale };

    [RecuperaReadOnlyFact]
    public async Task ScoreCincoSeis_MultiplasVendas_Nulos_EHistoricoSemInventarCiclo()
    {
        await using var conn = new SqlConnection(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));
        var rows = (await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Resumo), Parameters())).ToList();
        Assert.Equal(10L, (long)rows[0].TOTAL_VENDAS);
        Assert.Equal(2L, (long)rows.Single(r => r.CATEGORIA == "NAO_APTO").QUANTIDADE);
        Assert.Equal(4L, (long)rows.Single(r => r.CATEGORIA == "APTO_SEM_REGISTRO").QUANTIDADE);
        Assert.Equal(2L, (long)rows.Single(r => r.CATEGORIA == "CICLO_PENDENTE").QUANTIDADE);
        Assert.Equal(1L, (long)rows.Single(r => r.CATEGORIA == "SEM_SCORE").QUANTIDADE);
        Assert.Equal(1L, (long)rows.Single(r => r.CATEGORIA == "RESPONSAVEL_AMBIGUO").QUANTIDADE);
        Assert.Equal(100m, rows.Sum(r => (decimal)r.PERCENTUAL));
        Assert.All(rows, r => { Assert.Equal(0, (int)r.CICLO_VALIDADO); Assert.Equal(1, (int)r.JURIDICO_DISPONIVEL); });
        using var distribution = JsonDocument.Parse((string)rows.Single(r => r.CATEGORIA == "APTO_SEM_REGISTRO").RESPONSAVEIS_JSON);
        var groups = distribution.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Equal(3, groups.Single(g => g.GetProperty("RESPONSAVEL").GetString() == "operador").GetProperty("QUANTIDADE").GetInt32());
        Assert.Equal(1, groups.Single(g => g.GetProperty("RESPONSAVEL").ValueKind == JsonValueKind.Null).GetProperty("QUANTIDADE").GetInt32());
        using var history = JsonDocument.Parse((string)rows.Single(r => r.CATEGORIA == "CICLO_PENDENTE").RESPONSAVEIS_JSON);
        Assert.Equal(2, Assert.Single(history.RootElement.EnumerateArray()).GetProperty("QUANTIDADE").GetInt32());
    }

    [RecuperaReadOnlyFact]
    public async Task Filtros_Paginacao_EZeroUsamMesmoUniverso()
    {
        await using var conn = new SqlConnection(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));
        var filtered = (await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Resumo), Parameters(client: "%Maria%", user: "operador"))).ToList();
        Assert.Equal(2L, (long)filtered[0].TOTAL_VENDAS);
        Assert.Equal(50m, (decimal)filtered.Single(r => r.CATEGORIA == "NAO_APTO").PERCENTUAL);
        using var distribution = JsonDocument.Parse((string)filtered.Single(r => r.CATEGORIA == "APTO_SEM_REGISTRO").RESPONSAVEIS_JSON);
        Assert.Equal(1, Assert.Single(distribution.RootElement.EnumerateArray()).GetProperty("QUANTIDADE").GetInt32());
        var first = Assert.Single(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Detalhes), Parameters("NAO_APTO")));
        var second = Assert.Single(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Detalhes), Parameters("NAO_APTO", 1)));
        Assert.Equal(1, (int)first.NUM_VENDA); Assert.Equal(7, (int)second.NUM_VENDA);
        Assert.Equal(2L, (long)second.TOTAL_COUNT);
        Assert.Empty(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Detalhes), Parameters("NAO_APTO", 2)));
        var empty = await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Resumo), Parameters(client: "%inexistente%"));
        Assert.All(empty, r => { Assert.Equal(0L, (long)r.TOTAL_VENDAS); Assert.Equal(0m, (decimal)r.PERCENTUAL); Assert.Equal("[]", (string)r.RESPONSAVEIS_JSON); });
    }

    [RecuperaReadOnlyFact]
    public async Task ContatosSomenteDoResponsavel_ECategoriaFiltrosPreservados()
    {
        await using var conn = new SqlConnection(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));
        var row = Assert.Single(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Contatos), Parameters("CICLO_PENDENTE", sale: 3)));
        Assert.Equal(2L, (long)row.TOTAL_COUNT);
        Assert.Equal(2, (int)row.ID);
        Assert.Empty(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Contatos), Parameters("CICLO_PENDENTE", client: "%Maria%", sale: 3)));
        Assert.Empty(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Contatos), Parameters("APTO_SEM_REGISTRO", sale: 4)));
        Assert.Empty(await conn.QueryAsync(Sql(CarteiraAtuacaoSql.Contatos), Parameters("APTO_SEM_REGISTRO", sale: 5)));
    }
}

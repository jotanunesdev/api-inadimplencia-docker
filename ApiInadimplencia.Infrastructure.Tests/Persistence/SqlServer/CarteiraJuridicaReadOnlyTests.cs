using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public sealed class CarteiraJuridicaReadOnlyTests
{
    // Somente SELECT/VALUES; nunca cria tabelas nem altera dados reais.
    private const string Sources = """
        WITH Juridico AS (
            SELECT CAST(id AS varchar(20)) referencia,dt referencia_data,estado situacao,'Processo teste' numero
            FROM (VALUES (1,'2025-01-01',N'Novo Processo'),(1,'2026-01-01',N'Arquivado'),
                (2,'2025-01-01',N'Cancelado'),(2,'2026-01-01',N'Novo Processo'),(2,'2026-01-01',N'Novo Processo'),
                (3,'2026-01-01',N'Avaliação Jurídico'),(4,'2026-01-01',N'Instrução'),
                (5,'2026-01-01',N'Pendência Comercial'),(6,'2026-01-01',N'Recurso'),
                (7,'2026-01-01',N'Conciliação'),(7,'2026-01-01',N'Extinto'),
                (8,'2025-01-01',N'Novo Processo'),(8,'data invalida',N'Cancelado'),
                (9,'2026-01-01',N'Novo Processo'),(10,'2026-01-01',N'Novo Processo'),
                (NULL,'2026-01-01',N'Novo Processo')) j(id,dt,estado)
        ), Identificadores AS (
            SELECT idprocesso,idcliente,documento_cliente,'2026-03-30' referencia_data
            FROM (VALUES (1,1,'111.111.111-11'),(2,1,'11111111111'),(3,1,'11111111111'),
                (4,1,'22222222222'),(5,3,'33333333333'),(6,4,'44444444444'),
                (7,5,'55555555555'),(8,6,'66666666666'),(9,7,'77777777777'),(9,7,'88888888888')) p(idprocesso,idcliente,documento_cliente)
        ), Analise AS (
            SELECT NUM_VENDA,ID_CLIENTE,CPF_CNPJ,INADIMPLENTE,
                'Cliente teste' CLIENTE,'Obra teste' EMPREENDIMENTO,6 SCORE,'Ligação' SUGESTAO,100.0 VALOR_INADIMPLENTE
            FROM (VALUES (10,1,'11111111111','SIM'),(11,1,'111.111.111-11','SIM'),
                (12,2,'22222222222','SIM'),(13,3,'33333333333','SIM'),(14,4,'44444444444','NAO'),
                (15,5,'55555555555','SIM'),(16,6,'66666666666','SIM'),(17,7,'77777777777','SIM')) v(NUM_VENDA,ID_CLIENTE,CPF_CNPJ,INADIMPLENTE)
        ), Atribuicoes AS (
            SELECT * FROM (VALUES (10,'primeiro'),(11,'segundo')) r(NUM_VENDA_FK,NOME_USUARIO_FK)
        ),
        """;
    private static string Sql(string sql) => Sources + sql.Replace("WITH Regras", "Regras")
        .Replace("dw.fat_processos_juridicos_cv", "Juridico").Replace("dw.fat_processojur_cv", "Identificadores")
        .Replace("DW.fat_analise_inadimplencia_v4", "Analise").Replace("dbo.VENDA_RESPONSAVEL", "Atribuicoes");
    private static object Args(string? user = null, string? client = null, int offset = 0, int? sale = null) =>
        new { semResponsavel = false, nomeUsuario = user, cliente = client, offset, limit = 1, numVenda = sale };
    private static SqlConnection Connection() => new(Environment.GetEnvironmentVariable("RECUPERA_SQL_READONLY"));

    [RecuperaReadOnlyFact]
    public async Task UltimaSituacao_DeduplicaProcessosClientesESemConsolidarVendas()
    {
        await using var connection = Connection();
        var row = Assert.Single(await connection.QueryAsync(Sql(CarteiraJuridicaSql.Resumo), Args()));
        Assert.Equal(1L, (long)row.CLIENTES_ATIVOS);
        Assert.Equal(2L, (long)row.VENDAS_DOS_CLIENTES);
        Assert.Equal(2, (int)row.PROCESSOS_ATIVOS);
        Assert.Equal(1, (int)row.PROCESSOS_ENCERRADOS);
        Assert.Equal(3, (int)row.PROCESSOS_NAO_CLASSIFICADOS); // desconhecido, empate conflitante, data inválida
        Assert.Equal(7L, (long)row.BASE_VENDAS);
        Assert.Equal(2L, (long)row.GLOBAL_PROCESSOS_SEM_PONTE); // ausente e identidade conflitante
        Assert.Equal(1L, (long)row.GLOBAL_LINHAS_SEM_REFERENCIA);
    }

    [RecuperaReadOnlyFact]
    public async Task DetalhesPorVendaEPaginacao_PreservamFiltrosESugestao()
    {
        await using var connection = Connection();
        var row = Assert.Single(await connection.QueryAsync(Sql(CarteiraJuridicaSql.Detalhes), Args(offset: 1)));
        Assert.Equal(11, (int)row.NUM_VENDA); Assert.Equal(2L, (long)row.TOTAL_COUNT);
        Assert.Equal("segundo", (string)row.RESPONSAVEL); Assert.Equal("Ligação", (string)row.SUGESTAO);
        var filtered = Assert.Single(await connection.QueryAsync(Sql(CarteiraJuridicaSql.Resumo), Args(user: "primeiro")));
        Assert.Equal(1L, (long)filtered.VENDAS_DOS_CLIENTES);
        Assert.Equal(2, (int)filtered.PROCESSOS_ATIVOS);
        Assert.Empty(await connection.QueryAsync(Sql(CarteiraJuridicaSql.Detalhes), Args(offset: 2)));
        var empty = Assert.Single(await connection.QueryAsync(Sql(CarteiraJuridicaSql.Resumo), Args(client: "%ausente%")));
        Assert.Equal(0L, (long)empty.CLIENTES_ATIVOS); Assert.Equal(0L, (long)empty.VENDAS_DOS_CLIENTES);
    }

    [RecuperaReadOnlyFact]
    public async Task ProcessoExigeDocumentoEId_EFiltraVendaEResponsavel()
    {
        await using var connection = Connection();
        var row = Assert.Single(await connection.QueryAsync(Sql(CarteiraJuridicaSql.ProcessosDetalhes), Args(sale: 10)));
        Assert.Equal(2, (int)row.IDPROCESSO); Assert.Equal(2L, (long)row.TOTAL_COUNT);
        Assert.Equal("Novo Processo", (string)row.SITUACAO);
        Assert.Empty(await connection.QueryAsync(Sql(CarteiraJuridicaSql.ProcessosDetalhes), Args(user: "segundo", sale: 10)));
        Assert.Empty(await connection.QueryAsync(Sql(CarteiraJuridicaSql.ProcessosDetalhes), Args(sale: 12))); // mesmo documento, ID diferente
        Assert.Empty(await connection.QueryAsync(Sql(CarteiraJuridicaSql.ProcessosDetalhes), Args(sale: 17))); // ponte ambígua
    }

    [RecuperaReadOnlyFact]
    public async Task ClassificacaoCentral_NaoIncluiEncerradosOuDesconhecidosComoAtivos()
    {
        await using var connection = Connection();
        var rows = (await connection.QueryAsync("SELECT * FROM (VALUES " + CarteiraJuridicaSql.Classificacao + ") r(SITUACAO,CLASSE)")).ToList();
        Assert.Equal(12, rows.Count(r => r.CLASSE == "ATIVO"));
        Assert.Equal(6, rows.Count(r => r.CLASSE == "ENCERRADO"));
        Assert.DoesNotContain(rows, r => r.SITUACAO == "Envio de Notificação" || r.SITUACAO == "Pendência Comercial" || r.SITUACAO == "Novo Cliente");
    }
}

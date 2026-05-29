using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public class InadimplenciaDbContextTests
{
    [Fact]
    public void DbContext_Should_CreateSuccessfully_WithInMemoryProvider()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<InadimplenciaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SqlServerOptions.SectionName}:ConnectionString"] = "Data Source=:memory:",
                [$"{SqlServerOptions.SectionName}:CommandTimeoutSeconds"] = "30"
            })
            .Build();

        // Act
        using var context = new InadimplenciaDbContext(options, configuration);

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_Should_PersistAndRetrieve_Ocorrencia()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<InadimplenciaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SqlServerOptions.SectionName}:ConnectionString"] = "Data Source=:memory:",
                [$"{SqlServerOptions.SectionName}:CommandTimeoutSeconds"] = "30"
            })
            .Build();

        using var context = new InadimplenciaDbContext(options, configuration);
        var ocorrencia = Ocorrencia.Criar(
            numVendaFk: 12345,
            nomeUsuarioFk: "testuser",
            descricao: "Test occurrence",
            statusOcorrencia: "EmAndamento",
            dtOcorrencia: DateTime.UtcNow,
            horaOcorrencia: "10:00",
            proximaAcao: "Call customer",
            protocolo: "20250112000001");

        // Act
        context.Ocorrencias.Add(ocorrencia);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.Ocorrencias.FirstOrDefaultAsync(o => o.NumVendaFk == 12345);
        retrieved.Should().NotBeNull();
        retrieved!.Descricao.Should().Be("Test occurrence");
        retrieved.NomeUsuarioFk.Should().Be("testuser");
    }

    [Fact]
    public async Task DbContext_Should_PersistAndRetrieve_Atendimento()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<InadimplenciaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SqlServerOptions.SectionName}:ConnectionString"] = "Data Source=:memory:",
                [$"{SqlServerOptions.SectionName}:CommandTimeoutSeconds"] = "30"
            })
            .Build();

        using var context = new InadimplenciaDbContext(options, configuration);
        var atendimento = ApiInadimplencia.Domain.Atendimentos.Atendimento.Criar(
            protocolo: "2025011200001",
            cpf: "12345678901",
            numVendaFk: 12345,
            dadosVendaJson: "{\"teste\": \"valor\"}");

        // Act
        context.Atendimentos.Add(atendimento);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.Atendimentos.FirstOrDefaultAsync(a => a.NumVendaFk == 12345);
        retrieved.Should().NotBeNull();
        retrieved!.Protocolo.Should().Be("2025011200001");
        retrieved.Cpf.Should().Be("12345678901");
    }

    [Fact]
    public async Task DbContext_Should_Update_Ocorrencia()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<InadimplenciaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SqlServerOptions.SectionName}:ConnectionString"] = "Data Source=:memory:",
                [$"{SqlServerOptions.SectionName}:CommandTimeoutSeconds"] = "30"
            })
            .Build();

        using var context = new InadimplenciaDbContext(options, configuration);
        var ocorrencia = Ocorrencia.Criar(
            numVendaFk: 12345,
            nomeUsuarioFk: "testuser",
            descricao: "Test occurrence",
            statusOcorrencia: "EmAndamento",
            dtOcorrencia: DateTime.UtcNow,
            horaOcorrencia: "10:00");

        context.Ocorrencias.Add(ocorrencia);
        await context.SaveChangesAsync();

        // Act
        ocorrencia.Atualizar(descricao: "Updated description");
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.Ocorrencias.FirstOrDefaultAsync(o => o.NumVendaFk == 12345);
        retrieved.Should().NotBeNull();
        retrieved!.Descricao.Should().Be("Updated description");
    }
}

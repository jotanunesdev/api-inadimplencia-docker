using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Inadimplencias.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using ApiInadimplencia.Infrastructure;

namespace ApiInadimplencia.Application.Tests.Features.Inadimplencias.Queries;

[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
public class InadimplenciaQueryIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILegacySqlExecutor? _executor;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        
        // Add infrastructure
        services.AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddSingleton<SqlServerConnectionFactory>();
        services.AddScoped<ILegacySqlExecutor, LegacySqlExecutor>();

        _serviceProvider = services.BuildServiceProvider();
        _executor = _serviceProvider.GetRequiredService<ILegacySqlExecutor>();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task ListInadimplenciasQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new ListInadimplenciasQueryHandler(_executor);

        // Act
        var result = await handler.HandleAsync(new ListInadimplenciasQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<ApiInadimplencia.Application.Features.Inadimplencias.Dtos.InadimplenciaDto>>();
    }

    [Fact]
    public async Task GetInadimplenciaByCpfQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new GetInadimplenciaByCpfQueryHandler(_executor);
        var cpf = "12345678900"; // Test CPF

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByCpfQuery(cpf), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<ApiInadimplencia.Application.Features.Inadimplencias.Dtos.InadimplenciaDto>>();
    }

    [Fact]
    public async Task GetInadimplenciaByNumVendaQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new GetInadimplenciaByNumVendaQueryHandler(_executor);
        var numVenda = 1; // Test NUM_VENDA

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByNumVendaQuery(numVenda), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInadimplenciaByResponsavelQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new GetInadimplenciaByResponsavelQueryHandler(_executor);
        var nome = "admin"; // Test responsible name

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByResponsavelQuery(nome), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<ApiInadimplencia.Application.Features.Inadimplencias.Dtos.InadimplenciaDto>>();
    }

    [Fact]
    public async Task GetInadimplenciaByClienteQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new GetInadimplenciaByClienteQueryHandler(_executor);
        var nomeCliente = "Test"; // Test customer name

        // Act
        var result = await handler.HandleAsync(new GetInadimplenciaByClienteQuery(nomeCliente), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<ApiInadimplencia.Application.Features.Inadimplencias.Dtos.InadimplenciaDto>>();
    }
}

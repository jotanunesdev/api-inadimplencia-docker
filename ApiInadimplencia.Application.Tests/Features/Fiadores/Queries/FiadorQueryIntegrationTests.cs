using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Fiadores.Queries;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using ApiInadimplencia.Infrastructure;

namespace ApiInadimplencia.Application.Tests.Features.Fiadores.Queries;

[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
public class FiadorQueryIntegrationTests : IAsyncLifetime
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
    public async Task GetFiadoresByNumVendaQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new GetFiadoresByNumVendaQueryHandler(_executor);
        var numVenda = 1; // Test NUM_VENDA

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByNumVendaQuery(numVenda), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<ApiInadimplencia.Application.Features.Fiadores.Dtos.FiadorDto>>();
    }

    [Fact]
    public async Task GetFiadoresByCpfQuery_WhenSqlServerConfigured_ReturnsData()
    {
        // Skip if SQL Server not configured
        if (_executor is null || !_executor.IsConfigured)
        {
            return;
        }

        // Arrange
        var handler = new GetFiadoresByCpfQueryHandler(_executor);
        var cpf = "12345678900"; // Test CPF

        // Act
        var result = await handler.HandleAsync(new GetFiadoresByCpfQuery(cpf), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<ApiInadimplencia.Application.Features.Fiadores.Dtos.FiadorDto>>();
    }
}

using ApiInadimplencia.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_Should_RegisterRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlServer:ConnectionString"] = "Data Source=:memory:",
                ["SqlServer:CommandTimeoutSeconds"] = "30",
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/"
            })
            .Build();

        // Act
        services.AddInfrastructure(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_Should_ThrowException_WhenSqlServerOptionsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/"
            })
            .Build();

        // Act
        var action = () => services.AddInfrastructure(configuration);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddInfrastructure_Should_ThrowException_WhenRabbitMqOptionsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlServer:ConnectionString"] = "Data Source=:memory:",
                ["SqlServer:CommandTimeoutSeconds"] = "30"
            })
            .Build();

        // Act
        var action = () => services.AddInfrastructure(configuration);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddInfrastructure_Should_RegisterMassTransit()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlServer:ConnectionString"] = "Data Source=:memory:",
                ["SqlServer:CommandTimeoutSeconds"] = "30",
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/"
            })
            .Build();

        // Act
        services.AddInfrastructure(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var busControl = serviceProvider.GetService<IBusControl>();
        busControl.Should().NotBeNull();
    }
}

using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Notifications.Dtos;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Notifications;

public class NotificationDispatcherTests
{
    private readonly Mock<ICommandHandler<CreateNotificationCommand, Guid>> _createNotificationHandlerMock;
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<SseHub> _sseHubMock;
    private readonly Mock<ILogger<NotificationDispatcher>> _loggerMock;
    private readonly NotificationDispatcher _dispatcher;

    public NotificationDispatcherTests()
    {
        _createNotificationHandlerMock = new Mock<ICommandHandler<CreateNotificationCommand, Guid>>();
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _sseHubMock = new Mock<SseHub>(Mock.Of<ILogger<SseHub>>());
        _loggerMock = new Mock<ILogger<NotificationDispatcher>>();

        _dispatcher = new NotificationDispatcher(
            _createNotificationHandlerMock.Object,
            _notificationRepositoryMock.Object,
            _sseHubMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_CallsCreateNotificationCommand_And_SseHub()
    {
        // Arrange
        var tipo = NotificationType.VendaAtribuida;
        var username = "test.user";
        var numVenda = 12345;
        var mensagem = "Test notification";
        var notificationId = Guid.NewGuid();
        var notification = InadNotificacao.Criar(tipo, username, numVenda, null, mensagem);

        _createNotificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificationId);

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _dispatcher.DispatchAsync(tipo, username, numVenda, mensagem);

        // Assert
        _createNotificationHandlerMock.Verify(
            x => x.HandleAsync(It.Is<CreateNotificationCommand>(c =>
                c.Tipo == tipo &&
                c.Usuario == username &&
                c.NumVenda == numVenda &&
                c.Mensagem == mensagem &&
                c.DedupeKey == null),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _sseHubMock.Verify(
            x => x.BroadcastNotificationAsync(username, It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(notificationId, result);
    }

    [Fact]
    public async Task DispatchAsync_WhenSseHubFails_LogsWarning_And_ReturnsNotificationId()
    {
        // Arrange
        var tipo = NotificationType.VendaAtribuida;
        var username = "test.user";
        var numVenda = 12345;
        var mensagem = "Test notification";
        var notificationId = Guid.NewGuid();

        _createNotificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificationId);

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SSE error"));

        // Act
        var result = await _dispatcher.DispatchAsync(tipo, username, numVenda, mensagem);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        Assert.Equal(notificationId, result);
    }

    [Fact]
    public async Task DispatchManyAsync_CallsDispatchAsyncForEachUser()
    {
        // Arrange
        var tipo = NotificationType.VendaAtribuida;
        var usernames = new List<string> { "user1", "user2", "user3" };
        var numVenda = 12345;
        var mensagem = "Test notification";

        _createNotificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => InadNotificacao.Criar(tipo, "test", numVenda, null, mensagem));

        // Act
        var results = await _dispatcher.DispatchManyAsync(tipo, usernames, numVenda, mensagem);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(results.ContainsKey("user1"));
        Assert.True(results.ContainsKey("user2"));
        Assert.True(results.ContainsKey("user3"));

        _createNotificationHandlerMock
            .Verify(
            x => x.HandleAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task DispatchAsync_WithDedupeKey_ForwardsItToCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var dedupeKey = Guid.NewGuid().ToString();

        _createNotificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificationId);

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(InadNotificacao.Criar(NotificationType.SolicitacaoNegativacao, "aprovador", 12345, null, "{}", dedupeKey));

        // Act
        await _dispatcher.DispatchAsync(NotificationType.SolicitacaoNegativacao, "aprovador", 12345, "{}", null, dedupeKey);

        // Assert
        _createNotificationHandlerMock.Verify(
            x => x.HandleAsync(It.Is<CreateNotificationCommand>(c => c.DedupeKey == dedupeKey), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchManyAsync_WhenOneUserFails_ContinuesWithOthers()
    {
        // Arrange
        var tipo = NotificationType.VendaAtribuida;
        var usernames = new List<string> { "user1", "user2", "user3" };
        var numVenda = 12345;
        var mensagem = "Test notification";

        var callCount = 0;
        _createNotificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 2)
                    throw new Exception("User2 error");
                return Guid.NewGuid();
            });

        // Act
        var results = await _dispatcher.DispatchManyAsync(tipo, usernames, numVenda, mensagem);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.NotNull(results["user1"]);
        Assert.Null(results["user2"]); // Failed
        Assert.NotNull(results["user3"]);

        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

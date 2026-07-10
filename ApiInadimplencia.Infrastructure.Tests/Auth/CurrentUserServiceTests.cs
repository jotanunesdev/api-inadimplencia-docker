using System.Security.Claims;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Auth;

public class CurrentUserServiceTests
{
    [Fact]
    public void Username_WhenAuthenticated_ReturnsUsername()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.Name, "test.user") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Equal("test.user", username);
    }

    [Fact]
    public void IsAuthenticated_WhenAuthenticated_ReturnsTrue()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.Name, "test.user") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var isAuthenticated = userService.IsAuthenticated;

        // Assert
        Assert.True(isAuthenticated);
    }

    [Fact]
    public void Username_WhenNotAuthenticated_ReturnsNull()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Null(username);
    }

    [Fact]
    public void Username_WhenFluigSendsDisplayNameHeader_ReturnsNormalizedDisplayName()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Name"] = "  Aracy Mendonça  ";
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Equal("aracy mendonça", username);
        Assert.True(userService.IsAuthenticated);
    }

    [Fact]
    public void Username_WhenFluigSendsUserCodeHeader_ReturnsNormalizedUserCode()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Code"] = "  Aracy.Mendonca  ";
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Equal("aracy.mendonca", username);
        Assert.True(userService.IsAuthenticated);
    }

    [Fact]
    public void Username_WhenFluigSendsCodeAndDisplayName_PrefersUserCode()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Code"] = "aracy.mendonca";
        httpContext.Request.Headers["X-User-Name"] = "Aracy Mendonça";
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Equal("aracy.mendonca", username);
    }

    [Fact]
    public void IsAuthenticated_WhenNotAuthenticated_ReturnsFalse()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var isAuthenticated = userService.IsAuthenticated;

        // Assert
        Assert.False(isAuthenticated);
    }

    [Fact]
    public void Username_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        httpContextAccessor.HttpContext = null;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Null(username);
    }

    [Fact]
    public void IsAuthenticated_WhenHttpContextIsNull_ReturnsFalse()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        httpContextAccessor.HttpContext = null;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var isAuthenticated = userService.IsAuthenticated;

        // Assert
        Assert.False(isAuthenticated);
    }

    [Fact]
    public void Username_WhenUserIsNull_ReturnsNull()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var username = userService.Username;

        // Assert
        Assert.Null(username);
    }

    [Fact]
    public void IsAuthenticated_WhenUserIsNull_ReturnsFalse()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;
        httpContextAccessor.HttpContext = httpContext;

        var userService = new CurrentUserService(httpContextAccessor);

        // Act
        var isAuthenticated = userService.IsAuthenticated;

        // Assert
        Assert.False(isAuthenticated);
    }
}

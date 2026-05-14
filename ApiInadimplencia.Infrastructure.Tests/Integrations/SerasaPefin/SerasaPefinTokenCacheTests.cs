using ApiInadimplencia.Infrastructure.Integrations.SerasaPefin;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Integrations.SerasaPefin;

public class SerasaPefinTokenCacheTests
{
    [Fact]
    public void GetToken_ShouldReturnCachedToken_WhenTokenIsNotExpired()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        var expectedToken = "cached-token-123";
        
        // Act - Set initial token
        cache.SetToken(expectedToken, TimeSpan.FromMinutes(5));
        
        // Assert - Token should be returned from cache
        var token = cache.GetToken();
        Assert.Equal(expectedToken, token);
    }

    [Fact]
    public void GetToken_ShouldReturnNull_WhenCacheIsEmpty()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        
        // Act
        var token = cache.GetToken();
        
        // Assert
        Assert.Null(token);
    }

    [Fact]
    public void GetToken_ShouldReturnNull_WhenTokenIsExpired()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        cache.SetToken("expired-token", TimeSpan.FromMilliseconds(100));
        
        // Act - Wait for token to expire
        Thread.Sleep(150);
        var token = cache.GetToken();
        
        // Assert
        Assert.Null(token);
    }

    [Fact]
    public void GetToken_ShouldReturnNull_WhenTokenIsWithinBuffer()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache(bufferSeconds: 60);
        cache.SetToken("token-near-expiry", TimeSpan.FromSeconds(30));
        
        // Act - Token should be considered expired due to 60s buffer
        var token = cache.GetToken();
        
        // Assert
        Assert.Null(token);
    }

    [Fact]
    public void SetToken_ShouldOverwriteExistingToken()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        cache.SetToken("old-token", TimeSpan.FromMinutes(5));
        
        // Act
        cache.SetToken("new-token", TimeSpan.FromMinutes(5));
        var token = cache.GetToken();
        
        // Assert
        Assert.Equal("new-token", token);
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenTokenIsExpired()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        cache.SetToken("token", TimeSpan.FromMilliseconds(100));
        
        // Act - Wait for token to expire
        Thread.Sleep(150);
        
        // Assert
        Assert.True(cache.IsExpired());
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenCacheIsEmpty()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        
        // Act & Assert
        Assert.True(cache.IsExpired());
    }

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenTokenIsValid()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        cache.SetToken("valid-token", TimeSpan.FromMinutes(5));
        
        // Act & Assert
        Assert.False(cache.IsExpired());
    }

    [Fact]
    public void IsExpired_ShouldConsiderBuffer_WhenTokenIsNearExpiry()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache(bufferSeconds: 60);
        cache.SetToken("token", TimeSpan.FromSeconds(30));
        
        // Act & Assert - Token should be considered expired due to buffer
        Assert.True(cache.IsExpired());
    }

    [Fact]
    public void Clear_ShouldRemoveCachedToken()
    {
        // Arrange
        var cache = new SerasaPefinTokenCache();
        cache.SetToken("token-to-clear", TimeSpan.FromMinutes(5));
        
        // Act
        cache.Clear();
        var token = cache.GetToken();
        
        // Assert
        Assert.Null(token);
    }
}

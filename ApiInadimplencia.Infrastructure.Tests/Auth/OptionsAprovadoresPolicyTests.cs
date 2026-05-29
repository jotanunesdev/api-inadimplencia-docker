using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Infrastructure.Auth;
using ApiInadimplencia.Application.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Auth;

public class OptionsAprovadoresPolicyTests
{
    [Fact]
    public void IsAprovador_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new NegativacaoOptions
        {
            UsuariosAprovadores = new[] { "aracy.mendoca", "adriano.oliveira" }
        });
        var policy = new OptionsAprovadoresPolicy(options);

        // Act & Assert
        Assert.True(policy.IsAprovador("aracy.mendoca"));
        Assert.True(policy.IsAprovador("ARACY.MENDOCA"));
        Assert.True(policy.IsAprovador("Aracy.Mendoca"));
        Assert.True(policy.IsAprovador("adriano.oliveira"));
        Assert.True(policy.IsAprovador("ADRIANO.OLIVEIRA"));
    }

    [Fact]
    public void IsAprovador_NullOrEmptyUsername_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new NegativacaoOptions
        {
            UsuariosAprovadores = new[] { "aracy.mendoca", "adriano.oliveira" }
        });
        var policy = new OptionsAprovadoresPolicy(options);

        // Act & Assert
        Assert.False(policy.IsAprovador(null));
        Assert.False(policy.IsAprovador(""));
        Assert.False(policy.IsAprovador("   "));
        Assert.False(policy.IsAprovador(string.Empty));
    }

    [Fact]
    public void IsAprovador_NonAprovador_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new NegativacaoOptions
        {
            UsuariosAprovadores = new[] { "aracy.mendoca", "adriano.oliveira" }
        });
        var policy = new OptionsAprovadoresPolicy(options);

        // Act & Assert
        Assert.False(policy.IsAprovador("joao"));
        Assert.False(policy.IsAprovador("maria"));
        Assert.False(policy.IsAprovador("aracy.mendoca.fake"));
    }

    [Fact]
    public void ListAprovadores_ReturnsImmutableList()
    {
        // Arrange
        var options = Options.Create(new NegativacaoOptions
        {
            UsuariosAprovadores = new[] { "aracy.mendoca", "adriano.oliveira" }
        });
        var policy = new OptionsAprovadoresPolicy(options);

        // Act
        var aprovadores = policy.ListAprovadores();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<string>>(aprovadores);
        Assert.Equal(2, aprovadores.Count);
        Assert.Equal("aracy.mendoca", aprovadores[0]);
        Assert.Equal("adriano.oliveira", aprovadores[1]);
    }

    [Fact]
    public void ListAprovadores_TrimsWhitespace()
    {
        // Arrange
        var options = Options.Create(new NegativacaoOptions
        {
            UsuariosAprovadores = new[] { "  aracy.mendoca  ", "  adriano.oliveira  ", "", "   " }
        });
        var policy = new OptionsAprovadoresPolicy(options);

        // Act
        var aprovadores = policy.ListAprovadores();

        // Assert
        Assert.Equal(2, aprovadores.Count);
        Assert.Equal("aracy.mendoca", aprovadores[0]);
        Assert.Equal("adriano.oliveira", aprovadores[1]);
    }

    [Fact]
    public void ListAprovadores_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var options = Options.Create(new NegativacaoOptions
        {
            UsuariosAprovadores = Array.Empty<string>()
        });
        var policy = new OptionsAprovadoresPolicy(options);

        // Act
        var aprovadores = policy.ListAprovadores();

        // Assert
        Assert.Equal(0, aprovadores.Count);
    }
}

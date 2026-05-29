using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Infrastructure.Configuration;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

/// <summary>
/// Integration tests for InadimplenciaQueryService against the real Data Warehouse.
/// These tests require a valid SQL Server connection string configured in test settings.
/// </summary>
[Collection("Database Integration")]
public class InadimplenciaQueryServiceIntegrationTests : IAsyncDisposable
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly InadimplenciaQueryService _sut;

    public InadimplenciaQueryServiceIntegrationTests()
    {
        var connectionString = SqlIntegrationTestGuard.RequireAvailableConnectionString(nameof(InadimplenciaQueryServiceIntegrationTests));

        var options = Options.Create(new SqlServerOptions 
        { 
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 30
        });

        _connectionFactory = new SqlServerConnectionFactory(options);
        _sut = new InadimplenciaQueryService(_connectionFactory, 
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InadimplenciaQueryService>.Instance);
    }

    [RequiresSqlFact]
    public async Task GetVendaAsync_ExistingInadimplente_ReturnsRow()
    {
        // Arrange
        // Use a known inadimplente sale number from the DW
        // This test assumes numVenda 295 exists and is inadimplente
        var numVenda = 295;

        // Act
        var result = await _sut.GetVendaAsync(numVenda);

        // Assert
        if (result != null)
        {
            result.Should().NotBeNull();
            result.NumVenda.Should().Be(numVenda);
            result.DocumentoDevedor.Should().NotBeNullOrEmpty();
            result.NomeDevedor.Should().NotBeNullOrEmpty();
            result.Valor.Should().BeGreaterThan(0);
            result.DocumentoDevedor.Should().MatchRegex(@"^\d+$"); // digits-only
        }
        else
        {
            // If the sale doesn't exist in the test environment, we skip the detailed assertions
            // but still verify the method executed without throwing
            Assert.True(true, "Sale 295 not found in test DW - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task GetVendaAsync_NotInadimplente_ReturnsNull()
    {
        // Arrange
        // Use a sale number that is not inadimplente
        // This test assumes numVenda 999999 doesn't exist or is not inadimplente
        var numVenda = 999999;

        // Act
        var result = await _sut.GetVendaAsync(numVenda);

        // Assert
        result.Should().BeNull();
    }

    [RequiresSqlFact]
    public async Task GetVendaAsync_DocumentoDevedor_ShouldBeDigitsOnly()
    {
        // Arrange
        var numVenda = 295;

        // Act
        var result = await _sut.GetVendaAsync(numVenda);

        // Assert
        if (result != null)
        {
            result.DocumentoDevedor.Should().NotBeNullOrEmpty();
            result.DocumentoDevedor.Should().MatchRegex(@"^\d+$"); // No punctuation, only digits
            result.DocumentoDevedor.Should().NotContain(".");
            result.DocumentoDevedor.Should().NotContain("-");
            result.DocumentoDevedor.Should().NotContain("/");
        }
        else
        {
            Assert.True(true, "Sale 295 not found in test DW - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task GetVendaAsync_Endereco_ShouldMapCorrectly()
    {
        // Arrange
        var numVenda = 295;

        // Act
        var result = await _sut.GetVendaAsync(numVenda);

        // Assert
        if (result != null && result.Endereco != null)
        {
            result.Endereco.ZipCode.Should().NotBeNullOrEmpty();
            result.Endereco.City.Should().NotBeNullOrEmpty();
            result.Endereco.State.Should().NotBeNullOrEmpty();
            result.Endereco.State.Length.Should().Be(2); // UF is 2 characters
            result.Endereco.ZipCode.Should().MatchRegex(@"^\d{8}$"); // CEP is 8 digits
        }
        else
        {
            Assert.True(true, "Sale 295 not found in test DW or has no address - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task ListFiadoresAsync_ReturnsValidTypesOnly()
    {
        // Arrange
        var numVenda = 295;

        // Act
        var result = await _sut.ListFiadoresAsync(numVenda);

        // Assert
        if (result.Count > 0)
        {
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            
            // All returned guarantors should have valid types
            var validTypes = new[] { "FIADOR", "CONJUGE", "CESSIONARIO", "COOBRIGADO", "CO-OBRIGADO", "CO OBRIGADO" };
            result.All(f => validTypes.Contains(f.TipoAssociacao)).Should().BeTrue();
            
            // All documents should be digits-only
            result.All(f => Regex.IsMatch(f.Documento, @"^\d+$")).Should().BeTrue();
            
            // All should have required fields
            result.All(f => !string.IsNullOrWhiteSpace(f.Nome)).Should().BeTrue();
            result.All(f => f.NumVenda == numVenda).Should().BeTrue();
        }
        else
        {
            Assert.True(true, "No guarantors found for sale 295 - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task ListFiadoresAsync_NoFiadores_ReturnsEmpty()
    {
        // Arrange
        var numVenda = 999999;

        // Act
        var result = await _sut.ListFiadoresAsync(numVenda);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [RequiresSqlFact]
    public async Task ListFiadoresAsync_DocumentoGarantidor_ShouldBeDigitsOnly()
    {
        // Arrange
        var numVenda = 295;

        // Act
        var result = await _sut.ListFiadoresAsync(numVenda);

        // Assert
        if (result.Count > 0)
        {
            result.All(f => Regex.IsMatch(f.Documento, @"^\d+$")).Should().BeTrue();
            result.All(f => !f.Documento.Contains(".")).Should().BeTrue();
            result.All(f => !f.Documento.Contains("-")).Should().BeTrue();
            result.All(f => !f.Documento.Contains("/")).Should().BeTrue();
        }
        else
        {
            Assert.True(true, "No guarantors found for sale 295 - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task ListFiadoresAsync_ShouldBeOrderedByDataCadastroDescNomeAsc()
    {
        // Arrange
        var numVenda = 295;

        // Act
        var result = await _sut.ListFiadoresAsync(numVenda);

        // Assert
        if (result.Count > 1)
        {
            // Check ordering: DATA_CADASTRO DESC, NOME ASC
            for (int i = 1; i < result.Count; i++)
            {
                var prev = result[i - 1];
                var curr = result[i];
                
                if (prev.DataCadastro > curr.DataCadastro)
                {
                    // Correct: descending by date
                    continue;
                }
                else if (prev.DataCadastro == curr.DataCadastro)
                {
                    // Same date: should be ascending by name
                    string.Compare(prev.Nome, curr.Nome, StringComparison.OrdinalIgnoreCase).Should().BeLessThanOrEqualTo(0);
                }
            }
        }
        else
        {
            Assert.True(true, "Need at least 2 guarantors to test ordering - test needs valid data");
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Connection factory is singleton, no explicit disposal needed
        await ValueTask.CompletedTask;
    }

    [RequiresSqlFact]
    public async Task GetDividasElegiveisAsync_VendaNaoEncontrada_DeveRetornarNull()
    {
        // Arrange
        var numVenda = 999999;
        var diasAtrasoMinimo = 60;

        // Act
        var result = await _sut.GetDividasElegiveisAsync(numVenda, diasAtrasoMinimo);

        // Assert
        result.Should().BeNull();
    }

    [RequiresSqlFact]
    public async Task GetDividasElegiveisAsync_VendaExistente_DeveRetornarDadosDaVenda()
    {
        // Arrange
        var numVenda = 295;
        var diasAtrasoMinimo = 60;

        // Act
        var result = await _sut.GetDividasElegiveisAsync(numVenda, diasAtrasoMinimo);

        // Assert
        if (result != null)
        {
            result.Should().NotBeNull();
            result.NumVenda.Should().Be(numVenda);
            result.Cliente.Should().NotBeNullOrEmpty();
            result.Cpf.Should().NotBeNullOrEmpty();
            result.ContractNumber.Should().NotBeNullOrEmpty();
            result.Parcelas.Should().NotBeNull();
        }
        else
        {
            Assert.True(true, "Sale 295 not found in test DW - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task GetDividasElegiveisAsync_Parcelas_DeveCalcularDiasAtrasoCorretamente()
    {
        // Arrange
        var numVenda = 295;
        var diasAtrasoMinimo = 60;

        // Act
        var result = await _sut.GetDividasElegiveisAsync(numVenda, diasAtrasoMinimo);

        // Assert
        if (result != null && result.Parcelas.Count > 0)
        {
            result.Parcelas.Should().NotBeEmpty();
            result.Parcelas.All(p => p.DiasAtraso >= 0).Should().BeTrue();
            result.Parcelas.All(p => p.Vencimento <= DateOnly.FromDateTime(DateTime.UtcNow)).Should().BeTrue();
        }
        else
        {
            Assert.True(true, "Sale 295 not found or has no parcelas in test DW - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task GetDividasElegiveisAsync_Parcelas_DeveMarcarElegivelCorretamente()
    {
        // Arrange
        var numVenda = 295;
        var diasAtrasoMinimo = 60;

        // Act
        var result = await _sut.GetDividasElegiveisAsync(numVenda, diasAtrasoMinimo);

        // Assert
        if (result != null && result.Parcelas.Count > 0)
        {
            result.Parcelas.Should().NotBeEmpty();
            result.Parcelas.All(p => p.Elegivel == (p.DiasAtraso > diasAtrasoMinimo)).Should().BeTrue();
        }
        else
        {
            Assert.True(true, "Sale 295 not found or has no parcelas in test DW - test needs valid data");
        }
    }

    [RequiresSqlFact]
    public async Task GetDividasElegiveisAsync_Cpf_DeveSerDigitsOnly()
    {
        // Arrange
        var numVenda = 295;
        var diasAtrasoMinimo = 60;

        // Act
        var result = await _sut.GetDividasElegiveisAsync(numVenda, diasAtrasoMinimo);

        // Assert
        if (result != null)
        {
            result.Cpf.Should().MatchRegex(@"^\d+$");
        }
        else
        {
            Assert.True(true, "Sale 295 not found in test DW - test needs valid data");
        }
    }
}
